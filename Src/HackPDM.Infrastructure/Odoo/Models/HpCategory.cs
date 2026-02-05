using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_CATEGORY_NAME, OdooDefaultsConstants.HP_CATEGORY)]
public partial class HpCategory : HpBaseModelTransport<HpCategory>, IHpCategoryModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char, "cat_description")] public string? cat_description { get; set; }
	[OdooProp(OdooFieldType.Boolean, "track_version")] public bool? track_version { get; set; }
	[OdooProp(OdooFieldType.Boolean, "track_depends")] public bool? track_depends { get; set; }

	public void Test()
	{
		var convert = new HpCategory();
		HpCategory.GetFields();
	}
}
