using System;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Mvvm.ViewModels.Common;

namespace HackPDM.Mvvm.ViewModels.Settings;

/// <summary>Configures status log retention, auto-close behavior, and log clearing.</summary>
public partial class StatusSettingsViewModel : ObservableObject
{
	private readonly StatusDialogViewModel _statusLog;
	private readonly IDialogService _dialogs;

	[ObservableProperty] public partial string HistoryLengthText { get; set; }
	[ObservableProperty] public partial bool AutoClose { get; set; }

	public event Action? Saved;

	public StatusSettingsViewModel( StatusDialogViewModel statusLog, IDialogService dialogs )
	{
		_statusLog = statusLog;
		_dialogs = dialogs;
		HistoryLengthText = StatusDialogViewModel.HistoryLength.ToString();
		AutoClose = statusLog.AutoClose;
	}

	[RelayCommand]
	private async Task SubmitAsync()
	{
		if( !int.TryParse( HistoryLengthText, out int historyLength ) || historyLength < 10 || historyLength > 1_000_000 )
		{
			await _dialogs.ShowMessageAsync( "History Length must be a whole number between 10 and 1,000,000." );
			return;
		}
		_statusLog.SetHistoryLength( historyLength );
		_statusLog.AutoClose = AutoClose;
		await _dialogs.ShowMessageAsync( "Status settings saved" );
		Saved?.Invoke();
	}

	[RelayCommand]
	private async Task ClearLogsAsync()
	{
		if( _statusLog.IsInProcess )
		{
			await _dialogs.ShowMessageAsync( "Cannot clear logs while an operation is in progress." );
			return;
		}
		_statusLog.ClearLogs();
		await _dialogs.ShowMessageAsync( "Status logs cleared" );
	}
}
