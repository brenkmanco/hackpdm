using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using HackPDM.ClientUtils;
using HackPDM.Extensions.General;
using HackPDM.Extensions.Odoo;
using HackPDM.Forms.Hack;

using HackPDM.Forms.Settings;
using HackPDM.Hack;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Src.ClientUtils.Types;

using MessageBox = System.Windows.Forms.MessageBox;

namespace HackPDM.Odoo.Methods;

public static class Commit
{
    internal static async Task				            CommitInternal			(ArrayList entryIDs, IEnumerable<HackFile> hackFiles)
    {
        Notifier.CancelCheckLoop();
        HackFileManager.Dialog = new StatusDialog();
        await HackFileManager.Dialog?.ShowWait("Commit Files");

        HpEntry[] entries = HpEntry.GetRecordsByIds(entryIDs, includedFields: ["latest_version_id"]);
        HpEntry[] allEntries = null;

        if (entries is not null && entries.Length > 0)
        {
            ArrayList newIds = await HpEntry.GetEntryList([.. entries.Select(e => e.latest_version_id)]);
            newIds.AddRange(entryIDs);
            newIds = newIds.ToHashSet<int>().ToArrayList();
            allEntries = HpEntry.GetRecordsByIds(newIds, excludedFields: ["type_id", "cat_id", "checkout_node"], insertFields: ["directory_complete_name"]);
        }

        await AsyncHelper.AsyncRunner(() => Async_Commit((allEntries, hackFiles.ToList())), "Commit Files", HackFileManager.statusToken);
        Notifier.FileCheckLoop();
    }
    internal static async Task                          Async_Commit            (ValueTuple<HpEntry[], List<HackFile>> arguments)
    {
        object lockObject = new();
        var sd = StatusData.StaticData;
        // section for checking if the existing remote file already has a version with the same checksum 
        // or possibly an entry that has a newer version from that which is downloaded locally

        ConcurrentBag<HpEntry> entries = arguments.Item1.ToConcurrentBag();
        ConcurrentSet<HackFile> hackFiles = arguments.Item2;


        // testing filter hacks..
        if (entries is not null && entries.Count > 0)
        {
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Filtering out uncommitable entries found remotely");
            entries = await FilterCommitEntries(entries);
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.INFO, $"Able to commit ({entries.Count}) remote files");
        }
        else
        {
            entries = [];
        }

