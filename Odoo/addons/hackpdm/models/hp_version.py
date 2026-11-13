import io
import os
from odoo import models, fields, api, Command
from odoo.exceptions import AccessError, UserError, ValidationError
import hashlib as hash
import base64
import logging
import urllib.parse
import magic

from . import hp_guard
from . import hp_legacy

_logger = logging.getLogger(__name__)

# a version is a snapshot: once created only an administrator may swap its file or its entry
# what a commit fixed: the file, which entry it belongs to, the computer that committed it, what it
# references and its properties. Only an administrator changes these (a permanent delete clears the links).
_IMMUTABLE_VERSION_FIELDS = frozenset(
    ("entry_id", "file_contents", "attachment_id", "node_id", "child_ids", "parent_ids", "version_property_ids"))
_CHECKOUT_VERSION_FIELDS = frozenset(("checkout_user", "checkout_date", "checkout_node"))


class hp_version(models.Model):

    _name = 'hp.version'
    _description = 'hp version'
    _inherit = 'hp.common.model'

    name = fields.Char(
        related='entry_id.name',
        string='file name',
    )
    checksum = fields.Char(
        related='attachment_id.checksum',
        string='sha checksum',
    )
    file_ext = fields.Char(
        related='entry_id.type_id.file_ext',
        string='file extension',
    )
    directory_complete_name = fields.Char(
        related='entry_id.directory_complete_name',
        string='directory folder pathway',
    )
    windows_complete_name = fields.Char(
        related='entry_id.windows_complete_name',
        string='windows directory path name',
    )

    # the checkout is kept on the version (the entry shows its latest version's); set by checkout_entry
    checkout_date = fields.Datetime(
        string='checkout date',
    )
    file_modify_stamp = fields.Datetime(
        string='modified date',
        default=lambda self:fields.Datetime.now(),
        #required=True,
    )

    file_size = fields.Integer(
        related='attachment_id.file_size',
        string='file size',
        store=True,
    )

    preview_image = fields.Image('preview image')

    file_contents = fields.Binary(
        string='file contents',
        attachment=True,
    )

    deleted = fields.Boolean(
        related='entry_id.deleted',
        string='entry deleted',
    )
    is_checked_out = fields.Boolean(
        related='entry_id.is_checked_out',
        string='is checked out',
    )
    is_checkout_version = fields.Boolean(
        compute='_compute_is_checkout_version',
        string='if entry is checked out, is this the checked out version?',
    )
    is_latest_version = fields.Boolean(
        compute='_compute_is_latest_version',
        string='is this the latest version?',
    )

    dir_id = fields.Many2one(
        comodel_name='hp.directory',
        related='entry_id.dir_id',
        string='directory',
    )
    entry_id = fields.Many2one(
        comodel_name='hp.entry',
        string='entry'
    )
    node_id = fields.Many2one(
        comodel_name='hp.node',
        string='node'
    )
    checkout_user = fields.Many2one(
        comodel_name='res.users',
        index=True,
        string='user that checked out entry'
    )
    checkout_node = fields.Many2one(
        comodel_name='hp.node',
        string='node that has checked out entry'
    )
    attachment_id = fields.Many2one(
        comodel_name='ir.attachment',
        compute='_compute_attachment',
        store=True,
        string='attachment',
    )
    release_id = fields.Many2one(
        comodel_name='hp.release',
    )

    attachment_ids = fields.One2many(
        comodel_name='ir.attachment',
        inverse_name='res_id',
        domain=[('res_model', '=', 'hp.version'), ('type', '=', 'binary')],
        auto_join=True,
        string="Documents",
    )
    parent_ids = fields.Many2many(
        comodel_name='hp.version',
        relation='hp_version_relationship',
        column1='child_id',
        column2='parent_id',
        string='parent versions',
    )
    child_ids = fields.Many2many(
        comodel_name='hp.version',
        relation='hp_version_relationship',
        column1='parent_id',
        column2='child_id',
        string='child versions',
    )
    version_property_ids = fields.One2many(
        comodel_name='hp.version.property',
        inverse_name='version_id',
        string='version properties',
    )

    node_latest_ids = fields.Many2many(
        comodel_name='hp.node',
    )

    @api.model_create_multi
    def create(self, vals_list):
        """Creates versions; for regular users, only of files they may add a version to.

        The first version of a new file needs no checkout; every later one needs the file checked out
        to the caller. A version cannot be created already checked out (use :meth:`checkout_entry`).
        Commits go through here too.

        Args:
            vals_list (list[dict]): One dict per version: entry_id (int), file_contents (str, base64),
                node_id (int, the committing computer), and other version fields.

        Returns:
            hp.version: The new versions.

        Raises:
            UserError: The file does not exist, is deleted, is checked out to another user, or is not
                checked out to the caller.
            AccessError: Checkout fields were given.
        """
        if not hp_guard.is_pdm_admin(self.env):
            for vals in vals_list:
                if any(vals.get(f) for f in _CHECKOUT_VERSION_FIELDS):
                    raise AccessError("A new version cannot be created checked out; use checkout_entry.")
                self._check_entry_accepts_version(vals.get("entry_id"))
        return super().create(vals_list)

    def _check_entry_accepts_version(self, entry_id):
        """A new version may not be added to a deleted entry, or to one another user has checked out."""
        if not entry_id:
            return
        entry = self.env["hp.entry"].browse(entry_id).exists()
        if not entry:
            raise UserError(f"Entry {entry_id} does not exist.")
        if entry.deleted:
            raise UserError(f"Cannot add a version: '{entry.name}' is deleted.")
        if entry.checkout_user and entry.checkout_user.id != self.env.uid:
            raise UserError(
                f"Cannot add a version: '{entry.name}' is checked out to {entry.checkout_user.name}.")
        # the first version comes with the new file; every later one needs the file checked out
        if entry.version_ids and not entry.checkout_user:
            raise UserError(f"Cannot add a version: '{entry.name}' is not checked out to you.")

    def write(self, vals):
        """Writes version fields, keeping committed versions fixed for regular users.

        A regular user may not change a version's file, entry, committing node, dependency links or
        properties (only an administrator can, e.g. for a permanent delete). Checkout fields follow the
        checkout rules: check out only in your own name, only the holder changes or releases a
        checkout, and a deleted file cannot be checked out.

        Args:
            vals (dict): Field values to write.

        Returns:
            bool: True.

        Raises:
            AccessError: A fixed field was written, or a checkout in someone else's name.
            UserError: The checkout belongs to another user, or the file is deleted.
        """
        if not hp_guard.is_pdm_admin(self.env):
            locked = _IMMUTABLE_VERSION_FIELDS.intersection(vals)
            if locked:
                raise AccessError(
                    f"Committed versions cannot be changed ({', '.join(sorted(locked))}); commit a new version instead.")
            if _CHECKOUT_VERSION_FIELDS.intersection(vals):
                self._check_checkout_write(vals)
        return super().write(vals)

    def _check_checkout_write(self, vals):
        """The checkout (now kept here) follows the entry's rules: a user checks out only in their own
        name, and only the holder (or an administrator) changes or releases a checkout."""
        new_holder = vals.get("checkout_user")
        if isinstance(new_holder, (list, tuple)):
            new_holder = new_holder[0] if new_holder else False
        if new_holder and new_holder != self.env.uid:
            raise AccessError("You can only check entries out to yourself.")
        for rec in self:
            if rec.checkout_user and rec.checkout_user.id != self.env.uid:
                raise UserError(
                    f"'{rec.entry_id.name}' is checked out to {rec.checkout_user.name}; only they or an administrator can change it.")
            if new_holder and rec.entry_id.deleted:
                raise UserError(f"'{rec.entry_id.name}' is deleted and cannot be checked out.")

    @api.constrains("child_ids", "parent_ids")
    def _check_not_own_dependency(self):
        for rec in self:
            if rec in rec.child_ids or rec in rec.parent_ids:
                raise ValidationError(f"Version {rec.id} cannot depend on itself.")

    @api.depends('checkout_user', 'entry_id.version_ids')
    def _compute_is_checkout_version(self):
        for record in self:
            record.is_checkout_version = bool(record.checkout_user) and record.id == record.entry_id.latest_version_id.id

    @api.depends('entry_id.latest_version_id')
    def _compute_is_latest_version(self):
        for record in self:
            record.is_latest_version = record.entry_id.latest_version_id.id == record.id

    @api.depends('file_contents')
    def _compute_attachment(self):
        for rec in self:
            rec.attachment_id = self.env['ir.attachment'].search([
                ('res_model', '=', rec._name),
                ('res_id', '=', rec.id),
                ('res_field', '=', 'file_contents')
            ], limit=1)

    @api.depends('attachment_id')
    def _compute_md5sum(self):
        for record in self:

            attachment = self.attachment_id
            if attachment:
                record.md5sum = attachment.checksum
            else:
                record.md5sum = False


    @api.model
    def get_recursive_dependency(self, version_ids):
        """Returns the given versions and every version they reference, recursively.

        Follows the parent→child dependency links in SQL, skipping children whose file is deleted, and
        stops on cycles. Pending ORM changes are written first so links made in the same transaction
        count.

        Args:
            version_ids (int | list[int] | Iterable[int]): Starting hp.version ids.

        Returns:
            set[int] | list: The starting ids plus all dependency version ids; ``[]`` when none were
            given or the query fails (the error is logged).
        """
        if not version_ids:
            return []
        if isinstance(version_ids, int):
            version_ids = [version_ids]
        elif not isinstance(version_ids, list):
            version_ids = list(version_ids)

        # using raw sql for efficient recursive dependency traversal
        # this query finds all version IDs (initial + children + grandchildren, etc.)
        query_versions = """
            WITH RECURSIVE dependency_versions (version_id) AS (
                SELECT id FROM hp_version WHERE id = ANY(%s) -- start with initial versions
            UNION -- Use UNION ALL if cycles are impossible or handled; UNION removes duplicates earlier
                SELECT c.child_id
                FROM dependency_versions p, hp_version_relationship c
                JOIN hp_version vc ON vc.id = c.child_id
                JOIN hp_entry ec ON ec.id = vc.entry_id
                WHERE c.parent_id = p.version_id AND ec.deleted = false -- Recursively find children of non-deleted entries
            )
            SELECT DISTINCT version_id FROM dependency_versions;
        """
        # the query reads the tables directly: write pending ORM changes (links, deleted flags) first
        self.env.flush_all()
        try:
            self.env.cr.execute(query_versions, (version_ids,))
            version_results = self.env.cr.fetchall()
            all_dep_version_ids = {row[0] for row in version_results}

            if not all_dep_version_ids:
                return []
            # Now get unique entry_ids for these versions using the ORM for simplicity
            return all_dep_version_ids

        except Exception as e:
            _logger.error(f"Error fetching recursive dependencies for versions {version_ids}: {e}")
            return []

    @api.model
    def get_recursive_dependency_versions(self, version_ids):
        """Same as :meth:`get_recursive_dependency`, as a list (for RPC callers).

        Args:
            version_ids (int | list[int] | Iterable[int]): Starting hp.version ids.

        Returns:
            list[int]: The starting ids plus all dependency version ids; ``[]`` on no input or error.
        """
        try:
            if not version_ids:
                return []
            if isinstance(version_ids, int):
                version_ids = [version_ids]
            elif not isinstance(version_ids, list):
                version_ids = list(version_ids)

            complete_version_ids = self.get_recursive_dependency(version_ids)
            return list(complete_version_ids)
        except Exception as e:
            _logger.error(f"Error fetching recursive dependencies for versions {version_ids}: {e}")
            return []
    @api.model
    def get_recursive_dependency_entries(self, version_ids, node_id=False, update=False):
        """Returns the files (entries) of the given versions and everything they depend on.

        The client uses this to widen Get Latest, checkout and undo checkout to an assembly's parts.

        Args:
            version_ids (list[int]): Starting hp.version ids (usually the files' latest versions).
            node_id (int | bool, optional): hp.node id whose "latest versions" list is replaced when
                ``update`` is true. Defaults to False.
            update (bool, optional): Also store the found versions on that node. Only administrators may
                write nodes; for anyone else the write fails and an empty list is returned. Defaults to
                False.

        Returns:
            list[int]: Unique hp.entry ids; ``[]`` when nothing was found or anything failed (logged).
        """
        try:
            complete_version_ids = self.get_recursive_dependency_versions(version_ids)
            if len(complete_version_ids) < 1:
                return []

            if update and node_id:
                v_ids = list(complete_version_ids) + version_ids
                node = self.env["hp.node"].browse([node_id])
                node.write({"node_latest_ids": [(Command.SET, 0, v_ids)]})
            # '.ids' gives a list of unique IDs
            # Now get unique entry_ids for these versions using the ORM for simplicity
            versions = self.env['hp.version'].browse(list(complete_version_ids))
            entry_ids = versions.mapped('entry_id').ids
            return entry_ids
        except Exception as e:
            _logger.error(f"Error fetching recursive dependencies for versions {version_ids}: {e}")
            return []

    @api.model
    def get_entries_from_children(self, entryID):
        """Meant to return the file ids an entry's latest version depends on.

        Not usable as written: it adds the starting entry's id on every step, and reads ``child_id``
        from the child versions, which raises AttributeError as soon as the version has a dependency.
        Use :meth:`get_recursive_dependency_entries`.

        Args:
            entryID (int): The hp.entry id.

        Returns:
            set[int] | None: The starting entry's id, or None.

        Raises:
            IndexError: No entry has that id.
            AttributeError: The latest version has dependencies.
        """
        entry_ids = set()
        entry = self.env["hp.entry"].search([('id', '=', entryID)])[0]
        if entry:
            return self._iterative_add_entries(entry.latest_version_id)
        return None

    def _iterative_add_entries(self, version):
        stack = [version]
        entry_ids = set()

        while stack:
            current_version = stack.pop()
            if current_version.entry_id.id in entry_ids:
                continue

            entry_ids.add(version.entry_id.id)

            if current_version.child_ids:
                stack.extend(v.child_id for v in current_version.child_ids)
        if len(entry_ids) > 0:
            return entry_ids
        return None

    @api.model
    def read_contents(self, version_id, offset=0, length=None):
        """Reads part of a version's file, so a large file can be downloaded in pieces.

        Args:
            version_id (int): The hp.version id.
            offset (int): Where to start reading, in bytes.
            length (int | None): How many bytes to read; at most 8 MiB (also when None).

        Returns:
            dict: data (str, the bytes base64-encoded; shorter than length at the end of the file),
            size (int, the whole file's size) and checksum (str, its SHA-1).

        Raises:
            AccessError: The caller may not read the version.
            UserError: The version does not exist, or offset / length are invalid.
        """
        from .hp_upload import MAX_CHUNK_BYTES
        if not isinstance(version_id, int) or isinstance(version_id, bool):
            raise UserError(f"read_contents expects a version id, got {version_id!r}")
        if not isinstance(offset, int) or offset < 0:
            raise UserError(f"read_contents: invalid offset {offset!r}")
        if length is None:
            length = MAX_CHUNK_BYTES
        if not isinstance(length, int) or length < 0:
            raise UserError(f"read_contents: invalid length {length!r}")
        length = min(length, MAX_CHUNK_BYTES)
        version = self.browse(version_id).exists()
        if not version:
            raise UserError(f"read_contents: version {version_id} does not exist")
        version.check_access_rights("read")
        version.check_access_rule("read")
        attachment = version.sudo().attachment_id
        if not attachment:
            return {"data": "", "size": 0, "checksum": ""}
        if attachment.store_fname:
            with open(attachment._full_path(attachment.store_fname), "rb") as stored:
                stored.seek(offset)
                piece = stored.read(length)
        else:
            piece = (attachment.raw or b"")[offset:offset + length]
        return {
            "data": base64.b64encode(piece).decode(),
            "size": attachment.file_size,
            "checksum": attachment.checksum or "",
        }

    @api.model
    def _create_attachment(self, file_contents:bytes, field_name:str):
        file_name = f"{self.id}.{self.name}"
        file_contents_b64 = base64.b64encode(file_contents).decode('utf-8')
        mime_type = magic.Magic(mime=True).from_buffer(file_contents)

        values = {
            'name': file_name,
            'type': 'binary',
            'datas': file_contents_b64,
            'res_model': self._name,
            'res_field': field_name,
            'res_id': self.id,
            'mimetype': mime_type,
        }
        try:
            attachment = self.env['ir.attachment'].create(values)
            return attachment
        except Exception as e:
            logging.error(e)
            return None

    @api.model
    def _get_attachments(self, record, field_name):
        attachment_model = self.env['ir.attachment']
        attachments = attachment_model.search([
            ('res_id', '=', record.id),
            ('res_field', '=', field_name),
        ])
        return attachments

    @api.model
    def checkout_entry(self, version_id, node):
        """Checks a file out to the caller on the given computer.

        The checkout (user, date, node) is written to the file's latest version, whichever of its
        versions was passed. The version's node_id (the computer that committed it) is left alone.
        Checking out a file you already hold refreshes the date and node.

        Args:
            version_id (int): Any hp.version id of the file to check out.
            node (int | str): The caller's computer, as an hp.node id or name.

        Returns:
            bool: True.

        Raises:
            UserError: ``version_id`` is not an int or does not exist, the node is not found, the file
                is deleted, or it is checked out to another user.
        """
        if not isinstance(version_id, int):
            raise UserError(f"checkout_entry expects a version id, got {version_id!r}")
        node_rec = self.env["hp.node"]
        if isinstance(node, int):
            node_rec = self.env["hp.node"].browse(node).exists()
        elif isinstance(node, str):
            node_rec = self.env["hp.node"].search([("name", "=", node)], limit=1)
        if not node_rec:
            raise UserError(f"checkout_entry: node {node!r} not found")
        node_rec.ensure_one()
        v_model = self.env["hp.version"]
        v_rec = v_model.browse(version_id).exists()
        if not v_rec:
            raise UserError(f"checkout_entry: version {version_id} does not exist")
        
        entry = v_rec.entry_id
        date = fields.Datetime.now()
        
        # check out only if it isn't checked out or if same user
        if (not entry.checkout_user or entry.checkout_user.id == self.env.uid) and entry.deleted != True:
            # kept on the latest version (the one the entry shows), whichever version was asked for
            latest = entry.latest_version_id or v_rec
            latest.write({
                "checkout_user": self.env.uid,
                "checkout_date": date,
                "checkout_node": node_rec.id,
            })
            # node_id is the computer that committed the version; a checkout does not change it
        elif entry.deleted:
            raise UserError(f"'{entry.name}' is deleted and cannot be checked out")
        else:
            raise UserError(f"'{entry.name}' is already checked out to {entry.checkout_user.name}")
        return True

    @api.model
    def checkout_entries(self, version_ids, node):
        """Checks out several files with :meth:`checkout_entry`, all or nothing.

        If one fails, the RPC call fails and none of the checkouts are kept.

        Args:
            version_ids (list[int]): hp.version ids, one per file.
            node (int | str): The caller's computer, as an hp.node id or name.

        Returns:
            bool: True.

        Raises:
            UserError: As for :meth:`checkout_entry`, for the first file that cannot be checked out.
        """
        for version_id in version_ids:
            self.checkout_entry(version_id, node)
        return True

    @api.model
    @api.depends('entry_id', 'file_ext', 'name')
    def migrate_webdav_files_to_records(self):
        """One-off migration: downloads each version's file from the old WebDAV server into an attachment.

        Administrators only. Versions that already have a file are skipped; commits after every
        version. The WebDAV address and login come from the ``hackpdm.legacy_webdav_*`` system
        parameters (see ``hp_legacy``).

        Args:
            None.

        Returns:
            None: nothing.

        Raises:
            AccessError: The caller is not an administrator.
            UserError: A ``hackpdm.legacy_webdav_*`` system parameter is not set.
        """
        hp_guard.require_admin(self.env, "migrate_webdav_files_to_records")
        web_dav = hp_legacy.LegacyWebDav(self.env)
        all_records = self.env[self._name].search([])

        for record in all_records:
            attachment = record._get_attachments(record, "file_contents")
            if attachment and len(attachment)>0:
                logging.info(f"version record {attachment[0].res_id} has attachment id {attachment[0].id} named: {attachment[0].name}")
                continue
            file_contents = web_dav.get_file_from_webdav(record.entry_id.id, record.id, record.file_ext)
            if file_contents != b'':
                try:
                    attachment = record._create_attachment(file_contents, "file_contents")
                except:
                    logging.warning(f"didn't create attachment {record.id}.{record.name}")
                if (attachment != None):
                    logging.info(f"attachment id {attachment.id} created for version record {attachment.res_id} named: {attachment.name}")
            else:
                logging.warning(f"didn't create attachment {record.id}.{record.name}")
            self.env.cr.commit()


    @api.model
    @api.depends('name')
    def migrate_image_from_database(self):
        """One-off migration: copies preview images from the old HackPDM database into attachments.

        Administrators only. Versions that already have a preview are skipped; commits after every
        version. The old database comes from the ``hackpdm.legacy_db_url`` system parameter (see
        ``hp_legacy``).

        Args:
            None.

        Returns:
            None: nothing.

        Raises:
            AccessError: The caller is not an administrator.
            UserError: The ``hackpdm.legacy_db_url`` system parameter is not set.
        """
        hp_guard.require_admin(self.env, "migrate_image_from_database")
        hackpdm = hp_legacy.LegacyDatabase(self.env)
        all_records = self.env[self._name].search([])

        for record in all_records:
            attachment = record._get_attachments(record, "preview_image")
            if attachment and len(attachment)>0:
                logging.info(f"version record {attachment[0].res_id} has preview_image")
                continue

            try:
                raw_image = hackpdm.preview_image(record.id)
                if raw_image:
                    attachment = record._create_attachment(raw_image, "preview_image")
                    if (attachment != None):
                        logging.info(f"attachment id {attachment.id} created for version record {attachment.res_id} named: {attachment.name}")
                    else:
                        logging.warning(f"didn't create preview_image attachment {record.id}.{record.name}")
                else:
                    logging.warning(f"empty preview_image in db")
            except Exception as e:
                logging.error(e)
            self.env.cr.commit()

    @api.model
    def _delete_all_records(self):
        all_records = self.env["ir.attachment"].search([("res_model", "=", "hp.version")])
        logging.info(f"deleting {len(all_records)} records linked to hp.version")
        all_records.unlink()
        logging.info(f"attachments deleted successfully")

    @api.model
    def display_all_fnames(self):
        """Logs every hp.version attachment's id, name, field and filestore path (for debugging).

        Administrators only.

        Args:
            None.

        Returns:
            None: nothing (the output goes to the server log).

        Raises:
            AccessError: The caller is not an administrator.
        """
        hp_guard.require_admin(self.env, "display_all_fnames")
        all_records = self.env["ir.attachment"].search([("res_model", "=", "hp.version")])
        for record in all_records:
            logging.info(f"attachment id = {record.id}\nname = {record.name}\nres_model = {record.res_model}\nres_id = {record.res_id}\nres_fields = {record.res_field}\nfname = {record.store_fname}\n")

    @api.model
    def cleanup_filestore(self):
        """Placeholder for a filestore cleanup job; only checks that the caller is an administrator.

        Args:
            None.

        Returns:
            None: nothing.

        Raises:
            AccessError: The caller is not an administrator.
        """
        hp_guard.require_admin(self.env, "cleanup_filestore")

