from odoo import models, fields, api

class hp_property(models.Model):
    #base fields
    _name = 'hp.property'
    _description = 'hp property'
    _inherit = 'hp.common.model'

    #fields
    name = fields.Char(string='property name', required=True)
    prop_type = fields.Char(string='property type')
    active = fields.Boolean(string='active')

    #relational fields
