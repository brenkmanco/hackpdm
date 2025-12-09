using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.ClientUtils;
using HackPDM.Extensions.General;
using HackPDM.Extensions.Controls;
using HackPDM.Forms.Hack;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Src.ClientUtils.Types;

using Microsoft.UI.Xaml.Controls;

using StatusDialog = HackPDM.Forms.Settings.StatusDialog;

using MessageBox = System.Windows.Forms.MessageBox;
using HackPDM.Forms.Settings;
using HackPDM.Src.Extensions.General;
using HackPDM.Data;
namespace HackPDM.Odoo.Methods;

internal static class Latest
{
	internal static async Task      Async_GetLatest             ((ArrayList, CancellationToken) arguements, bool sentFromCheckout = false)
	{
		object lockObject = new();
		ArrayList entryIDs = arguements.Item1;
		HpVersion[] versions;
		var sd = StatusData.StaticData;
		// add status lines for entry id and upcoming versions
		lock (lockObject)
		{
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.FOUND, $"{entryIDs.Count} entries");
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Retrieving all latest versions associated with entries...");
		}

		versions = GetLatestVersions(entryIDs, ["preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents"]);

		IEnumerable<IEnumerable<HpVersion>>? versionBatches = Help.BatchArray(versions, OdooDefaults.DownloadBatchSize);

		sd.MaxCount = versions.Length;
		sd.SkipCounter = 0;
		sd.ProcessCounter = 0;
		sd.DownloadBytes = 0;

		try
		{
			await ProcessDownloadsAsync(versionBatches, arguements.Item2);
		}
		catch
		{
			MessageBox.Show("Cancelled Download");
		}

        HackFileManager.Dialog?.SetProgressBar(versions.Length, versions.Length);

