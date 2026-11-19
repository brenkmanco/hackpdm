using System.Collections.Concurrent;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

public readonly record struct EntryLoadProgress( string Phase, int Completed = 0, int Total = 0 );

/// <summary>Combines Odoo entries with local files in a directory and resolves each file's sync status.</summary>
public interface IEntryListService
{
	Task<EntryListResult> LoadAsync( int? directoryId, string? treePath, bool includeDeleted, bool filterUnknownTypes, CancellationToken token );

	Task<EntryListResult> LoadAsync(
		int? directoryId,
		string? treePath,
		bool includeDeleted,
		bool filterUnknownTypes,
		IProgress<EntryLoadProgress>? progress,
		CancellationToken token )
		=> LoadAsync( directoryId, treePath, includeDeleted, filterUnknownTypes, token );

	Task<EntryRow?> RefreshRowAsync( EntryRow? existing, string localPath, bool filterUnknownTypes );
}

public sealed class EntryListService : IEntryListService
{
	private const int MaxCachedChecksums = 10_000;
	private static readonly int HashConcurrency = Math.Clamp( Environment.ProcessorCount, 2, 8 );
	private static readonly int[] RetryDelaysMs = [ 200, 400, 800 ];
	private static readonly EnumerationOptions LocalFilesOptions = new()
	{
		RecurseSubdirectories = false,
		IgnoreInaccessible = true,
		ReturnSpecialDirectories = false,
		AttributesToSkip = FileAttributes.None,
	};

	private readonly record struct CachedChecksum( long Length, long LastWriteUtcTicks, string Sha1 );
	private readonly ConcurrentDictionary<string, CachedChecksum> _checksumCache = new( PwaPaths.PathComparer );

	public Task<EntryListResult> LoadAsync( int? directoryId, string? treePath, bool includeDeleted, bool filterUnknownTypes, CancellationToken token )
		=> LoadAsync( directoryId, treePath, includeDeleted, filterUnknownTypes, progress: null, token );

