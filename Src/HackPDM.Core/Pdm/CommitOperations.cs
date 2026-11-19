using System.Text.RegularExpressions;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.SolidWorks;
using HackPDM.Core.SolidWorks.Format;

namespace HackPDM.Core.Pdm;

/// <summary>Filters commit candidates, ensures server directories exist, and stages records for commit.</summary>
public static class CommitOperations
{
	private const int LookupChunkSize = 200;
	private static readonly string[] EntryLookupFields =
	[
		nameof( HpEntry.id ),
		nameof( HpEntry.name ),
		nameof( HpEntry.dir_id ),
		nameof( HpEntry.checkout_user ),
		nameof( HpEntry.latest_version_id ),
		nameof( HpEntry.windows_complete_name ),
	];
	private static readonly string[] ChecksumOnlyFields = [ nameof( HpVersion.checksum ) ];

	private sealed record CachedFilterRegex( string[] Patterns, Regex? Regex );
	private static CachedFilterRegex? _filterRegexCache;

	public static async Task<HpDirectory?> CreateDirectoriesAsync( HackFile? hack )
	{
		if( hack is null )
			return null;
		return ( await HpDirectory.CreateNew( DirectorySegments( hack ) ) )?.LastOrDefault()
			?? throw new InvalidOperationException( $"{HpDirectory.ModelName} didn't create any records" );
	}

	internal static string[] DirectorySegments( HackFile hack )
		=> PwaPaths.LocalSegments( hack.FullPath is { Length: > 0 } full ? Path.GetDirectoryName( full ) : hack.DirectoryName ) ?? [];

	internal static string ServerWindowsName( HackFile hack )
		=> PwaPaths.ToServerName( [ .. DirectorySegments( hack ), hack.Name ?? "" ] );

