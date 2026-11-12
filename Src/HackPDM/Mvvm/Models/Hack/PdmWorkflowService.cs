using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Infrastructure.Hack;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.Operations;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Types;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Mvvm.Models.Common;

namespace HackPDM.Mvvm.Models.Hack;

/// <summary>What will be downloaded when the user asks for a specific historical version.</summary>
/// <param name="Version">The requested version record.</param>
/// <param name="IsLatest">Whether <paramref name="Version"/> is its entry's latest version.</param>
/// <param name="VersionIds">The requested version id plus the ids of every version it depends on, each once.</param>
public sealed record VersionDownloadPlan( HpVersion Version, bool IsLatest, IReadOnlyList<int> VersionIds );

/// <summary>
/// PDM workflows (get latest, checkout, commit, delete, restore). Progress, log lines and the
/// final message go through <see cref="IOperationReporter"/>; no UI types or global view models.
/// </summary>
public interface IPdmWorkflowService
{
	/// <summary>Loads the Odoo lookup tables (nodes, settings, types, users...) the file manager needs.</summary>
	/// <remarks>
	/// Touches each lazily loaded property of <see cref="HackPDM.Infrastructure.Odoo.OdooDefaults.Instance"/> so the
	/// server reads happen now instead of on first use. Runs synchronously on the calling thread, so call it off the UI thread.
	/// </remarks>
	void PreloadMetadata();

