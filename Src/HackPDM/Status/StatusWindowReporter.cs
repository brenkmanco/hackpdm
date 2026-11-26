using System;
using System.Collections.Generic;
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
	private readonly List<OperationState> _activeOps = [];
	private readonly AsyncLocal<OperationState?> _currentOp = new();
	private readonly AsyncLocal<OperationScope?> _currentScope = new();
	private long _nextToken;

	/// <summary>Tracks per-operation cancellation and error state across nested or multi-stage PDM workflows.</summary>
	public sealed class OperationScope : IDisposable
	{
		private readonly AsyncLocal<OperationScope?> _slot;
		private readonly OperationScope? _previous;
		private int _hasErrors;
		private int _wasCancelled;
		private int _operationCount;
		private int _disposed;

		internal OperationScope( AsyncLocal<OperationScope?> slot )
		{
			_slot = slot;
			_previous = slot.Value;
			slot.Value = this;
		}

		public bool HasErrors => Volatile.Read( ref _hasErrors ) != 0;
		public bool WasCancelled => Volatile.Read( ref _wasCancelled ) != 0;
		public int OperationCount => Volatile.Read( ref _operationCount );
		public bool CompletedWithoutErrors => !WasCancelled && !HasErrors;

		internal void RecordOperationStarted() => Interlocked.Increment( ref _operationCount );
		internal void RecordError() => Interlocked.Exchange( ref _hasErrors, 1 );
		internal void RecordCancelled() => Interlocked.Exchange( ref _wasCancelled, 1 );

		public void Dispose()
		{
			if( Interlocked.Exchange( ref _disposed, 1 ) == 0 )
				_slot.Value = _previous;
		}
	}

	private sealed class OperationState( long token, OperationScope? scope, OperationPauseController? pause )
	{
		public long Token { get; } = token;
		public OperationScope? Scope { get; } = scope;
		public OperationPauseController? Pause { get; } = pause;
		public Action<OperationPauseState>? PauseHandler { get; set; }
		public CancellationTokenSource Cts { get; } = new();
		public CancellationTokenRegistration CancelRegistration { get; set; }
		public bool SuppressCancelDialog { get; set; }
		public bool HasCompleted { get; set; }
		public bool WasCancelled { get; set; }
	}

	/// <summary>Starts a scope that captures errors and cancellations across one or more workflow calls on the current async flow.</summary>
	public OperationScope BeginTrackingScope() => new( _currentScope );

	public void Log( StatusMessage statusMessage, string message )
	{
		if( statusMessage == StatusMessage.ERROR )
		{
			OperationState? op = _currentOp.Value;
			OperationScope? scope = op?.Scope ?? _currentScope.Value;
			scope?.RecordError();
		}
		status()?.AddLog( new BasicStatusMessage { Status = statusMessage, Message = message } );
	}

	public void ShowWindow() => dialogs.ShowStatusWindow();

	public OperationPauseController? PauseController => _currentOp.Value?.Pause;

	public CancellationTokenSource BeginOperation() => BeginOperation( canPause: false );

	public CancellationTokenSource BeginOperation( bool canPause )
	{
		OperationScope? scope = _currentScope.Value;
		OperationPauseController? pause = canPause ? new OperationPauseController() : null;
		OperationState op;
		long token;
		lock( _gate )
		{
			token = ++_nextToken;
			op = new OperationState( token, scope, pause );
			if( pause is not null )
			{
				op.PauseHandler = state => status()?.SetPauseState( canPause: true, state, token );
				pause.StateChanged += op.PauseHandler;
			}
			op.CancelRegistration = op.Cts.Token.Register( static state =>
			{
				var (reporter, operation) = ((StatusWindowReporter, OperationState))state!;
				reporter.OnOperationCancelled( operation );
			}, ( this, op ) );
			_activeOps.Add( op );
		}
		scope?.RecordOperationStarted();
		_currentOp.Value = op;
		StatusDialogViewModel? vm = status();
		vm?.SetPauseState( canPause, pause?.State ?? OperationPauseState.None, token );
		vm?.SetInProcess( true, token );
		return op.Cts;
	}

	public void TogglePause()
	{
		List<OperationPauseController>? targets = null;
		lock( _gate )
		{
			for( int i = 0; i < _activeOps.Count; i++ )
			{
				OperationState op = _activeOps[ i ];
				if( !op.HasCompleted && !op.WasCancelled && op.Pause is { } pause )
					( targets ??= [] ).Add( pause );
			}
		}
		if( targets is null )
			return;
		for( int i = 0; i < targets.Count; i++ )
			targets[ i ].TogglePause();
	}

	public void Cancel() => CancelCore( silent: false );

	internal void CancelSilent() => CancelCore( silent: true );

	private void CancelCore( bool silent )
	{
		List<OperationState>? toCancel = null;
		lock( _gate )
		{
			if( _activeOps.Count == 0 )
				return;
			toCancel = new List<OperationState>( _activeOps.Count );
			for( int i = 0; i < _activeOps.Count; i++ )
			{
				OperationState op = _activeOps[ i ];
				if( op.HasCompleted || op.Cts.IsCancellationRequested )
					continue;
				if( silent || toCancel.Count > 0 )
					op.SuppressCancelDialog = true;
				toCancel.Add( op );
			}
		}
		if( toCancel is null || toCancel.Count == 0 )
			return;
		for( int i = 0; i < toCancel.Count; i++ )
		{
			OperationState op = toCancel[ i ];
			DetachPauseHandler( op );
			op.Pause?.ResetForCompletion();
			try { op.Cts.Cancel(); }
			catch( ObjectDisposedException ) { }
		}
	}

	private static void DetachPauseHandler( OperationState op )
	{
		if( op.Pause is { } pause && op.PauseHandler is { } handler )
		{
			pause.StateChanged -= handler;
			op.PauseHandler = null;
		}
	}

	private void OnOperationCancelled( OperationState op )
	{
		bool showDialog;
		long? tokenToClear = null;
		lock( _gate )
		{
			if( op.HasCompleted || op.WasCancelled )
				return;
			op.WasCancelled = true;
			op.Scope?.RecordCancelled();
			showDialog = !op.SuppressCancelDialog;

			bool anyStillRunning = false;
			for( int i = 0; i < _activeOps.Count; i++ )
			{
				OperationState other = _activeOps[ i ];
				if( !other.HasCompleted && !other.WasCancelled )
				{
					anyStillRunning = true;
					break;
				}
			}
			if( !anyStillRunning )
				tokenToClear = _nextToken;
		}

		Log( StatusMessage.WARNING, "Operation cancelled" );
		if( tokenToClear is not null || showDialog )
		{
			UiDispatcher.RunOrPost( () =>
			{
				if( tokenToClear is long token )
					status()?.TryClearInProcess( token );
				if( showDialog )
					_ = dialogs.ShowMessageAsync( "Cancelled Operation" );
			} );
		}
	}

	public void SetTotal( int total ) => status()?.ResetProgress( total );
	public void Advance( int processed, int skipped = 0 ) => status()?.Advance( processed, skipped );
	public void SetProcessed( int processed ) => status()?.SetProcessed( processed );
	public void RestoreProgress( int total, int processed, int skipped, long downloadedBytes ) => status()?.RestoreProgress( total, processed, skipped, downloadedBytes );
	public void AddDownloadedBytes( long bytes ) => status()?.AddDownloadedBytes( bytes );
	public void AddUploadedBytes( long bytes ) => status()?.AddUploadedBytes( bytes );
	public void BeginDownloadTransfer() => status()?.BeginDownloadTransfer();
	public void EndDownloadTransfer() => status()?.EndDownloadTransfer();
	public void BeginUploadTransfer() => status()?.BeginUploadTransfer();
	public void EndUploadTransfer() => status()?.EndUploadTransfer();

	public void Complete()
	{
		OperationState? op = _currentOp.Value;
		long? tokenToComplete = null;
		CancellationTokenRegistration registration = default;
		OperationState? opToDispose = null;
		lock( _gate )
		{
			if( op is null )
			{
				if( _activeOps.Count > 0 )
					op = _activeOps[ ^1 ];
				else
					return;
			}
			if( op.HasCompleted )
				return;

			op.HasCompleted = true;
			if( op.Cts.IsCancellationRequested && !op.WasCancelled )
			{
				op.WasCancelled = true;
				op.Scope?.RecordCancelled();
			}
			registration = op.CancelRegistration;
			op.CancelRegistration = default;
			_activeOps.Remove( op );
			opToDispose = op;

			if( _activeOps.Count == 0 )
				tokenToComplete = _nextToken;
		}
		if( opToDispose is not null )
		{
			DetachPauseHandler( opToDispose );
			opToDispose.Pause?.ResetForCompletion();
		}
		registration.Dispose();
		opToDispose?.Cts.Dispose();

		if( tokenToComplete is long token )
			status()?.CompleteProgress( token );
	}

	public Task NotifyAsync( string message, string caption = "Status" )
	{
		OperationState? op = _currentOp.Value;
		OperationScope? scope = op?.Scope ?? _currentScope.Value;
		if( message.StartsWith( "Cancelled", StringComparison.OrdinalIgnoreCase ) )
		{
			if( op is not null )
				op.WasCancelled = true;
			scope?.RecordCancelled();
		}
		else if( message.StartsWith( "Download failed", StringComparison.OrdinalIgnoreCase )
			|| message.StartsWith( "Commit failed", StringComparison.OrdinalIgnoreCase ) )
		{
			scope?.RecordError();
		}
		return dialogs.ShowMessageAsync( message, caption, MessageBoxButtons.OK );
	}
}
