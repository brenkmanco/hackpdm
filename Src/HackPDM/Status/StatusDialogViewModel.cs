using System;
using System.Collections.Generic;
using System.Diagnostics;
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
public sealed partial class StatusDialogViewModel : ObservableObject, IDisposable
{
	private const string HistoryLengthSettingKey = "StatusHistoryLength";
	private const string AutoCloseSettingKey = "StatusAutoClose";
	private const int DefaultHistoryLength = 1000;
	private const int UiFlushIntervalMs = 75;
	private const int RateRefreshIntervalMs = 1000;
	private const int BatchListTrimThreshold = 1024;
	private const int MaxPendingTransientCeiling = 4096;

	private static readonly Lock SettingsSaveLock = new();
	private static int _cachedHistoryLength = -1;
	private static int _historySaveScheduled;

	private readonly Lock _gate = new();
	private readonly Lock _logGate = new();
	private readonly TransferRateTracker _downloadRate = new();
	private readonly TransferRateTracker _uploadRate = new();
	private readonly Action _cancelOperation;
	private readonly Action _cancelSilentOperation;
	private readonly IDialogService _dialogs;
	private readonly Queue<(long Seq, BasicStatusMessage Msg)> _pendingPriorityLogs = new();
	private readonly Queue<(long Seq, BasicStatusMessage Msg)> _pendingTransientLogs = new();
	private readonly List<(long Seq, BasicStatusMessage Msg)> _drainPriority = [];
	private readonly List<(long Seq, BasicStatusMessage Msg)> _drainTransient = [];
	private readonly List<BasicStatusMessage> _batchStatus = [];
	private readonly List<BasicStatusMessage> _batchInfo = [];
	private readonly List<BasicStatusMessage> _batchError = [];
	private readonly List<BasicStatusMessage> _batchWarnings = [];
	private readonly List<BasicStatusMessage> _batchErrorsOnly = [];
	private readonly Action _runUiFlush;
	private readonly Action _syncProgressAction;
	private readonly Timer _uiFlushTimer;
	private readonly Timer _rateRefreshTimer;

	private long _logSeq;
	private string? _latestQueuedProcessingMessage;
	private long _lastUiFlushTick;
	private long _activeToken;
	private int _uiFlushScheduled;
	private int _logDirty;
	private int _progressDirty;
	private int _disposed;
	private int _autoCloseSaveTarget;
	private int _autoCloseSaveScheduled;

	private int _maxCount;
	private int _totalProcessed;
	private int _skipCounter;
	private int _processCounter;
	private long _downloadedBytes;
	private long _sessionDownloadedBytes;
	private long _uploadedBytes;
	private long _sessionUploadedBytes;
	private long _progressStartTick;
	private long _lastEtaSampleTick;
	private int _lastEtaSampleProcessed;
	private double _emaItemsPerSec;
	private bool _isInProcess;
	private bool _rateRefreshRunning;
	private readonly bool _initialized;

	[ObservableProperty]
	public partial bool AutoClose { get; set; }

	[ObservableProperty]
	public partial string LatestProcessingMessage { get; private set; }

