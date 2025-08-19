//using static System.Net.Mime.MediaTypeNames;

namespace HackPDM
{
    public class HpEntryNameFilter : HpBaseModel<HpEntryNameFilter>
    {
        public string name_proto;
        public string name_regex;
        public string description;

        public HpEntryNameFilter() { }
        public HpEntryNameFilter(
            string name_proto = null,
            string name_regex = null,
            string description = null)
        {
            this.name_proto = name_proto;
            this.name_regex = name_regex;
            this.description = description;
        }
    }
}
