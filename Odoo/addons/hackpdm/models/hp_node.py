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
        self.write({"node_latest_ids": [(Command.SET, 0, version_ids)]})
