using System;


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM
{
    public class HpUser : HpBaseModel<HpUser>
    {
        public string name;
		public string login;
        public string email;
        public string signature;
        public string notification_type;
        public DateTime? login_date;
        
        public int? company_id;

        public bool? active;

        public HpUser() {}

		public HpUser( string name,
				string login = null,
				string email = null,
				string signature = null,
				string notification_type = null,
				DateTime? login_date = null,
				int? company_id = null,
				bool? active = null)
		{
			this.name= name;
			this.login= login;
			this.email= email;
			this.signature= signature;
			this.notification_type= notification_type;
			this.login_date= login_date;
			this.company_id= company_id;
			this.active= active;
		}
		public override string ToString()
		{
			return name;
		}
	}
}
