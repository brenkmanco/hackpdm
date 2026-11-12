"""The server functions the HackPDM client calls, driven the way the client drives them (same RPC
methods, same staged payload shapes) by a regular user, so a change that breaks them fails here."""
import base64
import hashlib
import importlib.util
import os

from odoo.exceptions import UserError
from odoo.tests import TransactionCase, tagged


def b64(data: bytes) -> str:
    return base64.b64encode(data).decode()


@tagged("post_install", "-at_install")
class TestServerFunctions(TransactionCase):

    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        group_user = cls.env.ref("base.group_user")
        cls.alice = cls.env["res.users"].with_context(no_reset_password=True).create(
            {"name": "Alice Functions", "login": "hp_fn_alice", "groups_id": [(6, 0, group_user.ids)]})
        cls.node = cls.env["hp.node"].create({"name": "hp-fn-node"})
        cls.parts_cat = cls.env["hp.category"].create({"name": "hp-fn parts"})
        cls.other_cat = cls.env["hp.category"].create({"name": "hp-fn other"})
        cls.asm_type = cls.env["hp.type"].create({"file_ext": "SLDASM", "cat_id": cls.parts_cat.id})
        cls.prop = cls.env["hp.property"].create({"name": "Material", "prop_type": "text"})

        Dir = cls.env["hp.directory"]
        cls.folder = Dir.create({"name": "hp_fn_tests"})
        cls.sub = Dir.create({"name": "Sub", "parent_id": cls.folder.id})
        cls.deep = Dir.create({"name": "Deep", "parent_id": cls.sub.id})

        cls.bracket_v1_data = b"bracket v1"
        cls.bracket = cls.env["hp.entry"].create({"name": "bracket.sldprt", "dir_id": cls.folder.id})
        cls.bracket_v1 = cls.env["hp.version"].create(
            {"entry_id": cls.bracket.id, "file_contents": b64(cls.bracket_v1_data)})
        cls.gone = cls.env["hp.entry"].create({"name": "gone.txt", "dir_id": cls.folder.id, "deleted": True})
        cls.nested = cls.env["hp.entry"].create({"name": "nested.txt", "dir_id": cls.sub.id})
        cls.env["hp.version"].create({"entry_id": cls.nested.id, "file_contents": b64(b"nested")})

    # ------------------------------------------------------------ helpers

    def _next_rpc(self):
        # each client call is its own transaction: write everything, then read back from the database
        self.env.flush_all()
        self.env.invalidate_all()

    def _commit(self, user):
        return self.env["hp.pdm.commit"].with_user(user).create({"name": "test commit", "node_by": self.node.id})

    def _stage(self, user, commit, model, payload):
        """One hp.record.staged row, as HpBaseModelTransport.PreStageRec builds it."""
        return self.env["hp.record.staged"].with_user(user).create({
            "commit_id": commit.id, "committing_id": commit.id, "target_model": model, "payload": payload})

    def _checkout(self, user, version):
        self.env["hp.version"].with_user(user).checkout_entry(version.id, self.node.id)
        self._next_rpc()

    # ------------------------------------------------------------ commit

    def test_commit_creates_entries_versions_links_and_properties(self):
        self._checkout(self.alice, self.bracket_v1)
        commit = self._commit(self.alice)
        frame_data = b"frame assembly"
        bracket_v2_data = b"bracket v2"

        # a new file: its entry, then its first version referring to the staged entry row
        frame_entry_row = self._stage(self.alice, commit, "hp.entry", {
            "name": "frame.sldasm", "deleted": False, "dir_id": self.folder.id,
            "type_id": self.asm_type.id, "cat_id": self.other_cat.id, "commit_id": commit.id})
        frame_version_row = self._stage(self.alice, commit, "hp.version", {
            "name": "frame.sldasm", "dir_id": self.folder.id, "entry_id": frame_entry_row.id,
            "file_ext": "sldasm", "node_id": self.node.id, "file_contents": b64(frame_data), "commit_id": commit.id})
        # a new version of a file checked out to the committer
        self._stage(self.alice, commit, "hp.version", {
            "__internal__existing": True, "name": "bracket.sldprt", "dir_id": self.folder.id,
            "entry_id": self.bracket.id, "file_ext": "sldprt", "node_id": self.node.id,
            "file_contents": b64(bracket_v2_data), "commit_id": commit.id})
        # the assembly references the part, and has a custom property
        self._stage(self.alice, commit, "hp.version.relationship", {
            "path": "C:\\vault\\hp_fn_tests\\bracket.sldprt",
            "windows_complete_name": self.bracket.windows_complete_name,
            "version": {"name": "frame.sldasm", "entry_id": frame_entry_row.id, "version_staged_id": frame_version_row.id}})
        self._stage(self.alice, commit, "hp.version.property", {
            "version_id": frame_version_row.id, "prop_id": self.prop.id, "sw_config_name": "Default",
            "text_value": "steel", "commit_id": commit.id})
        self._next_rpc()

        result = self.env["hp.pdm.commit"].with_user(self.alice).start_commit([commit.id])
        self._next_rpc()

        self.assertEqual(len(result), 1)
        self.assertTrue(result[0]["committed"], result[0]["message_exception"])
        self.assertFalse(result[0]["errored"])
        self.assertIn("hp.entry", commit.commit_summary)

        frame = self.env["hp.entry"].search([("name", "=", "frame.sldasm"), ("dir_id", "=", self.folder.id)])
        self.assertEqual(len(frame), 1)
        self.assertEqual(frame.type_id, self.asm_type)
        self.assertEqual(frame.commit_id, commit)
        frame_version = frame.latest_version_id
        self.assertEqual(frame_version.checksum, hashlib.sha1(frame_data).hexdigest())
        self.assertEqual(frame_version.file_size, len(frame_data))
        self.assertEqual(base64.b64decode(frame_version.file_contents), frame_data)
        self.assertEqual(frame_version.node_id, self.node)

        # committing checks the file back in
        self.assertEqual(len(self.bracket.version_ids), 2)
        bracket_v2 = self.bracket.latest_version_id
        self.assertNotEqual(bracket_v2, self.bracket_v1)
        self.assertEqual(base64.b64decode(bracket_v2.file_contents), bracket_v2_data)
        self.assertFalse(self.bracket.is_checked_out)
        self.assertFalse(self.bracket.checkout_user)
        # the checkout was kept on version 1, and the commit cleared it there too
        self.assertFalse(self.bracket_v1.checkout_user or self.bracket_v1.checkout_node or self.bracket_v1.checkout_date)
        self.assertFalse(bracket_v2.checkout_user)

        self.assertEqual(frame_version.child_ids, bracket_v2)
        self.assertEqual(bracket_v2.parent_ids, frame_version)
        self.assertEqual(frame_version.version_property_ids.mapped("text_value"), ["steel"])

        # related fields in the payloads did not write through to the shared type record
        self.assertEqual(self.asm_type.file_ext, "SLDASM")
        self.assertEqual(self.asm_type.cat_id, self.parts_cat)

    def test_failed_commit_creates_nothing_and_reports_why(self):
        commit = self._commit(self.alice)
        self._stage(self.alice, commit, "hp.entry", {"name": "good.txt", "dir_id": self.folder.id})
        self._stage(self.alice, commit, "hp.entry", {"name": "BRACKET.SLDPRT", "dir_id": self.folder.id})
        self._next_rpc()

        result = self.env["hp.pdm.commit"].with_user(self.alice).start_commit(commit.id)
        self._next_rpc()

        self.assertTrue(result[0]["errored"])
        self.assertFalse(result[0]["committed"])
        self.assertIn("already exists", result[0]["message_exception"])
        self.assertFalse(self.env["hp.entry"].search([("name", "=", "good.txt")]))
        self.assertFalse(commit.staged_ids)

    def test_commit_refuses_a_version_of_a_file_checked_out_to_someone_else(self):
        self._checkout(self.env.user, self.bracket_v1)
        commit = self._commit(self.alice)
        self._stage(self.alice, commit, "hp.version", {
            "__internal__existing": True, "entry_id": self.bracket.id, "file_contents": b64(b"sneaky")})
        self._next_rpc()

        result = self.env["hp.pdm.commit"].with_user(self.alice).start_commit([commit.id])
        self._next_rpc()

        self.assertTrue(result[0]["errored"])
        self.assertIn("checked out", result[0]["message_exception"])
        self.assertEqual(len(self.bracket.version_ids), 1)

    def test_clear_commit_resets_it(self):
        commit = self._commit(self.alice)
        self._stage(self.alice, commit, "hp.entry", {"name": "later.txt", "dir_id": self.folder.id})
        self._next_rpc()

        self.assertTrue(self.env["hp.pdm.commit"].with_user(self.alice).clear_commit([commit.id]))
        self._next_rpc()
        self.assertFalse(commit.staged_ids)
        self.assertFalse(commit.committed or commit.errored)

    def test_committed_commit_is_not_run_again(self):
        commit = self._commit(self.alice)
        self._stage(self.alice, commit, "hp.entry", {"name": "once.txt", "dir_id": self.folder.id})
        self._next_rpc()
        Commit = self.env["hp.pdm.commit"].with_user(self.alice)
        Commit.start_commit([commit.id])
        self._next_rpc()
        again = Commit.start_commit([commit.id])
        self.assertTrue(again[0]["committed"])
        self.assertEqual(self.env["hp.entry"].search_count([("name", "=", "once.txt")]), 1)

    # ------------------------------------------------------------ checkout

    def test_checkout_by_node_id_or_name_and_release(self):
        Version = self.env["hp.version"].with_user(self.alice)
        self.assertTrue(Version.checkout_entry(self.bracket_v1.id, self.node.name))
        self._next_rpc()
        self.assertTrue(self.bracket.is_checked_out)
        self.assertEqual(self.bracket.checkout_user, self.alice)
        self.assertEqual(self.bracket.checkout_node, self.node)
        self.assertTrue(self.bracket.checkout_date)
        self.assertTrue(self.bracket_v1.is_checkout_version)
        # kept on the latest version; the entry shows it through latest_version_id
        self.assertEqual(self.bracket.latest_version_id, self.bracket_v1)
        self.assertEqual(self.bracket_v1.checkout_user, self.alice)
        self.assertEqual(self.bracket_v1.checkout_node, self.node)
        self.assertEqual(self.bracket_v1.checkout_date, self.bracket.checkout_date)
        # entries can be searched by who holds them (the client lists "checked out to me" this way)
        mine = self.env["hp.entry"].search([("checkout_user", "=", self.alice.id), ("id", "=", self.bracket.id)])
        self.assertEqual(mine, self.bracket)
        self.assertIn(self.bracket, self.env["hp.entry"].search([("is_checked_out", "=", True)]))
        # checking out again is harmless
        self.assertTrue(Version.checkout_entry(self.bracket_v1.id, self.node.id))

        # the client's un-checkout: PdmCheckoutOperations.UnCheckOutEntryAsync
        self.bracket.with_user(self.alice).write(
            {"checkout_user": False, "checkout_date": False, "checkout_node": False, "is_checked_out": False})
        self._next_rpc()
        self.assertFalse(self.bracket.is_checked_out or self.bracket.checkout_user or self.bracket.checkout_node)
        self.assertFalse(self.bracket_v1.checkout_user or self.bracket_v1.checkout_node or self.bracket_v1.checkout_date)
        self.assertNotIn(self.bracket, self.env["hp.entry"].search([("is_checked_out", "=", True)]))

    def test_upgrade_moves_entry_checkouts_onto_the_latest_version(self):
        """migrations/16.0.1.1.0: before it, the checkout was stored in hp_entry columns."""
        path = os.path.join(os.path.dirname(__file__), "..", "migrations", "16.0.1.1.0", "post-migrate.py")
        spec = importlib.util.spec_from_file_location("hp_post_migrate_16_0_1_1_0", path)
        migration = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(migration)

        cr = self.env.cr
        self.env.flush_all()
        # the columns an older database still has (rolled back with the test)
        cr.execute("""ALTER TABLE hp_entry ADD COLUMN IF NOT EXISTS checkout_user integer,
                                           ADD COLUMN IF NOT EXISTS checkout_date timestamp,
                                           ADD COLUMN IF NOT EXISTS checkout_node integer,
                                           ADD COLUMN IF NOT EXISTS is_checked_out boolean""")
        cr.execute("UPDATE hp_entry SET checkout_user = %s, checkout_date = now(), checkout_node = %s, is_checked_out = true WHERE id = %s",
                   (self.alice.id, self.node.id, self.bracket.id))

        migration.migrate(cr, "16.0.1.0.0")
        self.env.invalidate_all()

        self.assertEqual(self.bracket_v1.checkout_user, self.alice)
        self.assertEqual(self.bracket_v1.checkout_node, self.node)
        self.assertTrue(self.bracket_v1.checkout_date)
        self.assertTrue(self.bracket.is_checked_out)
        self.assertEqual(self.bracket.checkout_user, self.alice)

    def test_checkout_refuses_bad_arguments(self):
        Version = self.env["hp.version"].with_user(self.alice)
        with self.assertRaises(UserError):
            Version.checkout_entry(self.bracket_v1.id, "no-such-node")
        with self.assertRaises(UserError):
            Version.checkout_entry(987654321, self.node.id)
        with self.assertRaises(UserError):
            Version.checkout_entry("1", self.node.id)

    def test_mark_deleted_and_restore(self):
        Entry = self.bracket.with_user(self.alice)
        Entry.write({"deleted": True})
        self._next_rpc()
        self.assertNotIn("bracket.sldprt", self.env["hp.directory"].get_entries([self.folder.id, False]))
        Entry.write({"deleted": False})
        self._next_rpc()
        self.assertIn("bracket.sldprt", self.env["hp.directory"].get_entries([self.folder.id, False]))

    # ------------------------------------------------------------ directories

    def test_get_entries(self):
        Dir = self.env["hp.directory"].with_user(self.alice)
        live = Dir.get_entries([self.folder.id, False])
        self.assertEqual(set(live), {"bracket.sldprt"})
        row = live["bracket.sldprt"]
        self.assertEqual(row["id"], self.bracket.id)
        self.assertEqual(row["latest"], self.bracket_v1.id)
        self.assertEqual(row["latest_checksum"], hashlib.sha1(self.bracket_v1_data).hexdigest())
        self.assertEqual(row["size"], len(self.bracket_v1_data))
        self.assertEqual(row["checkout"], 0)
        self.assertFalse(row["deleted"])
        self.assertEqual(set(Dir.get_entries([self.folder.id, True])), {"bracket.sldprt", "gone.txt"})

    def test_get_all_entry_ids(self):
        Dir = self.env["hp.directory"].with_user(self.alice)
        self.assertEqual(set(Dir.get_all_entry_ids(self.folder.id)), {self.bracket.id, self.nested.id})
        self.assertEqual(set(Dir.get_all_entry_ids(self.folder.id, True)), {self.bracket.id, self.nested.id, self.gone.id})
        self.assertEqual(set(Dir.get_all_entry_ids(self.folder.id, False, False)), {self.bracket.id})

    def test_get_children_directories_by_id(self):
        tree = self.env["hp.directory"].with_user(self.alice).get_children_directories_by_id(self.folder.id, True)
        self.assertEqual(tree["id"], self.folder.id)
        self.assertEqual(set(tree["directories"]), {"Sub"})
        sub = tree["directories"]["Sub"]
        self.assertEqual(sub["id"], self.sub.id)
        self.assertEqual(set(sub["entries"]), {"nested.txt"})
        self.assertEqual(sub["directories"]["Deep"]["id"], self.deep.id)
        self.assertEqual(set(tree["entries"]), {"bracket.sldprt"})
        bare = self.env["hp.directory"].get_children_directories_by_id(self.folder.id, False)
        self.assertEqual(bare["entries"], {})

    def test_path_lookup_walks_as_far_as_the_folders_exist(self):
        Dir = self.env["hp.directory"].with_user(self.alice)
        found = Dir._recurse_directories_finding(self.folder, ["root", "Sub", "Deep", "Missing", "More"], 0, True, [])
        self.assertEqual(found, {"index": 2, "dir_id": self.deep.id, "known": [self.folder.id, self.sub.id, self.deep.id]})
        # "sub" is the same folder as "Sub" on Windows: found, not created again
        found = Dir._recurse_directories_finding(self.folder, ["root", "sub", "deep"], 0, True, [])
        self.assertEqual(found["dir_id"], self.deep.id)
        with self.assertRaises(UserError):
            Dir.last_available_directory([])

    def test_folder_creation_like_the_client(self):
        # HpDirectory.CreateNew: one create per missing folder, parent first
        Dir = self.env["hp.directory"].with_user(self.alice)
        a = Dir.create({"name": "New", "parent_id": self.deep.id, "sandboxed": False, "deleted": False})
        b = Dir.create({"name": "Newer", "parent_id": a.id, "sandboxed": False, "deleted": False})
        self._next_rpc()
        self.assertEqual(b.complete_name, "hp_fn_tests / Sub / Deep / New / Newer")
        self.assertTrue(b.parent_path.startswith(self.folder.parent_path))

    # ------------------------------------------------------------ dependencies

    def _chain(self):
        """a -> b -> c -> a (a cycle, which SolidWorks files can have)."""
        Entry, Version = self.env["hp.entry"], self.env["hp.version"]
        entries = [Entry.create({"name": f"{n}.sldasm", "dir_id": self.sub.id}) for n in "abc"]
        versions = [Version.create({"entry_id": e.id}) for e in entries]
        for parent, child in zip(versions, versions[1:] + versions[:1]):
            parent.write({"child_ids": [(4, child.id)]})
        self._next_rpc()
        return entries, versions

    def test_recursive_dependencies_follow_children_and_stop_on_cycles(self):
        entries, versions = self._chain()
        Version = self.env["hp.version"].with_user(self.alice)
        self.assertEqual(set(Version.get_recursive_dependency_versions([versions[0].id])), {v.id for v in versions})
        self.assertEqual(set(Version.get_recursive_dependency_entries([versions[0].id])), {e.id for e in entries})
        self.assertEqual(Version.get_recursive_dependency_versions([]), [])

    def test_recursive_dependencies_skip_deleted_entries(self):
        entries, versions = self._chain()
        entries[1].write({"deleted": True})
        self._next_rpc()
        found = set(self.env["hp.version"].with_user(self.alice).get_recursive_dependency_versions([versions[0].id]))
        self.assertEqual(found, {versions[0].id})

    def test_recursive_dependencies_see_links_made_in_the_same_transaction(self):
        Version = self.env["hp.version"]
        child = Version.create({"entry_id": self.nested.id})
        self.bracket_v1.write({"child_ids": [(4, child.id)]})
        # no flush: the raw SQL query must still see the new link
        self.assertIn(child.id, Version.get_recursive_dependency_versions([self.bracket_v1.id]))

    # ------------------------------------------------------------ generic reads

    def test_related_browse(self):
        rows = self.env["hp.entry"].with_user(self.alice).related_browse([self.bracket.id], "version_ids", ["entry_id"])
        self.assertEqual([r["id"] for r in rows], [self.bracket_v1.id])
        self.assertEqual(rows[0]["entry_id"][0], self.bracket.id)

    def test_smart_read_resolves_dotted_fields(self):
        rows = self.env["hp.entry"].with_user(self.alice).smart_read(
            [("id", "=", self.bracket.id)], [], ["name", "dir_id", "dir_id.name"])
        self.assertEqual(len(rows), 1)
        row = rows[0]
        self.assertEqual(row["id"], self.bracket.id)
        self.assertEqual(row["name"], "bracket.sldprt")
        self.assertEqual(row["dir_id"], {"id": self.folder.id, "name": "hp_fn_tests", "model": "hp.directory"})
        self.assertEqual(row["dir_id.name"], [{"value": "hp_fn_tests",
                                               "path": [{"id": self.folder.id, "name": "hp_fn_tests", "model": "hp.directory"}]}])

    # ------------------------------------------------------------ permanent delete (admin)

    def test_permanent_delete_like_the_client(self):
        """PdmDeleteOperations.PermanentDeleteEntry, run by an administrator."""
        entries, versions = self._chain()
        target = entries[1]
        version_ids = target.version_ids.ids
        self.env["hp.version"].browse(version_ids).write({"parent_ids": [(5, 0, 0)], "child_ids": [(5, 0, 0)]})
        self.env["ir.attachment"].search([
            ("res_id", "in", version_ids), ("res_model", "=", "hp.version"), ("res_field", "=", "file_contents")]).unlink()
        self.env["hp.version"].browse(version_ids).unlink()
        target.unlink()
        self._next_rpc()
        self.assertFalse(target.exists())
        self.assertFalse(versions[0].child_ids)
        self.assertFalse(versions[2].parent_ids)
