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
		Result<IReadOnlyList<OdooRow>> found = await OdooRpc.SearchReadAsync( HpEntry.ModelName, domain, ResultFields, timeoutMs: OdooRpc.LongTimeout );
		if( !found.Ok )
			HackLogger.Error( "EntrySearch", $"Entry search failed: {found.Error}" );
		IReadOnlyList<OdooRow> results = found.ValueOr( [] );

		List<SearchRow> rows = query.LocalOnly || query.DeletedRemotely
			? await Task.Run( () => FindLocalFiles( results, query.FileName, query.MaxResults, matchRemote: !query.LocalOnly ) )
			: [ .. results.Take( query.MaxResults ).Select( ToRow ) ];

		rows.Sort( ( a, b ) => string.CompareOrdinal( a.Name, b.Name ) );
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

			IEnumerable<int> entryIds = matches.ValueOr( [] )
				.Select( m => m.RefId( "entry_id" ) )
				.OfType<int>()
				.Where( id => id != 0 );

			if( candidates is null )
				candidates = [ .. entryIds ];
			else
				candidates.IntersectWith( entryIds );
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

		Dictionary<string, List<string>> remotePathsByName = [];
		foreach( OdooRow entry in remoteResults )
		{
			if( entry.Text( "name" ) is not string name || entry.Text( "directory_complete_name" ) is not string path )
				continue;
			string key = name.ToLower();
			if( !remotePathsByName.TryGetValue( key, out List<string>? paths ) )
				remotePathsByName[ key ] = paths = [];
			paths.Add( path );
		}

		List<SearchRow> rows = [];
		foreach( FileInfo file in pwa.EnumerateFiles( $"*{fileName}*", SearchOption.AllDirectories ) )
		{
			if( rows.Count >= limit )
				break;

			string odooPath = PwaPaths.ToServerDirectory( file.DirectoryName ) ?? "";
			bool isRemote = remotePathsByName.TryGetValue( file.Name.ToLower(), out List<string>? paths ) && paths.Contains( odooPath );
			if( isRemote == matchRemote )
				rows.Add( new SearchRow { Id = null, Name = file.Name, Directory = file.DirectoryName } );
		}
		return rows;
	}
}
