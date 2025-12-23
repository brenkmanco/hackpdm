using System;
// ReSharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.RES_USERS_NAME, OdooDefaults.RES_USERS)]
public partial class HpUser : HpBaseModel<HpUser>
{
	[OdooField(OdooFieldType.Char)] public string name;
	[OdooField(OdooFieldType.Char)] public string login;
	[OdooField(OdooFieldType.Char)] public string email;
	[OdooField(OdooFieldType.Char)] public string signature;
	
	[OdooField(OdooFieldType.DateTime)] public DateTime? login_date;
	
	[OdooField(OdooFieldType.Boolean)] public bool? active;
	
	[OdooField(OdooFieldType.Many2one)] public int? company_id;

	public HpUser() {}

	public HpUser( string name,
		string? login = null,
		string? email = null,
		string? signature = null,
		DateTime? loginDate = null,
		int? companyId = null,
		bool? active = null)
	{
		this.name= name;
		this.login= login;
		this.email= email;
		this.signature= signature;
		this.login_date= loginDate;
		this.company_id= companyId;
		this.active= active;
	}
	
}
public partial class HpUser : HpBaseModel<HpUser> 
{
	public override string ToString()
	{
		return name;
	}
}