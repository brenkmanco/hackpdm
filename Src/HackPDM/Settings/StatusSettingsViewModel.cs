using System;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Status;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Settings;

/// <summary>Configures status log retention, auto-close behavior, download persistence, and log clearing.</summary>
[Bindable]
public sealed partial class StatusSettingsViewModel : ObservableObject
{
	private readonly StatusDialogViewModel _statusLog;
	private readonly IDialogService _dialogs;
	private readonly LoginSession? _session;
	private readonly IPdmWorkflowService? _workflow;

	[ObservableProperty] public partial string HistoryLengthText { get; set; }
	[ObservableProperty] public partial bool AutoClose { get; set; }
	[ObservableProperty] public partial bool PersistDownloadResume { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor( nameof( ClearSavedDownloadCommand ) )]
	public partial bool HasSavedDownload { get; private set; }

	[ObservableProperty] public partial string SavedDownloadStatusText { get; private set; } = string.Empty;

	public StatusSettingsViewModel(
		StatusDialogViewModel statusLog,
		IDialogService dialogs,
		LoginSession? session = null,
		IPdmWorkflowService? workflow = null )
	{
		_statusLog = statusLog;
		_dialogs = dialogs;
		_session = session;
		_workflow = workflow;
		HistoryLengthText = StatusDialogViewModel.HistoryLength.ToString();
		AutoClose = statusLog.AutoClose;
		PersistDownloadResume = HackDefaults.Instance.PersistDownloadResume;
		RefreshSavedDownloadState();
	}

	[RelayCommand]
	private async Task SubmitAsync()
	{
		if( !int.TryParse( HistoryLengthText, out int historyLength ) || historyLength < 10 || historyLength > 1_000_000 )
		{
			_session?.LogError( ProfileStatusCategory.Settings, $"Invalid Status History Length '{HistoryLengthText}': must be between 10 and 1,000,000." );
			await _dialogs.ShowMessageAsync( "History Length must be a whole number between 10 and 1,000,000." );
			return;
		}
		bool autoClose = AutoClose;
		bool persistDownloadResume = PersistDownloadResume;
		_statusLog.AutoClose = autoClose;
		await Task.Run( () =>
		{
			_statusLog.SetHistoryLength( historyLength );
			HackDefaults.Instance.PersistDownloadResume = persistDownloadResume;
			if( !persistDownloadResume )
				ClearStoredDownload();
		} );
		if( !persistDownloadResume )
		{
			_statusLog.RefreshResumableDownloadState();
			await RefreshSavedDownloadStateAsync();
		}
		_session?.LogInfo( ProfileStatusCategory.Settings, $"Saved Status settings (history length: {historyLength}, auto-close: {(autoClose ? "on" : "off")}, persist download resume: {(persistDownloadResume ? "on" : "off")})." );
		await _dialogs.ShowMessageAsync( "Status settings saved" );
	}

	private bool CanClearSavedDownload() => HasSavedDownload && !_statusLog.IsInProcess;

	[RelayCommand( CanExecute = nameof( CanClearSavedDownload ) )]
	private async Task ClearSavedDownloadAsync()
	{
		if( _statusLog.IsInProcess )
		{
			_session?.LogWarning( ProfileStatusCategory.Settings, "Could not clear saved download state while an operation is in progress." );
			await _dialogs.ShowMessageAsync( "Cannot clear saved download state while an operation is in progress." );
			return;
		}
		await Task.Run( ClearStoredDownload );
		_statusLog.RefreshResumableDownloadState();
		await RefreshSavedDownloadStateAsync();
		_session?.LogInfo( ProfileStatusCategory.Settings, "Cleared saved download resume state." );
		await _dialogs.ShowMessageAsync( "Saved download state cleared" );
	}

	private void ClearStoredDownload()
	{
		if( _workflow is not null )
			_workflow.ClearResumableDownload();
		else
			DownloadResumeStore.Default.Clear();
	}

	private void RefreshSavedDownloadState()
		=> _ = RefreshSavedDownloadStateAsync();

	private async Task RefreshSavedDownloadStateAsync()
	{
		try
		{
			DownloadResumeState? saved = await Task.Run( DownloadResumeStore.Default.Load ).ConfigureAwait( false );
			bool hasSaved = saved is { HasRemainingWork: true };
			string summary = hasSaved
				? saved!.FormatSummary()
				: "No unfinished download checkpoint is currently saved.";
			UiDispatcher.RunOrPost( () =>
			{
				HasSavedDownload = hasSaved;
				SavedDownloadStatusText = summary;
			} );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "StatusSettings", $"Could not inspect saved download checkpoint: {ex.Message}", ex );
		}
	}

	[RelayCommand]
	private async Task ClearLogsAsync()
	{
		if( _statusLog.IsInProcess )
		{
			_session?.LogWarning( ProfileStatusCategory.Settings, "Could not clear status logs while an operation is in progress." );
			await _dialogs.ShowMessageAsync( "Cannot clear logs while an operation is in progress." );
			return;
		}
		_statusLog.ClearLogs();
		_session?.LogInfo( ProfileStatusCategory.Settings, "Cleared Status Log history." );
		await _dialogs.ShowMessageAsync( "Status logs cleared" );
	}
}