	/// <summary>Lists the ids of the entries stored in a directory.</summary>
	/// <param name="directoryId">The Odoo id of the directory.</param>
	/// <param name="withSubdirectories">Whether entries in all subdirectories are included as well.</param>
	/// <param name="withDeleted">Whether logically deleted entries are included.</param>
	/// <returns>The entry ids; empty when the server call failed.</returns>
	/// <remarks>A failed server call is logged as an error to the status window instead of being thrown.
	/// Calls <see cref="HackPDM.Infrastructure.Odoo.Models.HpDirectory.GetDirectoryEntryIDsAsync(int, bool, bool)"/>.</remarks>
	Task<IReadOnlyList<int>> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false );

	/// <summary>Downloads the latest version of each entry and everything it depends on.</summary>
	/// <param name="entryIds">The entries to download; nothing happens when null or empty.</param>
	/// <remarks>
	/// Starts a cancellable operation on the reporter, expands the list with every dependency of the entries'
	/// latest versions (<see cref="HackPDM.Infrastructure.Operations.PdmDownloadOperations.GetAllEntriesAndDependenciesListAsync"/>),
	/// then downloads in size-limited batches through
	/// <see cref="HackPDM.Infrastructure.Operations.PdmDownloadOperations.ProcessDownloadsAsync"/>. Progress is reported per file;
	/// the final message lists files that failed to download and local copies with changes that were kept rather than overwritten.
	/// Download errors and cancellation are reported, not thrown.
	/// </remarks>
	Task GetLatestAsync( IReadOnlyList<int> entryIds );

	/// <summary>Checks the entries, and every entry their latest versions depend on, out to this user and node.</summary>
	/// <param name="entryIds">The entries to check out; nothing happens when null or empty.</param>
	/// <remarks>
	/// Does not download anything first (see <see cref="CheckoutLatestAsync"/>). Entries that are deleted or held by another
	/// user are skipped and logged as errors. Calls <see cref="HackPDM.Infrastructure.Operations.PdmCheckoutOperations.CheckOutEntriesAsync"/>,
	/// which makes the local copies writable, and shows "Completed!" at the end.
	/// </remarks>
	Task CheckoutAsync( IReadOnlyList<int> entryIds );

	/// <summary>Gets the latest of each entry (so the local copies are current), then checks them out.</summary>
	/// <param name="entryIds">The entries to download and check out.</param>
	/// <remarks>Runs <see cref="GetLatestAsync"/> to completion, then <see cref="CheckoutAsync"/>.</remarks>
	Task CheckoutLatestAsync( IReadOnlyList<int> entryIds );

	/// <summary>Releases this user's checkout of the entries and of every entry they depend on.</summary>
	/// <param name="entryIds">The entries to release; nothing happens when null or empty.</param>
	/// <remarks>
	/// Only entries checked out to the current user are released, through
	/// <see cref="HackPDM.Infrastructure.Operations.PdmCheckoutOperations.UnCheckOutEntriesAsync"/>; selected entries held by
	/// another user are logged as errors and left alone. Local changes are not reverted.
	/// </remarks>
	Task UnCheckoutAsync( IReadOnlyList<int> entryIds );

	/// <summary>Commits local files to the server as new entries or new versions in a single PDM commit.</summary>
	/// <param name="hackFiles">The local files to commit, usually from <see cref="CollectCommitFilesAsync"/> or <see cref="CollectFolderCommitFilesAsync"/>.</param>
	/// <remarks>
	/// Files are sorted by <see cref="HackPDM.Infrastructure.Operations.PdmCommitOperations.FilterCommitHackFilesAsync"/> into new files,
	/// changed files Odoo already knows and unchanged checked-out files. A <see cref="HackPDM.Infrastructure.Odoo.Models.HpPDMCommit"/>
	/// is created, each changed file is staged with <see cref="HackPDM.Infrastructure.Operations.PdmCommitOperations.CommitRecordAsync"/>
	/// (stopping at the first failure), and the commit is finalized with
	/// <see cref="HackPDM.Infrastructure.Odoo.Models.HpPDMCommit.ServerCommit"/>. Unchanged checked-out files are checked back in
	/// without a new version. A server-side commit failure is logged and shown to the user rather than thrown.
	/// </remarks>
	Task CommitAsync( IEnumerable<HackFile> hackFiles );

	/// <summary>Turns selected entry rows into local files to commit, pulling in SolidWorks dependencies.</summary>
	/// <param name="entries">The selected rows; rows without a local path or file type are skipped.</param>
	/// <returns>The existing local files to commit, each once; empty when none qualified.</returns>
	/// <remarks>
	/// SolidWorks files are expanded with every file they reference through
	/// <see cref="HackPDM.Infrastructure.Hack.DependencyScanner"/>. A SolidWorks file with missing references is left out
	/// (its broken references are logged as errors) and the remaining rows are still processed. Other files are included only
	/// when they exist on disk.
	/// </remarks>
	Task<HashSet<HackFile>> CollectCommitFilesAsync( IEnumerable<EntryRow> entries );

	/// <summary>Collects every local file directly in a folder, plus everything its SolidWorks files reference.</summary>
	/// <param name="folderPath">The local folder; subfolders are not scanned.</param>
	/// <returns>The files to commit, each once.</returns>
	/// <remarks>
	/// Names the server refuses (such as SolidWorks "~$" lock files) are skipped. SolidWorks files with missing references are
	/// left out and their broken references logged. Uses
	/// <see cref="HackPDM.Infrastructure.Hack.DependencyScanner.GetHackFolderWithDependenciesAsync"/>.
	/// </remarks>
	/// <exception cref="System.IO.DirectoryNotFoundException"><paramref name="folderPath"/> does not exist.</exception>
	Task<List<HackFile>> CollectFolderCommitFilesAsync( string folderPath );

	/// <summary>Marks the entries deleted on the server (a logical delete that can be restored).</summary>
	/// <param name="entryIds">The entries to delete; nothing happens when null or empty.</param>
	/// <remarks>
	/// Only the selected entries are deleted, never their dependencies, since those are often shared with other entries.
	/// Uses <see cref="HackPDM.Infrastructure.Operations.PdmDeleteOperations.SetDeletedAsync"/>; entries the server refuses are logged.
	/// </remarks>
	Task LogicalDeleteAsync( IReadOnlyList<int> entryIds );

	/// <summary>Restores every logically deleted entry directly in a directory.</summary>
	/// <param name="directoryId">The Odoo id of the directory; subdirectories are not included.</param>
	/// <remarks>Uses <see cref="HackPDM.Infrastructure.Operations.PdmDeleteOperations.SetDeletedAsync"/> and marks the status window's progress complete.</remarks>
	Task RestoreDeletedInDirectoryAsync( int directoryId );

	/// <summary>Permanently removes entries from the server, with all their versions and attachments.</summary>
	/// <param name="entryIds">The entries to remove.</param>
	/// <returns><see langword="true"/> when the entries, versions and attachments were all removed; <see langword="false"/> when the list was empty or any step failed.</returns>
	/// <remarks>This cannot be undone. Uses <see cref="HackPDM.Infrastructure.Operations.PdmDeleteOperations.PermanentDeleteEntry"/>
	/// and logs a success line when it completes.</remarks>
	Task<bool> PermanentDeleteAsync( IReadOnlyList<int> entryIds );

	/// <summary>Works out what downloading a historical version would involve, without downloading anything.</summary>
	/// <param name="versionId">The Odoo id of the version.</param>
	/// <returns>The version, whether it is its entry's latest, and the version ids to download (it plus its dependencies);
	/// <see langword="null"/> when <paramref name="versionId"/> is 0 or the version cannot be read.</returns>
	/// <remarks>
	/// Dependencies come from <see cref="HackPDM.Infrastructure.Operations.PdmDownloadOperations.GetVersionListAsync"/>; when that
	/// call fails a warning is logged and the plan holds only the requested version.
	/// </remarks>
	Task<VersionDownloadPlan?> PlanVersionDownloadAsync( int versionId );

	/// <summary>Downloads the given versions into their local folders.</summary>
	/// <param name="versionIds">The Odoo ids of the versions to download, typically <see cref="VersionDownloadPlan.VersionIds"/>.</param>
	/// <returns>The ids of the versions that failed to download; empty when all succeeded.</returns>
	/// <remarks>Downloads one version at a time with <see cref="HackPDM.Infrastructure.Odoo.VersionFileService.DownloadAllAsync"/>
	/// and does not report progress to the status window.</remarks>
	Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds );
}

