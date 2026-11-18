using System;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using HackPDM.Abstractions.Structures;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Settings;

/// <summary>
/// The status window's state: the log of the running operation, split by kind, and its progress.
/// <see cref="StatusWindowReporter"/> writes the counters from any thread; the window is told on the
/// UI thread, at most once per pending change, so it never polls.
/// </summary>
public partial class StatusDialogViewModel : ObservableObject, IRecipient<BasicStatusMessage>
{
	private readonly Lock _gate = new();
	private readonly Action _cancelOperation;
	private readonly IDialogService _dialogs;
	private int _refreshQueued;
	private bool _isInProcess;
	private bool _autoClose;

	/// <summary>Raised on the UI thread when an operation finishes (<see cref="CompleteProgress"/>).</summary>
	public event Action? OperationCompleted;

	/// <summary>Creates the window state with a new <see cref="StatusLogSession"/> and subscribes to its messages.</summary>
	/// <param name="dialogs">Asks the user before closing during an operation.</param>
	/// <param name="cancelOperation">Cancels the running operation (the Cancel button, or closing while busy).</param>
	/// <remarks>Registers with <see cref="WeakReferenceMessenger.Default"/> using the session id as the token.</remarks>
	public StatusDialogViewModel( IDialogService dialogs, Action cancelOperation )
	{
		_dialogs = dialogs;
		_cancelOperation = cancelOperation;
		_autoClose = HackDefaults.Instance?.SettingsProvider?.Get( "StatusAutoClose", false ) ?? false;
		Session = new StatusLogSession();
		WeakReferenceMessenger.Default.Register( this, Session.SessionId );
	}

	/// <summary>Messages logged to this session land in the window's lists.</summary>
	public StatusLogSession Session { get; }

	public ObservableRingBuffer<BasicStatusMessage> OStatus { get; } = new( HistoryLength );
	public ObservableRingBuffer<BasicStatusMessage> OInfo { get; } = new( HistoryLength );
	public ObservableRingBuffer<BasicStatusMessage> OError { get; } = new( HistoryLength );
	public ObservableRingBuffer<BasicStatusMessage> OWarnings { get; } = new( HistoryLength );
	public ObservableRingBuffer<BasicStatusMessage> OErrorsOnly { get; } = new( HistoryLength );

	public int ProcessingCount { get; private set; }
	public int SuccessCount { get; private set; }
	public int InfoCount => OInfo.Count;
	public int WarningCount => OWarnings.Count;
	public int ErrorCount => OErrorsOnly.Count;
	public string ProcessingSummaryText => IsInProcess ? $"Processing ({ProcessingCount})" : ( ProcessingCount + SuccessCount ) > 0 ? $"Processing ({ProcessingCount + SuccessCount})" : "Processing (0)";
	public string InfoSummaryText => $"Info ({InfoCount})";
	public string WarningSummaryText => $"Warnings ({WarningCount})";
	public string ErrorSummaryText => $"Errors ({ErrorCount})";
	public string LatestProcessingMessage { get; private set; } = "Idle";
	public string LatestInfoMessage { get; private set; } = string.Empty;
	public string CompactProgressText
	{
		get
		{
			if( MaxCount <= 0 )
				return IsInProcess ? "Working…" : "Idle";
			float percent = TotalProcessed / (float)MaxCount * 100f;
			return $"{TotalProcessed} / {MaxCount} ({percent:F0}%)";
		}
	}

	/// <summary>True from the start of an operation until it completes or is cancelled.</summary>
	public bool IsInProcess
	{
		get { lock( _gate ) return _isInProcess; }
		set => SetInProcess( value );
	}

