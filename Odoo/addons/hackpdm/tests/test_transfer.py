"""Large-file transfer: uploading a file in pieces (hp.upload), committing it as a version, and reading
a version's file back in pieces (hp.version.read_contents)."""
import base64
import hashlib
import os
from datetime import timedelta

from odoo import fields
from odoo.exceptions import AccessError, UserError
from odoo.tests import TransactionCase, tagged

from ..models.hp_upload import MAX_CHUNK_BYTES


def b64(data: bytes) -> str:
    return base64.b64encode(data).decode()


@tagged("post_install", "-at_install")
class TestTransfer(TransactionCase):

    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        users = cls.env["res.users"].with_context(no_reset_password=True)
        engineer = cls.env.ref("hackpdm.group_pdm_user")
        cls.alice = users.create({"name": "Alice Transfer", "login": "hp_tr_alice", "groups_id": [(6, 0, engineer.ids)]})
        cls.bob = users.create({"name": "Bob Transfer", "login": "hp_tr_bob", "groups_id": [(6, 0, engineer.ids)]})
        cls.viewer = users.create({"name": "Viewer Transfer", "login": "hp_tr_viewer",
                                   "groups_id": [(6, 0, cls.env.ref("hackpdm.group_pdm_viewer").ids)]})
        cls.node = cls.env["hp.node"].create({"name": "hp-tr-node"})
        cls.folder = cls.env["hp.directory"].create({"name": "hp_tr_tests"})
        # more than one chunk, and not a multiple of the chunk size
        cls.big = os.urandom(MAX_CHUNK_BYTES + 12345)

    # ------------------------------------------------------------ helpers

    def _next_rpc(self):
        self.env.flush_all()
        self.env.invalidate_all()

    def _upload(self, user, data, finish=True):
        Upload = self.env["hp.upload"].with_user(user)
        started = Upload.upload_begin(len(data), hashlib.sha1(data).hexdigest())
        offset = 0
        while offset < len(data):
            piece = data[offset:offset + started["chunk_size"]]
            offset = Upload.upload_chunk(started["id"], offset, b64(piece))
            self._next_rpc()
        if finish:
            Upload.upload_finish(started["id"])
            self._next_rpc()
        return started["id"]

    def _commit_new_file(self, user, name, upload_id):
        commit = self.env["hp.pdm.commit"].with_user(user).create({"name": "transfer", "node_by": self.node.id})
        Staged = self.env["hp.record.staged"].with_user(user)
        entry_row = Staged.create({"commit_id": commit.id, "committing_id": commit.id, "target_model": "hp.entry",
                                   "payload": {"name": name, "deleted": False, "dir_id": self.folder.id}})
        Staged.create({"commit_id": commit.id, "committing_id": commit.id, "target_model": "hp.version",
                       "payload": {"name": name, "dir_id": self.folder.id, "entry_id": entry_row.id,
                                   "node_id": self.node.id, "upload_id": upload_id}})
        self._next_rpc()
        result = self.env["hp.pdm.commit"].with_user(user).start_commit([commit.id])[0]
        self._next_rpc()
        return result

    def _read_all(self, user, version_id, length):
        out, offset = b"", 0
        while True:
            piece = self.env["hp.version"].with_user(user).read_contents(version_id, offset, length)
            data = base64.b64decode(piece["data"])
            if not data:
                return out, piece
            out += data
            offset += len(data)

    # ------------------------------------------------------------ upload and commit

    def test_large_file_round_trip(self):
        upload_id = self._upload(self.alice, self.big)
        result = self._commit_new_file(self.alice, "big.bin", upload_id)
        self.assertTrue(result["committed"], result["message_exception"])

        version = self.env["hp.entry"].search([("name", "=", "big.bin"), ("dir_id", "=", self.folder.id)]).latest_version_id
        self.assertEqual(version.file_size, len(self.big))
        self.assertEqual(version.checksum, hashlib.sha1(self.big).hexdigest())
        self.assertEqual(base64.b64decode(version.file_contents), self.big)
        self.assertEqual(self.env["hp.upload"].sudo().browse(upload_id).state, "used")
        # stored in the filestore under its SHA-1, as Odoo stores attachments, and not text-indexed
        attachment = version.sudo().attachment_id
        self.assertEqual(attachment.store_fname, f"{version.checksum[:2]}/{version.checksum}")
        self.assertTrue(os.path.isfile(attachment._full_path(attachment.store_fname)))
        self.assertFalse(attachment.index_content)

        # read back in pieces, by anyone who may read the vault
        got, last = self._read_all(self.viewer, version.id, 3 * 1024 * 1024)
        self.assertEqual(got, self.big)
        self.assertEqual(last["size"], len(self.big))
        self.assertEqual(last["checksum"], version.checksum)

    def test_text_file_upload_is_not_indexed(self):
        # Odoo indexes a text attachment by scanning it all in memory; an upload skips that
        data = b"plain text words " * 1000
        result = self._commit_new_file(self.alice, "notes.txt", self._upload(self.alice, data))
        self.assertTrue(result["committed"], result["message_exception"])
        version = self.env["hp.entry"].search([("name", "=", "notes.txt"), ("dir_id", "=", self.folder.id)]).latest_version_id
        self.assertFalse(version.sudo().attachment_id.index_content)
        self.assertEqual(base64.b64decode(version.file_contents), data)

    def test_same_file_twice_shares_the_stored_copy(self):
        data = b"identical contents"
        for name in ("one.bin", "two.bin"):
            self.assertTrue(self._commit_new_file(self.alice, name, self._upload(self.alice, data))["committed"])
        versions = self.env["hp.entry"].search([("name", "in", ["one.bin", "two.bin"])]).latest_version_id
        self.assertEqual(len(set(versions.sudo().attachment_id.mapped("store_fname"))), 1)
        for version in versions:
            self.assertEqual(base64.b64decode(version.file_contents), data)

    def test_empty_file_upload(self):
        upload_id = self._upload(self.alice, b"")
        result = self._commit_new_file(self.alice, "empty.bin", upload_id)
        self.assertTrue(result["committed"], result["message_exception"])

    def test_retried_or_out_of_order_chunk_is_ignored(self):
        data = b"0123456789" * 10
        Upload = self.env["hp.upload"].with_user(self.alice)
        upload_id = Upload.upload_begin(len(data), hashlib.sha1(data).hexdigest())["id"]
        self.assertEqual(Upload.upload_chunk(upload_id, 0, b64(data[:40])), 40)
        # the same piece again (a retry after a lost reply) changes nothing
        self.assertEqual(Upload.upload_chunk(upload_id, 0, b64(data[:40])), 40)
        # a piece past a gap is not written: the reply says where to continue
        self.assertEqual(Upload.upload_chunk(upload_id, 60, b64(data[60:])), 40)
        with self.assertRaises(UserError):
            Upload.upload_finish(upload_id)
        self.assertEqual(Upload.upload_chunk(upload_id, 40, b64(data[40:])), len(data))
        with self.assertRaises(UserError):
            Upload.upload_chunk(upload_id, len(data), b64(b"x"))
        self.assertTrue(Upload.upload_finish(upload_id))
        with self.assertRaises(UserError):
            Upload.upload_chunk(upload_id, 0, b64(data[:1]))

    def test_damaged_upload_is_refused(self):
        data = b"what was sent"
        Upload = self.env["hp.upload"].with_user(self.alice)
        upload_id = Upload.upload_begin(len(data), hashlib.sha1(b"something else!").hexdigest())["id"]
        Upload.upload_chunk(upload_id, 0, b64(data))
        self.assertFalse(Upload.upload_finish(upload_id))
        upload = self.env["hp.upload"].sudo().browse(upload_id)
        self.assertEqual((upload.state, upload.received), ("open", 0))
        # resent from the start (here still not matching): the first bytes are accepted again
        self.assertEqual(Upload.upload_chunk(upload_id, 0, b64(data)), len(data))
        result = self._commit_new_file(self.alice, "damaged.bin", upload_id)
        self.assertFalse(result["committed"])

    def test_bad_arguments_are_refused(self):
        Upload = self.env["hp.upload"].with_user(self.alice)
        for size, checksum in ((-1, "a" * 40), (2**31, "a" * 40), (10, "nothex"), ("10", "a" * 40)):
            with self.assertRaises(UserError):
                Upload.upload_begin(size, checksum)
        upload_id = Upload.upload_begin(10, "a" * 40)["id"]
        with self.assertRaises(UserError):
            Upload.upload_chunk(upload_id, 0, "not base64!")
        with self.assertRaises(UserError):
            Upload.upload_chunk(upload_id, 0, b64(b"x" * (MAX_CHUNK_BYTES + 1)))

    def test_unfinished_upload_cannot_be_committed(self):
        upload_id = self._upload(self.alice, b"half", finish=False)
        result = self._commit_new_file(self.alice, "half.bin", upload_id)
        self.assertFalse(result["committed"])
        self.assertIn("not a finished upload", result["message_exception"])

    def test_upload_can_be_committed_once(self):
        upload_id = self._upload(self.alice, b"once")
        self.assertTrue(self._commit_new_file(self.alice, "once.bin", upload_id)["committed"])
        again = self._commit_new_file(self.alice, "twice.bin", upload_id)
        self.assertFalse(again["committed"])

    # ------------------------------------------------------------ other users

    def test_cannot_use_someone_elses_upload(self):
        upload_id = self._upload(self.alice, b"alice's file", finish=False)
        Bob = self.env["hp.upload"].with_user(self.bob)
        with self.assertRaises(AccessError):
            Bob.upload_chunk(upload_id, 0, b64(b"bob"))
        with self.assertRaises(AccessError):
            Bob.upload_finish(upload_id)
        self.assertFalse(Bob.search([("id", "=", upload_id)]))
        self.env["hp.upload"].with_user(self.alice).upload_finish(upload_id)
        self._next_rpc()
        result = self._commit_new_file(self.bob, "stolen.bin", upload_id)
        self.assertFalse(result["committed"])

    def test_viewer_cannot_upload(self):
        with self.assertRaises(AccessError):
            self.env["hp.upload"].with_user(self.viewer).upload_begin(1, "a" * 40)

    def test_cannot_swap_a_versions_file(self):
        mine = self.env["hp.entry"].create({"name": "fixed.txt", "dir_id": self.folder.id})
        version = self.env["hp.version"].create({"entry_id": mine.id, "file_contents": b64(b"original")})
        other = self.env["ir.attachment"].create({"name": "other", "raw": b"other"})
        with self.assertRaises(AccessError):
            version.with_user(self.alice).write({"attachment_id": other.id})

    # ------------------------------------------------------------ reading

    def test_read_contents_bounds(self):
        entry = self.env["hp.entry"].create({"name": "small.txt", "dir_id": self.folder.id})
        version = self.env["hp.version"].create({"entry_id": entry.id, "file_contents": b64(b"small file")})
        Version = self.env["hp.version"].with_user(self.alice)
        self.assertEqual(base64.b64decode(Version.read_contents(version.id, 6, 100)["data"]), b"file")
        self.assertEqual(Version.read_contents(version.id, 1000, 10)["data"], "")
        for bad in ((version.id, -1, 10), (version.id, 0, -5), ("1", 0, 10), (999999999, 0, 10)):
            with self.assertRaises(UserError):
                Version.read_contents(*bad)

    def test_old_uploads_are_removed(self):
        stale = self._upload(self.alice, b"forgotten", finish=False)
        fresh = self._upload(self.alice, b"in progress", finish=False)
        Upload = self.env["hp.upload"].sudo()
        path = Upload.browse(stale)._spool_path()
        self.env.cr.execute("UPDATE hp_upload SET create_date = %s WHERE id = %s",
                            [fields.Datetime.now() - timedelta(days=2), stale])
        self._next_rpc()
        Upload._gc_uploads()
        self.assertFalse(Upload.browse(stale).exists())
        self.assertFalse(os.path.exists(path))
        self.assertTrue(Upload.browse(fresh).exists())
