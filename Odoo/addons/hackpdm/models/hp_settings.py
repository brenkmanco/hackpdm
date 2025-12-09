from odoo import fields, models


class hp_settings(models.Model):
    _name = 'hp.settings'
    _description = 'hp settings'
    _sql_constraints = [
        ('name_uniq', 'unique (name)', 'Setting names must be unique')
    ]

    name = fields.Char(
        required=True,
        string='Name',
    )

    description = fields.Text(
        string="Description",
    )

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

    bool_value = fields.Boolean(
        string='boolean value',
    )

    int_value = fields.Integer(
        string='integer value',
    )

    char_value = fields.Char(
        string='character value',
    )

    float_value = fields.Float(
        string='float value',
    )

    date_value = fields.Datetime(
        string='date value',
    )
