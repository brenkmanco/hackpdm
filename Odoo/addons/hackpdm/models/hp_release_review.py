import logging
from odoo import _, api, fields, models
from odoo.exceptions import AccessError, UserError, ValidationError

_logger = logging.getLogger(__name__)

class hp_release_review(models.Model):
    _name = 'hp.release.review'
    _description = 'hp release review'
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
        """Records an administrator's decision on a release review.

        When accepted, creates the hp.release for the reviewed version and persists
        the decision (accepted, reviewed, note, release_reviewed, cleared deadline,
        and release_id) onto the hp.release.review record in the same transaction.

        Args:
            review_id (int): The hp.release.review id.
            accepted (bool): Whether the release is accepted.
            note (str, optional): The reviewer's note. Defaults to None.

        Returns:
            dict: Updated review state including ``review_id``, ``reviewed``,
            ``accepted``, and ``release_id`` (int or False).

        Raises:
            AccessError: The caller is not a PDM administrator.
            ValidationError: ``review_id`` is not an int or ``accepted`` is not a bool.
            UserError: No review has that id, or the review was already decided.
        """
        userCaller = self.env.user
        if not userCaller.has_group('hackpdm.group_pdm_admin'):
            raise AccessError(_("%s attempted to review a release with insufficient permissions.", userCaller.name))

        if not isinstance(review_id, int) or isinstance(review_id, bool) or not isinstance(accepted, bool):
            raise ValidationError(_("invalid parameters for hp.release.review"))

        review = self.browse(review_id).exists()
        if not review:
            raise UserError(_("Release review %s was not found.", review_id))
        if review.reviewed:
            raise UserError(_("Release review %s has already been reviewed.", review_id))

        now = fields.Datetime.now()
        review_update = {
            'accepted': accepted,
            'reviewed': True,
            'review_note': note,
            'release_reviewed': now,
            'review_deadline': False,
        }
        if accepted:
            release_values = {
                'release_review_id': review.id,
                'release_user_id': review.review_user_id.id,
                'release_version_id': review.review_release_version_id.id,
                'release_stamp': now,
                'reviewer_user_id': userCaller.id,
            }
            record = self.env['hp.release'].create(release_values)
            review_update['release_id'] = record.id

        review.write(review_update)
        _logger.info(
            "PDM release review decided: review_id=%s uid=%s accepted=%s release_id=%s",
            review.id, userCaller.id, accepted, review.release_id.id or 0,
        )
        return {
            'review_id': review.id,
            'reviewed': review.reviewed,
            'accepted': review.accepted,
            'release_id': review.release_id.id or False,
        }