	public async Task<EntryListResult> LoadAsync(
		int? directoryId,
		string? treePath,
		bool includeDeleted,
		bool filterUnknownTypes,
		IProgress<EntryLoadProgress>? progress,
		CancellationToken token )
	{
		string localFolder = PwaPaths.ToAbsolute( treePath ) ?? PwaPaths.Vault;
		Task<Dictionary<string, FileInfo>> localScanTask = treePath is not null
			? Task.Run( () => ScanLocalDirectory( localFolder ), token )
			: Task.FromResult( new Dictionary<string, FileInfo>( PwaPaths.PathComparer ) );

		IReadOnlyList<RemoteEntryDto>? remote = null;
		if( directoryId is int id and not 0 )
		{
			progress?.Report( new EntryLoadProgress( "Fetching entries from Odoo…" ) );
			Result<IReadOnlyList<RemoteEntryDto>> entries = await HpDirectory.GetEntriesAsync( id, includeDeleted );
			token.ThrowIfCancellationRequested();
			if( !entries.Ok )
				HackLogger.Error( "EntryList", $"Entries of directory {id} could not be read: {entries.Error}" );
			remote = entries.Value;
		}

		Dictionary<string, FileInfo> localFilesByPath = await localScanTask;
		token.ThrowIfCancellationRequested();

		List<EntryRow> rows = new( ( remote?.Count ?? 0 ) + localFilesByPath.Count );
		HashSet<string> matchedLocalPaths = new( PwaPaths.PathComparer );
		HackFile?[]? resolvedLocals = null;
		string? folderRelPath = remote is { Count: > 0 } || localFilesByPath.Count > 0
			? BuildRelativeFolderPath( localFolder )
			: null;

		if( remote is { Count: > 0 } )
		{
			resolvedLocals = new HackFile?[ remote.Count ];
			List<(int Index, FileInfo Info)> toHash = [];

			for( int i = 0; i < remote.Count; i++ )
			{
				RemoteEntryDto entry = remote[ i ];
				if( entry.FullName is not { } remotePath || PwaPaths.ToAbsolute( remotePath ) is not { } candidate )
					continue;

				string fullLocalPath = Path.GetFullPath( candidate );
				if( !localFilesByPath.TryGetValue( fullLocalPath, out FileInfo? localInfo ) )
					continue;

				matchedLocalPaths.Add( fullLocalPath );
				bool needsChecksum = !entry.Deleted && entry.CheckoutUserId is null or 0 && entry.LatestChecksum is string;
				if( needsChecksum )
					toHash.Add( ( i, localInfo ) );
				else
					resolvedLocals[ i ] = CreateHackFileFromInfo( localInfo, folderRelPath, computeChecksum: false );
			}

			if( toHash.Count > 0 )
			{
				int total = toHash.Count;
				int done = 0;
				progress?.Report( new EntryLoadProgress( $"Comparing {total} local file{( total == 1 ? "" : "s" )}…", 0, total ) );

				await Parallel.ForEachAsync(
					toHash,
					new ParallelOptions { MaxDegreeOfParallelism = HashConcurrency, CancellationToken = token },
					( item, ct ) =>
					{
						ct.ThrowIfCancellationRequested();
						resolvedLocals[ item.Index ] = CreateHackFileFromInfo( item.Info, folderRelPath, computeChecksum: true );
						int current = Interlocked.Increment( ref done );
						if( current == total || current % 16 == 0 )
							progress?.Report( new EntryLoadProgress( $"Comparing local files ({current} / {total})…", current, total ) );
						return ValueTask.CompletedTask;
					} );
			}

			progress?.Report( new EntryLoadProgress( "Preparing entry list…" ) );
			for( int i = 0; i < remote.Count; i++ )
				rows.Add( CreateRemoteRow( remote[ i ], resolvedLocals[ i ] ) );
		}

		token.ThrowIfCancellationRequested();
		int localOnlyCount = 0;
		if( localFilesByPath.Count > matchedLocalPaths.Count )
		{
			foreach( var (fullPath, fileInfo) in localFilesByPath )
			{
				if( matchedLocalPaths.Contains( fullPath ) )
					continue;
				HackFile localFile = CreateHackFileFromInfo( fileInfo, folderRelPath, computeChecksum: false );
				rows.Add( CreateLocalRow( localFile, filterUnknownTypes ) );
				localOnlyCount++;
			}
		}

		return new EntryListResult( rows, remote?.Count ?? 0, localOnlyCount, matchedLocalPaths.Count );
	}

	public async Task<EntryRow?> RefreshRowAsync( EntryRow? existing, string localPath, bool filterUnknownTypes )
	{
		HackFile? local = await ReadLocalFileWithRetryAsync( localPath );
		if( existing?.Remote is { } remote )
			return CreateRemoteRow( remote, local );
		return local is null ? null : CreateLocalRow( local, filterUnknownTypes );
	}

	private static Dictionary<string, FileInfo> ScanLocalDirectory( string folderPath )
	{
		Dictionary<string, FileInfo> files = new( PwaPaths.PathComparer );
		if( string.IsNullOrEmpty( folderPath ) || !Directory.Exists( folderPath ) )
			return files;

		DirectoryInfo dir = new( Path.GetFullPath( folderPath ) );
		foreach( FileInfo file in dir.EnumerateFiles( "*", LocalFilesOptions ) )
			files[ file.FullName ] = file;
		return files;
	}

	private static string? BuildRelativeFolderPath( string folderPath )
	{
		try { return Path.Combine( [ PwaPaths.Root, .. PwaPaths.LocalSegments( folderPath ) ?? [] ] ); }
		catch { return null; }
	}

	private HackFile CreateHackFileFromInfo( FileInfo fileInfo, string? relativeFolder, bool computeChecksum )
	{
		if( !computeChecksum || !fileInfo.Exists )
		{
			HackFile unhashed = new( fileInfo, computeChecksum: false );
			if( relativeFolder is not null )
				unhashed.RelativePath = relativeFolder;
			return unhashed;
		}

		long length = fileInfo.Length;
		long ticks = fileInfo.LastWriteTimeUtc.Ticks;
		string path = fileInfo.FullName;
		if( _checksumCache.TryGetValue( path, out CachedChecksum cached )
			&& cached.Length == length
			&& cached.LastWriteUtcTicks == ticks )
		{
			HackFile reused = new( fileInfo, computeChecksum: false ) { Checksum = cached.Sha1 };
			if( relativeFolder is not null )
				reused.RelativePath = relativeFolder;
			return reused;
		}

		HackFile hack = new( fileInfo, computeChecksum: true );
		if( relativeFolder is not null )
			hack.RelativePath = relativeFolder;
		if( hack.Checksum is { } sha1 )
		{
			if( _checksumCache.Count >= MaxCachedChecksums )
				_checksumCache.Clear();
			_checksumCache[ path ] = new CachedChecksum( length, ticks, sha1 );
		}
		return hack;
	}

