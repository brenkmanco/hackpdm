using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
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

		private static string odooClientVersion;

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
		private static bool IsCorrectOdooVersion(Version version)
		{
			odooClientVersion = OdooDefaults.HpSettings.Where( s => s.name == OdooDefaults.OdooVersionKeyName ).First().char_value;
			
			if (odooClientVersion == $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}")
				return true;

			if ( MessageBox.Show( $"Latest version: {version.Major}.{version.Minor}.{version.Build}.{version.Revision} doesn't match odoo version: {odooClientVersion}\n" +
				$"Would you like to download the latest version?",
				"Versions",
				MessageBoxButtons.YesNoCancel ) == DialogResult.Yes )
			{
				UpdaterProcess( );
			}

			return false;
		}
		public static bool EnsureUpdated()
		{
			var info = CurrentVersion();
			//var ghBranch = await GetBranchRepo(repoID, branchName);
			//var taskSync = GetReleasesAsync(repoID);
			//taskSync.Wait();
			//var ghReleases = taskSync.Result;

			//if ( ghReleases.Count == 0 )
			//{
			//	MessageBox.Show( "No releases found on GitHub" );
			//	return;
			//}

			//if (!IsLatestVersion(ghReleases[0], info))
			//{
			//	if (MessageBox.Show($"Latest version: {ghReleases[0].TagName}, doesn't match your version: {info}\n" +
			//	 $"Would you like to download the latest version?",
			//	 "Versions",
			//	 MessageBoxButtons.YesNoCancel) == DialogResult.Yes)
			//	{
			//		UpdaterProcess(ghReleases[0]);
			//	}
			//	throw new Exception("Update to latest version");
			//}

			return IsCorrectOdooVersion(info);
		}
		public static void UpdaterProcess( )
		{
			try
			{
				MessageBox.Show($"Opening {publishURL}");
				Process proc = Process.Start( publishURL );
			}
			catch
			{
				Debug.WriteLine( "Failed to open download link.." );
			}
		}
	}
}
