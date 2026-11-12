using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Dialogs;

namespace HackPDM.Configuration;

/// <summary>Checks that this client's version matches the one the Odoo server expects, and offers to install the published one.</summary>
internal class HackUpdater
{
	const long REPO_ID = 28426033L;
	const string BRANCH_NAME = "justinOdooIntegration";
	const string PUBLISH_URL = "\\\\freedom\\Engineering\\hackpdm\\setup.exe";

	private static IHpSettingModel? _odooClientVersion;

	private static Version? CurrentVersion()
	{
		return Assembly.GetExecutingAssembly().GetName().Version;
	}
	private static async Task<bool> IsCorrectOdooVersion(Version? version, bool showOutdatedUpdater = false)
	{
		string _hackClientVersion = $"{version?.Major}.{version?.Minor}.{version?.Build}.{version?.Revision}";
		_odooClientVersion = OdooDefaults.Instance?.HpSettings?.FirstOrDefault(s => s.name == OdooDefaultsConstants.ODOO_VERSION_KEY_NAME);

		if (_odooClientVersion is not null && _odooClientVersion.char_value.Equals(_hackClientVersion))
			return true;

		if (showOutdatedUpdater && await HackPDM.Shell.AppComposition.Dialogs.ShowMessageAsync( $"Latest version: {_hackClientVersion} doesn't match odoo version: {_odooClientVersion}\n" +
		        $"Would you like to download the latest version?",
			    "Versions",
			    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information ) == DialogResult.Yes )
		{
			UpdaterProcess( );
		}

		return false;
	}
	/// <summary>Compares this assembly's version with the client version stored in the Odoo settings.</summary>
	/// <param name="showOutdatedUpdater">When the versions differ, ask the user whether to download the published version (and run <see cref="UpdaterProcess"/> on Yes).</param>
	/// <returns><see langword="true"/> when the server's client version equals this assembly's "major.minor.build.revision".</returns>
	/// <remarks>Reads the cached <see cref="OdooDefaults.Instance"/> settings, so the lookup tables must have been loaded from the server.</remarks>
	public static async Task<bool> EnsureUpdated(bool showOutdatedUpdater = false)
	{
		var info = CurrentVersion();
		return await IsCorrectOdooVersion(info, showOutdatedUpdater);
	}
	/// <summary>Opens the published installer on the network share and exits the app.</summary>
	/// <remarks>Failures are written to the debug output and the app keeps running.</remarks>
	public static void UpdaterProcess( )
	{
		try
		{
			_ = HackPDM.Shell.AppComposition.Dialogs.ShowMessageAsync($"Opening {PUBLISH_URL}");
			ShellLauncher.OpenUrl(PUBLISH_URL);
			HackApp.Current.Exit();
		}
		catch
		{
			Debug.WriteLine( "Failed to open download link.." );
		}
	}
}