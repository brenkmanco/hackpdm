from odoo import fields, models, Command

class hp_record_staged(models.Model):
    _name = 'hp.record.staged'
    
    commit_id = fields.Many2one(
        comodel_name="hp.pdm.commit",
        string="associated commit",
    )
    target_model = fields.Char(
        required=True,
    )
    target_id = fields.Integer(
        string="Id of the record that was created",
    )
    payload = fields.Json(
        required=True,
    )
    