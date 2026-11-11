using System;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms;
using HackPDM.UI.Forms.FormTransport;
using HackPDM.ViewModels;

namespace HackPDM.Services;

/// <summary>
/// Where long-running PDM operations send their log lines, progress and final message.
/// Lets workflow code report progress without knowing about the status window or view models.
/// </summary>
public interface IOperationReporter
{
	void Log( StatusMessage status, string message );

	/// <summary>Opens the status window.</summary>
	void ShowWindow();

	/// <summary>Starts an operation: renews the cancellation source and marks the window busy.</summary>
	CancellationTokenSource BeginOperation();
	/// <summary>Cancels the running operation, if any.</summary>
	void Cancel();

	/// <summary>Resets the progress counters for <paramref name="total"/> items.</summary>
	void SetTotal( int total );
	/// <summary>Adds to the processed/skipped counters. Safe to call from several threads.</summary>
	void Advance( int processed, int skipped = 0 );
	/// <summary>Sets the processed counter to an absolute value.</summary>
	void SetProcessed( int processed );
	/// <summary>Fills the progress bar and marks the window idle.</summary>
	void Complete();

	/// <summary>Shows a message to the user at the end of an operation.</summary>
	Task NotifyAsync( string message, string caption = "Status" );
}

/// <summary><see cref="IOperationReporter"/> backed by the app's StatusDialog window and log session.</summary>
public sealed class StatusWindowReporter( IDialogService dialogs, Func<StatusDialogViewModel?> status, Func<StatusLogSession?> session ) : IOperationReporter
{
	private readonly object _gate = new();
	private CancellationTokenSource _cancellation = new();

	public StatusWindowReporter()
		: this( new DialogService(), () => InstanceManager.Dialog, () => InstanceManager.Session ) { }

	/// <summary>The app-wide reporter, shared so the status window can cancel whatever is running.</summary>
	public static StatusWindowReporter Default => field ??= new();

	public void Log( StatusMessage statusMessage, string message ) => session()?.Log( statusMessage, message );

	public void ShowWindow() => dialogs.ShowStatusWindow();

	public CancellationTokenSource BeginOperation()
	{
		lock( _gate )
		{
			if( _cancellation.IsCancellationRequested )
			{
				_cancellation.Dispose();
				_cancellation = new();
			}
			if( status() is { } window )
				window.IsInProcess = true;
			_cancellation.Token.Register( OnCancelled );
			return _cancellation;
		}
	}

	public void Cancel()
	{
		lock( _gate )
			_cancellation.Cancel();
	}

	private void OnCancelled()
	{
		if( status() is { } window )
			window.IsInProcess = false;
		_ = dialogs.ShowMessageAsync( "Cancelled Operation" );
	}

	public void SetTotal( int total )
	{
		if( status() is not { } window )
			return;
		lock( _gate )
		{
			window.MaxCount = total;
			window.ProcessCounter = 0;
			window.SkipCounter = 0;
			window.TotalProcessed = 0;
			window.Downloaded = 0;
		}
	}

	public void Advance( int processed, int skipped = 0 )
	{
		if( status() is not { } window )
			return;
		lock( _gate )
		{
			window.ProcessCounter += processed;
			window.SkipCounter += skipped;
			window.TotalProcessed = window.ProcessCounter + window.SkipCounter;
		}
	}

	public void SetProcessed( int processed )
	{
		if( status() is not { } window )
			return;
		lock( _gate )
		{
			window.ProcessCounter = processed;
			window.TotalProcessed = window.ProcessCounter + window.SkipCounter;
		}
	}

	public void Complete()
	{
		if( status() is not { } window )
			return;
		lock( _gate )
		{
			window.ProcessCounter = Math.Max( window.MaxCount - window.SkipCounter, 0 );
			window.TotalProcessed = window.MaxCount;
			window.IsInProcess = false;
		}
	}

	public Task NotifyAsync( string message, string caption = "Status" )
		=> dialogs.ShowMessageAsync( message, caption, MessageBoxButtons.OK );
}
