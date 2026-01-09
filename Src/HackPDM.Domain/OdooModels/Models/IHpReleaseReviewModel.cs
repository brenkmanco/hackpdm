using System;
using System.Collections.Generic;
using System.Text;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_RELEASE_REVIEW_NAME, OdooDefaultsConstants.HP_RELEASE_REVIEW)]
public interface IHpReleaseReviewModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Many2one)] public int? review_release_version_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int? review_user_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int? release_id {get;set;}
	[OdooProp(OdooFieldType.Many2one)] public int? release_user_id {get;set;}

	[OdooProp(OdooFieldType.DateTime)] public DateTime? review_stamp {get;set;}
	[OdooProp(OdooFieldType.DateTime)] public DateTime? review_deadline {get;set;}
	[OdooProp(OdooFieldType.DateTime)] public DateTime? release_reviewed {get;set;}
	[OdooProp(OdooFieldType.DateTime)] public DateTime? release_date {get;set;}
	
	[OdooProp(OdooFieldType.Char)] public string? review_note {get;set;}
	[OdooProp(OdooFieldType.Char)] public string? release_note {get;set;}

	[OdooProp(OdooFieldType.Boolean)] public bool? reviewed {get;set;}
	[OdooProp(OdooFieldType.Boolean)] public bool? accepted {get;set;}
}

