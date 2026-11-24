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
}

public sealed class PdmWorkflowService( IOperationReporter reporter, ILocalFileWatcher? localFiles = null ) : IPdmWorkflowService
{
	private const int RpcIdChunkSize = 10_000;
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

	private async Task<IReadOnlyList<int>> ExpandWithDependenciesAsync( IReadOnlyList<int> entryIds, CancellationToken token = default )
	{
		HashSet<int> allIds = [ .. entryIds ];
		string serverKey = OdooDefaults.Instance?.OdooUrl ?? "";
		bool useDirectEntryRpc = string.IsNullOrEmpty( serverKey ) || !NoDirectDependencyServerUrls.ContainsKey( serverKey );
		List<int> fallbackEntryIds = [];
		int totalChunks = ( entryIds.Count + RpcIdChunkSize - 1 ) / RpcIdChunkSize;
		int parallelism = RpcParallelism;

		if( useDirectEntryRpc && totalChunks > 0 )
		{
			token.ThrowIfCancellationRequested();
			IReadOnlyList<int> firstChunk = totalChunks == 1 ? entryIds : OdooRpc.SliceChunk( entryIds, 0, RpcIdChunkSize );
			Result<IReadOnlyList<int>> firstDirect = await DownloadOperations.GetAllEntriesAndDependenciesByEntriesAsync( firstChunk, token ).ConfigureAwait( false );
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

	public Task GetLatestAsync( IReadOnlyList<int> entryIds )
		=> GetLatestCoreAsync( entryIds, preExpandedIds: null );

	private async Task<IReadOnlyList<int>?> GetLatestCoreAsync( IReadOnlyList<int> entryIds, IReadOnlyList<int>? preExpandedIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return null;

		CancellationTokenSource cancellation = reporter.BeginOperation();
		using IDisposable? watcherPause = localFiles?.Suspend();
		try
		{
			IReadOnlyList<int>? ids = preExpandedIds;
			bool downloadSucceeded = false;
			bool completed = await AsyncHelper.AsyncRunner( async () =>
			{
				if( ids is null )
				{
					Log( StatusMessage.INFO, $"Finding Entry Dependencies for {entryIds.Count} entries..." );
					ids = await ExpandWithDependenciesAsync( entryIds, cancellation.Token ).ConfigureAwait( false );
				}
				downloadSucceeded = await DownloadLatestAsync( ids, cancellation.Token ).ConfigureAwait( false );
			}, "Get Latest", cancellation ).ConfigureAwait( false );
			if( !completed )
			{
				await reporter.NotifyAsync( "Cancelled Download" ).ConfigureAwait( false );
				return null;
			}
			return downloadSucceeded ? ids : null;
		}
		catch( OperationCanceledException )
		{
			await reporter.NotifyAsync( "Cancelled Download" ).ConfigureAwait( false );
			return null;
		}
		finally
		{
			reporter.Complete();
		}
	}

	private async Task<bool> DownloadLatestAsync( IReadOnlyList<int> entryIds, CancellationToken token )
	{
		Log( StatusMessage.FOUND, $"{entryIds.Count} entries" );
		Log( StatusMessage.PROCESSING, "Retrieving all latest versions associated with entries..." );

		HpVersion[]? versions = await DownloadOperations.GetLatestVersionsAsync(
			entryIds, excludedFields: null, token: token, includedFields: LatestVersionIncludedFields, log: Log ).ConfigureAwait( false );
		reporter.SetTotal( versions?.Length ?? 0 );

		if( versions is not { Length: > 0 } )
		{
			reporter.Complete();
			await reporter.NotifyAsync( "Cancelled Download... No Versions to Process" ).ConfigureAwait( false );
			return false;
		}

		Log( StatusMessage.INFO, $"Retrieved {versions.Length} version(s); checking local files..." );

		DownloadOperations.DownloadSummary summary = new();
		try
		{
			List<HpVersion> toDownload = await DownloadOperations.FilterVersionsToDownloadAsync(
				versions,
				token,
				summary,
				log: Log,
				onProgress: ( processed, skipped ) => reporter.Advance( processed, skipped ) ).ConfigureAwait( false );

			if( toDownload.Count > 0 )
			{
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
						controller: controller ).ConfigureAwait( false );
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

	public Task CheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: true, preExpandedIds: null );
	public Task UnCheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: false, preExpandedIds: null );

	public async Task CheckoutLatestAsync( IReadOnlyList<int> entryIds )
	{
		IReadOnlyList<int>? expanded = await GetLatestCoreAsync( entryIds, preExpandedIds: null ).ConfigureAwait( false );
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

		CancellationTokenSource cancellation = reporter.BeginOperation();
		using IDisposable? watcherPause = localFiles?.Suspend();
		try
		{
			bool completed = await AsyncHelper.AsyncRunner(
				() => CommitFilesAsync( files, cancellation.Token ),
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
			reporter.Complete();
		}
	}

	private async Task CommitFilesAsync( List<HackFile> files, CancellationToken token )
	{
		try
		{
			var (localOnly, remoteExisting, unchanged, remoteByServerName) =
				await CommitOperations.FilterAndSegmentCommitFilesAsync( files, Log, token ).ConfigureAwait( false );
			if( localOnly.Length == 0 && remoteExisting.Length == 0 )
			{
				if( unchanged.Length > 0 )
				{
					await CheckInUnchangedAsync( unchanged, token ).ConfigureAwait( false );
					reporter.Complete();
					await reporter.NotifyAsync( "Completed!" ).ConfigureAwait( false );
					return;
				}
				Log( StatusMessage.ERROR, "--- No entries to commit ---" );
				await reporter.NotifyAsync( "No eligible entries to commit (see the status log).", "Commit Files" ).ConfigureAwait( false );
				return;
			}

			HpPDMCommit commit = new() { node_by = OdooDefaults.Instance.MyNode?.id };
			await commit.CreateCommitAsync( token ).ConfigureAwait( false );

			reporter.SetTotal( localOnly.Length + remoteExisting.Length );
			Log( StatusMessage.PROCESSING, "--- Preparing to stage records ---" );

			Dictionary<string, HpDirectory> directoryCache = new( StringComparer.OrdinalIgnoreCase );
			await CommitOperations.PreResolveDirectoriesAsync( localOnly, directoryCache, token ).ConfigureAwait( false );

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
							batchCts.Token ).ConfigureAwait( false ) )
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
									ct ).ConfigureAwait( false ) )
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

			var (committed, error) = await commit.ServerCommit( token ).ConfigureAwait( false );
			if( !committed )
			{
				Log( StatusMessage.ERROR, $"Commit failed on the server: {error}" );
				await reporter.NotifyAsync( $"Commit failed: {error}", "Commit Files" ).ConfigureAwait( false );
				return;
			}

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

	public Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds )
		=> Task.Run<IReadOnlyList<int>>( async () =>
		{
			HpVersion[] versions = await HpVersion.GetRecordsByIdsAsync( versionIds, excludedFields: HpVersion.UsualExcludedFields ).ConfigureAwait( false ) ?? [];
			return ( await versions.DownloadAllAsync().ConfigureAwait( false ) ).Ids();
		} );
}
