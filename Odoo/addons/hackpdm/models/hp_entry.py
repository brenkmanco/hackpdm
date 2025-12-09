import logging
from odoo import models, fields, api

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

    checkout_date = fields.Datetime(
        string='checkout date',
    )
    latest_date = fields.Datetime(
        related='latest_version_id.file_modify_stamp',
        string='latest date',
    )

    #active = fields.Boolean(string='active')
    deleted = fields.Boolean(
        default=False,
        store=True,
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
        comodel_name='res.users',
        string='checkout user',
    )
    checkout_node = fields.Many2one(
        comodel_name='hp.node',
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

    def first_helper(self, key = lambda x: True):
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


    #@api.depends('version_ids')
    def _compute_latest_version(self):
        for record in self:
            if record.version_ids:
                record.latest_version_id = record.version_ids.sorted(key=lambda x: x.id, reverse=True)[0]
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
        hp_version = self.env["hp.version"].search([("checksum", "=", checksum)])
        if hp_version:
            return False
        return True

    @api.model
    def checksum_list_nonmatches(self, checksums):
        unfound = []
        hp_version = self.env["hp.version"].search([("checksum", "not in", checksums)])

        if hp_version:
            for version in hp_version:
                unfound.append(version.checksum)

        return unfound