	public static async Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut)> FilesNotInOdooSegmentedAsync(
		IEnumerable<HackFile> hackFiles,
		Action<StatusMessage, string>? log = null )
	{
		var (localOnly, remoteExisting, unchanged, _) = await SegmentCommitFilesAsync( hackFiles, log );
		return ( localOnly, remoteExisting, unchanged );
	}

	internal static async Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut, Dictionary<string, HpEntry> RemoteByServerName)> SegmentCommitFilesAsync(
		IEnumerable<HackFile> hackFiles,
		Action<StatusMessage, string>? log = null,
		CancellationToken token = default )
	{
		List<(HackFile Hack, string FilePath, string ServerName)> allowed = [];
		OdooDefaults defaults = OdooDefaults.Instance;
		bool restrictTypes = defaults.RestrictTypes is true;

		foreach( HackFile hack in hackFiles )
		{
			if( restrictTypes && ( hack.TypeExt is not { Length: > 0 } ext || !defaults.ExtToType.ContainsKey( ext ) ) )
			{
				log?.Invoke( StatusMessage.SKIP, $"not a file type the vault accepts, not committed: {hack.Name}" );
				continue;
			}
			string filePath = PwaPaths.ToServerDirectory( hack.DirectoryName ) ?? hack.DirectoryName ?? "";
			allowed.Add( ( hack, filePath, ServerWindowsName( hack ) ) );
		}

		if( allowed.Count == 0 )
			return ( [], [], [], new( StringComparer.OrdinalIgnoreCase ) );

		// Batch-query all remote entries by windows_complete_name instead of 1 RPC per file
		Dictionary<string, HpEntry> entryByServerName = new( StringComparer.OrdinalIgnoreCase );
		List<string> distinctServerNames = new( allowed.Count );
		HashSet<string> seenNames = new( StringComparer.OrdinalIgnoreCase );
		for( int i = 0; i < allowed.Count; i++ )
		{
			if( seenNames.Add( allowed[ i ].ServerName ) )
				distinctServerNames.Add( allowed[ i ].ServerName );
		}

		for( int offset = 0; offset < distinctServerNames.Count; offset += LookupChunkSize )
		{
			token.ThrowIfCancellationRequested();
			string[] chunk = OdooRpc.SliceChunk( distinctServerNames, offset, LookupChunkSize );
			OdooDomain filter = OdooDomain.Where( nameof( HpEntry.windows_complete_name ), "in", chunk ).And( nameof( HpEntry.deleted ), false );
			HpEntry[]? batch = await HpEntry.GetRecordsBySearchAsync( searchFilter: filter, includedFields: EntryLookupFields );
			if( batch is not { Length: > 0 } )
				continue;
			foreach( HpEntry found in batch )
			{
				if( found.id is not null and not 0 && found.windows_complete_name is { Length: > 0 } wcn )
					entryByServerName.TryAdd( wcn, found );
			}
		}

		// Batch-query latest version checksums for all matched remote entries in 1 RPC per chunk
		Dictionary<int, string?> checksumByVersionId = [];
		if( entryByServerName.Count > 0 )
		{
			HashSet<int> versionIds = [];
			foreach( HpEntry found in entryByServerName.Values )
			{
				if( found.latest_version_id?.id is int vid and not 0 )
					versionIds.Add( vid );
			}
			if( versionIds.Count > 0 )
			{
				int[] allVids = [ .. versionIds ];
				for( int offset = 0; offset < allVids.Length; offset += LookupChunkSize )
				{
					token.ThrowIfCancellationRequested();
					int[] chunk = OdooRpc.SliceChunk( allVids, offset, LookupChunkSize );
					HpVersion[]? versions = await HpVersion.GetRecordsByIdsAsync( chunk, includedFields: ChecksumOnlyFields );
					foreach( HpVersion v in versions ?? [] )
					{
						if( v.id is int vid )
							checksumByVersionId[ vid ] = v.checksum;
					}
				}
			}
		}

		List<HackFile> localOnly = [];
		List<HackFile> remoteExisting = [];
		List<HpEntry> unchanged = [];
		int myId = defaults.OdooId;

		foreach( var (hack, filePath, serverName) in allowed )
		{
			if( entryByServerName.TryGetValue( serverName, out HpEntry? entry )
				&& string.Equals( entry.name, hack.Name, StringComparison.OrdinalIgnoreCase ) )
			{
				bool mine = entry.checkout_user?.id == myId;
				string? latest = entry.latest_version_id?.id is int vid and not 0
					? checksumByVersionId.GetValueOrDefault( vid )
					: null;
				if( latest is { Length: > 0 }
					&& string.Equals( latest, hack.GetChecksum(), StringComparison.OrdinalIgnoreCase ) )
				{
					log?.Invoke( StatusMessage.SKIP, mine
						? $"unchanged since the latest version, checked in without a new version: {filePath}"
						: $"unchanged since the latest version, nothing to commit: {filePath}" );
					if( mine )
						unchanged.Add( entry );
					continue;
				}
				if( mine )
				{
					log?.Invoke( StatusMessage.FOUND, $"entry found remotely for: {filePath}" );
					remoteExisting.Add( hack );
					continue;
				}
				log?.Invoke( StatusMessage.ERROR, $"entry found remotely but is not checked out to current user: {filePath}" );
				continue;
			}

			log?.Invoke( StatusMessage.INFO, $"Queued commit for {hack.Name} (Checksum: {hack.GetChecksum()}) for: {filePath}" );
			localOnly.Add( hack );
		}
		return ( [ .. localOnly ], [ .. remoteExisting ], [ .. unchanged ], entryByServerName );
	}

	public static async Task<bool> CommitRecordAsync(
		HackFile? hack,
		HpPDMCommit commit,
		bool inOdoo = false,
		Action<StatusMessage, string>? log = null,
		Action? onRecordStaged = null,
		Action<long>? onBytesUploaded = null,
		Dictionary<string, HpDirectory>? directoryCache = null,
		IReadOnlyDictionary<string, HpEntry>? remoteEntryCache = null,
		CancellationToken token = default )
	{
		if( hack is null )
			return false;
		if( hack.FullPath is null || !File.Exists( hack.FullPath ) )
		{
			log?.Invoke( StatusMessage.ERROR, $"{hack.Name}: the file is no longer there ({hack.FullPath}); nothing was committed" );
			await commit.ServerClear();
			return false;
		}

		// When using the managed reader, open the SolidWorks container once for preview + references + properties
		SwStreamStore? swStore = null;
		bool isSw = SwDocumentTypes.IsSolidWorks( hack.FullPath );
		byte[]? preReadPreview = null;
		bool hasPreReadPreview = false;
		if( isSw && SolidWorksUtil.DocMgr is SwFileReader )
		{
			try
			{
				SwStreamStore opened = SwStreamStore.Open( hack.FullPath );
				if( opened.Format != SwStreamStore.ContainerFormat.Unknown )
				{
					swStore = opened;
					preReadPreview = SwFileReader.GetPreviewPng( opened );
					hasPreReadPreview = true;
				}
			}
			catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or InvalidDataException )
			{
				HackLogger.Debug( "PdmCommit", $"Could not pre-open SolidWorks store for '{hack.FullPath}': {ex.Message}", ex );
			}
		}

		try
		{
			token.ThrowIfCancellationRequested();

			HpEntry? entry;
			if( inOdoo )
			{
				string serverName = ServerWindowsName( hack );
				if( remoteEntryCache is not null && remoteEntryCache.TryGetValue( serverName, out HpEntry? cached ) && cached.id is not null and not 0 )
				{
					cached.returnType = EntryReturnType.GotExisting;
					cached.commit_id = new Many2One { id = commit.id };
					cached.LocalFile = hack;
					entry = cached;
				}
				else
				{
					HpDirectory? dir = await ResolveDirectoryCachedAsync( hack, directoryCache );
					HpEntry probe = CommitStaging.NewEntry( hack, dir?.id ?? 0, commit ) ?? new();
					entry = await CommitStaging.FindRemoteEntryAsync( probe );
				}
				if( entry is null )
					throw new InvalidOperationException( $"Could not find existing remote entry for {hack.Name}; aborting commit to prevent duplicate records." );
			}
			else
			{
				HpDirectory? dir = await ResolveDirectoryCachedAsync( hack, directoryCache );
				entry = CommitStaging.NewEntry( hack, dir?.id ?? 0, commit )
					?? throw new InvalidOperationException( $"Could not create entry metadata for {hack.Name} (restricted file type)." );
			}

			HpRecordStaged? versionStaged = null;
			string? stageError = null;
			if( entry.returnType is EntryReturnType.GotExisting )
			{
				log?.Invoke( StatusMessage.FOUND, "existing entry found..." );
				log?.Invoke( StatusMessage.PROCESSING, "staging local version..." );
				versionStaged = await CommitStaging.StageVersionAsync(
					hack, entry, commit.id ?? 0, e => stageError = e, onBytesUploaded,
					hasPreReadPreview ? preReadPreview : null, hasPreReadPreview, token );
			}
			else if( entry.returnType is EntryReturnType.Staged )
			{
				HpRecordStaged? entryStaged = await CommitStaging.StageEntryAsync( entry );
				log?.Invoke( StatusMessage.PROCESSING, "staging local entry..." );
				log?.Invoke( StatusMessage.PROCESSING, "staging local version..." );
				versionStaged = entryStaged is null
					? null
					: await CommitStaging.StageVersionAsync(
						hack, entryStaged, e => stageError = e, onBytesUploaded,
						hasPreReadPreview ? preReadPreview : null, hasPreReadPreview, token );
			}
			if( entry.returnType is EntryReturnType.Failed )
			{
				await commit.ServerClear();
				log?.Invoke( StatusMessage.ERROR, $"Error creating entry {entry.name}. Rolling back records..." );
				return false;
			}

			if( versionStaged is not { id: not 0 } )
				throw new ArgumentException( $"Could not stage {hack.Name}: {stageError ?? "the version was not created"}" );

			if( !TryReadReferences( hack, swStore, log, out List<string[]>? preReadDeps ) )
			{
				onRecordStaged?.Invoke();
				return true;
			}

			log?.Invoke( StatusMessage.PROCESSING, "staging local version relationships..." );
			if( !await CommitStaging.StageRelationshipsAsync( versionStaged, preReadDeps ) )
				throw new ArgumentException( $"Failed to stage version relationships for {versionStaged.payload?.GetValueOrDefault( nameof( HpVersion.name ) )}" );

			log?.Invoke( StatusMessage.PROCESSING, "staging local version properties ..." );
			if( !await CommitStaging.StagePropertiesAsync( versionStaged, swStore ) )
				throw new ArgumentException( $"Failed to stage version properties for {versionStaged.payload?.GetValueOrDefault( nameof( HpVersion.name ) )}" );

			onRecordStaged?.Invoke();
		}
		catch( Exception e )
		{
			await commit.ServerClear();
			if( e is OperationCanceledException )
				throw;
			HackLogger.Error( "PdmCommit", $"Staging failed for commit {commit.id}: {e.Message}", e );
			log?.Invoke( StatusMessage.ERROR, e.Message );
			return false;
		}
		return true;
	}

	private static async Task<HpDirectory?> ResolveDirectoryCachedAsync( HackFile hack, Dictionary<string, HpDirectory>? cache )
	{
		if( cache is null )
			return await CreateDirectoriesAsync( hack );
		string key = string.Join( PwaPaths.NameSeparator, DirectorySegments( hack ) );
		if( cache.TryGetValue( key, out HpDirectory? existing ) )
			return existing;
		HpDirectory? created = await CreateDirectoriesAsync( hack );
		if( created is not null )
			cache[ key ] = created;
		return created;
	}

	private static bool TryReadReferences(
		HackFile hack,
		SwStreamStore? swStore,
		Action<StatusMessage, string>? log,
		out List<string[]>? dependencies )
	{
		dependencies = null;
		if( !SwDocumentTypes.IsSolidWorksExtension( hack.TypeExt ) || hack.FullPath is null )
			return true;
		try
		{
			if( swStore is not null )
			{
				List<string> refs = SwFileReader.GetReferences( swStore, Path.GetFileName( hack.FullPath ) );
				dependencies = new List<string[]>( refs.Count );
				for( int i = 0; i < refs.Count; i++ )
				{
					string path = refs[ i ];
					dependencies.Add( [ SwReferenceResolver.FileName( path ), path ] );
				}
			}
			else
			{
				dependencies = SolidWorksUtil.DocMgr?.GetDependencies( hack.FullPath );
			}
			return true;
		}
		catch( Exception e )
		{
			log?.Invoke( StatusMessage.WARNING, $"{hack.Name}: references and properties can't be read ({e.Message}); committing the file without them" );
			return false;
		}
	}

	private static Regex? EntryFilter()
	{
		OdooLookupCache? lookups = OdooDefaults.Instance?.Lookups;
		if( lookups is null )
			return null;
		if( lookups.EntryFilterRegex is { } compiled )
			return compiled;
		string[] patterns = lookups.EntryFilterPatterns;
		if( patterns.Length == 0 )
			return null;
		CachedFilterRegex? cached = _filterRegexCache;
		if( cached is not null && ReferenceEquals( cached.Patterns, patterns ) )
			return cached.Regex;
		Regex? built = OdooLookupCache.BuildFilterRegex( patterns );
		_filterRegexCache = new CachedFilterRegex( patterns, built );
		return built;
	}

	public static bool IsFilteredName( string? name )
		=> EntryFilter() is { } regex && regex.IsMatch( name ?? "" );

	public static Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut)> FilterCommitHackFilesAsync(
		IEnumerable<HackFile> hackFiles,
		Action<StatusMessage, string>? log = null )
		=> FilesNotInOdooSegmentedAsync( FilterByName( hackFiles ), log );

	internal static Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut, Dictionary<string, HpEntry> RemoteByServerName)> FilterAndSegmentCommitFilesAsync(
		IEnumerable<HackFile> hackFiles,
		Action<StatusMessage, string>? log = null,
		CancellationToken token = default )
		=> SegmentCommitFilesAsync( FilterByName( hackFiles ), log, token );

	private static List<HackFile> FilterByName( IEnumerable<HackFile> hackFiles )
	{
		Regex? regex = EntryFilter();
		List<HackFile> kept = [];
		foreach( HackFile h in hackFiles )
		{
			if( regex is null || !regex.IsMatch( h.Name ?? "" ) )
				kept.Add( h );
		}
		return kept;
	}
}
