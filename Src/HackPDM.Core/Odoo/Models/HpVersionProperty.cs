using System.Globalization;

using HackPDM.Core.Pdm;

namespace HackPDM.Core.Odoo.Models;

public sealed class HpVersionProperty : HpRecord<HpVersionProperty>, IOdooModel<HpVersionProperty>
{
	public static string ModelName => "hp.version.property";
	public static string[] AllFields { get; } =
	[
		nameof( prop_name ), nameof( sw_config_name ), nameof( text_value ), nameof( number_value ),
		nameof( yesno_value ), nameof( date_value ), nameof( version_id ), nameof( prop_id ), nameof( entry_id ),
	];

	public string? prop_name { get; set; }
	public string? sw_config_name { get; set; }
	public string? text_value { get; set; }
	public float? number_value { get; set; }
	public bool? yesno_value { get; set; }
	public DateTime? date_value { get; set; }
	public Many2One? version_id { get; set; }
	public Many2One? prop_id { get; set; }
	public Many2One? entry_id { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( prop_name ): prop_name = OdooValues.ReadString( value ); return true;
			case nameof( sw_config_name ): sw_config_name = OdooValues.ReadString( value ); return true;
			case nameof( text_value ): text_value = OdooValues.ReadString( value ); return true;
			case nameof( number_value ): number_value = OdooValues.ReadFloat( value ); return true;
			case nameof( yesno_value ): yesno_value = OdooValues.ReadBool( value ); return true;
			case nameof( date_value ): date_value = OdooValues.ReadDateTime( value ); return true;
			case nameof( version_id ): version_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( prop_id ): prop_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( entry_id ): entry_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( prop_name ) ] = prop_name,
		[ nameof( sw_config_name ) ] = sw_config_name,
		[ nameof( text_value ) ] = text_value,
		[ nameof( number_value ) ] = OdooValues.OutFloat( number_value ),
		[ nameof( yesno_value ) ] = OdooValues.OutBool( yesno_value ),
		[ nameof( date_value ) ] = OdooValues.OutDateTime( date_value ),
		[ nameof( version_id ) ] = version_id?.id,
		[ nameof( prop_id ) ] = prop_id?.id,
		[ nameof( entry_id ) ] = entry_id?.id,
	};

	public PropertyType GetValueType()
	{
		if( prop_id?.id is int propId
			&& OdooDefaults.Instance?.IdToProp.TryGetValue( propId, out HpProperty? prop ) is true )
		{
			switch( prop.prop_type )
			{
				case "text": return PropertyType.Text;
				case "number": return PropertyType.Number;
				case "yesno": return PropertyType.Yesno;
				case "date": return PropertyType.Date;
			}
		}

		if( !string.IsNullOrEmpty( text_value ) && text_value != "False" )
			return PropertyType.Text;
		if( date_value is not null )
			return PropertyType.Date;
		if( number_value is not null and not 0f )
			return PropertyType.Number;
		if( yesno_value is true )
			return PropertyType.Yesno;
		if( number_value is not null && yesno_value is null && text_value is null && date_value is null )
			return PropertyType.Number;
		if( yesno_value is not null && number_value is null && text_value is null && date_value is null )
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
		else
			property.prop_name = name;

		switch( type )
		{
			case "text":
				property.text_value = value as string ?? value?.ToString();
				break;
			case "date":
				if( value is DateTime dt )
					property.date_value = dt;
				else if( value is string ds && DateTime.TryParse( ds, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate ) )
					property.date_value = parsedDate;
				break;
			case "yesno":
				if( value is bool b )
					property.yesno_value = b;
				else if( value is string bs && bool.TryParse( bs, out bool parsedBool ) )
					property.yesno_value = parsedBool;
				break;
			case "number":
				if( value is float f )
					property.number_value = f;
				else if( value is IConvertible )
				{
					try { property.number_value = Convert.ToSingle( value, CultureInfo.InvariantCulture ); }
					catch( Exception e ) when( e is FormatException or OverflowException or InvalidCastException ) { }
				}
				break;
		}
		return property;
	}
}
