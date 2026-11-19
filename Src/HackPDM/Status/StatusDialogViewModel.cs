using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.Status;

/// <summary>Tracks operation log entries, transfer counters, and progress with coalesced UI-thread updates.</summary>
[Bindable]
public sealed partial class StatusDialogViewModel : ObservableObject
{
	private const string HistoryLengthSettingKey = "StatusHistoryLength";
	private const int DefaultHistoryLength = 1000;
	private const int MaxLogsPerUiTick = 200;

	private static int? _cachedHistoryLength;

	private readonly Lock _gate = new();
	private readonly Action _cancelOperation;
	private readonly IDialogService _dialogs;
	private readonly ConcurrentQueue<BasicStatusMessage> _pendingLogs = new();
	private readonly List<BasicStatusMessage> _batchStatus = [];
	private readonly List<BasicStatusMessage> _batchInfo = [];
	private readonly List<BasicStatusMessage> _batchError = [];
	private readonly List<BasicStatusMessage> _batchWarnings = [];
	private readonly List<BasicStatusMessage> _batchErrorsOnly = [];
	private readonly Action _runLogFlush;
	private readonly Action _runProgressRefresh;
	private int _refreshQueued;
	private int _logFlushQueued;
	private bool _isInProcess;
	private bool _autoClose;
	private string _latestProcessingMessage = "Idle";

	public event Action? OperationCompleted;

	public static int HistoryLength
	{
		get
		{
			if( _cachedHistoryLength is int cached )
				return cached;
			int saved = HackDefaults.Instance.SettingsProvider?.Get( HistoryLengthSettingKey, DefaultHistoryLength ) ?? DefaultHistoryLength;
			int clamped = Math.Clamp( saved, 10, 1_000_000 );
			_cachedHistoryLength = clamped;
			return clamped;
		}
		private set
		{
			int clamped = Math.Clamp( value, 10, 1_000_000 );
			_cachedHistoryLength = clamped;
			HackDefaults.Instance.SettingsProvider?.Set( HistoryLengthSettingKey, clamped );
		}
	}

	public StatusDialogViewModel( IDialogService dialogs, Action cancelOperation )
	{
		_dialogs = dialogs;
		_cancelOperation = cancelOperation;
		_autoClose = HackDefaults.Instance.SettingsProvider?.Get( "StatusAutoClose", false ) ?? false;
		_runLogFlush = FlushPendingLogs;
		_runProgressRefresh = RunProgressRefresh;
	}

	public RangeObservableCollection<BasicStatusMessage> OStatus { get; } = [];
	public RangeObservableCollection<BasicStatusMessage> OInfo { get; } = [];
	public RangeObservableCollection<BasicStatusMessage> OError { get; } = [];
	public RangeObservableCollection<BasicStatusMessage> OWarnings { get; } = [];
	public RangeObservableCollection<BasicStatusMessage> OErrorsOnly { get; } = [];

	public bool IsInProcess
	{
		get { lock( _gate ) return _isInProcess; }
		set => SetInProcess( value );
	}

	public bool AutoClose
	{
		get => _autoClose;
		set
		{
			if( !SetProperty( ref _autoClose, value ) )
				return;
			HackDefaults.Instance.SettingsProvider?.Set( "StatusAutoClose", value );
		}
	}

	public int MaxCount { get; private set; }
	public int TotalProcessed { get; private set; }
	public int SkipCounter { get; private set; }
	private int ProcessCounter { get; set; }
	public long DownloadedBytes { get; private set; }
	public long SessionDownloadedBytes { get; private set; }
	public long UploadedBytes { get; private set; }
	public long SessionUploadedBytes { get; private set; }

	public int InfoCount => OInfo.Count;
	public int WarningCount => OWarnings.Count;
	public int ErrorCount => OErrorsOnly.Count;

	public string LatestProcessingMessage => _latestProcessingMessage;
	public string ProcessingSummaryText => IsInProcess
		? ( MaxCount > 0 ? $"Processing {TotalProcessed}/{MaxCount}" : "Processing..." )
		: ( OStatus.Count > 0 ? $"{OStatus.Count} ops" : "Idle" );
	public string InfoSummaryText => InfoCount == 1 ? "1 info" : $"{InfoCount} info";
	public string WarningSummaryText => WarningCount == 1 ? "1 warning" : $"{WarningCount} warnings";
	public string ErrorSummaryText => ErrorCount == 1 ? "1 error" : $"{ErrorCount} errors";

	public string CompactProgressText
	{
		get
		{
			if( MaxCount <= 0 )
				return IsInProcess ? "Working..." : "Ready";
			float percent = TotalProcessed / (float)MaxCount * 100f;
			return $"{TotalProcessed}/{MaxCount} ({percent:f0}%)";
		}
	}

