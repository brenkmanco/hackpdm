//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpProperty : HpBaseModel<HpProperty>
{
    public string name;
    public string prop_type;
    public bool active;

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