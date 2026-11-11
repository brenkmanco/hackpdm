using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Infrastructure.Operations;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Models;

namespace HackPDM.UI.Forms.Hack;

/// <summary>
/// Presentation layer facade for PDM download workflows.
/// Delegates business logic to <see cref="PdmDownloadOperations"/>.
/// </summary>
internal static class Latest
{
	public static int DownloadBatchSize
	{
		get => OdooDefaults.Instance.DownloadBatchSize;
		set => OdooDefaults.Instance.DownloadBatchSize = value;
	}

	internal static async Task Async_GetLatest( (ArrayList, CancellationToken?) arguements )
	{
		ArrayList entryIDs = arguements.Item1;

		InstanceManager.Session.Log( StatusMessage.FOUND, $"{entryIDs.Count} entries" );
		InstanceManager.Session.Log( StatusMessage.PROCESSING, "Retrieving all latest versions associated with entries..." );

		var versions = await PdmDownloadOperations.GetLatestVersionsAsync(entryIDs, ["preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents"]);

		// batches are limited by total file size as well as count (see PdmDownloadOperations.MaxBatchBytes)
		List<List<HpVersion>>? versionBatches = versions is null ? null : PdmDownloadOperations.BatchBySize(versions, Latest.DownloadBatchSize);
		InstanceManager.Dialog.MaxCount = versions?.Length ?? 0;
		InstanceManager.Dialog.SkipCounter = 0;
		InstanceManager.Dialog.ProcessCounter = 0;
		InstanceManager.Dialog.Downloaded = 0;

		if( versionBatches is not { Count: > 0 } || versions is null ) {
			await MessageBox.ShowAsync( "Cancelled Download... No Versions to Process" );
			return;
		}
		PdmDownloadOperations.DownloadSummary? summary = null;
		try {
			CancellationToken token = arguements.Item2 ?? CancellationToken.None;
			summary = await PdmDownloadOperations.ProcessDownloadsAsync(
				versionBatches,
				token,
				OdooDefaults.Instance.ConcurrencySize,
				log: (status, msg) => InstanceManager.Session.Log( status, msg ),
				onProgress: (proc, skip) =>
				{
					InstanceManager.HFM.ProcessCounter += proc;
					InstanceManager.HFM.SkipCounter += skip;
					InstanceManager.HFM.TotalProcessed = InstanceManager.HFM.SkipCounter + InstanceManager.HFM.ProcessCounter;
				});
		} catch {
			await MessageBox.ShowAsync( "Cancelled Download" );
		}

		InstanceManager.Dialog?.SetProgressBar( versions.Length, versions.Length );
		if( summary is null )
			return;
		if( summary.Failed.IsEmpty )
		{
			await MessageBox.ShowAsync( $"Completed! {summary.Downloaded} downloaded, {summary.Skipped} already up to date." );
			return;
		}
		// never report success when files are missing (they used to be skipped silently)
		var failed = summary.Failed.ToArray();
		await MessageBox.ShowAsync(
			$"Completed with {failed.Length} file(s) NOT downloaded ({summary.Downloaded} downloaded, {summary.Skipped} up to date):\n"
			+ string.Join( "\n", failed.Take( 15 ) )
			+ ( failed.Length > 15 ? $"\n...and {failed.Length - 15} more (see the status log)" : "" ),
			"Get Latest" );
	}

	public static async Task ProcessDownloadsAsync( IEnumerable<IEnumerable<HpVersion>> versionBatches, CancellationToken? cToken, int maxConcurrency = 2 )
		=> await PdmDownloadOperations.ProcessDownloadsAsync(
			versionBatches,
			cToken ?? CancellationToken.None,
			maxConcurrency,
			log: (status, msg) => InstanceManager.Session.Log( status, msg ),
			onProgress: (proc, skip) =>
			{
				InstanceManager.HFM.ProcessCounter += proc;
				InstanceManager.HFM.SkipCounter += skip;
				InstanceManager.HFM.TotalProcessed = InstanceManager.HFM.SkipCounter + InstanceManager.HFM.ProcessCounter;
			});

	internal static async Task<ArrayList> GetAllEntriesAndDependenciesList( int[] entryIds, bool update = false )
		=> await PdmDownloadOperations.GetAllEntriesAndDependenciesListAsync( entryIds );

	internal static async Task GetLatestInternal( ArrayList entryIDs, HFM_VM viewModel )
	{
		InstanceManager.Session.Log( StatusMessage.INFO, "Finding Entry Dependencies..." );
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: [nameof(HpEntry.latest_version_id)]);

		if( entries is null || entries.Length < 1 )
			return;
		ArrayList newIds = await PdmDownloadOperations.GetAllEntriesAndDependenciesListAsync([.. entries.Select(entry => entry.latest_version_id?.id ?? 0)]);

		newIds.AddRange( entryIDs );
		newIds = newIds.ToHashSet<int>().ToArrayList();

		(ArrayList, CancellationToken?) arguments = (newIds, viewModel.statusToken?.Token);

		viewModel.statusToken?.Token.Register( CancelledOperation );
		await AsyncHelper.AsyncRunner( () => Async_GetLatest( arguments ), "Get Latest", viewModel.statusToken );
	}

	internal static void CancelledOperation()
	{
		InstanceManager.Dialog?.IsInProcess = false;
	}

	internal static async Task<HpVersion[]?> GetLatestVersions( ArrayList entryIDs, string[]? excludedFields = null )
		=> await PdmDownloadOperations.GetLatestVersionsAsync( entryIDs, excludedFields );

	internal static async Task<ArrayList> GetVersionList( params int[] versionIds )
		=> await PdmDownloadOperations.GetVersionListAsync( versionIds );
}
