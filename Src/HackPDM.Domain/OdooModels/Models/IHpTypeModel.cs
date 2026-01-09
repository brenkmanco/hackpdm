using HackPDM.Shared.GlobalData;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_TYPE_NAME, OdooDefaultsConstants.HP_TYPE)]
public interface IHpTypeModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char)] public string? description {get;set;}
	[OdooProp(OdooFieldType.Char)] public string? file_ext {get;set;}
	[OdooProp(OdooFieldType.Char)] public string? type_regex {get;set;}
	
	[OdooProp(OdooFieldType.Binary)] public string? icon {get;set;}
	
	[OdooProp(OdooFieldType.Many2one)] public int? cat_id {get;set;}
}