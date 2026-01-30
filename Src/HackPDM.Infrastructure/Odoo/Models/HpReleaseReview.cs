using System;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_REVIEW_NAME, OdooDefaultsConstants.HP_RELEASE_REVIEW)]
public partial class HpReleaseReview : HpBaseModelTransport<HpReleaseReview>, IHpReleaseReviewModel
{
	[OdooProp(OdooFieldType.Many2One, "review_release_version_id")] public Many2One? review_release_version_id { get; set; }
	IMany2One? IHpReleaseReviewModel.review_release_version_id { get =>(IMany2One?)review_release_version_id; set => review_release_version_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "review_user_id")] public Many2One? review_user_id { get; set; }
	IMany2One? IHpReleaseReviewModel.review_user_id { get =>(IMany2One?)review_user_id; set => review_user_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "release_id")] public Many2One? release_id { get; set; }
	IMany2One? IHpReleaseReviewModel.release_id { get =>(IMany2One?)release_id; set => release_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "release_user_id")] public Many2One? release_user_id { get; set; }
	IMany2One? IHpReleaseReviewModel.release_user_id { get =>(IMany2One?)release_user_id; set => release_user_id = (Many2One?)value; }

	[OdooProp(OdooFieldType.DateTime, "review_stamp")] public DateTime? review_stamp { get; set; }
	[OdooProp(OdooFieldType.DateTime, "review_deadline")] public DateTime? review_deadline { get; set; }
	[OdooProp(OdooFieldType.DateTime, "release_reviewed")] public DateTime? release_reviewed { get; set; }
	[OdooProp(OdooFieldType.DateTime, "release_date")] public DateTime? release_date { get; set; }

	[OdooProp(OdooFieldType.Char, "review_note")] public string? review_note { get; set; }
	[OdooProp(OdooFieldType.Char, "release_note")] public string? release_note { get; set; }

	[OdooProp(OdooFieldType.Boolean, "reviewed")] public bool? reviewed { get; set; }
	[OdooProp(OdooFieldType.Boolean, "accepted")] public bool? accepted { get; set; }
}
