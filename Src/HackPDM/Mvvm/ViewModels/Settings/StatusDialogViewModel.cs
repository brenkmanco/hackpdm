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

	/// <summary>Creates the window state with a new <see cref="StatusLogSession"/> and subscribes to its messages.</summary>
	/// <param name="dialogs">Asks the user before closing during an operation.</param>
	/// <param name="cancelOperation">Cancels the running operation (the Cancel button, or closing while busy).</param>
	/// <remarks>Registers with <see cref="WeakReferenceMessenger.Default"/> using the session id as the token.</remarks>
	public StatusDialogViewModel( IDialogService dialogs, Action cancelOperation )
	{
		_dialogs = dialogs;
		_cancelOperation = cancelOperation;
		Session = new StatusLogSession();
		WeakReferenceMessenger.Default.Register( this, Session.SessionId );
	}

	/// <summary>Messages logged to this session land in the window's lists.</summary>
	public StatusLogSession Session { get; }

	public ObservableRingBuffer<BasicStatusMessage> OStatus { get; } = new( HistoryLength );
	public ObservableRingBuffer<BasicStatusMessage> OInfo { get; } = new( HistoryLength );
	public ObservableRingBuffer<BasicStatusMessage> OError { get; } = new( HistoryLength );

	/// <summary>True from the start of an operation until it completes or is cancelled.</summary>
	public bool IsInProcess { get; set; }

	public int MaxCount { get; private set; }
	public int TotalProcessed { get; private set; }
	public int SkipCounter { get; private set; }
	private int ProcessCounter { get; set; }

	public string ProgressText
	{
		get
		{
			float percent = MaxCount > 0 ? TotalProcessed / (float)MaxCount * 100 : 0;
			return $"({percent:f2}%)\n{TotalProcessed} / {MaxCount}";
		}
	}

	public string SkippedText => $"({SkipCounter}) Skipped";

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
		=> UiDispatcher.Post( () => GetCollection( message.Status ).Add( message ) );

	/// <summary>Cancels the running operation.</summary>
	[RelayCommand]
	private void Cancel() => _cancelOperation();

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
			IsInProcess = false;
		}
		return close;
	}

	// ---------------------------------------------------------------- progress, from any thread

	/// <summary>Starts the progress over for a new number of items.</summary>
	/// <param name="total">The number of items the operation will process.</param>
	/// <remarks>Safe from any thread; the window is refreshed on the UI thread.</remarks>
	public void ResetProgress( int total )
		=> Update( () => ( MaxCount, ProcessCounter, SkipCounter, TotalProcessed ) = ( total, 0, 0, 0 ) );

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
		=> Update( () =>
		{
			ProcessCounter = Math.Max( MaxCount - SkipCounter, 0 );
			TotalProcessed = MaxCount;
			IsInProcess = false;
		} );

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
			OnPropertyChanged( nameof( MaxCount ) );
			OnPropertyChanged( nameof( TotalProcessed ) );
			OnPropertyChanged( nameof( ProgressText ) );
			OnPropertyChanged( nameof( SkippedText ) );
		} );
	}
}
