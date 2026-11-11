using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
public sealed record VersionDownloadPlan( HpVersion Version, bool IsLatest, ArrayList VersionIds );

/// <summary>
/// PDM workflows (get latest, checkout, commit, delete, restore). Progress, log lines and the
/// final message go through <see cref="IOperationReporter"/>; no UI types or global view models.
/// </summary>
public interface IPdmWorkflowService
{
	/// <summary>Loads the Odoo lookup tables (nodes, settings, types, users...) the file manager needs.</summary>
	void PreloadMetadata();

	Task<ArrayList?> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false );

	/// <summary>Downloads the latest version of each entry and everything it depends on.</summary>
	Task GetLatestAsync( ArrayList entryIds );
	Task CheckoutAsync( ArrayList entryIds );
	Task UnCheckoutAsync( ArrayList entryIds );
	Task CommitAsync( IEnumerable<HackFile> hackFiles );

	/// <summary>Turns selected entry rows into local files to commit, pulling in SolidWorks dependencies.</summary>
	Task<HashSet<HackFile>> CollectCommitFilesAsync( IEnumerable<EntryRow> entries );
	/// <summary>Collects every local file (and its dependencies) under a folder; null on failure.</summary>
	List<HackFile>? CollectFolderCommitFiles( string folderPath );

	Task LogicalDeleteAsync( ArrayList entryIds );
	Task RestoreDeletedInDirectoryAsync( int directoryId );
	Task<bool> PermanentDeleteAsync( ArrayList entryIds );

	Task<VersionDownloadPlan?> PlanVersionDownloadAsync( int versionId );
	/// <summary>Downloads the given versions; returns the ids that failed.</summary>
	Task<IReadOnlyList<int>> DownloadVersionsAsync( ArrayList versionIds );
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

	public Task<ArrayList?> GetDirectoryEntryIdsAsync( int directoryId, bool withSubdirectories, bool withDeleted = false )
		=> HpDirectory.GetDirectoryEntryIDsAsync( directoryId, withSubdirectories, withDeleted );

	/// <summary>Adds every entry the given entries' latest versions depend on, without duplicates.</summary>
	private static async Task<ArrayList> ExpandWithDependenciesAsync( ArrayList entryIds )
	{
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( entryIds, includedFields: [ nameof( HpEntry.latest_version_id ) ] );
		ArrayList ids = await PdmDownloadOperations.GetAllEntriesAndDependenciesListAsync(
			[ .. entries?.Select( e => e.latest_version_id?.id ?? 0 ) ?? [] ] );

		ids.AddRange( entryIds );
		return ids.ToHashSet<int>().ToArrayList();
	}

	#region Get latest

	public async Task GetLatestAsync( ArrayList entryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		CancellationTokenSource cancellation = reporter.BeginOperation();
		Log( StatusMessage.INFO, "Finding Entry Dependencies..." );
		ArrayList ids = await ExpandWithDependenciesAsync( entryIds );

		await AsyncHelper.AsyncRunner( () => DownloadLatestAsync( ids, cancellation.Token ), "Get Latest", cancellation );
	}

	private async Task DownloadLatestAsync( ArrayList entryIds, CancellationToken token )
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
		catch
		{
			await reporter.NotifyAsync( "Cancelled Download" );
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

	public async Task CheckoutAsync( ArrayList entryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		ArrayList ids = await ExpandWithDependenciesAsync( entryIds );
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( ids, excludedFields: HeavyEntryFields );
		if( entries is null or { Length: < 1 } )
			return;

		await AsyncHelper.AsyncRunner( async () =>
		{
			reporter.SetTotal( entries.Length );
			await PdmCheckoutOperations.CheckOutEntriesAsync( entries, Log, OnCheckoutProgress );
			reporter.Complete();
			await reporter.NotifyAsync( "Completed!" );
		}, "Checkout Files" );
	}

	public async Task UnCheckoutAsync( ArrayList entryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		int me = OdooDefaults.Instance.OdooId;
		ArrayList ids = await ExpandWithDependenciesAsync( entryIds );
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( ids,
		[
			new ArrayList { "checkout_user", OperatorConverter.OperatorToString( Operators.Equal ), me }
		], excludedFields: HeavyEntryFields );
		if( entries is null or { Length: < 1 } )
			return;

		await AsyncHelper.AsyncRunner( async () =>
		{
			reporter.SetTotal( entries.Length );
			// only entries checked out to this user are released
			await PdmCheckoutOperations.UnCheckOutEntriesAsync( entries, me, Log, OnCheckoutProgress );
			reporter.Complete();
			await reporter.NotifyAsync( "Completed!" );
		}, "UnCheckout Files" );
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

		await commit.ServerCommit();
		reporter.Complete();
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

			if( OdooDefaultsConstants.DependentExt.Contains( $".{item.Type.ToUpper()}" ) )
			{
				var (success, files, results) = await ExtensionOdoo.GetHackFileWithDependencies( item, true );
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

	public List<HackFile>? CollectFolderCommitFiles( string folderPath )
	{
		ExtensionOdoo.GetHackFolderWithDependencies( folderPath, true, out List<HackFile>? files );
		return files;
	}

	#endregion
	#region Delete and restore

	public async Task LogicalDeleteAsync( ArrayList entryIds )
	{
		if( entryIds is null or { Count: < 1 } )
			return;

		ArrayList ids = await ExpandWithDependenciesAsync( entryIds );
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( ids, excludedFields: DeleteExcludedFields );
		await AsyncHelper.AsyncRunner( () => PdmDeleteOperations.LogicalDeleteEntries( entries, Log ), "Logically Delete Files" );
	}

	public async Task RestoreDeletedInDirectoryAsync( int directoryId )
	{
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync( null,
			searchFilters: [ new ArrayList { "deleted", "=", true }, new ArrayList { "dir_id", "=", directoryId } ],
			excludedFields: DeleteExcludedFields );

		await AsyncHelper.AsyncRunner( () => PdmDeleteOperations.LogicalUnDeleteEntries( entries, Log ), "Logically UnDelete Files" );
		reporter.Complete();
	}

	public async Task<bool> PermanentDeleteAsync( ArrayList entryIds )
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

		HpEntry? entry = ( await HpEntry.GetRecordsByIdsAsync( [ version.entry_id ] ) )?.FirstOrDefault();
		ArrayList versionIds = await PdmDownloadOperations.GetVersionListAsync( versionId );
		if( !versionIds.Contains( version.id ?? 0 ) )
			versionIds.Add( version.id ?? 0 );

		return new VersionDownloadPlan( version, entry?.latest_version_id == versionId, versionIds );
	}

	public async Task<IReadOnlyList<int>> DownloadVersionsAsync( ArrayList versionIds )
	{
		HpVersion[] versions = await HpVersion.GetRecordsByIdsAsync( versionIds ) ?? [];
		if( versions.DownloadAll( out List<HpVersion> failed ) )
			return [];

		return failed.GetIDs().ToArray<int>();
	}

	#endregion
}
