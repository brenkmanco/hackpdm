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
public sealed record VersionDownloadPlan( HpVersion Version, bool IsLatest, IReadOnlyList<int> VersionIds );

/// <summary>
/// PDM workflows (get latest, checkout, commit, delete, restore). Progress, log lines and the
/// final message go through <see cref="IOperationReporter"/>; no UI types or global view models.
/// </summary>
public interface IPdmWorkflowService
{
	/// <summary>Loads the Odoo lookup tables (nodes, settings, types, users...) the file manager needs.</summary>
	void PreloadMetadata();

	/// <summary>The directory's entry ids; empty (and logged) when the server call failed.</summary>
	Task<IReadOnlyList<int>> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false );

	/// <summary>Downloads the latest version of each entry and everything it depends on.</summary>
	Task GetLatestAsync( IReadOnlyList<int> entryIds );
	Task CheckoutAsync( IReadOnlyList<int> entryIds );
	/// <summary>Gets the latest of each entry (so the local copies are current), then checks them out.</summary>
	Task CheckoutLatestAsync( IReadOnlyList<int> entryIds );
	Task UnCheckoutAsync( IReadOnlyList<int> entryIds );
	Task CommitAsync( IEnumerable<HackFile> hackFiles );

	/// <summary>Turns selected entry rows into local files to commit, pulling in SolidWorks dependencies.</summary>
	Task<HashSet<HackFile>> CollectCommitFilesAsync( IEnumerable<EntryRow> entries );
	/// <summary>Collects every local file (and its dependencies) under a folder; null on failure.</summary>
	Task<List<HackFile>> CollectFolderCommitFilesAsync( string folderPath );

	Task LogicalDeleteAsync( IReadOnlyList<int> entryIds );
	Task RestoreDeletedInDirectoryAsync( int directoryId );
	Task<bool> PermanentDeleteAsync( IReadOnlyList<int> entryIds );

	Task<VersionDownloadPlan?> PlanVersionDownloadAsync( int versionId );
	/// <summary>Downloads the given versions; returns the ids that failed.</summary>
	Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds );
}

public sealed class PdmWorkflowService( IOperationReporter reporter ) : IPdmWorkflowService
{
	private static readonly string[] HeavyEntryFields = [ "type_id", "cat_id" ];
	private static readonly string[] DeleteExcludedFields = [ "type_id", "cat_id", "checkout_node" ];
	private static readonly string[] LatestVersionExcludedFields = [ "preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents" ];

	private void Log( StatusMessage status, string message ) => reporter.Log( status, message );

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

		if( summary.Failed.IsEmpty )
		{
			await reporter.NotifyAsync( $"Completed! {summary.Downloaded} downloaded, {summary.Skipped} already up to date." );
			return;
		}
		// never report success when files are missing
		var failed = summary.Failed.ToArray();
		await reporter.NotifyAsync(
			$"Completed with {failed.Length} file(s) NOT downloaded ({summary.Downloaded} downloaded, {summary.Skipped} up to date):\n"
			+ string.Join( "\n", failed.Take( 15 ) )
			+ ( failed.Length > 15 ? $"\n...and {failed.Length - 15} more (see the status log)" : "" ),
			"Get Latest" );
	}

	#endregion
	#region Checkout

	public Task CheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: true );

	public Task UnCheckoutAsync( IReadOnlyList<int> entryIds ) => ChangeCheckoutAsync( entryIds, checkout: false );

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

	public Task CommitAsync( IEnumerable<HackFile> hackFiles )
	{
		List<HackFile> files = [ .. hackFiles ];
		return AsyncHelper.AsyncRunner( () => CommitFilesAsync( files ), "Commit Files" );
	}

	private async Task CommitFilesAsync( List<HackFile> files )
	{
		if( files.Count == 0 )
			return;

		var (localOnly, remoteExisting) = await PdmCommitOperations.FilterCommitHackFilesAsync( files, Log );
		if( localOnly is null or [] && remoteExisting is null or [] )
		{
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
		reporter.Complete();
		if( !committed )
		{
			// nothing was created on the server; never report a failed commit as done
			Log( StatusMessage.ERROR, $"Commit failed on the server: {error}" );
			await reporter.NotifyAsync( $"Commit failed: {error}", "Commit Files" );
			return;
		}
		await reporter.NotifyAsync( "Completed!" );
	}

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

	public async Task LogicalDeleteAsync( IReadOnlyList<int> entryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		IReadOnlyList<int> ids = await ExpandWithDependenciesAsync( entryIds );
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( ids, excludedFields: DeleteExcludedFields );
		await AsyncHelper.AsyncRunner( () => PdmDeleteOperations.SetDeletedAsync( entries, true, Log ), "Logically Delete Files" );
	}

	public async Task RestoreDeletedInDirectoryAsync( int directoryId )
	{
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( null,
			searchFilters: OdooDomain.Where( "deleted", true ).And( "dir_id", directoryId ),
			excludedFields: DeleteExcludedFields );

		await AsyncHelper.AsyncRunner( () => PdmDeleteOperations.SetDeletedAsync( entries, false, Log ), "Logically UnDelete Files" );
		reporter.Complete();
	}

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

	public async Task<IReadOnlyList<int>> DownloadVersionsAsync( IReadOnlyList<int> versionIds )
	{
		HpVersion[] versions = await HpVersion.GetRecordsByIdsAsync( versionIds ) ?? [];
		return ( await versions.DownloadAllAsync() ).Ids();
	}

	#endregion
}
