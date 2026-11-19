using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;
using HackPDM.Core.SolidWorks;

namespace HackPDM.Core.Services;

public sealed record VersionDownloadPlan( HpVersion Version, bool IsLatest, IReadOnlyList<int> VersionIds );

/// <summary>Orchestrates core PDM workflows (get latest, checkout, commit, delete, restore, historical versions).</summary>
public interface IPdmWorkflowService
{
	void PreloadMetadata();
	Task<IReadOnlyList<int>> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false );
	Task GetLatestAsync( IReadOnlyList<int> entryIds );
	Task CheckoutAsync( IReadOnlyList<int> entryIds );
	Task CheckoutLatestAsync( IReadOnlyList<int> entryIds );
	Task UnCheckoutAsync( IReadOnlyList<int> entryIds );
	Task CommitAsync( IEnumerable<HackFile> hackFiles );
	Task<HashSet<HackFile>> CollectCommitFilesAsync( IEnumerable<EntryRow> entries );
	Task<List<HackFile>> CollectFolderCommitFilesAsync( string folderPath );
	Task LogicalDeleteAsync( IReadOnlyList<int> entryIds );
	Task RestoreDeletedInDirectoryAsync( int directoryId, bool withSubdirectories = false );
	Task<bool> IsCurrentUserPdmAdminAsync();
	Task<bool> PermanentDeleteAsync( IReadOnlyList<int> entryIds );
	Task<VersionDownloadPlan?> PlanVersionDownloadAsync( int versionId );
	Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds );
}

public sealed class PdmWorkflowService( IOperationReporter reporter ) : IPdmWorkflowService
{
	private const int RpcIdChunkSize = 500;
	private static readonly string[] HeavyEntryFields = [ nameof( HpEntry.type_id ), nameof( HpEntry.cat_id ) ];
	private static readonly string[] DeleteExcludedFields = [ nameof( HpEntry.type_id ), nameof( HpEntry.cat_id ), nameof( HpEntry.checkout_node ) ];
	private static readonly string[] LatestVersionIncludedFields =
	[
		nameof( HpVersion.id ),
		nameof( HpVersion.name ),
		nameof( HpVersion.checksum ),
		nameof( HpVersion.file_size ),
		nameof( HpVersion.windows_complete_name ),
		nameof( HpVersion.dir_id ),
		nameof( HpVersion.checkout_user ),
	];

	private void Log( StatusMessage status, string message ) => reporter.Log( status, message );

	public void PreloadMetadata() { }

