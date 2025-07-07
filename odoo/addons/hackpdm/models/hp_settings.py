from odoo import fields, models


class hp_settings(models.Model):
    _name = 'hp.settings'
    _description = 'hp settings'
    _sql_constraints = [
        ('name_uniq', 'unique (name)', 'Setting names must be unique')
    ]

    name = fields.Char(string='Name', required=True)
    description = fields.Text(string="Description")
    type = fields.Selection(
        selection=[
            ("bool", "Boolean"),
            ("int", "Integer"),
            ("char", "Char"),
            ("float", "Float"),
            ("date", "Datetime"),
        ],
        required=True,
    )
    bool_value = fields.Boolean()
    int_value = fields.Integer()
    char_value = fields.Char()
    float_value = fields.Float()
    date_value = fields.Datetime()
