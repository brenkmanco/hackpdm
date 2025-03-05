using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System;
using System.Deployment;

using Octokit;

using Application = System.Windows.Forms.Application;

namespace HackPDM.HackClient
{
	internal class HackUpdater
	{
		const long repoID = 28426033L;
		const string branchName = "justinOdooIntegration";
		private static Version CurrentVersion()
		{
			
			return Assembly.GetExecutingAssembly().GetName().Version;
		}
		private async static Task<Branch> GetBranchRepo(long repositoryID, string repoBranchName)
		{
			var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("hackpdm"));
			return await ghClient.Repository.Branch.Get(repositoryID, repoBranchName);
		}
		private static bool IsLatestVersion (Branch branch, Version version)
		{
			var latestCommit = branch.Commit.Sha;
			return false;
		}
		public async static void EnsureUpdated()
		{
			var info = CurrentVersion();
			var ghBranch = await GetBranchRepo(repoID, branchName);

			if (!IsLatestVersion(ghBranch, info))
			{
				 if (MessageBox.Show($"Latest version: {ghBranch.Commit.Sha}, doesn't match your version: {info}\n" +
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
