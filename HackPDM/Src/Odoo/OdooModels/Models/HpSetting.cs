using System;
// ReSharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Odoo.OdooModels.Models;

public partial class HpSetting : HpBaseModel<HpSetting>
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
		bool boolValue=default,
		int intValue=default,
		string charValue=null,
		float floatValue=default,
		DateTime dateValue=default)
	{
		this.name = name;
		this.description = description;
		this.type = type;
		this.bool_value = boolValue;
		this.int_value = intValue;
		this.char_value = charValue;
		this.float_value = floatValue;
		this.date_value = dateValue;
	}
}
public partial class HpSetting : HpBaseModel<HpSetting> {}