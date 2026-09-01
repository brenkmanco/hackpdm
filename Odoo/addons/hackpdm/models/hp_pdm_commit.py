import logging

from odoo import api, fields, models
from odoo.exceptions import UserError
#from odoo.addons import queue_job

class hp_pdm_commit(models.Model):
    _name = 'hp.pdm.commit'
    _inherit = 'hp.common.model'

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
    commit_summary = fields.Text(
        string="commit summary",
    )

    progress_total = fields.Integer(
        compute="_compute_total_files",
        string="commit total",
    )
    duration_seconds = fields.Float(
        string="commit duration",
        compute="_compute_duration",
    )

    node_by = fields.Many2one(
        comodel_name="hp.node",
        string="computer node",
    )

    staged_ids = fields.One2many(
        comodel_name="hp.record.staged",
        inverse_name="committing_id",
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
    # CLIENT CALLS THIS — resets commit records
    # ------------------------------------------------------------
    @api.model
    def clear_commit(self, id):
        if id is list:
            id = id[0]
        record = self.env["hp.pdm.commit"].browse([id])
        record.ensure_one()
        records = record.staged_ids

        if records:
            records.unlink()
            record.write({
                "committed": False, 
                "committing": False, 
                "errored": False, 
                "message_exception": False, 
                "commit_summary": False,
                "committed_at": False,
                "commit_finished_at": False,
            })
        return True


    # ------------------------------------------------------------ 
    # CLIENT CALLS THIS — returns immediately 
    # ------------------------------------------------------------
    @api.model
    def start_commit(self, commit_id):
        # 1. Corrected type checking and avoided shadowing Python's built-in `id`
        if isinstance(commit_id, list):
            commit_id = commit_id[0]
        
        # 2. Simplified browse call
        record = self.browse(commit_id)
        record.ensure_one()

        # 3. Cleaned up logging to match Odoo production standards
        
        if record.committing:
            raise UserError("Commit already in progress.")

        if record.committed:
            raise UserError("This commit has already been completed.")

        if not record.staged_ids:
            raise UserError("No staged records to commit.")

        # 4. Removed the invalid `on_error` argument
        logging.info("Starting commit for record %s. Committing status: %s", record.id, record.committing)
        job = record.with_delay().run_commit_job()
        logging.info("Commit job created for record %s. Job UUID: %s", record.id, job.uuid)
        record.write({
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
    #@job
    def run_commit_job(self):
        try:
            
            self.ensure_one()

            # ATOMIC BLOCK — all or nothing
            self._create_all_records_atomically()

            # success
            self.write({
                "committed": True,
                "committing": False,
                "commit_finished_at": fields.Datetime.now(),
            })
        except Exception as e:
            logging.error("Commit job failed for record %s: %s", self.id, str(e))
            self.handle_commit_failure(self, e)
            raise  # Re-raise the exception to ensure the job is marked as failed

    def _write_summary(self, model_dict:dict):
        summary_text = "models created:\n"
        for model, ids in model_dict.items():
            summary_text += f"\t- {model}:\n"
            for id in ids:
                summary_text += f"\t\t- {id}\n"
        return summary_text

    model_order = [
        "hp.settings",
        "hp.category",
        "hp.entry.name.filter",
        "hp.type",
        "hp.property",
        "hp.category.property",
        "hp.directory",
        "hp.entry",
        "hp.version",
        "hp.version.property",
        "hp.version.relationship",
        "hp.release.review",
        "hp.release",
    ]
    def _create_records(self, staged, summary_model):
        for index, row in enumerate(staged, start=1):
            logging.info("record for commit %s. id: %s", row.target_model, self.id)
            payload = row.payload
            model = self.env[row.target_model]
            if row.target_model == "hp.version":
                payload["entry_id"] = summary_model.get("hp.entry", {}).get(row.payload["entry_id"])
            if row.target_model == "hp.version.property":
                payload["version_id"] = summary_model.get("hp.version", {}).get(row.payload["version_id"])
            logging.info("Creating record for model %s :: \nsummary: %s\n", row.target_model, summary_model)
            
            rec = model.create(payload)
            summary_model.setdefault(row.target_model, {}).setdefault(row.id, rec.id)
            row.write({"target_id": rec.id})
        
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
        logging.info("Starting atomic record creation for commit %s", self.id)
        staged = self.staged_ids
        total = len(staged)

        for model_name in self.model_order:
            logging.info("Processing model %s for commit %s", model_name, self.id)
            staged_for_model = staged.filtered(lambda r: r.target_model == model_name)
            if staged_for_model and len(staged_for_model) > 0:
                logging.info("Creating %d records for model %s in commit %s", len(staged_for_model), model_name, self.id)
                self._create_records(staged_for_model, summary_model)

        # wipe payloads only if everything succeeded
        self.write({"commit_summary": self._write_summary(summary_model)})
        #staged.write({"payload": "{}"})  # Clear payloads after successful commit