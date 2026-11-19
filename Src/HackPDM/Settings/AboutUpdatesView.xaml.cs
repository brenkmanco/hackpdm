using System;
using System.Runtime.InteropServices;

using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Login;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Settings;

/// <summary>Displays application version, server update policy, and on-demand update checks.</summary>
public sealed partial class AboutUpdatesView : Page
{
	private UpdateInfo? _availableUpdate;
	private string _releaseNotesUrl = "https://github.com/moonxbears/wyrmpdm/releases";

	public AboutUpdatesView()
	{
		InitializeComponent();
		Loaded += AboutUpdatesView_Loaded;
	}

	private void AboutUpdatesView_Loaded( object sender, RoutedEventArgs e )
	{
		VersionText.Text = $"Version {ClientUpdate.CurrentText}";
		RuntimeText.Text = $"{RuntimeInformation.FrameworkDescription}  •  {RuntimeInformation.OSDescription.Trim()}";

		string? required = HackUpdater.RequiredVersionRequirement;
		string? latest = HackUpdater.LatestServerVersion;
		ServerPolicyText.Text = ( string.IsNullOrWhiteSpace( required ), string.IsNullOrWhiteSpace( latest ) ) switch
		{
			( true, true ) => "Server update policy: not connected or no policy configured (GitHub Releases fallback enabled).",
			( false, true ) => $"Server required version: {required}",
			( true, false ) => $"Server latest published version: {latest}",
			( false, false ) => $"Server required version: {required}  •  Latest published: {latest}",
		};
	}

	private async void CheckUpdates_Click( object sender, RoutedEventArgs e )
	{
		CheckUpdatesButton.IsEnabled = false;
		InstallUpdateButton.Visibility = Visibility.Collapsed;
		_availableUpdate = null;
		UpdateStatusText.Text = "Checking for updates…";

		try
		{
			var result = await HackUpdater.CheckForUpdateAsync( allowGitHubFallback: true );
			if( !result.Ok )
			{
				UpdateStatusText.Text = $"Update check failed: {result.Error}";
				return;
			}

			if( result.Value is null )
			{
				UpdateStatusText.Text = $"You are up to date (HackPDM {ClientUpdate.CurrentText}).";
				return;
			}

			_availableUpdate = result.Value;
			if( !string.IsNullOrWhiteSpace( _availableUpdate.ReleaseNotesUrl ) )
				_releaseNotesUrl = _availableUpdate.ReleaseNotesUrl;

			string kind = _availableUpdate.IsMandatory ? "Required update" : "Update";
			UpdateStatusText.Text = $"{kind} available: HackPDM {_availableUpdate.Version} (from {_availableUpdate.Source}).";
			InstallUpdateButton.Content = $"Download & Install {_availableUpdate.Version}";
			InstallUpdateButton.Visibility = Visibility.Visible;
		}
		catch( Exception ex )
		{
			HackLogger.Error( "Updater", $"Manual update check failed: {ex.Message}", ex );
			UpdateStatusText.Text = $"Update check failed: {ex.Message}";
		}
		finally
		{
			CheckUpdatesButton.IsEnabled = true;
		}
	}

	private async void InstallUpdate_Click( object sender, RoutedEventArgs e )
	{
		if( _availableUpdate is null )
			return;

		CheckUpdatesButton.IsEnabled = false;
		InstallUpdateButton.IsEnabled = false;
		UpdateStatusText.Text = $"Downloading and verifying HackPDM {_availableUpdate.Version}…";
		try
		{
			await HackUpdater.InstallUpdateAsync( _availableUpdate );
			UpdateStatusText.Text = "Update package downloaded.";
		}
		finally
		{
			CheckUpdatesButton.IsEnabled = true;
			InstallUpdateButton.IsEnabled = true;
		}
	}

	private void ReleaseNotes_Click( object sender, RoutedEventArgs e )
		=> ShellLauncher.OpenUrl( _releaseNotesUrl );

	private void OpenLogs_Click( object sender, RoutedEventArgs e )
		=> ShellLauncher.OpenFolder( HackLogger.LogDirectory );

	private void OpenRepo_Click( object sender, RoutedEventArgs e )
		=> ShellLauncher.OpenUrl( "https://github.com/moonxbears/wyrmpdm" );
}
