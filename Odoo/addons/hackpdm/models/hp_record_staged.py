from odoo import api, fields, models, Command
from odoo.exceptions import AccessError, UserError

from . import hp_guard

# what a commit creates (hp.pdm.commit.model_order, plus the links it makes itself)
_STAGEABLE = frozenset((
    "hp.settings", "hp.category", "hp.entry.name.filter", "hp.type", "hp.property", "hp.category.property",
    "hp.directory", "hp.entry", "hp.version", "hp.version.property", "hp.version.relationship",
    "hp.release.review", "hp.release",
))

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
    

    
    @api.model_create_multi
    def create(self, vals_list):
        if not hp_guard.is_pdm_admin(self.env):
            for vals in vals_list:
                self._check_own_commit(vals.get("committing_id"))
                if vals.get("target_model") not in _STAGEABLE:
                    raise UserError(f"A commit cannot create {vals.get('target_model')!r} records.")
        return super().create(vals_list)

    def write(self, vals):
        if not hp_guard.is_pdm_admin(self.env):
            for rec in self:
                self._check_own_commit(rec.committing_id.id)
            if "committing_id" in vals:
                self._check_own_commit(vals["committing_id"])
            if "target_model" in vals and vals["target_model"] not in _STAGEABLE:
                raise UserError(f"A commit cannot create {vals['target_model']!r} records.")
        return super().write(vals)

    def _check_own_commit(self, commit_id):
        """A commit runs with its creator's rights: rows staged into another user's commit would
        be created as that user."""
        commit = self.env["hp.pdm.commit"].browse(commit_id).exists() if commit_id else None
        if not commit:
            raise UserError(f"Commit {commit_id} does not exist.")
        if commit.create_uid.id != self.env.uid:
            raise AccessError(f"Commit {commit.id} belongs to another user.")
