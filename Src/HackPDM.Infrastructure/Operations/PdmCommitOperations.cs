using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Operations;

/// <summary>
/// Infrastructure service for PDM commit staging and directory creation.
/// Encapsulates server commits, version staging, and relationship linking.
/// </summary>
/// <example>
/// A typical check-in of a set of local files:
/// <code>
/// var (newFiles, newVersions, unchanged) = await PdmCommitOperations.FilterCommitHackFilesAsync( files, log );
/// HpPDMCommit commit = new() { node_by = OdooDefaults.Instance.MyNode?.id };
/// await commit.CreateCommitAsync();     // opens the hp.pdm.commit on the server
/// foreach( HackFile file in newFiles )
///     if( !await PdmCommitOperations.CommitRecordAsync( file, commit, inOdoo: false, log ) )
///         return;      // the commit was already cleared on the server
/// foreach( HackFile file in newVersions )
///     if( !await PdmCommitOperations.CommitRecordAsync( file, commit, inOdoo: true, log ) )
///         return;
/// var (committed, error) = await commit.ServerCommit();   // the server turns staged records into real ones
/// </code>
/// </example>
public static class PdmCommitOperations
{
    /// <summary>Makes sure the server has the directory chain a local file belongs in, creating missing directories.</summary>
    /// <param name="hack">The local file; its folder relative to the working folder gives the directory names.</param>
    /// <returns>The deepest directory (the file's own), or null when <paramref name="hack"/> is null.</returns>
    /// <remarks>Calls <c>HpDirectory.CreateNew</c> with the folder segments; a file outside the working folder maps to the root.</remarks>
    /// <exception cref="InvalidOperationException">The server created or returned no directory records.</exception>
    public static async Task<HpDirectory?> CreateDirectoriesAsync(HackFile? hack)
    {
        if (hack is null) return null;
        return (await HpDirectory.CreateNew(DirectorySegments(hack)))?.LastOrDefault()
            ?? throw new InvalidOperationException($"{HpDirectory.GetHpModel()} didn't create any records");
    }

    /// <summary>
    /// The server directories a file belongs in, from the working folder down ("Projects", "Parts");
    /// none (the root) for a file outside it.
    /// </summary>
    internal static string[] DirectorySegments(HackFile hack)
        => PwaPaths.LocalSegments(hack.FullPath is { Length: > 0 } full ? Path.GetDirectoryName(full) : hack.DirectoryName) ?? [];

    /// <summary>The entry's windows_complete_name on the server: "root\dir\sub\name".</summary>
    internal static string ServerWindowsName(HackFile hack)
        => PwaPaths.ToServerName([.. DirectorySegments(hack), hack.Name ?? ""]);

