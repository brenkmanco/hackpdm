from odoo import models, fields, api

class hp_user(models.Model):
    #base fields
    _name = 'hp.user'
    _description = 'user login'
    _inherit = 'hp.common.model'

    #fields
    login_name = fields.Char(string='login username', default='admin')
    password = fields.Char(string='login password', default='admin')
    first_name = fields.Char(string='first name', default='Admin')
    last_name = fields.Char(string='last name', default='User')
    email = fields.Char(string='email username')
    modify_timestamp = fields.Datetime(
        string='modified date', 
        default=lambda self:fields.Datetime.now(),
    )
    