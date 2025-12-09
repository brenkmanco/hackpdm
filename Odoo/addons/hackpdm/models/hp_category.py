from odoo import models, fields, api

class hp_category(models.Model):
    _name = 'hp.category'
    _description = 'category'
    _inherit = 'hp.common.model'

    name = fields.Char(
        string='category name',
        index='trigram',
        required=True,
    )
    cat_description = fields.Char(
        default='CAD files are versioned and have dependencies.',
        string='category description',
    )
    #parent_path = fields.Char(index=True, unaccent=False)

    track_version = fields.Boolean(
        default=True,
        string='track version',
    )
    track_depends = fields.Boolean(
        default=True,
        string='track depends',
    )


class hp_category_property(models.Model):
    _name = 'hp.category.property'
    _description = 'hp category property'
    _inherit = 'hp.common.model'

    cat_id = fields.Many2one(
        comodel_name='hp.category',
        string='category id',
    )
    prop_id = fields.Many2one(
        comodel_name='hp.property',
        string='property',
    )