/// <summary>
/// <see cref="IPdmWorkflowService"/> backed by the Infrastructure PDM operations, reporting through an
/// <see cref="IOperationReporter"/>.
/// </summary>
/// <param name="reporter">Receives log lines, progress and the final message of each workflow.</param>
public sealed class PdmWorkflowService( IOperationReporter reporter ) : IPdmWorkflowService
{
	private static readonly string[] HeavyEntryFields = [ "type_id", "cat_id" ];
	private static readonly string[] DeleteExcludedFields = [ "type_id", "cat_id", "checkout_node" ];
	private static readonly string[] LatestVersionExcludedFields = [ "preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents" ];

	private void Log( StatusMessage status, string message ) => reporter.Log( status, message );

	/// <inheritdoc/>
	public void PreloadMetadata()
	{
		// each property loads lazily on first access
		var defaults = OdooDefaults.Instance;
		_ = defaults?.HpNodes;
		_ = defaults?.MyNode;
		_ = defaults?.HpSettings;
		_ = defaults?.HpDirectoryRoot;
		_ = defaults?.ExtToFilter;
		_ = defaults?.ExtToType;
		_ = defaults?.ExtToProp;
		_ = defaults?.ExtToCat;
		_ = defaults?.IdToProp;
		_ = defaults?.IdToUser;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<int>> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false )
	{
		Result<IReadOnlyList<int>> ids = await HpDirectory.GetDirectoryEntryIDsAsync( directoryId, withSubdirectories, withDeleted );
		if( !ids.Ok )
			Log( StatusMessage.ERROR, $"Could not list the directory's entries: {ids.Error}" );
		return ids.ValueOr( [] );
	}

	/// <summary>Adds every entry the given entries' latest versions depend on, without duplicates.</summary>
	private async Task<IReadOnlyList<int>> ExpandWithDependenciesAsync( IReadOnlyList<int> entryIds )
	{
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( entryIds, includedFields: [ nameof( HpEntry.latest_version_id ) ] );
		Result<IReadOnlyList<int>> dependencies = await PdmDownloadOperations.GetAllEntriesAndDependenciesListAsync(
			[ .. entries?.Select( e => e.latest_version_id?.id ?? 0 ) ?? [] ] );
		if( !dependencies.Ok )
			Log( StatusMessage.WARNING, $"Dependencies could not be listed: {dependencies.Error}" );

		return [ .. dependencies.ValueOr( [] ).Concat( entryIds ).Distinct() ];
	}

	#region Get latest

