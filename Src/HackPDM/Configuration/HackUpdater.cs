using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Helper;

namespace HackPDM.Configuration;

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

		if (showOutdatedUpdater && await MessageBox.ShowAsync( $"Latest version: {_hackClientVersion} doesn't match odoo version: {_odooClientVersion}\n" +
		        $"Would you like to download the latest version?",
			    "Versions",
			    MessageBoxButtons.YesNoCancel ) == DialogResult.Yes )
		{
			UpdaterProcess( );
		}

		return false;
	}
	public static async Task<bool> EnsureUpdated(bool showOutdatedUpdater = false)
	{
		var info = CurrentVersion();
		return await IsCorrectOdooVersion(info, showOutdatedUpdater);
	}
	public static void UpdaterProcess( )
	{
		try
		{
			MessageBox.ShowAsync($"Opening {PUBLISH_URL}");
			Process proc = Process.Start( PUBLISH_URL );
			HackApp.Current.Exit();
		}
		catch
		{
			Debug.WriteLine( "Failed to open download link.." );
		}
	}
}