	private static async Task<HackFile?> ReadLocalFileWithRetryAsync( string localPath )
	{
		for( int attempt = 0; ; attempt++ )
		{
			HackFile? local = await Task.Run( () => File.Exists( localPath ) && HackFile.GetFromPath( localPath ) is { Exists: true } hack ? hack : null );
			if( local is null || local.Checksum is not null || attempt >= RetryDelaysMs.Length || !File.Exists( localPath ) )
				return local;
			await Task.Delay( RetryDelaysMs[ attempt ] );
		}
	}

	private static EntryRow CreateRemoteRow( RemoteEntryDto entry, HackFile? local )
	{
		OdooDefaults defaults = OdooDefaults.Instance;
		HpUser? checkout = entry.CheckoutUserId is int userId ? defaults.IdToUser.GetValueOrDefault( userId ) : null;
		FileStatus status = EntryStatusResolver.ResolveRemote(
			deleted: entry.Deleted,
			checkoutUserId: entry.CheckoutUserId,
			myUserId: defaults.OdooId,
			latestChecksum: entry.LatestChecksum ?? (object)false,
			localChecksum: local?.Checksum,
			remoteDate: entry.LatestDate ?? default,
			localModified: local?.ModifiedDate );

		HpCategory? resolvedCategory = null;
		if( entry.Category is string category )
		{
			if( !defaults.NameToCategory.TryGetValue( category, out resolvedCategory )
				&& defaults.NameToCategory.Count == 0
				&& defaults.HpCategories is { } cats )
			{
				for( int i = 0; i < cats.Length; i++ )
				{
					if( string.Equals( cats[ i ].name, category, StringComparison.Ordinal ) )
					{
						resolvedCategory = cats[ i ];
						break;
					}
				}
			}
		}

		return new EntryRow
		{
			Id = entry.Id,
			Name = entry.Name,
			Type = entry.Type,
			Size = entry.Size,
			Checkout = checkout,
			ReprType = local?.Exists is true ? EntryReprType.Both : EntryReprType.Remote,
			LatestReleaseId = entry.LatestReleaseId,
			LatestId = entry.LatestVersionId,
			RemoteDate = entry.LatestDate,
			LocalDate = local?.ModifiedDate.Year is null or 1 ? null : local?.ModifiedDate,
			Status = status,
			Category = resolvedCategory,
			FullName = entry.FullName,
			LocalFile = local?.Exists is true ? local.Info : null,
			Remote = entry,
		};
	}

	private static EntryRow CreateLocalRow( HackFile file, bool filterUnknownTypes )
	{
		string extension = file.TypeExt?.ToLowerInvariant() ?? "";
		OdooDefaults defaults = OdooDefaults.Instance;
		FileStatus status = EntryStatusResolver.ResolveLocal(
			restrictTypes: defaults.RestrictTypes is true,
			filtered: filterUnknownTypes,
			knownType: defaults.ExtToType.ContainsKey( extension ),
			ignored: CommitOperations.IsFilteredName( file.Name ) );

		return new EntryRow
		{
			Id = null,
			Name = file.Name,
			Type = extension.TrimStart( '.' ),
			Size = file.FileSize,
			LocalDate = file.ModifiedDate,
			RemoteDate = null,
			Status = status,
			Checkout = null,
			Category = defaults.ExtToCat.GetValueOrDefault( extension ),
			FullName = file.FullPath,
			LocalFile = file.Exists is true ? file.Info : null,
			ReprType = EntryReprType.Local,
		};
	}
}
