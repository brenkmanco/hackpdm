from odoo import models, fields, api

class hp_release(models.Model):
    #base fields
    _name = 'hp.release'
    _description = 'hp release'
    _inherit = 'hp.common.model'
    _inherit = 'hp.common.model'
    #fields
    release_stamp = fields.Datetime(
        string='time stamp',
        default=lambda self:fields.Datetime.now(),
    )
    release_note = fields.Char(string='release note')

    #relational fields
    release_user_id = fields.Many2one(
        comodel_name='res.users',
        string='release user',
    )

class hp_release_version_rel(models.Model):
    #base fields
    _name = 'hp.release.version.rel'
    _description = 'hp release relative version'
    _inherit = 'hp.common.model'

    #fields

    #relational fields
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