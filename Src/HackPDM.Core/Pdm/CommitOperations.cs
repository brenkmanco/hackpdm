using System.Text.RegularExpressions;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Services;
using HackPDM.Core.SolidWorks;
using HackPDM.Core.SolidWorks.Format;

namespace HackPDM.Core.Pdm;

/// <summary>Filters commit candidates, ensures server directories exist, and stages records for commit.</summary>
public static class CommitOperations
{
	private const int LookupChunkSize = 50;
	private const int VersionChunkSize = 200;
	private static readonly string[] EntryLookupFields =
	[
		nameof( HpEntry.id ),
		nameof( HpEntry.name ),
		nameof( HpEntry.dir_id ),
		nameof( HpEntry.deleted ),
		nameof( HpEntry.checkout_user ),
		nameof( HpEntry.latest_version_id ),
		nameof( HpEntry.windows_complete_name ),
	];
	private static readonly string[] ChecksumOnlyFields = [ nameof( HpVersion.checksum ), nameof( HpVersion.file_size ) ];

	private sealed record CachedFilterRegex( string[] Patterns, Regex? Regex );
	private static CachedFilterRegex? _filterRegexCache;

	public static async Task<HpDirectory?> CreateDirectoriesAsync( HackFile? hack, CancellationToken token = default )
	{
		if( hack is null )
			return null;
		return ( await HpDirectory.CreateNew( DirectorySegments( hack ), token ).ConfigureAwait( false ) )?.LastOrDefault()
			?? throw new InvalidOperationException( $"{HpDirectory.ModelName} didn't create any records" );
	}

	internal static string EscapeLikeLiteral( string value )
		=> value
			.Replace( "\\", "\\\\", StringComparison.Ordinal )
			.Replace( "%", "\\%", StringComparison.Ordinal )
			.Replace( "_", "\\_", StringComparison.Ordinal );

	internal static string[] DirectorySegments( HackFile hack )
		=> PwaPaths.LocalSegments( hack.FullPath is { Length: > 0 } full ? Path.GetDirectoryName( full ) : hack.DirectoryName ) ?? [];

	internal static string ServerWindowsName( HackFile hack )
		=> PwaPaths.ToServerName( [ .. DirectorySegments( hack ), hack.Name ?? "" ] );

