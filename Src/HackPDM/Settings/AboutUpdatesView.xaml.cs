using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using HackPDM.Common;
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
	private CancellationTokenSource? _updateCts;
	private bool _isUpdating;

	public AboutUpdatesView()
	{
		InitializeComponent();
		Loaded += AboutUpdatesView_Loaded;
		Unloaded += AboutUpdatesView_Unloaded;
	}

	private void AboutUpdatesView_Unloaded( object sender, RoutedEventArgs e )
		=> CtsHelper.CancelAndDispose( ref _updateCts );

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
		bool started = false;
		try
		{
			if( _isUpdating )
				return;
			_isUpdating = true;
			started = true;

			CancellationToken token = CtsHelper.Replace( ref _updateCts ).Token;
			CheckUpdatesButton.IsEnabled = false;
			InstallUpdateButton.Visibility = Visibility.Collapsed;
			_availableUpdate = null;
			UpdateStatusText.Text = "Checking for updates…";

			var result = await HackUpdater.CheckForUpdateAsync( allowGitHubFallback: true, token: token );
			if( token.IsCancellationRequested || !IsLoaded )
				return;
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
		catch( OperationCanceledException )
		{
		}
		catch( Exception ex )
		{
			HackLogger.Error( "Updater", $"Manual update check failed: {ex.Message}", ex );
			try
			{
				if( IsLoaded )
					UpdateStatusText.Text = $"Update check failed: {ex.Message}";
			}
			catch
			{
			}
		}
		finally
		{
			if( started )
			{
				_isUpdating = false;
				try { CheckUpdatesButton.IsEnabled = true; }
				catch( Exception ex ) { HackLogger.Debug( "Updater", $"Ignoring UI cleanup on unloaded view: {ex.Message}" ); }
			}
		}
	}

	private async void InstallUpdate_Click( object sender, RoutedEventArgs e )
	{
		bool started = false;
		try
		{
			if( _isUpdating || _availableUpdate is not { } update )
				return;
			_isUpdating = true;
			started = true;

			CancellationToken token = CtsHelper.Replace( ref _updateCts ).Token;
			CheckUpdatesButton.IsEnabled = false;
			InstallUpdateButton.IsEnabled = false;
			UpdateStatusText.Text = $"Downloading and verifying HackPDM {update.Version}…";

			bool ok = await HackUpdater.InstallUpdateAsync( update, token );
			if( token.IsCancellationRequested || !IsLoaded )
				return;
			UpdateStatusText.Text = ok ? "Update package downloaded." : "Update was not installed.";
		}
		catch( OperationCanceledException )
		{
		}
		catch( Exception ex )
		{
			HackLogger.Error( "Updater", $"Installing update failed: {ex.Message}", ex );
			try
			{
				if( IsLoaded )
					UpdateStatusText.Text = $"Update failed: {ex.Message}";
			}
			catch
			{
			}
		}
		finally
		{
			if( started )
			{
				_isUpdating = false;
				try
				{
					CheckUpdatesButton.IsEnabled = true;
					InstallUpdateButton.IsEnabled = true;
				}
				catch( Exception ex )
				{
					HackLogger.Debug( "Updater", $"Ignoring UI cleanup on unloaded view: {ex.Message}" );
				}
			}
		}
	}

	private void ReleaseNotes_Click( object sender, RoutedEventArgs e )
		=> ShellLauncher.OpenUrl( _releaseNotesUrl );

	private void OpenLogs_Click( object sender, RoutedEventArgs e )
	{
		try { Directory.CreateDirectory( HackLogger.LogDirectory ); }
		catch { }
		ShellLauncher.OpenFolder( HackLogger.LogDirectory );
	}

	private void OpenRepo_Click( object sender, RoutedEventArgs e )
		=> ShellLauncher.OpenUrl( "https://github.com/moonxbears/wyrmpdm" );
}