        // section for checking if hack files have a checksum that matches the fullpath
        if (hackFiles is not null && hackFiles.Count > 0)
        {
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Filtering out uncommitable entries found locally");
            hackFiles = await FilterCommitHackFiles(hackFiles);
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.INFO, $"Able to commit ({hackFiles.Count}) local only files");
        }
        else
        {
            hackFiles = [];
        }

        List<HpVersion> versions = new(entries.Count + hackFiles.Count);
		
        var datas = new List<(HackFile, HpEntry, HashedValueStoring)>(entries.Count);

        entries = entries.TakeOutLatest(out IEnumerable<HpEntry> latestRecommit).ToConcurrentBag();
        bool willRecommit = latestRecommit.MessageToRecommit();

        while (entries.TryTake(out HpEntry entry))
        {
            string entryDir = HpDirectory.ConvertToWindowsPath(entry.HashedValues["directory_complete_name"] as string, false);
            HackFile hack = HackFile.GetFromPath(Path.Combine(HackDefaults.PwaPathAbsolute, entryDir, entry.name));
            datas.Add((hack, entry, HashedValueStoring.None));
            //HpVersion newVersion = await OdooDefaults.CreateNewVersion(hack, entry);
            //versions.Add(newVersion);
        }

        var versionBatches = Help.BatchList(datas, OdooDefaults.DownloadBatchSize);

        sd.ProcessCounter = 0;
        sd.SkipCounter = 0;
        sd.MaxCount = entries.Count;
        if (versionBatches.Count > 0) HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Commiting new versions to database...");
        else HackFileManager.Dialog?.AddStatusLine(StatusMessage.INFO, $"No new remote versions to commit for existing entries to the database...");

		while (hackFiles.TryTake(out HackFile result))
		{
			(EntryReturnType entryReturn, HpVersion? newVersion) = await OdooDefaults.ConvertHackFile(result);
			if (entryReturn is not EntryReturnType.GotExisting and not EntryReturnType.Created && (entryReturn != EntryReturnType.InvalidType || OdooDefaults.RestrictTypes is true))
			{ HackFileManager.Dialog?.AddStatusLine(StatusMessage.ERROR, $"unable to commit file: {result.FullPath}"); continue; }
			else
			{
				lock (lockObject)
				{
					HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"commiting new version for local only file: {result.FullPath}");
				}
			}
			versions.Add(newVersion);
		}
		for (int i = 0; i < versionBatches.Count; i++)
        {
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Commiting batch {i + 1}/{versionBatches.Count}...");

            HpVersion[] vbatch = await HpVersion.CreateAllNew([.. versionBatches[i]]);
            versions.AddRange(vbatch);

            sd.ProcessCounter += versionBatches[i].Count;
            HackFileManager.Dialog?.SetProgressBar((sd.SkipCounter + sd.ProcessCounter) / 3, sd.MaxCount);
        }

        // create new parent, child hp_version_relationship's for versions
        if (versions.Count < 1)
        {
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.INFO, $"No new version relationship commits for database...");
        }
        else
        {
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Commiting new version relationship commits to database...");
            HpVersionRelationship.Create([.. versions]);
        }
        HackFileManager.Dialog?.SetProgressBar(2 * (sd.MaxCount) / 3, sd.MaxCount);

        if (versions.Count < 1)
        {
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.INFO, $"No new version property commits for database...");
        }
        else
        {
            HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Commiting new version property commits to database...");
            HpVersionProperty.Create([.. versions]);
        }
        HackFileManager.Dialog?.SetProgressBar(sd.MaxCount, sd.MaxCount);

		var hackFM = ISingletonPage<HackFileManager>.Singleton;
		hackFM?.RestartTree();
		hackFM?.RestartEntries();

		MessageBox.Show($"Completed!");
    }
    internal static async Task<ConcurrentBag<HpEntry>>  FilterCommitEntries     (ConcurrentBag<HpEntry> entries)
    {
        if (entries == null || entries.Count < 1) return null;

        string[] excludedFields = ["preview_image", "attachment_id", "file_modify_stamp", "file_size", "node_id", "file_contents"];
        ConcurrentBag<Task<HpEntry>> tasks = [];
        object lockObject = new();

        while (entries.TryTake(out HpEntry entry))
        {
            Task<HpEntry?> entryTask = Task.Run(() =>
            {
                // true means that this entry is checked out
                if (entry.checkout_user != OdooDefaults.OdooId)
                {
                    if (entry.checkout_user == 0)
                    {
                        lock (lockObject)
                        {
                            HackFileManager.Dialog?.AddStatusLine(StatusMessage.ERROR, $"entry is not checked out to you: {entry.name} ({entry.Id})");
                        }
                    }
                    else
                    {
                        lock (lockObject)
                        {
                            string userString = OdooDefaults.IdToUser.TryGetValue(entry.checkout_user ?? 0, out HpUser user) ? $"{user.name} (id: {user.Id}))" : $"(id: {entry.checkout_user})";
                            HackFileManager.Dialog?.AddStatusLine(StatusMessage.ERROR, $"checked out to user {userString}: {entry.name} ({entry.Id}) ");
                        }
                    }
                    return null;
                }
                // can eventually just change this to get the list of id's available instead
                HpVersion latestVersion = HpEntry.GetRelatedRecordByIds<HpVersion>([entry.Id], "latest_version_id", excludedFields).FirstOrDefault();

                if (latestVersion is null) return null;

                // check if latest version checksum matches local file
                if (HackFile.GetLocalVersion(latestVersion, out HackFile hack))
                {
                    lock (lockObject)
                    {
                        HackFileManager.Dialog?.AddStatusLine(StatusMessage.WARNING, $"Latest remote version {latestVersion.name} matches local version");
                    }
                    entry.IsLatest = true;
                    // return null;
                    return entry;
                }

                if (hack?.Exists is null or false)
                {
                    lock (lockObject)
                    {
                        HackFileManager.Dialog?.AddStatusLine(StatusMessage.ERROR, $"{latestVersion.name} has no local version");
                    }

                    return null;
                }

                lock (lockObject)
                {
                    HackFileManager.Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"commiting {latestVersion.name}");
                }
                return entry;
            });
            await entryTask;
            tasks.Add(entryTask);
        }
        await Task.WhenAll(tasks);
        return tasks.SkipSelect(
            taskPredicate =>
            {
                return taskPredicate.Result == null;
            },
            taskSelect => taskSelect.Result).ToConcurrentBag();
    }
    internal static async Task<ConcurrentSet<HackFile>> FilterCommitHackFiles   (ConcurrentSet<HackFile> hackFiles)
    {
        List<Task<HackFile>> tasks = [];
        object lockObject = new();
        string combinedPattern = string.Join("|", OdooDefaults.EntryFilterPatterns);
        var regex = new Regex(combinedPattern, RegexOptions.IgnoreCase);
        //string[] filePaths = hackFiles.Select(hack => hack.FullPath).ToArray();

        List<HackFile> hacks = new();
        foreach (HackFile hack in hackFiles)
        {
            regex = new Regex(combinedPattern, RegexOptions.IgnoreCase);
            if (!regex.IsMatch($".{hack.TypeExt.ToLower()}"))
            {
                hacks.Add(hack);
            }
        }
        HackFile[] files = await FileOperations.FilesNotInOdoo(hacks);
        return files;
    }
}