    /// <summary>
    /// Sorts files for a commit by looking each one up on the server (by name and windows_complete_name, not deleted).
    /// </summary>
    /// <param name="hackFiles">The local files to check; enumerated once.</param>
    /// <param name="log">Receives a status line for every file (queued, found, skipped, or error); may be null.</param>
    /// <returns>
    /// <c>LocalOnly</c>: files with no server entry, to be created. <c>RemoteExisting</c>: files whose entry is checked out
    /// to this user and whose contents changed, to get a new version. <c>UnchangedCheckedOut</c>: entries checked out to this
    /// user whose file matches the latest version's checksum; they get no new version, but committing them checks them back in.
    /// </returns>
    /// <remarks>
    /// Files are left out of all three lists, with a log line, when their type isn't allowed (server RestrictTypes setting),
    /// when they are unchanged but checked out to someone else (or nobody), or when they changed but aren't checked out to this user.
    /// Makes one or two server reads per file, one after another, and computes each file's checksum.
    /// </remarks>
    public static async Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut)> FilesNotInOdooSegmentedAsync(
        IEnumerable<HackFile> hackFiles,
        Action<StatusMessage, string>? log = null)
    {
        HackFile[] hackArr = [.. hackFiles];
        List<HackFile> hacks = [];
        List<HackFile> hacksFound = [];
        List<HpEntry> unchanged = [];

        for (int i = 0; i < hackArr.Length; i++)
        {
            string? filepath = hackArr[i].TypeExt?.ToLower();
            if (OdooDefaults.Instance.RestrictTypes is true && !OdooDefaults.Instance.ExtToType.ContainsKey(filepath ?? "-=-=-"))
            {
                log?.Invoke(StatusMessage.SKIP, $"not a file type the vault accepts, not committed: {hackArr[i].Name}");
                continue;
            }

            string filePath = PwaPaths.ToServerDirectory(hackArr[i].DirectoryName) ?? hackArr[i].DirectoryName ?? "";
            OdooDomain sameFile = OdooDomain.Where("name", hackArr[i].Name)
                .And("windows_complete_name", ServerWindowsName(hackArr[i]))
                .And("deleted", false);
            // a plain search_read: smart_read returned many2one fields as {id, name, model} maps,
            // so checkout_user never matched and a checked-out file could not be committed
            HpEntry? entry = (await HpEntry.GetRecordsBySearchAsync(
                searchFilter: sameFile,
                includedFields: [nameof(HpEntry.id), nameof(HpEntry.name), nameof(HpEntry.dir_id), nameof(HpEntry.checkout_user), nameof(HpEntry.latest_version_id), nameof(HpEntry.windows_complete_name)]))?.FirstOrDefault();

            if (entry is not null and { id: not null and not 0 })
            {
                bool mine = entry.checkout_user?.id == OdooDefaults.Instance.OdooId;
                // an unchanged file has nothing to commit, whoever has it checked out (e.g. a part that
                // came along with its assembly); one checked out to this user is checked back in
                if (await LatestChecksumAsync(entry) is { Length: > 0 } latest
                    && string.Equals(latest, hackArr[i].GetChecksum(), StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke(StatusMessage.SKIP, mine
                        ? $"unchanged since the latest version, checked in without a new version: {filePath}"
                        : $"unchanged since the latest version, nothing to commit: {filePath}");
                    if (mine)
                        unchanged.Add(entry);
                    continue;
                }
                if (mine)
                {
                    log?.Invoke(StatusMessage.FOUND, $"entry found remotely for: {filePath}");
                    hacksFound.Add(hackArr[i]);
                    continue;
                }
                log?.Invoke(StatusMessage.ERROR, $"entry found remotely but is not checked out to current user: {filePath}");
                continue;
            }

            log?.Invoke(StatusMessage.INFO, $"Queued commit for {hackArr[i].Name} (Checksum: {hackArr[i].Checksum}) for: {filePath}");
            hacks.Add(hackArr[i]);
        }
        return ([.. hacks], [.. hacksFound], [.. unchanged]);
    }

    /// <summary>
    /// Stages one local file in an open commit: its directories, its entry (new files only), a new version with the file's
    /// contents and preview, and for SolidWorks files the version's references and custom properties.
    /// </summary>
    /// <param name="hack">The local file to commit; null returns false.</param>
    /// <param name="commit">The open hp.pdm.commit the records are staged under.</param>
    /// <param name="inOdoo">True when the file already has a server entry (from <c>RemoteExisting</c>), so a version is staged for that entry instead of a new entry.</param>
    /// <param name="log">Receives progress and error lines; may be null.</param>
    /// <param name="onRecordStaged">Called once when the file has been staged successfully (for progress counting).</param>
    /// <returns>True when everything was staged; false when the file is missing or any step failed.</returns>
    /// <remarks>
    /// <para>
    /// Side effect on failure: the whole commit is cleared on the server (<c>hp.pdm.commit.clear_commit</c>), discarding
    /// what other files already staged in it, so callers should stop at the first false.
    /// </para>
    /// <para>
    /// A SolidWorks file whose references can't be read is still committed, without relationships or properties, and a
    /// warning is logged. Nothing is checked in or written to the server's real tables until <c>commit.ServerCommit()</c> runs.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The server created no directory records for the file's folder.</exception>
    public static async Task<bool> CommitRecordAsync(
        HackFile? hack,
        HpPDMCommit commit,
        bool inOdoo = false,
        Action<StatusMessage, string>? log = null,
        Action? onRecordStaged = null)
    {
        if (hack is null)
            return false;
        if (hack.FullPath is null || !File.Exists(hack.FullPath))
        {
            log?.Invoke(StatusMessage.ERROR, $"{hack.Name}: the file is no longer there ({hack.FullPath}); nothing was committed");
            await commit.ServerClear();
            return false;
        }

        HpDirectory? dir = await CreateDirectoriesAsync(hack);

        HpEntry entry = CommitStaging.NewEntry(hack, dir?.id ?? 0, commit) ?? new();
        if (inOdoo)
        {
            entry = await CommitStaging.FindRemoteEntryAsync(entry) ?? entry;
        }

        HpRecordStaged? versionStaged = null;
        if (entry.returnType is EntryReturnType.GotExisting)
        {
            log?.Invoke(StatusMessage.FOUND, "existing entry found...");
            log?.Invoke(StatusMessage.PROCESSING, "staging local version...");
            versionStaged = await CommitStaging.StageVersionAsync(hack, entry, commit.id ?? 0);
        }
        if (entry.returnType is EntryReturnType.Staged)
        {
            HpRecordStaged? entryStaged = await CommitStaging.StageEntryAsync(entry);
            log?.Invoke(StatusMessage.PROCESSING, "staging local entry...");
            log?.Invoke(StatusMessage.PROCESSING, "staging local version...");
            versionStaged = entryStaged is null ? null : await CommitStaging.StageVersionAsync(hack, entryStaged);
        }
        if (entry.returnType is EntryReturnType.Failed)
        {
            await commit.ServerClear();
            log?.Invoke(StatusMessage.ERROR, $"Error creating entry {entry.name}. Rolling back records...");
            return false;
        }

        try
        {
            if (versionStaged is not { id: not 0 })
                throw new ArgumentException("Version staging failed, Version is null or has an id of 0");

            if (!CanReadReferences(hack, log))
            {
                onRecordStaged?.Invoke();
                return true;
            }

            log?.Invoke(StatusMessage.PROCESSING, "staging local version relationships...");
            if (!await CommitStaging.StageRelationshipsAsync(versionStaged))
                throw new ArgumentException($"Failed to stage version relationships for {versionStaged.payload?.GetValueOrDefault("name")}");

            log?.Invoke(StatusMessage.PROCESSING, "staging local version properties ...");
            if (!await CommitStaging.StagePropertiesAsync(versionStaged))
                throw new ArgumentException($"Failed to stage version properties for {versionStaged.payload?.GetValueOrDefault("name")}");

            onRecordStaged?.Invoke();
        }
        catch (Exception e)
        {
            await commit.ServerClear();
            Debug.WriteLine(e.Message);
            log?.Invoke(StatusMessage.ERROR, e.Message);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Whether a SolidWorks file's references and properties can be read. One that can't (damaged,
    /// or a format the reader doesn't know) is still committed, without them, rather than failing
    /// the whole commit.
    /// </summary>
    private static bool CanReadReferences(HackFile hack, Action<StatusMessage, string>? log)
    {
        if (!SwDocumentTypes.IsSolidWorksExtension(hack.TypeExt) || hack.FullPath is null)
            return true;
        try
        {
            SldWrks.SolidWorksUtil.DocMgr?.GetDependencies(hack.FullPath);
            return true;
        }
        catch (Exception e)
        {
            log?.Invoke(StatusMessage.WARNING, $"{hack.Name}: references and properties can't be read ({e.Message}); committing the file without them");
            return false;
        }
    }

    private static async Task<string?> LatestChecksumAsync(HpEntry entry)
    {
        if (entry.latest_version_id?.id is not int versionId || versionId == 0)
            return null;
        return (await HpVersion.GetRecordsByIdsAsync([versionId], includedFields: [nameof(HpVersion.checksum)]))?.FirstOrDefault()?.checksum;
    }

    /// <summary>
    /// The server's entry name filters (hp.entry.name.filter) as one regex, matched against file
    /// names as hp_pdm_commit.py does; null when there are none (an empty pattern would match every file).
    /// </summary>
    private static Regex? EntryFilter()
    {
        string[] patterns = [.. (OdooDefaults.Instance?.EntryFilterPatterns ?? []).Where(p => !string.IsNullOrEmpty(p))];
        return patterns.Length == 0 ? null : new Regex(string.Join("|", patterns), RegexOptions.IgnoreCase);
    }

    /// <summary>Checks whether the server would refuse a file of this name (a SolidWorks "~$" lock file, say).</summary>
    /// <param name="name">The file name (not the path); null is treated as "".</param>
    /// <returns>True when any of the server's hp.entry.name.filter patterns match (case-insensitive); false when there are no patterns.</returns>
    /// <remarks>The regex is rebuilt from <see cref="OdooDefaults.EntryFilterPatterns"/> on every call.</remarks>
    /// <exception cref="ArgumentException">A server filter pattern is not a valid .NET regular expression.</exception>
    public static bool IsFilteredName(string? name) => EntryFilter() is { } regex && regex.IsMatch(name ?? "");

    /// <summary>
    /// Drops files the server's entry name filters reject, then sorts the rest with <see cref="FilesNotInOdooSegmentedAsync"/>.
    /// This is the entry point for deciding what a commit will do.
    /// </summary>
    /// <param name="hackFiles">The local files picked for the commit.</param>
    /// <param name="log">Receives a status line per file; may be null. Filtered files are dropped without a log line.</param>
    /// <returns>The same three lists as <see cref="FilesNotInOdooSegmentedAsync"/>.</returns>
    /// <exception cref="ArgumentException">A server filter pattern is not a valid .NET regular expression.</exception>
    public static async Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting, HpEntry[] UnchangedCheckedOut)> FilterCommitHackFilesAsync(
        IEnumerable<HackFile> hackFiles,
        Action<StatusMessage, string>? log = null)
    {
        Regex? regex = EntryFilter();
        List<HackFile> hacks = [];
        foreach (HackFile hack in hackFiles)
        {
            if (regex is null || !regex.IsMatch(hack.Name ?? ""))
            {
                hacks.Add(hack);
            }
        }
        return await FilesNotInOdooSegmentedAsync(hacks, log);
    }
}
