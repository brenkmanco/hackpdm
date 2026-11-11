using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.Models.Odoo;

/// <summary>What the search window asks for.</summary>
/// <param name="FileName">Name filter; empty matches everything.</param>
/// <param name="NameComparer">Odoo domain operator for the name ("ilike", "=", ...).</param>
/// <param name="CheckedOutToMe">Only entries checked out to the current user.</param>
/// <param name="DeletedRemotely">List local files whose remote entry is deleted.</param>
/// <param name="LocalOnly">List local files that have no remote entry.</param>
/// <param name="Properties">Property filters; an entry must match all of them.</param>
public sealed record EntrySearchQuery(
	string FileName,
	string NameComparer,
	bool CheckedOutToMe,
	bool DeletedRemotely,
	bool LocalOnly,
	int MaxResults,
	IReadOnlyList<SearchPropertiesRow> Properties );

/// <summary>Searches Odoo entries (and the local PWA) for the search window.</summary>
public interface IEntrySearchService
{
	/// <summary>Results sorted by name, at most <see cref="EntrySearchQuery.MaxResults"/>.</summary>
	Task<List<SearchRow>> SearchAsync( EntrySearchQuery query );

	/// <summary>Properties the user can filter on.</summary>
	IEnumerable<SearchPropertiesRow> GetSearchableProperties();

	/// <summary>Tree segments below the root folder for a result's directory (used to locate it in the file manager).</summary>
	IReadOnlyList<string> TreeSegmentsBelowRoot( SearchRow row );
}

public sealed class EntrySearchService : IEntrySearchService
{
	private static readonly string[] ResultFields = [ "id", "name", "directory_complete_name" ];

	public async Task<List<SearchRow>> SearchAsync( EntrySearchQuery query )
	{
		OdooDomain domain = await BuildDomainAsync( query );
		Result<IReadOnlyList<OdooRow>> found = await OdooRpc.SearchReadAsync( HpEntry.GetHpModel()!, domain, ResultFields, timeoutMs: OdooRpc.LongTimeout );
		if( !found.Ok )
			System.Diagnostics.Debug.WriteLine( $"entry search failed: {found.Error}" );
		IReadOnlyList<OdooRow> results = found.ValueOr( [] );

		List<SearchRow> rows = query.LocalOnly || query.DeletedRemotely
			// local only: files with no matching entry; deleted: files whose entry is deleted
			? await Task.Run( () => FindLocalFiles( results, query.FileName, query.MaxResults, matchRemote: !query.LocalOnly ) )
			: [ .. results.Take( query.MaxResults ).Select( ToRow ) ];

		rows.Sort( ( a, b ) => string.CompareOrdinal( a.Name, b.Name ) );
		return rows;
	}

	public IEnumerable<SearchPropertiesRow> GetSearchableProperties()
		=> OdooDefaults.Instance?.IdToProp?.Select( pair => new SearchPropertiesRow
		{
			Name = pair.Value.name,
			ID = pair.Key,
			IsTextOrDate = pair.Value.prop_type == "text",
		} ) ?? [];

	public IReadOnlyList<string> TreeSegmentsBelowRoot( SearchRow row )
	{
		if( string.IsNullOrEmpty( row.Directory ) )
			return [];

		// remote results: "root / a / b"
		if( row.Id is not null )
			return row.Directory.Split( " / " ).Skip( 1 ).ToArray();

		// local results: a folder inside the PWA
		return PwaPaths.LocalSegments( row.Directory ) ?? [];
	}

	// internal: covered by HackPDM.Tests (Characterization/EntrySearchDomainTests), which compiles this file in
	internal static async Task<OdooDomain> BuildDomainAsync( EntrySearchQuery query )
	{
		OdooDomain domain = OdooDomain.All;
		if( query.FileName.Length > 0 )
			domain = domain.And( "name", query.NameComparer, query.FileName );

		// remote filters don't apply to a local-only search
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

	/// <summary>Ids of entries that have a version matching every property filter.</summary>
	private static async Task<HashSet<int>> EntriesMatchingAllPropertiesAsync( IReadOnlyList<SearchPropertiesRow> properties )
	{
		HashSet<int>? candidates = null;
		foreach( SearchPropertiesRow property in properties )
		{
			OdooDomain filter = OdooDomain.Where( "prop_id", property.ID ).And( "text_value", property.Comparer, property.Value );
			Result<IReadOnlyList<OdooRow>> matches = await OdooRpc.SearchReadAsync( HpVersionProperty.GetHpModel()!, filter, [ "entry_id" ], timeoutMs: OdooRpc.LongTimeout );

			IEnumerable<int> entryIds = matches.ValueOr( [] ).Select( m => m.RefId( "entry_id" ) ).OfType<int>().Where( id => id != 0 );

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

	/// <summary>
	/// Local files named like <paramref name="fileName"/>. With <paramref name="matchRemote"/> only files that
	/// appear in <paramref name="remoteResults"/> are kept; without it only files that don't.
	/// </summary>
	private static List<SearchRow> FindLocalFiles( IReadOnlyList<OdooRow> remoteResults, string fileName, int limit, bool matchRemote )
	{
		var defaults = HackDefaults.Instance;
		DirectoryInfo pwa = new( defaults.PwaPathAbsolute );
		if( !pwa.Exists )
			return [];

		Dictionary<string, List<string>> remotePathsByName = [];
		foreach( OdooRow entry in remoteResults )
		{
			if( entry.Text( "name" ) is not string name || entry.Text( "directory_complete_name" ) is not string path )
				continue;
			string key = name.ToLower();
			if( !remotePathsByName.TryGetValue( key, out var paths ) )
				remotePathsByName[ key ] = paths = [];
			paths.Add( path );
		}

		List<SearchRow> rows = [];
		foreach( FileInfo file in pwa.EnumerateFiles( $"*{fileName}*", SearchOption.AllDirectories ) )
		{
			if( rows.Count >= limit )
				break;

			string odooPath = PwaPaths.ToServerDirectory( file.DirectoryName ) ?? "";
			bool isRemote = remotePathsByName.TryGetValue( file.Name.ToLower(), out var paths ) && paths.Contains( odooPath );
			if( isRemote == matchRemote )
				rows.Add( new SearchRow { Id = null, Name = file.Name, Directory = file.DirectoryName } );
		}
		return rows;
	}
}
