using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Operations;

/// <summary>
/// Infrastructure service for dependency resolution and batch version downloading.
/// Encapsulates recursive Odoo commands, checksum comparison, and throttled parallel downloads.
/// </summary>
public static class PdmDownloadOperations
{
    public static async Task<ArrayList> GetAllEntriesAndDependenciesListAsync(int[] entryIds)
    {
        return await OdooClient.CommandAsync<ArrayList>(
            HpVersion.GetHpModel(),
            "get_recursive_dependency_entries",
            [entryIds.ToArrayList()],
            1000000);
    }

    public static async Task<ArrayList> GetVersionListAsync(params int[] versionIds)
    {
        return await OdooClient.CommandAsync<ArrayList>(
            HpVersion.GetHpModel(),
            "get_recursive_dependency_versions",
            [versionIds.ToArrayList()],
            1000000);
    }

    public static async Task<HpVersion[]?> GetLatestVersionsAsync(ArrayList entryIDs, string[]? excludedFields = null)
    {
        excludedFields ??= ["preview_image", "file_contents"];
        return await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(entryIDs, nameof(HpEntry.latest_version_id), excludedFields);
    }

    public static async Task ProcessDownloadsAsync(
        IEnumerable<IEnumerable<HpVersion>> versionBatches,
        CancellationToken cToken,
        int maxConcurrency = 2,
        Action<StatusMessage, string>? log = null,
        Action<int, int>? onProgress = null)
    {
        using SemaphoreSlim throttler = new(maxConcurrency);
        List<Task> allTasks = [];

        foreach (var batch in versionBatches ?? [])
        {
            Task task = Task.Run(async () =>
            {
                await throttler.WaitAsync(cToken);
                cToken.ThrowIfCancellationRequested();
                try
                {
                    await ProcessVersionBatchAsync(batch, cToken, log, onProgress);
                }
                finally
                {
                    throttler.Release();
                }
            }, cToken);

            allTasks.Add(task);
        }

        await Task.WhenAll(allTasks);
    }

    public static async Task ProcessVersionBatchAsync(
        IEnumerable<HpVersion> batchVersions,
        CancellationToken cToken,
        Action<StatusMessage, string>? log = null,
        Action<int, int>? onProgress = null)
    {
        ConcurrentBag<HpVersion> processVersions = [];
        int skipCount = 0;
        int processCount = 0;

        foreach (HpVersion version in batchVersions)
        {
            bool willProcess = true;

            if (string.IsNullOrEmpty(version.checksum) || version.checksum == "False")
            {
                log?.Invoke(StatusMessage.ERROR, $"Checksum not found for version: {version.name}");
                skipCount++;
                willProcess = false;
            }
            if (willProcess && FileOperations.SameChecksum(version, ChecksumType.Sha1))
            {
                log?.Invoke(StatusMessage.FOUND, $"Skipping version download: {version.name}");
                skipCount++;
                willProcess = false;
            }

            if (willProcess)
            {
                string fileName = Path.Combine(version.WinPathway, version.name);
                processVersions.Add(version);
                log?.Invoke(StatusMessage.PROCESSING, $"Downloading latest version: {fileName}");
                processCount++;
            }

            onProgress?.Invoke(processCount, skipCount);
        }

        if (!processVersions.IsEmpty)
        {
            await Task.Run(async () =>
            {
                await Task.WhenAll(HpVersion.BatchDownloadFiles([.. processVersions]));
            }, cToken);
        }
    }
}