class hp_version_property(models.Model):
    #base fields
    _name = 'hp.version.property'
    _description = 'hp version property'
    _inherit = 'hp.common.model'

    # a version's properties are part of what was committed: a commit creates them (with elevated
    # rights, after checking the version is one it just created); users cannot add, change or remove them
    @api.model_create_multi
    def create(self, vals_list):
        """Creates version properties; administrators only (a commit creates them with elevated rights).

        A version's properties are part of what was committed, so users cannot add them directly.

        Args:
            vals_list (list[dict]): One dict per property: version_id (int), prop_id (int),
                sw_config_name (str) and one of text_value, number_value, yesno_value, date_value.

        Returns:
            hp.version.property: The new properties.

        Raises:
            AccessError: The caller is not an administrator.
        """
        if not hp_guard.is_pdm_admin(self.env):
            raise AccessError("Version properties are set by committing a new version.")
        return super().create(vals_list)

    def write(self, vals):
        """Changes version properties; administrators only.

        Args:
            vals (dict): Field values to write.

        Returns:
            bool: True.

        Raises:
            AccessError: The caller is not an administrator.
        """
        if not hp_guard.is_pdm_admin(self.env):
            raise AccessError("Committed version properties cannot be changed.")
        return super().write(vals)

    #fields
    sw_config_name = fields.Char(string='solidworks config name')
    text_value = fields.Text(string='text value')
    number_value = fields.Float(string='number value')
    yesno_value = fields.Boolean(string='yes or no')
    date_value = fields.Datetime(
        string='time stamp',
    )
    prop_name = fields.Char(
        related='prop_id.name',
        string='property name'
    )

    #relational fields
    version_id = fields.Many2one(
        comodel_name='hp.version',
        string='version id',
    )
    prop_id = fields.Many2one(
        comodel_name='hp.property',
        string='property',
    )
    entry_id = fields.Many2one(
        comodel_name='hp.entry',
        related='version_id.entry_id',
        string='entry ID',
    )


# class hp_version_relationship(models.Model):
#     #base fields
#     _name = 'hp.version.relationship'
#     _description = 'hp version relationship'
#     _inherit = 'hp.common.model'

#     #fields

#     #relational fields
#     parent_id = fields.Many2one(
#         comodel_name='hp.version',
#         string='version parent',
#     )
#     child_id = fields.Many2one(
#         comodel_name='hp.version',
#         string='version child',
#     )