	public async Task<IReadOnlyList<int>> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false )
	{
		Result<IReadOnlyList<int>> ids = await HpDirectory.GetDirectoryEntryIDsAsync( directoryId, withSubdirectories, withDeleted );
		if( !ids.Ok )
			Log( StatusMessage.ERROR, $"Could not list the directory's entries: {ids.Error}" );
		return ids.ValueOr( [] );
	}

	private async Task<IReadOnlyList<int>> ExpandWithDependenciesAsync( IReadOnlyList<int> entryIds, CancellationToken token = default )
	{
		HashSet<int> allIds = [ .. entryIds ];
		bool useDirectEntryRpc = true;
		List<int> fallbackEntryIds = [];

		for( int offset = 0; offset < entryIds.Count; offset += RpcIdChunkSize )
		{
			token.ThrowIfCancellationRequested();
			int[] chunk = OdooRpc.SliceChunk( entryIds, offset, RpcIdChunkSize );

			if( useDirectEntryRpc )
			{
				Result<IReadOnlyList<int>> direct = await DownloadOperations.GetAllEntriesAndDependenciesByEntriesAsync( chunk, token );
				if( direct.Ok )
				{
					if( direct.Value is { Count: > 0 } depIds )
						allIds.UnionWith( depIds );
					if( entryIds.Count > RpcIdChunkSize )
						Log( StatusMessage.PROCESSING, $"Checked dependencies for {Math.Min( offset + chunk.Length, entryIds.Count )}/{entryIds.Count} entries..." );
					continue;
				}
				useDirectEntryRpc = false;
			}
			fallbackEntryIds.AddRange( chunk );
		}

		if( fallbackEntryIds.Count == 0 )
			return [ .. allIds ];

		List<int> versionIds = [];
		for( int offset = 0; offset < fallbackEntryIds.Count; offset += RpcIdChunkSize )
		{
			token.ThrowIfCancellationRequested();
			int[] chunk = OdooRpc.SliceChunk( fallbackEntryIds, offset, RpcIdChunkSize );
			HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( chunk, includedFields: [ nameof( HpEntry.latest_version_id ) ] );
			foreach( HpEntry e in entries ?? [] )
			{
				if( e.latest_version_id?.id is int vid and > 0 )
					versionIds.Add( vid );
			}
		}

		for( int offset = 0; offset < versionIds.Count; offset += RpcIdChunkSize )
		{
			token.ThrowIfCancellationRequested();
			int[] chunk = OdooRpc.SliceChunk( versionIds, offset, RpcIdChunkSize );
			Result<IReadOnlyList<int>> dependencies = await DownloadOperations.GetAllEntriesAndDependenciesListAsync( chunk, token );
			if( !dependencies.Ok )
				Log( StatusMessage.WARNING, $"Dependencies could not be listed: {dependencies.Error}" );
			else if( dependencies.Value is { Count: > 0 } depIds )
				allIds.UnionWith( depIds );
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
		try
		{
			IReadOnlyList<int> ids;
			if( preExpandedIds is not null )
			{
				ids = preExpandedIds;
			}
			else
			{
				Log( StatusMessage.INFO, $"Finding Entry Dependencies for {entryIds.Count} entries..." );
				ids = await ExpandWithDependenciesAsync( entryIds, cancellation.Token );
			}
			await AsyncHelper.AsyncRunner( () => DownloadLatestAsync( ids, cancellation.Token ), "Get Latest", cancellation );
			return ids;
		}
		catch( OperationCanceledException )
		{
			await reporter.NotifyAsync( "Cancelled Download" );
			return null;
		}
		finally
		{
			reporter.Complete();
		}
	}

	private async Task DownloadLatestAsync( IReadOnlyList<int> entryIds, CancellationToken token )
	{
		Log( StatusMessage.FOUND, $"{entryIds.Count} entries" );
		Log( StatusMessage.PROCESSING, "Retrieving all latest versions associated with entries..." );

		HpVersion[]? versions = await DownloadOperations.GetLatestVersionsAsync(
			entryIds, excludedFields: null, token: token, includedFields: LatestVersionIncludedFields, log: Log );
		long effectiveBatchBytes = DownloadOperations.EffectiveBatchBytes();
		List<List<HpVersion>>? batches = versions is null
			? null
			: DownloadOperations.BatchBySize( versions, OdooDefaults.Instance.DownloadBatchSize, effectiveBatchBytes );
		reporter.SetTotal( versions?.Length ?? 0 );

		if( batches is not { Count: > 0 } || versions is null )
		{
			reporter.Complete();
			await reporter.NotifyAsync( "Cancelled Download... No Versions to Process" );
			return;
		}

		Log( StatusMessage.INFO, $"Prepared {versions.Length} version(s) in {batches.Count} batch(es); comparing local files and downloading..." );

		DownloadOperations.DownloadSummary? summary = null;
		try
		{
			summary = await DownloadOperations.ProcessDownloadsAsync(
				batches,
				token,
				OdooDefaults.Instance.ConcurrencySize,
				log: Log,
				onProgress: ( processed, skipped ) => reporter.Advance( processed, skipped ),
				onBytesDownloaded: bytes => reporter.AddDownloadedBytes( bytes ) );
		}
		catch( OperationCanceledException )
		{
			await reporter.NotifyAsync( "Cancelled Download" );
		}
		catch( Exception e )
		{
			Log( StatusMessage.ERROR, $"download failed: {e}" );
			await reporter.NotifyAsync( $"Download failed: {e.Message}", "Get Latest" );
		}

		reporter.Complete();
		if( summary is null )
			return;

		string kept = summary.KeptLocalChanges.IsEmpty
			? ""
			: $"\n{summary.KeptLocalChanges.Count} file(s) with local changes were kept, not overwritten:\n"
				+ string.Join( "\n", summary.KeptLocalChanges.Take( 15 ) );
		if( summary.Failed.IsEmpty )
		{
			await reporter.NotifyAsync( $"Completed! {summary.Downloaded} downloaded, {summary.Skipped - summary.KeptLocalChanges.Count} already up to date.{kept}" );
			return;
		}

		string[] failed = [ .. summary.Failed ];
		await reporter.NotifyAsync(
			$"Completed with {failed.Length} file(s) NOT downloaded ({summary.Downloaded} downloaded, {summary.Skipped} up to date):\n"
				+ string.Join( "\n", failed.Take( 15 ) )
				+ ( failed.Length > 15 ? $"\n...and {failed.Length - 15} more (see the status log)" : "" ) + kept,
			"Get Latest" );
	}

	public Task CheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: true, preExpandedIds: null );
	public Task UnCheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: false, preExpandedIds: null );

	public async Task CheckoutLatestAsync( IReadOnlyList<int> entryIds )
	{
		IReadOnlyList<int>? expanded = await GetLatestCoreAsync( entryIds, preExpandedIds: null );
		if( expanded is { Count: > 0 } )
			await ChangeCheckoutAsync( entryIds, checkout: true, preExpandedIds: expanded );
	}

	private async Task ChangeCheckoutAsync( IReadOnlyList<int> entryIds, bool checkout, IReadOnlyList<int>? preExpandedIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		CancellationTokenSource cancellation = reporter.BeginOperation();
		try
		{
			int me = OdooDefaults.Instance.OdooId;
			IReadOnlyList<int> ids = preExpandedIds ?? await ExpandWithDependenciesAsync( entryIds, cancellation.Token );
			HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(
				ids,
				checkout ? null : OdooDomain.Where( nameof( HpEntry.checkout_user ), Operators.Equal, me ),
				excludedFields: HeavyEntryFields );

			if( !checkout )
			{
				HpEntry[]? others = await HpEntry.GetRecordsByIdsAsync(
					entryIds,
					OdooDomain.Where( nameof( HpEntry.checkout_user ), Operators.NotEqual, me ).And( nameof( HpEntry.checkout_user ), Operators.NotEqual, false ),
					includedFields: [ nameof( HpEntry.id ), nameof( HpEntry.name ), nameof( HpEntry.checkout_user ) ] );
				foreach( HpEntry other in others ?? [] )
					Log( StatusMessage.ERROR, $"Cannot undo the check out of {other.name}: it is checked out to {other.checkout_user?.name ?? "another user"}" );
			}
			if( entries is null or { Length: < 1 } )
				return;

			await AsyncHelper.AsyncRunner( async () =>
			{
				reporter.SetTotal( entries.Length );
				if( checkout )
					await CheckoutOperations.CheckOutEntriesAsync( entries, Log, OnCheckoutProgress, cancellation.Token );
				else
					await CheckoutOperations.UnCheckOutEntriesAsync( entries, me, Log, OnCheckoutProgress, cancellation.Token );
				reporter.Complete();
				await reporter.NotifyAsync( "Completed!" );
			}, checkout ? "Checkout Files" : "UnCheckout Files", cancellation );
		}
		catch( OperationCanceledException )
		{
			await reporter.NotifyAsync( checkout ? "Cancelled Checkout" : "Cancelled UnCheckout" );
		}
		finally
		{
			reporter.Complete();
		}
	}

	private void OnCheckoutProgress( int processed, int total )
	{
		reporter.SetTotal( total );
		reporter.SetProcessed( processed );
	}

	public Task CommitAsync( IEnumerable<HackFile> hackFiles )
	{
		List<HackFile> files = [ .. hackFiles ];
		return AsyncHelper.AsyncRunner( () => CommitFilesAsync( files ), "Commit Files" );
	}

	private async Task CommitFilesAsync( List<HackFile> files )
	{
		if( files.Count == 0 )
			return;

		CancellationTokenSource cancellation = reporter.BeginOperation();
		try
		{
			var (localOnly, remoteExisting, unchanged, remoteByServerName) =
				await CommitOperations.FilterAndSegmentCommitFilesAsync( files, Log, cancellation.Token );
			if( localOnly.Length == 0 && remoteExisting.Length == 0 )
			{
				if( unchanged.Length > 0 )
				{
					await CheckInUnchangedAsync( unchanged, cancellation.Token );
					reporter.Complete();
					await reporter.NotifyAsync( "Completed!" );
					return;
				}
				Log( StatusMessage.ERROR, "--- No entries to commit ---" );
				return;
			}

			HpPDMCommit commit = new() { node_by = OdooDefaults.Instance.MyNode?.id };
			await commit.CreateCommitAsync();

			reporter.SetTotal( localOnly.Length + remoteExisting.Length );
			Log( StatusMessage.PROCESSING, "--- Preparing to stage records ---" );

			Dictionary<string, HpDirectory> directoryCache = new( StringComparer.OrdinalIgnoreCase );
			var staged = localOnly.Select( f => ( file: f, inOdoo: false ) )
				.Concat( remoteExisting.Select( f => ( file: f, inOdoo: true ) ) );
			foreach( var (file, inOdoo) in staged )
			{
				if( !await CommitOperations.CommitRecordAsync(
					file,
					commit,
					inOdoo,
					Log,
					() => reporter.Advance( 1 ),
					bytes => reporter.AddUploadedBytes( bytes ),
					directoryCache,
					remoteByServerName,
					cancellation.Token ) )
					return;
			}

			var (committed, error) = await commit.ServerCommit();
			if( !committed )
			{
				Log( StatusMessage.ERROR, $"Commit failed on the server: {error}" );
				await reporter.NotifyAsync( $"Commit failed: {error}", "Commit Files" );
				return;
			}

			await CheckInUnchangedAsync( unchanged, cancellation.Token );
			reporter.Complete();
			await reporter.NotifyAsync( "Completed!" );
		}
		catch( OperationCanceledException )
		{
			await reporter.NotifyAsync( "Cancelled Commit" );
		}
		finally
		{
			reporter.Complete();
		}
	}

	private async Task CheckInUnchangedAsync( HpEntry[] unchanged, CancellationToken token = default )
	{
		if( unchanged.Length == 0 )
			return;
		Log( StatusMessage.INFO, $"{unchanged.Length} unchanged file(s): checked in without a new version" );
		await CheckoutOperations.UnCheckOutEntriesAsync( unchanged, OdooDefaults.Instance.OdooId, Log, token: token );
	}

	public async Task<HashSet<HackFile>> CollectCommitFilesAsync( IEnumerable<EntryRow> entries )
	{
		Log( StatusMessage.PROCESSING, "checking entries and dependencies" );
		HashSet<HackFile> hackFiles = [];
		bool restrictTypes = OdooDefaults.Instance.RestrictTypes is true;
		foreach( EntryRow item in entries )
		{
			if( string.IsNullOrEmpty( item.FullName ) )
				continue;
			if( restrictTypes && string.IsNullOrEmpty( item.Type ) )
				continue;

			if( SwDocumentTypes.IsSolidWorksExtension( item.Type ) )
			{
				var (success, files, broken) = await DependencyScanner.GetFileWithDependenciesAsync( item );
				if( success )
				{
					foreach( HackFile f in files )
						hackFiles.Add( f );
					continue;
				}

				foreach( BrokenDependency b in broken )
					Log( StatusMessage.ERROR, $"({b.Reason}) file: {b.FullPath}" );
				Log( StatusMessage.ERROR, "Commit Terminated" );
			}
			else
			{
				string? localPath = item.ReprType is EntryReprType.Local
					? item.FullName
					: PwaPaths.ToAbsolute( item.FullName );
				if( HackFile.GetFromPath( localPath ) is { Exists: true } hack )
					hackFiles.Add( hack );
			}
		}

		if( hackFiles.Count == 0 )
			Log( StatusMessage.INFO, "no valid entries to process" );
		return hackFiles;
	}

	public async Task<List<HackFile>> CollectFolderCommitFilesAsync( string folderPath )
	{
		var (files, broken) = await DependencyScanner.GetFolderWithDependenciesAsync( folderPath );
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
			HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( entryIds, excludedFields: DeleteExcludedFields );
			await AsyncHelper.AsyncRunner( () => DeleteOperations.SetDeletedAsync( entries, true, Log, cancellation.Token ), "Logically Delete Files", cancellation );
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
				IReadOnlyList<int> ids = await GetDirectoryEntryIdsAsync( directoryId, withSubdirectories: true, withDeleted: true );
				if( ids.Count == 0 )
					return;
				entries = await HpEntry.GetRecordsByIdsAsync(
					ids,
					searchFilters: OdooDomain.Where( nameof( HpEntry.deleted ), true ),
					excludedFields: DeleteExcludedFields );
			}
			else
			{
				entries = await HpEntry.GetRecordsByIdsAsync(
					null,
					searchFilters: OdooDomain.Where( nameof( HpEntry.deleted ), true ).And( nameof( HpEntry.dir_id ), directoryId ),
					excludedFields: DeleteExcludedFields );
			}

			if( entries is not { Length: > 0 } )
				return;

			await AsyncHelper.AsyncRunner( () => DeleteOperations.SetDeletedAsync( entries, false, Log, cancellation.Token ), "Logically UnDelete Files", cancellation );
		}
		finally
		{
			reporter.Complete();
		}
	}

	public async Task<bool> IsCurrentUserPdmAdminAsync()
	{
		Result<bool> result = await OdooRpc.ExecuteAsync<bool>( "res.users", "has_group", [ "hackpdm.group_pdm_admin" ] );
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
				deleted = await DeleteOperations.PermanentDeleteEntry( entryIds, Log );
				if( deleted )
					Log( StatusMessage.SUCCESS, "Completed permanent delete" );
			}, "Permanently Delete Files", cancellation );
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

		HpVersion? version = ( await HpVersion.GetRecordsByIdsAsync( [ versionId ], excludedFields: HpVersion.UsualExcludedFields ) )?.FirstOrDefault();
		if( version is null )
			return null;

		HpEntry? entry = version.entry_id?.id is int entryId ? await HpEntry.GetRecordByIdAsync( entryId ) : null;
		Result<IReadOnlyList<int>> dependencies = await DownloadOperations.GetVersionListAsync( versionId );
		if( !dependencies.Ok )
			Log( StatusMessage.WARNING, $"Dependencies of version {versionId} could not be listed: {dependencies.Error}" );
		IReadOnlyList<int> versionIds = [ .. dependencies.ValueOr( [] ).Append( versionId ).Distinct() ];

		return new VersionDownloadPlan( version, entry?.latest_version_id == versionId, versionIds );
	}

	public async Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds )
	{
		HpVersion[] versions = await HpVersion.GetRecordsByIdsAsync( versionIds, excludedFields: HpVersion.UsualExcludedFields ) ?? [];
		return ( await versions.DownloadAllAsync() ).Ids();
	}
}
