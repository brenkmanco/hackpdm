"""Server-side safeguards: run with
odoo -d <db> -i hackpdm --test-enable --test-tags /hackpdm --stop-after-init"""
from psycopg2 import IntegrityError

from odoo.exceptions import AccessError, UserError, ValidationError
from odoo.tests import TransactionCase, tagged
from odoo.tools import mute_logger

from ..models import hp_guard


@tagged("post_install", "-at_install")
class TestSafeguards(TransactionCase):

    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        users = cls.env["res.users"].with_context(no_reset_password=True)
        group_user = cls.env.ref("base.group_user")
        cls.alice = users.create({"name": "Alice Safeguard", "login": "hp_alice", "groups_id": [(6, 0, group_user.ids)]})
        cls.bob = users.create({"name": "Bob Safeguard", "login": "hp_bob", "groups_id": [(6, 0, group_user.ids)]})
        cls.node = cls.env["hp.node"].create({"name": "hp-safeguard-node"})
        cls.folder = cls.env["hp.directory"].create({"name": "hp_safeguard_tests"})
        cls.entry = cls.env["hp.entry"].create({"name": "bracket.sldprt", "dir_id": cls.folder.id})
        cls.version = cls.env["hp.version"].create({"entry_id": cls.entry.id})

    def _checkout_as(self, user):
        self.env["hp.version"].with_user(user).checkout_entry(self.version.id, self.node.id)
        self._as_next_rpc()

    def _as_next_rpc(self):
        # each client call is its own transaction: write everything, then read back from the database
        self.env.flush_all()
        self.env.invalidate_all()

    # --- names ------------------------------------------------------------

    def test_name_rules(self):
        for bad in ["", "   ", ".", "..", "a/b", "..\\..\\evil.dll", "x:y", "what?", "tab\there",
                    "trailing.", "trailing ", "CON", "nul.txt", "Com1.sldprt", "a" * 256]:
            self.assertTrue(hp_guard.name_problem(bad), f"{bad!r} should be refused")
        for good in ["bracket.sldprt", "Plate (rev 2).SLDDRW", "COM10", ".hidden", "ünïcode.txt"]:
            self.assertIsNone(hp_guard.name_problem(good), f"{good!r} should be accepted")

    def test_entry_and_folder_names_are_path_safe(self):
        Entry = self.env["hp.entry"]
        with self.assertRaises(ValidationError):
            Entry.create({"name": "..\\..\\escape.txt", "dir_id": self.folder.id})
        with self.assertRaises(ValidationError):
            Entry.create({"name": "AUX.sldprt", "dir_id": self.folder.id})
        with self.assertRaises(ValidationError):
            self.entry.write({"name": "sub/dir.txt"})
        with self.assertRaises(ValidationError):
            self.env["hp.directory"].create({"name": "..", "parent_id": self.folder.id})

    def test_folder_cannot_be_moved_into_itself(self):
        child = self.env["hp.directory"].create({"name": "child", "parent_id": self.folder.id})
        # Odoo's parent store refuses the loop (UserError "Recursion Detected.")
        with self.assertRaises(UserError):
            self.folder.write({"parent_id": child.id})

    # --- uniqueness -----------------------------------------------------------

    @mute_logger("odoo.sql_db")
    def test_entry_names_unique_per_folder_ignoring_case(self):
        with self.assertRaises(IntegrityError):
            self.env["hp.entry"].create({"name": "BRACKET.SLDPRT", "dir_id": self.folder.id})

    def test_same_name_in_another_folder_is_fine(self):
        other = self.env["hp.directory"].create({"name": "other", "parent_id": self.folder.id})
        self.env["hp.entry"].create({"name": "bracket.sldprt", "dir_id": other.id})

    @mute_logger("odoo.sql_db")
    def test_folder_names_unique_ignoring_case(self):
        self.env["hp.directory"].create({"name": "Parts", "parent_id": self.folder.id})
        with self.assertRaises(IntegrityError):
            self.env["hp.directory"].create({"name": "PARTS", "parent_id": self.folder.id})

    def test_commit_refuses_duplicate_name_ignoring_case(self):
        with self.assertRaises(UserError):
            self.env["hp.pdm.commit"]._validate_entry_payload({"name": "Bracket.SLDPRT", "dir_id": self.folder.id})

    def test_commit_name_check_treats_like_wildcards_literally(self):
        # "_" and "%" are LIKE wildcards: "bracket_sldprt" must not collide with "bracket.sldprt"
        self.env["hp.pdm.commit"]._validate_entry_payload({"name": "bracket_sldprt", "dir_id": self.folder.id})
        self.env["hp.pdm.commit"]._validate_entry_payload({"name": "%.sldprt", "dir_id": self.folder.id})

    # --- checkout -------------------------------------------------------------

    def test_checkout_holder_keeps_control(self):
        self._checkout_as(self.alice)
        self.assertEqual(self.entry.checkout_user, self.alice)
        as_bob = self.entry.with_user(self.bob)
        for vals in ({"is_checked_out": False, "checkout_user": False},
                     {"deleted": True},
                     {"name": "renamed.sldprt"}):
            with self.assertRaises(UserError):
                as_bob.write(vals)
        with self.assertRaises(UserError):
            self._checkout_as(self.bob)
        # the holder can release it, and an administrator can always step in
        self.entry.with_user(self.alice).write({"is_checked_out": False, "checkout_user": False})
        self._as_next_rpc()
        self._checkout_as(self.bob)
        self.entry.write({"is_checked_out": False, "checkout_user": False})
        self._as_next_rpc()
        self.assertFalse(self.entry.checkout_user)

    def test_cannot_check_out_in_someone_elses_name(self):
        with self.assertRaises(AccessError):
            self.entry.with_user(self.bob).write({"is_checked_out": True, "checkout_user": self.alice.id})

    def test_deleted_entry_cannot_be_checked_out(self):
        self.entry.write({"deleted": True})
        with self.assertRaises(UserError):
            self._checkout_as(self.alice)
        with self.assertRaises(UserError):
            self.entry.with_user(self.alice).write({"is_checked_out": True, "checkout_user": self.alice.id})

    # --- versions -------------------------------------------------------------

    def test_no_new_version_on_entry_checked_out_to_another_user(self):
        self._checkout_as(self.alice)
        with self.assertRaises(UserError):
            self.env["hp.version"].with_user(self.bob).create({"entry_id": self.entry.id})
        self.env["hp.version"].with_user(self.alice).create({"entry_id": self.entry.id})

    def test_no_new_version_on_deleted_entry(self):
        self.entry.write({"deleted": True})
        with self.assertRaises(UserError):
            self.env["hp.version"].with_user(self.alice).create({"entry_id": self.entry.id})

    def test_committed_version_is_immutable_for_users(self):
        other = self.env["hp.entry"].create({"name": "plate.sldprt", "dir_id": self.folder.id})
        as_bob = self.version.with_user(self.bob)
        with self.assertRaises(AccessError):
            as_bob.write({"entry_id": other.id})
        with self.assertRaises(AccessError):
            as_bob.write({"file_contents": b"aGVsbG8="})
        # so do its dependency links and the computer that committed it; only an administrator
        # clears the links (the first step of a permanent delete)
        with self.assertRaises(AccessError):
            as_bob.write({"child_ids": [(5, 0, 0)], "parent_ids": [(5, 0, 0)]})
        with self.assertRaises(AccessError):
            as_bob.write({"node_id": self.node.id})
        self.version.write({"child_ids": [(5, 0, 0)], "parent_ids": [(5, 0, 0)]})

    def test_version_cannot_depend_on_itself(self):
        with self.assertRaises(ValidationError):
            self.version.write({"child_ids": [(4, self.version.id)]})

    # --- commits --------------------------------------------------------------

    def test_commit_belongs_to_its_creator(self):
        commit = self.env["hp.pdm.commit"].with_user(self.alice).create({"name": "alice's commit"})
        Commit = self.env["hp.pdm.commit"].with_user(self.bob)
        with self.assertRaises(AccessError):
            Commit.start_commit([commit.id])
        with self.assertRaises(AccessError):
            Commit.clear_commit(commit.id)
        self.env["hp.pdm.commit"].with_user(self.alice).start_commit([commit.id])
        self.env["hp.pdm.commit"].with_user(self.alice).clear_commit(commit.id)

    def test_missing_commit_is_reported(self):
        with self.assertRaises(UserError):
            self.env["hp.pdm.commit"].clear_commit(987654321)
        self.assertEqual(self.env["hp.pdm.commit"].start_commit([987654321]), [])

    # --- reads ----------------------------------------------------------------

    def test_smart_read_without_fields_returns_ids(self):
        rows = self.env["hp.entry"].smart_read([("id", "=", self.entry.id)], [], None)
        self.assertEqual(rows, [{"id": self.entry.id}])

    def test_smart_read_rejects_unknown_fields(self):
        with self.assertRaises(UserError):
            self.env["hp.entry"].smart_read([("id", "=", self.entry.id)], [], ["dir_id.no_such_field"])
        with self.assertRaises(UserError):
            self.env["hp.entry"].related_browse([self.entry.id], "no_such_field", ["id"])

    def test_fast_read_goes_through_the_orm(self):
        rows = self.env["hp.entry"].with_user(self.bob).fast_read([self.entry.id], ["name", "deleted", "bogus"])
        self.assertEqual(rows, {str(self.entry.id): {"name": "bracket.sldprt", "deleted": False}})

    # --- maintenance ----------------------------------------------------------

    def test_maintenance_jobs_are_admin_only(self):
        Version = self.env["hp.version"].with_user(self.bob)
        for job in ("migrate_webdav_files_to_records", "migrate_image_from_database", "display_all_fnames", "cleanup_filestore"):
            with self.assertRaises(AccessError, msg=job):
                getattr(Version, job)()
        with self.assertRaises(AccessError):
            self.env["hp.entry"].with_user(self.bob).migrate_webdav_files_to_records()
