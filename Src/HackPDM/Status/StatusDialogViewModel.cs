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
	private const int UiFlushIntervalMs = 120;

	private static int _cachedHistoryLength = -1;

	private readonly Lock _gate = new();
	private readonly Action _cancelOperation;
	private readonly Action _cancelSilentOperation;
	private readonly IDialogService _dialogs;
	private readonly ConcurrentQueue<BasicStatusMessage> _pendingLogs = new();
	private readonly List<BasicStatusMessage> _batchStatus = [];
	private readonly List<BasicStatusMessage> _batchInfo = [];
	private readonly List<BasicStatusMessage> _batchError = [];
	private readonly List<BasicStatusMessage> _batchWarnings = [];
	private readonly List<BasicStatusMessage> _batchErrorsOnly = [];
	private readonly Action _runUiFlush;
	private readonly Timer _uiFlushTimer;
	private long _lastUiFlushTick;
	private int _uiFlushScheduled;
	private int _logDirty;
	private int _progressDirty;
	private int _maxCount;
	private int _totalProcessed;
	private int _skipCounter;
	private int _processCounter;
	private long _downloadedBytes;
	private long _sessionDownloadedBytes;
	private long _uploadedBytes;
	private long _sessionUploadedBytes;
	private bool _isInProcess;
	private bool _autoClose;
	private string _latestProcessingMessage = "Idle";

	public event Action? OperationCompleted;

	public static int HistoryLength
	{
		get
		{
			int cached = Volatile.Read( ref _cachedHistoryLength );
			if( cached >= 0 )
				return cached;
			int saved = HackDefaults.Instance.SettingsProvider?.Get( HistoryLengthSettingKey, DefaultHistoryLength ) ?? DefaultHistoryLength;
			int clamped = Math.Clamp( saved, 10, 1_000_000 );
			Volatile.Write( ref _cachedHistoryLength, clamped );
			return clamped;
		}
		private set
		{
			int clamped = Math.Clamp( value, 10, 1_000_000 );
			Volatile.Write( ref _cachedHistoryLength, clamped );
			HackDefaults.Instance.SettingsProvider?.Set( HistoryLengthSettingKey, clamped );
		}
	}

	public StatusDialogViewModel( IDialogService dialogs, Action cancelOperation, Action? cancelSilentOperation = null )
	{
		_dialogs = dialogs;
		_cancelOperation = cancelOperation;
		_cancelSilentOperation = cancelSilentOperation ?? cancelOperation;
		_autoClose = HackDefaults.Instance.SettingsProvider?.Get( "StatusAutoClose", false ) ?? false;
		int capacity = HistoryLength;
		OStatus = new( capacity, newestFirst: true );
		OInfo = new( capacity, newestFirst: true );
		OError = new( capacity, newestFirst: true );
		OWarnings = new( capacity, newestFirst: true );
		OErrorsOnly = new( capacity, newestFirst: true );
		_runUiFlush = RunUiFlush;
		_uiFlushTimer = new Timer( _ => UiDispatcher.Post( _runUiFlush ), null, Timeout.Infinite, Timeout.Infinite );
	}

	public ObservableRingBuffer<BasicStatusMessage> OStatus { get; }
	public ObservableRingBuffer<BasicStatusMessage> OInfo { get; }
	public ObservableRingBuffer<BasicStatusMessage> OError { get; }
	public ObservableRingBuffer<BasicStatusMessage> OWarnings { get; }
	public ObservableRingBuffer<BasicStatusMessage> OErrorsOnly { get; }

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

	public int MaxCount => Volatile.Read( ref _maxCount );
	public int TotalProcessed => Volatile.Read( ref _totalProcessed );
	public int SkipCounter => Volatile.Read( ref _skipCounter );
	public long DownloadedBytes => Volatile.Read( ref _downloadedBytes );
	public long SessionDownloadedBytes => Volatile.Read( ref _sessionDownloadedBytes );
	public long UploadedBytes => Volatile.Read( ref _uploadedBytes );
	public long SessionUploadedBytes => Volatile.Read( ref _sessionUploadedBytes );

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
			OStatus.Resize( clamped );
			OInfo.Resize( clamped );
			OError.Resize( clamped );
			OWarnings.Resize( clamped );
			OErrorsOnly.Resize( clamped );
			NotifySummaryChanged();
		} );
	}

	public void AddLog( BasicStatusMessage message )
	{
		_pendingLogs.Enqueue( message );
		Volatile.Write( ref _logDirty, 1 );
		ScheduleUiFlush();
	}

	private void ScheduleUiFlush()
	{
		if( Interlocked.CompareExchange( ref _uiFlushScheduled, 1, 0 ) != 0 )
			return;
		if( UiDispatcher.Current is InlineUiDispatcher )
		{
			UiDispatcher.Post( _runUiFlush );
			return;
		}
		long elapsed = Environment.TickCount64 - Volatile.Read( ref _lastUiFlushTick );
		if( elapsed >= UiFlushIntervalMs )
			UiDispatcher.Post( _runUiFlush );
		else
			_uiFlushTimer.Change( (int)( UiFlushIntervalMs - elapsed ), Timeout.Infinite );
	}

	private void RunUiFlush()
	{
		Volatile.Write( ref _lastUiFlushTick, Environment.TickCount64 );
		Volatile.Write( ref _uiFlushScheduled, 0 );

		FlushPendingLogsCore();
		if( Interlocked.Exchange( ref _progressDirty, 0 ) != 0 )
			NotifyProgressChanged();

		if( ( Volatile.Read( ref _logDirty ) != 0 || Volatile.Read( ref _progressDirty ) != 0 || !_pendingLogs.IsEmpty )
			&& Interlocked.CompareExchange( ref _uiFlushScheduled, 1, 0 ) == 0 )
		{
			_uiFlushTimer.Change( UiFlushIntervalMs, Timeout.Infinite );
		}
	}

	private void FlushPendingLogsCore()
	{
		Volatile.Write( ref _logDirty, 0 );
		if( _pendingLogs.IsEmpty )
			return;

		int drained = 0;
		_batchStatus.Clear();
		_batchInfo.Clear();
		_batchError.Clear();
		_batchWarnings.Clear();
		_batchErrorsOnly.Clear();

		while( _pendingLogs.TryDequeue( out BasicStatusMessage? message ) )
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
			OStatus.AddRange( _batchStatus );
			OInfo.AddRange( _batchInfo );
			OError.AddRange( _batchError );
			OWarnings.AddRange( _batchWarnings );
			OErrorsOnly.AddRange( _batchErrorsOnly );
			_batchStatus.Clear();
			_batchInfo.Clear();
			_batchError.Clear();
			_batchWarnings.Clear();
			_batchErrorsOnly.Clear();
			NotifySummaryChanged();
		}
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

	public void ClearLogs()
	{
		if( IsInProcess )
			return;
		lock( _gate )
		{
			( _maxCount, _processCounter, _skipCounter, _totalProcessed ) = ( 0, 0, 0, 0 );
			_downloadedBytes = 0;
			_uploadedBytes = 0;
		}
		UiDispatcher.Post( () =>
		{
			while( _pendingLogs.TryDequeue( out _ ) ) { }
			Volatile.Write( ref _logDirty, 0 );
			Volatile.Write( ref _progressDirty, 0 );
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
			_cancelSilentOperation();
			SetInProcess( false );
		}
		return close;
	}

	public void ResetProgress( int total )
	{
		lock( _gate )
		{
			( _maxCount, _processCounter, _skipCounter, _totalProcessed ) = ( total, 0, 0, 0 );
			_downloadedBytes = 0;
			_uploadedBytes = 0;
		}
		QueueProgressRefresh();
	}

	public void AddDownloadedBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		lock( _gate )
		{
			_downloadedBytes += bytes;
			_sessionDownloadedBytes += bytes;
		}
		QueueProgressRefresh();
	}

	public void AddUploadedBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		lock( _gate )
		{
			_uploadedBytes += bytes;
			_sessionUploadedBytes += bytes;
		}
		QueueProgressRefresh();
	}

	public void Advance( int processed, int skipped )
	{
		lock( _gate )
		{
			_processCounter += processed;
			_skipCounter += skipped;
			_totalProcessed = _processCounter + _skipCounter;
		}
		QueueProgressRefresh();
	}

	public void SetProcessed( int processed )
	{
		lock( _gate )
		{
			_processCounter = processed;
			_totalProcessed = _processCounter + _skipCounter;
		}
		QueueProgressRefresh();
	}

	public void CompleteProgress()
	{
		bool wasInProcess;
		lock( _gate )
		{
			wasInProcess = _isInProcess;
			_processCounter = Math.Max( _maxCount - _skipCounter, 0 );
			_totalProcessed = _maxCount;
			_isInProcess = false;
		}
		UiDispatcher.Post( () =>
		{
			FlushPendingLogsCore();
			Volatile.Write( ref _progressDirty, 0 );
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
		Volatile.Write( ref _progressDirty, 1 );
		ScheduleUiFlush();
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
