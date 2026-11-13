"""hp.entry.dependency_graph: the files a file uses (children), the files that use it (parents), or both."""
import base64

from odoo.exceptions import UserError
from odoo.tests import TransactionCase, tagged


def b64(data: bytes) -> str:
    return base64.b64encode(data).decode()


@tagged("post_install", "-at_install")
class TestDependencyGraph(TransactionCase):
    """    top.sldasm ──> sub.sldasm ──> bolt.sldprt
                 │                ──> plate.sldprt
                 └──────────────────> bolt.sldprt
           other.sldasm (latest) ──> plate.sldprt
           old.sldasm: its older version used bolt, its latest uses nothing
    """

    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        cls.viewer = cls.env["res.users"].with_context(no_reset_password=True).create(
            {"name": "Viewer Graph", "login": "hp_graph_viewer",
             "groups_id": [(6, 0, cls.env.ref("hackpdm.group_pdm_viewer").ids)]})
        folder = cls.env["hp.directory"].create({"name": "hp_graph_tests"})

        def file(name, data):
            entry = cls.env["hp.entry"].create({"name": name, "dir_id": folder.id})
            version = cls.env["hp.version"].create({"entry_id": entry.id, "file_contents": b64(data)})
            return entry, version

        cls.bolt, cls.bolt_v = file("bolt.sldprt", b"b" * 100)
        cls.plate, cls.plate_v = file("plate.sldprt", b"p" * 3000)
        cls.sub, cls.sub_v = file("sub.sldasm", b"s" * 50)
        cls.top, cls.top_v = file("top.sldasm", b"t" * 10)
        cls.other, cls.other_v = file("other.sldasm", b"o")
        cls.old, cls.old_v1 = file("old.sldasm", b"old 1")
        cls.lonely, _ = file("lonely.txt", b"nothing")
        cls.sub_v.write({"child_ids": [(6, 0, [cls.bolt_v.id, cls.plate_v.id])]})
        cls.top_v.write({"child_ids": [(6, 0, [cls.sub_v.id, cls.bolt_v.id])]})
        cls.other_v.write({"child_ids": [(6, 0, [cls.plate_v.id])]})
        cls.old_v1.write({"child_ids": [(6, 0, [cls.bolt_v.id])]})
        cls.env["hp.version"].create({"entry_id": cls.old.id, "file_contents": b64(b"old 2")})

    def _graph(self, roots, direction, **kw):
        return self.env["hp.entry"].with_user(self.viewer).dependency_graph(roots, direction, **kw)

    @staticmethod
    def _edges(graph):
        return {tuple(edge) for edge in graph["edges"]}

    def test_children(self):
        graph = self._graph(self.top.id, "children")
        self.assertEqual(graph["roots"], [self.top.id])
        self.assertEqual({n["id"] for n in graph["nodes"]}, {self.top.id, self.sub.id, self.bolt.id, self.plate.id})
        self.assertEqual(self._edges(graph), {
            (self.top.id, self.sub.id), (self.top.id, self.bolt.id),
            (self.sub.id, self.bolt.id), (self.sub.id, self.plate.id)})
        self.assertFalse(graph["truncated"])
        plate = next(n for n in graph["nodes"] if n["id"] == self.plate.id)
        self.assertEqual((plate["name"], plate["ext"], plate["size"]), ("plate.sldprt", "sldprt", 3000))
        self.assertEqual(plate["version_id"], self.plate_v.id)
        self.assertIn("plate.sldprt", plate["path"])
        self.assertEqual((plate["versions"], plate["checkout_user"]), (1, ""))
        self.assertIsInstance(plate["modified"], str)

    def test_parents_follow_only_latest_versions(self):
        graph = self._graph(self.bolt.id, "parents")
        # old.sldasm used the bolt only in an older version
        self.assertEqual({n["id"] for n in graph["nodes"]}, {self.bolt.id, self.sub.id, self.top.id})
        self.assertEqual(self._edges(graph), {
            (self.sub.id, self.bolt.id), (self.top.id, self.bolt.id), (self.top.id, self.sub.id)})

    def test_both(self):
        graph = self._graph(self.sub.id, "both")
        self.assertEqual({n["id"] for n in graph["nodes"]}, {self.sub.id, self.top.id, self.bolt.id, self.plate.id})
        self.assertIn((self.top.id, self.sub.id), self._edges(graph))
        self.assertIn((self.sub.id, self.plate.id), self._edges(graph))

    def test_several_roots_and_a_file_without_dependencies(self):
        graph = self._graph([self.plate.id, self.lonely.id], "parents")
        self.assertEqual(sorted(graph["roots"]), sorted([self.plate.id, self.lonely.id]))
        self.assertEqual({n["id"] for n in graph["nodes"]},
                         {self.plate.id, self.lonely.id, self.sub.id, self.top.id, self.other.id})

    def test_limits(self):
        graph = self._graph(self.top.id, "children", max_depth=1)
        self.assertEqual({n["id"] for n in graph["nodes"]}, {self.top.id, self.sub.id, self.bolt.id})
        self.assertTrue(graph["truncated"])
        graph = self._graph(self.top.id, "children", max_nodes=2)
        self.assertEqual(len(graph["nodes"]), 2)
        self.assertTrue(graph["truncated"])

    def test_missing_files_are_left_out(self):
        self.assertEqual(self._graph([999999999], "children")["nodes"], [])

    def test_bad_arguments(self):
        for args in (("1", "children"), ([True], "children"), (self.top.id, "sideways"),
                     (self.top.id, "children", -1), (self.top.id, "children", 5, 0)):
            with self.assertRaises(UserError):
                self.env["hp.entry"].with_user(self.viewer).dependency_graph(*args)
