from odoo import fields, models, Command

class hp_record_staged(models.TransientModel):
    _name = 'hp.record.staged'
    _inherit = 'hp.common.model'
    # The maximum number of hours a staged record can be kept in the database before 
    # it is automatically deleted. This is to prevent the database from being filled with stale records.
    _transient_max_hours = 24.0
    
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
    payload = fields.Json(
        required=True,
    )


    