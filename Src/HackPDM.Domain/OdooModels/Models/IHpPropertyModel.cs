//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming

using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_PROPERTY_NAME, OdooDefaultsConstants.HP_PROPERTY)]
public interface IHpPropertyModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char)] public string name {get;set;}
    [OdooProp(OdooFieldType.Char)] public string prop_type {get;set;}
	[OdooProp(OdooFieldType.Boolean)] public bool active {get;set;}
}