	/// <summary>Whether the status window should close automatically once the operation completes.</summary>
	public bool AutoClose
	{
		get => _autoClose;
		set
		{
			if( !SetProperty( ref _autoClose, value ) )
				return;
			HackDefaults.Instance?.SettingsProvider?.Set( "StatusAutoClose", value );
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

	public string ProgressText
	{
		get
		{
			float percent = MaxCount > 0 ? TotalProcessed / (float)MaxCount * 100 : 0;
			return $"({percent:f2}%)\n{TotalProcessed} / {MaxCount}";
		}
	}

	public string SkippedText => $"({SkipCounter}) Skipped";
	public string DownloadedText => $"Downloaded: {ByteSize.Format( DownloadedBytes )}";
	public string SessionDownloadedText => $"Session Downloaded: {ByteSize.Format( SessionDownloadedBytes )}";
	public string UploadedText => $"Uploaded: {ByteSize.Format( UploadedBytes )}";
	public string SessionUploadedText => $"Session Uploaded: {ByteSize.Format( SessionUploadedBytes )}";

	/// <summary>How many lines each log list keeps before the oldest are dropped.</summary>
	/// <value>Read from the app settings at startup (10000 by default); setting it saves the new value but does not
	/// resize lists that already exist.</value>
	public static int HistoryLength
	{
		get;
		set
		{
			field = value;
			HackDefaults.Instance?.SettingsProvider?.Set( "HistoryLength", field );
		}
	} = HackDefaults.Instance?.SettingsProvider?.Get( "HistoryLength", 10000 ) ?? 10000;

	/// <summary>Receives a line logged to <see cref="Session"/> and adds it to its list.</summary>
	/// <param name="message">The logged line.</param>
	public void Receive( BasicStatusMessage message ) => AddLog( message );

	/// <summary>Picks the list a kind of log line belongs in.</summary>
	/// <param name="action">The kind of line.</param>
	/// <returns><see cref="OStatus"/> for processing and success lines, <see cref="OError"/> for warnings and errors,
	/// <see cref="OInfo"/> for everything else.</returns>
	public ObservableRingBuffer<BasicStatusMessage> GetCollection( StatusMessage action ) => action switch
	{
		StatusMessage.PROCESSING or StatusMessage.SUCCESS => OStatus,
		StatusMessage.WARNING or StatusMessage.ERROR => OError,
		_ => OInfo,
	};

	/// <summary>Adds a log line to its list.</summary>
	/// <param name="message">The line to add.</param>
	/// <remarks>Safe from any thread: the list is changed later on the UI thread through <see cref="UiDispatcher.Post"/>.</remarks>
	public void AddLog( BasicStatusMessage message )
		=> UiDispatcher.Post( () =>
		{
			GetCollection( message.Status ).Add( message );
			switch( message.Status )
			{
				case StatusMessage.WARNING:
					OWarnings.Add( message );
					break;
				case StatusMessage.ERROR:
					OErrorsOnly.Add( message );
					break;
				case StatusMessage.PROCESSING:
					ProcessingCount++;
					if( message.Message is { Length: > 0 } procMsg )
						LatestProcessingMessage = procMsg;
					break;
				case StatusMessage.SUCCESS:
					SuccessCount++;
					if( message.Message is { Length: > 0 } succMsg )
						LatestProcessingMessage = succMsg;
					break;
				default:
					if( message.Message is { Length: > 0 } infoMsg )
						LatestInfoMessage = infoMsg;
					break;
			}
			NotifyCategoryCountsChanged();
		} );

	/// <summary>Cancels the running operation.</summary>
	[RelayCommand( CanExecute = nameof( CanCancel ) )]
	private void Cancel() => _cancelOperation();

	private bool CanCancel() => IsInProcess;

	/// <summary>Updates <see cref="IsInProcess"/> safely from any thread and refreshes UI command state.</summary>
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

	/// <summary>Updates <see cref="HistoryLength"/>, persists it, and resizes the log buffers.</summary>
	public void SetHistoryLength( int length )
	{
		if( length <= 0 )
			throw new ArgumentOutOfRangeException( nameof( length ), "History length must be positive." );
		HistoryLength = length;
		UiDispatcher.Post( () =>
		{
			OStatus.Resize( length );
			OInfo.Resize( length );
			OError.Resize( length );
			OWarnings.Resize( length );
			OErrorsOnly.Resize( length );
		} );
	}

	/// <summary>Clears all status log lists and resets counters when idle.</summary>
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
			OStatus.Clear();
			OInfo.Clear();
			OError.Clear();
			OWarnings.Clear();
			OErrorsOnly.Clear();
			ProcessingCount = 0;
			SuccessCount = 0;
			LatestProcessingMessage = "Idle";
			LatestInfoMessage = string.Empty;
			NotifyProgressChanged();
			NotifyCategoryCountsChanged();
		} );
	}

	/// <summary>
	/// Whether the window may close: always when idle; while an operation runs only once the user
	/// confirms, which also cancels the operation.
	/// </summary>
	/// <returns><see langword="true"/> when the window may close.</returns>
	public async Task<bool> ConfirmCloseAsync()
	{
		if( !IsInProcess )
			return true;
		bool close = DialogResult.OK == await _dialogs.ShowMessageAsync(
			"There are items still processing..\nWould you like to continue and close the window and operations?",
			"Cancel Operation?",
			MessageBoxButtons.OKCancel, MessageBoxIcon.Warning );
		if( close )
		{
			_cancelOperation();
			SetInProcess( false );
		}
		return close;
	}

	// ---------------------------------------------------------------- progress, from any thread

	/// <summary>Starts the progress over for a new number of items.</summary>
	/// <param name="total">The number of items the operation will process.</param>
	/// <remarks>Safe from any thread; the window is refreshed on the UI thread.</remarks>
	public void ResetProgress( int total )
		=> Update( () =>
		{
			( MaxCount, ProcessCounter, SkipCounter, TotalProcessed ) = ( total, 0, 0, 0 );
			DownloadedBytes = 0;
			UploadedBytes = 0;
		} );

	/// <summary>Adds downloaded bytes to the current operation and session totals.</summary>
	public void AddDownloadedBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		Update( () =>
		{
			DownloadedBytes += bytes;
			SessionDownloadedBytes += bytes;
		} );
	}

	/// <summary>Adds uploaded bytes to the current operation and session totals.</summary>
	public void AddUploadedBytes( long bytes )
	{
		if( bytes <= 0 )
			return;
		Update( () =>
		{
			UploadedBytes += bytes;
			SessionUploadedBytes += bytes;
		} );
	}

	/// <summary>Adds to the processed and skipped counters.</summary>
	/// <param name="processed">How many more items were processed.</param>
	/// <param name="skipped">How many more items were skipped.</param>
	/// <remarks>Safe from any thread; the window is refreshed on the UI thread.</remarks>
	public void Advance( int processed, int skipped )
		=> Update( () =>
		{
			ProcessCounter += processed;
			SkipCounter += skipped;
			TotalProcessed = ProcessCounter + SkipCounter;
		} );

	/// <summary>Sets the processed counter to an absolute value; the skipped counter is kept.</summary>
	/// <param name="processed">The number of items processed so far.</param>
	/// <remarks>Safe from any thread; the window is refreshed on the UI thread.</remarks>
	public void SetProcessed( int processed )
		=> Update( () =>
		{
			ProcessCounter = processed;
			TotalProcessed = ProcessCounter + SkipCounter;
		} );

	/// <summary>Fills the progress bar and marks the window idle (<see cref="IsInProcess"/> false).</summary>
	/// <remarks>Safe from any thread; the window is refreshed on the UI thread.</remarks>
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
			if( wasInProcess )
			{
				OnPropertyChanged( nameof( IsInProcess ) );
				CancelCommand.NotifyCanExecuteChanged();
				OperationCompleted?.Invoke();
			}
		} );
	}

	private void Update( Action change )
	{
		lock( _gate )
			change();
		// one UI refresh for any number of changes made before it runs
		if( Interlocked.Exchange( ref _refreshQueued, 1 ) == 1 )
			return;
		UiDispatcher.Post( () =>
		{
			Volatile.Write( ref _refreshQueued, 0 );
			NotifyProgressChanged();
		} );
	}

	private void NotifyProgressChanged()
	{
		OnPropertyChanged( nameof( MaxCount ) );
		OnPropertyChanged( nameof( TotalProcessed ) );
		OnPropertyChanged( nameof( ProgressText ) );
		OnPropertyChanged( nameof( CompactProgressText ) );
		OnPropertyChanged( nameof( SkippedText ) );
		OnPropertyChanged( nameof( DownloadedText ) );
		OnPropertyChanged( nameof( SessionDownloadedText ) );
		OnPropertyChanged( nameof( UploadedText ) );
		OnPropertyChanged( nameof( SessionUploadedText ) );
	}

	private void NotifyCategoryCountsChanged()
	{
		OnPropertyChanged( nameof( ProcessingCount ) );
		OnPropertyChanged( nameof( SuccessCount ) );
		OnPropertyChanged( nameof( InfoCount ) );
		OnPropertyChanged( nameof( WarningCount ) );
		OnPropertyChanged( nameof( ErrorCount ) );
		OnPropertyChanged( nameof( ProcessingSummaryText ) );
		OnPropertyChanged( nameof( InfoSummaryText ) );
		OnPropertyChanged( nameof( WarningSummaryText ) );
		OnPropertyChanged( nameof( ErrorSummaryText ) );
		OnPropertyChanged( nameof( LatestProcessingMessage ) );
		OnPropertyChanged( nameof( LatestInfoMessage ) );
	}
}
