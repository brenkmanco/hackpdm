from odoo import fields, models, api, Command
from odoo.addons.queue_job.job import job

from odoo.exceptions import UserError

class hp_pdm_commit(models.Model):
    _name = 'hp.pdm.commit'
    _description = 'commit collection'
    _inherit = ['hp.common.model', 'queue.job.mixin']

    name = fields.Char()
    job_uuid = fields.Char(
        string="job id",
    )

    committed_at = fields.Datetime(
        string="time commit was initiated",
    )
    commit_finished_at = fields.Datetime(
        string="when the commit finished",
    )

    committing = fields.Boolean(
        string="currently being committed",
        default=False,
        readonly=True,
    )
    committed = fields.Boolean(
        string="have these files been committed?",
        default=False,
        readonly=True,
    )
    errored = fields.Boolean(
        string="error when commiting files",
        default=False,
    )

    message_exception = fields.Text(
        string="error message",
    )

    progress_total = fields.Integer(
        compute="_compute_total_files",
        string="commit total",
    )
    duration_seconds = fields.Float(
        string="commit duration",
        compute="_compute_duration",
    )
    commit_summary = fields.Text(
        string="commit summary",
    )
   
    created_by = fields.Many2one(
        comodel_name="res.users", 
        default=lambda self: self.env.uid,
    )
    node_by = fields.Many2one(
        comodel_name="hp.node",
        string="computer node",
    )

    staged_ids = fields.One2many(
        comodel_name="hp.record.staged",
        inverse_name="commit_id",
        string="list of records staged",
    )

    @api.depends('staged_ids')
    def _compute_total_files(self):
        for commit in self:
            commit.progress_total = len(commit.staged_ids)

    @api.depends('committed_at', 'commit_finished_at')
    def _compute_duration(self):
        for rec in self:
            if rec.committed_at and rec.commit_finished_at:
                rec.duration_seconds = (rec.commit_finished_at - rec.committed_at).total_seconds()
            else:
                rec.duration_seconds = 0

    def _rollback_records_staged(self, staged_ids):
        staged = self.env["hp.record.staged"].browse(staged_ids)
        #staged.write({"payload": False})
        staged.unlink()

    # ------------------------------------------------------------ 
    # CLIENT CALLS THIS — returns immediately 
    # ------------------------------------------------------------
    def start_commit(self):
        self.ensure_one()

        if self.committing:
            raise UserError("Commit already in progress.")
        
        if self.committed:
            raise UserError("This commit has already been completed.")
        
        if not self.staged_ids:
            raise UserError("No staged records to commit.")

        #enqueue job
        job = self.with_delay().run_commit_job()
        self.write({
            "job_uuid": job.uuid,
            "committing": True,
            "committed_at": fields.Datetime.now(),
        })

        return True
    
    def handle_commit_failure(self, job, exc):
        self.write({
            "errored": True,
            "message_exception": str(exc),
            "committing": False,
            "committed": False,
            "commit_finished_at": fields.Datetime.now(),
        })

        # cleanup staged records
        self._rollback_records_staged(self.staged_ids.ids)

    # ------------------------------------------------------------
    # BACKGROUND WORKER — runs even if client disconnects
    # ------------------------------------------------------------
    @job(on_error='handle_commit_failure')
    def run_commit_job(self):
        self.ensure_one()

        # ATOMIC BLOCK — all or nothing
        self._create_all_records_atomically()

        # success
        self.write({
            "committed": True,
            "committing": False,
            "commit_finished_at": fields.Datetime.now(),
        })

    def _write_summary(self, model_dict:dict):
        summary_text = "models created:\n"
        for model, ids in model_dict.items():
            summary_text += f"\t- {model}:\n"
            for id in ids:
                summary_text += f"\t\t- {id}\n"
        return summary_text


    # ------------------------------------------------------------
    # ATOMIC CREATION — ONE BIG TRANSACTION
    # ------------------------------------------------------------
    def _create_all_records_atomically(self):
        """
        This method runs inside the queue job's transaction.
        If ANY record creation fails, the entire transaction rolls back.
        No savepoints. No partial commits.
        """
        summary_model = {}
        staged = self.staged_ids
        total = len(staged)

        for index, row in enumerate(staged, start=1):
            model = self.env[row.target_model]
            rec = model.create(row.payload)
            summary_model.setdefault(row.target_model, []).append(rec.id)
            row.write({"target_id": rec.id})

        # wipe payloads only if everything succeeded
        self.write({"commit_summary": self._write_summary(summary_model)})
        staged.write({"payload": False})


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
    