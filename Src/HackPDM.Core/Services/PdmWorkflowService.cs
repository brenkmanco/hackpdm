using System.Collections.Concurrent;
using System.Text.Json;

using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;
using HackPDM.Core.SolidWorks;

namespace HackPDM.Core.Services;

public sealed record VersionDownloadPlan( HpVersion Version, bool IsLatest, IReadOnlyList<int> VersionIds );

/// <summary>Orchestrates core PDM workflows (get latest, checkout, commit, delete, restore, historical versions).</summary>
public interface IPdmWorkflowService
{
	event Action? ResumableDownloadChanged;
	bool HasActiveCommit { get; }
	Task<IReadOnlyList<int>> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false );
	Task GetLatestAsync( IReadOnlyList<int> entryIds );
	Task CheckoutAsync( IReadOnlyList<int> entryIds );
	Task CheckoutLatestAsync( IReadOnlyList<int> entryIds );
	Task UnCheckoutAsync( IReadOnlyList<int> entryIds );
	Task CommitAsync( IEnumerable<HackFile> hackFiles );
	Task<HashSet<HackFile>> CollectCommitFilesAsync( IEnumerable<EntryRow> entries, CancellationToken token = default );
	Task<List<HackFile>> CollectFolderCommitFilesAsync( string folderPath, CancellationToken token = default );
	Task LogicalDeleteAsync( IReadOnlyList<int> entryIds );
	Task RestoreDeletedInDirectoryAsync( int directoryId, bool withSubdirectories = false );
	Task<bool> IsCurrentUserPdmAdminAsync();
	Task<bool> PermanentDeleteAsync( IReadOnlyList<int> entryIds );
	Task<VersionDownloadPlan?> PlanVersionDownloadAsync( int versionId );
	Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds );
	DownloadResumeState? GetResumableDownload();
	Task<bool> ResumeDownloadAsync();
	void ClearResumableDownload();
	bool PrepareForShutdown();
	Task WaitActiveCommitCleanupAsync( TimeSpan timeout );
}

public sealed class PdmWorkflowService( IOperationReporter reporter, ILocalFileWatcher? localFiles = null, DownloadResumeStore? resumeStore = null ) : IPdmWorkflowService
{
	private const int RpcIdChunkSize = 10_000;
	private const int VersionReadChunkSize = 2000;
	private static readonly ConcurrentDictionary<string, byte> NoDirectDependencyServerUrls = new( StringComparer.OrdinalIgnoreCase );
	private static readonly string[] LatestVersionIdFields = [ nameof( HpEntry.latest_version_id ) ];
	private static readonly string[] HeavyEntryFields = [ nameof( HpEntry.type_id ), nameof( HpEntry.cat_id ) ];
	private static readonly string[] DeleteExcludedFields = [ nameof( HpEntry.type_id ), nameof( HpEntry.cat_id ), nameof( HpEntry.checkout_node ) ];
	private static readonly string[] LatestVersionIncludedFields =
	[
		nameof( HpVersion.id ),
		nameof( HpVersion.name ),
		nameof( HpVersion.checksum ),
		nameof( HpVersion.file_size ),
		nameof( HpVersion.windows_complete_name ),
	];

	private readonly DownloadResumeStore _resumeStore = resumeStore ?? DownloadResumeStore.Default;
	private readonly Lock _stateGate = new();
	private DownloadCheckpointTracker? _activeDownloadCheckpoint;
	private bool _shutdownPreservingDownload;
	private int _activeCommitCount;
	private Task? _activeCommitTask;

	public event Action? ResumableDownloadChanged;

	public bool HasActiveCommit => Volatile.Read( ref _activeCommitCount ) > 0;

	private void Log( StatusMessage status, string message ) => reporter.Log( status, message );

	private static int RpcParallelism => Math.Clamp( OdooDefaults.Instance?.ConcurrencySize ?? OdooLookupCache.DefaultConcurrency, 2, 4 );

