using System;
using System.Collections.Generic;
using System.Text;

namespace HackPDM.Odoo.OdooModels.Models;

internal class HpReleaseReview : HpBaseModel<HpReleaseReview>
{
	public int? review_release_version_id;
	public int? review_user_id;
	public int? release_id;
	public int? release_user_id;

	public DateTime? review_stamp;
	public DateTime? review_deadline;
	public DateTime? release_reviewed;
	public DateTime? release_date;
	
	public string? review_note;
	public string? release_note;

	public bool? reviewed;
	public bool? accepted;
	 
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

