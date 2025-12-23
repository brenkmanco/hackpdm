//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
using System;

namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_CATEGORY_NAME, OdooDefaults.HP_CATEGORY)]
public partial class HpCategory : HpBaseModel<HpCategory>
{
    internal readonly string[] UsualExcludedFields = [];
    [OdooField(OdooFieldType.Char)]
    public string name;
	[OdooField(OdooFieldType.Char)]
	public string cat_description;
	[OdooField(OdooFieldType.Boolean)]
	public bool track_version;
	[OdooField(OdooFieldType.Boolean)]
	public bool track_depends;
    public HpCategory() { }
    public HpCategory(
        string name,
        string catDescription = "CAD files are versioned and have dependencies",
        bool trackVersion = true,
        bool trackDepends = true)
    {
        this.name = name;
        this.cat_description = catDescription;
        this.track_version = trackVersion;
        this.track_depends = trackDepends;
    }
}
public partial class HpCategory : HpBaseModel<HpCategory>
{
    public override string ToString()
    {
        return name;
    }
}