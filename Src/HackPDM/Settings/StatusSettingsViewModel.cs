using System;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Status;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Settings;

/// <summary>Configures status log retention, auto-close behavior, and log clearing.</summary>
[Bindable]
public partial class StatusSettingsViewModel : ObservableObject
{
	private readonly StatusDialogViewModel _statusLog;
	private readonly IDialogService _dialogs;
	private readonly LoginSession? _session;

	[ObservableProperty] public partial string HistoryLengthText { get; set; }
	[ObservableProperty] public partial bool AutoClose { get; set; }

	public event Action? Saved;

	public StatusSettingsViewModel( StatusDialogViewModel statusLog, IDialogService dialogs, LoginSession? session = null )
	{
		_statusLog = statusLog;
		_dialogs = dialogs;
		_session = session;
		HistoryLengthText = StatusDialogViewModel.HistoryLength.ToString();
		AutoClose = statusLog.AutoClose;
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
		_statusLog.SetHistoryLength( historyLength );
		_statusLog.AutoClose = AutoClose;
		_session?.LogInfo( ProfileStatusCategory.Settings, $"Saved Status settings (history length: {historyLength}, auto-close: {(AutoClose ? "on" : "off")})." );
		await _dialogs.ShowMessageAsync( "Status settings saved" );
		Saved?.Invoke();
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
