using System;
// ReSharper disable InconsistentNaming


//using static System.Net.Mime.MediaTypeNames;



namespace HackPDM.Odoo.OdooModels.Models;

[OdooModel(OdooDefaults.HP_SETTINGS_NAME, OdooDefaults.HP_SETTINGS)]
public partial class HpSetting : HpBaseModel<HpSetting>
{
	[OdooField(OdooFieldType.Char)] public string name;
	[OdooField(OdooFieldType.Char)] public string description;
	[OdooField(OdooFieldType.Char)] public string type;
	[OdooField(OdooFieldType.Boolean)] public bool bool_value;
	[OdooField(OdooFieldType.Integer)] public int int_value;
	[OdooField(OdooFieldType.Char)] public string char_value;
	[OdooField(OdooFieldType.Float)] public float float_value;
	[OdooField(OdooFieldType.DateTime)] public DateTime date_value;

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