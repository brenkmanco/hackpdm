using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_PROPERTY_NAME, OdooDefaultsConstants.HP_VERSION_PROPERTY)]
public interface IHpVersionPropertyModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char, "prop_name")] public string? prop_name {get;set;}
	[OdooProp(OdooFieldType.Char, "sw_config_name")] public string? sw_config_name {get;set;}
    [OdooProp(OdooFieldType.Char, "text_value")] public string? text_value {get;set;}

    [OdooProp(OdooFieldType.Float, "number_value")] public float? number_value {get;set;}

    [OdooProp(OdooFieldType.Boolean, "yesno_value")] public bool? yesno_value {get;set;}

    [OdooProp(OdooFieldType.DateTime, "date_value")] public DateTime? date_value {get;set;}

    [OdooProp(OdooFieldType.Many2One, "version_id")] public IMany2One? version_id {get;set;}
    [OdooProp(OdooFieldType.Many2One, "prop_id")] public IMany2One? prop_id {get;set;}
}