	/// <inheritdoc/>
	public async Task GetLatestAsync( IReadOnlyList<int> entryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		CancellationTokenSource cancellation = reporter.BeginOperation();
		Log( StatusMessage.INFO, "Finding Entry Dependencies..." );
		IReadOnlyList<int> ids = await ExpandWithDependenciesAsync( entryIds );

		await AsyncHelper.AsyncRunner( () => DownloadLatestAsync( ids, cancellation.Token ), "Get Latest", cancellation );
	}

	private async Task DownloadLatestAsync( IReadOnlyList<int> entryIds, CancellationToken token )
	{
		Log( StatusMessage.FOUND, $"{entryIds.Count} entries" );
		Log( StatusMessage.PROCESSING, "Retrieving all latest versions associated with entries..." );

		HpVersion[]? versions = await PdmDownloadOperations.GetLatestVersionsAsync( entryIds, LatestVersionExcludedFields );
		// batches are limited by total file size as well as count (see PdmDownloadOperations.MaxBatchBytes)
		List<List<HpVersion>>? batches = versions is null ? null : PdmDownloadOperations.BatchBySize( versions, OdooDefaults.Instance.DownloadBatchSize );
		reporter.SetTotal( versions?.Length ?? 0 );

		if( batches is not { Count: > 0 } || versions is null )
		{
			reporter.Complete();
			await reporter.NotifyAsync( "Cancelled Download... No Versions to Process" );
			return;
		}

		PdmDownloadOperations.DownloadSummary? summary = null;
		try
		{
			summary = await PdmDownloadOperations.ProcessDownloadsAsync(
				batches,
				token,
				OdooDefaults.Instance.ConcurrencySize,
				log: Log,
				onProgress: ( processed, skipped ) => reporter.Advance( processed, skipped ) );
		}
		catch( OperationCanceledException )
		{
			await reporter.NotifyAsync( "Cancelled Download" );
		}
		catch( Exception e )
		{
			// a failure is not a cancellation: say what went wrong
			Log( StatusMessage.ERROR, $"download failed: {e}" );
			await reporter.NotifyAsync( $"Download failed: {e.Message}", "Get Latest" );
		}

		reporter.Complete();
		if( summary is null )
			return;

		string kept = summary.KeptLocalChanges.IsEmpty ? "" :
			$"\n{summary.KeptLocalChanges.Count} file(s) with local changes were kept, not overwritten:\n"
			+ string.Join( "\n", summary.KeptLocalChanges.Take( 15 ) );
		if( summary.Failed.IsEmpty )
		{
			await reporter.NotifyAsync( $"Completed! {summary.Downloaded} downloaded, {summary.Skipped - summary.KeptLocalChanges.Count} already up to date.{kept}" );
			return;
		}
		// never report success when files are missing
		var failed = summary.Failed.ToArray();
		await reporter.NotifyAsync(
			$"Completed with {failed.Length} file(s) NOT downloaded ({summary.Downloaded} downloaded, {summary.Skipped} up to date):\n"
			+ string.Join( "\n", failed.Take( 15 ) )
			+ ( failed.Length > 15 ? $"\n...and {failed.Length - 15} more (see the status log)" : "" ) + kept,
			"Get Latest" );
	}

	#endregion
	#region Checkout

