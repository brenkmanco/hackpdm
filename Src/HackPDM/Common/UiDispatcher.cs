using System;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core.Configuration;

using Microsoft.UI.Dispatching;

namespace HackPDM.Common;

public interface IUiDispatcher
{
	bool HasThreadAccess { get; }
	bool Post( Action action );
	Task RunAsync( Action action, CancellationToken token = default );
}

public static class UiDispatcher
{
	public static IUiDispatcher Current { get; set; } = InlineUiDispatcher.Instance;
	public static bool Post( Action action ) => Current.Post( action );

	public static void RunOrPost( Action action )
	{
		if( Current.HasThreadAccess )
		{
			try { action(); }
			catch( Exception e ) { HackLogger.Error( nameof( UiDispatcher ), $"UI work item failed: {e.Message}", e ); }
		}
		else
		{
			Current.Post( action );
		}
	}
}

/// <summary>Forwards progress reports to the UI dispatcher thread, coalescing bursts to ~75ms.</summary>
public sealed class UiThreadProgress<T> : IProgress<T>, IDisposable
{
	private const int CoalesceIntervalMs = 75;

	private readonly Action<T> _handler;
	private readonly Action _flushAction;
	private readonly Lock _gate = new();
	private Timer? _timer;
	private T? _latestValue;
	private bool _hasValue;
	private bool _flushScheduled;
	private bool _disposed;
	private long _lastDispatchTick;

	public UiThreadProgress( Action<T> handler )
	{
		_handler = handler;
		_flushAction = FlushOnUiThread;
	}

	public void Report( T value )
	{
		if( UiDispatcher.Current is InlineUiDispatcher )
		{
			if( !Volatile.Read( ref _disposed ) )
				UiDispatcher.Post( () => _handler( value ) );
			return;
		}

		bool postNow = false;
		int delayMs = 0;
		lock( _gate )
		{
			if( _disposed )
				return;
			_latestValue = value;
			_hasValue = true;
			if( _flushScheduled )
				return;

			_flushScheduled = true;
			long elapsed = Environment.TickCount64 - _lastDispatchTick;
			if( elapsed >= CoalesceIntervalMs )
				postNow = true;
			else
				delayMs = (int)( CoalesceIntervalMs - elapsed );
		}

		if( postNow )
			UiDispatcher.Post( _flushAction );
		else
			ScheduleTimer( delayMs );
	}

	private void ScheduleTimer( int delayMs )
	{
		Timer? timer;
		lock( _gate )
		{
			if( _disposed )
				return;
			timer = _timer ??= new Timer(
				static state => ( (UiThreadProgress<T>)state! ).OnTimer(),
				this,
				Timeout.Infinite,
				Timeout.Infinite );
		}
		try { timer.Change( delayMs, Timeout.Infinite ); }
		catch( ObjectDisposedException ) { }
	}

	private void OnTimer()
	{
		if( Volatile.Read( ref _disposed ) )
			return;
		UiDispatcher.Post( _flushAction );
	}

	private void FlushOnUiThread()
	{
		T value;
		bool hasValue;
		lock( _gate )
		{
			if( _disposed )
				return;
			hasValue = _hasValue;
			value = _latestValue!;
			_latestValue = default;
			_hasValue = false;
			_lastDispatchTick = Environment.TickCount64;
			_flushScheduled = false;
		}

		if( hasValue )
			_handler( value );

		bool reschedule = false;
		lock( _gate )
		{
			if( !_disposed && _hasValue && !_flushScheduled )
			{
				_flushScheduled = true;
				reschedule = true;
			}
		}
		if( reschedule )
			ScheduleTimer( CoalesceIntervalMs );
	}

	public void Dispose()
	{
		Timer? timer;
		lock( _gate )
		{
			if( _disposed )
				return;
			_disposed = true;
			_hasValue = false;
			_latestValue = default;
			_flushScheduled = false;
			timer = _timer;
			_timer = null;
		}
		timer?.Dispose();
	}
}

public sealed class InlineUiDispatcher : IUiDispatcher
{
	public static readonly InlineUiDispatcher Instance = new();
	public bool HasThreadAccess => true;

	public bool Post( Action action )
	{
		try { action(); }
		catch( Exception e ) { HackLogger.Error( nameof( UiDispatcher ), $"UI work item failed: {e.Message}", e ); }
		return true;
	}

	public Task RunAsync( Action action, CancellationToken token = default )
	{
		if( token.IsCancellationRequested )
			return Task.FromCanceled( token );
		try
		{
			action();
			return Task.CompletedTask;
		}
		catch( Exception e )
		{
			return Task.FromException( e );
		}
	}
}

public sealed class DispatcherQueueUiDispatcher( DispatcherQueue queue ) : IUiDispatcher
{
	public bool HasThreadAccess => queue.HasThreadAccess;

	public bool Post( Action action )
	{
		if( queue.TryEnqueue( () => Run( action ) ) )
			return true;
		HackLogger.Warn( nameof( UiDispatcher ), "UI dispatcher rejected the work item; it was not run" );
		return false;
	}

	public Task RunAsync( Action action, CancellationToken token = default )
	{
		if( token.IsCancellationRequested )
			return Task.FromCanceled( token );
		if( queue.HasThreadAccess )
			return InlineUiDispatcher.Instance.RunAsync( action, token );

		TaskCompletionSource tcs = new( TaskCreationOptions.RunContinuationsAsynchronously );
		QueuedRunState state = new( tcs, action, token );
		if( token.CanBeCanceled )
		{
			state.Registration = token.Register( static s =>
			{
				var st = (QueuedRunState)s!;
				st.Tcs.TrySetCanceled( st.Token );
			}, state );
		}

		if( !queue.TryEnqueue( () =>
		{
			state.Registration.Dispose();
			if( state.Token.IsCancellationRequested )
			{
				state.Tcs.TrySetCanceled( state.Token );
				return;
			}
			try
			{
				state.Action();
				state.Tcs.TrySetResult();
			}
			catch( Exception e )
			{
				state.Tcs.TrySetException( e );
			}
		} ) )
		{
			state.Registration.Dispose();
			tcs.TrySetException( new InvalidOperationException( "UI dispatcher rejected the work item" ) );
		}
		return tcs.Task;
	}

	private static void Run( Action action )
	{
		try { action(); }
		catch( Exception e ) { HackLogger.Error( nameof( UiDispatcher ), $"UI work item failed: {e.Message}", e ); }
	}

	private sealed class QueuedRunState( TaskCompletionSource tcs, Action action, CancellationToken token )
	{
		public readonly TaskCompletionSource Tcs = tcs;
		public readonly Action Action = action;
		public readonly CancellationToken Token = token;
		public CancellationTokenRegistration Registration;
	}
}
