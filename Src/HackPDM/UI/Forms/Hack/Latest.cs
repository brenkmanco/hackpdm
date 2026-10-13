using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.Helper;

using OClient = HackPDM.Infrastructure.Odoo.OdooClient;
using Path = System.IO.Path;

namespace HackPDM.UI.Forms.Hack;

internal static class Latest
{
	public static int DownloadBatchSize
	{
		get => OdooDefaults.Instance.DownloadBatchSize;
		set => OdooDefaults.Instance.DownloadBatchSize = value;
	}

	internal static async Task Async_GetLatest( (ArrayList, CancellationToken?) arguements )
	{
		object lockObject = new();
		ArrayList entryIDs = arguements.Item1;

		// add status lines for entry id and upcoming versions
		lock( lockObject ) {
			InstanceManager.Session.Log( StatusMessage.FOUND, $"{entryIDs.Count} entries" );
			InstanceManager.Session.Log( StatusMessage.PROCESSING, $"Retrieving all latest versions associated with entries..." );
		}

		var versions = await Latest.GetLatestVersions(entryIDs, ["preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents"]);

		IEnumerable<IEnumerable<HpVersion>>? versionBatches = Help.BatchArray(versions, Latest.DownloadBatchSize);
		InstanceManager.Dialog.MaxCount = versions.Length;
		InstanceManager.Dialog.SkipCounter = 0;
		InstanceManager.Dialog.ProcessCounter = 0;
		InstanceManager.Dialog.Downloaded = 0;

		if( versionBatches is null ) {
			MessageBox.ShowAsync( "Cancelled Download... No Versions to Process" );
			return;
		}
		try {
			await Latest.ProcessDownloadsAsync( versionBatches, arguements.Item2, OdooDefaults.Instance.ConcurrencySize );
		} catch {
			await MessageBox.ShowAsync( "Cancelled Download" );
		}

		InstanceManager.Dialog?.SetProgressBar( versions.Length, versions.Length );

		await MessageBox.ShowAsync( "Completed!" );
		//TreeHelper.RestartEntries( ViewModel, OdooDirectoryTree, OdooEntryList );
	}
	internal static async Task Async_GetLatest( (ArrayList, CancellationToken?) arguements )
	{
		object lockObject = new();
		ArrayList entryIDs = arguements.Item1;

		// add status lines for entry id and upcoming versions
		lock( lockObject ) {
			InstanceManager.Session.Log( StatusMessage.FOUND, $"{entryIDs.Count} entries" );
			InstanceManager.Session.Log( StatusMessage.PROCESSING, $"Retrieving all latest versions associated with entries..." );
		}

		var versions = await Latest.GetLatestVersions(entryIDs, ["preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents"]);

		IEnumerable<IEnumerable<HpVersion>>? versionBatches = Help.BatchArray(versions, Latest.DownloadBatchSize);
		InstanceManager.Dialog.MaxCount = versions.Length;
		InstanceManager.Dialog.SkipCounter = 0;
		InstanceManager.Dialog.ProcessCounter = 0;
		InstanceManager.Dialog.Downloaded = 0;

		if( versionBatches is null ) {
			MessageBox.ShowAsync( "Cancelled Download... No Versions to Process" );
			return;
		}
		try {
			await Latest.ProcessDownloadsAsync( versionBatches, arguements.Item2, OdooDefaults.Instance.ConcurrencySize );
		} catch {
			await MessageBox.ShowAsync( "Cancelled Download" );
		}

		InstanceManager.Dialog?.SetProgressBar( versions.Length, versions.Length );

		await MessageBox.ShowAsync( "Completed!" );
		//TreeHelper.RestartEntries( ViewModel, OdooDirectoryTree, OdooEntryList );
	}
	public static async Task ProcessDownloadsAsync( IEnumerable<IEnumerable<HpVersion>> versionBatches, CancellationToken? cToken, int maxConcurrency = 2 )
	{
		SemaphoreSlim throttler = new(maxConcurrency);
		int size = versionBatches.Count();
		List<Task> allTasks = new(size);
		//Dialog?.UpdateStatusDialogLoop( cToken );

		foreach( var batch in versionBatches ?? [] )
		{
			Task task = Task.Run(async () =>
			{
				await throttler.WaitAsync(cToken ??= new());
				cToken?.ThrowIfCancellationRequested();
				try
				{
					await ProcessVersionBatchAsync(batch);
				}
				finally
				{
					throttler.Release();
				}
			}, cToken ??= new());

			allTasks.Add( task );
		}

		await Task.WhenAll( allTasks );
		//Dialog?.EndStatusDialogLoop();
	}
	internal static async Task<ArrayList> GetAllEntriesAndDependenciesList( int[] entryIds, bool update = false )
	{
		ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_entries", [entryIds.ToArrayList()], 1000000);
		return arr;
	}

