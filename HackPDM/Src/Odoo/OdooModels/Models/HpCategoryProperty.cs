//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_CATEGORY_PROPERTY_NAME, OdooDefaults.HP_CATEGORY_PROPERTY)]
public partial class HpCategoryProperty : HpBaseModel<HpCategoryProperty>
{
	[OdooField(OdooFieldType.Many2one)]
	public int cat_id;
	[OdooField(OdooFieldType.Many2one)]
	public int prop_id;

    public HpCategoryProperty() { }
    public HpCategoryProperty(
        int catId = 0,
        int propId = 0)
    {
        this.cat_id = catId;
        this.prop_id = propId;
    }
}
public partial class HpCategoryProperty : HpBaseModel<HpCategoryProperty>
{
}