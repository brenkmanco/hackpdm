from odoo import fields, models, api, Command
import datetime as dt
import numpy as np

class hp_node(models.Model):
    _name = 'hp.node'
    _description = 'node'
    _inherit = 'hp.common.model'

    name = fields.Char(
        required=True,
        string='name',
    )

    node_latest_ids = fields.Many2many(
        comodel_name='hp.version',
    )

    @api.model
    def update_node_latest_versions(self, version_ids):
        """Replaces the node's list of versions it holds the latest of.

        Declared ``@api.model``, so over RPC it runs on an empty recordset and changes nothing; the
        client sets the list through ``hp.version.get_recursive_dependency_entries(update=True)``.
        Regular users cannot write nodes (only administrators can).

        Args:
            version_ids (list[int]): hp.version ids.

        Returns:
            None: nothing.

        Raises:
            AccessError: The caller may not write hp.node records.
        """
        self.write({"node_latest_ids": [(Command.SET, 0, version_ids)]})