	public string ProgressText
	{
		get
		{
			float percent = MaxCount > 0 ? TotalProcessed / (float)MaxCount * 100f : 0f;
			return $"({percent:f2}%)\n{TotalProcessed} / {MaxCount}";
		}
	}

	public string SkippedText => $"({SkipCounter}) Skipped";
	public string DownloadedText => $"Downloaded: {ByteSize.Format( DownloadedBytes )}";
	public string SessionDownloadedText => $"Session Downloaded: {ByteSize.Format( SessionDownloadedBytes )}";
	public string UploadedText => $"Uploaded: {ByteSize.Format( UploadedBytes )}";
	public string SessionUploadedText => $"Session Uploaded: {ByteSize.Format( SessionUploadedBytes )}";

	public void SetHistoryLength( int length )
	{
		int clamped = Math.Clamp( length, 10, 1_000_000 );
		HistoryLength = clamped;
		UiDispatcher.Post( () =>
		{
			OStatus.TrimStartTo( clamped );
			OInfo.TrimStartTo( clamped );
			OError.TrimStartTo( clamped );
			OWarnings.TrimStartTo( clamped );
			OErrorsOnly.TrimStartTo( clamped );
			NotifySummaryChanged();
		} );
	}

	public void AddLog( BasicStatusMessage message )
	{
		_pendingLogs.Enqueue( message );
		ScheduleLogFlush();
	}

	private void ScheduleLogFlush()
	{
		if( Interlocked.Exchange( ref _logFlushQueued, 1 ) == 1 )
			return;
		UiDispatcher.Post( _runLogFlush );
	}

	private void FlushPendingLogs()
	{
		Volatile.Write( ref _logFlushQueued, 0 );
		int limit = HistoryLength;
		int drained = 0;
		_batchStatus.Clear();
		_batchInfo.Clear();
		_batchError.Clear();
		_batchWarnings.Clear();
		_batchErrorsOnly.Clear();

		while( drained < MaxLogsPerUiTick && _pendingLogs.TryDequeue( out BasicStatusMessage? message ) )
		{
			switch( message.Status )
			{
				case StatusMessage.PROCESSING or StatusMessage.SUCCESS:
					_batchStatus.Add( message );
					if( !string.IsNullOrWhiteSpace( message.Message ) )
						_latestProcessingMessage = message.Message;
					break;
				case StatusMessage.WARNING:
					_batchError.Add( message );
					_batchWarnings.Add( message );
					break;
				case StatusMessage.ERROR:
					_batchError.Add( message );
					_batchErrorsOnly.Add( message );
					break;
				default:
					_batchInfo.Add( message );
					break;
			}
			drained++;
		}
		if( drained > 0 )
		{
			OStatus.AddRangeBounded( _batchStatus, limit );
			OInfo.AddRangeBounded( _batchInfo, limit );
			OError.AddRangeBounded( _batchError, limit );
			OWarnings.AddRangeBounded( _batchWarnings, limit );
			OErrorsOnly.AddRangeBounded( _batchErrorsOnly, limit );
			_batchStatus.Clear();
			_batchInfo.Clear();
			_batchError.Clear();
			_batchWarnings.Clear();
			_batchErrorsOnly.Clear();
			NotifySummaryChanged();
		}
		if( !_pendingLogs.IsEmpty )
			ScheduleLogFlush();
	}

	[RelayCommand( CanExecute = nameof( CanCancel ) )]
	private void Cancel() => _cancelOperation();

	private bool CanCancel() => IsInProcess;

	public void SetInProcess( bool inProcess )
	{
		lock( _gate )
		{
			if( _isInProcess == inProcess )
				return;
			_isInProcess = inProcess;
		}
		UiDispatcher.Post( () =>
		{
			OnPropertyChanged( nameof( IsInProcess ) );
			OnPropertyChanged( nameof( ProcessingSummaryText ) );
			OnPropertyChanged( nameof( CompactProgressText ) );
			CancelCommand.NotifyCanExecuteChanged();
		} );
	}

	[RelayCommand]
	public void ClearLogs()
	{
		if( IsInProcess )
			return;
		lock( _gate )
		{
			( MaxCount, ProcessCounter, SkipCounter, TotalProcessed ) = ( 0, 0, 0, 0 );
			DownloadedBytes = 0;
			UploadedBytes = 0;
		}
		UiDispatcher.Post( () =>
		{
			while( _pendingLogs.TryDequeue( out _ ) ) { }
			OStatus.Clear();
			OInfo.Clear();
			OError.Clear();
			OWarnings.Clear();
			OErrorsOnly.Clear();
			_latestProcessingMessage = "Idle";
			NotifyProgressChanged();
			NotifySummaryChanged();
		} );
	}

