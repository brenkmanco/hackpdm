
import logging
import re

from odoo import api, fields, models
from odoo.exceptions import UserError

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
    def clear_commit(self, rec_id: int | list[int]):
        if isinstance(rec_id, list):
            rec_id = rec_id[0]
        record = self.browse([rec_id])
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


    @api.model
    def start_commit(self, commit_ids: int | list[int]):
        if isinstance(commit_ids, int):
            commit_ids = [commit_ids]
        records = self.browse(commit_ids)

        for record in records:
            if record.committing or record.committed or not record.staged_ids:
                continue

            try:
                # Savepoint isolates this record and rolls back all its children if it fails
                with self.env.cr.savepoint():
                    record.write({
                        "committing": True,
                        "committed_at": fields.Datetime.now(),
                    })
                    record.run_commit_job()
            except Exception as exc:
                logging.exception("Commit failed for record %s", record.id)
                record.handle_commit_failure(exc)

        return True

    def run_commit_job(self):
        self.ensure_one()

        # ATOMIC BLOCK — all or nothing
        self._create_all_records_atomically()

        # Mark success
        self.write({
            "committed": True,
            "committing": False,
            "commit_finished_at": fields.Datetime.now(),
        })


    def handle_commit_failure(self, exc):
        self.write({
            "errored": True,
            "message_exception": str(exc),
            "committing": False,
            "committed": False,
            "commit_finished_at": fields.Datetime.now(),
        })

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
            payload = row.payload or {}
            target_model = row.target_model

            # Clean audit fields from payload if present
            for f in ("create_uid", "create_date", "write_uid", "write_date"):
                payload.pop(f, None)

            # --------------------------------------------------------
            # 1. VALIDATIONS FOR hp.entry
            # --------------------------------------------------------
            if target_model == "hp.entry":
                entry_name = payload.get("name")
                dir_id = payload.get("dir_id")

                if not entry_name:
                    raise UserError("Cannot commit an entry without a file name.")

                # Check 1a: Directory existence and status
                if dir_id:
                    dir_rec = self.env["hp.directory"].browse(dir_id)
                    if not dir_rec.exists():
                        raise UserError(f"Directory with ID {dir_id} for entry '{entry_name}' does not exist.")
                    if dir_rec.deleted:
                        raise UserError(f"Directory '{dir_rec.complete_name}' for entry '{entry_name}' has been deleted.")

                # Check 1b: Entry name filter check (If / Ft)
                filters = self.env["hp.entry.name.filter"].search([])
                for flt in filters:
                    if flt.name_regex:
                        try:
                            if re.search(flt.name_regex, entry_name, re.IGNORECASE):
                                desc = flt.description or flt.name_regex
                                raise UserError(f"Entry '{entry_name}' matches filter pattern '{desc}' and cannot be committed.")
                        except re.error as e:
                            logging.warning("Invalid regex filter ID %s (%s): %s", flt.id, flt.name_regex, e)

                # Check 1c: Collision check (Lo vs existing)
                if dir_id and entry_name:
                    existing_entry = self.env["hp.entry"].search([
                        ("name", "=", entry_name),
                        ("dir_id", "=", dir_id),
                    ], limit=1)
                    if existing_entry:
                        if existing_entry.deleted:
                            raise UserError(
                                f"Entry '{entry_name}' already exists in directory '{dir_rec.complete_name}' "
                                f"but is marked as deleted (Dt)."
                            )
                        else:
                            raise UserError(
                                f"Entry '{entry_name}' already exists in directory '{dir_rec.complete_name}'. "
                                f"A version should be committed for the existing entry instead of creating a duplicate."
                            )

            # --------------------------------------------------------
            # 2. VALIDATIONS FOR hp.version
            # --------------------------------------------------------
            elif target_model == "hp.version":
                # Resolve entry_id (from summary_model if new entry, or from existing entry)
                if not payload.get("__internal__existing"):
                    staged_entry_id = payload.get("entry_id")
                    resolved_entry_id = summary_model.get("hp.entry", {}).get(staged_entry_id)
                    if not resolved_entry_id:
                        raise UserError(
                            f"Cannot commit version '{payload.get('name')}': Associated staged entry ID "
                            f"{staged_entry_id} was not created."
                        )
                    payload["entry_id"] = resolved_entry_id
                else:
                    del payload["__internal__existing"]

                entry_id = payload.get("entry_id")
                entry_rec = self.env["hp.entry"].browse(entry_id)
                if not entry_rec.exists():
                    raise UserError(f"Target entry ID {entry_id} does not exist.")

                # Check 2a: Deleted check (Dt)
                if entry_rec.deleted:
                    raise UserError(f"Cannot commit version: entry '{entry_rec.name}' is marked as deleted (Dt).")

                # Check 2b: Checkout check (Co vs Cm vs No checkout)
                # If entry is checked out to someone else, reject (Co)
                if entry_rec.checkout_user and entry_rec.checkout_user.id != self.env.uid:
                    raise UserError(
                        f"Cannot commit version for '{entry_rec.name}': "
                        f"The file is checked out to another user '{entry_rec.checkout_user.name}' ({entry_rec.checkout_user.login}) [Co]."
                    )
                # If entry is not checked out, allow commit and populate node_id if missing
                if not entry_rec.checkout_user:
                    if not payload.get("node_id") and self.node_by:
                        payload["node_id"] = self.node_by.id

            # --------------------------------------------------------
            # 3. VALIDATIONS FOR hp.version.property
            # --------------------------------------------------------
            elif target_model == "hp.version.property":
                staged_version_id = payload.get("version_id")
                resolved_version_id = summary_model.get("hp.version", {}).get(staged_version_id)
                if not resolved_version_id:
                    if not staged_version_id or not self.env["hp.version"].browse(staged_version_id).exists():
                        raise UserError(
                            f"Cannot create version property: Associated version ID {staged_version_id} was not found."
                        )
                    resolved_version_id = staged_version_id
                payload["version_id"] = resolved_version_id

            # --------------------------------------------------------
            # 4. SPECIAL HANDLING FOR hp.version.relationship
            # --------------------------------------------------------
            if target_model == "hp.version.relationship" and target_model not in self.env:
                version_info = payload.get("version", {})
                parent_staged_id = version_info.get("version_staged_id")
                parent_version_id = summary_model.get("hp.version", {}).get(parent_staged_id)
                dep_path = payload.get("path")

                if parent_version_id and dep_path:
                    parent_version = self.env["hp.version"].browse(parent_version_id)
                    dep_filename = dep_path.replace("\\", "/").split("/")[-1]
                    child_entry = self.env["hp.entry"].search([("name", "=", dep_filename), ("deleted", "=", False)], limit=1)
                    if child_entry and child_entry.latest_version_id:
                        parent_version.write({"child_ids": [(4, child_entry.latest_version_id.id)]})
                row.write({"payload": {"complete": True}})
                continue

            model = self.env[target_model]
            payload["commit_id"] = row.commit_id.id if row.commit_id else self.id
            rec = model.create(payload)
            summary_model.setdefault(target_model, {}).setdefault(row.id, rec.id)
            logging.info("\nCreated record for model %s :: summary: %s\n", target_model, summary_model)
            row.write({"target_id": rec.id, "payload": {"complete": True}})

            # --------------------------------------------------------
            # POST-CREATION: Un-checkout / Check-in the entry
            # --------------------------------------------------------
            if target_model == "hp.version":
                entry_rec = rec.entry_id
                if entry_rec:
                    entry_vals = {
                        "checkout_user": False,
                        "checkout_date": False,
                        "checkout_node": False,
                    }
                    if "is_checked_in" in entry_rec._fields:
                        entry_vals["is_checked_in"] = True
                    entry_rec.write(entry_vals)
        
    # ------------------------------------------------------------
    # ATOMIC CREATION — ONE BIG TRANSACTION
    # ------------------------------------------------------------
    def _create_all_records_atomically(self):
        """
        If ANY record creation fails, the entire transaction rolls back.
        """
        summary_model = {}
        logging.info("Starting atomic record creation for commit %s", self.id)
        staged = self.staged_ids
        total = len(staged)

        for model_name in self.model_order:
            logging.info("Processing model %s for commit %s", model_name, self.id)
            staged_for_model = staged.filtered(lambda r: r.target_model == model_name)
            if staged_for_model and len(staged_for_model) > 0:
                self._create_records(staged_for_model, summary_model)
                logging.info("Created %d records for model %s in commit %s", len(staged_for_model), model_name, self.id)
            else:
                logging.info("No records to create for model %s in commit %s", model_name, self.id)

        # wipe payloads only if everything succeeded
        self.write({"commit_summary": self._write_summary(summary_model)})
        #staged.write({"payload": "{}"})  # Clear payloads after successful commit