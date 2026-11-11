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
public sealed class StatusWindowReporter( IDialogService dialogs, Func<StatusDialogViewModel?> status ) : IOperationReporter
{
	private readonly object _gate = new();
	private CancellationTokenSource _cancellation = new();

	public void Log( StatusMessage statusMessage, string message ) => status()?.Session.Log( statusMessage, message );

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

	public void SetTotal( int total ) => status()?.ResetProgress( total );

	public void Advance( int processed, int skipped = 0 ) => status()?.Advance( processed, skipped );

	public void SetProcessed( int processed ) => status()?.SetProcessed( processed );

	public void Complete() => status()?.CompleteProgress();

	public Task NotifyAsync( string message, string caption = "Status" )
		=> dialogs.ShowMessageAsync( message, caption, MessageBoxButtons.OK );
}
