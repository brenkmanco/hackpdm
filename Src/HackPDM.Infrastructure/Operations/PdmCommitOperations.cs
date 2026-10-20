using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        ArrayList? paths = hack.RelativePath?.Split<ArrayList>("\\", StringSplitOptions.RemoveEmptyEntries);
        paths?.RemoveAt(paths.Count - 1);
        return (await HpDirectory.CreateNew(paths))?.LastOrDefault()
            ?? throw new InvalidOperationException($"{HpDirectory.GetHpModel()} didn't create any records");
    }

    public static async Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting)> FilesNotInOdooSegmentedAsync(
        IEnumerable<HackFile> hackFiles,
        Action<StatusMessage, string>? log = null)
    {
        HackFile[] hackArr = [.. hackFiles];
        List<HackFile> hacks = [];
        List<HackFile> hacksFound = [];

        for (int i = 0; i < hackArr.Length; i++)
        {
            string? filepath = hackArr[i].TypeExt?.ToLower();
            if (OdooDefaults.Instance.RestrictTypes is true && !OdooDefaults.Instance.ExtToType.ContainsKey(filepath ?? "-=-=-"))
                continue;

            string filePath = FileOperations.WindowsToOdooPath(hackArr[i].RelativePath);
            ArrayList arrList =
            [
                new ArrayList() { "name", "=", hackArr[i].Name },
                new ArrayList() { "windows_complete_name", "=", (@"root\" + hackArr[i].RelativePath) },
            ];
            HpEntry? entry = (await HpEntry.GetRecordsBySmartSearchAsync(
                searchFilter: arrList,
                includedFields: [nameof(HpEntry.id), nameof(HpEntry.name), nameof(HpEntry.dir_id), nameof(HpEntry.checkout_user)],
                insertFields: ["version_ids.checksum"]))?.FirstOrDefault();

            if (entry is not null and { id: not null and not 0 })
            {
                if (entry.checkout_user?.id == OdooDefaults.Instance.OdooId)
                {
                    log?.Invoke(StatusMessage.FOUND, $"entry found remotely for: {filePath}");
                    hacksFound.Add(hackArr[i]);
                    continue;
                }
                else
                {
                    log?.Invoke(StatusMessage.ERROR, $"entry found remotely but is not checked out to current user: {filePath}");
                    continue;
                }
            }

            log?.Invoke(StatusMessage.INFO, $"Queued commit for {hackArr[i].Name} (Checksum: {hackArr[i].Checksum}) for: {filePath}");
            hacks.Add(hackArr[i]);
        }
        return ([.. hacks], [.. hacksFound]);
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

        HpEntry entry = await HpEntry.ConvertToEntry(hack, dir?.id ?? 0, commit) ?? new();
        if (inOdoo)
        {
            entry = await entry?.FindRemoteEntry() ?? entry;
        }

        HpRecordStaged? versionStaged = null;
        if (entry.returnType is EntryReturnType.GotExisting)
        {
            log?.Invoke(StatusMessage.FOUND, "existing entry found...");
            log?.Invoke(StatusMessage.PROCESSING, "staging local version...");
            versionStaged = await HpVersion.StageVersion(hack, entry, commit.id ?? 0);
        }
        if (entry.returnType is EntryReturnType.Staged)
        {
            HpRecordStaged? entryStaged = await entry?.StageEntry();
            log?.Invoke(StatusMessage.PROCESSING, "staging local entry...");
            log?.Invoke(StatusMessage.PROCESSING, "staging local version...");
            versionStaged = await HpVersion.StageVersion(hack, entryStaged);
        }
        if (entry.returnType is EntryReturnType.Failed)
        {
            commit.ServerClear();
            log?.Invoke(StatusMessage.ERROR, $"Error creating entry {entry.name}. Rolling back records...");
            return false;
        }

        try
        {
            if (versionStaged is not { id: not 0 })
                throw new ArgumentException("Version staging failed, Version is null or has an id of 0");

            log?.Invoke(StatusMessage.PROCESSING, "staging local version relationships...");
            if (!await HpVersionRelationship.StageRelationshipRecords(versionStaged))
                throw new ArgumentException($"Failed to stage version relationships for {versionStaged.payload?["name"]}");

            log?.Invoke(StatusMessage.PROCESSING, "staging local version properties ...");
            if (!await HpVersionProperty.StagePropertyRecords(versionStaged))
                throw new ArgumentException($"Failed to stage version properties for {versionStaged.payload?["name"]}");

            onRecordStaged?.Invoke();
        }
        catch (Exception e)
        {
            commit.ServerClear();
            Debug.WriteLine(e.Message);
            log?.Invoke(StatusMessage.ERROR, e.Message);
            return false;
        }
        return true;
    }

    public static async Task<(HackFile[] LocalOnly, HackFile[] RemoteExisting)> FilterCommitHackFilesAsync(
        IEnumerable<HackFile> hackFiles,
        Action<StatusMessage, string>? log = null)
    {
        string combinedPattern = string.Join("|", OdooDefaults.Instance?.EntryFilterPatterns ?? []);
        var regex = new Regex(combinedPattern, RegexOptions.IgnoreCase);

        List<HackFile> hacks = [];
        foreach (HackFile hack in hackFiles)
        {
            if (!regex.IsMatch($".{hack.TypeExt?.ToLower()}"))
            {
                hacks.Add(hack);
            }
        }
        return await FilesNotInOdooSegmentedAsync(hacks, log);
    }
}
