import logging
from odoo import models, fields, api
from odoo.exceptions import AccessError, UserError

from . import hp_guard

# checkout state: kept on the entry's latest version (hp.version.checkout_*), shown here through
# latest_version_id; set by hp.version.checkout_entry, cleared by un-checkout and by a commit
_CHECKOUT_FIELDS = ("is_checked_out", "checkout_user", "checkout_node", "checkout_date")
# what only the user holding the checkout (or an administrator) may change
_HOLDER_ONLY_FIELDS = frozenset(_CHECKOUT_FIELDS + ("deleted", "name", "dir_id", "type_id"))

class hp_entry_name_filter(models.Model):
    #base fields
    _name = 'hp.entry.name.filter'
    _description = 'hp entry name filter'
    _inherit = 'hp.common.model'

    #fields
    name_proto = fields.Char(string='name prototype')
    name_regex = fields.Char(string='name regex')
    description = fields.Char(string='description')

class hp_entry(models.Model):
    _name = 'hp.entry'
    _description = 'hp entry'
    _inherit = 'hp.common.model'

    name = fields.Char(
        required=True,
        string='file name',
    )
    directory_complete_name = fields.Char(
        related='dir_id.complete_name',
        string='directory folder path',
    )
    windows_complete_name = fields.Char(
        compute='_compute_windows_path',
        store=True,
        string='windows directory path name',
    )
    windows_complete_path = fields.Char(
        compute='_compute_windows_path',
        string='windows directory path',
    )

    # the checkout lives on the latest version; these read and write it there. A commit makes a new
    # (not checked out) latest version, so committing checks the entry in by itself.
    checkout_date = fields.Datetime(
        related='latest_version_id.checkout_date',
        readonly=False,
        string='checkout date',
    )
    latest_date = fields.Datetime(
        related='latest_version_id.file_modify_stamp',
        string='latest date',
    )

    deleted = fields.Boolean(
        default=False,
        store=True,
    )
    is_checked_out = fields.Boolean(
        compute='_compute_is_checked_out',
        inverse='_inverse_is_checked_out',
        search='_search_is_checked_out',
        string='is checked out',
        help='Whether the latest version is checked out (has a checkout user); setting it to False releases it',
    )
    latest_file_size = fields.Integer(
        related='latest_version_id.file_size',
        string='latest file size',
    )

    latest_version_id = fields.Many2one(
        comodel_name='hp.version',
        index=True,
        search='_search_latest_version',
        compute='_compute_latest_version',
        string='latest version',
    )
    latest_release_id = fields.Many2one(
        comodel_name='hp.release',
        index=True,
        search='_search_latest_release',
        compute='_compute_latest_release',
        string='latest release'
    )
    dir_id = fields.Many2one(
        comodel_name='hp.directory',
        string='directory id',
    )
    type_id = fields.Many2one(
        comodel_name='hp.type',
        string='type',
    )
    cat_id = fields.Many2one(
        comodel_name='hp.category',
        related='type_id.cat_id',
        string='category id',
    )
    checkout_user = fields.Many2one(
        related='latest_version_id.checkout_user',
        readonly=False,
        string='checkout user',
    )
    checkout_node = fields.Many2one(
        related='latest_version_id.checkout_node',
        readonly=False,
        string='checkout node',
    )

    version_ids = fields.One2many(
        comodel_name='hp.version',
        inverse_name='entry_id',
        string='versions',
    )
    release_ids = fields.One2many(
        comodel_name='hp.release',
        inverse_name='entry_id',
        string='releases',
    )
    version_property_ids = fields.One2many(
        comodel_name='hp.version.property',
        compute='_compute_all_properties',
        string='version properties',
    )
    
    def init(self):
        """Creates the case-insensitive unique index on (dir_id, lower(name)) at install/upgrade.

        Odoo calls this when the module is installed or updated; it is not meant for clients. The index
        is skipped (with a warning) while existing files in one folder still differ only by case.

        Args:
            None.

        Returns:
            None: nothing.
        """
        hp_guard.create_case_insensitive_unique_index(
            self.env.cr, self._table, "hp_entry_dir_name_ci_uniq", "dir_id")

    @api.constrains("name")
    def _check_name_path_safe(self):
        for rec in self:
            hp_guard.check_name(rec.name, "file")

    def write(self, vals):
        """Writes entry fields, guarding checkouts for users who are not administrators.

        A regular user may not change the checkout, name, folder, type or deleted flag of a file another
        user has checked out, may not check a file out in someone else's name, and may not check out a
        deleted file. Writing ``is_checked_out=False`` also clears checkout_user, checkout_node and
        checkout_date. The checkout fields are stored on the latest version (these are related fields),
        so that version's own guards apply as well.

        Args:
            vals (dict): Field values to write.

        Returns:
            bool: True.

        Raises:
            AccessError: Checking out in someone else's name.
            UserError: The file is checked out to another user, or is deleted and being checked out.
            ValidationError: The new name is not a safe file name.
        """
        if not hp_guard.is_pdm_admin(self.env):
            self._check_user_write(vals)
        if "is_checked_out" in vals and not vals["is_checked_out"]:
            # releasing a checkout clears who/where/when held it
            vals = dict(vals, checkout_user=False, checkout_node=False, checkout_date=False)
        return super().write(vals)

    def _check_user_write(self, vals):
        """A user may not take over, clear, delete, rename or move an entry another user has
        checked out, nor check an entry out in someone else's name."""
        if not _HOLDER_ONLY_FIELDS.intersection(vals):
            return
        uid = self.env.uid
        new_holder = vals.get("checkout_user")
        if isinstance(new_holder, (list, tuple)):
            new_holder = new_holder[0] if new_holder else False
        if new_holder and new_holder != uid:
            raise AccessError("You can only check entries out to yourself.")
        for rec in self:
            holder = rec.checkout_user
            if holder and holder.id != uid:
                raise UserError(
                    f"'{rec.name}' is checked out to {holder.name}; only they or an administrator can change it.")
            if (vals.get("is_checked_out") or new_holder) and rec.deleted and vals.get("deleted", True):
                raise UserError(f"'{rec.name}' is deleted and cannot be checked out.")

    @api.depends('version_ids.checkout_user')
    def _compute_is_checked_out(self):
        for rec in self:
            rec.is_checked_out = bool(rec.checkout_user)

    def _inverse_is_checked_out(self):
        # only releasing is done this way; checking out goes through hp.version.checkout_entry
        for rec in self:
            if not rec.is_checked_out and rec.latest_version_id:
                rec.latest_version_id.write({"checkout_user": False, "checkout_date": False, "checkout_node": False})

    def _search_is_checked_out(self, operator, value):
        if operator not in ("=", "!="):
            raise UserError(f"Unsupported search on is_checked_out: {operator} {value!r}")
        checked_out = bool(value) == (operator == "=")
        return [("checkout_user", "!=" if checked_out else "=", False)]

    def first_helper(self, key = lambda x: True):
        """Returns the first record of this recordset that satisfies a predicate.

        A Python helper; it takes a function, so it cannot be called over RPC.

        Args:
            key (Callable[[record], bool], optional): The test. Defaults to accepting any record.

        Returns:
            hp.entry | bool: The first matching record, or False when none matches.
        """
        try:
            return next(x for x in self if key(x))
        except:
            return False

    @api.depends('directory_complete_name', 'name')
    def _compute_windows_path(self):
        for rec in self:
            if rec.directory_complete_name:
                paths = str(rec.directory_complete_name).split(" / ")
                windows_path = "\\".join(paths)
                windows_path_name = "\\".join([windows_path, rec.name])
                rec.windows_complete_path = windows_path
                rec.windows_complete_name = windows_path_name

    #@api.depends('version_ids')
    def _compute_all_properties(self):
        for record in self:
            if record.version_ids:
                recordmap = record.version_ids.mapped('version_property_ids')
                record.version_property_ids = recordmap
            else:
                record.version_property_ids = False


    def _search_latest_version(self, operator, value):
        """Search on the computed latest_version_id (the highest version id of each entry).

        Odoo also calls this when a version's checkout fields change, to find the entries whose
        computed checkout info depends on it; without it every commit failed on the server."""
        self.env["hp.version"].flush_model(["entry_id"])
        self.env.cr.execute(
            "SELECT entry_id, MAX(id) FROM hp_version WHERE entry_id IS NOT NULL GROUP BY entry_id"
        )
        latest_by_entry = dict(self.env.cr.fetchall())

        if value is False or value is None:
            if operator in ("=", "!="):
                # "= False": entries without any version
                return [("id", "not in" if operator == "=" else "in", list(latest_by_entry))]
            raise UserError(f"Unsupported search on latest version: {operator} {value!r}")

        if isinstance(value, str) or (isinstance(value, (list, tuple)) and value and isinstance(value[0], str)):
            version_domain = [("name", operator, value)]
        else:
            version_domain = [("id", operator, value)]
        matching = set(self.env["hp.version"].search(version_domain).ids)
        return [("id", "in", [entry for entry, latest in latest_by_entry.items() if latest in matching])]

    # not stored, but its cached value must follow new versions: the checkout fields are read through
    # it, so a commit that reads them before adding a version would otherwise keep seeing the old latest
    @api.depends('version_ids')
    def _compute_latest_version(self):
        for record in self:
            if record.version_ids:
                record.latest_version_id = record.version_ids.sorted(
                    key=lambda x: x.id, reverse=True
                    )[0]
            else:
                record.latest_version_id = False

    def _compute_latest_release(self):
        for record in self:
            if record.release_ids:
                latest_versions = record.version_ids.sorted(key=lambda x: x.id, reverse=True)
                latest_version = latest_versions.first_helper(lambda x: x.release_id != False)
                record.latest_release_id = latest_version.release_id
            else:
                record.latest_release_id = False

    @api.model
    def checksum_nonmatches(self, checksum):
        """Tells whether no version in the vault has the given checksum.

        Not reliable: ``checksum`` is related to the version's ir.attachment, and searches on
        attachments skip those that belong to a field, so the search never matches and this always
        returns True. No client calls it.

        Args:
            checksum (str): SHA-1 checksum, lowercase hex.

        Returns:
            bool: True when no version was found with that checksum.
        """
        hp_version = self.env["hp.version"].search([("checksum", "=", checksum)])
        if hp_version:
            return False
        return True

    @api.model
    def checksum_list_nonmatches(self, checksums):
        """Returns the checksums of versions whose checksum is not in the given list.

        Not reliable, for the same reason as :meth:`checksum_nonmatches`. No client calls it.

        Args:
            checksums (list[str]): SHA-1 checksums, lowercase hex.

        Returns:
            list[str]: Checksums of the versions found outside the list.
        """
        unfound = []
        hp_version = self.env["hp.version"].search([("checksum", "not in", checksums)])

        if hp_version:
            for version in hp_version:
                unfound.append(version.checksum)

        return unfound