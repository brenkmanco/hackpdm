//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_PROPERTY_NAME, OdooDefaults.HP_PROPERTY)]
public partial class HpProperty : HpBaseModel<HpProperty>
{
	[OdooField(OdooFieldType.Char)] public string name;
    [OdooField(OdooFieldType.Char)] public string prop_type;
	[OdooField(OdooFieldType.Boolean)] public bool active;

    public HpProperty() { }
    public HpProperty(
        string name,
        string propType = null,
        bool active = default)
    {
        this.name = name;
        this.prop_type = propType;
        this.active = active;
    }
}
public partial class HpProperty : HpBaseModel<HpProperty>
{
    public override string ToString()
    {
        return name;
    }
}