using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

namespace HackPDM.Login;

/// <summary>
/// Checks that this client satisfies the server's <c>client_version</c> policy, offers non-blocking prompts when
/// <c>client_latest_version</c> is newer, and supports on-demand checks against Odoo or GitHub Releases.
/// </summary>
internal static class HackUpdater
{
	private static bool _promptedRecommendedThisSession;

	private static string? Setting( string name )
	{
		var settings = OdooDefaults.Instance?.HpSettings;
		if( settings is null )
			return null;
		for( int i = 0; i < settings.Length; i++ )
		{
			if( settings[ i ].name == name )
				return settings[ i ].char_value;
		}
		return null;
	}

	public static string? RequiredVersionRequirement => Setting( PdmConstants.ClientVersionKey );
	public static string? LatestServerVersion => Setting( PdmConstants.ClientLatestVersionKey );

	public static async Task<bool> EnsureUpdatedAsync( bool showOutdatedUpdater = false, CancellationToken token = default )
	{
		token.ThrowIfCancellationRequested();
		string? required = RequiredVersionRequirement;
		VersionCheckResult check = ClientUpdate.CheckVersion( ClientUpdate.Current, required );
		if( !check.IsAllowed )
		{
			HackLogger.Warn( "Updater", $"Client version check denied access: {check.Reason}" );
			if( showOutdatedUpdater )
			{
				UpdateInfo? mandatoryUpdate = GetServerUpdateInfo( isMandatory: true );
				if( mandatoryUpdate is not null )
				{
					if( await AppComposition.Dialogs.ShowMessageAsync(
							$"{check.Reason}\n" +
							"Access is denied until you update HackPDM to a compatible version.\n\n" +
							"Download and install the published version now?",
							"Version Mismatch — Update Required",
							MessageBoxButtons.YesNo, MessageBoxIcon.Warning ) == DialogResult.Yes )
					{
						await InstallUpdateAsync( mandatoryUpdate, token );
					}
				}
				else
				{
					await AppComposition.Dialogs.ShowMessageAsync(
						$"{check.Reason}\n" +
						"Access is denied until you update HackPDM to a compatible version.",
						"Version Mismatch — Update Required",
						MessageBoxButtons.OK, MessageBoxIcon.Warning );
				}
			}
			return false;
		}

		if( showOutdatedUpdater && !_promptedRecommendedThisSession )
		{
			UpdateInfo? serverUpdate = GetServerUpdateInfo( isMandatory: false );
			if( serverUpdate is not null )
			{
				_promptedRecommendedThisSession = true;
				HackLogger.Info( "Updater", $"Recommended client update {serverUpdate.Version} available from server (current {ClientUpdate.CurrentText})." );
				if( await AppComposition.Dialogs.ShowMessageAsync(
						$"A newer version of HackPDM ({serverUpdate.Version}) is available (you are running {ClientUpdate.CurrentText}).\n\n" +
						"Download and install the update now?",
						"HackPDM Update Available",
						MessageBoxButtons.YesNo, MessageBoxIcon.Information ) == DialogResult.Yes )
				{
					await InstallUpdateAsync( serverUpdate, token );
				}
			}
		}

		return true;
	}

	public static async Task<Result<UpdateInfo?>> CheckForUpdateAsync( bool allowGitHubFallback = true, CancellationToken token = default )
	{
		token.ThrowIfCancellationRequested();
		VersionCheckResult serverCheck = ClientUpdate.CheckVersion( ClientUpdate.Current, RequiredVersionRequirement );
		UpdateInfo? serverUpdate = GetServerUpdateInfo( isMandatory: !serverCheck.IsAllowed );
		if( serverUpdate is not null )
			return Result<UpdateInfo?>.Success( serverUpdate );

		return allowGitHubFallback
			? await Task.Run( () => ClientUpdate.CheckGitHubLatestReleaseAsync( token: token ), token )
			: Result<UpdateInfo?>.Success( null );
	}

	private static UpdateInfo? GetServerUpdateInfo( bool isMandatory )
	{
		string? url = Setting( PdmConstants.ClientDownloadUrlKey );
		string? sha = ClientUpdate.ParseSha256File( Setting( PdmConstants.ClientDownloadSha256Key ) );
		if( string.IsNullOrWhiteSpace( url ) || sha is null )
			return null;

		string? latest = LatestServerVersion?.Trim();
		if( !isMandatory && !ClientUpdate.IsOutdated( ClientUpdate.Current, latest ) )
			return null;

		string versionLabel = !string.IsNullOrWhiteSpace( latest )
			? latest
			: ( RequiredVersionRequirement?.Trim() ?? "latest" );

		return new UpdateInfo(
			Version: versionLabel,
			DownloadUrl: url.Trim(),
			Sha256: sha,
			ReleaseNotesUrl: "https://github.com/moonxbears/wyrmpdm/releases",
			Source: "Odoo Server",
			IsMandatory: isMandatory );
	}

	public static Task<bool> InstallUpdateAsync( UpdateInfo update, CancellationToken token = default )
		=> DownloadAndLaunchAsync( update.DownloadUrl, update.Sha256, token );

	private static async Task<bool> DownloadAndLaunchAsync( string? url, string? sha256, CancellationToken token = default )
	{
		var dialogs = AppComposition.Dialogs;
		HackLogger.Info( "Updater", $"Starting client update download from '{url ?? "(unset)"}'" );
		Result<string> installer = await Task.Run( () => ClientUpdate.DownloadInstallerAsync( url, sha256, token: token ), token );
		token.ThrowIfCancellationRequested();
		if( !installer.Ok )
		{
			HackLogger.Error( "Updater", $"Client update aborted: {installer.Error}" );
			await dialogs.ShowMessageAsync( $"The update was not installed: {installer.Error}", "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return false;
		}
		string installerPath = installer.Value!;
		HackLogger.Info( "Updater", $"Verified update package ready at '{installerPath}'" );

		bool started = await Task.Run( () => OperatingSystem.IsWindows()
			? LaunchWindowsInstaller( installerPath )
			: ShellLauncher.OpenFolder( Path.GetDirectoryName( installerPath )! ), token );

		if( !started )
		{
			HackLogger.Warn( "Updater", $"Could not launch verified update at '{installerPath}'" );
			await dialogs.ShowMessageAsync( $"The update was downloaded to {installerPath} but could not be opened; run it from there.",
				"Update", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return false;
		}
		if( OperatingSystem.IsWindows() )
		{
			HackLogger.Info( "Updater", "Installer launched; exiting HackPDM for upgrade." );
			await Task.Run( UIStorage.SaveSafe );
			try { AppComposition.DisposeLocalFilesIfCreated(); } catch { }
			Environment.Exit( 0 );
		}
		else
		{
			await dialogs.ShowMessageAsync( $"The checked update is in {installerPath}. Close HackPDM and install it.", "Update",
				MessageBoxButtons.OK, MessageBoxIcon.Information );
		}
		return true;
	}

	private static bool LaunchWindowsInstaller( string path )
	{
		try
		{
			if( path.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
			{
				using Process? _ = Process.Start( new ProcessStartInfo( path )
				{
					Arguments = "/CLOSEAPPLICATIONS",
					UseShellExecute = true,
				} );
				return true;
			}
			return ShellLauncher.OpenFile( path );
		}
		catch
		{
			return ShellLauncher.OpenFile( path );
		}
	}
}
