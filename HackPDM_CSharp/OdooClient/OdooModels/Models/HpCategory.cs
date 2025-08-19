//using static System.Net.Mime.MediaTypeNames;

namespace HackPDM
{
    public class HpCategory : HpBaseModel<HpCategory>
    {
        internal readonly string[] usualExcludedFields = [];
        public string name;
        public string cat_description;
        public bool track_version;
        public bool track_depends;

        public HpCategory() { }
        public HpCategory(
            string name,
            string cat_description = "CAD files are versioned and have dependencies",
            bool track_version = true,
            bool track_depends = true)
        {
            this.name = name;
            this.cat_description = cat_description;
            this.track_version = track_version;
            this.track_depends = track_depends;
        }
		public override string ToString()
		{
			return name;
		}
	}
}
