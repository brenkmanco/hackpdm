using HackPDM.Core.Pdm;

namespace HackPDM.Core.Odoo.Models;

public sealed class HpEntry : HpRecord<HpEntry>, IOdooModel<HpEntry>
{
	public static string ModelName => "hp.entry";
	public static string[] AllFields { get; } =
	[
		nameof( name ), nameof( windows_complete_name ), nameof( checkout_date ), nameof( deleted ), nameof( is_checked_out ),
		nameof( latest_version_id ), nameof( dir_id ), nameof( type_id ), nameof( checkout_user ), nameof( checkout_node ), nameof( cat_id ),
	];

	public EntryReturnType returnType { get; set; }

	public HackFile? LocalFile
	{
		get => field ??= GetLocalFile();
		set;
	}

	private HpType? TypeStore
		=> field ??= LocalFile?.TypeExt is { } ext
			&& OdooDefaults.Instance.ExtToType.TryGetValue( ext, out HpType? found )
				? found
				: null;

	private string? _name;
	private string? _windowsCompleteName;

	public string? name
	{
		get => _name ??= LocalFile?.Name;
		set => _name = value;
	}

	public string? windows_complete_name
	{
		get => _windowsCompleteName ??= LocalFile is { } local ? CommitOperations.ServerWindowsName( local ) : null;
		set => _windowsCompleteName = value;
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
		get => field ??= TypeStore?.cat_id;
		set;
	}

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( windows_complete_name ): windows_complete_name = OdooValues.ReadString( value ); return true;
			case nameof( checkout_date ): checkout_date = OdooValues.ReadDateTime( value ); return true;
			case nameof( deleted ): deleted = OdooValues.ReadBool( value ); return true;
			case nameof( is_checked_out ): is_checked_out = OdooValues.ReadBool( value ); return true;
			case nameof( latest_version_id ): latest_version_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( dir_id ): dir_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( type_id ): type_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( checkout_user ): checkout_user = OdooValues.ReadMany2One( value ); return true;
			case nameof( checkout_node ): checkout_node = OdooValues.ReadMany2One( value ); return true;
			case nameof( cat_id ): cat_id = OdooValues.ReadMany2One( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name ) ] = name,
		[ nameof( windows_complete_name ) ] = windows_complete_name,
		[ nameof( checkout_date ) ] = OdooValues.OutDateTime( checkout_date ),
		[ nameof( deleted ) ] = OdooValues.OutBool( deleted ),
		[ nameof( is_checked_out ) ] = OdooValues.OutBool( is_checked_out ),
		[ nameof( latest_version_id ) ] = latest_version_id?.id,
		[ nameof( dir_id ) ] = dir_id?.id,
		[ nameof( type_id ) ] = type_id?.id,
		[ nameof( checkout_user ) ] = checkout_user?.id,
		[ nameof( checkout_node ) ] = checkout_node?.id,
		[ nameof( cat_id ) ] = cat_id?.id,
	};

	public HackFile? GetLocalFile()
	{
		if( !string.IsNullOrEmpty( _windowsCompleteName ) )
			return HackFile.GetFromPath( PwaPaths.ToAbsolute( _windowsCompleteName ), computeChecksum: false );
		if( dir_id?.name is { Length: > 0 } dir && !string.IsNullOrEmpty( _name ) && PwaPaths.ToAbsolute( dir ) is { } absDir )
			return HackFile.GetFromPath( Path.Combine( absDir, _name ), computeChecksum: false );
		return null;
	}

	public bool CanCheckOut() => ( checkout_user?.id is null or 0 ) && deleted is not true;

	public bool CanUnCheckOut( int? userId = null )
		=> checkout_user is not null && checkout_user == ( userId ?? OdooDefaults.Instance.OdooId );

	public override string ToString() => name ?? "";
}
