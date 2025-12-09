//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
using System;

namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpCategory : HpBaseModel<HpCategory>
{
    internal readonly string[] UsualExcludedFields = [];
    public string name;
    public string cat_description;
    public bool track_version;
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