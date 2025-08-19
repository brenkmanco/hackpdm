using System;


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM
{
    public class HpSetting : HpBaseModel<HpSetting>
	{
        public string name;
        public string description;
        public string type;
        public bool bool_value;
        public int int_value;
        public string char_value;
        public float float_value;
        public DateTime date_value;

		public HpSetting()
		{
		}
        public HpSetting(
            string name,
            string description,
            string type,
            bool bool_value=default,
            int int_value=default,
            string char_value=null,
            float float_value=default,
            DateTime date_value=default)
		{
			this.name = name;
            this.description = description;
            this.type = type;
            this.bool_value = bool_value;
            this.int_value = int_value;
            this.char_value = char_value;
            this.float_value = float_value;
            this.date_value = date_value;
		}
	}
}
