from odoo import models, fields, api

class test_model(models.Model):
    _name = "test.model.custom"
    _description = "debug model"

    user = fields.Many2one(
        comodel_name="res.users",
        string="user"
    )