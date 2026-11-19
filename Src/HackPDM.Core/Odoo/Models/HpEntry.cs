using HackPDM.Core.Pdm;

namespace HackPDM.Core.Odoo.Models;

public sealed class HpEntry : HpRecord<HpEntry>, IOdooModel<HpEntry>
{
	public static string ModelName => "hp.entry";
	public static string[] AllFields { get; } =
	[
		"name", "windows_complete_name", "checkout_date", "deleted", "is_checked_out",
		"latest_version_id", "dir_id", "type_id", "checkout_user", "checkout_node", "cat_id",
	];

	public EntryReturnType returnType { get; set; }

	public HackFile? LocalFile
	{
		get => field ??= GetLocalFile();
		set;
	}

	private HpType? TypeStore
		=> field ??= LocalFile?.TypeExt is { } ext
			&& OdooDefaults.Instance.ExtToType.TryGetValue( ext.ToLower(), out HpType? found )
				? found
				: null;

	public string? name
	{
		get => field ??= LocalFile?.Name;
		set;
	}

	public string? windows_complete_name
	{
		get => field ??= LocalFile is { } local ? CommitOperations.ServerWindowsName( local ) : null;
		set;
	}

	public DateTime? checkout_date { get; set; }
	public bool? deleted { get; set; }
	public bool? is_checked_out { get; set; }
	public Many2One? latest_version_id { get; set; }
	public Many2One? dir_id { get; set; }

	public Many2One? type_id
	{
		get => field ??= TypeStore?.id;
		set;
	}

	public Many2One? checkout_user { get; set; }
	public Many2One? checkout_node { get; set; }

	public Many2One? cat_id
	{
		get => field ??= TypeStore?.id;
		set;
	}

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case "name": name = OdooValues.ReadString( value ); return true;
			case "windows_complete_name": windows_complete_name = OdooValues.ReadString( value ); return true;
			case "checkout_date": checkout_date = OdooValues.ReadDateTime( value ); return true;
			case "deleted": deleted = OdooValues.ReadBool( value ); return true;
			case "is_checked_out": is_checked_out = OdooValues.ReadBool( value ); return true;
			case "latest_version_id": latest_version_id = OdooValues.ReadMany2One( value ); return true;
			case "dir_id": dir_id = OdooValues.ReadMany2One( value ); return true;
			case "type_id": type_id = OdooValues.ReadMany2One( value ); return true;
			case "checkout_user": checkout_user = OdooValues.ReadMany2One( value ); return true;
			case "checkout_node": checkout_node = OdooValues.ReadMany2One( value ); return true;
			case "cat_id": cat_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ "name" ] = name,
		[ "windows_complete_name" ] = windows_complete_name,
		[ "checkout_date" ] = OdooValues.OutDateTime( checkout_date ),
		[ "deleted" ] = OdooValues.OutBool( deleted ),
		[ "is_checked_out" ] = OdooValues.OutBool( is_checked_out ),
		[ "latest_version_id" ] = latest_version_id?.id,
		[ "dir_id" ] = dir_id?.id,
		[ "type_id" ] = type_id?.id,
		[ "checkout_user" ] = checkout_user?.id,
		[ "checkout_node" ] = checkout_node?.id,
		[ "cat_id" ] = cat_id?.id,
	};

	public HackFile? GetLocalFile()
	{
		if( HashedValues.GetValueOrDefault( "windows_complete_name" ) is string path )
			return new HackFile( PwaPaths.ToAbsolute( path ) );
		if( ( dir_id?.name ?? OdooValues.Many2OneName( HashedValues.GetValueOrDefault( nameof( dir_id ) ) ) ) is { Length: > 0 } dir )
			return new HackFile( PwaPaths.ToAbsolute( dir ) );
		return null;
	}

	public bool CanCheckOut() => ( checkout_user?.id is null or 0 ) && deleted is false;

	public bool CanUnCheckOut( int? userId = null )
		=> checkout_user is not null && checkout_user == ( userId ?? OdooDefaults.Instance.OdooId );

	public override string ToString() => name ?? "";
}
