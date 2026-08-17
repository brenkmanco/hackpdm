from odoo import fields, models, Command

class hp_record_staged(models.Model):
    _name = 'hp.record.staged'
    
    committing_id = fields.Many2one(
        comodel_name="hp.pdm.commit",
        required=True,
        string="associated commit",
    )
    target_model = fields.Char(
        required=True,
        string="Name of the record model"
    )
    target_id = fields.Integer(
        string="Id of the record",
    )
    dependency_tree_ids = fields.Many2many(
        "hp.record.staged",
        "hp_record_staged_rel",
        "parent_id",
        "child_id",
        string="dependency tree"
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

    