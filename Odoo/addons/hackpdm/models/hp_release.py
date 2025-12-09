from odoo import models, fields, api

class hp_release(models.Model):
    _name = 'hp.release'
    _description = 'hp release'
    _inherit = 'hp.common.model'
    _inherit = 'hp.common.model'

    release_note = fields.Char(
        related='release_review_id.release_note',
        string='release note',
    )
    release_stamp = fields.Datetime(
        string='time stamp',
        default=lambda self:fields.Datetime.now(),
    )
    release_version_id = fields.Many2one(
        comodel_name='hp.version',
    )
    release_user_id = fields.Many2one(
        comodel_name='res.users',
        string='release user',
    )
    reviewer_user_id = fields.Many2one(
        comodel_name='res.users',
        string='reviewed by'
    )
    entry_id = fields.Many2one(
        related='release_version_id.entry_id',
        comodel_name='hp.entry',
        string='entry'
    )
    release_review_id = fields.Many2one(
        comodel_name='hp.release.review',
        string='release review',
    )


class hp_release_version_rel(models.Model):
    _name = 'hp.release.version.rel'
    _description = 'hp release relative version'
    _inherit = 'hp.common.model'

    release_id = fields.Many2one(
        comodel_name='hp.release',
        string='release id',
    )
    release_version = fields.Many2one(
        comodel_name='hp.version',
        string='release version',
    )
    release_user = fields.Many2one(
        comodel_name='res.users',
        related='release_id.release_user_id',
        string='relative user release',
    )