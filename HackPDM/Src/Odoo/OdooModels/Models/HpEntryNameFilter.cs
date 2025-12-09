//using static System.Net.Mime.MediaTypeNames;

// ReSharper disable InconsistentNaming
namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpEntryNameFilter : HpBaseModel<HpEntryNameFilter>
{
	public string name_proto;
        public string name_regex;
        public string description;
    
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