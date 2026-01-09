using System;
using HackPDM.Shared.GlobalData;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_SETTINGS_NAME, OdooDefaultsConstants.HP_SETTINGS)]
public interface IHpSettingModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char)] public string name {get;set;}
	[OdooProp(OdooFieldType.Char)] public string description {get;set;}
	[OdooProp(OdooFieldType.Char)] public string type {get;set;}
	[OdooProp(OdooFieldType.Boolean)] public bool bool_value {get;set;}
	[OdooProp(OdooFieldType.Integer)] public int int_value {get;set;}
	[OdooProp(OdooFieldType.Char)] public string char_value {get;set;}
	[OdooProp(OdooFieldType.Float)] public float float_value {get;set;}
	[OdooProp(OdooFieldType.DateTime)] public DateTime date_value {get;set;}
}