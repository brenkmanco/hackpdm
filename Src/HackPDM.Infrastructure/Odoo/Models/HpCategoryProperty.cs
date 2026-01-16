using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_CATEGORY_PROPERTY_NAME, OdooDefaultsConstants.HP_CATEGORY_PROPERTY)]
public partial class HpCategoryProperty : HpBaseModelTransport<HpCategoryProperty>, IHpCategoryPropertyModel
{
	[OdooProp(OdooFieldType.Many2one, "cat_id")] public Many2One? cat_id { get; set; }
	IMany2One? IHpCategoryPropertyModel.cat_id { get =>(IMany2One?)cat_id; set => cat_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2one, "prop_id")] public Many2One? prop_id { get; set; }
	IMany2One? IHpCategoryPropertyModel.prop_id { get =>(IMany2One?)prop_id; set => prop_id = (Many2One?)value; }
}