	public async Task<IReadOnlyList<int>> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false )
	{
		Result<IReadOnlyList<int>> ids = await HpDirectory.GetDirectoryEntryIDsAsync( directoryId, withSubdirectories, withDeleted ).ConfigureAwait( false );
		if( !ids.Ok )
			Log( StatusMessage.ERROR, $"Could not list the directory's entries: {ids.Error}" );
		return ids.ValueOr( [] );
	}

	private static bool IsMissingMethodError( string? error )
		=> error is { Length: > 0 }
			&& ( error.Contains( "AttributeError", StringComparison.OrdinalIgnoreCase )
				|| error.Contains( "has no attribute", StringComparison.OrdinalIgnoreCase )
				|| error.Contains( "doesn't exist", StringComparison.OrdinalIgnoreCase )
				|| error.Contains( "does not exist", StringComparison.OrdinalIgnoreCase )
				|| error.Contains( "not found", StringComparison.OrdinalIgnoreCase ) );

	private async Task<IReadOnlyList<int>> ExpandWithDependenciesAsync(
		IReadOnlyList<int> entryIds,
		CancellationToken token = default,
		OperationPauseController? pause = null )
	{
		HashSet<int> allIds = [ .. entryIds ];
		string serverKey = OdooDefaults.Instance?.OdooUrl ?? "";
		bool useDirectEntryRpc = string.IsNullOrEmpty( serverKey ) || !NoDirectDependencyServerUrls.ContainsKey( serverKey );
		List<int> fallbackEntryIds = [];
		int totalChunks = ( entryIds.Count + RpcIdChunkSize - 1 ) / RpcIdChunkSize;
		int parallelism = RpcParallelism;

		if( useDirectEntryRpc && totalChunks > 0 )
		{
			if( pause is not null )
				await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );
			token.ThrowIfCancellationRequested();
			IReadOnlyList<int> firstChunk = totalChunks == 1 ? entryIds : OdooRpc.SliceChunk( entryIds, 0, RpcIdChunkSize );
			Result<IReadOnlyList<int>> firstDirect;
			using( OperationPauseController.EnterScope( pause ) )
				firstDirect = await DownloadOperations.GetAllEntriesAndDependenciesByEntriesAsync( firstChunk, token ).ConfigureAwait( false );
			if( firstDirect.Ok )
			{
				if( firstDirect.Value is { Count: > 0 } depIds )
					allIds.UnionWith( depIds );
				if( totalChunks > 1 )
				{
					IReadOnlyList<int>?[] remainingDeps = new IReadOnlyList<int>?[ totalChunks - 1 ];
					int checkedEntries = firstChunk.Count;
					await Parallel.ForEachAsync(
						Enumerable.Range( 1, totalChunks - 1 ),
						new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token },
						async ( chunkIndex, ct ) =>
						{
							if( pause is not null )
								await pause.WaitIfPausedAsync( ct ).ConfigureAwait( false );
							using OperationPauseController.WorkScope _ = OperationPauseController.EnterScope( pause );
							IReadOnlyList<int> chunk = OdooRpc.SliceChunk( entryIds, chunkIndex * RpcIdChunkSize, RpcIdChunkSize );
							Result<IReadOnlyList<int>> direct = await DownloadOperations.GetAllEntriesAndDependenciesByEntriesAsync( chunk, ct ).ConfigureAwait( false );
							if( direct.Ok )
								remainingDeps[ chunkIndex - 1 ] = direct.Value;
							int done = Interlocked.Add( ref checkedEntries, chunk.Count );
							Log( StatusMessage.PROCESSING, $"Checked dependencies for {done}/{entryIds.Count} entries..." );
						} ).ConfigureAwait( false );

					for( int i = 0; i < remainingDeps.Length; i++ )
					{
						if( remainingDeps[ i ] is { Count: > 0 } deps )
							allIds.UnionWith( deps );
					}
				}
				return [ .. allIds ];
			}
			if( IsMissingMethodError( firstDirect.Error ) && !string.IsNullOrEmpty( serverKey ) )
				NoDirectDependencyServerUrls.TryAdd( serverKey, 0 );
		}

		fallbackEntryIds.AddRange( entryIds );
		if( fallbackEntryIds.Count == 0 )
			return [ .. allIds ];

		int fallbackChunkCount = ( fallbackEntryIds.Count + RpcIdChunkSize - 1 ) / RpcIdChunkSize;
		List<int>[] versionIdPages = new List<int>[ fallbackChunkCount ];
		await Parallel.ForEachAsync(
			Enumerable.Range( 0, fallbackChunkCount ),
			new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token },
			async ( chunkIndex, ct ) =>
			{
				if( pause is not null )
					await pause.WaitIfPausedAsync( ct ).ConfigureAwait( false );
				using OperationPauseController.WorkScope _ = OperationPauseController.EnterScope( pause );
				int offset = chunkIndex * RpcIdChunkSize;
				IReadOnlyList<int> chunk = fallbackChunkCount == 1
					? fallbackEntryIds
					: OdooRpc.SliceChunk( fallbackEntryIds, offset, RpcIdChunkSize );
				Result<JsonElement> rows = await OdooRpc.ExecuteAsync<JsonElement>(
					HpEntry.ModelName,
					"read",
					[ chunk, LatestVersionIdFields ],
					90_000,
					ct ).ConfigureAwait( false );
				if( !rows.Ok || rows.Value.ValueKind != JsonValueKind.Array )
					return;
				List<int> pageIds = new( chunk.Count );
				foreach( JsonElement row in rows.Value.EnumerateArray() )
				{
					if( row.ValueKind != JsonValueKind.Object || !row.TryGetProperty( nameof( HpEntry.latest_version_id ), out JsonElement lv ) )
						continue;
					int vid = lv.ValueKind switch
					{
						JsonValueKind.Array when lv.GetArrayLength() > 0 && lv[ 0 ].ValueKind == JsonValueKind.Number && lv[ 0 ].TryGetInt32( out int a ) => a,
						JsonValueKind.Number when lv.TryGetInt32( out int n ) => n,
						_ => 0,
					};
					if( vid > 0 )
						pageIds.Add( vid );
				}
				versionIdPages[ chunkIndex ] = pageIds;
			} ).ConfigureAwait( false );

		List<int> versionIds = new( fallbackEntryIds.Count );
		for( int i = 0; i < versionIdPages.Length; i++ )
		{
			if( versionIdPages[ i ] is { Count: > 0 } page )
				versionIds.AddRange( page );
		}

		int verChunkCount = ( versionIds.Count + RpcIdChunkSize - 1 ) / RpcIdChunkSize;
		if( verChunkCount > 0 )
		{
			IReadOnlyList<int>?[] depPages = new IReadOnlyList<int>?[ verChunkCount ];
			await Parallel.ForEachAsync(
				Enumerable.Range( 0, verChunkCount ),
				new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = token },
				async ( chunkIndex, ct ) =>
				{
					if( pause is not null )
						await pause.WaitIfPausedAsync( ct ).ConfigureAwait( false );
					using OperationPauseController.WorkScope _ = OperationPauseController.EnterScope( pause );
					int offset = chunkIndex * RpcIdChunkSize;
					IReadOnlyList<int> chunk = verChunkCount == 1
						? versionIds
						: OdooRpc.SliceChunk( versionIds, offset, RpcIdChunkSize );
					Result<IReadOnlyList<int>> dependencies = await DownloadOperations.GetAllEntriesAndDependenciesListAsync( chunk, ct ).ConfigureAwait( false );
					if( !dependencies.Ok )
						Log( StatusMessage.WARNING, $"Dependencies could not be listed: {dependencies.Error}" );
					else
						depPages[ chunkIndex ] = dependencies.Value;
				} ).ConfigureAwait( false );

			for( int i = 0; i < depPages.Length; i++ )
			{
				if( depPages[ i ] is { Count: > 0 } depIds )
					allIds.UnionWith( depIds );
			}
		}

		return [ .. allIds ];
	}

	private static string CurrentAccountKey()
	{
		OdooDefaults? odoo = OdooDefaults.Instance;
		HackDefaults? hack = HackDefaults.Instance;
		return DownloadResumeStore.BuildAccountKey( odoo?.OdooUrl, odoo?.OdooDb, odoo?.OdooUser, hack?.PwaPathAbsolute );
	}

	public static DownloadResumeState? GetStoredResumableDownload( DownloadResumeStore? store = null )
	{
		if( !( HackDefaults.Instance?.PersistDownloadResume ?? true ) )
			return null;
		return ( store ?? DownloadResumeStore.Default ).LoadForAccount( CurrentAccountKey() );
	}

	public DownloadResumeState? GetResumableDownload()
		=> GetStoredResumableDownload( _resumeStore );

	public void ClearResumableDownload()
	{
		DownloadCheckpointTracker? active;
		lock( _stateGate )
			active = _activeDownloadCheckpoint;
		active?.Clear();
		_resumeStore.Clear();
		ResumableDownloadChanged?.Invoke();
	}

	public bool PrepareForShutdown()
	{
		bool persist = HackDefaults.Instance?.PersistDownloadResume ?? true;
		DownloadCheckpointTracker? active;
		lock( _stateGate )
		{
			active = _activeDownloadCheckpoint;
			if( active is not null && persist )
				_shutdownPreservingDownload = true;
		}
		if( active is not null )
		{
			if( persist )
				active.FlushSync();
			else
				active.Clear();
		}
		return HasActiveCommit;
	}

	public async Task WaitActiveCommitCleanupAsync( TimeSpan timeout )
	{
		Task? task;
		lock( _stateGate )
			task = _activeCommitTask;
		if( task is null || task.IsCompleted )
			return;
		try
		{
			await task.WaitAsync( timeout ).ConfigureAwait( false );
		}
		catch
		{
		}
	}

	public Task GetLatestAsync( IReadOnlyList<int> entryIds )
		=> GetLatestCoreAsync( entryIds, preExpandedIds: null, DownloadResumeKind.GetLatest, checkoutEntryIds: null );

	private async Task<IReadOnlyList<int>?> GetLatestCoreAsync(
		IReadOnlyList<int> entryIds,
		IReadOnlyList<int>? preExpandedIds,
		string resumeKind,
		IReadOnlyList<int>? checkoutEntryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return null;

		CancellationTokenSource cancellation = reporter.BeginOperation( canPause: true );
		OperationPauseController? pause = reporter.PauseController;
		using IDisposable? watcherPause = localFiles?.Suspend();
		bool persistEnabled = HackDefaults.Instance?.PersistDownloadResume ?? true;
		DownloadCheckpointTracker? checkpoint = persistEnabled
			? new DownloadCheckpointTracker(
				_resumeStore,
				enabled: true,
				accountKey: CurrentAccountKey(),
				vaultPath: HackDefaults.Instance?.PwaPathAbsolute ?? string.Empty,
				operationKind: resumeKind,
				label: resumeKind == DownloadResumeKind.CheckoutLatest ? "Checkout & Get Latest" : "Get Latest",
				pendingEntryIds: entryIds,
				checkoutEntryIds: checkoutEntryIds )
			: null;
		checkpoint?.InitializePendingEntries( flushNow: true );

		Action<OperationPauseState>? pauseHandler = BeginDownloadSession( pause, checkpoint, persistEnabled );

		try
		{
			IReadOnlyList<int>? ids = preExpandedIds;
			bool downloadSucceeded = false;
			bool completed = await AsyncHelper.AsyncRunner( async () =>
			{
				if( pause is not null )
					await pause.WaitIfPausedAsync( cancellation.Token ).ConfigureAwait( false );
				if( ids is null )
				{
					Log( StatusMessage.INFO, $"Finding Entry Dependencies for {entryIds.Count} entries..." );
					ids = await ExpandWithDependenciesAsync( entryIds, cancellation.Token, pause ).ConfigureAwait( false );
				}
				downloadSucceeded = await DownloadLatestAsync( ids, cancellation.Token, pause, checkpoint ).ConfigureAwait( false );
			}, "Get Latest", cancellation ).ConfigureAwait( false );
			if( !completed )
			{
				await HandleDownloadCancellationAsync( checkpoint ).ConfigureAwait( false );
				return null;
			}
			return downloadSucceeded ? ids : null;
		}
		catch( OperationCanceledException )
		{
			await HandleDownloadCancellationAsync( checkpoint ).ConfigureAwait( false );
			return null;
		}
		finally
		{
			EndDownloadSession( pause, pauseHandler, checkpoint );
		}
	}

	private Action<OperationPauseState>? BeginDownloadSession(
		OperationPauseController? pause,
		DownloadCheckpointTracker? checkpoint,
		bool persistEnabled )
	{
		Action<OperationPauseState>? handler = null;
		if( pause is not null )
		{
			handler = state =>
			{
				if( state == OperationPauseState.Paused )
				{
					checkpoint?.FlushSync();
					Log( StatusMessage.INFO, persistEnabled
						? "Download paused at a safe stopping point. Click Resume to continue (or close the app to resume later)."
						: "Download paused at a safe stopping point. Click Resume to continue." );
				}
				else if( state == OperationPauseState.Running )
				{
					Log( StatusMessage.INFO, "Resuming download..." );
				}
			};
			pause.StateChanged += handler;
		}

		lock( _stateGate )
		{
			_shutdownPreservingDownload = false;
			_activeDownloadCheckpoint = checkpoint;
		}
		return handler;
	}

	private Task HandleDownloadCancellationAsync( DownloadCheckpointTracker? checkpoint )
	{
		bool preserve;
		lock( _stateGate )
			preserve = _shutdownPreservingDownload;
		if( preserve && checkpoint is { Enabled: true } && checkpoint.HasWork() )
		{
			checkpoint.FlushSync();
			return Task.CompletedTask;
		}
		checkpoint?.Clear();
		return reporter.NotifyAsync( "Cancelled Download" );
	}

	private void EndDownloadSession(
		OperationPauseController? pause,
		Action<OperationPauseState>? handler,
		DownloadCheckpointTracker? checkpoint )
	{
		if( pause is not null && handler is not null )
			pause.StateChanged -= handler;
		lock( _stateGate )
		{
			if( ReferenceEquals( _activeDownloadCheckpoint, checkpoint ) )
				_activeDownloadCheckpoint = null;
			_shutdownPreservingDownload = false;
		}
		reporter.Complete();
		ResumableDownloadChanged?.Invoke();
	}

	private async Task<bool> DownloadLatestAsync(
		IReadOnlyList<int> entryIds,
		CancellationToken token,
		OperationPauseController? pause = null,
		DownloadCheckpointTracker? checkpoint = null )
	{
		if( pause is not null )
			await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );

		Log( StatusMessage.FOUND, $"{entryIds.Count} entries" );
		Log( StatusMessage.PROCESSING, "Retrieving all latest versions associated with entries..." );

		HpVersion[]? versions = await DownloadOperations.GetLatestVersionsAsync(
			entryIds, excludedFields: null, token: token, includedFields: LatestVersionIncludedFields, log: Log, pause: pause ).ConfigureAwait( false );
		reporter.SetTotal( versions?.Length ?? 0 );

		if( versions is not { Length: > 0 } )
		{
			checkpoint?.Clear();
			reporter.Complete();
			await reporter.NotifyAsync( "Cancelled Download... No Versions to Process" ).ConfigureAwait( false );
			return false;
		}

		Log( StatusMessage.INFO, $"Retrieved {versions.Length} version(s); checking local files..." );

		DownloadOperations.DownloadSummary summary = new();
		try
		{
			if( pause is not null )
				await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );

			List<HpVersion> toDownload = await DownloadOperations.FilterVersionsToDownloadAsync(
				versions,
				token,
				summary,
				log: Log,
				onProgress: ( processed, skipped ) => reporter.Advance( processed, skipped ) ).ConfigureAwait( false );

			if( toDownload.Count > 0 )
			{
				checkpoint?.SetResolvedVersions( toDownload, versions.Length, summary.Skipped );

				if( pause is not null )
					await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );

				long totalBytes = 0;
				for( int i = 0; i < toDownload.Count; i++ )
					totalBytes += Math.Max( 0, (long)( toDownload[ i ].file_size ?? 0 ) );

				int maxBatchCount = Math.Clamp(
					OdooDefaults.Instance.DownloadBatchSize,
					1,
					AdaptiveTransferController.MaxAdaptiveBatchCount );
				AdaptiveTransferController controller = new( maxBatchCount );

				Log(
					StatusMessage.INFO,
					$"Downloading {toDownload.Count} file(s) ({ByteSize.Format( totalBytes )}, {summary.Skipped} skipped; adaptive ≤{maxBatchCount} files / {ByteSize.Format( controller.CurrentBatchBytes )} targeting {AdaptiveTransferController.TargetRpcSeconds:F0}s/RPC)..." );

				reporter.BeginDownloadTransfer();
				try
				{
					await DownloadOperations.DownloadAdaptiveAsync(
						toDownload,
						token,
						summary,
						maxBatchCount: maxBatchCount,
						maxConcurrency: OdooDefaults.Instance.ConcurrencySize,
						log: Log,
						onProgress: ( processed, skipped ) => reporter.Advance( processed, skipped ),
						onBytesDownloaded: bytes => reporter.AddDownloadedBytes( bytes ),
						controller: controller,
						pause: pause,
						onVersionCompleted: ( vid, bytes ) => checkpoint?.MarkVersionCompleted( vid, bytes ) ).ConfigureAwait( false );
				}
				finally
				{
					reporter.EndDownloadTransfer();
				}
			}
		}
		catch( Exception e ) when( e is not OperationCanceledException )
		{
			Log( StatusMessage.ERROR, $"download failed: {e}" );
			await reporter.NotifyAsync( $"Download failed: {e.Message}", "Get Latest" ).ConfigureAwait( false );
			return false;
		}

		if( summary.Failed.IsEmpty )
			checkpoint?.Clear();
		else if( checkpoint is { Enabled: true } && !summary.FailedIds.IsEmpty )
		{
			checkpoint.SaveFailedOnly( summary.FailedIds );
			Log( StatusMessage.INFO, $"{summary.FailedIds.Count} unfinished file(s) saved for resume." );
		}
		else
			checkpoint?.Clear();

		reporter.Complete();

		string kept = summary.KeptLocalChanges.IsEmpty
			? ""
			: $"\n{summary.KeptLocalChanges.Count} file(s) with local changes were kept, not overwritten:\n"
				+ string.Join( "\n", summary.KeptLocalChanges.Take( 15 ) );
		if( summary.Failed.IsEmpty )
		{
			await reporter.NotifyAsync( $"Completed! {summary.Downloaded} downloaded, {summary.Skipped - summary.KeptLocalChanges.Count} already up to date.{kept}" ).ConfigureAwait( false );
			return true;
		}

		string[] failed = [ .. summary.Failed ];
		await reporter.NotifyAsync(
			$"Completed with {failed.Length} file(s) NOT downloaded ({summary.Downloaded} downloaded, {summary.Skipped} up to date):\n"
				+ string.Join( "\n", failed.Take( 15 ) )
				+ ( failed.Length > 15 ? $"\n...and {failed.Length - 15} more (see the status log)" : "" ) + kept,
			"Get Latest" ).ConfigureAwait( false );
		return false;
	}

	public async Task<bool> ResumeDownloadAsync()
	{
		DownloadResumeState? state = GetResumableDownload();
		if( state is null || !state.HasRemainingWork )
		{
			_resumeStore.Clear();
			ResumableDownloadChanged?.Invoke();
			return false;
		}

		if( state.RemainingVersionIds.Length == 0 && state.PendingEntryIds.Length > 0 )
		{
			if( string.Equals( state.OperationKind, DownloadResumeKind.CheckoutLatest, StringComparison.Ordinal ) )
			{
				await CheckoutLatestAsync( state.PendingEntryIds ).ConfigureAwait( false );
				return GetResumableDownload() is null;
			}
			await GetLatestAsync( state.PendingEntryIds ).ConfigureAwait( false );
			return GetResumableDownload() is null;
		}

		CancellationTokenSource cancellation = reporter.BeginOperation( canPause: true );
		OperationPauseController? pause = reporter.PauseController;
		using IDisposable? watcherPause = localFiles?.Suspend();
		bool persistEnabled = HackDefaults.Instance?.PersistDownloadResume ?? true;
		DownloadCheckpointTracker? checkpoint = persistEnabled
			? new DownloadCheckpointTracker(
				_resumeStore,
				enabled: true,
				accountKey: state.AccountKey,
				vaultPath: state.VaultPath,
				operationKind: state.OperationKind,
				label: state.Label,
				pendingEntryIds: null,
				checkoutEntryIds: state.CheckoutEntryIds,
				existingState: state )
			: null;

		Action<OperationPauseState>? pauseHandler = BeginDownloadSession( pause, checkpoint, persistEnabled );

		bool succeeded = false;
		bool runCheckout = false;
		try
		{
			int initialProcessed = Math.Max( 0, state.CompletedCount - state.SkippedCount );
			reporter.RestoreProgress( Math.Max( state.TotalCount, state.RemainingVersionIds.Length ), initialProcessed, state.SkippedCount, state.DownloadedBytes );
			Log( StatusMessage.INFO, $"Resuming saved download ({state.RemainingVersionIds.Length} remaining version(s))..." );

			bool completed = await AsyncHelper.AsyncRunner( async () =>
			{
				if( pause is not null )
					await pause.WaitIfPausedAsync( cancellation.Token ).ConfigureAwait( false );

				HpVersion[] versions = await FetchVersionsByIdsChunkedAsync(
					state.RemainingVersionIds,
					pause,
					cancellation.Token ).ConfigureAwait( false );

				if( versions.Length == 0 )
				{
					checkpoint?.Clear();
					succeeded = true;
					runCheckout = string.Equals( state.OperationKind, DownloadResumeKind.CheckoutLatest, StringComparison.Ordinal )
						&& state.CheckoutEntryIds.Length > 0;
					reporter.Complete();
					await reporter.NotifyAsync( "Completed! All remaining files were already up to date." ).ConfigureAwait( false );
					return;
				}

				DownloadOperations.DownloadSummary summary = new();
				if( pause is not null )
					await pause.WaitIfPausedAsync( cancellation.Token ).ConfigureAwait( false );

				List<HpVersion> toDownload = await DownloadOperations.FilterVersionsToDownloadAsync(
					versions,
					cancellation.Token,
					summary,
					log: Log,
					onProgress: ( processed, skipped ) => reporter.Advance( processed, skipped ) ).ConfigureAwait( false );

				if( toDownload.Count > 0 )
				{
					checkpoint?.SetResolvedVersions( toDownload, Math.Max( state.TotalCount, versions.Length ), summary.Skipped );
					if( pause is not null )
						await pause.WaitIfPausedAsync( cancellation.Token ).ConfigureAwait( false );

					int maxBatchCount = Math.Clamp( OdooDefaults.Instance.DownloadBatchSize, 1, AdaptiveTransferController.MaxAdaptiveBatchCount );
					AdaptiveTransferController controller = new( maxBatchCount );

					reporter.BeginDownloadTransfer();
					try
					{
						await DownloadOperations.DownloadAdaptiveAsync(
							toDownload,
							cancellation.Token,
							summary,
							maxBatchCount: maxBatchCount,
							maxConcurrency: OdooDefaults.Instance.ConcurrencySize,
							log: Log,
							onProgress: ( processed, skipped ) => reporter.Advance( processed, skipped ),
							onBytesDownloaded: bytes => reporter.AddDownloadedBytes( bytes ),
							controller: controller,
							pause: pause,
							onVersionCompleted: ( vid, bytes ) => checkpoint?.MarkVersionCompleted( vid, bytes ) ).ConfigureAwait( false );
					}
					finally
					{
						reporter.EndDownloadTransfer();
					}
				}

				if( summary.Failed.IsEmpty )
				{
					checkpoint?.Clear();
					succeeded = true;
					runCheckout = string.Equals( state.OperationKind, DownloadResumeKind.CheckoutLatest, StringComparison.Ordinal )
						&& state.CheckoutEntryIds.Length > 0;
					reporter.Complete();
					string kept = summary.KeptLocalChanges.IsEmpty
						? ""
						: $"\n{summary.KeptLocalChanges.Count} file(s) with local changes were kept, not overwritten:\n"
							+ string.Join( "\n", summary.KeptLocalChanges.Take( 15 ) );
					await reporter.NotifyAsync( $"Completed! {summary.Downloaded} downloaded, {summary.Skipped - summary.KeptLocalChanges.Count} already up to date.{kept}" ).ConfigureAwait( false );
				}
				else
				{
					if( checkpoint is { Enabled: true } && !summary.FailedIds.IsEmpty )
					{
						checkpoint.SaveFailedOnly( summary.FailedIds );
						Log( StatusMessage.INFO, $"{summary.FailedIds.Count} unfinished file(s) saved for resume." );
					}
					else
					{
						checkpoint?.Clear();
					}
					reporter.Complete();
					string[] failed = [ .. summary.Failed ];
					await reporter.NotifyAsync(
						$"Completed with {failed.Length} file(s) NOT downloaded ({summary.Downloaded} downloaded, {summary.Skipped} up to date):\n"
							+ string.Join( "\n", failed.Take( 15 ) )
							+ ( failed.Length > 15 ? $"\n...and {failed.Length - 15} more (see the status log)" : "" ),
						"Resume Download" ).ConfigureAwait( false );
				}
			}, "Resume Download", cancellation ).ConfigureAwait( false );

			if( !completed )
			{
				await HandleDownloadCancellationAsync( checkpoint ).ConfigureAwait( false );
				return false;
			}
		}
		catch( OperationCanceledException )
		{
			await HandleDownloadCancellationAsync( checkpoint ).ConfigureAwait( false );
			return false;
		}
		finally
		{
			EndDownloadSession( pause, pauseHandler, checkpoint );
		}

		if( runCheckout )
			await ChangeCheckoutAsync( state.CheckoutEntryIds, checkout: true, preExpandedIds: null ).ConfigureAwait( false );

		return succeeded;
	}

	private async Task<HpVersion[]> FetchVersionsByIdsChunkedAsync(
		IReadOnlyList<int> versionIds,
		OperationPauseController? pause,
		CancellationToken token )
	{
		if( versionIds.Count == 0 )
			return [];
		if( versionIds.Count <= VersionReadChunkSize )
		{
			if( pause is not null )
				await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );
			using OperationPauseController.WorkScope _ = OperationPauseController.EnterScope( pause );
			return await HpVersion.GetRecordsByIdsAsync( versionIds, includedFields: LatestVersionIncludedFields, token: token ).ConfigureAwait( false ) ?? [];
		}

		int chunkCount = ( versionIds.Count + VersionReadChunkSize - 1 ) / VersionReadChunkSize;
		HpVersion[]?[] pages = new HpVersion[]?[ chunkCount ];
		await Parallel.ForEachAsync(
			Enumerable.Range( 0, chunkCount ),
			new ParallelOptions { MaxDegreeOfParallelism = RpcParallelism, CancellationToken = token },
			async ( chunkIndex, ct ) =>
			{
				if( pause is not null )
					await pause.WaitIfPausedAsync( ct ).ConfigureAwait( false );
				using OperationPauseController.WorkScope _ = OperationPauseController.EnterScope( pause );
				int[] chunk = OdooRpc.SliceChunk( versionIds, chunkIndex * VersionReadChunkSize, VersionReadChunkSize );
				pages[ chunkIndex ] = await HpVersion.GetRecordsByIdsAsync( chunk, includedFields: LatestVersionIncludedFields, token: ct ).ConfigureAwait( false );
			} ).ConfigureAwait( false );

		int total = 0;
		for( int i = 0; i < pages.Length; i++ )
			total += pages[ i ]?.Length ?? 0;
		if( total == 0 )
			return [];

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

	public Task CheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: true, preExpandedIds: null );
	public Task UnCheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: false, preExpandedIds: null );

	public async Task CheckoutLatestAsync( IReadOnlyList<int> entryIds )
	{
		IReadOnlyList<int>? expanded = await GetLatestCoreAsync(
			entryIds,
			preExpandedIds: null,
			DownloadResumeKind.CheckoutLatest,
			checkoutEntryIds: entryIds ).ConfigureAwait( false );
		if( expanded is { Count: > 0 } )
			await ChangeCheckoutAsync( entryIds, checkout: true, preExpandedIds: expanded ).ConfigureAwait( false );
	}

	private static async Task<Result<List<HpEntry>>> SearchEntriesByIdChunksAsync(
		IReadOnlyList<int> entryIds,
		OdooDomain? extraFilter,
		FieldSelection fields,
		CancellationToken token )
	{
		if( entryIds.Count == 0 )
			return Result<List<HpEntry>>.Success( [] );

		int chunkCount = ( entryIds.Count + RpcIdChunkSize - 1 ) / RpcIdChunkSize;
		Result<HpEntry[]>[] pages = new Result<HpEntry[]>[ chunkCount ];

		async Task FetchPageAsync( int chunkIndex, CancellationToken ct )
		{
			int offset = chunkIndex * RpcIdChunkSize;
			IReadOnlyList<int> chunk = chunkCount == 1
				? entryIds
				: OdooRpc.SliceChunk( entryIds, offset, RpcIdChunkSize );
			OdooDomain domain = extraFilter is null
				? OdooDomain.IdIn( chunk )
				: extraFilter.And( nameof( HpEntry.id ), Operators.In, chunk );
			pages[ chunkIndex ] = await Repository<HpEntry>.SearchReadAsync( domain, fields, token: ct ).ConfigureAwait( false );
		}

		if( chunkCount == 1 )
		{
			token.ThrowIfCancellationRequested();
			await FetchPageAsync( 0, token ).ConfigureAwait( false );
		}
		else
		{
			await Parallel.ForEachAsync(
				Enumerable.Range( 0, chunkCount ),
				new ParallelOptions { MaxDegreeOfParallelism = RpcParallelism, CancellationToken = token },
				async ( idx, ct ) => await FetchPageAsync( idx, ct ).ConfigureAwait( false ) ).ConfigureAwait( false );
		}

		List<HpEntry> entries = new( entryIds.Count );
		for( int i = 0; i < pages.Length; i++ )
		{
			Result<HpEntry[]> batch = pages[ i ];
			if( !batch.Ok )
				return Result<List<HpEntry>>.Fail( batch.Error );
			if( batch.Value is { Length: > 0 } rows )
				entries.AddRange( rows );
		}
		return Result<List<HpEntry>>.Success( entries );
	}

	private async Task ChangeCheckoutAsync( IReadOnlyList<int> entryIds, bool checkout, IReadOnlyList<int>? preExpandedIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		string opTitle = checkout ? "Checkout Files" : "UnCheckout Files";
		CancellationTokenSource cancellation = reporter.BeginOperation();
		using IDisposable? watcherPause = localFiles?.Suspend();
		try
		{
			int me = OdooDefaults.Instance.OdooId;
			bool completed = await AsyncHelper.AsyncRunner( async () =>
			{
				IReadOnlyList<int> ids = preExpandedIds ?? await ExpandWithDependenciesAsync( entryIds, cancellation.Token ).ConfigureAwait( false );
				Result<List<HpEntry>> entriesResult = await SearchEntriesByIdChunksAsync(
					ids,
					checkout ? null : OdooDomain.Where( nameof( HpEntry.checkout_user ), Operators.Equal, me ),
					new( Excluded: HeavyEntryFields ),
					cancellation.Token ).ConfigureAwait( false );
				if( !entriesResult.Ok )
				{
					string message = $"Could not fetch entries to {( checkout ? "check out" : "undo check out of" )}: {entriesResult.Error}";
					Log( StatusMessage.ERROR, message );
					await reporter.NotifyAsync( message, opTitle ).ConfigureAwait( false );
					return;
				}

				if( !checkout )
				{
					Result<List<HpEntry>> othersResult = await SearchEntriesByIdChunksAsync(
						entryIds,
						OdooDomain.Where( nameof( HpEntry.checkout_user ), Operators.NotEqual, me ).And( nameof( HpEntry.checkout_user ), Operators.NotEqual, false ),
						FieldSelection.Only( nameof( HpEntry.id ), nameof( HpEntry.name ), nameof( HpEntry.checkout_user ) ),
						cancellation.Token ).ConfigureAwait( false );
					if( !othersResult.Ok )
					{
						string message = $"Could not check who has entries checked out: {othersResult.Error}";
						Log( StatusMessage.ERROR, message );
						await reporter.NotifyAsync( message, opTitle ).ConfigureAwait( false );
						return;
					}
					foreach( HpEntry other in othersResult.Value! )
						Log( StatusMessage.ERROR, $"Cannot undo the check out of {other.name}: it is checked out to {other.checkout_user?.name ?? "another user"}" );
				}
				if( entriesResult.Value is not { Count: > 0 } entries )
					return;

				reporter.SetTotal( entries.Count );
				HpEntry[] entryArray = [ .. entries ];
				if( checkout )
					await CheckoutOperations.CheckOutEntriesAsync( entryArray, Log, OnCheckoutProgress, cancellation.Token ).ConfigureAwait( false );
				else
					await CheckoutOperations.UnCheckOutEntriesAsync( entryArray, me, Log, OnCheckoutProgress, cancellation.Token ).ConfigureAwait( false );
				reporter.Complete();
				await reporter.NotifyAsync( "Completed!" ).ConfigureAwait( false );
			}, opTitle, cancellation ).ConfigureAwait( false );
			if( !completed )
				await reporter.NotifyAsync( checkout ? "Cancelled Checkout" : "Cancelled UnCheckout" ).ConfigureAwait( false );
		}
		catch( OperationCanceledException )
		{
			await reporter.NotifyAsync( checkout ? "Cancelled Checkout" : "Cancelled UnCheckout" ).ConfigureAwait( false );
		}
		finally
		{
			reporter.Complete();
		}
	}

	private void OnCheckoutProgress( int processed, int total )
		=> reporter.SetProcessed( processed );

	public async Task CommitAsync( IEnumerable<HackFile> hackFiles )
	{
		List<HackFile> files = [ .. hackFiles ];
		if( files.Count == 0 )
			return;

		TaskCompletionSource commitDone = new( TaskCreationOptions.RunContinuationsAsynchronously );
		lock( _stateGate )
		{
			_activeCommitCount++;
			_activeCommitTask = commitDone.Task;
		}

		CancellationTokenSource cancellation = reporter.BeginOperation( canPause: true );
		OperationPauseController? pause = reporter.PauseController;
		using IDisposable? watcherPause = localFiles?.Suspend();

		void OnPauseStateChanged( OperationPauseState state )
		{
			if( state == OperationPauseState.Paused )
				Log( StatusMessage.INFO, "Commit paused at a safe stopping point. Click Resume to continue (commits do not persist across app restarts)." );
			else if( state == OperationPauseState.Running )
				Log( StatusMessage.INFO, "Resuming commit..." );
		}

		if( pause is not null )
			pause.StateChanged += OnPauseStateChanged;

		try
		{
			bool completed = await AsyncHelper.AsyncRunner(
				() => CommitFilesAsync( files, cancellation.Token, pause ),
				"Commit Files",
				cancellation ).ConfigureAwait( false );
			if( !completed )
				await reporter.NotifyAsync( "Cancelled Commit" ).ConfigureAwait( false );
		}
		catch( OperationCanceledException )
		{
			await reporter.NotifyAsync( "Cancelled Commit" ).ConfigureAwait( false );
		}
		finally
		{
			if( pause is not null )
				pause.StateChanged -= OnPauseStateChanged;
			commitDone.TrySetResult();
			lock( _stateGate )
			{
				_activeCommitCount--;
				if( ReferenceEquals( _activeCommitTask, commitDone.Task ) )
					_activeCommitTask = null;
			}
			reporter.Complete();
		}
	}

	private async Task CommitFilesAsync( List<HackFile> files, CancellationToken token, OperationPauseController? pause )
	{
		try
		{
			if( pause is not null )
				await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );

			var (localOnly, remoteExisting, unchanged, remoteByServerName) =
				await CommitOperations.FilterAndSegmentCommitFilesAsync( files, Log, token, pause ).ConfigureAwait( false );
			if( localOnly.Length == 0 && remoteExisting.Length == 0 )
			{
				if( unchanged.Length > 0 )
				{
					if( pause is not null )
						await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );
					await CheckInUnchangedAsync( unchanged, token ).ConfigureAwait( false );
					reporter.Complete();
					await reporter.NotifyAsync( "Completed!" ).ConfigureAwait( false );
					return;
				}
				Log( StatusMessage.ERROR, "--- No entries to commit ---" );
				await reporter.NotifyAsync( "No eligible entries to commit (see the status log).", "Commit Files" ).ConfigureAwait( false );
				return;
			}

			if( pause is not null )
				await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );

			HpPDMCommit commit = new() { node_by = OdooDefaults.Instance.MyNode?.id };
			using( OperationPauseController.EnterScope( pause ) )
				await commit.CreateCommitAsync( token ).ConfigureAwait( false );

			reporter.SetTotal( localOnly.Length + remoteExisting.Length );
			Log( StatusMessage.PROCESSING, "--- Preparing to stage records ---" );

			Dictionary<string, HpDirectory> directoryCache = new( StringComparer.OrdinalIgnoreCase );
			await CommitOperations.PreResolveDirectoriesAsync( localOnly, directoryCache, token, pause ).ConfigureAwait( false );

			(HackFile File, bool InOdoo)[] staged = new (HackFile, bool)[ localOnly.Length + remoteExisting.Length ];
			for( int i = 0; i < localOnly.Length; i++ )
				staged[ i ] = ( localOnly[ i ], false );
			for( int i = 0; i < remoteExisting.Length; i++ )
				staged[ localOnly.Length + i ] = ( remoteExisting[ i ], true );

			string? lastStageError = null;
			void LogWithStageError( StatusMessage status, string message )
			{
				if( status == StatusMessage.ERROR )
					Volatile.Write( ref lastStageError, message );
				Log( status, message );
			}

			reporter.BeginUploadTransfer();
			int stageFailed = 0;
			try
			{
				using CancellationTokenSource batchCts = CancellationTokenSource.CreateLinkedTokenSource( token );
				try
				{
					if( staged.Length == 1 )
					{
						if( pause is not null )
							await pause.WaitIfPausedAsync( batchCts.Token ).ConfigureAwait( false );
						if( !await CommitOperations.CommitRecordInternalAsync(
							staged[ 0 ].File,
							commit,
							staged[ 0 ].InOdoo,
							LogWithStageError,
							() => reporter.Advance( 1 ),
							bytes => reporter.AddUploadedBytes( bytes ),
							directoryCache,
							remoteByServerName,
							clearOnFailure: false,
							batchCts.Token,
							pause ).ConfigureAwait( false ) )
						{
							stageFailed = 1;
						}
					}
					else
					{
						await Parallel.ForEachAsync(
							staged,
							new ParallelOptions { MaxDegreeOfParallelism = RpcParallelism, CancellationToken = batchCts.Token },
							async ( item, ct ) =>
							{
								if( pause is not null )
									await pause.WaitIfPausedAsync( ct ).ConfigureAwait( false );
								if( !await CommitOperations.CommitRecordInternalAsync(
									item.File,
									commit,
									item.InOdoo,
									LogWithStageError,
									() => reporter.Advance( 1 ),
									bytes => reporter.AddUploadedBytes( bytes ),
									directoryCache,
									remoteByServerName,
									clearOnFailure: false,
									ct,
									pause ).ConfigureAwait( false ) )
								{
									Interlocked.Exchange( ref stageFailed, 1 );
									batchCts.Cancel();
								}
							} ).ConfigureAwait( false );
					}
				}
				catch( OperationCanceledException ) when( !token.IsCancellationRequested && Volatile.Read( ref stageFailed ) != 0 )
				{
				}
				catch( OperationCanceledException )
				{
					await commit.ServerClear().ConfigureAwait( false );
					throw;
				}

				if( Volatile.Read( ref stageFailed ) != 0 )
				{
					await commit.ServerClear( token ).ConfigureAwait( false );
					await reporter.NotifyAsync(
						$"Commit failed: {Volatile.Read( ref lastStageError ) ?? "staging failed; changes rolled back."}",
						"Commit Files" ).ConfigureAwait( false );
					return;
				}
			}
			finally
			{
				reporter.EndUploadTransfer();
			}

			if( pause is not null )
			{
				try
				{
					await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );
				}
				catch( OperationCanceledException )
				{
					await commit.ServerClear().ConfigureAwait( false );
					throw;
				}
			}

			(bool committed, string? error) serverCommitResult;
			using( OperationPauseController.EnterScope( pause ) )
				serverCommitResult = await commit.ServerCommit( token ).ConfigureAwait( false );
			if( !serverCommitResult.committed )
			{
				Log( StatusMessage.ERROR, $"Commit failed on the server: {serverCommitResult.error}" );
				await reporter.NotifyAsync( $"Commit failed: {serverCommitResult.error}", "Commit Files" ).ConfigureAwait( false );
				return;
			}

			if( pause is not null )
				await pause.WaitIfPausedAsync( token ).ConfigureAwait( false );
			await CheckInUnchangedAsync( unchanged, token ).ConfigureAwait( false );
			reporter.Complete();
			await reporter.NotifyAsync( "Completed!" ).ConfigureAwait( false );
		}
		catch( OdooException ex )
		{
			Log( StatusMessage.ERROR, $"Commit failed: {ex.Message}" );
			await reporter.NotifyAsync( $"Commit failed: {ex.Message}", "Commit Files" ).ConfigureAwait( false );
		}
		catch( Exception ex ) when( ex is not OperationCanceledException )
		{
			Log( StatusMessage.ERROR, $"Commit failed: {ex.Message}" );
			await reporter.NotifyAsync( $"Commit failed: {ex.Message}", "Commit Files" ).ConfigureAwait( false );
		}
	}

	private async Task CheckInUnchangedAsync( HpEntry[] unchanged, CancellationToken token = default )
	{
		if( unchanged.Length == 0 )
			return;
		Log( StatusMessage.INFO, $"{unchanged.Length} unchanged file(s): checked in without a new version" );
		await CheckoutOperations.UnCheckOutEntriesAsync( unchanged, OdooDefaults.Instance.OdooId, Log, token: token ).ConfigureAwait( false );
	}

	public async Task<HashSet<HackFile>> CollectCommitFilesAsync( IEnumerable<EntryRow> entries, CancellationToken token = default )
	{
		Log( StatusMessage.PROCESSING, "checking entries and dependencies" );
		EntryRow[] snapshot = entries as EntryRow[] ?? [ .. entries ];
		bool restrictTypes = OdooDefaults.Instance.RestrictTypes is true;
		var (hackFiles, brokenDeps) = await Task.Run( () =>
		{
			HashSet<HackFile> collected = [];
			SwReferenceResolver? sharedResolver = null;
			for( int i = 0; i < snapshot.Length; i++ )
			{
				token.ThrowIfCancellationRequested();
				EntryRow item = snapshot[ i ];
				if( string.IsNullOrEmpty( item.FullName ) )
					continue;
				if( restrictTypes && string.IsNullOrEmpty( item.Type ) )
					continue;

				if( SwDocumentTypes.IsSolidWorksExtension( item.Type ) )
				{
					sharedResolver ??= new SwReferenceResolver( HackDefaults.Instance.PwaPathAbsolute );
					var (success, files, broken) = DependencyScanner.ScanSingle( new HackFile( item ), sharedResolver, token );
					if( success )
					{
						foreach( HackFile f in files )
							collected.Add( f );
						continue;
					}
					return ( collected, broken );
				}

				string? localPath = item.ReprType is EntryReprType.Local
					? item.FullName
					: PwaPaths.ToAbsolute( item.FullName );
				if( HackFile.GetFromPath( localPath ) is { Exists: true } hack )
					collected.Add( hack );
			}
			return ( collected, (List<BrokenDependency>?)null );
		}, token ).ConfigureAwait( false );

		if( brokenDeps is { Count: > 0 } )
		{
			foreach( BrokenDependency b in brokenDeps )
				Log( StatusMessage.ERROR, $"({b.Reason}) file: {b.FullPath}" );
			Log( StatusMessage.ERROR, "Commit Terminated" );
			return [];
		}

		if( hackFiles.Count == 0 )
			Log( StatusMessage.INFO, "no valid entries to process" );
		return hackFiles;
	}

	public async Task<List<HackFile>> CollectFolderCommitFilesAsync( string folderPath, CancellationToken token = default )
	{
		var (files, broken) = await DependencyScanner.GetFolderWithDependenciesAsync( folderPath, token ).ConfigureAwait( false );
		if( broken.Count > 0 )
		{
			foreach( BrokenDependency b in broken )
				Log( StatusMessage.ERROR, $"({b.Reason}) file: {b.FullPath}" );
			Log( StatusMessage.WARNING, "SolidWorks files with missing references were left out of the commit" );
		}
		return files;
	}

	public async Task LogicalDeleteAsync( IReadOnlyList<int> entryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		CancellationTokenSource cancellation = reporter.BeginOperation();
		try
		{
			HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( entryIds, excludedFields: DeleteExcludedFields, token: cancellation.Token ).ConfigureAwait( false );
			await AsyncHelper.AsyncRunner( () => DeleteOperations.SetDeletedAsync( entries, true, Log, cancellation.Token ), "Logically Delete Files", cancellation ).ConfigureAwait( false );
		}
		finally
		{
			reporter.Complete();
		}
	}

	public async Task RestoreDeletedInDirectoryAsync( int directoryId, bool withSubdirectories = false )
	{
		CancellationTokenSource cancellation = reporter.BeginOperation();
		try
		{
			HpEntry[]? entries;
			if( withSubdirectories )
			{
				Result<IReadOnlyList<int>> idsResult = await HpDirectory.GetDirectoryEntryIDsAsync( directoryId, withSubEntries: true, withDeleted: true, token: cancellation.Token ).ConfigureAwait( false );
				if( !idsResult.Ok )
				{
					Log( StatusMessage.ERROR, $"Could not list the directory's entries: {idsResult.Error}" );
					return;
				}
				IReadOnlyList<int> ids = idsResult.ValueOr( [] );
				if( ids.Count == 0 )
					return;
				entries = await HpEntry.GetRecordsByIdsAsync(
					ids,
					searchFilters: OdooDomain.Where( nameof( HpEntry.deleted ), true ),
					excludedFields: DeleteExcludedFields,
					token: cancellation.Token ).ConfigureAwait( false );
			}
			else
			{
				entries = await HpEntry.GetRecordsByIdsAsync(
					null,
					searchFilters: OdooDomain.Where( nameof( HpEntry.deleted ), true ).And( nameof( HpEntry.dir_id ), directoryId ),
					excludedFields: DeleteExcludedFields,
					token: cancellation.Token ).ConfigureAwait( false );
			}

			if( entries is not { Length: > 0 } )
				return;

			await AsyncHelper.AsyncRunner( () => DeleteOperations.SetDeletedAsync( entries, false, Log, cancellation.Token ), "Logically UnDelete Files", cancellation ).ConfigureAwait( false );
		}
		finally
		{
			reporter.Complete();
		}
	}

	public async Task<bool> IsCurrentUserPdmAdminAsync()
	{
		Result<bool> result = await OdooRpc.ExecuteAsync<bool>( "res.users", "has_group", [ "hackpdm.group_pdm_admin" ] ).ConfigureAwait( false );
		return result.ValueOr( false );
	}

	public async Task<bool> PermanentDeleteAsync( IReadOnlyList<int> entryIds )
	{
		bool deleted = false;
		CancellationTokenSource cancellation = reporter.BeginOperation();
		try
		{
			await AsyncHelper.AsyncRunner( async () =>
			{
				deleted = await DeleteOperations.PermanentDeleteEntry( entryIds, Log, cancellation.Token ).ConfigureAwait( false );
				if( deleted )
					Log( StatusMessage.SUCCESS, "Completed permanent delete" );
			}, "Permanently Delete Files", cancellation ).ConfigureAwait( false );
			return deleted;
		}
		finally
		{
			reporter.Complete();
		}
	}

	public async Task<VersionDownloadPlan?> PlanVersionDownloadAsync( int versionId )
	{
		if( versionId is 0 )
			return null;

		HpVersion? version = ( await HpVersion.GetRecordsByIdsAsync( [ versionId ], excludedFields: HpVersion.UsualExcludedFields ).ConfigureAwait( false ) )?.FirstOrDefault();
		if( version is null )
			return null;

		HpEntry? entry = version.entry_id?.id is int entryId ? await HpEntry.GetRecordByIdAsync( entryId ).ConfigureAwait( false ) : null;
		Result<IReadOnlyList<int>> dependencies = await DownloadOperations.GetVersionListAsync( versionId ).ConfigureAwait( false );
		if( !dependencies.Ok )
			Log( StatusMessage.WARNING, $"Dependencies of version {versionId} could not be listed: {dependencies.Error}" );
		IReadOnlyList<int> versionIds = [ .. dependencies.ValueOr( [] ).Append( versionId ).Distinct() ];

		return new VersionDownloadPlan( version, entry?.latest_version_id == versionId, versionIds );
	}

	public async Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds )
	{
		if( versionIds is null or { Count: 0 } )
			return [];

		int[] requestedIds = [ .. versionIds.Where( static id => id > 0 ).Distinct() ];
		if( requestedIds.Length == 0 )
			return [];

		CancellationTokenSource cancellation = reporter.BeginOperation( canPause: true );
		OperationPauseController? pause = reporter.PauseController;
		using IDisposable? watcherPause = localFiles?.Suspend();
		bool persistEnabled = HackDefaults.Instance?.PersistDownloadResume ?? true;
		DownloadCheckpointTracker? checkpoint = persistEnabled
			? new DownloadCheckpointTracker(
				_resumeStore,
				enabled: true,
				accountKey: CurrentAccountKey(),
				vaultPath: HackDefaults.Instance?.PwaPathAbsolute ?? string.Empty,
				operationKind: DownloadResumeKind.DownloadVersions,
				label: "Download Versions",
				existingState: new DownloadResumeState
				{
					TotalCount = requestedIds.Length,
					RemainingVersionIds = requestedIds,
				} )
			: null;
		checkpoint?.FlushSync();

		Action<OperationPauseState>? pauseHandler = BeginDownloadSession( pause, checkpoint, persistEnabled );

		try
		{
			List<HpVersion> failed = [];
			bool completed = await AsyncHelper.AsyncRunner( async () =>
			{
				if( pause is not null )
					await pause.WaitIfPausedAsync( cancellation.Token ).ConfigureAwait( false );

				HpVersion[] versions = await FetchVersionsByIdsChunkedAsync( requestedIds, pause, cancellation.Token ).ConfigureAwait( false );
				reporter.SetTotal( versions.Length );
				if( versions.Length == 0 )
				{
					checkpoint?.Clear();
					return;
				}

				checkpoint?.SetResolvedVersions( versions, versions.Length, skippedDelta: 0 );
				reporter.BeginDownloadTransfer();
				try
				{
					failed = await versions.DownloadAllAsync(
						cancellation.Token,
						pause: pause,
						onVersionCompleted: ( vid, bytes ) => checkpoint?.MarkVersionCompleted( vid, bytes ),
						onProgress: ( processed, skipped ) => reporter.Advance( processed, skipped ),
						onBytesDownloaded: bytes => reporter.AddDownloadedBytes( bytes ),
						log: Log ).ConfigureAwait( false );
				}
				finally
				{
					reporter.EndDownloadTransfer();
				}

				int[] failedIds = failed.Ids();
				if( failedIds.Length == 0 )
					checkpoint?.Clear();
				else if( checkpoint is { Enabled: true } )
					checkpoint.SaveFailedOnly( failedIds );
				else
					checkpoint?.Clear();
			}, "Download Versions", cancellation ).ConfigureAwait( false );

			if( !completed )
			{
				await HandleDownloadCancellationAsync( checkpoint ).ConfigureAwait( false );
				return [];
			}

			return failed.Ids();
		}
		catch( OperationCanceledException )
		{
			await HandleDownloadCancellationAsync( checkpoint ).ConfigureAwait( false );
			return [];
		}
		finally
		{
			EndDownloadSession( pause, pauseHandler, checkpoint );
		}
	}
}
