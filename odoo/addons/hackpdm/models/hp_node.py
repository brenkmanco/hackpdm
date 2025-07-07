from odoo import fields, models, api
import datetime as dt
import numpy as np

class hp_node(models.Model):
    #base fields
    _name = 'hp.node'
    _description = 'node'
    _inherit = 'hp.common.model'

    #fields
    name = fields.Char(string='name', required=True)

    #relational fields
