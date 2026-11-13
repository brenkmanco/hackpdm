"""Chunked file uploads, so a large file never has to fit in one JSON-RPC request.

The client calls :meth:`hp_upload.upload_begin` with the file's size and SHA-1, sends the file in
pieces with :meth:`hp_upload.upload_chunk`, and :meth:`hp_upload.upload_finish` checks the size and
checksum. A staged hp.version then carries ``upload_id`` instead of ``file_contents``, and the commit
stores the spooled file as the version's attachment (:meth:`hp_upload._attach_to`).

Chunks are appended to a spool file in the database's filestore, not to the database, so a file of
hundreds of megabytes costs no table bloat. Spool files of uploads that were never committed are
removed by :meth:`hp_upload._gc_uploads` (a daily scheduled action).
"""
import base64
import binascii
import hashlib
import logging
import os
import re
import secrets
from datetime import timedelta

from odoo import api, fields, models
from odoo.exceptions import AccessError, UserError
from odoo.tools import config

from . import hp_guard

_logger = logging.getLogger(__name__)

# largest piece one upload_chunk / read_contents call moves, in bytes (before base64)
MAX_CHUNK_BYTES = 8 * 1024 * 1024
# ir.attachment.file_size is a 32-bit integer
MAX_FILE_BYTES = 2**31 - 1
# uploads not committed within this long are removed
UPLOAD_LIFETIME = timedelta(days=1)
_SHA1 = re.compile(r"^[0-9a-f]{40}$")


