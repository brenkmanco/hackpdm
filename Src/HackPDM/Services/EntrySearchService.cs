using System;
using System.Collections;
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

namespace HackPDM.Services;

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
	private const int BrowseLimit = 10000;
	private static readonly ArrayList ResultFields = [ "id", "name", "directory_complete_name" ];

	public async Task<List<SearchRow>> SearchAsync( EntrySearchQuery query )
	{
		ArrayList domain = await BuildDomainAsync( query );
		ArrayList results = await OdooClient.BrowseAsync( HpEntry.GetHpModel()!, [ domain, ResultFields ], BrowseLimit ) ?? [];

		List<SearchRow> rows = query.LocalOnly || query.DeletedRemotely
			// local only: files with no matching entry; deleted: files whose entry is deleted
			? await Task.Run( () => FindLocalFiles( results, query.FileName, query.MaxResults, matchRemote: !query.LocalOnly ) )
			: [ .. results.OfType<Hashtable>().Take( query.MaxResults ).Select( ToRow ) ];

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

		// local results: a Windows folder inside the PWA
		string relative = Path.GetRelativePath( HackDefaults.Instance.PwaPathAbsolute, row.Directory );
		return relative == "." ? [] : relative.Split( [ '\\', '/' ], StringSplitOptions.RemoveEmptyEntries );
	}

	private static async Task<ArrayList> BuildDomainAsync( EntrySearchQuery query )
	{
		ArrayList domain = [];
		if( query.FileName.Length > 0 )
			domain.Add( new ArrayList { "name", query.NameComparer, query.FileName } );

		// remote filters don't apply to a local-only search
		if( query.LocalOnly )
			return domain;

		if( query.CheckedOutToMe )
			domain.Add( new ArrayList { "checkout_user", "=", OdooDefaults.Instance.OdooId } );
		if( query.DeletedRemotely )
			domain.Add( new ArrayList { "deleted", "=", true } );
		if( query.Properties.Count > 0 )
			domain.Add( new ArrayList { "id", "in", new ArrayList( ( await EntriesMatchingAllPropertiesAsync( query.Properties ) ).ToArray() ) } );
		return domain;
	}

	/// <summary>Ids of entries that have a version matching every property filter.</summary>
	private static async Task<HashSet<int>> EntriesMatchingAllPropertiesAsync( IReadOnlyList<SearchPropertiesRow> properties )
	{
		ArrayList fields = [ "entry_id" ];
		HashSet<int>? candidates = null;
		foreach( SearchPropertiesRow property in properties )
		{
			ArrayList filter =
			[
				new ArrayList { "prop_id", "=", property.ID },
				new ArrayList { "text_value", property.Comparer.OperatorConversion(), property.Value },
			];
			ArrayList matches = await OdooClient.BrowseAsync( HpVersionProperty.GetHpModel()!, [ filter, fields ], BrowseLimit ) ?? [];

			IEnumerable<int> entryIds = matches.OfType<Hashtable>()
				.Select( m => ( m[ "entry_id" ] as ArrayList )?[ 0 ] as int? ?? 0 )
				.Where( id => id != 0 );

			if( candidates is null )
				candidates = [ .. entryIds ];
			else
				candidates.IntersectWith( entryIds );
		}
		return candidates ?? [];
	}

	private static SearchRow ToRow( Hashtable entry ) => new()
	{
		Id = entry[ "id" ] as int?,
		Name = entry[ "name" ]?.ToString(),
		Directory = entry[ "directory_complete_name" ]?.ToString(),
	};

	/// <summary>
	/// Local files named like <paramref name="fileName"/>. With <paramref name="matchRemote"/> only files that
	/// appear in <paramref name="remoteResults"/> are kept; without it only files that don't.
	/// </summary>
	private static List<SearchRow> FindLocalFiles( ArrayList remoteResults, string fileName, int limit, bool matchRemote )
	{
		var defaults = HackDefaults.Instance;
		DirectoryInfo pwa = new( defaults.PwaPathAbsolute );
		if( !pwa.Exists )
			return [];

		Dictionary<string, List<string>> remotePathsByName = [];
		foreach( Hashtable entry in remoteResults.OfType<Hashtable>() )
		{
			if( entry[ "name" ] is not string name || entry[ "directory_complete_name" ] is not string path )
				continue;
			string key = name.ToLower();
			if( !remotePathsByName.TryGetValue( key, out var paths ) )
				remotePathsByName[ key ] = paths = [];
			paths.Add( path );
		}

		int prefixLength = defaults.PwaPathAbsolute.Length - defaults.PwaPathRelative.Length;
		List<SearchRow> rows = [];
		foreach( FileInfo file in pwa.EnumerateFiles( $"*{fileName}*", SearchOption.AllDirectories ) )
		{
			if( rows.Count >= limit )
				break;

			string odooPath = FileOperations.WindowsToOdooPath( file.DirectoryName![ prefixLength.. ] );
			bool isRemote = remotePathsByName.TryGetValue( file.Name.ToLower(), out var paths ) && paths.Contains( odooPath );
			if( isRemote == matchRemote )
				rows.Add( new SearchRow { Id = null, Name = file.Name, Directory = file.DirectoryName } );
		}
		return rows;
	}
}
