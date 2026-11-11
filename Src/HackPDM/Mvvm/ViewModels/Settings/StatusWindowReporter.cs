using System;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Mvvm.Models.Common;

namespace HackPDM.Mvvm.ViewModels.Settings;

/// <summary><see cref="IOperationReporter"/> backed by the app's StatusDialog window and log session.</summary>
public sealed class StatusWindowReporter( IDialogService dialogs, Func<StatusDialogViewModel?> status, Func<StatusLogSession?> session ) : IOperationReporter
{
	private readonly object _gate = new();
	private CancellationTokenSource _cancellation = new();

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
