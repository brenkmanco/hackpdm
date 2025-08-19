using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HackPDM.ClientUtils;
using HackPDM.Extensions.General;
using HackPDM.Extensions.Odoo;
using SD = HackPDM.Forms.Settings.StatusData;
using HackPDM.Forms.Settings;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;

namespace HackPDM.OdooClient.OdooMethods
{
    public static class Commit
    {
        internal static async Task				            CommitInternal			(ArrayList entryIDs, IEnumerable<HackFile> hackFiles)
        {
            Notifier.CancelCheckLoop();
            StatusDialog.Dialog = new StatusDialog();
            await StatusDialog.Dialog.ShowWait("Commit Files");

            HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, includedFields: ["latest_version_id"]);
            HpEntry[] allEntries = null;

            if (entries is not null && entries.Length > 0)
            {
                ArrayList newIds = await HpEntry.GetEntryList([.. entries.Select(e => e.latest_version_id)]);
                newIds.AddRange(entryIDs);
                newIds = newIds.ToHashSet<int>().ToArrayList();
                allEntries = HpEntry.GetRecordsByIDS(newIds, excludedFields: ["type_id", "cat_id", "checkout_node"], insertFields: ["directory_complete_name"]);
            }

            await AsyncHelper.AsyncRunner(() => Async_Commit((allEntries, hackFiles.ToList())), "Commit Files");
            Notifier.FileCheckLoop();
        }
        internal static async Task                          Async_Commit            (ValueTuple<HpEntry[], List<HackFile>> Arguments)
        {
            object lockObject = new();
            var SD = StatusData.StaticData;
            // section for checking if the existing remote file already has a version with the same checksum 
            // or possibly an entry that has a newer version from that which is downloaded locally

            ConcurrentBag<HpEntry> entries = Arguments.Item1.ToConcurrentBag();
            ConcurrentSet<HackFile> hackFiles = Arguments.Item2;


            // testing filter hacks..
            if (entries is not null && entries.Count > 0)
            {
                StatusDialog.Dialog.AddStatusLine(StatusMessage.PROCESSING, $"Filtering out uncommitable entries found remotely");
                entries = await FilterCommitEntries(entries);
                StatusDialog.Dialog.AddStatusLine(StatusMessage.INFO, $"Able to commit ({entries.Count}) remote files");
            }
            else
            {
                entries = [];
            }

            // section for checking if hack files have a checksum that matches the fullpath
            if (hackFiles is not null && hackFiles.Count > 0)
            {
                StatusDialog.Dialog.AddStatusLine(StatusMessage.PROCESSING, $"Filtering out uncommitable entries found locally");
                hackFiles = await FilterCommitHackFiles(hackFiles);
                StatusDialog.Dialog.AddStatusLine(StatusMessage.INFO, $"Able to commit ({hackFiles.Count}) local only files");
            }
            else
            {
                hackFiles = [];
            }

            List<HpVersion> versions = new(entries.Count + hackFiles.Count);

            while (hackFiles.TryTake(out HackFile result))
            {
                HpVersion newVersion = await OdooDefaults.ConvertHackFile(result);
                versions.Add(newVersion);
            }

            var datas = new List<(HackFile, HpEntry, HashedValueStoring)>(entries.Count);

            entries = entries.TakeOutLatest(out IEnumerable<HpEntry> latestRecommit).ToConcurrentBag();
            bool willRecommit = latestRecommit.MessageToRecommit();

            while (entries.TryTake(out HpEntry entry))
            {
                string entry_dir = HpDirectory.ConvertToWindowsPath(entry.HashedValues["directory_complete_name"] as string, false);
                HackFile hack = HackFile.GetFromPath(Path.Combine(HackDefaults.PWAPathAbsolute, entry_dir, entry.name));
                datas.Add((hack, entry, HashedValueStoring.None));
                //HpVersion newVersion = await OdooDefaults.CreateNewVersion(hack, entry);
                //versions.Add(newVersion);
            }

            var versionBatches = Utils.BatchList(datas, OdooDefaults.DownloadBatchSize);


            SD.ProcessCounter = 0;
            SD.SkipCounter = 0;
            SD.MaxCount = entries.Count;
            if (versionBatches.Count > 0) StatusDialog.Dialog.AddStatusLine(StatusMessage.PROCESSING, $"Commiting new versions to database...");
            else StatusDialog.Dialog.AddStatusLine(StatusMessage.INFO, $"No new remote versions to commit for existing entries to the database...");
            for (int i = 0; i < versionBatches.Count; i++)
            {
                StatusDialog.Dialog.AddStatusLine(StatusMessage.PROCESSING, $"Commiting batch {i + 1}/{versionBatches.Count}...");

                HpVersion[] vbatch = await HpVersion.CreateAllNew([.. versionBatches[i]]);
                versions.AddRange(vbatch);

                SD.ProcessCounter += versionBatches[i].Count;
                StatusDialog.Dialog.SetProgressBar((SD.SkipCounter + SD.ProcessCounter) / 3, SD.MaxCount);
            }

            // create new parent, child hp_version_relationship's for versions
            if (versions.Count < 1)
            {
                StatusDialog.Dialog.AddStatusLine(StatusMessage.INFO, $"No new version relationship commits for database...");
            }
            else
            {
                StatusDialog.Dialog.AddStatusLine(StatusMessage.PROCESSING, $"Commiting new version relationship commits to database...");
                HpVersionRelationship.Create([.. versions]);
            }
            StatusDialog.Dialog.SetProgressBar(2 * (SD.MaxCount) / 3, SD.MaxCount);

            if (versions.Count < 1)
            {
                StatusDialog.Dialog.AddStatusLine(StatusMessage.INFO, $"No new version property commits for database...");
            }
            else
            {
                StatusDialog.Dialog.AddStatusLine(StatusMessage.PROCESSING, $"Commiting new version property commits to database...");
                HpVersionProperty.Create([.. versions]);
            }
            StatusDialog.Dialog.SetProgressBar(SD.MaxCount, SD.MaxCount);

            MessageBox.Show($"Completed!");
            HackFileManager.Singleton.RestartEntries();
        }
        internal static async Task<ConcurrentBag<HpEntry>>  FilterCommitEntries     (ConcurrentBag<HpEntry> entries)
        {
            if (entries == null || entries.Count < 1) return null;

            string[] excludedFields = ["preview_image", "attachment_id", "file_modify_stamp", "file_size", "node_id", "file_contents"];
            ConcurrentBag<Task<HpEntry>> tasks = [];
            object lockObject = new();

            while (entries.TryTake(out HpEntry entry))
            {
                Task<HpEntry> entryTask = Task.Run(() =>
                {
                    // true means that this entry is checked out
                    if (entry.checkout_user != OdooDefaults.OdooID)
                    {
                        if (entry.checkout_user == 0)
                        {
                            lock (lockObject)
                            {
                                StatusDialog.Dialog.AddStatusLine(StatusMessage.ERROR, $"entry is not checked out to you: {entry.name} ({entry.ID})");
                            }
                        }
                        else
                        {
                            lock (lockObject)
                            {
                                string userString = OdooDefaults.IDToUser.TryGetValue(entry.checkout_user ?? 0, out HpUser user) ? $"{user.name} (id: {user.ID}))" : $"(id: {entry.checkout_user})";
                                StatusDialog.Dialog.AddStatusLine(StatusMessage.ERROR, $"checked out to user {userString}: {entry.name} ({entry.ID}) ");
                            }
                        }
                        return null;
                    }
                    // can eventually just change this to get the list of id's available instead
                    HpVersion latestVersion = HpEntry.GetRelatedRecordByIDS<HpVersion>([entry.ID], "latest_version_id", excludedFields).FirstOrDefault();

                    if (latestVersion is null) return null;

                    // check if latest version checksum matches local file
                    if (HackFile.GetLocalVersion(latestVersion, out HackFile hack))
                    {
                        lock (lockObject)
                        {
                            StatusDialog.Dialog.AddStatusLine(StatusMessage.WARNING, $"Latest remote version {latestVersion.name} matches local version");
                        }
                        entry.IsLatest = true;
                        // return null;
                        return entry;
                    }

                    if (!hack.Exists)
                    {
                        lock (lockObject)
                        {
                            StatusDialog.Dialog.AddStatusLine(StatusMessage.ERROR, $"{latestVersion.name} has no local version");
                        }

                        return null;
                    }

                    lock (lockObject)
                    {
                        StatusDialog.Dialog.AddStatusLine(StatusMessage.PROCESSING, $"commiting {latestVersion.name}");
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
}
