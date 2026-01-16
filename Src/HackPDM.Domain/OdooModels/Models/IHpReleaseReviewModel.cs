using System;
using System.Collections.Generic;
using System.Text;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_REVIEW_NAME, OdooDefaultsConstants.HP_RELEASE_REVIEW)]
public interface IHpReleaseReviewModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Many2one, "review_release_version_id")] public IMany2One? review_release_version_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "review_user_id")] public IMany2One? review_user_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "release_id")] public IMany2One? release_id {get;set;}
	[OdooProp(OdooFieldType.Many2one, "release_user_id")] public IMany2One? release_user_id {get;set;}

	[OdooProp(OdooFieldType.DateTime, "review_stamp")] public DateTime? review_stamp {get;set;}
	[OdooProp(OdooFieldType.DateTime, "review_deadline")] public DateTime? review_deadline {get;set;}
	[OdooProp(OdooFieldType.DateTime, "release_reviewed")] public DateTime? release_reviewed {get;set;}
	[OdooProp(OdooFieldType.DateTime, "release_date")] public DateTime? release_date {get;set;}
	
	[OdooProp(OdooFieldType.Char, "review_note")] public string? review_note {get;set;}
	[OdooProp(OdooFieldType.Char, "release_note")] public string? release_note {get;set;}

	[OdooProp(OdooFieldType.Boolean, "reviewed")] public bool? reviewed {get;set;}
	[OdooProp(OdooFieldType.Boolean, "accepted")] public bool? accepted {get;set;}
}

