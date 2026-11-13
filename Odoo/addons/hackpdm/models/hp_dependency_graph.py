"""The dependency graph of files, for the client's Dependency Graph page.

Works on files (hp.entry), through their latest versions: a file's children are the files its latest
version references (hp.version.child_ids); its parents are the files whose latest version references
any version of it ("where used" by the current assemblies).
"""
from odoo import api, fields, models
from odoo.exceptions import UserError

DIRECTIONS = ("children", "parents", "both")
MAX_DEPTH = 50
MAX_NODES = 2000


class hp_entry_dependency_graph(models.Model):
    _inherit = "hp.entry"

    @api.model
    def dependency_graph(self, entry_ids, direction="children", max_depth=MAX_DEPTH, max_nodes=MAX_NODES):
        """Returns the files that the given files use (children), are used by (parents), or both, as a graph.

        Args:
            entry_ids (int | list[int]): The files to start from (the "roots").
            direction (str): "children", "parents" or "both".
            max_depth (int): How many levels to follow in each direction; at most 50.
            max_nodes (int): Stop adding files after this many; at most 2000.

        Returns:
            dict: roots (list[int], the starting entry ids that exist); nodes (list[dict] with id, name,
            ext, size (bytes of the latest version), category, version_id, deleted, checked_out and path);
            edges (list[[parent_entry_id, child_entry_id]]); truncated (bool, whether a limit cut it short).

        Raises:
            UserError: The arguments are invalid.
            AccessError: The caller may not read the files.
        """
        if isinstance(entry_ids, int) and not isinstance(entry_ids, bool):
            entry_ids = [entry_ids]
        if not isinstance(entry_ids, list) or not all(isinstance(i, int) and not isinstance(i, bool) for i in entry_ids):
            raise UserError(f"dependency_graph expects entry ids, got {entry_ids!r}")
        if direction not in DIRECTIONS:
            raise UserError(f"dependency_graph: direction must be one of {', '.join(DIRECTIONS)}")
        if not isinstance(max_depth, int) or max_depth < 0:
            raise UserError(f"dependency_graph: invalid max_depth {max_depth!r}")
        if not isinstance(max_nodes, int) or max_nodes < 1:
            raise UserError(f"dependency_graph: invalid max_nodes {max_nodes!r}")
        max_depth = min(max_depth, MAX_DEPTH)
        max_nodes = min(max_nodes, MAX_NODES)

        roots = self.browse(entry_ids).exists()
        roots.check_access_rights("read")
        roots.check_access_rule("read")

        seen = set(roots.ids)
        edges = set()
        truncated = False
        if direction in ("children", "both"):
            truncated |= self._walk(roots, seen, edges, max_depth, max_nodes, self._children_of)
        if direction in ("parents", "both"):
            truncated |= self._walk(roots, seen, edges, max_depth, max_nodes, self._parents_of)

        nodes = self.browse(sorted(seen))
        return {
            "roots": roots.ids,
            "nodes": [self._graph_node(entry) for entry in nodes],
            "edges": sorted([parent, child] for parent, child in edges),
            "truncated": truncated,
        }

    def _walk(self, roots, seen, edges, max_depth, max_nodes, step):
        """Breadth-first from roots: step(entries) gives (entry, neighbour, edge) triples."""
        frontier = roots
        truncated = False
        for _depth in range(max_depth):
            if not frontier:
                break
            found = self.browse()
            for neighbour, edge in step(frontier):
                if neighbour.id not in seen:
                    if len(seen) >= max_nodes:
                        truncated = True
                        continue
                    seen.add(neighbour.id)
                    found |= neighbour
                edges.add(edge)
            frontier = found
        else:
            # stopped by max_depth: is there anything past the last level?
            truncated = truncated or any(neighbour.id not in seen for neighbour, _edge in step(frontier))
        return truncated

    def _children_of(self, entries):
        """(child entry, (entry, child)) for each file the entries' latest versions reference."""
        for entry in entries:
            for child in entry.latest_version_id.child_ids.entry_id:
                if child != entry:
                    yield child, (entry.id, child.id)

    def _parents_of(self, entries):
        """(parent entry, (parent, entry)) for each file whose latest version references a version of the entries."""
        versions = entries.version_ids
        if not versions:
            return
        users = self.env["hp.version"].search([("child_ids", "in", versions.ids)])
        for parent_version in users:
            parent = parent_version.entry_id
            if not parent or parent.latest_version_id != parent_version:
                continue
            for used in parent_version.child_ids.entry_id & entries:
                if used != parent:
                    yield parent, (parent.id, used.id)

    @staticmethod
    def _graph_node(entry):
        name = entry.name or ""
        ext = entry.type_id.file_ext or (name.rsplit(".", 1)[1] if "." in name else "")
        latest = entry.latest_version_id
        return {
            "id": entry.id,
            "name": name,
            "ext": (ext or "").lower(),
            "size": entry.latest_version_id.file_size or 0,
            "category": entry.cat_id.name or "",
            "version_id": entry.latest_version_id.id or 0,
            "deleted": bool(entry.deleted),
            "checked_out": bool(entry.checkout_user),
            "checkout_user": entry.checkout_user.name or "",
            "path": entry.windows_complete_name or "",
            "type": entry.type_id.description or "",
            "modified": fields.Datetime.to_string(latest.file_modify_stamp) if latest.file_modify_stamp else "",
            "versions": len(entry.version_ids),
        }
