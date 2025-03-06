using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

using Octokit;

namespace HackPDM.HackClient
{
	internal class HackUpdater
	{
		const long repoID = 28426033L;
		const string branchName = "justinOdooIntegration";
		const string publishURL = "\\\\freedom\\Engineering\\hackpdm\\setup.exe";
		private static Version CurrentVersion()
		{
			return Assembly.GetExecutingAssembly().GetName().Version;
		}
		private async static Task<IReadOnlyList<Release>> GetReleasesAsync(long repositoryID )
		{
			var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("hackpdm"));
			return await ghClient.Repository.Release.GetAll( repositoryID );
		}
		private static bool IsLatestVersion (Release release, Version version)
		{
			Debug.WriteLine($"tagname: {release.TagName}\nname: {release.Name}");
			string vStr = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
			return release.TagName == vStr;
		}
		public static void EnsureUpdated()
		{
			var info = CurrentVersion();
			//var ghBranch = await GetBranchRepo(repoID, branchName);
			var taskSync = GetReleasesAsync(repoID);
			taskSync.Wait();
			var ghReleases = taskSync.Result;

			if ( ghReleases.Count == 0 )
			{
				MessageBox.Show( "No releases found on GitHub" );
				return;
			}

			if (!IsLatestVersion(ghReleases[0], info))
			{
				 if (MessageBox.Show($"Latest version: {ghReleases[0].Name}, doesn't match your version: {info}\n" +
				 $"Would you like to download the latest version?", 
				 "Versions", 
				 MessageBoxButtons.YesNoCancel) == DialogResult.Yes)
				 {
					UpdaterProcess(ghReleases[0]);
				 }
				 throw new Exception("Update to latest version");
			}
		}
		public static void UpdaterProcess( Release release )
		{
			try
			{
				Process.Start( publishURL );
			}
			catch
			{
				Debug.WriteLine("Failed to open download link..\nDownloading from github..");
				Process.Start( "explorer.exe", release.ZipballUrl );
			}
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
		public static void UpdaterProcess(Branch branch)
		{
			Process.Start("explorer.exe", branch.Commit.Url);
		}
	}
}
