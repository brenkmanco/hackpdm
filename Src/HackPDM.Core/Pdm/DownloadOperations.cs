using System.Collections.Concurrent;

using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Dependency resolution, RAM-aware batching, checksum guards, and parallel version downloads.</summary>
public static class DownloadOperations
{
	private const int DependencyTimeoutMs = 1_000_000;
	private const int MetadataChunkSize = 250;
	private const int DefaultBatchCount = 5;
	private const int MinTimeoutMs = 90_000;
	private const int MaxTimeoutMs = 30 * 60_000;
	private const long OneGiB = 1024L * 1024 * 1024;
	private const long HalfGiB = 512L * 1024 * 1024;

	private static readonly string[] ContentFields =
	[
		nameof( HpVersion.file_contents ),
		nameof( HpVersion.dir_id ),
		nameof( HpVersion.name ),
		nameof( HpVersion.file_modify_stamp ),
		nameof( HpVersion.file_size ),
		nameof( HpVersion.checkout_user ),
		nameof( HpVersion.windows_complete_name ),
	];

	private static readonly string[] LargeFileFields =
	[
		.. ContentFields.Where( f => f != nameof( HpVersion.file_contents ) ),
		nameof( HpVersion.checksum ),
	];

	public static long MaxBatchBytes { get; set; } = 16L * 1024 * 1024;
	public static long MinThroughputBytesPerSecond { get; set; } = 256 * 1024;
	public static int MaxAttempts { get; set; } = 3;

	public static Task<Result<IReadOnlyList<int>>> GetAllEntriesAndDependenciesListAsync( IReadOnlyList<int> versionIds, CancellationToken token = default )
		=> OdooRpc.ExecuteAsync<IReadOnlyList<int>>( HpVersion.ModelName, "get_recursive_dependency_entries", [ versionIds ], DependencyTimeoutMs, token );

	public static Task<Result<IReadOnlyList<int>>> GetAllEntriesAndDependenciesByEntriesAsync( IReadOnlyList<int> entryIds, CancellationToken token = default )
		=> OdooRpc.ExecuteAsync<IReadOnlyList<int>>( HpVersion.ModelName, "get_recursive_dependency_entries_by_entries", [ entryIds ], DependencyTimeoutMs, token );

	public static Task<Result<IReadOnlyList<int>>> GetVersionListAsync( params int[] versionIds )
		=> OdooRpc.ExecuteAsync<IReadOnlyList<int>>( HpVersion.ModelName, "get_recursive_dependency_versions", [ versionIds ], DependencyTimeoutMs );

	public static async Task<HpVersion[]?> GetLatestVersionsAsync(
		IReadOnlyList<int> entryIDs,
		string[]? excludedFields = null,
		CancellationToken token = default,
		string[]? includedFields = null,
		Action<StatusMessage, string>? log = null )
	{
		excludedFields ??= HpVersion.UsualExcludedFields;
		if( entryIDs.Count <= MetadataChunkSize )
		{
			token.ThrowIfCancellationRequested();
			return await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
				entryIDs,
				nameof( HpEntry.latest_version_id ),
				includedFields is null ? excludedFields : null,
				includedFields: includedFields,
				token: token );
		}