	internal static async Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut, Dictionary<string, HpEntry> RemoteByServerName)> SegmentCommitFilesAsync(
		IEnumerable<HackFile> hackFiles,
		Action<StatusMessage, string>? log = null,
		CancellationToken token = default,
		OperationPauseController? pause = null )
	{
		List<(HackFile Hack, string FilePath, string ServerName)> allowed = [];
		OdooDefaults defaults = OdooDefaults.Instance;
		bool restrictTypes = defaults.RestrictTypes is true;

		foreach( HackFile hack in hackFiles )
		{
			token.ThrowIfCancellationRequested();
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

		int parallelism = Math.Clamp( defaults.ConcurrencySize, 2, 4 );
		int entryChunkCount = ( distinctServerNames.Count + LookupChunkSize - 1 ) / LookupChunkSize;
		HpEntry[]?[] entryPages = new HpEntry[]?[ entryChunkCount ];

		async Task FetchEntryChunkAsync( int chunkIndex, CancellationToken ct )
		{
			if( pause is not null )
				await pause.WaitIfPausedAsync( ct ).ConfigureAwait( false );
			using OperationPauseController.WorkScope _ = OperationPauseController.EnterScope( pause );
			int offset = chunkIndex * LookupChunkSize;
			string[] chunk = OdooRpc.SliceChunk( distinctServerNames, offset, LookupChunkSize );
			OdooDomain filter = OdooDomain.Where( nameof( HpEntry.windows_complete_name ), Operators.ILikeEqual, EscapeLikeLiteral( chunk[ 0 ] ) );
			for( int i = 1; i < chunk.Length; i++ )
				filter |= OdooDomain.Where( nameof( HpEntry.windows_complete_name ), Operators.ILikeEqual, EscapeLikeLiteral( chunk[ i ] ) );

			Result<HpEntry[]> batchResult = await Repository<HpEntry>.SearchReadAsync(
				filter,
				FieldSelection.Only( EntryLookupFields ),
				token: ct ).ConfigureAwait( false );
			if( !batchResult.Ok )
				throw new OdooException( $"Could not look up existing remote entries: {batchResult.Error}" );
			entryPages[ chunkIndex ] = batchResult.Value;
		}

		if( entryChunkCount == 1 )
		{
			token.ThrowIfCancellationRequested();
			await FetchEntryChunkAsync( 0, token ).ConfigureAwait( false );
		}
		else
		{
			await Parallel.ForEachAsync(
				Enumerable.Range( 0, entryChunkCount ),
				new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token },
				async ( idx, ct ) => await FetchEntryChunkAsync( idx, ct ).ConfigureAwait( false ) ).ConfigureAwait( false );
		}

		for( int p = 0; p < entryPages.Length; p++ )
		{
			if( entryPages[ p ] is not { Length: > 0 } batch )
				continue;
			foreach( HpEntry found in batch )
			{
				if( found.id is not null and not 0 && found.windows_complete_name is { Length: > 0 } wcn )
				{
					if( !entryByServerName.TryGetValue( wcn, out HpEntry? existing )
						|| ( existing.deleted is true && found.deleted is not true ) )
					{
						entryByServerName[ wcn ] = found;
					}
				}
			}
		}

		// Batch-query latest version checksums for all matched remote entries
		Dictionary<int, (string? Checksum, int? FileSize)> versionInfoById = [];
		if( entryByServerName.Count > 0 )
		{
			HashSet<int> versionIds = [];
			foreach( HpEntry found in entryByServerName.Values )
			{
				if( found.deleted is not true && found.latest_version_id?.id is int vid and not 0 )
					versionIds.Add( vid );
			}
			if( versionIds.Count > 0 )
			{
				int[] allVids = [ .. versionIds ];
				int versionChunkCount = ( allVids.Length + VersionChunkSize - 1 ) / VersionChunkSize;
				HpVersion[]?[] versionPages = new HpVersion[]?[ versionChunkCount ];

				async Task FetchVersionChunkAsync( int chunkIndex, CancellationToken ct )
				{
					if( pause is not null )
						await pause.WaitIfPausedAsync( ct ).ConfigureAwait( false );
					using OperationPauseController.WorkScope _ = OperationPauseController.EnterScope( pause );
					int offset = chunkIndex * VersionChunkSize;
					int[] chunk = OdooRpc.SliceChunk( allVids, offset, VersionChunkSize );
					Result<HpVersion[]> versionsResult = await Repository<HpVersion>.ReadAsync(
						chunk,
						FieldSelection.Only( ChecksumOnlyFields ),
						token: ct ).ConfigureAwait( false );
					if( !versionsResult.Ok )
						throw new OdooException( $"Could not look up latest version checksums: {versionsResult.Error}" );
					versionPages[ chunkIndex ] = versionsResult.Value;
				}

				if( versionChunkCount == 1 )
				{
					token.ThrowIfCancellationRequested();
					await FetchVersionChunkAsync( 0, token ).ConfigureAwait( false );
				}
				else
				{
					await Parallel.ForEachAsync(
						Enumerable.Range( 0, versionChunkCount ),
						new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token },
						async ( idx, ct ) => await FetchVersionChunkAsync( idx, ct ).ConfigureAwait( false ) ).ConfigureAwait( false );
				}

				for( int p = 0; p < versionPages.Length; p++ )
				{
					foreach( HpVersion v in versionPages[ p ] ?? [] )
					{
						if( v.id is int vid )
							versionInfoById[ vid ] = ( v.checksum, v.file_size );
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
				if( entry.deleted is true )
				{
					log?.Invoke( StatusMessage.ERROR, $"entry '{filePath}' already exists on the server as deleted (Dt); restore or permanently delete it before committing" );
					continue;
				}
				bool mine = entry.checkout_user?.id == myId;
				string? latest = null;
				if( entry.latest_version_id?.id is int vid and not 0 && versionInfoById.TryGetValue( vid, out var info ) )
				{
					latest = !string.IsNullOrEmpty( info.Checksum )
						? info.Checksum
						: ( info.FileSize == 0 ? Checksums.EmptySha1 : null );
				}
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

	public static Task<bool> CommitRecordAsync(
		HackFile? hack,
		HpPDMCommit commit,
		bool inOdoo = false,
		Action<StatusMessage, string>? log = null,
		Action? onRecordStaged = null,
		Action<long>? onBytesUploaded = null,
		Dictionary<string, HpDirectory>? directoryCache = null,
		IReadOnlyDictionary<string, HpEntry>? remoteEntryCache = null,
		CancellationToken token = default,
		OperationPauseController? pause = null )
		=> CommitRecordInternalAsync(
			hack, commit, inOdoo, log, onRecordStaged, onBytesUploaded,
			directoryCache, remoteEntryCache, clearOnFailure: true, token, pause );

	internal static async Task<bool> CommitRecordInternalAsync(
		HackFile? hack,
		HpPDMCommit commit,
		bool inOdoo,
		Action<StatusMessage, string>? log,
		Action? onRecordStaged,
		Action<long>? onBytesUploaded,
		Dictionary<string, HpDirectory>? directoryCache,
		IReadOnlyDictionary<string, HpEntry>? remoteEntryCache,
		bool clearOnFailure,
		CancellationToken token,
		OperationPauseController? pause = null )
	{
		if( hack is null )
			return false;
		if( hack.FullPath is null || !File.Exists( hack.FullPath ) )
		{
			log?.Invoke( StatusMessage.ERROR, $"{hack.Name}: the file is no longer there ({hack.FullPath}); nothing was committed" );
			if( clearOnFailure )
				await commit.ServerClear().ConfigureAwait( false );
			return false;
		}

		if( pause is not null )
			await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );

		// Extract preview, references, and properties up front so SwStreamStore (full file byte[]) can be GC'd before upload RPCs.
		ReadSwCommitMetadata(
			hack,
			out byte[]? preReadPreview,
			out bool hasPreReadPreview,
			out List<string[]>? preReadDeps,
			out List<SwFileReader.SwProperty>? preReadProperties,
			out string? swMetaError );

		try
		{
			token.ThrowIfCancellationRequested();

			HpEntry? entry;
			HpRecordStaged? entryStaged = null;
			using( OperationPauseController.EnterScope( pause ) )
			{
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
						HpDirectory? dir = await ResolveDirectoryCachedAsync( hack, directoryCache, token ).ConfigureAwait( false );
						HpEntry probe = CommitStaging.NewEntry( hack, dir?.id ?? 0, commit ) ?? new();
						entry = await CommitStaging.FindRemoteEntryAsync( probe, token ).ConfigureAwait( false );
					}
					if( entry is null )
						throw new InvalidOperationException( $"Could not find existing remote entry for {hack.Name}; aborting commit to prevent duplicate records." );
				}
				else
				{
					HpDirectory? dir = await ResolveDirectoryCachedAsync( hack, directoryCache, token ).ConfigureAwait( false );
					entry = CommitStaging.NewEntry( hack, dir?.id ?? 0, commit )
						?? throw new InvalidOperationException( $"Could not create entry metadata for {hack.Name} (restricted file type)." );
				}

				if( entry.returnType is EntryReturnType.Staged )
					entryStaged = await CommitStaging.StageEntryAsync( entry, token ).ConfigureAwait( false );
			}

			HpRecordStaged? versionStaged = null;
			string? stageError = null;
			log?.Invoke( StatusMessage.PROCESSING, $"Staging {hack.Name}..." );
			if( entry.returnType is EntryReturnType.GotExisting )
			{
				versionStaged = await CommitStaging.StageVersionAsync(
					hack, entry, commit.id ?? 0, e => stageError = e, onBytesUploaded,
					hasPreReadPreview ? preReadPreview : null, hasPreReadPreview, token, pause ).ConfigureAwait( false );
			}
			else if( entry.returnType is EntryReturnType.Staged && entryStaged is not null )
			{
				versionStaged = await CommitStaging.StageVersionAsync(
					hack, entryStaged, e => stageError = e, onBytesUploaded,
					hasPreReadPreview ? preReadPreview : null, hasPreReadPreview, token, pause ).ConfigureAwait( false );
			}
			preReadPreview = null;
			if( entry.returnType is EntryReturnType.Failed )
			{
				if( clearOnFailure )
					await commit.ServerClear().ConfigureAwait( false );
				log?.Invoke( StatusMessage.ERROR, $"Error creating entry {entry.name}. Rolling back records..." );
				return false;
			}

			if( versionStaged is not { id: not 0 } )
				throw new ArgumentException( $"Could not stage {hack.Name}: {stageError ?? "the version was not created"}" );

			if( swMetaError is not null )
			{
				log?.Invoke( StatusMessage.WARNING, $"{hack.Name}: references and properties can't be read ({swMetaError}); committing the file without them" );
				onRecordStaged?.Invoke();
				return true;
			}

			if( !TryResolveReferences( hack, preReadDeps, log, out List<string[]>? dependencies ) )
			{
				onRecordStaged?.Invoke();
				return true;
			}

			if( pause is not null )
				await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );

			using( OperationPauseController.EnterScope( pause ) )
			{
				if( !await CommitStaging.StageRelationshipsAsync( versionStaged, dependencies, token ).ConfigureAwait( false ) )
					throw new ArgumentException( $"Failed to stage version relationships for {versionStaged.payload?.GetValueOrDefault( nameof( HpVersion.name ) )}" );

				bool propsOk = preReadProperties is not null
					? await CommitStaging.StagePropertiesAsync( versionStaged, preReadProperties, token ).ConfigureAwait( false )
					: await CommitStaging.StagePropertiesAsync( versionStaged, preOpenedStore: null, token ).ConfigureAwait( false );
				if( !propsOk )
					throw new ArgumentException( $"Failed to stage version properties for {versionStaged.payload?.GetValueOrDefault( nameof( HpVersion.name ) )}" );
			}

			onRecordStaged?.Invoke();
		}
		catch( Exception e )
		{
			if( clearOnFailure )
				await commit.ServerClear().ConfigureAwait( false );
			if( e is OperationCanceledException )
				throw;
			HackLogger.Error( "PdmCommit", $"Staging failed for commit {commit.id}: {e.Message}", e );
			log?.Invoke( StatusMessage.ERROR, e.Message );
			return false;
		}
		return true;
	}

	private static void ReadSwCommitMetadata(
		HackFile hack,
		out byte[]? preReadPreview,
		out bool hasPreReadPreview,
		out List<string[]>? preReadDeps,
		out List<SwFileReader.SwProperty>? preReadProperties,
		out string? swMetaError )
	{
		preReadPreview = null;
		hasPreReadPreview = false;
		preReadDeps = null;
		preReadProperties = null;
		swMetaError = null;

		if( hack.FullPath is null || !SwDocumentTypes.IsSolidWorks( hack.FullPath ) || SolidWorksUtil.DocMgr is not SwFileReader )
			return;

		SwStreamStore? opened;
		try
		{
			opened = SwStreamStore.Open( hack.FullPath );
			if( opened.Format == SwStreamStore.ContainerFormat.Unknown )
				return;
		}
		catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException or InvalidDataException )
		{
			HackLogger.Debug( "PdmCommit", $"Could not pre-open SolidWorks store for '{hack.FullPath}': {ex.Message}", ex );
			return;
		}

		try
		{
			preReadPreview = SwFileReader.GetPreviewPng( opened );
			hasPreReadPreview = true;
			List<string> refs = SwFileReader.GetReferences( opened, Path.GetFileName( hack.FullPath ) );
			preReadDeps = new List<string[]>( refs.Count );
			for( int i = 0; i < refs.Count; i++ )
			{
				string path = refs[ i ];
				preReadDeps.Add( [ SwReferenceResolver.FileName( path ), path ] );
			}
			preReadProperties = SwFileReader.ReadProperties( opened );
		}
		catch( Exception ex )
		{
			preReadDeps = null;
			preReadProperties = null;
			swMetaError = ex.Message;
		}
	}

	internal static async Task PreResolveDirectoriesAsync(
		IEnumerable<HackFile> files,
		Dictionary<string, HpDirectory> directoryCache,
		CancellationToken token,
		OperationPauseController? pause = null )
	{
		List<(string Key, HackFile Representative)> missing = [];
		HashSet<string> seen = new( StringComparer.OrdinalIgnoreCase );
		foreach( HackFile hack in files )
		{
			token.ThrowIfCancellationRequested();
			if( hack.FullPath is null || !File.Exists( hack.FullPath ) )
				continue;
			string key = string.Join( PwaPaths.NameSeparator, DirectorySegments( hack ) );
			if( !seen.Add( key ) )
				continue;
			lock( directoryCache )
			{
				if( directoryCache.ContainsKey( key ) )
					continue;
			}
			missing.Add( ( key, hack ) );
		}

		for( int i = 0; i < missing.Count; i++ )
		{
			if( pause is not null )
				await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );
			token.ThrowIfCancellationRequested();
			using OperationPauseController.WorkScope _ = OperationPauseController.EnterScope( pause );
			await ResolveDirectoryCachedAsync( missing[ i ].Representative, directoryCache, token ).ConfigureAwait( false );
		}
	}

	private static ValueTask<HpDirectory?> ResolveDirectoryCachedAsync( HackFile hack, Dictionary<string, HpDirectory>? cache, CancellationToken token )
	{
		if( cache is null )
			return new ValueTask<HpDirectory?>( CreateDirectoriesAsync( hack, token ) );
		string key = string.Join( PwaPaths.NameSeparator, DirectorySegments( hack ) );
		lock( cache )
		{
			if( cache.TryGetValue( key, out HpDirectory? existing ) )
				return ValueTask.FromResult<HpDirectory?>( existing );
		}
		return ResolveDirectorySlowAsync( hack, key, cache, token );
	}

	private static async ValueTask<HpDirectory?> ResolveDirectorySlowAsync(
		HackFile hack,
		string key,
		Dictionary<string, HpDirectory> cache,
		CancellationToken token )
	{
		HpDirectory? created = await CreateDirectoriesAsync( hack, token ).ConfigureAwait( false );
		if( created is not null )
		{
			lock( cache )
				cache[ key ] = created;
		}
		return created;
	}

	private static bool TryResolveReferences(
		HackFile hack,
		List<string[]>? preReadDeps,
		Action<StatusMessage, string>? log,
		out List<string[]>? dependencies )
	{
		dependencies = preReadDeps;
		if( dependencies is not null || !SwDocumentTypes.IsSolidWorksExtension( hack.TypeExt ) || hack.FullPath is null )
			return true;
		try
		{
			dependencies = SolidWorksUtil.DocMgr?.GetDependencies( hack.FullPath );
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

	internal static Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut, Dictionary<string, HpEntry> RemoteByServerName)> FilterAndSegmentCommitFilesAsync(
		IEnumerable<HackFile> hackFiles,
		Action<StatusMessage, string>? log = null,
		CancellationToken token = default,
		OperationPauseController? pause = null )
		=> SegmentCommitFilesAsync( FilterByName( hackFiles ), log, token, pause );

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
