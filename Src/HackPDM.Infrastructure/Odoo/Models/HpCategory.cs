using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo.FormTransport;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_CATEGORY_NAME, OdooDefaultsConstants.HP_CATEGORY)]
public partial class HpCategory : HpBaseModelTransport<HpCategory>, IHpCategoryModel
{
	[OdooProp(OdooFieldType.Char)] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char)] public string? cat_description { get; set; }
	[OdooProp(OdooFieldType.Boolean)] public bool track_version { get; set; }
	[OdooProp(OdooFieldType.Boolean)] public bool track_depends { get; set; }

	public void Test()
	{
		//IHpModelConvert<IHpCategoryModel, HpCategory> convert = new HpCategory();
	}

	public int id { get; set; }
}
