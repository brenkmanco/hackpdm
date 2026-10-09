using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

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
	

	public static async Task ProcessDownloadsAsync( IEnumerable<IEnumerable<HpVersion>> versionBatches, CancellationToken? cToken, int maxConcurrency = 2 )
	{
		SemaphoreSlim throttler = new(maxConcurrency);
		int size = versionBatches.Count();
		List<Task> allTasks = new(size);
		Dialog?.UpdateStatusDialogLoop( cToken );

		foreach( var batch in versionBatches ?? [] )
		{
			Task task = Task.Run(async () =>
			{
				await throttler.WaitAsync(cToken);
				cToken.ThrowIfCancellationRequested();
				try
				{
					await Latest.ProcessVersionBatchAsync(batch);
				}
				finally
				{
					throttler.Release();
				}
			}, cToken);

			allTasks.Add( task );
		}

		await Task.WhenAll( allTasks );
		Dialog?.EndStatusDialogLoop();
	}
	internal static async Task<ArrayList> GetAllEntriesAndDependenciesList( int[] entryIds, bool update = false )
	{
		ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_entries", [entryIds.ToArrayList()], 1000000);
		return arr;
	}


	internal static async Task<HpVersion[]?> GetLatestVersions( ArrayList entryIDs, string[]? excludedFields = null )
	{
		if( excludedFields == null )
			excludedFields = [ "preview_image", "file_contents" ];
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
				HackFileManager.QueueAsyncStatus.Enqueue( (StatusMessage.ERROR, $"Checksum not found for version: {version.name}") );
				SkipCounter++;
				willProcess = false;
			}
			if( willProcess && FileOperations.SameChecksum( version, ChecksumType.Sha1 ) )
			{
				HackFileManager.

								//unprocessedVersions.Add(version.ID);
								QueueAsyncStatus.Enqueue( (StatusMessage.FOUND, $"Skipping version download: {version.name}") );
				SkipCounter++;
				willProcess = false;
			}
			// ==============================================================
			if( willProcess )
			{
				string fileName = Path.Combine(version.WinPathway, version.name);
				processVersions.Add( version );
				HackFileManager.
								QueueAsyncStatus.Enqueue( (StatusMessage.PROCESSING, $"Downloading latest version: {fileName}") );
				ProcessCounter++;
			}

			TotalProcessed = SkipCounter + ProcessCounter;
		}

		await Task.Run( async () =>
		{
			if( !processVersions.IsEmpty )
			{
				Task<(int, long)?[]> finishSuccesses = Task.WhenAll(HpVersion.BatchDownloadFiles([.. processVersions]));
				await finishSuccesses;
				//return finishSuccesses.Result[0];
			}
			return 0;
		}, HackFileManager.statusToken.Token );
	}
}