from odoo import models, fields, api

class hp_type(models.Model):
    #base fields
    _name = 'hp.type'
    _description = 'file type'
    _inherit = 'hp.common.model'

    #fields
    description = fields.Char(
        string='description',
    )
    file_ext = fields.Char(
        string='file extension',
    )
    type_regex = fields.Char(
        string='regex type',
    )

    icon = fields.Image(
        string='icon image',
        attachment=True,
    )

    cat_id = fields.Many2one(
        comodel_name='hp.category',
        string='default category',
    )