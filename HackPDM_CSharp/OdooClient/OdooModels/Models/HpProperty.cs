//using static System.Net.Mime.MediaTypeNames;

namespace HackPDM
{
    public class HpProperty : HpBaseModel<HpProperty>
    {
        public string name;
        public string prop_type;
        public bool active;

        public HpProperty() { }
        public HpProperty(
            string name,
            string prop_type = null,
            bool active = default)
        {
            this.name = name;
            this.prop_type = prop_type;
            this.active = active;
        }
		public override string ToString()
		{
			return name;
		}
	}
}