		if (!sentFromCheckout)
		{
			MessageBox.Show($"Completed!");
		}
		var hack = ISingletonPage<HackFileManager>.Singleton;
		hack?.RestartTree();
		hack?.RestartEntries();
		//InstanceManager.GetAPage<HackFileManager>().RestartEntries();
	}
	internal static HpVersion []	GetLatestVersions			(ArrayList entryIDs, string[] excludedFields = null)
	{
		if (excludedFields == null) excludedFields = ["preview_image", "file_contents"];
		return HpEntry.GetRelatedRecordByIds<HpVersion>(entryIDs, "latest_version_id", excludedFields);
	}
	internal static async Task		ProcessVersionBatchAsync	(IEnumerable<HpVersion> batchVersions)
	{
		object lockObject = new();
		ConcurrentBag<HpVersion> processVersions = [];
		ConcurrentBag<int> unprocessedVersions = [];
		List<Task> tasks = [];
		var sd = StatusData.StaticData;

		foreach (HpVersion version in batchVersions)
		{
			bool willProcess = true;
                
			// ==============================================================
			// check to see if the version has a checksum and if it is the
			// same as the one locally; if not don't download
			// ==============================================================
			if (version.checksum == null || version.checksum.Length == 0 || version.checksum == "False")
			{
				HackFileManager.QueueAsyncStatus.Enqueue((StatusMessage.ERROR, $"Checksum not found for version: {version.name}"));
				sd.SkipCounter++;
				willProcess = false;
			}
			if (willProcess && FileOperations.SameChecksum(version, ChecksumType.Sha1))
			{

				//unprocessedVersions.Add(version.ID);
				HackFileManager.QueueAsyncStatus.Enqueue((StatusMessage.FOUND, $"Skipping version download: {version.name}"));
				sd.SkipCounter++;
				willProcess = false;
			}
			// ==============================================================
			if (willProcess)
			{
				string fileName = Path.Combine(version.WinPathway, version.name);
				processVersions.Add(version);

				HackFileManager.QueueAsyncStatus.Enqueue((StatusMessage.PROCESSING, $"Downloading latest version: {fileName}"));
				sd.ProcessCounter++;
			}
			sd.totalProcessed = sd.SkipCounter + sd.ProcessCounter;
			if (sd.totalProcessed % 25 == 0 || sd.totalProcessed >= sd.MaxCount)
			{
                HackFileManager.Dialog?.SetTotalDownloaded(StatusData.SessionDownloadBytes);
                HackFileManager.Dialog?.SetDownloaded(sd.DownloadBytes);
                HackFileManager.Dialog?.AddStatusLines(HackFileManager.QueueAsyncStatus);
			}
            HackFileManager.Dialog?.SetProgressBar(sd.SkipCounter + sd.ProcessCounter, sd.MaxCount);
		}
			
		await Task.Run(async () =>
		{
			if (!processVersions.IsEmpty)
			{
				Task<int[]> finishSuccesses = Task.WhenAll(HpVersion.BatchDownloadFiles([.. processVersions]));
				await finishSuccesses;
				return finishSuccesses.Result[0];
			}
			return 0;
		});
	}
	internal static async Task		ProcessDownloadsAsync		(IEnumerable<IEnumerable<HpVersion>> versionBatches, CancellationToken cToken)
	{
		SemaphoreSlim throttler = new(OdooDefaults.ConcurrencySize);
		ConcurrentQueue<Task> tasks = new();
			
		foreach (var batch in versionBatches)
		{
			await throttler.WaitAsync(cToken);

			Task task = Task.Run(async () =>
			{
				cToken.ThrowIfCancellationRequested();
				try
				{
					await ProcessVersionBatchAsync(batch);
				}
				finally
				{
					throttler.Release();
				}
			}, cToken);

			if (tasks.Count > OdooDefaults.ConcurrencySize)
			{
				tasks.TryDequeue(out _);
			}
			tasks.Enqueue(task);
		}

		await Task.WhenAll(tasks);
	}
	internal static async void		GetLatestFromTreeNode		(bool withSubdirectories = false, bool sentFromCheckout = false)
	{
		object lockObject = new();

		var hfm = InstanceManager.GetAPage<HackFileManager>();

        TreeViewNode? tnCurrent = hfm.LastSelectedNode;
		TreeData? data = hfm.LastSelectedNode?.LinkedData;
		
		if ( tnCurrent == null )
		{
			MessageBox.Show( "current directory doesn't exist remotely" );
			return;
		}

		// directory only needs ID set to find that record's entries
		HpDirectory directory = new()
		{
			Id = data?.DirectoryId ?? 0,
			name = data?.Name ?? "",
		};

		ArrayList? entryIDs = await directory.GetDirectoryEntryIDsAsync( withSubdirectories, false);
		await GetLatestInternal(entryIDs, sentFromCheckout);
	}
	internal static async Task      GetLatestInternal           (ArrayList entryIDs, bool sentFromCheckout = false)
	{
		Notifier.CancelCheckLoop();
        HackFileManager.Dialog = new StatusDialog();
		await HackFileManager.Dialog!.ShowWait("Get Latest");

        HackFileManager.Dialog?.AddStatusLine(StatusMessage.INFO, "Finding Entry Dependencies...");
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: ["latest_version_id"]);
		//HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, includedFields: ["latest_version_id"]);
		if (entries is null) { return; }
		HackFileManager.statusToken = await HackFileManager.statusToken.RenewTokenSourceAsync();
		HackFileManager.Dialog?.IsInProcess = true;
		ArrayList newIds = await HpEntry.GetEntryList([.. entries.Select(entry => entry.latest_version_id)]);

		newIds.AddRange(entryIDs);
		newIds = newIds.ToHashSet<int>().ToArrayList();
		(ArrayList, CancellationToken) arguments = (newIds, HackFileManager.statusToken!.Token);
		await AsyncHelper.AsyncRunner(() => Async_GetLatest(arguments, sentFromCheckout), "Get Latest", HackFileManager.statusToken);
		HackFileManager.Dialog?.IsInProcess = false;
		Notifier.FileCheckLoop();
	}
}