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
public static class PdmCommitOperations
{
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
    /// Splits the files into new ones (<c>LocalOnly</c>), new versions of entries checked out to this user
    /// (<c>RemoteExisting</c>), and entries checked out to this user whose file did not change
    /// (<c>UnchangedCheckedOut</c>): those get no new version, but committing them checks them back in.
    /// </summary>
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
                continue;

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

    public static async Task<bool> CommitRecordAsync(
        HackFile? hack,
        HpPDMCommit commit,
        bool inOdoo = false,
        Action<StatusMessage, string>? log = null,
        Action? onRecordStaged = null)
    {
        if (hack is null)
            return false;

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

    /// <summary>Whether the server would refuse a file of this name (a SolidWorks "~$" lock file, say).</summary>
    public static bool IsFilteredName(string? name) => EntryFilter() is { } regex && regex.IsMatch(name ?? "");

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
