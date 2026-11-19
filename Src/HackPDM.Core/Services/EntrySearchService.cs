using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

public sealed record EntrySearchQuery(
	string FileName,
	string NameComparer,
	bool CheckedOutToMe,
	bool DeletedRemotely,
	bool LocalOnly,
	int MaxResults,
	IReadOnlyList<SearchPropertiesRow> Properties );

/// <summary>Searches Odoo entries and the local workspace folder.</summary>
public interface IEntrySearchService
{
	Task<List<SearchRow>> SearchAsync( EntrySearchQuery query );
	IEnumerable<SearchPropertiesRow> GetSearchableProperties();
	IReadOnlyList<string> TreeSegmentsBelowRoot( SearchRow row );
}

public sealed class EntrySearchService : IEntrySearchService
{
	private static readonly string[] ResultFields = [ "id", "name", "directory_complete_name" ];

	public async Task<List<SearchRow>> SearchAsync( EntrySearchQuery query )
	{
		OdooDomain domain = await BuildDomainAsync( query );
		bool needsLocalScan = query.LocalOnly || query.DeletedRemotely;
		int? rpcLimit = !needsLocalScan && query.MaxResults > 0 ? query.MaxResults : null;
		Result<IReadOnlyList<OdooRow>> found = await OdooRpc.SearchReadAsync(
			HpEntry.ModelName, domain, ResultFields, limit: rpcLimit, timeoutMs: OdooRpc.LongTimeout );
		if( !found.Ok )
			HackLogger.Error( "EntrySearch", $"Entry search failed: {found.Error}" );
		IReadOnlyList<OdooRow> results = found.ValueOr( [] );

		List<SearchRow> rows;
		if( needsLocalScan )
		{
			rows = await Task.Run( () => FindLocalFiles( results, query.FileName, query.MaxResults, matchRemote: !query.LocalOnly ) );
		}
		else
		{
			int count = Math.Min( results.Count, query.MaxResults );
			rows = new List<SearchRow>( count );
			for( int i = 0; i < count; i++ )
				rows.Add( ToRow( results[ i ] ) );
		}

		rows.Sort( static ( a, b ) => string.CompareOrdinal( a.Name, b.Name ) );
		return rows;
	}

	public IEnumerable<SearchPropertiesRow> GetSearchableProperties()
		=> OdooDefaults.Instance.IdToProp.Select( pair => new SearchPropertiesRow
		{
			Name = pair.Value.name,
			ID = pair.Key,
			IsTextOrDate = pair.Value.prop_type == "text",
		} );

	public IReadOnlyList<string> TreeSegmentsBelowRoot( SearchRow row )
	{
		if( string.IsNullOrEmpty( row.Directory ) )
			return [];
		if( row.Id is not null )
			return [ .. row.Directory.Split( " / " ).Skip( 1 ) ];
		return PwaPaths.LocalSegments( row.Directory ) ?? [];
	}

	internal static async Task<OdooDomain> BuildDomainAsync( EntrySearchQuery query )
	{
		OdooDomain domain = OdooDomain.All;
		if( query.FileName.Length > 0 )
			domain = domain.And( "name", query.NameComparer, query.FileName );

		if( query.LocalOnly )
			return domain;

		if( query.CheckedOutToMe )
			domain = domain.And( "checkout_user", OdooDefaults.Instance.OdooId );
		if( query.DeletedRemotely )
			domain = domain.And( "deleted", true );
		if( query.Properties.Count > 0 )
			domain = domain.And( "id", "in", ( await EntriesMatchingAllPropertiesAsync( query.Properties ) ).ToArray() );
		return domain;
	}

	private static async Task<HashSet<int>> EntriesMatchingAllPropertiesAsync( IReadOnlyList<SearchPropertiesRow> properties )
	{
		HashSet<int>? candidates = null;
		foreach( SearchPropertiesRow property in properties )
		{
			OdooDomain filter = OdooDomain.Where( "prop_id", property.ID ).And( "text_value", property.Comparer, property.Value );
			Result<IReadOnlyList<OdooRow>> matches = await OdooRpc.SearchReadAsync(
				HpVersionProperty.ModelName, filter, [ "entry_id" ], timeoutMs: OdooRpc.LongTimeout );

			IReadOnlyList<OdooRow> rows = matches.ValueOr( [] );
			if( candidates is null )
			{
				candidates = new HashSet<int>( rows.Count );
				for( int i = 0; i < rows.Count; i++ )
				{
					if( rows[ i ].RefId( "entry_id" ) is int id and not 0 )
						candidates.Add( id );
				}
			}
			else
			{
				HashSet<int> next = new( rows.Count );
				for( int i = 0; i < rows.Count; i++ )
				{
					if( rows[ i ].RefId( "entry_id" ) is int id and not 0 && candidates.Contains( id ) )
						next.Add( id );
				}
				candidates = next;
			}
			if( candidates.Count == 0 )
				break;
		}
		return candidates ?? [];
	}

	private static SearchRow ToRow( OdooRow entry ) => new()
	{
		Id = entry.Int( "id" ),
		Name = entry.Text( "name" ),
		Directory = entry.Text( "directory_complete_name" ),
	};

	private static List<SearchRow> FindLocalFiles( IReadOnlyList<OdooRow> remoteResults, string fileName, int limit, bool matchRemote )
	{
		DirectoryInfo pwa = new( HackDefaults.Instance.PwaPathAbsolute );
		if( !pwa.Exists )
			return [];

		Dictionary<string, HashSet<string>> remotePathsByName = new( StringComparer.OrdinalIgnoreCase );
		for( int i = 0; i < remoteResults.Count; i++ )
		{
			OdooRow entry = remoteResults[ i ];
			if( entry.Text( "name" ) is not string name || entry.Text( "directory_complete_name" ) is not string path )
				continue;
			if( !remotePathsByName.TryGetValue( name, out HashSet<string>? paths ) )
				remotePathsByName[ name ] = paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
			paths.Add( path );
		}

		List<SearchRow> rows = [];
		foreach( FileInfo file in pwa.EnumerateFiles( $"*{fileName}*", SearchOption.AllDirectories ) )
		{
			if( rows.Count >= limit )
				break;

			string odooPath = PwaPaths.ToServerDirectory( file.DirectoryName ) ?? "";
			bool isRemote = remotePathsByName.TryGetValue( file.Name, out HashSet<string>? paths ) && paths.Contains( odooPath );
			if( isRemote == matchRemote )
				rows.Add( new SearchRow { Id = null, Name = file.Name, Directory = file.DirectoryName } );
		}
		return rows;
	}
}
