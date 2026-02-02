from odoo import models, fields, api

class hp_test(models.Model):
    #base fields
    _name = 'hp.test'
    _description = 'test'
    _inherit = 'hp.common.model'

    #fields
    binary = fields.Binary()
    boolean = fields.Boolean()
    character = fields.Char()
    dates = fields.Date()
    datetimes = fields.Datetime()
    floats = fields.Float()
    html = fields.Html()
    image = fields.Image()
    integer = fields.Integer()
    json = fields.Json()
    monetary = fields.Monetary()
    selection = fields.Selection([
        ('option_a', 'Option A'),
        ('option_b', 'Option B'),
        ('option_c', 'Option C'),
    ], string="Selection")

    text = fields.Text()

    many2one = fields.Many2One(comodel="hp.test")
    one2many = fields.One2Many(comodel="hp.test", inverse_name="many2one")
    many2many = fields.Many2Many(comodel="hp.test", inverse_name="many2many")
