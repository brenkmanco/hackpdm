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
	private static readonly int HashConcurrency = Math.Clamp( Environment.ProcessorCount, 2, 8 );
	private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

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
			: Task.FromResult( new Dictionary<string, FileInfo>( PathComparer ) );

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
		HashSet<string> matchedLocalPaths = new( PathComparer );
		HackFile?[]? resolvedLocals = null;

		if( remote is { Count: > 0 } )
		{
			resolvedLocals = new HackFile?[ remote.Count ];
			List<(int Index, FileInfo Info)> toHash = [];
			string? remoteRelPath = BuildRelativeFolderPath( localFolder );

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
					resolvedLocals[ i ] = CreateHackFileFromInfo( localInfo, remoteRelPath, computeChecksum: false );
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
						resolvedLocals[ item.Index ] = CreateHackFileFromInfo( item.Info, remoteRelPath, computeChecksum: true );
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
			string? folderRelPath = BuildRelativeFolderPath( localFolder );
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
		Dictionary<string, FileInfo> files = new( PathComparer );
		if( string.IsNullOrEmpty( folderPath ) || !Directory.Exists( folderPath ) )
			return files;

		EnumerationOptions opts = new()
		{
			RecurseSubdirectories = false,
			IgnoreInaccessible = true,
			ReturnSpecialDirectories = false,
			AttributesToSkip = FileAttributes.None,
		};
		DirectoryInfo dir = new( Path.GetFullPath( folderPath ) );
		foreach( FileInfo file in dir.EnumerateFiles( "*", opts ) )
			files[ file.FullName ] = file;
		return files;
	}

	private static string? BuildRelativeFolderPath( string folderPath )
	{
		try { return Path.Combine( [ PwaPaths.Root, .. PwaPaths.LocalSegments( folderPath ) ?? [] ] ); }
		catch { return null; }
	}

	private static HackFile CreateHackFileFromInfo( FileInfo fileInfo, string? relativeFolder, bool computeChecksum )
	{
		HackFile hack = new( fileInfo, computeChecksum );
		if( relativeFolder is not null )
			hack.RelativePath = relativeFolder;
		return hack;
	}

	private static async Task<HackFile?> ReadLocalFileWithRetryAsync( string localPath )
	{
		int[] delaysMs = [ 200, 400, 800 ];
		for( int attempt = 0; ; attempt++ )
		{
			HackFile? local = await Task.Run( () => File.Exists( localPath ) && HackFile.GetFromPath( localPath ) is { Exists: true } hack ? hack : null );
			if( local is null || local.Checksum is not null || attempt >= delaysMs.Length || !File.Exists( localPath ) )
				return local;
			await Task.Delay( delaysMs[ attempt ] );
		}
	}

	private static EntryRow CreateRemoteRow( RemoteEntryDto entry, HackFile? local )
	{
		HpUser? checkout = entry.CheckoutUserId is int userId ? OdooDefaults.Instance.IdToUser.GetValueOrDefault( userId ) : null;
		FileStatus status = EntryStatusResolver.ResolveRemote(
			deleted: entry.Deleted,
			checkoutUserId: checkout?.id,
			myUserId: OdooDefaults.Instance.OdooId,
			latestChecksum: entry.LatestChecksum ?? (object)false,
			localChecksum: local?.Checksum,
			remoteDate: entry.LatestDate ?? default,
			localModified: local?.ModifiedDate );

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
			Category = entry.Category is string category
				? OdooDefaults.Instance.HpCategories?.FirstOrDefault( c => c.name?.Equals( category ) is true )
				: null,
			FullName = entry.FullName,
			LocalFile = local?.Exists is true ? local.Info : null,
			Remote = entry,
		};
	}

	private static EntryRow CreateLocalRow( HackFile file, bool filterUnknownTypes )
	{
		string extension = file.TypeExt?.ToLower() ?? "";
		OdooDefaults defaults = OdooDefaults.Instance;
		FileStatus status = EntryStatusResolver.ResolveLocal(
			restrictTypes: defaults.RestrictTypes is true,
			filtered: filterUnknownTypes,
			knownType: defaults.ExtToType.ContainsKey( extension ),
			ignored: defaults.ExtToFilter.ContainsKey( extension ) );

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
