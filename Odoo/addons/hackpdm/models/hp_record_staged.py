from odoo import fields, models, Command

class hp_record_staged(models.Model):
    _name = 'hp.record.staged'
    
    commit_id = fields.Many2one(
        comodel_name="hp.pdm.commit",
        required=True,
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
    child_parent_ids = fields.Many2many(
        comodel_name="hp.record.staged",
        relation="hp_record_staged_dependency_rel",
        column1="child_id",
        column2="parent_id",
        string="Depends On",
    )
    parent_child_ids = fields.Many2many(
        comodel_name="hp.record.staged",
        relation="hp_record_staged_dependency_rel",
        column1="parent_id",
        column2="child_id",
        string="Required For",
    )

    