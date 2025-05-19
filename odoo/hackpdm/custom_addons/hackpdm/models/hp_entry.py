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
    #base fields
    _name = 'hp.entry'
    _description = 'hp entry'
    _inherit = 'hp.common.model'

    #fields
    name = fields.Char(string='file name', required=True)
    checkout_date = fields.Datetime(
        string='checkout date', 
    )
    #active = fields.Boolean(string='active')
    deleted = fields.Boolean(
        default=False,
        store=True,
    )
    latest_date = fields.Datetime(
        related='latest_version_id.file_modify_stamp',
        string='latest date',
    )
    latest_file_size = fields.Integer(
        related='latest_version_id.file_size',
        string='latest file size',
    )

    #relational fields
    version_ids = fields.One2many(
        comodel_name='hp.version',
        inverse_name='entry_id',
        string='versions',
    )
    latest_version_id = fields.Many2one(
        comodel_name='hp.version',
        index=True,
        search='_search_latest_version',
        compute='_compute_latest_version',
        string='latest version',
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
    directory_complete_name = fields.Char(
        related='dir_id.complete_name',
        string='directory folder path',
    )
    version_property_ids = fields.One2many(
        comodel_name='hp.version.property',
        compute='_compute_all_properties',
        string='version properties',
    )
    mapped_properties = fields.One2many(
        compute='_compute_map_properties',
        string='mapped properties',
    )

    def _compute_map_properties(self):
        for record in self:
            if record.version_property_ids:
                record.mapped_properties = record.version_property_ids.mapped('')

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
                record.latest_version_id = record.version_ids.sorted(key=lambda x: x.create_date, reverse=True)[0]
            else:
                record.latest_version_id = False
        
    # def _search_latest_version(self, operator, value):

    #     # version id = value
    #     entry = self.env["hp.version"].search([('entry_id', '=', value)])[0]
        


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