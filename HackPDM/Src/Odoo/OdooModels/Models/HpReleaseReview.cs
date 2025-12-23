using System;
using System.Collections.Generic;
using System.Text;

namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_RELEASE_REVIEW_NAME, OdooDefaults.HP_RELEASE_REVIEW)]
internal class HpReleaseReview : HpBaseModel<HpReleaseReview>
{
	[OdooField(OdooFieldType.Many2one)] public int? review_release_version_id;
	[OdooField(OdooFieldType.Many2one)] public int? review_user_id;
	[OdooField(OdooFieldType.Many2one)] public int? release_id;
	[OdooField(OdooFieldType.Many2one)] public int? release_user_id;

	[OdooField(OdooFieldType.DateTime)] public DateTime? review_stamp;
	[OdooField(OdooFieldType.DateTime)] public DateTime? review_deadline;
	[OdooField(OdooFieldType.DateTime)] public DateTime? release_reviewed;
	[OdooField(OdooFieldType.DateTime)] public DateTime? release_date;
	
	[OdooField(OdooFieldType.Char)] public string? review_note;
	[OdooField(OdooFieldType.Char)] public string? release_note;

	[OdooField(OdooFieldType.Boolean)] public bool? reviewed;
	[OdooField(OdooFieldType.Boolean)] public bool? accepted;
	 
	public HpReleaseReview() { }
	public HpReleaseReview(
		int? releaseId = null,
		int? reviewReleaseVersionId=null,
		int? reviewUserId = null,
		int? releaseUserId = null,
		DateTime? reviewStamp = null,
		DateTime? reviewDeadline = null,
		DateTime? releaseDate = null,
		DateTime? releaseReview = null,
		string? releaseNote = null,
		string? reviewNote = null,
		bool? reviewed = null,
		bool? accepted = null)
	{
		review_release_version_id = releaseId;
		review_release_version_id = reviewReleaseVersionId;
		review_user_id = reviewUserId;
		release_user_id = releaseUserId;

		review_stamp = reviewStamp;
		review_deadline = reviewDeadline;
		release_date = releaseDate;
		release_reviewed = releaseReview;
		
		release_note = releaseNote;
		review_note = releaseNote;

		this.reviewed = reviewed;
		this.accepted = accepted;
	}
}