		List<HpVersion> all = new( entryIDs.Count );
		for( int offset = 0; offset < entryIDs.Count; offset += MetadataChunkSize )
		{
			token.ThrowIfCancellationRequested();
			int[] chunk = OdooRpc.SliceChunk( entryIDs, offset, MetadataChunkSize );
			HpVersion[]? page = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
				chunk,
				nameof( HpEntry.latest_version_id ),
				includedFields is null ? excludedFields : null,
				includedFields: includedFields,
				token: token );
			if( page is { Length: > 0 } )
				all.AddRange( page );
			int fetched = Math.Min( offset + chunk.Length, entryIDs.Count );
			log?.Invoke( StatusMessage.PROCESSING, $"Retrieved version metadata for {fetched}/{entryIDs.Count} entries..." );
		}
		return all.Count > 0 ? [ .. all ] : null;
	}

	private static bool TryReadMemoryPressure( out double loadRatio, out long freeBytes )
	{
		GCMemoryInfo mem = GC.GetGCMemoryInfo();
		long total = mem.TotalAvailableMemoryBytes;
		if( total <= 0 )
		{
			loadRatio = 0;
			freeBytes = long.MaxValue;
			return false;
		}
		freeBytes = Math.Max( 0, total - mem.MemoryLoadBytes );
		loadRatio = (double)mem.MemoryLoadBytes / total;
		return true;
	}

	public static long EffectiveBatchBytes( long? requestedMaxBytes = null )
	{
		long cap = requestedMaxBytes ?? MaxBatchBytes;
		if( !TryReadMemoryPressure( out double loadRatio, out long free ) )
			return cap;

		long ramCap = ( loadRatio, free ) switch
		{
			( >= 0.88, _ ) or ( _, < HalfGiB ) => 2L * 1024 * 1024,
			( >= 0.80, _ ) or ( _, < OneGiB ) => 4L * 1024 * 1024,
			( >= 0.70, _ ) or ( _, < 2 * OneGiB ) => 8L * 1024 * 1024,
			_ => cap,
		};
		return Math.Min( cap, ramCap );
	}

	public static int EffectiveConcurrency( int requestedConcurrency )
	{
		int capped = Math.Max( 1, requestedConcurrency );
		if( !TryReadMemoryPressure( out double loadRatio, out long free ) )
			return capped;
		if( loadRatio >= 0.85 || free < HalfGiB )
			return 1;
		if( loadRatio >= 0.75 || free < OneGiB )
			return Math.Min( capped, 2 );
		return capped;
	}

	private static bool IsMemoryPressureHigh()
		=> TryReadMemoryPressure( out double loadRatio, out long free ) && ( loadRatio >= 0.86 || free < HalfGiB );

	public static List<List<HpVersion>> BatchBySize( IEnumerable<HpVersion>? versions, int maxCount, long? maxBytes = null )
	{
		long byteBudget = maxBytes ?? MaxBatchBytes;
		if( maxCount <= 0 )
			maxCount = DefaultBatchCount;

		List<List<HpVersion>> batches = [];
		List<HpVersion> current = [];
		long currentBytes = 0;

		foreach( HpVersion version in ( versions ?? [] ).OrderBy( v => v.file_size ?? 0 ) )
		{
			long size = Math.Max( 0, (long)( version.file_size ?? 0 ) );
			if( current.Count > 0 && ( current.Count >= maxCount || currentBytes + size > byteBudget ) )
			{
				batches.Add( current );
				current = [];
				currentBytes = 0;
			}
			current.Add( version );
			currentBytes += size;
		}
		if( current.Count > 0 )
			batches.Add( current );
		return batches;
	}

	public static int ContentsTimeoutMs( long fileBytes, int attempt = 1 )
	{
		double wireBytes = fileBytes * 4.0 / 3.0;
		double ms = ( 30_000 + wireBytes / MinThroughputBytesPerSecond * 1000 ) * Math.Max( 1, attempt );
		return (int)Math.Clamp( ms, MinTimeoutMs, MaxTimeoutMs );
	}

	public sealed class DownloadSummary
	{
		private int _downloaded;
		private int _skipped;

		public int Downloaded => _downloaded;
		public int Skipped => _skipped;
		public ConcurrentQueue<string> Failed { get; } = new();
		public ConcurrentQueue<string> KeptLocalChanges { get; } = new();

		internal void AddDownloaded() => Interlocked.Increment( ref _downloaded );
		internal void AddSkipped() => Interlocked.Increment( ref _skipped );
	}

	public static async Task<DownloadSummary> ProcessDownloadsAsync(
		IEnumerable<IEnumerable<HpVersion>> versionBatches,
		CancellationToken cToken,
		int maxConcurrency = 2,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? onProgress = null,
		Action<long>? onBytesDownloaded = null )
	{
		DownloadSummary summary = new();
		int effectiveConcurrency = EffectiveConcurrency( maxConcurrency );
		if( effectiveConcurrency < Math.Max( 1, maxConcurrency ) )
			log?.Invoke( StatusMessage.INFO, $"Memory pressure detected; throttling concurrent download batches from {maxConcurrency} to {effectiveConcurrency}." );

		using SemaphoreSlim highMemoryGate = new( 1, 1 );
		await Parallel.ForEachAsync(
			versionBatches ?? [],
			new ParallelOptions { MaxDegreeOfParallelism = effectiveConcurrency, CancellationToken = cToken },
			async ( batch, ct ) =>
			{
				ct.ThrowIfCancellationRequested();
				bool serialized = false;
				if( IsMemoryPressureHigh() )
				{
					await highMemoryGate.WaitAsync( ct );
					serialized = true;
				}
				try
				{
					await ProcessVersionBatchAsync( batch, ct, log, onProgress, summary, onBytesDownloaded );
				}
				finally
				{
					if( serialized )
						highMemoryGate.Release();
				}
			} );

		return summary;
	}

	public static async Task ProcessVersionBatchAsync(
		IEnumerable<HpVersion> batchVersions,
		CancellationToken cToken,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? onProgress = null,
		DownloadSummary? summary = null,
		Action<long>? onBytesDownloaded = null )
	{
		summary ??= new();
		List<HpVersion> toDownload = [];
		List<(HpVersion Version, string LocalChecksum)> changedLocalCandidates = [];

		foreach( HpVersion version in batchVersions )
		{
			cToken.ThrowIfCancellationRequested();
			if( string.IsNullOrEmpty( version.checksum ) || version.checksum == "False" )
			{
				log?.Invoke( StatusMessage.ERROR, $"Checksum not found for version: {version.name}" );
				summary.AddSkipped();
				onProgress?.Invoke( 0, 1 );
				continue;
			}

			string? localPath = ResolveLocalPath( version );
			string? localChecksum = localPath is not null ? Checksums.OfFile( localPath ) : null;

			if( localChecksum is { Length: > 0 } && string.Equals( localChecksum, version.checksum, StringComparison.OrdinalIgnoreCase ) )
			{
				log?.Invoke( StatusMessage.FOUND, $"Skipping version download: {version.name}" );
				summary.AddSkipped();
				onProgress?.Invoke( 0, 1 );
				continue;
			}

			if( localChecksum is not null )
			{
				changedLocalCandidates.Add( ( version, localChecksum ) );
				continue;
			}

			log?.Invoke( StatusMessage.PROCESSING, $"Downloading latest version: {DisplayPath( version )}" );
			toDownload.Add( version );
		}

		if( changedLocalCandidates.Count > 0 )
		{
			HashSet<string> knownChecksumsByPath = await FetchVaultChecksumsAsync( changedLocalCandidates, cToken, log );
			foreach( var (version, localChecksum) in changedLocalCandidates )
			{
				string key = $"{version.windows_complete_name}\0{localChecksum}";
				if( !knownChecksumsByPath.Contains( key ) )
				{
					string shown = DisplayPath( version );
					log?.Invoke(
						StatusMessage.WARNING,
						$"Kept local changes to {shown}: the local file is not a version from the vault, so it was not overwritten (commit it, or delete the local copy to get the vault's)" );
					summary.KeptLocalChanges.Enqueue( shown );
					summary.AddSkipped();
					onProgress?.Invoke( 0, 1 );
					continue;
				}

				log?.Invoke( StatusMessage.PROCESSING, $"Downloading latest version: {DisplayPath( version )}" );
				toDownload.Add( version );
			}
		}

		if( toDownload.Count > 0 )
			await DownloadAndWriteAsync( toDownload, cToken, log, summary, onProgress, onBytesDownloaded );
	}

	private const int VaultChecksumLookupChunkSize = 200;
	private const int VaultChecksumLookupTimeoutMs = 60_000;

	private static string DisplayPath( HpVersion version )
		=> Path.Combine( version.WinPathway ?? "", version.name ?? $"version {version.id}" );

	private static string? ResolveLocalPath( HpVersion version )
	{
		if( version.windows_complete_name is { Length: > 0 } complete )
			return PwaPaths.ToAbsolute( complete );
		if( version.name is { Length: > 0 } name && version.WinPathway is not null )
			return Path.Combine( PwaPaths.Vault, version.WinPathway, name );
		return null;
	}

	private static async Task<HashSet<string>> FetchVaultChecksumsAsync(
		List<(HpVersion Version, string LocalChecksum)> candidates,
		CancellationToken cToken,
		Action<StatusMessage, string>? log )
	{
		HashSet<string> known = new( StringComparer.OrdinalIgnoreCase );
		string[] paths = [ .. candidates.Select( c => c.Version.windows_complete_name ).OfType<string>().Distinct( StringComparer.OrdinalIgnoreCase ) ];
		if( paths.Length == 0 )
			return known;

		for( int offset = 0; offset < paths.Length; offset += VaultChecksumLookupChunkSize )
		{
			cToken.ThrowIfCancellationRequested();
			string[] chunk = OdooRpc.SliceChunk( paths, offset, VaultChecksumLookupChunkSize );
			Result<HpVersion[]> versions = await Repository<HpVersion>.SearchReadAsync(
				OdooDomain.Where( "entry_id.windows_complete_name", Operators.In, chunk ),
				FieldSelection.Only( nameof( HpVersion.checksum ), nameof( HpVersion.windows_complete_name ) ),
				timeoutMs: VaultChecksumLookupTimeoutMs,
				token: cToken );
			if( !versions.Ok || versions.Value is null )
			{
				log?.Invoke( StatusMessage.WARNING, $"Could not verify vault version history for {chunk.Length} local file(s): {versions.Error ?? "no data returned"}" );
				continue;
			}

			foreach( HpVersion v in versions.Value )
			{
				if( v.windows_complete_name is { Length: > 0 } path && v.checksum is { Length: > 0 } checksum )
					known.Add( $"{path}\0{checksum}" );
			}
		}
		return known;
	}

	private static async Task DownloadAndWriteAsync(
		List<HpVersion> versions,
		CancellationToken cToken,
		Action<StatusMessage, string>? log,
		DownloadSummary summary,
		Action<int, int>? onProgress,
		Action<long>? onBytesDownloaded )
	{
		Dictionary<int, HpVersion> pending = new( versions.Count );
		for( int i = 0; i < versions.Count; i++ )
		{
			HpVersion v = versions[ i ];
			if( v.id is int id and > 0 )
				pending.TryAdd( id, v );
		}
		Dictionary<int, string> lastError = [];

		List<HpVersion>? largeVersions = null;
		foreach( HpVersion v in pending.Values )
		{
			if( ContentTransfer.IsLarge( v.file_size ) )
				( largeVersions ??= [] ).Add( v );
		}
		if( largeVersions is not null )
		{
			foreach( HpVersion large in largeVersions )
			{
				cToken.ThrowIfCancellationRequested();
				pending.Remove( large.id!.Value );
				if( await DownloadLargeAsync( large, cToken, onBytesDownloaded ) is { } error )
					Fail( large, error );
				else
					summary.AddDownloaded();
				onProgress?.Invoke( 1, 0 );
			}
		}

		for( int attempt = 1; attempt <= MaxAttempts && pending.Count > 0; attempt++ )
		{
			cToken.ThrowIfCancellationRequested();
			if( attempt > 1 )
				log?.Invoke( StatusMessage.INFO, $"Retrying {pending.Count} file(s) individually (attempt {attempt} of {MaxAttempts})" );

			List<List<HpVersion>> groups = attempt == 1
				? [ [ .. pending.Values ] ]
				: [ .. pending.Values.Select( static v => new List<HpVersion> { v } ) ];

			foreach( List<HpVersion> group in groups )
			{
				cToken.ThrowIfCancellationRequested();
				long bytes = 0;
				for( int i = 0; i < group.Count; i++ )
					bytes += group[ i ].file_size ?? 0;
				int timeout = ContentsTimeoutMs( bytes, attempt );

				Result<HpVersion[]> read = await Repository<HpVersion>.ReadAsync( group.Ids(), FieldSelection.Only( ContentFields ), timeout, token: cToken );
				string rpcError = read.Error ?? "no data returned";
				Dictionary<int, HpVersion> byId = read.ValueOr( [] ).Where( v => v?.id is > 0 ).ToDictionary( v => v.id!.Value );

				foreach( HpVersion requested in group )
				{
					int id = requested.id!.Value;
					if( !byId.TryGetValue( id, out HpVersion? got )
						|| ( got.file_contents is not { Length: > 0 } && ( got.file_size ?? 0 ) > 0 ) )
					{
						lastError[ id ] = !read.Ok
							? $"{rpcError} ({ByteSize.Format( bytes )} request, {timeout / 1000}s timeout)"
							: "contents missing from the server response";
						continue;
					}

					long downloadedSize = got.file_contents?.Length ?? ( got.file_size ?? 0 );
					if( downloadedSize > 0 )
						onBytesDownloaded?.Invoke( downloadedSize );

					bool wroteOk = await LocalFileWriter.WriteAsync( got.ToHackFile() );
					got.file_contents = null;

					if( !wroteOk )
					{
						pending.Remove( id );
						Fail( requested, "could not write the file locally" );
						onProgress?.Invoke( 1, 0 );
						continue;
					}

					pending.Remove( id );
					summary.AddDownloaded();
					onProgress?.Invoke( 1, 0 );
				}
			}
		}

		foreach( HpVersion version in pending.Values )
		{
			Fail( version, lastError.GetValueOrDefault( version.id ?? 0, "unknown error" ) );
			onProgress?.Invoke( 1, 0 );
		}

		void Fail( HpVersion version, string reason )
		{
			string name = DisplayPath( version );
			summary.Failed.Enqueue( $"{name}: {reason}" );
			log?.Invoke( StatusMessage.ERROR, $"Failed to download {name} after {MaxAttempts} attempt(s): {reason}" );
		}
	}

	private static async Task<string?> DownloadLargeAsync( HpVersion requested, CancellationToken cToken, Action<long>? onBytesDownloaded )
	{
		Result<HpVersion[]> read = await Repository<HpVersion>.ReadAsync( [ requested.id!.Value ], FieldSelection.Only( LargeFileFields ), OdooRpc.LongTimeout, token: cToken );
		if( read.Value is not [ { } version, .. ] )
			return read.Error ?? "the version could not be read";
		Result<string> temp = await ContentTransfer.DownloadToTempFileAsync( version, cToken, onBytesDownloaded );
		if( !temp.Ok )
			return temp.Error;
		if( await LocalFileWriter.WriteFromFileAsync( version.ToHackFile( contentsInFile: true ), temp.Value! ) )
			return null;
		FileOps.TryDelete( temp.Value );
		return "could not write the file locally";
	}
}
