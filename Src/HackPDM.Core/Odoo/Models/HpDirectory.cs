using HackPDM.Core.Pdm;

namespace HackPDM.Core.Odoo.Models;

public sealed class HpDirectory : HpRecord<HpDirectory>, IOdooModel<HpDirectory>
{
	public static string ModelName => "hp.directory";
	public static string[] AllFields { get; } = [ nameof( name ), nameof( parent_path ), nameof( parent_id ), nameof( default_cat ), nameof( deleted ), nameof( sandboxed ) ];

	public string? name { get; set; }
	public string? parent_path { get; set; }
	public Many2One? parent_id { get; set; }
	public Many2One? default_cat { get; set; }
	public bool? deleted { get; set; }
	public bool? sandboxed { get; set; }

	public override bool TrySetField( string field, object? value )
	{
		switch( field )
		{
			case nameof( name ): name = OdooValues.ReadString( value ); return true;
			case nameof( parent_path ): parent_path = OdooValues.ReadString( value ); return true;
			case nameof( parent_id ): parent_id = OdooValues.ReadMany2One( value ); return true;
			case nameof( default_cat ): default_cat = OdooValues.ReadMany2One( value ); return true;
			case nameof( deleted ): deleted = OdooValues.ReadBool( value ); return true;
			case nameof( sandboxed ): sandboxed = OdooValues.ReadBool( value ); return true;
			default: return false;
		}
	}

	public override Dictionary<string, object?> ToOdoo() => new()
	{
		[ nameof( name ) ] = name,
		[ nameof( parent_path ) ] = parent_path,
		[ nameof( parent_id ) ] = parent_id?.id,
		[ nameof( default_cat ) ] = default_cat?.id,
		[ nameof( deleted ) ] = OdooValues.OutBool( deleted ),
		[ nameof( sandboxed ) ] = OdooValues.OutBool( sandboxed ),
	};

	public static async Task<Result<LastAvailableDirectory>> LastAvailableDirectory( IReadOnlyList<string> paths, CancellationToken token = default )
	{
		Result<OdooRow> last = await OdooRpc.ExecuteAsync<OdooRow>( ModelName, "last_available_directory", [ paths ], token: token );
		return last.Map( row => new LastAvailableDirectory( row?.Int( "index" ) ?? 0, row?.Int( "dir_id" ) ?? 1 ) );
	}

	public static async Task<HpDirectory[]?> CreateNew( IEnumerable<string> paths, CancellationToken token = default )
	{
		List<string> path = [ PwaPaths.Root, .. paths ];
		LastAvailableDirectory last = ( await LastAvailableDirectory( path, token ) ).Unwrap();
		int nextIndex = last.Index + 1;
		if( nextIndex >= path.Count )
			return await GetRecordByIdAsync( last.DirectoryId, token: token ) is { } existing ? [ existing ] : null;

		Many2One? defaultCategory = OdooDefaults.Instance.HpCategories?.Any( c => c.id == 1 ) is true ? 1 : null;
		HpDirectory[] directories = new HpDirectory[ path.Count - nextIndex ];
		int lastParentId = last.DirectoryId;

		for( int i = nextIndex; i < path.Count; i++ )
		{
			token.ThrowIfCancellationRequested();
			HpDirectory newDirectory = new()
			{
				name = path[ i ],
				parent_id = lastParentId,
				sandboxed = false,
				deleted = false,
				default_cat = defaultCategory,
			};
			if( await newDirectory.CreateAsync( false, token: token ) == 0 )
				throw new InvalidOperationException( $"directory '{path[ i ]}' not created: {newDirectory.LastError}" );

			directories[ i - nextIndex ] = newDirectory;
			lastParentId = newDirectory.id ?? 0;
		}
		return directories;
	}

	public async Task<Result<SubdirectoryDto>> GetSubdirectoriesAsync( bool withEntries = true, CancellationToken token = default )
	{
		if( !IsRecord || id is not int directoryId )
			return Result<SubdirectoryDto>.Fail( "not a directory read from the server" );
		Result<OdooRow> tree = await OdooRpc.ExecuteAsync<OdooRow>(
			ModelName,
			"get_children_directories_by_id",
			[ directoryId, withEntries ],
			OdooRpc.LongTimeout,
			token );
		return tree.Map( row => SubdirectoryDto.FromRow( row ?? [] ) );
	}

	public static async Task<Result<IReadOnlyList<RemoteEntryDto>>> GetEntriesAsync( int directoryId, bool showInActive = false, CancellationToken token = default )
	{
		Result<OdooRow> entries = await OdooRpc.ExecuteAsync<OdooRow>(
			ModelName,
			"get_entries",
			[ new object?[] { directoryId, showInActive } ],
			OdooRpc.LongTimeout,
			token );
		return entries.Map( RemoteEntryDto.ListFrom );
	}

	public static Task<Result<IReadOnlyList<int>>> GetDirectoryEntryIDsAsync(
		int directoryId,
		bool withSubEntries = false,
		bool withDeleted = false,
		CancellationToken token = default )
		=> directoryId != 0
			? OdooRpc.ExecuteAsync<IReadOnlyList<int>>( ModelName, "get_all_entry_ids", [ directoryId, withDeleted, withSubEntries ], 60_000, token )
			: Task.FromResult( Result<IReadOnlyList<int>>.Fail( "no directory" ) );
}