	/// <inheritdoc/>
	public Task CheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: true );

	/// <inheritdoc/>
	public Task UnCheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: false );

	/// <inheritdoc/>
	public async Task CheckoutLatestAsync( IReadOnlyList<int> entryIds )
	{
		await GetLatestAsync( entryIds );
		await CheckoutAsync( entryIds );
	}

	private async Task ChangeCheckoutAsync( IReadOnlyList<int> entryIds, bool checkout )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		int me = OdooDefaults.Instance.OdooId;
		IReadOnlyList<int> ids = await ExpandWithDependenciesAsync( entryIds );
		// only entries checked out to this user are released
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( ids,
			checkout ? null : OdooDomain.Where( "checkout_user", Operators.Equal, me ), excludedFields: HeavyEntryFields );
		if( !checkout )
		{
			// the selected files someone else holds are left alone; say so rather than a bare "Completed!"
			HpEntry[]? others = await HpEntry.GetRecordsByIdsAsync( entryIds,
				OdooDomain.Where( "checkout_user", Operators.NotEqual, me ).And( "checkout_user", Operators.NotEqual, false ),
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
				await PdmCheckoutOperations.CheckOutEntriesAsync( entries, Log, OnCheckoutProgress );
			else
				await PdmCheckoutOperations.UnCheckOutEntriesAsync( entries, me, Log, OnCheckoutProgress );
			reporter.Complete();
			await reporter.NotifyAsync( "Completed!" );
		}, checkout ? "Checkout Files" : "UnCheckout Files" );
	}

	private void OnCheckoutProgress( int processed, int total )
	{
		reporter.SetTotal( total );
		reporter.SetProcessed( processed );
	}

	#endregion
	#region Commit

	/// <inheritdoc/>
	public Task CommitAsync( IEnumerable<HackFile> hackFiles )
	{
		List<HackFile> files = [ .. hackFiles ];
		return AsyncHelper.AsyncRunner( () => CommitFilesAsync( files ), "Commit Files" );
	}

	private async Task CommitFilesAsync( List<HackFile> files )
	{
		if( files.Count == 0 )
			return;

		var (localOnly, remoteExisting, unchanged) = await PdmCommitOperations.FilterCommitHackFilesAsync( files, Log );
		if( localOnly is null or [] && remoteExisting is null or [] )
		{
			if( unchanged.Length > 0 )
			{
				// nothing changed, but the files were checked out: committing them checks them back in
				await CheckInUnchangedAsync( unchanged );
				await reporter.NotifyAsync( "Completed!" );
				return;
			}
			Log( StatusMessage.ERROR, "--- No entries to commit ---" );
			return;
		}

		HpPDMCommit commit = new() { node_by = OdooDefaults.Instance?.MyNode?.id };
		await commit.CreateCommitAsync();

		reporter.BeginOperation();
		reporter.SetTotal( ( localOnly?.Length ?? 0 ) + ( remoteExisting?.Length ?? 0 ) );
		Log( StatusMessage.PROCESSING, "--- Preparing to stage records ---" );

		// new files first, then new versions of files Odoo already knows
		var staged = ( localOnly ?? [] ).Select( f => (file: f, inOdoo: false) )
			.Concat( ( remoteExisting ?? [] ).Select( f => (file: f, inOdoo: true) ) );
		foreach( var (file, inOdoo) in staged )
		{
			if( !await PdmCommitOperations.CommitRecordAsync( file, commit, inOdoo, Log, () => reporter.Advance( 1 ) ) )
			{
				reporter.Complete();
				return;
			}
		}

		var (committed, error) = await commit.ServerCommit();
		if( !committed )
		{
			reporter.Complete();
			// nothing was created on the server; never report a failed commit as done
			Log( StatusMessage.ERROR, $"Commit failed on the server: {error}" );
			await reporter.NotifyAsync( $"Commit failed: {error}", "Commit Files" );
			return;
		}
		// the committed entries are checked in by the server; the unchanged ones that came along
		// (e.g. the parts of a checked-out assembly) are released here
		await CheckInUnchangedAsync( unchanged );
		reporter.Complete();
		await reporter.NotifyAsync( "Completed!" );
	}

	/// <summary>Releases the checkout of entries that were committed without changes (no new version).</summary>
	private async Task CheckInUnchangedAsync( HpEntry[] unchanged )
	{
		if( unchanged.Length == 0 )
			return;
		Log( StatusMessage.INFO, $"{unchanged.Length} unchanged file(s): checked in without a new version" );
		await PdmCheckoutOperations.UnCheckOutEntriesAsync( unchanged, OdooDefaults.Instance.OdooId, Log );
	}

	/// <inheritdoc/>
	public async Task<HashSet<HackFile>> CollectCommitFilesAsync( IEnumerable<EntryRow> entries )
	{
		Log( StatusMessage.PROCESSING, "checking entries and dependencies" );
		HashSet<HackFile> hackFiles = [];
		foreach( EntryRow item in entries )
		{
			if( string.IsNullOrEmpty( item.FullName ) || string.IsNullOrEmpty( item.Type ) )
				continue;

			if( SwDocumentTypes.IsSolidWorksExtension( item.Type ) )
			{
				var (success, files, results) = await DependencyScanner.GetHackFileWithDependencies( item );
				if( success )
				{
					hackFiles.AddAll( files ?? [] );
					continue;
				}

				foreach( var result in results )
					Log( StatusMessage.ERROR, $"({result.Value?.Result}) file: {result.Value?.Hack?.FullPath}" );
				Log( StatusMessage.ERROR, "Commit Terminated" );
			}
			else if( HackFile.GetFromPath( item.FullName ) is { Exists: true } hack )
			{
				hackFiles.Add( hack );
			}
		}

		if( hackFiles.Count == 0 )
			Log( StatusMessage.INFO, "no valid entries to process" );
		return hackFiles;
	}

	/// <inheritdoc/>
	public async Task<List<HackFile>> CollectFolderCommitFilesAsync( string folderPath )
	{
		var (files, broken) = await DependencyScanner.GetHackFolderWithDependenciesAsync( folderPath );
		if( broken.Count > 0 )
		{
			// same rule as committing selected files: a SolidWorks file with missing references is not committed
			foreach( var result in broken )
				Log( StatusMessage.ERROR, $"({result.Value?.Result}) file: {result.Value?.Hack?.FullPath}" );
			Log( StatusMessage.WARNING, "SolidWorks files with missing references were left out of the commit" );
		}
		return files;
	}

	#endregion
	#region Delete and restore

	/// <inheritdoc/>
	public async Task LogicalDeleteAsync( IReadOnlyList<int> entryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		// only the selected entries: what they depend on (an assembly's parts) is often used by other
		// entries too, and deleting an assembly must not take its parts with it
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( entryIds, excludedFields: DeleteExcludedFields );
		await AsyncHelper.AsyncRunner( () => PdmDeleteOperations.SetDeletedAsync( entries, true, Log ), "Logically Delete Files" );
	}

	/// <inheritdoc/>
	public async Task RestoreDeletedInDirectoryAsync( int directoryId )
	{
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( null,
			searchFilters: OdooDomain.Where( "deleted", true ).And( "dir_id", directoryId ),
			excludedFields: DeleteExcludedFields );

		await AsyncHelper.AsyncRunner( () => PdmDeleteOperations.SetDeletedAsync( entries, false, Log ), "Logically UnDelete Files" );
		reporter.Complete();
	}

	/// <inheritdoc/>
	public async Task<bool> PermanentDeleteAsync( IReadOnlyList<int> entryIds )
	{
		bool deleted = false;
		await AsyncHelper.AsyncRunner( async () =>
		{
			// DeleteEntry also removes versions, version properties, version relationships and attachments
			deleted = await PdmDeleteOperations.PermanentDeleteEntry( entryIds, Log );
			if( deleted )
				Log( StatusMessage.SUCCESS, "Completed permanent delete" );
		}, "Permanently Delete Files" );
		return deleted;
	}

	#endregion
	#region Historical versions

	/// <inheritdoc/>
	public async Task<VersionDownloadPlan?> PlanVersionDownloadAsync( int versionId )
	{
		if( versionId is 0 )
			return null;

		HpVersion? version = ( await HpVersion.GetRecordsByIdsAsync( [ versionId ] ) )?.FirstOrDefault();
		if( version is null )
			return null;

		HpEntry? entry = version.entry_id?.id is int entryId ? await HpEntry.GetRecordByIdAsync( entryId ) : null;
		Result<IReadOnlyList<int>> dependencies = await PdmDownloadOperations.GetVersionListAsync( versionId );
		if( !dependencies.Ok )
			Log( StatusMessage.WARNING, $"Dependencies of version {versionId} could not be listed: {dependencies.Error}" );
		IReadOnlyList<int> versionIds = [ .. dependencies.ValueOr( [] ).Append( versionId ).Distinct() ];

		return new VersionDownloadPlan( version, entry?.latest_version_id == versionId, versionIds );
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds )
	{
		HpVersion[] versions = await HpVersion.GetRecordsByIdsAsync( versionIds ) ?? [];
		return ( await versions.DownloadAllAsync() ).Ids();
	}

	#endregion
}
