// Resharper disable InconsistentNaming
using System;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.OdooModels.Models;

[OdooModel(OdooDefaultsConstants.HP_CATEGORY_NAME, OdooDefaultsConstants.HP_CATEGORY)] 
public interface IHpCategoryModel : IHpOdooRecord
{
    [OdooProp(OdooFieldType.Char)] public string name { get; set; }
	[OdooProp(OdooFieldType.Char)] public string cat_description { get; set; }
	[OdooProp(OdooFieldType.Boolean)] public bool track_version { get; set; }
	[OdooProp(OdooFieldType.Boolean)] public bool track_depends { get; set; }
}
