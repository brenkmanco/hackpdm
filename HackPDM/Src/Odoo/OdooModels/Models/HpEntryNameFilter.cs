//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_ENTRY_NAME_FILTER_NAME, OdooDefaults.HP_ENTRY_NAME_FILTER)]
public partial class HpEntryNameFilter : HpBaseModel<HpEntryNameFilter>
{
	[OdooField(OdooFieldType.Char)] public string name_proto;
	[OdooField(OdooFieldType.Char)] public string name_regex;
	[OdooField(OdooFieldType.Char)] public string description;
    
    public HpEntryNameFilter() { }
    public HpEntryNameFilter(
        string nameProto = null,
        string nameRegex = null,
        string description = null)
    {
        this.name_proto = nameProto;
        this.name_regex = nameRegex;
        this.description = description;
    }
}
public partial class HpEntryNameFilter : HpBaseModel<HpEntryNameFilter>
{
}