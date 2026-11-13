"""Attempts to break the vault through the same RPC methods the client uses, as a regular user
(not an administrator: those bypass the user-level guards). Each test is an attack that must be
refused, plus, where it matters, the legitimate call next to it that must still work."""
import base64

from odoo.exceptions import AccessError, UserError, ValidationError
from odoo.tests import TransactionCase, tagged
from odoo.tools import mute_logger


def b64(data: bytes) -> str:
    return base64.b64encode(data).decode()


REFUSED = (AccessError, UserError, ValidationError)


@tagged("post_install", "-at_install")
class TestAttacks(TransactionCase):

    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        users = cls.env["res.users"].with_context(no_reset_password=True)
        group_user = cls.env.ref("hackpdm.group_pdm_user")
        cls.alice = users.create({"name": "Alice Attack", "login": "hp_atk_alice", "groups_id": [(6, 0, group_user.ids)]})
        cls.bob = users.create({"name": "Bob Attack", "login": "hp_atk_bob", "groups_id": [(6, 0, group_user.ids)]})
        cls.node = cls.env["hp.node"].create({"name": "hp-atk-node"})
        cls.other_node = cls.env["hp.node"].create({"name": "hp-atk-other-node"})
        cls.prop = cls.env["hp.property"].create({"name": "AtkMaterial", "prop_type": "text"})
        cls.folder = cls.env["hp.directory"].create({"name": "hp_atk_tests"})
        cls.part = cls.env["hp.entry"].create({"name": "part.sldprt", "dir_id": cls.folder.id})
        cls.part_v1 = cls.env["hp.version"].create(
            {"entry_id": cls.part.id, "file_contents": b64(b"part v1"), "node_id": cls.node.id})
        cls.asm = cls.env["hp.entry"].create({"name": "asm.sldasm", "dir_id": cls.folder.id})
        cls.asm_v1 = cls.env["hp.version"].create({"entry_id": cls.asm.id, "file_contents": b64(b"asm v1")})
        cls.asm_v1.write({"child_ids": [(4, cls.part_v1.id)]})
        cls.env["hp.version.property"].create(
            {"version_id": cls.part_v1.id, "prop_id": cls.prop.id, "text_value": "steel"})

    # ------------------------------------------------------------ helpers

    def _next_rpc(self):
        self.env.flush_all()
        self.env.invalidate_all()

    def _checkout(self, user, version):
        self.env["hp.version"].with_user(user).checkout_entry(version.id, self.node.id)
        self._next_rpc()

    def _commit(self, user):
        return self.env["hp.pdm.commit"].with_user(user).create({"name": "attack", "node_by": self.node.id})

    def _stage(self, user, commit, model, payload, committing=None):
        return self.env["hp.record.staged"].with_user(user).create({
            "commit_id": commit.id, "committing_id": (committing or commit).id,
            "target_model": model, "payload": payload})

    @mute_logger("root")
    def _run(self, user, commit):
        self._next_rpc()
        result = self.env["hp.pdm.commit"].with_user(user).start_commit([commit.id])
        self._next_rpc()
        return result[0]

    def _assert_refused(self, attempt):
        try:
            with self.env.cr.savepoint():
                attempt()
        except REFUSED:
            return
        self.fail("the attempt was not refused")

    def _new_version_payload(self, entry, data=b"changed", **extra):
        return dict({"__internal__existing": True, "name": entry.name, "entry_id": entry.id,
                     "node_id": self.node.id, "file_contents": b64(data)}, **extra)

    # ------------------------------------------------------------ commits

    def test_commit_new_version_of_a_file_nobody_checked_out(self):
        """The client refuses this (\"not checked out to current user\"); the server must too."""
        commit = self._commit(self.bob)
        self._stage(self.bob, commit, "hp.version", self._new_version_payload(self.part))
        result = self._run(self.bob, commit)
        self.assertTrue(result["errored"], "a version was committed to a file Bob never checked out")
        self.assertIn("not checked out", result["message_exception"])
        self.assertEqual(self.part.version_ids, self.part_v1)

    def test_commit_refuses_a_version_without_contents(self):
        commit = self._commit(self.bob)
        row = self._stage(self.bob, commit, "hp.entry", {"name": "empty-handed.txt", "dir_id": self.folder.id})
        self._stage(self.bob, commit, "hp.version", {"name": "empty-handed.txt", "entry_id": row.id, "file_contents": None})
        result = self._run(self.bob, commit)
        self.assertTrue(result["errored"])
        self.assertIn("contents are missing", result["message_exception"])
        self.assertFalse(self.env["hp.entry"].search([("name", "=", "empty-handed.txt")]))
        # an empty file is a file
        commit = self._commit(self.bob)
        row = self._stage(self.bob, commit, "hp.entry", {"name": "empty.txt", "dir_id": self.folder.id})
        self._stage(self.bob, commit, "hp.version", {"name": "empty.txt", "entry_id": row.id, "file_contents": ""})
        self.assertTrue(self._run(self.bob, commit)["committed"])

    def test_create_version_directly_on_a_file_nobody_checked_out(self):
        with self.assertRaises(UserError):
            self.env["hp.version"].with_user(self.bob).create({"entry_id": self.part.id, "file_contents": b64(b"x")})

    def test_first_version_of_a_new_file_needs_no_checkout(self):
        entry = self.env["hp.entry"].with_user(self.bob).create({"name": "new.txt", "dir_id": self.folder.id})
        self.env["hp.version"].with_user(self.bob).create({"entry_id": entry.id, "file_contents": b64(b"new")})
        # but a second one does
        with self.assertRaises(UserError):
            self.env["hp.version"].with_user(self.alice).create({"entry_id": entry.id, "file_contents": b64(b"2")})

    def test_new_version_cannot_carry_someone_elses_checkout(self):
        """A version created with checkout fields would make the file look checked out to anyone."""
        self._checkout(self.bob, self.part_v1)
        commit = self._commit(self.bob)
        self._stage(self.bob, commit, "hp.version",
                    self._new_version_payload(self.part, checkout_user=self.alice.id, checkout_node=self.other_node.id))
        result = self._run(self.bob, commit)
        if not result["errored"]:
            self.assertFalse(self.part.checkout_user, "the commit left the file checked out to Alice")
        new = self.env["hp.entry"].with_user(self.bob).create({"name": "locked.txt", "dir_id": self.folder.id})
        with self.assertRaises(AccessError):
            self.env["hp.version"].with_user(self.bob).create(
                {"entry_id": new.id, "file_contents": b64(b"x"), "checkout_user": self.alice.id})

    def test_cannot_stage_rows_into_someone_elses_commit(self):
        """Alice's commit runs with Alice's rights: rows Bob slips into it would be created as her."""
        alices = self._commit(self.alice)
        bobs = self._commit(self.bob)
        with self.assertRaises(AccessError):
            self._stage(self.bob, bobs, "hp.entry", {"name": "planted.txt", "dir_id": self.folder.id}, committing=alices)
        row = self._stage(self.alice, alices, "hp.entry", {"name": "honest.txt", "dir_id": self.folder.id})
        self._next_rpc()
        with self.assertRaises(AccessError):
            row.with_user(self.bob).write({"payload": {"name": "swapped.txt", "dir_id": self.folder.id}})
        with self.assertRaises(AccessError):
            row.with_user(self.bob).write({"committing_id": bobs.id})

    def test_cannot_change_someone_elses_commit(self):
        alices = self._commit(self.alice)
        self._next_rpc()
        with self.assertRaises(AccessError):
            alices.with_user(self.bob).write({"committed": True, "errored": True, "message_exception": "nope"})

    def test_committed_commit_cannot_be_cleared(self):
        commit = self._commit(self.alice)
        self._stage(self.alice, commit, "hp.entry", {"name": "done.txt", "dir_id": self.folder.id})
        self.assertTrue(self._run(self.alice, commit)["committed"])
        with self.assertRaises(UserError):
            self.env["hp.pdm.commit"].with_user(self.alice).clear_commit(commit.id)
        self._next_rpc()
        self.assertTrue(commit.committed)

    def test_commit_cannot_attach_properties_to_an_existing_version(self):
        commit = self._commit(self.bob)
        self._stage(self.bob, commit, "hp.version.property",
                    {"version_id": self.part_v1.id, "prop_id": self.prop.id, "text_value": "unobtainium"})
        result = self._run(self.bob, commit)
        self.assertNotIn("unobtainium", self.part_v1.version_property_ids.mapped("text_value"))
        self.assertTrue(result["errored"])

    def test_properties_of_a_committed_version_are_fixed(self):
        Prop = self.env["hp.version.property"].with_user(self.bob)
        with self.assertRaises(AccessError):
            Prop.create({"version_id": self.part_v1.id, "prop_id": self.prop.id, "text_value": "x"})
        with self.assertRaises(AccessError):
            self.part_v1.version_property_ids.with_user(self.bob).write({"text_value": "x"})

    @mute_logger("root")
    def test_commit_cannot_create_records_of_other_models(self):
        commit = self._commit(self.bob)
        with self.assertRaises(UserError):
            self._stage(self.bob, commit, "res.users", {"name": "Mallory", "login": "hp_atk_mallory"})
        # a vault model the user may not create: the whole commit fails
        self._stage(self.bob, commit, "hp.settings", {"name": "pwned"})
        result = self._run(self.bob, commit)
        self.assertFalse(self.env["res.users"].search([("login", "=", "hp_atk_mallory")]))
        self.assertTrue(result["errored"])

    def test_commit_refuses_a_file_outside_any_folder(self):
        commit = self._commit(self.bob)
        self._stage(self.bob, commit, "hp.entry", {"name": "floating.txt"})
        result = self._run(self.bob, commit)
        self.assertTrue(result["errored"])
        self.assertFalse(self.env["hp.entry"].search([("name", "=", "floating.txt")]))

    def test_commit_refuses_unsafe_names(self):
        for bad in ["..\\..\\evil.dll", "con.txt", "a:b.txt", "trailing.", ""]:
            commit = self._commit(self.bob)
            self._stage(self.bob, commit, "hp.entry", {"name": bad, "dir_id": self.folder.id})
            result = self._run(self.bob, commit)
            self.assertTrue(result["errored"], f"{bad!r} was committed")

    # ------------------------------------------------------------ checkouts

    def test_cannot_take_or_release_someone_elses_checkout(self):
        self._checkout(self.alice, self.part_v1)
        as_bob_entry = self.part.with_user(self.bob)
        as_bob_version = self.part_v1.with_user(self.bob)
        attempts = [
            lambda: self.env["hp.version"].with_user(self.bob).checkout_entry(self.part_v1.id, self.node.id),
            lambda: as_bob_entry.write({"checkout_user": self.bob.id}),
            lambda: as_bob_entry.write({"checkout_user": False}),
            lambda: as_bob_entry.write({"is_checked_out": False}),
            lambda: as_bob_entry.write({"checkout_node": self.other_node.id}),
            lambda: as_bob_entry.write({"deleted": True}),
            lambda: as_bob_entry.write({"name": "renamed.sldprt"}),
            lambda: as_bob_version.write({"checkout_user": False}),
            lambda: as_bob_version.write({"checkout_date": False}),
        ]
        for attempt in attempts:
            self._assert_refused(attempt)
            self._next_rpc()
            self.assertEqual(self.part.checkout_user, self.alice)

    def test_cannot_check_out_in_someone_elses_name(self):
        with self.assertRaises(AccessError):
            self.part.with_user(self.bob).write({"checkout_user": self.alice.id})
        with self.assertRaises(AccessError):
            self.part_v1.with_user(self.bob).write({"checkout_user": self.alice.id})

    def test_deleted_file_cannot_be_checked_out_by_writing_the_fields(self):
        self.part.write({"deleted": True})
        self._next_rpc()
        with self.assertRaises(UserError):
            self.part.with_user(self.bob).write({"checkout_user": self.bob.id})
        with self.assertRaises(UserError):
            self.part_v1.with_user(self.bob).write({"checkout_user": self.bob.id})
        self._next_rpc()
        self.assertFalse(self.part.checkout_user)

    def test_checkout_of_an_old_version_locks_the_latest(self):
        self._checkout(self.alice, self.part_v1)
        v2 = self.env["hp.version"].with_user(self.alice).create({"entry_id": self.part.id, "file_contents": b64(b"v2")})
        self._next_rpc()
        self.assertFalse(self.part.checkout_user, "a new version checks the file in")
        self._checkout(self.alice, self.part_v1)  # the old version id
        self.assertEqual(v2.checkout_user, self.alice)
        with self.assertRaises(UserError):
            self.env["hp.version"].with_user(self.bob).checkout_entry(self.part_v1.id, self.node.id)

    # ------------------------------------------------------------ committed versions

    def test_dependency_links_of_committed_versions_are_fixed(self):
        """Clearing them is how the (admin) permanent delete starts; a user doing it loses the
        assembly's references even though the delete itself is then refused."""
        as_bob = self.asm_v1.with_user(self.bob)
        with self.assertRaises(AccessError):
            as_bob.write({"child_ids": [(5, 0, 0)]})
        with self.assertRaises(AccessError):
            self.part_v1.with_user(self.bob).write({"parent_ids": [(3, self.asm_v1.id)]})
        with self.assertRaises(AccessError):
            as_bob.write({"child_ids": [(4, self.asm_v1.id)]})
        self._next_rpc()
        self.assertEqual(self.asm_v1.child_ids, self.part_v1)

    def test_committing_node_is_fixed(self):
        with self.assertRaises(AccessError):
            self.part_v1.with_user(self.bob).write({"node_id": self.other_node.id})

    def test_users_cannot_delete_vault_records(self):
        for record in (self.part, self.part_v1, self.part_v1.version_property_ids, self.folder):
            with self.assertRaises(AccessError):
                record.with_user(self.bob).unlink()

    def test_permanent_delete_by_a_user_leaves_everything_intact(self):
        """The client's PermanentDeleteEntry steps, in its order, by a regular user."""
        bob = self.bob
        steps = [
            lambda: self.part_v1.version_property_ids.with_user(bob).unlink(),
            lambda: self.part.version_ids.with_user(bob).write({"parent_ids": [(5, 0, 0)], "child_ids": [(5, 0, 0)]}),
            lambda: self.part.version_ids.with_user(bob).unlink(),
            lambda: self.part.with_user(bob).unlink(),
        ]
        for step in steps:
            with self.assertRaises(AccessError):
                step()
            self._next_rpc()
        self.assertTrue(self.part.exists())
        self.assertEqual(self.part_v1.parent_ids, self.asm_v1)
        self.assertTrue(self.part_v1.version_property_ids)

    # ------------------------------------------------------------ server functions

    def test_dependency_walk_cannot_rewrite_another_nodes_list(self):
        self.env["hp.version"].with_user(self.bob).get_recursive_dependency_entries(
            [self.asm_v1.id], self.other_node.id, True)
        self._next_rpc()
        self.assertFalse(self.other_node.node_latest_ids)

    def test_read_helpers_do_not_leak_passwords(self):
        Entry = self.env["hp.entry"].with_user(self.bob)
        for path in ["create_uid.password", "write_uid.groups_id.users.password"]:
            try:
                with self.env.cr.savepoint():
                    rows = Entry.smart_read([("id", "=", self.part.id)], None, ["name", path])
            except REFUSED:
                continue
            self.assertNotIn("admin", repr(rows))

    # ------------------------------------------------------------ access groups

    def test_cannot_read_someone_elses_staged_rows(self):
        alices = self._commit(self.alice)
        row = self._stage(self.alice, alices, "hp.entry", {"name": "secret.txt", "dir_id": self.folder.id})
        self._next_rpc()
        Staged = self.env["hp.record.staged"].with_user(self.bob)
        self.assertFalse(Staged.search([("id", "=", row.id)]))
        with self.assertRaises(AccessError):
            Staged.browse(row.id).read(["payload"])
        self.assertTrue(self.env["hp.record.staged"].with_user(self.alice).search([("id", "=", row.id)]))

    def test_administrator_reads_every_staged_row(self):
        admin = self.env["res.users"].with_context(no_reset_password=True).create(
            {"name": "Admin Attack", "login": "hp_atk_admin", "groups_id": [(6, 0, self.env.ref("hackpdm.group_pdm_admin").ids)]})
        row = self._stage(self.alice, self._commit(self.alice), "hp.entry", {"name": "a.txt", "dir_id": self.folder.id})
        self._next_rpc()
        self.assertTrue(self.env["hp.record.staged"].with_user(admin).search([("id", "=", row.id)]))

    def test_viewer_reads_but_cannot_check_out_or_commit(self):
        viewer = self.env["res.users"].with_context(no_reset_password=True).create(
            {"name": "Viewer Attack", "login": "hp_atk_viewer", "groups_id": [(6, 0, self.env.ref("hackpdm.group_pdm_viewer").ids)]})
        self.assertEqual(self.env["hp.entry"].with_user(viewer).browse(self.part.id).name, "part.sldprt")
        self.env["hp.node"].with_user(viewer).create({"name": "hp-atk-viewer-node"})
        with self.assertRaises(AccessError):
            self._checkout(viewer, self.part_v1)
        with self.assertRaises(AccessError):
            self._commit(viewer)
        with self.assertRaises(AccessError):
            self.env["hp.entry"].with_user(viewer).create({"name": "v.txt", "dir_id": self.folder.id})

    def test_regular_user_cannot_run_admin_jobs_or_touch_hp_test(self):
        self.assertFalse(self.alice.has_group("hackpdm.group_pdm_admin"))
        with self.assertRaises(AccessError):
            self.env["hp.test"].with_user(self.alice).search([])

    def test_settings_administrators_are_pdm_administrators(self):
        self.assertTrue(self.env.ref("base.user_admin").has_group("hackpdm.group_pdm_admin"))

    def test_new_internal_users_start_as_engineers(self):
        user = self.env["res.users"].with_context(no_reset_password=True).create({"name": "Fresh", "login": "hp_atk_fresh"})
        self.assertTrue(user.has_group("hackpdm.group_pdm_user"))
        self.assertFalse(user.has_group("hackpdm.group_pdm_admin"))
