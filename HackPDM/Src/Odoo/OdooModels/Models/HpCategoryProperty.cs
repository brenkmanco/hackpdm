//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpCategoryProperty : HpBaseModel<HpCategoryProperty>
{
    public int cat_id;
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