using System.Drawing;

using HackPDM.Extensions.General;


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM
{
    public class HpType : HpBaseModel<HpType>
    {
        public string description;
        public string file_ext;
        public string icon;
        public string type_regex;
        public int cat_id;
        public Image image_save {get;set;}

        public HpType()
        { 
        }
        public HpType(
            string description = null,
            string file_ext = null,
            string iconBase64 = null,
            string type_regex = null,
            int cat_id = 0)
        {
            this.description = description;
            this.file_ext = file_ext; 
            this.icon = iconBase64;
            this.type_regex = type_regex;
            this.cat_id = cat_id;
            this.image_save = null;
        }
		public HpType(
			string description = null,
			string file_ext = null,
			Image icon = null,
			string type_regex = null,
			int cat_id = 0,
            bool saveImageType = false)
		{
			this.description = description;
			this.file_ext = file_ext;
			this.type_regex = type_regex;
			this.cat_id = cat_id;

            if (saveImageType) this.image_save = icon;
            else this.image_save = null;

			this.icon = icon?.ToBase64String();
		}
	}
}
