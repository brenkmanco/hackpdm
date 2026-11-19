using System;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

namespace HackPDM.Status;

/// <summary>Reports PDM operation logs, progress, and cancellation directly to <see cref="StatusDialogViewModel"/>.</summary>
public sealed class StatusWindowReporter( IDialogService dialogs, Func<StatusDialogViewModel?> status ) : IOperationReporter
{
	private readonly object _gate = new();
	private CancellationTokenSource _cancellation = new();
	private bool _cancelRegistered;
	private int _suppressCancelDialog;

	public void Log( StatusMessage statusMessage, string message )
		=> status()?.AddLog( new BasicStatusMessage { Status = statusMessage, Message = message } );

	public void ShowWindow() => dialogs.ShowStatusWindow();

	public CancellationTokenSource BeginOperation()
	{
		lock( _gate )
		{
			Volatile.Write( ref _suppressCancelDialog, 0 );
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

	public void Cancel() => CancelCore( silent: false );

	internal void CancelSilent() => CancelCore( silent: true );

	private void CancelCore( bool silent )
	{
		CancellationTokenSource? toCancel = null;
		lock( _gate )
		{
			if( silent )
				Volatile.Write( ref _suppressCancelDialog, 1 );
			if( !_cancellation.IsCancellationRequested )
				toCancel = _cancellation;
		}
		if( toCancel is not null )
		{
			try { toCancel.Cancel(); }
			catch( ObjectDisposedException ) { }
		}
	}

	private void OnCancelled()
	{
		Log( StatusMessage.WARNING, "Operation cancelled" );
		bool showDialog = Volatile.Read( ref _suppressCancelDialog ) == 0;
		UiDispatcher.Post( () =>
		{
			status()?.SetInProcess( false );
			if( showDialog )
				_ = dialogs.ShowMessageAsync( "Cancelled Operation" );
		} );
	}

	public void SetTotal( int total ) => status()?.ResetProgress( total );
	public void Advance( int processed, int skipped = 0 ) => status()?.Advance( processed, skipped );
	public void SetProcessed( int processed ) => status()?.SetProcessed( processed );
	public void AddDownloadedBytes( long bytes ) => status()?.AddDownloadedBytes( bytes );
	public void AddUploadedBytes( long bytes ) => status()?.AddUploadedBytes( bytes );
	public void Complete() => status()?.CompleteProgress();

	public Task NotifyAsync( string message, string caption = "Status" )
		=> dialogs.ShowMessageAsync( message, caption, MessageBoxButtons.OK );
}
