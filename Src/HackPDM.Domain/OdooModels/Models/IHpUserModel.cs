using System;
using HackPDM.Shared.GlobalData;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.RES_USERS_NAME, OdooDefaultsConstants.RES_USERS)]
public interface IHpUserModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char)] public string? name {get;set;}
	[OdooProp(OdooFieldType.Char)] public string? login {get;set;}
	[OdooProp(OdooFieldType.Char)] public string? email {get;set;}
	[OdooProp(OdooFieldType.Char)] public string? signature {get;set;}
	
	[OdooProp(OdooFieldType.DateTime)] public DateTime? login_date {get;set;}
	
	[OdooProp(OdooFieldType.Boolean)] public bool? active {get;set;}
	
	[OdooProp(OdooFieldType.Many2one)] public int? company_id {get;set;}
}