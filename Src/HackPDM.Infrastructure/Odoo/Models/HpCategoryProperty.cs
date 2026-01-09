using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_CATEGORY_PROPERTY_NAME, OdooDefaultsConstants.HP_CATEGORY_PROPERTY)]
public partial class HpCategoryProperty : HpBaseModelTransport<HpCategoryProperty>, IHpCategoryPropertyModel
{
	[OdooProp(OdooFieldType.Many2one)]
	public int cat_id { get; set; }
	[OdooProp(OdooFieldType.Many2one)]
	public int prop_id { get; set; }
}
