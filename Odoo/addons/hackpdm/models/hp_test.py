from odoo import models, fields, api

class hp_test(models.Model):
    #base fields
    _name = 'hp.test'
    _description = 'test'
    _inherit = 'hp.common.model'
    _parent_name = 'manytoone'
    _parent_store = True

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
    selection = fields.Selection([
        ('option_a', 'Option A'),
        ('option_b', 'Option B'),
        ('option_c', 'Option C'),
    ], string="Selection")

    text = fields.Text()

    parent_path = fields.Char(
        index=True,
        unaccent=False,
    )

    manytoone = fields.Many2one(comodel_name="hp.test", string="Parent")
    onetomany = fields.One2many("hp.test", 'manytoone', string="Children")
    manytomany = fields.Many2many('hp.test', 'hp_test_rel', 'left_id', 'right_id', string="many2many")

    currency_id = fields.Many2one(
        'res.currency',
        string='Currency',
        required=True,
        default=lambda self: self.env.company.currency_id.id
    )
    monetary = fields.Monetary(currency_field='currency_id')