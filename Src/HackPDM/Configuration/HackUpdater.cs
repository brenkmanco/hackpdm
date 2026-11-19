using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.Logging;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Operations;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Dialogs;

namespace HackPDM.Configuration;

/// <summary>
/// Checks that this client is not older than the server requires (hp.settings <c>client_version</c>) and offers
/// to install the published one: downloaded from <c>client_download_url</c> and checked against
/// <c>client_download_sha256</c> before it is run (<see cref="ClientUpdate"/>).
/// </summary>
internal static class HackUpdater
{
	private static string? Setting( string name )
		=> OdooDefaults.Instance?.HpSettings?.FirstOrDefault( s => s.name == name )?.char_value;

	/// <summary>Returns the server's configured <c>client_version</c> requirement, if any.</summary>
	public static string? RequiredVersionRequirement => Setting( OdooDefaultsConstants.ODOO_VERSION_KEY_NAME );

	/// <summary>Whether this client satisfies the server's <c>client_version</c> requirement; when not, optionally offers to update.</summary>
	/// <param name="showOutdatedUpdater">When mismatched, ask the user whether to download and install the published version.</param>
	/// <returns>
	/// <see langword="true"/> when this client's version satisfies the server's <c>client_version</c> rule (or the server sets none);
	/// <see langword="false"/> when it does not match.
	/// </returns>
	/// <remarks>Reads the cached <see cref="OdooDefaults.Instance"/> settings, so the lookup tables must have been loaded from the server.</remarks>
	public static async Task<bool> EnsureUpdated( bool showOutdatedUpdater = false )
	{
		string? required = RequiredVersionRequirement;
		VersionCheckResult check = ClientUpdate.CheckVersion( ClientUpdate.Current, required );
		if( check.IsAllowed )
			return true;

		HackLogger.Warn( "Updater", $"Client version check denied access: {check.Reason}" );
		if( showOutdatedUpdater && await HackPDM.Shell.AppComposition.Dialogs.ShowMessageAsync(
				$"{check.Reason}\n" +
				"Access is denied until you update HackPDM to a compatible version.\n\n" +
				"Download and install the published version now?",
				"Version Mismatch — Update Required",
				MessageBoxButtons.YesNo, MessageBoxIcon.Warning ) == DialogResult.Yes )
			await UpdaterProcessAsync();
		return false;
	}

	/// <summary>Downloads the published installer, checks its SHA-256, runs it and exits the app.</summary>
	/// <remarks>When the download or check fails the user is told why and the app keeps running; nothing unchecked is run.</remarks>
	public static async Task UpdaterProcessAsync()
	{
		var dialogs = HackPDM.Shell.AppComposition.Dialogs;
		string? url = Setting( OdooDefaultsConstants.CLIENT_DOWNLOAD_URL_NAME );
		HackLogger.Info( "Updater", $"Starting client update download from '{url ?? "(unset)"}'" );
		Result<string> installer = await ClientUpdate.DownloadInstallerAsync(
			url, Setting( OdooDefaultsConstants.CLIENT_DOWNLOAD_SHA256_NAME ) );
		if( !installer.Ok )
		{
			HackLogger.Error( "Updater", $"Client update aborted: {installer.Error}" );
			await dialogs.ShowMessageAsync( $"The update was not installed: {installer.Error}", "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		HackLogger.Info( "Updater", $"Verified update package ready at '{installer.Value}'" );
		// Windows runs the installer; elsewhere the folder with the checked package is opened
		bool started = OperatingSystem.IsWindows()
			? ShellLauncher.OpenFile( installer.Value! )
			: ShellLauncher.OpenFolder( Path.GetDirectoryName( installer.Value! )! );
		if( !started )
		{
			HackLogger.Warn( "Updater", $"Could not launch verified update at '{installer.Value}'" );
			await dialogs.ShowMessageAsync( $"The update was downloaded to {installer.Value} but could not be opened; run it from there.",
				"Update", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		if( OperatingSystem.IsWindows() )
		{
			HackLogger.Info( "Updater", "Installer launched; exiting HackPDM for upgrade." );
			HackApp.Current.Exit();
		}
		else
			await dialogs.ShowMessageAsync( $"The checked update is in {installer.Value}. Close HackPDM and install it.", "Update",
				MessageBoxButtons.OK, MessageBoxIcon.Information );
	}
}
