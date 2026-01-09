using HackPDM.Shared.GlobalData;

// Resharper disable InconsistentNaming

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_PROPERTY_NAME, OdooDefaultsConstants.HP_VERSION_PROPERTY)]
public interface IHpVersionPropertyModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char)] public string prop_name {get;set;}
	[OdooProp(OdooFieldType.Char)] public string sw_config_name {get;set;}
    [OdooProp(OdooFieldType.Char)] public string text_value {get;set;}

    [OdooProp(OdooFieldType.Float)] public float number_value {get;set;}

    [OdooProp(OdooFieldType.Boolean)] public bool yesno_value {get;set;}

    [OdooProp(OdooFieldType.DateTime)] public string date_value {get;set;}

    [OdooProp(OdooFieldType.Many2one)] public int version_id {get;set;}
    [OdooProp(OdooFieldType.Many2one)] public int prop_id {get;set;}
}