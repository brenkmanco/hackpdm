import io
import os
from odoo import models, fields, api, Command
from odoo.exceptions import AccessError, UserError, ValidationError
from webdav3.client import Client
import hashlib as hash
import base64
import logging
import urllib.parse
import magic

import sqlalchemy
from sqlalchemy import Connection, Engine, MetaData, Table, create_engine, select, text, update
from sqlalchemy.orm import Mapped, Session, sessionmaker, relationship

from . import hp_guard

_logger = logging.getLogger(__name__)

# a version is a snapshot: once created only an administrator may swap its file or its entry
_IMMUTABLE_VERSION_FIELDS = frozenset(("entry_id", "file_contents"))
_CHECKOUT_VERSION_FIELDS = frozenset(("checkout_user", "checkout_date", "checkout_node"))

class Database:
    sessionURL = ""
    engine:Engine = None
    connection:Connection = None
    metadata:MetaData = None
    session:Session = None
    
    def __init__(self, db_username:str, db_password:str, db_name:str, db_hostname:str, db_port):
        self.sessionURL = f'postgresql://{db_username}:{db_password}@{db_hostname}:{db_port}/{db_name}'

        self.engine = create_engine(self.sessionURL)
        self.connection = self.engine.connect()
        self.metadata = MetaData()
        self._Session = sessionmaker(bind=self.engine)

    def get_table(self, table_name):
        return Table(table_name, self.metadata, autoload_with=self.engine)

    def start_session(self):
        self.session = self._Session()

    # def execute(self, statement):
    #     return self.session.execute(statement)
class WebDav():
    logging.basicConfig(level=logging.DEBUG)
    logger = logging.getLogger(__name__)
    local_path = "~/dev/tempDirectory"

    def __init__(self, client:Client):
        self.client = client
    def __init__(self, options:dict[str, any]):
        self.client = Client(options)
    def __init__(self):
        options = {
            'webdav_hostname': "http://hackpdm.zip.azi/webdav",
            'webdav_port': 80,
            'webdav_login': "mtaylor",
            'webdav_password': "mtaylor",
            'webdav_override_methods': {
                'check': 'GET',
            },
            'webdav_timeout': 30,
        }
        self.client = Client(options)

    def files_in_entry(self, entry_id:int):
        try:
            directory_path = urllib.parse.quote(f"/{entry_id}")
            return self.client.list(directory_path)
        except:
            logging.error(f"entry '{entry_id}' doesn't exist")
            return []

    def get_file_from_webdav(self, entry_id:int, version_id:int, file_extension:str):
        file_path = urllib.parse.quote(f"/{entry_id}/{version_id}.{file_extension}")
        file_info = {}
        try:
            file_info = self.client.info(file_path)
            buffer = io.BytesIO()

            self.client.download_from(remote_path=file_path, buff=buffer)
            buffer.seek(0)
            file_contents = buffer.read()

            return file_contents
        except Exception as e:
            if f"{version_id}.{file_extension}" in self.files_in_entry(entry_id):
                logging.error(e)
            else:
                logging.error(f"file version '{version_id}' doesn't exist")
        return b''

        #print(f"File information: {file_info}")

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
        if not hp_guard.is_pdm_admin(self.env):
            for vals in vals_list:
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

    def write(self, vals):
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
        """Checks out the entry of version_id to the calling user on node (an hp.node id or name).
        Returns True; raises UserError when the entry is checked out to someone else or deleted."""
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
            v_rec.node_id = node_rec.id
        elif entry.deleted:
            raise UserError(f"'{entry.name}' is deleted and cannot be checked out")
        else:
            raise UserError(f"'{entry.name}' is already checked out to {entry.checkout_user.name}")
        return True

    @api.model
    def checkout_entries(self, version_ids, node):
        for version_id in version_ids:
            self.checkout_entry(version_id, node)
        return True

    @api.model
    @api.depends('entry_id', 'file_ext', 'name')
    def migrate_webdav_files_to_records(self):
        hp_guard.require_admin(self.env, "migrate_webdav_files_to_records")
        web_dav = WebDav()
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


    def _get_image_bytes(self, database1:Database, record):
        lost_on_step = 0
        try:
            # Use raw SQL to bypass SQLAlchemy Table reflection entirely
            stmt = text(f"SELECT preview_image FROM hp_version WHERE version_id = {record.id}")
            lost_on_step = 1

            result = database1.connection.execute(stmt).first()

            lost_on_step = 2
            return result[0] if result else None
        except Exception as e:
            logging.error(f"query error on step: {lost_on_step} | error: {e}")
            return None

    @api.model
    @api.depends('name')
    def migrate_image_from_database(self):
        hp_guard.require_admin(self.env, "migrate_image_from_database")
        hackpdm = Database("hackpdm", "hackpdm", "hackpdm", "alderaan", 5432)
        hackpdm.start_session()
        all_records = self.env[self._name].search([])

        for record in all_records:
            attachment = record._get_attachments(record, "preview_image")
            if attachment and len(attachment)>0:
                logging.info(f"version record {attachment[0].res_id} has preview_image")
                continue

            try:
                image = self._get_image_bytes(hackpdm, record)
                if image != None:
                    raw_image = bytes(image)
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
        hp_guard.require_admin(self.env, "display_all_fnames")
        all_records = self.env["ir.attachment"].search([("res_model", "=", "hp.version")])
        for record in all_records:
            logging.info(f"attachment id = {record.id}\nname = {record.name}\nres_model = {record.res_model}\nres_id = {record.res_id}\nres_fields = {record.res_field}\nfname = {record.store_fname}\n")

    @api.model
    def cleanup_filestore(self):
        hp_guard.require_admin(self.env, "cleanup_filestore")

class hp_version_property(models.Model):
    #base fields
    _name = 'hp.version.property'
    _description = 'hp version property'
    _inherit = 'hp.common.model'

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
