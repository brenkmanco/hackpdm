using System;
// ReSharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpUser : HpBaseModel<HpUser>
{
	public string name;
	public string login;
	public string email;
	public string signature;
	public DateTime? login_date;
        
	public int? company_id;

	public bool? active;

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