	private UiProgressState _ui = UiProgressState.Initial;

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
		}
	}

	public StatusDialogViewModel( IDialogService dialogs, Action cancelOperation, Action? cancelSilentOperation = null )
	{
		_dialogs = dialogs;
		_cancelOperation = cancelOperation;
		_cancelSilentOperation = cancelSilentOperation ?? cancelOperation;
		bool savedAutoClose = HackDefaults.Instance.SettingsProvider?.Get( AutoCloseSettingKey, false ) ?? false;
		_autoCloseSaveTarget = savedAutoClose ? 1 : 0;
		AutoClose = savedAutoClose;
		LatestProcessingMessage = "Idle";
		_initialized = true;

		int capacity = HistoryLength;
		OStatus = new( capacity, newestFirst: true );
		OInfo = new( capacity, newestFirst: true );
		OError = new( capacity, newestFirst: true );
		OWarnings = new( capacity, newestFirst: true );
		OErrorsOnly = new( capacity, newestFirst: true );

		_runUiFlush = RunUiFlush;
		_syncProgressAction = SyncProgressFromGate;
		_uiFlushTimer = new Timer( static state => ( (StatusDialogViewModel)state! ).OnUiFlushTimer(), this, Timeout.Infinite, Timeout.Infinite );
		_rateRefreshTimer = new Timer( static state => ( (StatusDialogViewModel)state! ).RefreshRatesIfActive(), this, Timeout.Infinite, Timeout.Infinite );
	}

	public ObservableRingBuffer<BasicStatusMessage> OStatus { get; }
	public ObservableRingBuffer<BasicStatusMessage> OInfo { get; }
	public ObservableRingBuffer<BasicStatusMessage> OError { get; }
	public ObservableRingBuffer<BasicStatusMessage> OWarnings { get; }
	public ObservableRingBuffer<BasicStatusMessage> OErrorsOnly { get; }

	public bool IsInProcess
	{
		get { lock( _gate ) return _isInProcess; }
	}

	public bool CanSortLogColumns => !_ui.IsInProcess;
	public bool IsIndeterminate => _ui.IsIndeterminate;
	public int MaxCount => _ui.MaxCount;
	public int TotalProcessed => _ui.TotalProcessed;
	public int SkipCounter => _ui.SkipCounter;
	public long DownloadedBytes => _ui.DownloadedBytes;
	public long SessionDownloadedBytes => _ui.SessionDownloadedBytes;
	public long UploadedBytes => _ui.UploadedBytes;
	public long SessionUploadedBytes => _ui.SessionUploadedBytes;

	public int InfoCount => OInfo.Count;
	public int WarningCount => OWarnings.Count;
	public int ErrorCount => OErrorsOnly.Count;

	public string ProcessingSummaryText => _ui.ProcessingSummaryText;
	public string InfoSummaryText => InfoCount == 1 ? "1 info" : $"{InfoCount} info";
	public string WarningSummaryText => WarningCount == 1 ? "1 warning" : $"{WarningCount} warnings";
	public string ErrorSummaryText => ErrorCount == 1 ? "1 error" : $"{ErrorCount} errors";

	public string CompactProgressText => _ui.CompactProgressText;
	public string ProgressText => _ui.ProgressText;
	public string EtaText => _ui.EtaText;
	public bool HasEta => _ui.HasEta;
	public string SkippedText => _ui.SkippedText;
	public string DownloadedText => _ui.DownloadedText;
	public string SessionDownloadedText => _ui.SessionDownloadedText;
	public string UploadedText => _ui.UploadedText;
	public string SessionUploadedText => _ui.SessionUploadedText;

	partial void OnAutoCloseChanged( bool value )
	{
		if( !_initialized )
			return;
		Volatile.Write( ref _autoCloseSaveTarget, value ? 1 : 0 );
		QueueSaveAutoClose();
	}

	public void SetHistoryLength( int length )
	{
		int clamped = Math.Clamp( length, 10, 1_000_000 );
		HistoryLength = clamped;
		QueueSaveHistoryLength();

		UiDispatcher.RunOrPost( () =>
		{
			OStatus.Resize( clamped );
			OInfo.Resize( clamped );
			OError.Resize( clamped );
			OWarnings.Resize( clamped );
			OErrorsOnly.Resize( clamped );
			SyncProgressFromGate();
			NotifySummaryChanged();
		} );
	}

	public void AddLog( BasicStatusMessage message )
	{
		if( Volatile.Read( ref _disposed ) != 0 )
			return;

		bool isTransient = message.Status is StatusMessage.PROCESSING or StatusMessage.FOUND;
		int transientCap = (int)Math.Clamp( (long)HistoryLength * 2, 32L, MaxPendingTransientCeiling );

		lock( _logGate )
		{
			long seq = ++_logSeq;
			if( isTransient )
			{
				if( message.Status == StatusMessage.PROCESSING && !string.IsNullOrWhiteSpace( message.Message ) )
					_latestQueuedProcessingMessage = message.Message;
				while( _pendingTransientLogs.Count >= transientCap )
					_pendingTransientLogs.Dequeue();
				_pendingTransientLogs.Enqueue( ( seq, message ) );
			}
			else
			{
				_pendingPriorityLogs.Enqueue( ( seq, message ) );
			}
		}

		Volatile.Write( ref _logDirty, 1 );
		ScheduleUiFlush();
	}

	private void OnUiFlushTimer()
	{
		if( Volatile.Read( ref _disposed ) != 0 )
			return;
		UiDispatcher.Post( _runUiFlush );
	}

	private void ScheduleUiFlush()
	{
		if( Volatile.Read( ref _disposed ) != 0 )
			return;
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
			TryChangeTimer( _uiFlushTimer, (int)( UiFlushIntervalMs - elapsed ), Timeout.Infinite );
	}

	private void RunUiFlush()
	{
		if( Volatile.Read( ref _disposed ) != 0 )
			return;
		try
		{
			FlushPendingLogsCore();
			if( Interlocked.Exchange( ref _progressDirty, 0 ) != 0 )
				SyncProgressFromGate();
			else
				SyncProcessingSummaryIfChanged();
		}
		finally
		{
			Volatile.Write( ref _lastUiFlushTick, Environment.TickCount64 );
			Volatile.Write( ref _uiFlushScheduled, 0 );
		}

		if( Volatile.Read( ref _disposed ) == 0
			&& ( Volatile.Read( ref _logDirty ) != 0 || Volatile.Read( ref _progressDirty ) != 0 || HasPendingLogs() )
			&& Interlocked.CompareExchange( ref _uiFlushScheduled, 1, 0 ) == 0 )
		{
			if( UiDispatcher.Current is InlineUiDispatcher )
				UiDispatcher.Post( _runUiFlush );
			else
				TryChangeTimer( _uiFlushTimer, UiFlushIntervalMs, Timeout.Infinite );
		}
	}

	private bool HasPendingLogs()
	{
		lock( _logGate )
			return _pendingPriorityLogs.Count > 0 || _pendingTransientLogs.Count > 0;
	}

	private void FlushPendingLogsCore()
	{
		Volatile.Write( ref _logDirty, 0 );

		string? latestFromQueue;
		lock( _logGate )
		{
			if( _pendingPriorityLogs.Count == 0 && _pendingTransientLogs.Count == 0 )
			{
				latestFromQueue = _latestQueuedProcessingMessage;
				_latestQueuedProcessingMessage = null;
				if( !string.IsNullOrWhiteSpace( latestFromQueue ) )
					LatestProcessingMessage = latestFromQueue;
				return;
			}

			_drainPriority.Clear();
			while( _pendingPriorityLogs.TryDequeue( out var entry ) )
				_drainPriority.Add( entry );

			_drainTransient.Clear();
			while( _pendingTransientLogs.TryDequeue( out var entry ) )
				_drainTransient.Add( entry );

			latestFromQueue = _latestQueuedProcessingMessage;
			_latestQueuedProcessingMessage = null;
		}

		string nextLatest = LatestProcessingMessage;
		_batchStatus.Clear();
		_batchInfo.Clear();
		_batchError.Clear();
		_batchWarnings.Clear();
		_batchErrorsOnly.Clear();

		int pi = 0;
		int ti = 0;
		while( pi < _drainPriority.Count || ti < _drainTransient.Count )
		{
			BasicStatusMessage message;
			if( ti >= _drainTransient.Count || ( pi < _drainPriority.Count && _drainPriority[ pi ].Seq <= _drainTransient[ ti ].Seq ) )
				message = _drainPriority[ pi++ ].Msg;
			else
				message = _drainTransient[ ti++ ].Msg;

			switch( message.Status )
			{
				case StatusMessage.PROCESSING or StatusMessage.SUCCESS:
					_batchStatus.Add( message );
					if( !string.IsNullOrWhiteSpace( message.Message ) )
						nextLatest = message.Message;
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
		}

		_drainPriority.Clear();
		_drainTransient.Clear();

		if( !string.IsNullOrWhiteSpace( latestFromQueue ) && _batchStatus.Count == 0 )
			nextLatest = latestFromQueue;

		bool statusAdded = _batchStatus.Count > 0;
		bool infoAdded = _batchInfo.Count > 0;
		bool warningsAdded = _batchWarnings.Count > 0;
		bool errorsAdded = _batchErrorsOnly.Count > 0;

		if( statusAdded )
			OStatus.AddRange( _batchStatus );
		if( infoAdded )
			OInfo.AddRange( _batchInfo );
		if( _batchError.Count > 0 )
			OError.AddRange( _batchError );
		if( warningsAdded )
			OWarnings.AddRange( _batchWarnings );
		if( errorsAdded )
			OErrorsOnly.AddRange( _batchErrorsOnly );

		ResetBatchList( _batchStatus );
		ResetBatchList( _batchInfo );
		ResetBatchList( _batchError );
		ResetBatchList( _batchWarnings );
		ResetBatchList( _batchErrorsOnly );

		if( infoAdded )
		{
			OnPropertyChanged( nameof( InfoCount ) );
			OnPropertyChanged( nameof( InfoSummaryText ) );
		}
		if( warningsAdded )
		{
			OnPropertyChanged( nameof( WarningCount ) );
			OnPropertyChanged( nameof( WarningSummaryText ) );
		}
		if( errorsAdded )
		{
			OnPropertyChanged( nameof( ErrorCount ) );
			OnPropertyChanged( nameof( ErrorSummaryText ) );
		}

		LatestProcessingMessage = nextLatest;
		if( statusAdded )
			SyncProcessingSummaryIfChanged();
	}

	private static void ResetBatchList( List<BasicStatusMessage> batch )
	{
		batch.Clear();
		if( batch.Capacity > BatchListTrimThreshold )
			batch.TrimExcess();
	}

	[RelayCommand( CanExecute = nameof( CanCancel ) )]
	private void Cancel() => _cancelOperation();

	private bool CanCancel() => IsInProcess;

	public void SetInProcess( bool inProcess ) => SetInProcessCore( inProcess, token: null );

	internal void SetInProcess( bool inProcess, long token ) => SetInProcessCore( inProcess, token );

	internal void TryClearInProcess( long expectedToken )
	{
		bool changed = false;
		lock( _gate )
		{
			if( _activeToken != expectedToken )
				return;
			if( _isInProcess )
			{
				_isInProcess = false;
				changed = true;
			}
		}
		if( changed )
			UiDispatcher.RunOrPost( _syncProgressAction );
	}

	private void SetInProcessCore( bool inProcess, long? token )
	{
		bool changed;
		lock( _gate )
		{
			if( token is long t )
				_activeToken = t;
			else if( !inProcess )
				_activeToken++;
			changed = _isInProcess != inProcess;
			_isInProcess = inProcess;
			if( inProcess && changed && _progressStartTick == 0 && _maxCount > 0 )
			{
				_progressStartTick = Stopwatch.GetTimestamp();
				_lastEtaSampleTick = _progressStartTick;
			}
		}
		if( changed )
			UiDispatcher.RunOrPost( _syncProgressAction );
	}

	public void ClearLogs()
	{
		bool stopTimer;
		lock( _gate )
		{
			if( _isInProcess )
				return;
			( _maxCount, _processCounter, _skipCounter, _totalProcessed ) = ( 0, 0, 0, 0 );
			_downloadedBytes = 0;
			_uploadedBytes = 0;
			ResetEtaStateLocked( total: 0 );
			_downloadRate.Reset();
			_uploadRate.Reset();
			stopTimer = TakeStopRateTimerLocked();
		}
		if( stopTimer )
			TryChangeTimer( _rateRefreshTimer, Timeout.Infinite, Timeout.Infinite );

		CancelPendingUiFlush();

		UiDispatcher.RunOrPost( () =>
		{
			if( IsInProcess )
				return;
			CancelPendingUiFlush();
			OStatus.Clear();
			OInfo.Clear();
			OError.Clear();
			OWarnings.Clear();
			OErrorsOnly.Clear();
			LatestProcessingMessage = "Idle";
			SyncProgressFromGate();
			NotifySummaryChanged();
		} );
	}

	private void CancelPendingUiFlush()
	{
		TryChangeTimer( _uiFlushTimer, Timeout.Infinite, Timeout.Infinite );
		lock( _logGate )
		{
			_pendingPriorityLogs.Clear();
			_pendingTransientLogs.Clear();
			_latestQueuedProcessingMessage = null;
		}
		Volatile.Write( ref _logDirty, 0 );
		Volatile.Write( ref _progressDirty, 0 );
		Volatile.Write( ref _uiFlushScheduled, 0 );
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
		bool stopTimer;
		lock( _gate )
		{
			( _maxCount, _processCounter, _skipCounter, _totalProcessed ) = ( total, 0, 0, 0 );
			_downloadedBytes = 0;
			_uploadedBytes = 0;
			ResetEtaStateLocked( total );
			_downloadRate.Reset();
			_uploadRate.Reset();
			stopTimer = TakeStopRateTimerLocked();
		}
		if( stopTimer )
			TryChangeTimer( _rateRefreshTimer, Timeout.Infinite, Timeout.Infinite );
		QueueProgressRefresh();
	}

	public void BeginDownloadTransfer()
	{
		bool startTimer;
		lock( _gate )
		{
			_downloadRate.BeginTransfer();
			startTimer = TakeStartRateTimerLocked();
		}
		if( startTimer )
			TryChangeTimer( _rateRefreshTimer, RateRefreshIntervalMs, RateRefreshIntervalMs );
		QueueProgressRefresh();
	}

	public void EndDownloadTransfer()
	{
		bool stopTimer;
		lock( _gate )
		{
			_downloadRate.EndTransfer();
			stopTimer = TakeStopRateTimerLocked();
		}
		if( stopTimer )
			TryChangeTimer( _rateRefreshTimer, Timeout.Infinite, Timeout.Infinite );
		QueueProgressRefresh();
	}

	public void BeginUploadTransfer()
	{
		bool startTimer;
		lock( _gate )
		{
			_uploadRate.BeginTransfer();
			startTimer = TakeStartRateTimerLocked();
		}
		if( startTimer )
			TryChangeTimer( _rateRefreshTimer, RateRefreshIntervalMs, RateRefreshIntervalMs );
		QueueProgressRefresh();
	}

	public void EndUploadTransfer()
	{
		bool stopTimer;
		lock( _gate )
		{
			_uploadRate.EndTransfer();
			stopTimer = TakeStopRateTimerLocked();
		}
		if( stopTimer )
			TryChangeTimer( _rateRefreshTimer, Timeout.Infinite, Timeout.Infinite );
		QueueProgressRefresh();
	}

	public void AddDownloadedBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		bool startTimer;
		lock( _gate )
		{
			_downloadedBytes += bytes;
			_sessionDownloadedBytes += bytes;
			_downloadRate.AddBytes( bytes );
			startTimer = TakeStartRateTimerLocked();
		}
		if( startTimer )
			TryChangeTimer( _rateRefreshTimer, RateRefreshIntervalMs, RateRefreshIntervalMs );
		QueueProgressRefresh();
	}

	public void AddUploadedBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		bool startTimer;
		lock( _gate )
		{
			_uploadedBytes += bytes;
			_sessionUploadedBytes += bytes;
			_uploadRate.AddBytes( bytes );
			startTimer = TakeStartRateTimerLocked();
		}
		if( startTimer )
			TryChangeTimer( _rateRefreshTimer, RateRefreshIntervalMs, RateRefreshIntervalMs );
		QueueProgressRefresh();
	}

	public void Advance( int processed, int skipped )
	{
		lock( _gate )
		{
			if( _progressStartTick == 0 && _maxCount > 0 )
			{
				_progressStartTick = Stopwatch.GetTimestamp();
				_lastEtaSampleTick = _progressStartTick;
			}
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
			if( _progressStartTick == 0 && _maxCount > 0 )
			{
				_progressStartTick = Stopwatch.GetTimestamp();
				_lastEtaSampleTick = _progressStartTick;
			}
			_processCounter = processed;
			_totalProcessed = _processCounter + _skipCounter;
		}
		QueueProgressRefresh();
	}

	public void CompleteProgress() => CompleteProgressCore( expectedToken: null );

	internal void CompleteProgress( long expectedToken ) => CompleteProgressCore( expectedToken );

	private void CompleteProgressCore( long? expectedToken )
	{
		bool wasInProcess;
		long completedToken;
		bool stopTimer;
		lock( _gate )
		{
			if( expectedToken is long expected && _activeToken != expected )
				return;
			completedToken = _activeToken;
			wasInProcess = _isInProcess;
			_processCounter = Math.Max( _maxCount - _skipCounter, 0 );
			_totalProcessed = _maxCount;
			_isInProcess = false;
			_emaItemsPerSec = 0;
			_downloadRate.EndTransfer();
			_uploadRate.EndTransfer();
			stopTimer = TakeStopRateTimerLocked();
		}
		if( stopTimer )
			TryChangeTimer( _rateRefreshTimer, Timeout.Infinite, Timeout.Infinite );
		TryChangeTimer( _uiFlushTimer, Timeout.Infinite, Timeout.Infinite );
		Volatile.Write( ref _uiFlushScheduled, 0 );

		UiDispatcher.RunOrPost( () =>
		{
			if( Volatile.Read( ref _disposed ) != 0 )
				return;
			TryChangeTimer( _uiFlushTimer, Timeout.Infinite, Timeout.Infinite );
			Volatile.Write( ref _progressDirty, 0 );
			FlushPendingLogsCore();
			SyncProgressFromGate();

			bool stillCompleted;
			lock( _gate )
				stillCompleted = !_isInProcess && _activeToken == completedToken;
			if( wasInProcess && stillCompleted )
				OperationCompleted?.Invoke();
		} );
	}

	private bool TakeStartRateTimerLocked()
	{
		if( _rateRefreshRunning || Volatile.Read( ref _disposed ) != 0 || UiDispatcher.Current is InlineUiDispatcher )
			return false;
		_rateRefreshRunning = true;
		return true;
	}

	private bool TakeStopRateTimerLocked()
	{
		if( !_rateRefreshRunning || _downloadRate.IsActive || _uploadRate.IsActive )
			return false;
		_rateRefreshRunning = false;
		return true;
	}

	private void RefreshRatesIfActive()
	{
		if( Volatile.Read( ref _disposed ) != 0 )
			return;
		bool active;
		lock( _gate )
		{
			active = _downloadRate.IsActive || _uploadRate.IsActive;
			if( !active )
				_rateRefreshRunning = false;
		}
		if( active )
			QueueProgressRefresh();
		else
			TryChangeTimer( _rateRefreshTimer, Timeout.Infinite, Timeout.Infinite );
	}

	[RelayCommand]
	private async Task OpenLogsFolderAsync()
	{
		string dir = HackLogger.LogDirectory;
		try
		{
			await Task.Run( () =>
			{
				Directory.CreateDirectory( dir );
				ShellLauncher.OpenFolder( dir );
			} );
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
		if( Volatile.Read( ref _disposed ) != 0 )
			return;
		Volatile.Write( ref _progressDirty, 1 );
		ScheduleUiFlush();
	}

	private void SyncProgressFromGate()
	{
		ProgressSnapshot snap;
		lock( _gate )
			snap = CaptureProgressSnapshotLocked();
		PublishProgressSnapshot( in snap );
	}

	private void SyncProcessingSummaryIfChanged()
	{
		string nextSummary = FormatProcessingSummary( _ui.IsInProcess, _ui.MaxCount, _ui.TotalProcessed, OStatus.Count );
		if( string.Equals( nextSummary, _ui.ProcessingSummaryText, StringComparison.Ordinal ) )
			return;
		_ui = _ui with { ProcessingSummaryText = nextSummary };
		OnPropertyChanged( nameof( ProcessingSummaryText ) );
	}

	private ProgressSnapshot CaptureProgressSnapshotLocked()
	{
		string etaText = ComputeEtaTextLocked();
		return new ProgressSnapshot(
			_isInProcess,
			_maxCount,
			_totalProcessed,
			_skipCounter,
			_downloadedBytes,
			_sessionDownloadedBytes,
			_uploadedBytes,
			_sessionUploadedBytes,
			_downloadRate.IsActive,
			_downloadRate.CurrentMbps,
			_uploadRate.IsActive,
			_uploadRate.CurrentMbps,
			etaText );
	}

	private void PublishProgressSnapshot( in ProgressSnapshot snap )
	{
		UiProgressState prev = _ui;
		bool isIndeterminate = snap.IsInProcess && snap.MaxCount <= 0;

		string dlSpeed = TransferRateTracker.FormatMbps( snap.DownloadMbps );
		string ulSpeed = TransferRateTracker.FormatMbps( snap.UploadMbps );
		string activeSpeed = ( snap.DownloadActive, snap.UploadActive ) switch
		{
			( true, false ) => dlSpeed,
			( false, true ) => ulSpeed,
			_ => dlSpeed.Length > 0 ? dlSpeed : ulSpeed,
		};

		string compactProgressText;
		if( snap.MaxCount <= 0 )
		{
			compactProgressText = snap.IsInProcess ? "Working..." : "Ready";
		}
		else
		{
			float percent = snap.TotalProcessed / (float)snap.MaxCount * 100f;
			compactProgressText = activeSpeed.Length > 0
				? $"{snap.TotalProcessed}/{snap.MaxCount} ({percent:f0}%) • {activeSpeed}"
				: $"{snap.TotalProcessed}/{snap.MaxCount} ({percent:f0}%)";
		}

		float fullPercent = snap.MaxCount > 0 ? snap.TotalProcessed / (float)snap.MaxCount * 100f : 0f;
		string progressText = $"({fullPercent:f2}%)\n{snap.TotalProcessed} / {snap.MaxCount}";
		string skippedText = $"({snap.SkipCounter}) Skipped";
		string downloadedText = dlSpeed.Length > 0
			? $"Downloaded: {ByteSize.Format( snap.DownloadedBytes )} ({dlSpeed})"
			: $"Downloaded: {ByteSize.Format( snap.DownloadedBytes )}";
		string sessionDownloadedText = $"Session Downloaded: {ByteSize.Format( snap.SessionDownloadedBytes )}";
		string uploadedText = ulSpeed.Length > 0
			? $"Uploaded: {ByteSize.Format( snap.UploadedBytes )} ({ulSpeed})"
			: $"Uploaded: {ByteSize.Format( snap.UploadedBytes )}";
		string sessionUploadedText = $"Session Uploaded: {ByteSize.Format( snap.SessionUploadedBytes )}";
		string processingSummaryText = FormatProcessingSummary( snap.IsInProcess, snap.MaxCount, snap.TotalProcessed, OStatus.Count );
		bool hasEta = snap.EtaText.Length > 0;

		UiProgressState next = new(
			snap.IsInProcess,
			isIndeterminate,
			snap.MaxCount,
			snap.TotalProcessed,
			snap.SkipCounter,
			snap.DownloadedBytes,
			snap.SessionDownloadedBytes,
			snap.UploadedBytes,
			snap.SessionUploadedBytes,
			snap.EtaText,
			hasEta,
			compactProgressText,
			progressText,
			skippedText,
			downloadedText,
			sessionDownloadedText,
			uploadedText,
			sessionUploadedText,
			processingSummaryText );

		_ui = next;

		if( next.IsInProcess != prev.IsInProcess )
		{
			OnPropertyChanged( nameof( IsInProcess ) );
			OnPropertyChanged( nameof( CanSortLogColumns ) );
			CancelCommand.NotifyCanExecuteChanged();
		}
		if( next.IsIndeterminate != prev.IsIndeterminate )
			OnPropertyChanged( nameof( IsIndeterminate ) );
		if( next.MaxCount != prev.MaxCount )
			OnPropertyChanged( nameof( MaxCount ) );
		if( next.TotalProcessed != prev.TotalProcessed )
			OnPropertyChanged( nameof( TotalProcessed ) );
		if( next.SkipCounter != prev.SkipCounter )
			OnPropertyChanged( nameof( SkipCounter ) );
		if( next.DownloadedBytes != prev.DownloadedBytes )
			OnPropertyChanged( nameof( DownloadedBytes ) );
		if( next.SessionDownloadedBytes != prev.SessionDownloadedBytes )
			OnPropertyChanged( nameof( SessionDownloadedBytes ) );
		if( next.UploadedBytes != prev.UploadedBytes )
			OnPropertyChanged( nameof( UploadedBytes ) );
		if( next.SessionUploadedBytes != prev.SessionUploadedBytes )
			OnPropertyChanged( nameof( SessionUploadedBytes ) );
		if( !string.Equals( next.EtaText, prev.EtaText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( EtaText ) );
		if( next.HasEta != prev.HasEta )
			OnPropertyChanged( nameof( HasEta ) );
		if( !string.Equals( next.CompactProgressText, prev.CompactProgressText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( CompactProgressText ) );
		if( !string.Equals( next.ProgressText, prev.ProgressText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( ProgressText ) );
		if( !string.Equals( next.SkippedText, prev.SkippedText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( SkippedText ) );
		if( !string.Equals( next.DownloadedText, prev.DownloadedText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( DownloadedText ) );
		if( !string.Equals( next.SessionDownloadedText, prev.SessionDownloadedText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( SessionDownloadedText ) );
		if( !string.Equals( next.UploadedText, prev.UploadedText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( UploadedText ) );
		if( !string.Equals( next.SessionUploadedText, prev.SessionUploadedText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( SessionUploadedText ) );
		if( !string.Equals( next.ProcessingSummaryText, prev.ProcessingSummaryText, StringComparison.Ordinal ) )
			OnPropertyChanged( nameof( ProcessingSummaryText ) );
	}

	private static string FormatProcessingSummary( bool isInProcess, int maxCount, int totalProcessed, int statusCount )
		=> isInProcess
			? ( maxCount > 0 ? $"Processing {totalProcessed}/{maxCount}" : "Processing..." )
			: ( statusCount > 0 ? $"{statusCount} ops" : "Idle" );

	private void ResetEtaStateLocked( int total )
	{
		_progressStartTick = total > 0 && _isInProcess ? Stopwatch.GetTimestamp() : 0;
		_lastEtaSampleTick = _progressStartTick;
		_lastEtaSampleProcessed = 0;
		_emaItemsPerSec = 0;
	}

	private string ComputeEtaTextLocked()
	{
		if( !_isInProcess || _maxCount <= 0 || _totalProcessed <= 0 || _totalProcessed >= _maxCount )
			return string.Empty;

		long now = Stopwatch.GetTimestamp();
		if( _progressStartTick == 0 )
		{
			_progressStartTick = now;
			_lastEtaSampleTick = now;
			_lastEtaSampleProcessed = _totalProcessed;
			return string.Empty;
		}

		double sampleDt = (double)( now - _lastEtaSampleTick ) / Stopwatch.Frequency;
		if( sampleDt >= 0.35 )
		{
			int deltaItems = _totalProcessed - _lastEtaSampleProcessed;
			if( deltaItems > 0 )
			{
				double instantRate = deltaItems / sampleDt;
				const double alpha = 0.30;
				_emaItemsPerSec = _emaItemsPerSec <= 0
					? instantRate
					: _emaItemsPerSec * ( 1.0 - alpha ) + instantRate * alpha;
				_lastEtaSampleTick = now;
				_lastEtaSampleProcessed = _totalProcessed;
			}
			else if( sampleDt >= 5.0 && _emaItemsPerSec > 0 )
			{
				_emaItemsPerSec *= 0.7;
				_lastEtaSampleTick = now;
			}
		}

		double totalElapsed = (double)( now - _progressStartTick ) / Stopwatch.Frequency;
		double rate = _emaItemsPerSec > 0
			? _emaItemsPerSec
			: ( totalElapsed >= 0.5 && _totalProcessed >= 2 ? _totalProcessed / totalElapsed : 0 );

		if( rate <= 0.001 )
			return string.Empty;

		int remaining = _maxCount - _totalProcessed;
		return FormatEta( remaining / rate );
	}

	private static string FormatEta( double seconds )
	{
		if( double.IsNaN( seconds ) || double.IsInfinity( seconds ) || seconds < 0 )
			return string.Empty;
		if( seconds < 1.0 )
			return "ETA <1s";
		int totalSec = (int)Math.Min( Math.Round( seconds ), 86400 * 7 );
		if( totalSec < 60 )
			return $"ETA {totalSec}s";
		int minutes = totalSec / 60;
		int remSec = totalSec % 60;
		if( minutes < 60 )
			return $"ETA {minutes}m {remSec:D2}s";
		int hours = minutes / 60;
		int remMin = minutes % 60;
		return hours < 24 ? $"ETA {hours}h {remMin:D2}m" : "ETA >24h";
	}

	private void NotifySummaryChanged()
	{
		OnPropertyChanged( nameof( InfoCount ) );
		OnPropertyChanged( nameof( WarningCount ) );
		OnPropertyChanged( nameof( ErrorCount ) );
		OnPropertyChanged( nameof( InfoSummaryText ) );
		OnPropertyChanged( nameof( WarningSummaryText ) );
		OnPropertyChanged( nameof( ErrorSummaryText ) );
	}

	private static void QueueSaveHistoryLength()
	{
		if( Interlocked.CompareExchange( ref _historySaveScheduled, 1, 0 ) != 0 )
			return;
		_ = Task.Run( () =>
		{
			try
			{
				lock( SettingsSaveLock )
				{
					Volatile.Write( ref _historySaveScheduled, 0 );
					int value = Volatile.Read( ref _cachedHistoryLength );
					if( value >= 10 )
						HackDefaults.Instance.SettingsProvider?.Set( HistoryLengthSettingKey, value );
				}
			}
			catch( Exception ex )
			{
				Volatile.Write( ref _historySaveScheduled, 0 );
				HackLogger.Warn( "StatusDialog", $"Could not persist {HistoryLengthSettingKey}: {ex.Message}", ex );
			}
		} );
	}

	private void QueueSaveAutoClose()
	{
		if( Interlocked.CompareExchange( ref _autoCloseSaveScheduled, 1, 0 ) != 0 )
			return;
		_ = Task.Run( () =>
		{
			try
			{
				lock( SettingsSaveLock )
				{
					Volatile.Write( ref _autoCloseSaveScheduled, 0 );
					bool value = Volatile.Read( ref _autoCloseSaveTarget ) != 0;
					HackDefaults.Instance.SettingsProvider?.Set( AutoCloseSettingKey, value );
				}
			}
			catch( Exception ex )
			{
				Volatile.Write( ref _autoCloseSaveScheduled, 0 );
				HackLogger.Warn( "StatusDialog", $"Could not persist {AutoCloseSettingKey}: {ex.Message}", ex );
			}
		} );
	}

	private void TryChangeTimer( Timer timer, int dueTimeMs, int periodMs )
	{
		if( Volatile.Read( ref _disposed ) != 0 )
			return;
		try { timer.Change( dueTimeMs, periodMs ); }
		catch( ObjectDisposedException ) { }
	}

	public void Dispose()
	{
		if( Interlocked.Exchange( ref _disposed, 1 ) != 0 )
			return;
		_uiFlushTimer.Dispose();
		_rateRefreshTimer.Dispose();
	}

	private readonly record struct ProgressSnapshot(
		bool IsInProcess,
		int MaxCount,
		int TotalProcessed,
		int SkipCounter,
		long DownloadedBytes,
		long SessionDownloadedBytes,
		long UploadedBytes,
		long SessionUploadedBytes,
		bool DownloadActive,
		double DownloadMbps,
		bool UploadActive,
		double UploadMbps,
		string EtaText );

	private readonly record struct UiProgressState(
		bool IsInProcess,
		bool IsIndeterminate,
		int MaxCount,
		int TotalProcessed,
		int SkipCounter,
		long DownloadedBytes,
		long SessionDownloadedBytes,
		long UploadedBytes,
		long SessionUploadedBytes,
		string EtaText,
		bool HasEta,
		string CompactProgressText,
		string ProgressText,
		string SkippedText,
		string DownloadedText,
		string SessionDownloadedText,
		string UploadedText,
		string SessionUploadedText,
		string ProcessingSummaryText )
	{
		public static readonly UiProgressState Initial = new(
			IsInProcess: false,
			IsIndeterminate: false,
			MaxCount: 0,
			TotalProcessed: 0,
			SkipCounter: 0,
			DownloadedBytes: 0,
			SessionDownloadedBytes: 0,
			UploadedBytes: 0,
			SessionUploadedBytes: 0,
			EtaText: string.Empty,
			HasEta: false,
			CompactProgressText: "Ready",
			ProgressText: "(0.00%)\n0 / 0",
			SkippedText: "(0) Skipped",
			DownloadedText: "Downloaded: 0 B",
			SessionDownloadedText: "Session Downloaded: 0 B",
			UploadedText: "Uploaded: 0 B",
			SessionUploadedText: "Session Uploaded: 0 B",
			ProcessingSummaryText: "Idle" );
	}
}
