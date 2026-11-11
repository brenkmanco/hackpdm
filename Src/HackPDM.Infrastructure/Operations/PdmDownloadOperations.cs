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
    private const int DependencyTimeoutMs = 1000000;

    /// <summary>The entries and every entry they depend on (recursively), each once.</summary>
    public static Task<Result<IReadOnlyList<int>>> GetAllEntriesAndDependenciesListAsync(IReadOnlyList<int> entryIds)
        => OdooRpc.ExecuteAsync<IReadOnlyList<int>>(HpVersion.GetHpModel()!, "get_recursive_dependency_entries", [entryIds], DependencyTimeoutMs);

    /// <summary>The versions and every version they depend on (recursively).</summary>
    public static Task<Result<IReadOnlyList<int>>> GetVersionListAsync(params int[] versionIds)
        => OdooRpc.ExecuteAsync<IReadOnlyList<int>>(HpVersion.GetHpModel()!, "get_recursive_dependency_versions", [versionIds], DependencyTimeoutMs);

    public static async Task<HpVersion[]?> GetLatestVersionsAsync(IReadOnlyList<int> entryIDs, string[]? excludedFields = null)
    {
        excludedFields ??= ["preview_image", "file_contents"];
        return await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(entryIDs, nameof(HpEntry.latest_version_id), excludedFields);
    }

    // ---- download tuning -------------------------------------------------------------
    // A batch used to be "N files" regardless of size, read in one RPC call with a fixed 90 s
    // timeout. A batch of large CAD files could not finish in time on a slower link; the read
    // failed, OdooClient turned the error into "no result", and those files were silently
    // skipped. Batches are now limited by bytes as well as count, the timeout scales with the
    // payload, and anything that does not arrive is retried one file at a time and reported.

    /// <summary>Most file bytes requested in one RPC call (a larger file gets a call of its own).</summary>
    public static long MaxBatchBytes { get; set; } = 16L * 1024 * 1024;

    /// <summary>Slowest link the timeouts allow for, in bytes per second.</summary>
    public static long MinThroughputBytesPerSecond { get; set; } = 256 * 1024;

    /// <summary>Attempts per file: the first as part of its batch, the rest on its own.</summary>
    public static int MaxAttempts { get; set; } = 3;

    private const int DefaultBatchCount = 5;
    private const int MinTimeoutMs = 90_000;
    private const int MaxTimeoutMs = 30 * 60_000;

    private static readonly string[] ContentFields =
    [
        nameof(HpVersion.file_contents),
        nameof(HpVersion.dir_id),
        nameof(HpVersion.name),
        nameof(HpVersion.file_modify_stamp),
        nameof(HpVersion.file_size),
        nameof(HpVersion.checkout_user),
        nameof(HpVersion.windows_complete_name),
    ];

    /// <summary>
    /// Splits versions into download batches of at most <paramref name="maxCount"/> files and
    /// <see cref="MaxBatchBytes"/> bytes. Smaller files are packed first, so large files end up
    /// in small batches (or alone).
    /// </summary>
    public static List<List<HpVersion>> BatchBySize(IEnumerable<HpVersion>? versions, int maxCount, long? maxBytes = null)
    {
        long byteBudget = maxBytes ?? MaxBatchBytes;
        if (maxCount <= 0) maxCount = DefaultBatchCount; // a 0 setting used to divide by zero

        List<List<HpVersion>> batches = [];
        List<HpVersion> current = [];
        long currentBytes = 0;

        foreach (HpVersion version in (versions ?? []).OrderBy(v => v.file_size ?? 0))
        {
            long size = Math.Max(0, (long)(version.file_size ?? 0));
            if (current.Count > 0 && (current.Count >= maxCount || currentBytes + size > byteBudget))
            {
                batches.Add(current);
                current = [];
                currentBytes = 0;
            }
            current.Add(version);
            currentBytes += size;
        }
        if (current.Count > 0) batches.Add(current);
        return batches;
    }

    /// <summary>RPC timeout for reading <paramref name="fileBytes"/> of file contents (base64 adds a third).</summary>
    public static int ContentsTimeoutMs(long fileBytes, int attempt = 1)
    {
        double wireBytes = fileBytes * 4.0 / 3.0;
        double ms = 30_000 + wireBytes / MinThroughputBytesPerSecond * 1000;
        ms *= Math.Max(1, attempt);
        return (int)Math.Clamp(ms, MinTimeoutMs, MaxTimeoutMs);
    }

    /// <summary>Outcome of a download run; <see cref="Failed"/> lists every file that was not written.</summary>
    public sealed class DownloadSummary
    {
        private int _downloaded;
        private int _skipped;
        public int Downloaded => _downloaded;
        public int Skipped => _skipped;
        public ConcurrentQueue<string> Failed { get; } = new();
        internal void AddDownloaded() => Interlocked.Increment(ref _downloaded);
        internal void AddSkipped() => Interlocked.Increment(ref _skipped);
    }

    public static async Task<DownloadSummary> ProcessDownloadsAsync(
        IEnumerable<IEnumerable<HpVersion>> versionBatches,
        CancellationToken cToken,
        int maxConcurrency = 2,
        Action<StatusMessage, string>? log = null,
        Action<int, int>? onProgress = null)
    {
        DownloadSummary summary = new();
        using SemaphoreSlim throttler = new(Math.Max(1, maxConcurrency));
        List<Task> allTasks = [];

        foreach (var batch in versionBatches ?? [])
        {
            Task task = Task.Run(async () =>
            {
                await throttler.WaitAsync(cToken);
                try
                {
                    cToken.ThrowIfCancellationRequested();
                    await ProcessVersionBatchAsync(batch, cToken, log, onProgress, summary);
                }
                finally
                {
                    throttler.Release();
                }
            }, cToken);

            allTasks.Add(task);
        }

        await Task.WhenAll(allTasks);
        return summary;
    }

    public static async Task ProcessVersionBatchAsync(
        IEnumerable<HpVersion> batchVersions,
        CancellationToken cToken,
        Action<StatusMessage, string>? log = null,
        Action<int, int>? onProgress = null,
        DownloadSummary? summary = null)
    {
        summary ??= new();
        List<HpVersion> processVersions = [];

        foreach (HpVersion version in batchVersions)
        {
            if (string.IsNullOrEmpty(version.checksum) || version.checksum == "False")
            {
                log?.Invoke(StatusMessage.ERROR, $"Checksum not found for version: {version.name}");
                summary.AddSkipped();
                onProgress?.Invoke(0, 1); // increments: the UI adds these to its counters
                continue;
            }
            if (Checksums.MatchesLocalCopy(version))
            {
                log?.Invoke(StatusMessage.FOUND, $"Skipping version download: {version.name}");
                summary.AddSkipped();
                onProgress?.Invoke(0, 1);
                continue;
            }

            log?.Invoke(StatusMessage.PROCESSING, $"Downloading latest version: {Path.Combine(version.WinPathway ?? "", version.name ?? "")}");
            processVersions.Add(version);
            onProgress?.Invoke(1, 0);
        }

        if (processVersions.Count > 0)
            await DownloadAndWriteAsync(processVersions, cToken, log, summary);
    }

    /// <summary>
    /// Reads file contents and writes the files. The first attempt reads the whole batch in one
    /// call; whatever did not arrive is retried one file per call with a longer timeout.
    /// Files still missing after <see cref="MaxAttempts"/> are logged and added to the summary.
    /// </summary>
    private static async Task DownloadAndWriteAsync(
        List<HpVersion> versions, CancellationToken cToken, Action<StatusMessage, string>? log, DownloadSummary summary)
    {
        Dictionary<int, HpVersion> pending = versions.Where(v => v.id is > 0).DistinctBy(v => v.id).ToDictionary(v => v.id!.Value);
        Dictionary<int, string> lastError = [];

        for (int attempt = 1; attempt <= MaxAttempts && pending.Count > 0; attempt++)
        {
            cToken.ThrowIfCancellationRequested();
            if (attempt > 1)
                log?.Invoke(StatusMessage.INFO, $"Retrying {pending.Count} file(s) individually (attempt {attempt} of {MaxAttempts})");

            List<List<HpVersion>> groups = attempt == 1 ? [[.. pending.Values]] : [.. pending.Values.Select(v => new List<HpVersion> { v })];

            foreach (List<HpVersion> group in groups)
            {
                cToken.ThrowIfCancellationRequested();
                long bytes = group.Sum(v => (long)(v.file_size ?? 0));
                int timeout = ContentsTimeoutMs(bytes, attempt);

                Result<HpVersion[]> read = await Repository<HpVersion>.ReadAsync(
                    group.Ids(), FieldSelection.Only(ContentFields), timeout);
                string rpcError = read.Error ?? "no data returned";

                Dictionary<int, HpVersion> byId = read.ValueOr([]).Where(v => v?.id is > 0).ToDictionary(v => v.id!.Value);
                foreach (HpVersion requested in group)
                {
                    int id = requested.id!.Value;
                    if (!byId.TryGetValue(id, out HpVersion? got)
                        || (got.file_contents is not { Length: > 0 } && (got.file_size ?? 0) > 0))
                    {
                        lastError[id] = !read.Ok
                            ? $"{rpcError} ({ByteSize.Format( bytes )} request, {timeout / 1000}s timeout)"
                            : "contents missing from the server response";
                        continue;
                    }

                    if (got.file_contents is { Length: > 0 } && !await LocalFileWriter.WriteAsync(got.ToHackFile()))
                    {
                        // a local write problem will not be fixed by downloading again
                        pending.Remove(id);
                        Fail(requested, "could not write the file locally");
                        continue;
                    }

                    pending.Remove(id);
                    summary.AddDownloaded();
                }
            }
        }

        foreach (HpVersion version in pending.Values)
            Fail(version, lastError.GetValueOrDefault(version.id ?? 0, "unknown error"));

        void Fail(HpVersion version, string reason)
        {
            string name = Path.Combine(version.WinPathway ?? "", version.name ?? $"version {version.id}");
            summary.Failed.Enqueue($"{name}: {reason}");
            log?.Invoke(StatusMessage.ERROR, $"Failed to download {name} after {MaxAttempts} attempt(s): {reason}");
        }
    }
}
