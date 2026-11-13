using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
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

	/// <summary>Whether this client is recent enough for the server; when not, optionally offers to update.</summary>
	/// <param name="showOutdatedUpdater">When outdated, ask the user whether to download and install the published version.</param>
	/// <returns>
	/// <see langword="true"/> when this client's version is at least the server's <c>client_version</c> (or the server sets none);
	/// <see langword="false"/> when it is older.
	/// </returns>
	/// <remarks>Reads the cached <see cref="OdooDefaults.Instance"/> settings, so the lookup tables must have been loaded from the server.</remarks>
	public static async Task<bool> EnsureUpdated( bool showOutdatedUpdater = false )
	{
		string? required = Setting( OdooDefaultsConstants.ODOO_VERSION_KEY_NAME );
		if( !ClientUpdate.IsOutdated( ClientUpdate.Current, required ) )
			return true;

		if( showOutdatedUpdater && await HackPDM.Shell.AppComposition.Dialogs.ShowMessageAsync(
				$"This is HackPDM {ClientUpdate.CurrentText}; the server needs version {required} or newer.\n" +
				"Download and install the new version now?",
				"Update required",
				MessageBoxButtons.YesNo, MessageBoxIcon.Information ) == DialogResult.Yes )
			await UpdaterProcessAsync();
		return false;
	}

	/// <summary>Downloads the published installer, checks its SHA-256, runs it and exits the app.</summary>
	/// <remarks>When the download or check fails the user is told why and the app keeps running; nothing unchecked is run.</remarks>
	public static async Task UpdaterProcessAsync()
	{
		var dialogs = HackPDM.Shell.AppComposition.Dialogs;
		Result<string> installer = await ClientUpdate.DownloadInstallerAsync(
			Setting( OdooDefaultsConstants.CLIENT_DOWNLOAD_URL_NAME ), Setting( OdooDefaultsConstants.CLIENT_DOWNLOAD_SHA256_NAME ) );
		if( !installer.Ok )
		{
			await dialogs.ShowMessageAsync( $"The update was not installed: {installer.Error}", "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		// Windows runs the installer; elsewhere the folder with the checked package is opened
		bool started = OperatingSystem.IsWindows()
			? ShellLauncher.OpenFile( installer.Value! )
			: ShellLauncher.OpenFolder( Path.GetDirectoryName( installer.Value! )! );
		if( !started )
		{
			await dialogs.ShowMessageAsync( $"The update was downloaded to {installer.Value} but could not be opened; run it from there.",
				"Update", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		if( OperatingSystem.IsWindows() )
			HackApp.Current.Exit();
		else
			await dialogs.ShowMessageAsync( $"The checked update is in {installer.Value}. Close HackPDM and install it.", "Update",
				MessageBoxButtons.OK, MessageBoxIcon.Information );
	}
}
