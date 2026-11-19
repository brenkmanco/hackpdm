using HackPDM.Core.Pdm;

namespace HackPDM.Core.Odoo.Models;

public sealed class HpVersion : HpRecord<HpVersion>, IOdooModel<HpVersion>
{
	public static string ModelName => "hp.version";
	public static string[] AllFields { get; } =
	[
		nameof( name ), nameof( file_ext ), nameof( checksum ), nameof( windows_complete_name ),
		nameof( entry_id ), nameof( node_id ), nameof( dir_id ), nameof( checkout_user ), nameof( attachment_id ),
		nameof( parent_ids ), nameof( child_ids ), nameof( file_modify_stamp ), nameof( file_size ),
		nameof( preview_image ), nameof( file_contents ),
	];

	static HpVersion() => UsualExcludedFields = [ nameof( preview_image ), nameof( file_contents ) ];

	public string? name { get; set; }
	public string? file_ext { get; set; }
	public string? checksum { get; set; }
	public string? windows_complete_name { get; set; }
	public Many2One? entry_id { get; set; }
	public Many2One? node_id { get; set; }
	public Many2One? dir_id { get; set; }
	public Many2One? checkout_user { get; set; }
	public Many2One? attachment_id { get; set; }
	public Many2Many? parent_ids { get; set; }
	public Many2Many? child_ids { get; set; }
	public DateTime? file_modify_stamp { get; set; }
	public int? file_size { get; set; }
	public byte[]? preview_image { get; set; }
	public byte[]? file_contents { get; set; }

	public string? WinPathway
	{
		get => field ??= windows_complete_name is null ? null : PwaPaths.FolderOf( windows_complete_name );
		set;
	}

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( file_ext ): file_ext = OdooValues.ReadString( value ); return true;
			case nameof( checksum ): checksum = OdooValues.ReadString( value ); return true;
			case nameof( windows_complete_name ): windows_complete_name = OdooValues.ReadString( value ); return true;
			case nameof( entry_id ): entry_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( node_id ): node_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( dir_id ): dir_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( checkout_user ): checkout_user = OdooValues.ReadMany2One( value ); return true;
			case nameof( attachment_id ): attachment_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( parent_ids ): parent_ids = OdooValues.ReadMany2Many( value ); return true;
			case nameof( child_ids ): child_ids = OdooValues.ReadMany2Many( value ); return true;
			case nameof( file_modify_stamp ): file_modify_stamp = OdooValues.ReadDateTime( value ); return true;
			case nameof( file_size ): file_size = OdooValues.ReadInt( value ); return true;
			case nameof( preview_image ): preview_image = OdooValues.ReadBytes( value ); return true;
			case nameof( file_contents ): file_contents = OdooValues.ReadBytes( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => ToOdoo( excludedFields: null );

	protected override Dictionary<string, object?> ToOdoo( string[]? excludedFields )
	{
		bool skipPreview = excludedFields is not null && Array.IndexOf( excludedFields, nameof( preview_image ) ) >= 0;
		bool skipContents = excludedFields is not null && Array.IndexOf( excludedFields, nameof( file_contents ) ) >= 0;
		return new()
		{
			[ nameof( name ) ] = name,
			[ nameof( file_ext ) ] = file_ext,
			[ nameof( checksum ) ] = checksum,
			[ nameof( windows_complete_name ) ] = windows_complete_name,
			[ nameof( entry_id ) ] = entry_id?.id,
			[ nameof( node_id ) ] = node_id?.id,
			[ nameof( dir_id ) ] = dir_id?.id,
			[ nameof( checkout_user ) ] = checkout_user?.id,
			[ nameof( attachment_id ) ] = attachment_id?.id,
			[ nameof( parent_ids ) ] = OdooValues.OutX2Many( parent_ids ),
			[ nameof( child_ids ) ] = OdooValues.OutX2Many( child_ids ),
			[ nameof( file_modify_stamp ) ] = OdooValues.OutDateTime( file_modify_stamp ),
			[ nameof( file_size ) ] = file_size,
			[ nameof( preview_image ) ] = skipPreview ? null : OdooValues.OutBytes( preview_image ),
			[ nameof( file_contents ) ] = skipContents ? null : OdooValues.OutBytes( file_contents ),
		};
	}

	public override void CompleteConstruction()
	{
		if( dir_id?.name is { Length: > 0 } directory )
			WinPathway = PwaPaths.ToRelative( directory );
		else if( HashedValues.GetValueOrDefault( "directory_complete_name" ) is string completeName )
			WinPathway = PwaPaths.ToRelative( completeName );
		base.CompleteConstruction();
	}

	public static async Task<List<HpVersionProperty[]?>> GetAllVersionPropertiesAsync( IReadOnlyList<int> ids )
	{
		if( ids.Count == 0 )
			return [];
		const string versionPropField = "version_property_ids";
		Result<IReadOnlyList<OdooRow>> rowsResult = await OdooRpc.ReadAsync( ModelName, ids, [ versionPropField ] );
		IReadOnlyList<OdooRow> rows = rowsResult.ValueOr( [] );
		if( rows.Count == 0 )
			return [];

		List<IReadOnlyList<int>> perRowPropIds = new( rows.Count );
		HashSet<int> uniquePropIds = [];
		for( int i = 0; i < rows.Count; i++ )
		{
			IReadOnlyList<int> propIds = rows[ i ].RefIds( versionPropField );
			perRowPropIds.Add( propIds );
			for( int j = 0; j < propIds.Count; j++ )
				uniquePropIds.Add( propIds[ j ] );
		}

		Dictionary<int, HpVersionProperty>? propById = null;
		if( uniquePropIds.Count > 0 )
		{
			HpVersionProperty[]? fetched = await HpVersionProperty.GetRecordsByIdsAsync( [ .. uniquePropIds ] );
			if( fetched is { Length: > 0 } )
			{
				propById = new Dictionary<int, HpVersionProperty>( fetched.Length );
				foreach( HpVersionProperty p in fetched )
				{
					if( p.id is int pid and not 0 )
						propById[ pid ] = p;
				}
			}
		}

		List<HpVersionProperty[]?> versionProperties = new( rows.Count );
		for( int i = 0; i < perRowPropIds.Count; i++ )
		{
			IReadOnlyList<int> propIds = perRowPropIds[ i ];
			if( propIds.Count == 0 || propById is null )
			{
				versionProperties.Add( null );
				continue;
			}
			List<HpVersionProperty> list = new( propIds.Count );
			for( int j = 0; j < propIds.Count; j++ )
			{
				if( propById.TryGetValue( propIds[ j ], out HpVersionProperty? prop ) )
					list.Add( prop );
			}
			versionProperties.Add( list.Count > 0 ? [ .. list ] : null );
		}
		return versionProperties;
	}

	public override string ToString() => name ?? "";
}
