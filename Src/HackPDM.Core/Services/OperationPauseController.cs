namespace HackPDM.Core.Services;

/// <summary>Execution state of a cooperatively pausable workflow operation.</summary>
public enum OperationPauseState
{
	None = 0,
	Running = 1,
	Pausing = 2,
	Paused = 3,
}

/// <summary>
/// Lock-free hot-path pause gate that lets concurrent workers finish their current atomic step
/// before entering <see cref="OperationPauseState.Paused"/> and waiting for <see cref="Resume"/>.
/// </summary>
public sealed class OperationPauseController
{
	private readonly Lock _gate = new();
	private int _pauseFlag;
	private int _activeWorkUnits;
	private OperationPauseState _state = OperationPauseState.Running;
	private TaskCompletionSource? _resumeTcs;

	public event Action<OperationPauseState>? StateChanged;

	public OperationPauseState State
	{
		get
		{
			lock( _gate )
				return _state;
		}
	}

	public bool IsPauseRequested => Volatile.Read( ref _pauseFlag ) != 0;

	public static WorkScope EnterScope( OperationPauseController? controller )
	{
		if( controller is null )
			return default;
		controller.IncrementWorkUnits();
		return new WorkScope( controller );
	}

	public WorkScope EnterWorkScope() => EnterScope( this );

	public ValueTask WaitIfPausedAsync( CancellationToken token = default )
	{
		if( Volatile.Read( ref _pauseFlag ) == 0 )
		{
			token.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
		return WaitSlowAsync( token );
	}

	public void RequestPause()
	{
		Action<OperationPauseState>? handler;
		OperationPauseState next;
		lock( _gate )
		{
			if( _pauseFlag != 0 || _state == OperationPauseState.None )
				return;
			Volatile.Write( ref _pauseFlag, 1 );
			_resumeTcs ??= new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
			next = _activeWorkUnits > 0 ? OperationPauseState.Pausing : OperationPauseState.Paused;
			_state = next;
			handler = StateChanged;
		}
		handler?.Invoke( next );
	}

	public void Resume()
	{
		Action<OperationPauseState>? handler;
		TaskCompletionSource? tcs;
		lock( _gate )
		{
			if( _pauseFlag == 0 || _state == OperationPauseState.None )
				return;
			Volatile.Write( ref _pauseFlag, 0 );
			_state = OperationPauseState.Running;
			tcs = _resumeTcs;
			_resumeTcs = null;
			handler = StateChanged;
		}
		tcs?.TrySetResult();
		handler?.Invoke( OperationPauseState.Running );
	}

	public void TogglePause()
	{
		bool pause;
		lock( _gate )
		{
			if( _state == OperationPauseState.None )
				return;
			pause = _pauseFlag == 0;
		}
		if( pause )
			RequestPause();
		else
			Resume();
	}

	public void ResetForCompletion()
	{
		Action<OperationPauseState>? handler = null;
		TaskCompletionSource? tcs;
		lock( _gate )
		{
			Volatile.Write( ref _pauseFlag, 0 );
			if( _state != OperationPauseState.None )
			{
				_state = OperationPauseState.None;
				handler = StateChanged;
			}
			tcs = _resumeTcs;
			_resumeTcs = null;
		}
		tcs?.TrySetResult();
		handler?.Invoke( OperationPauseState.None );
	}

	private async ValueTask WaitSlowAsync( CancellationToken token )
	{
		while( true )
		{
			token.ThrowIfCancellationRequested();
			Task waitTask;
			lock( _gate )
			{
				if( Volatile.Read( ref _pauseFlag ) == 0 || _state == OperationPauseState.None )
					return;
				_resumeTcs ??= new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
				waitTask = _resumeTcs.Task;
			}
			await waitTask.WaitAsync( token ).ConfigureAwait( false );
		}
	}

	private void IncrementWorkUnits()
	{
		lock( _gate )
			_activeWorkUnits++;
	}

	private void DecrementWorkUnits()
	{
		Action<OperationPauseState>? handler = null;
		OperationPauseState next = default;
		lock( _gate )
		{
			if( _activeWorkUnits > 0 )
				_activeWorkUnits--;
			if( _activeWorkUnits == 0 && _pauseFlag != 0 && _state == OperationPauseState.Pausing )
			{
				next = OperationPauseState.Paused;
				_state = next;
				handler = StateChanged;
			}
		}
		handler?.Invoke( next );
	}

	public struct WorkScope : IDisposable
	{
		private OperationPauseController? _owner;

		internal WorkScope( OperationPauseController owner ) => _owner = owner;

		public void Dispose()
		{
			OperationPauseController? owner = Interlocked.Exchange( ref _owner, null );
			owner?.DecrementWorkUnits();
		}
	}
}
