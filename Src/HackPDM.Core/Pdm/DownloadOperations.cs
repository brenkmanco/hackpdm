using System.Collections.Concurrent;
using System.Diagnostics;

using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;

namespace HackPDM.Core.Pdm;

/// <summary>Dependency resolution, RAM-aware batching, checksum guards, and parallel version downloads.</summary>
public static class DownloadOperations
{
	private const int DependencyTimeoutMs = 1_000_000;
	private const int MetadataChunkSize = 2000;
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
				token: token ).ConfigureAwait( false );
		}

		int chunkCount = ( entryIDs.Count + MetadataChunkSize - 1 ) / MetadataChunkSize;
		HpVersion[]?[] pages = new HpVersion[]?[ chunkCount ];
		int fetchedEntries = 0;
		int parallelism = Math.Clamp( OdooDefaults.Instance?.ConcurrencySize ?? OdooLookupCache.DefaultConcurrency, 2, 4 );

		await Parallel.ForEachAsync(
			Enumerable.Range( 0, chunkCount ),
			new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token },
			async ( chunkIndex, ct ) =>
			{
				int offset = chunkIndex * MetadataChunkSize;
				int[] chunk = OdooRpc.SliceChunk( entryIDs, offset, MetadataChunkSize );
				HpVersion[]? page = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
					chunk,
					nameof( HpEntry.latest_version_id ),
					includedFields is null ? excludedFields : null,
					includedFields: includedFields,
					token: ct ).ConfigureAwait( false );
				pages[ chunkIndex ] = page;
				int done = Interlocked.Add( ref fetchedEntries, chunk.Length );
				log?.Invoke( StatusMessage.PROCESSING, $"Retrieved version metadata for {done}/{entryIDs.Count} entries..." );
			} ).ConfigureAwait( false );

		int total = 0;
		for( int i = 0; i < pages.Length; i++ )
			total += pages[ i ]?.Length ?? 0;
		if( total == 0 )
			return null;

		HpVersion[] all = new HpVersion[ total ];
		int pos = 0;
		for( int i = 0; i < pages.Length; i++ )
		{
			if( pages[ i ] is { Length: > 0 } page )
			{
				Array.Copy( page, 0, all, pos, page.Length );
				pos += page.Length;
			}
		}
		return all;
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
			( _, < HalfGiB ) or ( >= 0.92, < 2 * OneGiB ) => 2L * 1024 * 1024,
			( _, < OneGiB ) or ( >= 0.88, < 3 * OneGiB ) => 4L * 1024 * 1024,
			( _, < 2 * OneGiB ) => 8L * 1024 * 1024,
			_ => cap,
		};
		return Math.Min( cap, ramCap );
	}

	public static int EffectiveConcurrency( int requestedConcurrency )
	{
		int capped = Math.Max( 1, requestedConcurrency );
		if( !TryReadMemoryPressure( out double loadRatio, out long free ) )
			return capped;
		if( free < HalfGiB || ( loadRatio >= 0.92 && free < 2 * OneGiB ) )
			return 1;
		if( free < OneGiB || ( loadRatio >= 0.88 && free < 3 * OneGiB ) )
			return Math.Min( capped, 2 );
		return capped;
	}

	private static bool IsMemoryPressureHigh()
		=> TryReadMemoryPressure( out double loadRatio, out long free )
			&& ( free < HalfGiB || ( loadRatio >= 0.92 && free < 2 * OneGiB ) );

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
		await RunBatchesAsync(
			versionBatches,
			cToken,
			maxConcurrency,
			log,
			( batch, ct ) => ProcessVersionBatchAsync( batch, ct, log, onProgress, summary, onBytesDownloaded ) ).ConfigureAwait( false );
		return summary;
	}

	public static Task DownloadBatchesAsync(
		IEnumerable<List<HpVersion>> versionBatches,
		CancellationToken cToken,
		DownloadSummary summary,
		int maxConcurrency = 2,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? onProgress = null,
		Action<long>? onBytesDownloaded = null )
		=> RunBatchesAsync(
			versionBatches,
			cToken,
			maxConcurrency,
			log,
			async ( batch, ct ) =>
			{
				foreach( HpVersion version in batch )
					log?.Invoke( StatusMessage.PROCESSING, $"Downloading latest version: {DisplayPath( version )}" );
				await DownloadAndWriteAsync( batch, ct, log, summary, onProgress, onBytesDownloaded ).ConfigureAwait( false );
			} );

	/// <summary>
	/// Downloads pre-filtered versions using interleaved dispatch (large files interleaved with medium
	/// and small inline batches) and an adaptive controller targeting ~2s per RPC for both batches and chunks.
	/// </summary>
	public static async Task DownloadAdaptiveAsync(
		IReadOnlyList<HpVersion> versions,
		CancellationToken cToken,
		DownloadSummary summary,
		int maxBatchCount = AdaptiveTransferController.MaxAdaptiveBatchCount,
		int maxConcurrency = 2,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? onProgress = null,
		Action<long>? onBytesDownloaded = null,
		AdaptiveTransferController? controller = null )
	{
		if( versions.Count == 0 )
			return;

		controller ??= new AdaptiveTransferController( maxBatchCount );
		InterleavedDownloadQueue queue = new( versions );

		int effectiveConcurrency = EffectiveConcurrency( maxConcurrency );
		if( effectiveConcurrency < Math.Max( 1, maxConcurrency ) )
			log?.Invoke( StatusMessage.INFO, $"Memory pressure detected; throttling concurrent downloads from {maxConcurrency} to {effectiveConcurrency}." );

		using SemaphoreSlim highMemoryGate = new( 1, 1 );
		int workerCount = Math.Min( effectiveConcurrency, versions.Count );
		Task[] workers = new Task[ workerCount ];
		for( int i = 0; i < workerCount; i++ )
		{
			workers[ i ] = Task.Run( async () =>
			{
				while( queue.TryDequeue( controller, out List<HpVersion> batch, out _ ) )
				{
					cToken.ThrowIfCancellationRequested();
					bool serialized = false;
					if( IsMemoryPressureHigh() )
					{
						await highMemoryGate.WaitAsync( cToken ).ConfigureAwait( false );
						serialized = true;
					}
					try
					{
						for( int j = 0; j < batch.Count; j++ )
							log?.Invoke( StatusMessage.PROCESSING, $"Downloading latest version: {DisplayPath( batch[ j ] )}" );
						await DownloadAndWriteAsync( batch, cToken, log, summary, onProgress, onBytesDownloaded, controller ).ConfigureAwait( false );
					}
					finally
					{
						if( serialized )
							highMemoryGate.Release();
					}
				}
			}, cToken );
		}

		await Task.WhenAll( workers ).ConfigureAwait( false );
	}

	private static async Task RunBatchesAsync<TBatch>(
		IEnumerable<TBatch>? versionBatches,
		CancellationToken cToken,
		int maxConcurrency,
		Action<StatusMessage, string>? log,
		Func<TBatch, CancellationToken, Task> processBatch )
	{
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
					await highMemoryGate.WaitAsync( ct ).ConfigureAwait( false );
					serialized = true;
				}
				try
				{
					await processBatch( batch, ct ).ConfigureAwait( false );
				}
				finally
				{
					if( serialized )
						highMemoryGate.Release();
				}
			} ).ConfigureAwait( false );
	}

	public static async Task<List<HpVersion>> FilterVersionsToDownloadAsync(
		IReadOnlyList<HpVersion> versions,
		CancellationToken cToken,
		DownloadSummary summary,
		Action<StatusMessage, string>? log = null,
		Action<int, int>? onProgress = null )
	{
		if( versions.Count == 0 )
			return [];

		ConcurrentBag<HpVersion> toDownload = [];
		ConcurrentBag<(HpVersion Version, string LocalChecksum)> changedLocalCandidates = [];

		int diskParallelism = Math.Clamp( Environment.ProcessorCount, 2, 8 );
		Parallel.ForEach(
			versions,
			new ParallelOptions { MaxDegreeOfParallelism = diskParallelism, CancellationToken = cToken },
			version =>
			{
				if( string.IsNullOrEmpty( version.checksum ) || version.checksum == "False" )
				{
					log?.Invoke( StatusMessage.ERROR, $"Checksum not found for version: {version.name}" );
					summary.AddSkipped();
					onProgress?.Invoke( 0, 1 );
					return;
				}

				string? localPath = ResolveLocalPath( version );
				string? localChecksum = localPath is not null ? Checksums.OfFile( localPath ) : null;

				if( localChecksum is { Length: > 0 } && string.Equals( localChecksum, version.checksum, StringComparison.OrdinalIgnoreCase ) )
				{
					log?.Invoke( StatusMessage.FOUND, $"Skipping version download: {version.name}" );
					summary.AddSkipped();
					onProgress?.Invoke( 0, 1 );
					return;
				}

				if( localChecksum is not null )
				{
					changedLocalCandidates.Add( ( version, localChecksum ) );
					return;
				}

				toDownload.Add( version );
			} );

		List<HpVersion> result = [ .. toDownload ];
		if( !changedLocalCandidates.IsEmpty )
		{
			List<(HpVersion Version, string LocalChecksum)> candidateList = [ .. changedLocalCandidates ];
			HashSet<string> knownChecksumsByPath = await FetchVaultChecksumsAsync( candidateList, cToken, log ).ConfigureAwait( false );
			foreach( var (version, localChecksum) in candidateList )
			{
				cToken.ThrowIfCancellationRequested();
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

				result.Add( version );
			}
		}

		return result;
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
			HashSet<string> knownChecksumsByPath = await FetchVaultChecksumsAsync( changedLocalCandidates, cToken, log ).ConfigureAwait( false );
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
			await DownloadAndWriteAsync( toDownload, cToken, log, summary, onProgress, onBytesDownloaded ).ConfigureAwait( false );
	}

	private const int VaultChecksumLookupChunkSize = 500;
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
				token: cToken ).ConfigureAwait( false );
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
		Action<long>? onBytesDownloaded,
		AdaptiveTransferController? controller = null )
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
				if( await DownloadLargeAsync( large, cToken, onBytesDownloaded, controller ).ConfigureAwait( false ) is { } error )
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

				long rpcStart = Stopwatch.GetTimestamp();
				Result<HpVersion[]> read = await Repository<HpVersion>.ReadAsync(
					group.Ids(),
					FieldSelection.Only( ContentFields ),
					timeout,
					token: cToken ).ConfigureAwait( false );
				TimeSpan rpcElapsed = Stopwatch.GetElapsedTime( rpcStart );
				string rpcError = read.Error ?? "no data returned";
				HpVersion[] readRecords = read.ValueOr( [] );
				Dictionary<int, HpVersion> byId = new( readRecords.Length );
				long receivedBatchBytes = 0;
				int receivedBatchFiles = 0;
				for( int i = 0; i < readRecords.Length; i++ )
				{
					HpVersion rec = readRecords[ i ];
					if( rec?.id is int recId and > 0 )
					{
						byId[ recId ] = rec;
						receivedBatchFiles++;
						receivedBatchBytes += rec.file_contents?.Length ?? Math.Max( 0, rec.file_size ?? 0 );
					}
				}
				if( read.Ok && receivedBatchFiles > 0 )
					controller?.RecordRpcSample( receivedBatchBytes, receivedBatchFiles, rpcElapsed );

				foreach( HpVersion requested in group )
				{
					int id = requested.id!.Value;
					if( !byId.Remove( id, out HpVersion? got )
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

					bool wroteOk = await LocalFileWriter.WriteAsync( got.ToHackFile() ).ConfigureAwait( false );
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

	private static async Task<string?> DownloadLargeAsync(
		HpVersion requested,
		CancellationToken cToken,
		Action<long>? onBytesDownloaded,
		AdaptiveTransferController? controller = null )
	{
		Result<HpVersion[]> read = await Repository<HpVersion>.ReadAsync(
			[ requested.id!.Value ],
			FieldSelection.Only( LargeFileFields ),
			OdooRpc.LongTimeout,
			token: cToken ).ConfigureAwait( false );
		if( read.Value is not [ { } version, .. ] )
			return read.Error ?? "the version could not be read";
		Result<string> temp = await ContentTransfer.DownloadToTempFileAsync( version, cToken, onBytesDownloaded, controller ).ConfigureAwait( false );
		if( !temp.Ok )
			return temp.Error;
		if( await LocalFileWriter.WriteFromFileAsync( version.ToHackFile( contentsInFile: true ), temp.Value! ).ConfigureAwait( false ) )
			return null;
		FileOps.TryDelete( temp.Value );
		return "could not write the file locally";
	}
}
