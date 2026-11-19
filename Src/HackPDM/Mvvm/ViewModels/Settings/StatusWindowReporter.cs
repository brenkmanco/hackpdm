using System;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.Models.Common;

namespace HackPDM.Mvvm.ViewModels.Settings;

/// <summary><see cref="IOperationReporter"/> backed by the app's StatusDialog window and log session.</summary>
/// <param name="dialogs">Opens the status window and shows the final messages.</param>
/// <param name="status">Returns the status window's state; calls are dropped while it returns null.</param>
public sealed class StatusWindowReporter( IDialogService dialogs, Func<StatusDialogViewModel?> status ) : IOperationReporter
{
	private readonly object _gate = new();
	private CancellationTokenSource _cancellation = new();
	private bool _cancelRegistered;

	/// <inheritdoc/>
	public void Log( StatusMessage statusMessage, string message ) => status()?.Session.Log( statusMessage, message );

	/// <inheritdoc/>
	public void ShowWindow() => dialogs.ShowStatusWindow();

	/// <inheritdoc/>
	public CancellationTokenSource BeginOperation()
	{
		lock( _gate )
		{
			if( _cancellation.IsCancellationRequested )
			{
				_cancellation.Dispose();
				_cancellation = new();
				_cancelRegistered = false;
			}
			if( !_cancelRegistered )
			{
				_cancellation.Token.Register( OnCancelled );
				_cancelRegistered = true;
			}
			status()?.SetInProcess( true );
			return _cancellation;
		}
	}

	/// <inheritdoc/>
	public void Cancel()
	{
		lock( _gate )
		{
			if( !_cancellation.IsCancellationRequested )
				_cancellation.Cancel();
		}
	}

	private void OnCancelled()
	{
		Log( StatusMessage.WARNING, "Operation cancelled" );
		UiDispatcher.Post( () =>
		{
			status()?.SetInProcess( false );
			_ = dialogs.ShowMessageAsync( "Cancelled Operation" );
		} );
	}

	/// <inheritdoc/>
	public void SetTotal( int total ) => status()?.ResetProgress( total );

	/// <inheritdoc/>
	public void Advance( int processed, int skipped = 0 ) => status()?.Advance( processed, skipped );

	/// <inheritdoc/>
	public void SetProcessed( int processed ) => status()?.SetProcessed( processed );

	/// <inheritdoc/>
	public void AddDownloadedBytes( long bytes ) => status()?.AddDownloadedBytes( bytes );

	/// <inheritdoc/>
	public void AddUploadedBytes( long bytes ) => status()?.AddUploadedBytes( bytes );

	/// <inheritdoc/>
	public void Complete() => status()?.CompleteProgress();

	/// <inheritdoc/>
	public Task NotifyAsync( string message, string caption = "Status" )
		=> dialogs.ShowMessageAsync( message, caption, MessageBoxButtons.OK );
}