	internal static async Task Async_GetLatest( (ArrayList, CancellationToken?) arguements )
	{
		object lockObject = new();
		ArrayList entryIDs = arguements.Item1;
		var logger = InstanceManager.Session.Logger;
		var dialog = InstanceManager.Dialog;
		// add status lines for entry id and upcoming versions
		lock( lockObject ) {
			logger( StatusMessage.FOUND, $"{entryIDs.Count} entries" );
			logger( StatusMessage.PROCESSING, $"Retrieving all latest versions associated with entries..." );
		}

		var versions = await Latest.GetLatestVersions(entryIDs, ["preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents"]);

		IEnumerable<IEnumerable<HpVersion>>? versionBatches = Help.BatchArray(versions, Latest.DownloadBatchSize);
		dialog.MaxCount = versions?.Length ?? 0;
		dialog.SkipCounter = 0;
		dialog.ProcessCounter = 0;
		dialog.Downloaded = 0;

		if( versionBatches is null ) {
			MessageBox.ShowAsync( "Cancelled Download... No Versions to Process" );
			return;
		}
		try {
			await Latest.ProcessDownloadsAsync( versionBatches, arguements.Item2, OdooDefaults.Instance.ConcurrencySize );
		} catch {
			await MessageBox.ShowAsync( "Cancelled Download" );
		}

		dialog?.SetProgressBar( versions.Length, versions.Length );

		await MessageBox.ShowAsync( "Completed!" );
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	internal static async Task GetLatestInternal( ArrayList entryIDs, HFM_VM viewModel )
	{
		InstanceManager.Session.Logger( StatusMessage.INFO, "Finding Entry Dependencies..." );
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: [nameof(HpEntry.latest_version_id)]);
		//HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, includedFields: [nameof(HpEntry.latest_version_id)]);

		if( entries is null || entries.Length < 1 )
			return;
		ArrayList newIds = await Latest.GetAllEntriesAndDependenciesList([.. entries.Select(entry => entry.latest_version_id?.id ?? 0)]);

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
	{
		excludedFields ??= [ "preview_image", "file_contents" ];
		return await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>( entryIDs, nameof( HpEntry.latest_version_id ), excludedFields );
	}
	internal static async Task<ArrayList> GetVersionList( params int[] versionIds )
	{
		ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_versions", [versionIds.ToArrayList()], 1000000);
		return arr;
	}
	internal static async Task ProcessVersionBatchAsync( IEnumerable<HpVersion> batchVersions )
	{
		object lockObject = new();
		ConcurrentBag<HpVersion> processVersions = [];
		ConcurrentBag<int> unprocessedVersions = [];
		List<Task> tasks = [];

		foreach( HpVersion version in batchVersions )
		{
			bool willProcess = true;

			// ==============================================================
			// check to see if the version has a checksum and if it is the
			// same as the one locally; if not don't download
			// ==============================================================
			if( version.checksum == null || version.checksum.Length == 0 || version.checksum == "False" )
			{
				InstanceManager.Session.Log( StatusMessage.ERROR, $"Checksum not found for version: {version.name}" );
				InstanceManager.HFM.SkipCounter++;
				willProcess = false;
			}
			if( willProcess && FileOperations.SameChecksum( version, ChecksumType.Sha1 ) )
			{
				InstanceManager.Session.Log( StatusMessage.FOUND, $"Skipping version download: {version.name}") ;
				InstanceManager.HFM.SkipCounter++;
				willProcess = false;
			}
			// ==============================================================
			if( willProcess )
			{
				string fileName = Path.Combine(version.WinPathway, version.name);
				processVersions.Add( version );
				InstanceManager.Session.Log( StatusMessage.PROCESSING, $"Downloading latest version: {fileName}") ;
				InstanceManager.HFM.ProcessCounter++;
			}

			InstanceManager.HFM.TotalProcessed = InstanceManager.HFM.SkipCounter + InstanceManager.HFM.ProcessCounter;
		}
		InstanceManager.HFM.statusToken ??= new();
		await Task.Run( async () =>
		{
			if( !processVersions.IsEmpty )
			{
				Task<(int, long)?[]> finishSuccesses = Task.WhenAll(HpVersion.BatchDownloadFiles([.. processVersions]));
				await finishSuccesses;
				//return finishSuccesses.Result[0];
			}
			return 0;
		}, InstanceManager.HFM.statusToken.Token );
	}
}