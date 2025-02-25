using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Octokit;

namespace HackPDM.HackClient
{
	internal class HackUpdater
	{
		const long repoID = 28426033L;
		const string branchName = "justinOdooIntegration";
		private static (string, string) CurrentVersion()
		{
			// git log --format="%H | %cd" --date=iso
			var assembly = Assembly.GetExecutingAssembly();
			
			var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (informationalVersion != null)
            {
                var parts = informationalVersion.Split([.. ", "]);
                var commitHash = parts[1];
                var commitDate = $"{parts[4]} {parts[5]} {parts[6]}";

                //MessageBox.Show($"Commit Hash: {commitHash}");
                //MessageBox.Show($"Commit Date: {commitDate}");
				return (commitHash, commitDate);
            }
            else
            {
                MessageBox.Show("Commit information not found.");
            }
			return (null, null);
		}
		private async static Task<Branch> GetBranchRepo(long repositoryID, string repoBranchName)
		{
			var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("hackpdm"));
			return await ghClient.Repository.Branch.Get(repositoryID, repoBranchName);
		}
		private static bool IsLatestVersion (Branch branch, string commitHash)
		{
			var latestCommit = branch.Commit.Sha;
			
			return latestCommit == commitHash;
		}
		public async static void EnsureUpdated()
		{
			var info = CurrentVersion();
			var ghBranch = await GetBranchRepo(repoID, branchName);
			if (!IsLatestVersion(ghBranch, info.Item1))
			{
				 if (MessageBox.Show($"Latest version: {ghBranch.Commit.Sha}, doesn't match your version: {info.Item1}\n" +
				 $"Would you like to navigate to {ghBranch.Commit.Url}?", 
				 "Versions", 
				 MessageBoxButtons.YesNoCancel) == DialogResult.Yes)
				 {
					UpdaterProcess(ghBranch);
				 }
				 throw new Exception("Update to latest version");
			}
		}
		public static void UpdaterProcess(Branch branch)
		{
			Process.Start("explorer.exe", branch.Commit.Url);
		}
	}
}
