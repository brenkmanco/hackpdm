from odoo import models, fields, api

class hp_category(models.Model):
    #base fields
    _name = 'hp.category'
    _description = 'category'
    _inherit = 'hp.common.model'

    #fields
    name = fields.Char(
        string='category name', 
        index='trigram', 
        required=True, 
    )
    
    #parent_path = fields.Char(index=True, unaccent=False)
    cat_description = fields.Char(
        string='category description', 
        default='CAD files are versioned and have dependencies.'
    )
    track_version = fields.Boolean(string='track version', default=True)
    track_depends = fields.Boolean(string='track depends', default=True)

    #relational fields


class hp_category_property(models.Model):
    #base fields
    _name = 'hp.category.property'
    _description = 'hp category property'
    _inherit = 'hp.common.model'

    #fields
    
    #relational fields
    cat_id = fields.Many2one(
        comodel_name='hp.category', 
        string='category id',
    )
    prop_id = fields.Many2one(
        comodel_name='hp.property',
        string='property',
    )
