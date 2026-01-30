using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_TYPE_NAME, OdooDefaultsConstants.HP_TYPE)]
public interface IHpTypeModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char, "description")] public string? description {get;set;}
	[OdooProp(OdooFieldType.Char, "file_ext")] public string? file_ext {get;set;}
	[OdooProp(OdooFieldType.Char, "type_regex")] public string? type_regex {get;set;}
	
	[OdooProp(OdooFieldType.Binary, "icon")] public string? icon {get;set;}
	
	[OdooProp(OdooFieldType.Many2One, "cat_id")] public IMany2One? cat_id {get;set;}
}