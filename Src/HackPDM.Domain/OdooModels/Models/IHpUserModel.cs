using System;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.RES_USERS_NAME, OdooDefaultsConstants.RES_USERS)]
public interface IHpUserModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name {get;set;}
	[OdooProp(OdooFieldType.Char, "login")] public string? login {get;set;}
	[OdooProp(OdooFieldType.Char, "email")] public string? email {get;set;}
	[OdooProp(OdooFieldType.Char, "signature")] public string? signature {get;set;}
	
	[OdooProp(OdooFieldType.DateTime, "login_date")] public DateTime? login_date {get;set;}
	
	[OdooProp(OdooFieldType.Boolean, "active")] public bool? active {get;set;}
	
	[OdooProp(OdooFieldType.Many2One, "company_id")] public IMany2One? company_id {get;set;}
}