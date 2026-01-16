//using static System.Net.Mime.MediaTypeNames;

// Resharper disable InconsistentNaming

using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_PROPERTY_NAME, OdooDefaultsConstants.HP_PROPERTY)]
public interface IHpPropertyModel : IHpOdooRecord
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name {get;set;}
    [OdooProp(OdooFieldType.Char, "prop_type")] public string? prop_type {get;set;}
	[OdooProp(OdooFieldType.Boolean, "active")] public bool? active {get;set;}
}