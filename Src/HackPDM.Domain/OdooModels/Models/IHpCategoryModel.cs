// Resharper disable InconsistentNaming
using System;

using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_CATEGORY_NAME, OdooDefaultsConstants.HP_CATEGORY)] 
public interface IHpCategoryModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char, "cat_description")] public string? cat_description { get; set; }
	[OdooProp(OdooFieldType.Boolean, "track_version")] public bool? track_version { get; set; }
	[OdooProp(OdooFieldType.Boolean, "track_depends")] public bool? track_depends { get; set; }
}
