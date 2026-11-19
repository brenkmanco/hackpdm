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

    /// <summary>Asks the server for the given entries plus every entry they depend on, recursively, each once.</summary>
    /// <param name="entryIds">The hp.version ids to start from.</param>
    /// <returns>The entry ids, or a failed result with the server's or the transport's error.</returns>
    /// <remarks>Calls <c>hp.version.get_recursive_dependency_entries</c> with a timeout of about 17 minutes, since large assemblies take long.</remarks>
    public static Task<Result<IReadOnlyList<int>>> GetAllEntriesAndDependenciesListAsync(IReadOnlyList<int> entryIds)
        => OdooRpc.ExecuteAsync<IReadOnlyList<int>>(HpVersion.GetHpModel()!, "get_recursive_dependency_entries", [entryIds], DependencyTimeoutMs);

    /// <summary>
    /// Asks the server for the given entry ids plus every entry their latest versions depend on, recursively, in one SQL call.
    /// </summary>
    public static Task<Result<IReadOnlyList<int>>> GetAllEntriesAndDependenciesByEntriesAsync(IReadOnlyList<int> entryIds)
        => OdooRpc.ExecuteAsync<IReadOnlyList<int>>(HpVersion.GetHpModel()!, "get_recursive_dependency_entries_by_entries", [entryIds], DependencyTimeoutMs);

    /// <summary>Asks the server for the given versions plus every version they depend on, recursively.</summary>
    /// <param name="versionIds">The hp.version ids to start from.</param>
    /// <returns>The version ids, or a failed result with the server's or the transport's error.</returns>
    /// <remarks>Calls <c>hp.version.get_recursive_dependency_versions</c> with a timeout of about 17 minutes.</remarks>
    public static Task<Result<IReadOnlyList<int>>> GetVersionListAsync(params int[] versionIds)
        => OdooRpc.ExecuteAsync<IReadOnlyList<int>>(HpVersion.GetHpModel()!, "get_recursive_dependency_versions", [versionIds], DependencyTimeoutMs);

    private const int MetadataChunkSize = 500;

    /// <summary>Reads the latest version of each entry (following <c>latest_version_id</c>), without the file contents by default.</summary>
    /// <param name="entryIDs">The hp.entry ids.</param>
    /// <param name="excludedFields">Version fields not to read; ignored when <paramref name="includedFields"/> is specified.</param>
    /// <param name="token">Cancels between metadata chunks.</param>
    /// <param name="includedFields">When non-null, reads only these fields (much faster than excluding heavy relational fields).</param>
    /// <returns>The versions, or null when there are none or the read failed.</returns>
    /// <remarks>Uses the addon's <c>related_browse</c> in bounded chunks so large directories don't time out or spike memory.</remarks>
    public static async Task<HpVersion[]?> GetLatestVersionsAsync(
        IReadOnlyList<int> entryIDs,
        string[]? excludedFields = null,
        CancellationToken token = default,
        string[]? includedFields = null)
    {
        excludedFields ??= ["preview_image", "file_contents"];
        if (entryIDs.Count <= MetadataChunkSize)
            return await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
                entryIDs, nameof(HpEntry.latest_version_id),
                includedFields is null ? excludedFields : null,
                includedFields: includedFields);

        List<HpVersion> all = new(entryIDs.Count);
        for (int offset = 0; offset < entryIDs.Count; offset += MetadataChunkSize)
        {
            token.ThrowIfCancellationRequested();
            int count = Math.Min(MetadataChunkSize, entryIDs.Count - offset);
            int[] chunk = [.. entryIDs.Skip(offset).Take(count)];
            HpVersion[]? page = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
                chunk, nameof(HpEntry.latest_version_id),
                includedFields is null ? excludedFields : null,
                includedFields: includedFields);
            if (page is { Length: > 0 })
                all.AddRange(page);
        }
        return all.Count > 0 ? [.. all] : null;
    }

    // ---- download tuning & memory-aware throttling -----------------------------------
    // A batch used to be "N files" regardless of size, read in one RPC call with a fixed 90 s
    // timeout. Batches are now limited by bytes (scaled down automatically under RAM pressure)
    // as well as count, concurrency scales down when memory load is high, payloads are released
    // immediately after writing, and anything that does not arrive is retried one file at a time.

    /// <summary>Most file bytes requested in one RPC call (a larger file gets a call of its own).</summary>
    public static long MaxBatchBytes { get; set; } = 16L * 1024 * 1024;

    /// <summary>Slowest link the timeouts allow for, in bytes per second.</summary>
    public static long MinThroughputBytesPerSecond { get; set; } = 256 * 1024;

    /// <summary>Attempts per file: the first as part of its batch, the rest on its own.</summary>
    public static int MaxAttempts { get; set; } = 3;

    private const long OneGiB = 1024L * 1024 * 1024;
    private const long HalfGiB = 512L * 1024 * 1024;

    /// <summary>
    /// Computes a safe per-batch byte budget based on current system memory availability.
    /// Base64 + JSON/XML-RPC deserialization amplifies raw file bytes by ~4–6x in managed memory,
    /// so smaller batches prevent LOH thrashing on low-RAM machines.
    /// </summary>
    public static long EffectiveBatchBytes(long? requestedMaxBytes = null)
    {
        long cap = requestedMaxBytes ?? MaxBatchBytes;
        GCMemoryInfo mem = GC.GetGCMemoryInfo();
        long total = mem.TotalAvailableMemoryBytes;
        if (total <= 0)
            return cap;

        long free = Math.Max(0, total - mem.MemoryLoadBytes);
        double loadRatio = (double)mem.MemoryLoadBytes / total;

        long ramCap = (loadRatio, free) switch
        {
            ( >= 0.88, _ ) or ( _, < HalfGiB ) => 2L * 1024 * 1024,
            ( >= 0.80, _ ) or ( _, < OneGiB ) => 4L * 1024 * 1024,
            ( >= 0.70, _ ) or ( _, < 2 * OneGiB ) => 8L * 1024 * 1024,
            _ => cap,
        };
        return Math.Min(cap, ramCap);
    }

    /// <summary>
    /// Computes an effective batch concurrency capped by both <paramref name="requestedConcurrency"/>
    /// and current system memory pressure.
    /// </summary>
    public static int EffectiveConcurrency(int requestedConcurrency)
    {
        int capped = Math.Max(1, requestedConcurrency);
        GCMemoryInfo mem = GC.GetGCMemoryInfo();
        long total = mem.TotalAvailableMemoryBytes;
        if (total <= 0)
            return capped;

        long free = Math.Max(0, total - mem.MemoryLoadBytes);
        double loadRatio = (double)mem.MemoryLoadBytes / total;

        if (loadRatio >= 0.85 || free < HalfGiB)
            return 1;
        if (loadRatio >= 0.75 || free < OneGiB)
            return Math.Min(capped, 2);
        return capped;
    }

    private static bool IsMemoryPressureHigh()
    {
        GCMemoryInfo mem = GC.GetGCMemoryInfo();
        long total = mem.TotalAvailableMemoryBytes;
        if (total <= 0)
            return false;
        long free = Math.Max(0, total - mem.MemoryLoadBytes);
        double loadRatio = (double)mem.MemoryLoadBytes / total;
        return loadRatio >= 0.86 || free < HalfGiB;
    }

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
    /// Splits versions into download batches of at most <paramref name="maxCount"/> files and at most
    /// <paramref name="maxBytes"/> bytes. Smaller files are packed first, so large files end up
    /// in small batches (or alone).
    /// </summary>
    /// <param name="versions">The versions to download; null gives no batches.</param>
    /// <param name="maxCount">Most files per batch; 0 or less means 5.</param>
    /// <param name="maxBytes">Most file bytes per batch (by <c>file_size</c>); <see cref="MaxBatchBytes"/> when null. A single larger file still gets a batch of its own.</param>
    /// <returns>The batches, ordered by file size ascending.</returns>
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

    /// <summary>Computes the RPC timeout for reading <paramref name="fileBytes"/> of file contents, allowing for base64 (a third larger) at <see cref="MinThroughputBytesPerSecond"/>.</summary>
    /// <param name="fileBytes">Total file bytes requested in the call.</param>
    /// <param name="attempt">The attempt number (1-based); the timeout is multiplied by it so retries wait longer.</param>
    /// <returns>30 seconds plus the transfer time, times the attempt, clamped to 90 seconds .. 30 minutes, in milliseconds.</returns>
    public static int ContentsTimeoutMs(long fileBytes, int attempt = 1)
    {
        double wireBytes = fileBytes * 4.0 / 3.0;
        double ms = 30_000 + wireBytes / MinThroughputBytesPerSecond * 1000;
        ms *= Math.Max(1, attempt);
        return (int)Math.Clamp(ms, MinTimeoutMs, MaxTimeoutMs);
    }

    /// <summary>Outcome of a download run; <see cref="Failed"/> lists every file that was not written. Safe to update from parallel batches.</summary>
    public sealed class DownloadSummary
    {
        private int _downloaded;
        private int _skipped;
        /// <summary>Number of files read from the server and written (or empty files accepted).</summary>
        public int Downloaded => _downloaded;
        /// <summary>Number of files not downloaded: already up to date, with no checksum on the server, or kept because of local changes.</summary>
        public int Skipped => _skipped;
        /// <summary>"path: reason" for each file that could not be downloaded or written.</summary>
        public ConcurrentQueue<string> Failed { get; } = new();
        /// <summary>Local files that were not overwritten because they hold changes no vault version has.</summary>
        public ConcurrentQueue<string> KeptLocalChanges { get; } = new();
        internal void AddDownloaded() => Interlocked.Increment(ref _downloaded);
        internal void AddSkipped() => Interlocked.Increment(ref _skipped);
    }

    /// <summary>Downloads batches of versions in parallel, at most <paramref name="maxConcurrency"/> batches at a time, and writes them into the working folder.</summary>
    /// <param name="versionBatches">The batches, for example from <see cref="BatchBySize"/>; null downloads nothing.</param>
    /// <param name="cToken">Cancels waiting batches and stops running ones between files.</param>
    /// <param name="maxConcurrency">Most batches in flight at once; values below 1 mean 1.</param>
    /// <param name="log">Receives status lines; called from several threads at once.</param>
    /// <param name="onProgress">Receives increments (files queued for download, files skipped); called from several threads at once.</param>
    /// <returns>The combined summary of all batches.</returns>
    /// <remarks>See <see cref="ProcessVersionBatchAsync"/> for what happens to each file.</remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cToken"/> was cancelled.</exception>
    /// <example>
    /// <code>
    /// var versions = await PdmDownloadOperations.GetLatestVersionsAsync( entryIds ) ?? [];
    /// var batches = PdmDownloadOperations.BatchBySize( versions, OdooDefaults.Instance.DownloadBatchSize );
    /// var summary = await PdmDownloadOperations.ProcessDownloadsAsync( batches, token, OdooDefaults.Instance.ConcurrencySize, log );
    /// foreach( string failure in summary.Failed )
    ///     Console.WriteLine( failure );
    /// </code>
    /// </example>
    public static async Task<DownloadSummary> ProcessDownloadsAsync(
        IEnumerable<IEnumerable<HpVersion>> versionBatches,
        CancellationToken cToken,
        int maxConcurrency = 2,
        Action<StatusMessage, string>? log = null,
        Action<int, int>? onProgress = null,
        Action<long>? onBytesDownloaded = null)
    {
        DownloadSummary summary = new();
        int effectiveConcurrency = EffectiveConcurrency(maxConcurrency);
        if (effectiveConcurrency < Math.Max(1, maxConcurrency))
            log?.Invoke(StatusMessage.INFO, $"Memory pressure detected; throttling concurrent download batches from {maxConcurrency} to {effectiveConcurrency}.");

        // Serialize RPC payload reads when live RAM pressure spikes so concurrent base64/LOH buffers never exhaust RAM
        using SemaphoreSlim highMemoryGate = new(1, 1);

        await Parallel.ForEachAsync(
            versionBatches ?? [],
            new ParallelOptions { MaxDegreeOfParallelism = effectiveConcurrency, CancellationToken = cToken },
            async (batch, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                bool serialized = false;
                if (IsMemoryPressureHigh())
                {
                    await highMemoryGate.WaitAsync(ct);
                    serialized = true;
                }
                try
                {
                    await ProcessVersionBatchAsync(batch, ct, log, onProgress, summary, onBytesDownloaded);
                }
                finally
                {
                    if (serialized)
                        highMemoryGate.Release();
                }
            });

        return summary;
    }

    /// <summary>Downloads one batch of versions and writes the files that need it into the working folder.</summary>
    /// <param name="batchVersions">The versions; their <c>checksum</c>, <c>windows_complete_name</c> and <c>file_size</c> must be loaded.</param>
    /// <param name="cToken">Checked between server reads.</param>
    /// <param name="log">Receives a status line per file; may be null.</param>
    /// <param name="onProgress">Receives increments (files queued for download, files skipped); may be null.</param>
    /// <param name="summary">Collects the outcome; a new, discarded one when null.</param>
    /// <remarks>
    /// <para>
    /// A version is skipped when it has no checksum, when the local copy already matches it, or when the local copy holds
    /// changes that are in no vault version of the file (it is then listed in <see cref="DownloadSummary.KeptLocalChanges"/>
    /// and left untouched, also when the vault can't be asked).
    /// </para>
    /// <para>
    /// The rest are read in one call with a size-based timeout (<see cref="ContentsTimeoutMs"/>); files that don't arrive are
    /// retried one per call up to <see cref="MaxAttempts"/> in total, then recorded in <see cref="DownloadSummary.Failed"/>.
    /// Files are written through <see cref="LocalFileWriter.WriteAsync"/>, which leaves a copy not checked out to this user read-only.
    /// </para>
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cToken"/> was cancelled.</exception>
    public static async Task ProcessVersionBatchAsync(
        IEnumerable<HpVersion> batchVersions,
        CancellationToken cToken,
        Action<StatusMessage, string>? log = null,
        Action<int, int>? onProgress = null,
        DownloadSummary? summary = null,
        Action<long>? onBytesDownloaded = null)
    {
        summary ??= new();
        List<HpVersion> processVersions = [];
        List<(HpVersion Version, string LocalChecksum)> changedLocalCandidates = [];

        foreach (HpVersion version in batchVersions)
        {
            cToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(version.checksum) || version.checksum == "False")
            {
                log?.Invoke(StatusMessage.ERROR, $"Checksum not found for version: {version.name}");
                summary.AddSkipped();
                onProgress?.Invoke(0, 1); // increments: the UI adds these to its counters
                continue;
            }

            // Compute local checksum at most once (fast-path: missing file needs no hashing or history lookup)
            string? localPath = PwaPaths.ToAbsolute(version.windows_complete_name);
            string? localChecksum = localPath is not null && File.Exists(localPath)
                ? Checksums.OfFile(localPath)
                : null;

            if (localChecksum is { Length: > 0 } && localChecksum == version.checksum)
            {
                log?.Invoke(StatusMessage.FOUND, $"Skipping version download: {version.name}");
                summary.AddSkipped();
                onProgress?.Invoke(0, 1);
                continue;
            }

            if (localChecksum is not null)
            {
                changedLocalCandidates.Add((version, localChecksum));
                continue;
            }

            log?.Invoke(StatusMessage.PROCESSING, $"Downloading latest version: {Path.Combine(version.WinPathway ?? "", version.name ?? "")}");
            processVersions.Add(version);
        }

        // Batch-check all locally differing files in one RPC call instead of one SearchReadAsync per file
        if (changedLocalCandidates.Count > 0)
        {
            HashSet<string> knownChecksumsByPath = await FetchVaultChecksumsAsync(changedLocalCandidates);
            foreach (var (version, localChecksum) in changedLocalCandidates)
            {
                string key = $"{version.windows_complete_name}\0{localChecksum}";
                if (!knownChecksumsByPath.Contains(key))
                {
                    string shown = Path.Combine(version.WinPathway ?? "", version.name ?? "");
                    log?.Invoke(StatusMessage.WARNING,
                        $"Kept local changes to {shown}: the local file is not a version from the vault, so it was not overwritten (commit it, or delete the local copy to get the vault's)");
                    summary.KeptLocalChanges.Enqueue(shown);
                    summary.AddSkipped();
                    onProgress?.Invoke(0, 1);
                    continue;
                }

                log?.Invoke(StatusMessage.PROCESSING, $"Downloading latest version: {Path.Combine(version.WinPathway ?? "", version.name ?? "")}");
                processVersions.Add(version);
            }
        }

        if (processVersions.Count > 0)
            await DownloadAndWriteAsync(processVersions, cToken, log, summary, onProgress, onBytesDownloaded);
    }

    /// <summary>
    /// Batch-reads all known vault checksums for the given candidate files in a single RPC call so
    /// we can tell whether a differing local copy is an older vault version (safe to overwrite)
    /// or contains uncommitted local edits (must be kept).
    /// </summary>
    private static async Task<HashSet<string>> FetchVaultChecksumsAsync(List<(HpVersion Version, string LocalChecksum)> candidates)
    {
        HashSet<string> known = new(StringComparer.OrdinalIgnoreCase);
        string[] paths = [.. candidates.Select(c => c.Version.windows_complete_name).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];
        if (paths.Length == 0)
            return known;

        // Read checksum + windows_complete_name in one call; if the vault can't be asked, returns empty (safely keeping local files)
        Result<HpVersion[]> versions = await Repository<HpVersion>.SearchReadAsync(
            OdooDomain.Where("entry_id.windows_complete_name", Operators.In, paths),
            FieldSelection.Only([nameof(HpVersion.checksum), nameof(HpVersion.windows_complete_name)]));
        if (!versions.Ok || versions.Value is null)
            return known;

        foreach (HpVersion v in versions.Value)
        {
            if (v.windows_complete_name is { Length: > 0 } path && v.checksum is { Length: > 0 } checksum)
                known.Add($"{path}\0{checksum}");
        }
        return known;
    }

    /// <summary>
    /// Reads file contents and writes the files. The first attempt reads the whole batch in one
    /// call; whatever did not arrive is retried one file per call with a longer timeout.
    /// Files still missing after <see cref="MaxAttempts"/> are logged and added to the summary.
    /// </summary>
    private static async Task DownloadAndWriteAsync(
        List<HpVersion> versions,
        CancellationToken cToken,
        Action<StatusMessage, string>? log,
        DownloadSummary summary,
        Action<int, int>? onProgress,
        Action<long>? onBytesDownloaded)
    {
        Dictionary<int, HpVersion> pending = versions.Where(v => v.id is > 0).DistinctBy(v => v.id).ToDictionary(v => v.id!.Value);
        Dictionary<int, string> lastError = [];

        // large files come in pieces, each with its own retries (ContentTransfer), one file at a time
        foreach (HpVersion large in pending.Values.Where(v => ContentTransfer.IsLarge(v.file_size)).ToList())
        {
            cToken.ThrowIfCancellationRequested();
            pending.Remove(large.id!.Value);
            if (await DownloadLargeAsync(large, cToken, onBytesDownloaded) is { } error)
                Fail(large, error);
            else
                summary.AddDownloaded();
            onProgress?.Invoke(1, 0);
        }

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

                    long downloadedSize = got.file_contents?.Length ?? (got.file_size ?? 0);
                    if (downloadedSize > 0)
                        onBytesDownloaded?.Invoke(downloadedSize);

                    bool wroteOk = got.file_contents is not { Length: > 0 } || await LocalFileWriter.WriteAsync(got.ToHackFile());
                    // Release the downloaded byte[] immediately so LOH buffers can be reclaimed before the rest of the batch finishes
                    got.file_contents = null;

                    if (!wroteOk)
                    {
                        // a local write problem will not be fixed by downloading again
                        pending.Remove(id);
                        Fail(requested, "could not write the file locally");
                        onProgress?.Invoke(1, 0);
                        continue;
                    }

                    pending.Remove(id);
                    summary.AddDownloaded();
                    onProgress?.Invoke(1, 0);
                }
            }
        }

        foreach (HpVersion version in pending.Values)
        {
            Fail(version, lastError.GetValueOrDefault(version.id ?? 0, "unknown error"));
            onProgress?.Invoke(1, 0);
        }

        void Fail(HpVersion version, string reason)
        {
            string name = Path.Combine(version.WinPathway ?? "", version.name ?? $"version {version.id}");
            summary.Failed.Enqueue($"{name}: {reason}");
            log?.Invoke(StatusMessage.ERROR, $"Failed to download {name} after {MaxAttempts} attempt(s): {reason}");
        }
    }

    private static readonly string[] LargeFileFields = [.. ContentFields.Where(f => f != nameof(HpVersion.file_contents)), nameof(HpVersion.checksum)];

    /// <summary>Downloads one large file in pieces to a temporary file, checks it, and moves it into the working folder.</summary>
    /// <returns>Null when the file is in place; otherwise why not.</returns>
    private static async Task<string?> DownloadLargeAsync(HpVersion requested, CancellationToken cToken, Action<long>? onBytesDownloaded)
    {
        Result<HpVersion[]> read = await Repository<HpVersion>.ReadAsync([requested.id!.Value], FieldSelection.Only(LargeFileFields), OdooRpc.LongTimeout);
        if (read.Value is not [{ } version, ..])
            return read.Error ?? "the version could not be read";
        Result<string> temp = await ContentTransfer.DownloadToTempFileAsync(version, cToken, onBytesDownloaded);
        if (!temp.Ok)
            return temp.Error;
        if (await LocalFileWriter.WriteFromFileAsync(version.ToHackFile(contentsInFile: true), temp.Value!))
            return null;
        try { File.Delete(temp.Value!); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        // a local write problem will not be fixed by downloading again
        return "could not write the file locally";
    }
}
