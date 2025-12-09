using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using DialogResult = System.Windows.MessageBoxResult;

using HackPDM.Odoo;
using HackPDM.Odoo.OdooModels.Models;

namespace HackPDM.Hack;

internal class HackUpdater
{
	const long REPO_ID = 28426033L;
	const string BRANCH_NAME = "justinOdooIntegration";
	const string PUBLISH_URL = "\\\\freedom\\Engineering\\hackpdm\\setup.exe";

	private static HpSetting? _odooClientVersion;

	private static Version? CurrentVersion()
	{
		return Assembly.GetExecutingAssembly().GetName().Version;
	}
	private static bool IsCorrectOdooVersion(Version? version, bool showOutdatedUpdater = false)
	{
		string _hackClientVersion = $"{version?.Major}.{version?.Minor}.{version?.Build}.{version?.Revision}";
		_odooClientVersion = OdooDefaults.HpSettings.Where( s => s.name == OdooDefaults.ODOO_VERSION_KEY_NAME ).FirstOrDefault();

		if (_odooClientVersion is not null && _odooClientVersion.char_value.Equals(_hackClientVersion))
			return true;

		if (showOutdatedUpdater && MessageBox.Show( $"Latest version: {_hackClientVersion} doesn't match odoo version: {_odooClientVersion}\n" +
		        $"Would you like to download the latest version?",
			    "Versions",
			    MessageBoxButton.YesNoCancel ) == DialogResult.Yes )
		{
			UpdaterProcess( );
		}

		return false;
	}
	public static bool EnsureUpdated(bool showOutdatedUpdater = false)
	{
		var info = CurrentVersion();
		return IsCorrectOdooVersion(info, showOutdatedUpdater);
	}
	public static void UpdaterProcess( )
	{
		try
		{
			MessageBox.Show($"Opening {PUBLISH_URL}");
			Process proc = Process.Start( PUBLISH_URL );
			HackApp.Current.Exit();
		}
		catch
		{
			Debug.WriteLine( "Failed to open download link.." );
		}
	}
}