class hp_upload(models.Model):
    _name = "hp.upload"
    _description = "file being uploaded in pieces"

    size = fields.Integer(string="file size", required=True, readonly=True)
    checksum = fields.Char(string="expected SHA-1", required=True, readonly=True)
    received = fields.Integer(string="bytes received", default=0, readonly=True)
    state = fields.Selection(
        [("open", "Receiving"), ("done", "Verified"), ("used", "Committed")],
        default="open", required=True, readonly=True)
    token = fields.Char(readonly=True, copy=False)

    # ------------------------------------------------------------------ client API

    @api.model
    def upload_begin(self, size, checksum):
        """Starts an upload.

        Args:
            size (int): The file's size in bytes.
            checksum (str): The file's SHA-1, lower-case hex.

        Returns:
            dict: id (the upload to send chunks to) and chunk_size (the largest chunk accepted).

        Raises:
            UserError: The size or checksum is invalid, or the file is too large.
        """
        if not isinstance(size, int) or isinstance(size, bool) or size < 0:
            raise UserError(f"upload_begin: invalid size {size!r}")
        if size > MAX_FILE_BYTES:
            raise UserError(f"Files larger than {MAX_FILE_BYTES} bytes cannot be stored.")
        checksum = (checksum or "").lower()
        if not _SHA1.match(checksum):
            raise UserError("upload_begin: checksum must be a SHA-1 in hex")
        upload = self.create({"size": size, "checksum": checksum, "token": secrets.token_hex(16)})
        upload._spool_path(create_dir=True)
        open(upload._spool_path(), "wb").close()
        return {"id": upload.id, "chunk_size": MAX_CHUNK_BYTES}

    @api.model
    def upload_chunk(self, upload_id, offset, data):
        """Appends one piece of the file.

        Only a piece starting exactly at the bytes received so far is written. Any other piece is
        ignored: one already received (a retry after a lost reply) or one past a gap (an earlier piece
        whose call failed). Either way the bytes received so far are returned, and the client always
        continues from there.

        Args:
            upload_id (int): The upload from :meth:`upload_begin`.
            offset (int): Where in the file this piece starts.
            data (str): The piece, base64-encoded; at most chunk_size bytes once decoded.

        Returns:
            int: The bytes received so far.

        Raises:
            UserError: The upload is unknown or finished, or the piece is not base64, too large, or runs
                past the file's size.
        """
        upload = self._own_open_upload(upload_id)
        if not isinstance(offset, int) or offset < 0:
            raise UserError(f"upload_chunk: invalid offset {offset!r}")
        if offset != upload.received:
            return upload.received
        try:
            raw = base64.b64decode(data or "", validate=True)
        except (binascii.Error, ValueError, TypeError):
            raise UserError("upload_chunk: data is not base64")
        if len(raw) > MAX_CHUNK_BYTES:
            raise UserError(f"upload_chunk: a chunk may hold at most {MAX_CHUNK_BYTES} bytes")
        if offset + len(raw) > upload.size:
            raise UserError("upload_chunk: more data than the file's size")
        with open(upload._spool_path(), "r+b") as spool:
            # bytes past `received` belong to a call whose transaction was rolled back
            spool.truncate(offset)
            spool.seek(offset)
            spool.write(raw)
        upload.sudo().write({"received": offset + len(raw)})
        return offset + len(raw)

    @api.model
    def upload_finish(self, upload_id):
        """Checks that the whole file arrived intact.

        Args:
            upload_id (int): The upload from :meth:`upload_begin`.

        Returns:
            bool: True when the file is intact; the upload can then be committed (``upload_id`` in a
            staged hp.version). False when it arrived damaged (checksum mismatch): the upload is reset
            to 0 bytes received, and the client sends the file again.

        Raises:
            UserError: Bytes are missing.
        """
        upload = self._own_open_upload(upload_id)
        if upload.received != upload.size:
            raise UserError(f"upload_finish: received {upload.received} of {upload.size} bytes")
        if upload._spool_checksum() != upload.checksum:
            # not raised: an error would roll back this reset too
            _logger.warning("upload %s arrived damaged; reset for a resend", upload.id)
            upload.sudo().write({"received": 0})
            return False
        upload.sudo().write({"state": "done"})
        return True

    # ------------------------------------------------------------------ commit

    def _attach_to(self, version):
        """Stores this verified upload as ``version``'s file and removes the spool file.

        Called by the commit, after the version was created. The caller must have checked that the
        upload belongs to the committer (:meth:`_committable`).
        """
        self.ensure_one()
        with open(self._spool_path(), "rb") as spool:
            raw = spool.read()
        attachment = self.env["ir.attachment"].sudo().create({
            "name": f"{version.id}.{version.name}",
            "type": "binary",
            "raw": raw,
            "res_model": version._name,
            "res_field": "file_contents",
            "res_id": version.id,
        })
        version.sudo().write({"attachment_id": attachment.id})
        self.sudo().write({"state": "used"})
        # only once the commit is saved: a commit rolled back after this still has its spool file
        path = self._spool_path()
        self.env.cr.postcommit.add(lambda: _remove_file(path))
        return attachment

    @api.model
    def _committable(self, upload_id):
        """The verified, unused upload ``upload_id`` of the current user."""
        upload = self.sudo().browse(upload_id).exists() if isinstance(upload_id, int) else self.browse()
        if not upload or upload.state != "done":
            raise UserError(f"Upload {upload_id!r} is not a finished upload.")
        if upload.create_uid.id != self.env.uid and not hp_guard.is_pdm_admin(self.env):
            raise AccessError("You can only commit your own uploads.")
        return upload

    # ------------------------------------------------------------------ housekeeping

    @api.model
    def _gc_uploads(self):
        """Removes uploads (and spool files) older than a day that were never committed, and committed
        upload records. Run daily by a scheduled action."""
        stale = self.sudo().search([
            "|", ("state", "=", "used"), ("create_date", "<", fields.Datetime.now() - UPLOAD_LIFETIME)])
        for upload in stale:
            upload._remove_spool()
        stale.unlink()
        return len(stale)

    def _own_open_upload(self, upload_id):
        upload = self.browse(upload_id).exists() if isinstance(upload_id, int) else self.browse()
        if not upload:
            raise UserError(f"Upload {upload_id!r} does not exist.")
        # record rules already hide other users' uploads; this also covers administrators
        if upload.create_uid.id != self.env.uid and not self.env.su:
            raise AccessError("You can only add to your own uploads.")
        # one chunk at a time per upload, even across server workers; read its state after the lock
        self.env.cr.execute("SELECT id FROM hp_upload WHERE id = %s FOR UPDATE", [upload.id])
        upload.invalidate_recordset(["received", "state"])
        if upload.state != "open":
            raise UserError(f"Upload {upload_id} is already finished.")
        return upload

    def _spool_path(self, create_dir=False):
        self.ensure_one()
        directory = os.path.join(config.filestore(self.env.cr.dbname), "hackpdm_uploads")
        if create_dir:
            os.makedirs(directory, exist_ok=True)
        return os.path.join(directory, f"{self.id}-{self.token}")

    def _spool_checksum(self):
        sha1 = hashlib.sha1()
        with open(self._spool_path(), "rb") as spool:
            for block in iter(lambda: spool.read(1024 * 1024), b""):
                sha1.update(block)
        return sha1.hexdigest()

    def _remove_spool(self):
        _remove_file(self._spool_path())


def _remove_file(path):
    try:
        os.remove(path)
    except FileNotFoundError:
        pass
    except OSError as exc:
        _logger.warning("could not remove upload spool %s: %s", path, exc)
