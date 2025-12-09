from odoo import models, fields, api, Command

class hp_release_review(models.Model):
    _name = 'hp.release.review'
    _description = 'hp release review'
    _inherit = 'hp.common.model'
    _inherit = 'hp.common.model'

    review_stamp = fields.Datetime(
        string='review started date',
        default=lambda self:fields.Datetime.now(),
    )
    review_release_version_id = fields.Many2one(
        comodel_name='hp.version',
        string='release version',
    )
    review_user_id = fields.Many2one(
        comodel_name='res.users',
        string='review user',
    )
    review_deadline = fields.Datetime(
        string='review deadline',
        default=None,
    )
    release_note = fields.Char(
        string='release note',
    )

    reviewed = fields.Boolean(
        string='has been reviewed',
    )
    accepted = fields.Boolean(
        string='has been accepted',
    )
    release_reviewed = fields.Datetime(
        string='release reviewed date',
        default=None,
    )
    release_user_id = fields.Many2one(
        comodel_name='res.users',
        string='release user',
    )
    release_id = fields.Many2one(
        comodel_name='hp.release',
        string='release',
    )
    release_date = fields.Datetime(
        related='release_id.release_stamp',
        string='released date',
    )
    review_note = fields.Char(
        string='review notes',
    )

    entry_id = fields.Many2one(
        related='review_release_version_id.entry_id',
        comodel_name='hp.entry',
        string='entry',
    )
    #
    @api.model
    def reviewer_decision(self, review_id, accepted, note:str=None):
        # Check if the user belongs to a group that has permission
        userCaller = self.env.user
        if not userCaller.has_group('base.group_system'):
            raise PermissionError(_(f"{userCaller.name} attempted to review a release with insufficient permissions."))

        review = self.env["hp.release.review"].search([
            ('id', '=', review_id),
        ], limit=1)[0]

        if not review or (accepted is None):
            raise ValueError(_("invalid parameters for hp.release.review"))

        review_update = {
            'accepted': accepted,
            'reviewed': True,
            'review_note': note,
            'release_reviewed': fields.Datetime.now(),
            'review_deadline': None,
        }
        if accepted:
            release_values = {
                'release_review_id': review.id,
                'release_user_id': review.review_user_id.id,
                'release_version_id': review.review_release_version_id.id,
                'release_stamp': fields.Datetime.now(),
                'reviewer_user_id': userCaller.id,
            }
            record = self.env['hp.release'].create(release_values)
            review_update['release_id'] = record.id

        writing = Command.update(review.id, review_update)
