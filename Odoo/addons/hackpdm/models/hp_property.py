from odoo import models, fields, api

class hp_property(models.Model):
    _name = 'hp.property'
    _description = 'hp property'
    _inherit = 'hp.common.model'

    name = fields.Char(
        required=True,
        string='property name',
    )
    prop_type = fields.Char(
        string='property type',
    )

    active = fields.Boolean(
        string='active',
    )