	public async Task<bool> ConfirmCloseAsync()
	{
		if( !IsInProcess )
			return true;
		bool close = DialogResult.OK == await _dialogs.ShowMessageAsync(
			"There are items still processing.\nCancel the running operation and close this window?",
			"Cancel Operation?",
			MessageBoxButtons.OKCancel,
			MessageBoxIcon.Warning );
		if( close )
		{
			_cancelOperation();
			SetInProcess( false );
		}
		return close;
	}

	public void ResetProgress( int total )
	{
		lock( _gate )
		{
			( MaxCount, ProcessCounter, SkipCounter, TotalProcessed ) = ( total, 0, 0, 0 );
			DownloadedBytes = 0;
			UploadedBytes = 0;
		}
		QueueProgressRefresh();
	}

	public void AddDownloadedBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		lock( _gate )
		{
			DownloadedBytes += bytes;
			SessionDownloadedBytes += bytes;
		}
		QueueProgressRefresh();
	}

	public void AddUploadedBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		lock( _gate )
		{
			UploadedBytes += bytes;
			SessionUploadedBytes += bytes;
		}
		QueueProgressRefresh();
	}

	public void Advance( int processed, int skipped )
	{
		lock( _gate )
		{
			ProcessCounter += processed;
			SkipCounter += skipped;
			TotalProcessed = ProcessCounter + SkipCounter;
		}
		QueueProgressRefresh();
	}

	public void SetProcessed( int processed )
	{
		lock( _gate )
		{
			ProcessCounter = processed;
			TotalProcessed = ProcessCounter + SkipCounter;
		}
		QueueProgressRefresh();
	}

	public void CompleteProgress()
	{
		bool wasInProcess;
		lock( _gate )
		{
			wasInProcess = _isInProcess;
			ProcessCounter = Math.Max( MaxCount - SkipCounter, 0 );
			TotalProcessed = MaxCount;
			_isInProcess = false;
		}
		UiDispatcher.Post( () =>
		{
			NotifyProgressChanged();
			NotifySummaryChanged();
			if( wasInProcess )
			{
				OnPropertyChanged( nameof( IsInProcess ) );
				CancelCommand.NotifyCanExecuteChanged();
				OperationCompleted?.Invoke();
			}
		} );
	}

	[RelayCommand]
	private void OpenLogsFolder()
	{
		string dir = HackLogger.LogDirectory;
		try
		{
			Directory.CreateDirectory( dir );
			ShellLauncher.OpenFolder( dir );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "StatusDialog", $"Could not open logs folder '{dir}'", ex );
		}
	}

	public string BuildDiagnosticSummary()
	{
		OdooDefaults defaults = OdooDefaults.Instance;
		return HackLogger.BuildDiagnosticSummary(
			clientVersion: ClientUpdate.CurrentText,
			odooUrl: defaults.OdooUrl,
			odooDb: defaults.OdooDb );
	}

	private void QueueProgressRefresh()
	{
		if( Interlocked.Exchange( ref _refreshQueued, 1 ) == 1 )
			return;
		UiDispatcher.Post( _runProgressRefresh );
	}

	private void RunProgressRefresh()
	{
		Volatile.Write( ref _refreshQueued, 0 );
		NotifyProgressChanged();
	}

	private void NotifyProgressChanged()
	{
		OnPropertyChanged( nameof( MaxCount ) );
		OnPropertyChanged( nameof( TotalProcessed ) );
		OnPropertyChanged( nameof( ProgressText ) );
		OnPropertyChanged( nameof( CompactProgressText ) );
		OnPropertyChanged( nameof( ProcessingSummaryText ) );
		OnPropertyChanged( nameof( SkippedText ) );
		OnPropertyChanged( nameof( DownloadedText ) );
		OnPropertyChanged( nameof( SessionDownloadedText ) );
		OnPropertyChanged( nameof( UploadedText ) );
		OnPropertyChanged( nameof( SessionUploadedText ) );
	}

	private void NotifySummaryChanged()
	{
		OnPropertyChanged( nameof( InfoCount ) );
		OnPropertyChanged( nameof( WarningCount ) );
		OnPropertyChanged( nameof( ErrorCount ) );
		OnPropertyChanged( nameof( InfoSummaryText ) );
		OnPropertyChanged( nameof( WarningSummaryText ) );
		OnPropertyChanged( nameof( ErrorSummaryText ) );
		OnPropertyChanged( nameof( ProcessingSummaryText ) );
		OnPropertyChanged( nameof( LatestProcessingMessage ) );
	}
}
