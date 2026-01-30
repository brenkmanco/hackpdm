from odoo import fields, models, api, Command
import datetime as dt
import numpy as np

class hp_pdm_commit(models.Model):
    _name = 'hp.pdm.commit'
    _description = 'commit collection'
    _inherit = 'hp.common.model'

    name = fields.Char()
    target_model = fields.Char(
        required=True,
        string="precise name of the model",
    )

    created_by = fields.Many2one(
        comodel_name="res.users", 
        default=lambda self: self.env.uid,
    )
    created_at = fields.Datetime(
        default=fields.Datetime.now,
    )
    committed = fields.Boolean(
        string="have these files been committed?",
        default=False,
    )
    
    staged_ids = fields.One2many(
        comodel_name="hp.record.staged",
        inverse_name="commit_id",
        string="list of records staged",
    )

    @api.model
    def create_staging(self):
        pass


    @api.model
    def commit(self, commit_id):
        commit = self.browse(commit_id)
        staged = commit.staged_ids

        created = []

        for row in staged:
            model = self.env[row.target_model]
            rec = model.create(row.payload)  # if this fails → rollback everything
            rec.write({"target_id": rec.id})
            created.append(rec.id)

        # Only executed if everything succeeded
        staged.write({"payload": False})

        return created


class hp_record_staged(models.Model):
    _name = 'hp.record.staged'
    
    commit_id = fields.Many2one(
        comodel_name="hp.commit",
        string="associated commit",
    )
    target_model = fields.Char(
        required=True,
    )
    target_id = fields.Integer(
        string="Id of the record that was created"
    )
    payload = fields.Json(
        required=True,
    )
    