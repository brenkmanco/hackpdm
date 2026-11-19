using System.Globalization;

using HackPDM.Core.Pdm;

namespace HackPDM.Core.Odoo.Models;

public sealed class HpVersionProperty : HpRecord<HpVersionProperty>, IOdooModel<HpVersionProperty>
{
	public static string ModelName => "hp.version.property";
	public static string[] AllFields { get; } =
	[
		"prop_name", "sw_config_name", "text_value", "number_value",
		"yesno_value", "date_value", "version_id", "prop_id",
	];

	public string? prop_name { get; set; }
	public string? sw_config_name { get; set; }
	public string? text_value { get; set; }
	public float? number_value { get; set; }
	public bool? yesno_value { get; set; }
	public DateTime? date_value { get; set; }
	public Many2One? version_id { get; set; }
	public Many2One? prop_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "prop_name": prop_name = OdooValues.ReadString( value ); return true;
			case "sw_config_name": sw_config_name = OdooValues.ReadString( value ); return true;
			case "text_value": text_value = OdooValues.ReadString( value ); return true;
			case "number_value": number_value = OdooValues.ReadFloat( value ); return true;
			case "yesno_value": yesno_value = OdooValues.ReadBool( value ); return true;
			case "date_value": date_value = OdooValues.ReadDateTime( value ); return true;
			case "version_id": version_id = OdooValues.ReadMany2One( value ); return true;
			case "prop_id": prop_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "prop_name" ] = prop_name,
		[ "sw_config_name" ] = sw_config_name,
		[ "text_value" ] = text_value,
		[ "number_value" ] = OdooValues.OutFloat( number_value ),
		[ "yesno_value" ] = OdooValues.OutBool( yesno_value ),
		[ "date_value" ] = OdooValues.OutDateTime( date_value ),
		[ "version_id" ] = version_id?.id,
		[ "prop_id" ] = prop_id?.id,
	};

	public PropertyType GetValueType()
	{
		if( !string.IsNullOrEmpty( text_value ) && text_value != "False" )
			return PropertyType.Text;
		if( date_value is not null )
			return PropertyType.Date;
		if( number_value != 0 )
			return PropertyType.Number;
		if( yesno_value is true )
			return PropertyType.Yesno;
		return PropertyType.None;
	}

	public static HpVersionProperty? FromSwProperty(
		string config,
		string name,
		string type,
		object? value,
		int versionId,
		Many2One? commitId )
	{
		HpProperty? known = null;
		bool isKnown = OdooDefaults.Instance.ExtToProp?.TryGetValue( name, out known ) ?? false;
		if( !isKnown && OdooDefaults.Instance.RestrictProperties is not false )
			return null;

		HpVersionProperty property = new()
		{
			sw_config_name = config == "" ? null : config,
			version_id = versionId,
			commit_id = commitId,
		};
		if( known is not null )
			property.prop_id = known.id ?? 0;

		switch( type )
		{
			case "text": property.text_value = (string?)value; break;
			case "date": property.date_value = (DateTime?)value; break;
			case "yesno": property.yesno_value = (bool?)value; break;
			case "number": property.number_value = value is null ? null : Convert.ToSingle( value, CultureInfo.InvariantCulture ); break;
		}
		return property;
	}
}
