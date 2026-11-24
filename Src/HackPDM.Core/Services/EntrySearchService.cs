using System.Globalization;

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
	private static readonly string[] ResultFields = [ nameof( HpEntry.id ), nameof( HpEntry.name ), "directory_complete_name" ];
	private static readonly EnumerationOptions LocalSearchOptions = new()
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		ReturnSpecialDirectories = false,
	};

	public async Task<List<SearchRow>> SearchAsync( EntrySearchQuery query )
	{
		var (domain, impossible) = await BuildDomainWithStatusAsync( query );
		if( impossible )
			return [];

		bool needsLocalScan = query.LocalOnly || query.DeletedRemotely;
		int maxResults = query.MaxResults > 0 ? query.MaxResults : int.MaxValue;
		int? rpcLimit = !needsLocalScan && query.MaxResults > 0 ? query.MaxResults : null;
		Result<IReadOnlyList<OdooRow>> found = await OdooRpc.SearchReadAsync(
			HpEntry.ModelName, domain, ResultFields, limit: rpcLimit, timeoutMs: OdooRpc.LongTimeout );
		if( !found.Ok )
			HackLogger.Error( "EntrySearch", $"Entry search failed: {found.Error}" );
		IReadOnlyList<OdooRow> results = found.ValueOr( [] );

		List<SearchRow> rows;
		if( needsLocalScan )
		{
			rows = await Task.Run( () => FindLocalFiles( results, query.FileName, maxResults, matchRemote: !query.LocalOnly ) );
		}
		else
		{
			int count = Math.Min( results.Count, maxResults );
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
			IsTextOrDate = pair.Value.prop_type is "text" or "date",
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
		=> ( await BuildDomainWithStatusAsync( query ) ).Domain;

	private static async Task<(OdooDomain Domain, bool Impossible)> BuildDomainWithStatusAsync( EntrySearchQuery query )
	{
		OdooDomain domain = OdooDomain.All;
		if( query.FileName.Length > 0 )
			domain = domain.And( nameof( HpEntry.name ), query.NameComparer, query.FileName );

		if( query.LocalOnly )
			return ( domain, false );

		if( query.CheckedOutToMe )
			domain = domain.And( nameof( HpEntry.checkout_user ), OdooDefaults.Instance.OdooId );
		if( query.DeletedRemotely )
			domain = domain.And( nameof( HpEntry.deleted ), true );
		if( query.Properties.Count > 0 )
		{
			int[] matchedIds = [ .. await EntriesMatchingAllPropertiesAsync( query.Properties ) ];
			domain = domain.And( nameof( HpEntry.id ), "in", matchedIds );
			if( matchedIds.Length == 0 )
				return ( domain, true );
		}
		return ( domain, false );
	}

	private static (string Field, object? Value) PropertyFilterTerm( SearchPropertiesRow property )
	{
		string? propType = OdooDefaults.Instance.IdToProp.GetValueOrDefault( property.ID )?.prop_type;
		return propType switch
		{
			"number" when double.TryParse( property.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double num )
				=> ( nameof( HpVersionProperty.number_value ), num ),
			"yesno" when bool.TryParse( property.Value, out bool flag )
				=> ( nameof( HpVersionProperty.yesno_value ), flag ),
			"date" => ( nameof( HpVersionProperty.date_value ), property.Value ),
			_ => ( nameof( HpVersionProperty.text_value ), property.Value ),
		};
	}

	private static async Task<HashSet<int>> EntriesMatchingAllPropertiesAsync( IReadOnlyList<SearchPropertiesRow> properties )
	{
		HashSet<int>? candidates = null;
		foreach( SearchPropertiesRow property in properties )
		{
			var (field, value) = PropertyFilterTerm( property );
			OdooDomain filter = OdooDomain.Where( nameof( HpVersionProperty.prop_id ), property.ID ).And( field, property.Comparer, value );
			Result<IReadOnlyList<OdooRow>> matches = await OdooRpc.SearchReadAsync(
				HpVersionProperty.ModelName, filter, [ nameof( HpVersionProperty.entry_id ) ], timeoutMs: OdooRpc.LongTimeout );

			IReadOnlyList<OdooRow> rows = matches.ValueOr( [] );
			if( candidates is null )
			{
				candidates = new HashSet<int>( rows.Count );
				for( int i = 0; i < rows.Count; i++ )
				{
					if( rows[ i ].RefId( nameof( HpVersionProperty.entry_id ) ) is int id and not 0 )
						candidates.Add( id );
				}
			}
			else
			{
				HashSet<int> next = new( rows.Count );
				for( int i = 0; i < rows.Count; i++ )
				{
					if( rows[ i ].RefId( nameof( HpVersionProperty.entry_id ) ) is int id and not 0 && candidates.Contains( id ) )
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
		Id = entry.Int( nameof( HpEntry.id ) ),
		Name = entry.Text( nameof( HpEntry.name ) ),
		Directory = entry.Text( "directory_complete_name" ),
	};

	private static List<SearchRow> FindLocalFiles( IReadOnlyList<OdooRow> remoteResults, string fileName, int limit, bool matchRemote )
	{
		string pwaPath = HackDefaults.Instance.PwaPathAbsolute;
		if( string.IsNullOrWhiteSpace( pwaPath ) )
			return [];
		DirectoryInfo pwa = new( pwaPath );
		if( !pwa.Exists )
			return [];

		Dictionary<string, HashSet<string>> remotePathsByName = new( StringComparer.OrdinalIgnoreCase );
		for( int i = 0; i < remoteResults.Count; i++ )
		{
			OdooRow entry = remoteResults[ i ];
			if( entry.Text( nameof( HpEntry.name ) ) is not string name || entry.Text( "directory_complete_name" ) is not string path )
				continue;
			if( !remotePathsByName.TryGetValue( name, out HashSet<string>? paths ) )
				remotePathsByName[ name ] = paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
			paths.Add( path );
		}

		List<SearchRow> rows = [];
		foreach( FileInfo file in pwa.EnumerateFiles( $"*{fileName}*", LocalSearchOptions ) )
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
