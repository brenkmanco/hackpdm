using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Login;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Settings;

/// <summary>View model for <see cref="AboutUpdatesView"/>: version metadata, update checks/install, and diagnostics links.</summary>
[Bindable]
public sealed partial class AboutUpdatesViewModel : ObservableObject, IDisposable
{
	private string _releaseNotesUrl = "https://github.com/moonxbears/wyrmpdm/releases";
	private CancellationTokenSource? _updateCts;

	public string VersionText { get; } = $"Version {ClientUpdate.CurrentText}";
	public string RuntimeText { get; } = $"{RuntimeInformation.FrameworkDescription}  •  {RuntimeInformation.OSDescription.Trim()}";

	[ObservableProperty] public partial string ServerPolicyText { get; set; } = string.Empty;
	[ObservableProperty] public partial string UpdateStatusText { get; set; } = "Click Check for Updates to query your Odoo server and GitHub Releases.";

	[ObservableProperty]
	[NotifyCanExecuteChangedFor( nameof( CheckUpdatesCommand ), nameof( InstallUpdateCommand ) )]
	public partial bool IsUpdating { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor( nameof( InstallUpdateCommand ) )]
	[NotifyPropertyChangedFor( nameof( HasAvailableUpdate ), nameof( InstallButtonText ) )]
	public partial UpdateInfo? AvailableUpdate { get; set; }

	public bool HasAvailableUpdate => AvailableUpdate is not null;
	public string InstallButtonText => AvailableUpdate is { } u ? $"Download & Install {u.Version}" : "Download & Install";

	private bool CanCheckUpdates() => !IsUpdating;
	private bool CanInstallUpdate() => !IsUpdating && AvailableUpdate is not null;

	public void RefreshServerPolicy()
	{
		string? required = HackUpdater.RequiredVersionRequirement;
		string? latest = HackUpdater.LatestServerVersion;
		ServerPolicyText = ( string.IsNullOrWhiteSpace( required ), string.IsNullOrWhiteSpace( latest ) ) switch
		{
			( true, true ) => "Server update policy: not connected or no policy configured (GitHub Releases fallback enabled).",
			( false, true ) => $"Server required version: {required}",
			( true, false ) => $"Server latest published version: {latest}",
			( false, false ) => $"Server required version: {required}  •  Latest published: {latest}",
		};
	}

	public void CancelPendingUpdates() => CtsHelper.CancelAndDispose( ref _updateCts );

	public void Dispose() => CancelPendingUpdates();

	[RelayCommand( CanExecute = nameof( CanCheckUpdates ) )]
	private async Task CheckUpdatesAsync()
	{
		if( IsUpdating )
			return;
		CancellationToken token = CtsHelper.Replace( ref _updateCts ).Token;
		IsUpdating = true;
		AvailableUpdate = null;
		UpdateStatusText = "Checking for updates…";
		try
		{
			Result<UpdateInfo?> result = await HackUpdater.CheckForUpdateAsync( allowGitHubFallback: true, token: token );
			if( token.IsCancellationRequested )
				return;
			if( !result.Ok )
			{
				UpdateStatusText = $"Update check failed: {result.Error}";
				return;
			}

			if( result.Value is null )
			{
				UpdateStatusText = $"You are up to date (HackPDM {ClientUpdate.CurrentText}).";
				return;
			}

			UpdateInfo update = result.Value;
			AvailableUpdate = update;
			if( !string.IsNullOrWhiteSpace( update.ReleaseNotesUrl ) )
				_releaseNotesUrl = update.ReleaseNotesUrl;

			string kind = update.IsMandatory ? "Required update" : "Update";
			UpdateStatusText = $"{kind} available: HackPDM {update.Version} (from {update.Source}).";
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
		}
		catch( Exception ex )
		{
			HackLogger.Error( "Updater", $"Manual update check failed: {ex.Message}", ex );
			UpdateStatusText = $"Update check failed: {ex.Message}";
		}
		finally
		{
			IsUpdating = false;
		}
	}

	[RelayCommand( CanExecute = nameof( CanInstallUpdate ) )]
	private async Task InstallUpdateAsync()
	{
		if( IsUpdating || AvailableUpdate is not { } update )
			return;
		CancellationToken token = CtsHelper.Replace( ref _updateCts ).Token;
		IsUpdating = true;
		UpdateStatusText = $"Downloading and verifying HackPDM {update.Version}…";
		try
		{
			bool ok = await HackUpdater.InstallUpdateAsync( update, token );
			if( token.IsCancellationRequested )
				return;
			UpdateStatusText = ok ? "Update package downloaded." : "Update was not installed.";
		}
		catch( OperationCanceledException ) when( token.IsCancellationRequested )
		{
		}
		catch( Exception ex )
		{
			HackLogger.Error( "Updater", $"Installing update failed: {ex.Message}", ex );
			UpdateStatusText = $"Update failed: {ex.Message}";
		}
		finally
		{
			IsUpdating = false;
		}
	}

	[RelayCommand]
	private void OpenReleaseNotes() => ShellLauncher.OpenUrl( _releaseNotesUrl );

	[RelayCommand]
	private async Task OpenLogsAsync()
	{
		await Task.Run( static () =>
		{
			try { Directory.CreateDirectory( HackLogger.LogDirectory ); }
			catch { }
			ShellLauncher.OpenFolder( HackLogger.LogDirectory );
		} );
	}

	[RelayCommand]
	private void OpenRepo() => ShellLauncher.OpenUrl( "https://github.com/moonxbears/wyrmpdm" );
}
