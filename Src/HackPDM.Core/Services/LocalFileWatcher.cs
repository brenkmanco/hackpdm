using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

public enum LocalChangeKind
{
	Created,
	Updated,
	Deleted,
}

public sealed record LocalFileChange( string FullPath, LocalChangeKind Kind, bool IsDirectory = false );

/// <summary>Debounces and merges rapid OS file events per path before dispatching.</summary>
public sealed class LocalChangeBatcher( TimeSpan quiet )
{
	private readonly Dictionary<string, (LocalChangeKind? Kind, DateTime Due, bool IsDirectory)> _pending = new( PwaPaths.PathComparer );
	private readonly List<string> _dueKeys = [];

	public int PendingCount => _pending.Count;

	public void Clear()
	{
		_pending.Clear();
		_dueKeys.Clear();
	}

	public void Add( string path, LocalChangeKind kind, DateTime now, bool isDirectory = false )
	{
		bool knownDir = isDirectory;
		LocalChangeKind? merged;
		if( _pending.TryGetValue( path, out var earlier ) )
		{
			merged = Merge( earlier.Kind, kind );
			knownDir = earlier.IsDirectory || isDirectory;
		}
		else
		{
			merged = kind;
		}
		_pending[ path ] = ( merged, now + quiet, knownDir );
	}

	public static LocalChangeKind? Merge( LocalChangeKind? earlier, LocalChangeKind later ) => ( earlier, later ) switch
	{
		( null, _ ) => later,
		( LocalChangeKind.Created, LocalChangeKind.Deleted ) => null,
		( LocalChangeKind.Created, _ ) => LocalChangeKind.Created,
		( LocalChangeKind.Deleted, LocalChangeKind.Created ) => LocalChangeKind.Updated,
		( _, LocalChangeKind.Deleted ) => LocalChangeKind.Deleted,
		_ => LocalChangeKind.Updated,
	};

	public List<LocalFileChange> TakeDue( DateTime now )
	{
		if( _pending.Count == 0 )
			return [];
		_dueKeys.Clear();
		List<LocalFileChange>? due = null;
		foreach( var (path, (kind, dueAt, isDir)) in _pending )
		{
			if( dueAt > now )
				continue;
			_dueKeys.Add( path );
			if( kind is { } k )
				( due ??= [] ).Add( new( path, k, isDir ) );
		}
		for( int i = 0; i < _dueKeys.Count; i++ )
			_pending.Remove( _dueKeys[ i ] );
		_dueKeys.Clear();
		return due ?? [];
	}
}

public interface ILocalFileWatcher
{
	event Action<IReadOnlyList<LocalFileChange>>? Changed;
	event Action? Overflowed;
	void Watch( string? folder );
	IDisposable Suspend() => NoOpDisposable.Instance;
}

internal sealed class NoOpDisposable : IDisposable
{
	public static readonly NoOpDisposable Instance = new();
	public void Dispose() { }
}

/// <summary>Watches the local workspace folder and emits debounced change batches.</summary>
public sealed class LocalFileWatcher : ILocalFileWatcher, IDisposable
{
	private const int MaxScannedInNewFolder = 1000;
	internal const int MaxPendingChanges = 200;
	private const int OverflowCooldownMs = 1500;
	private const int BurstSettleMs = 800;
	private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds( 400 );
	private static readonly EnumerationOptions NewFolderScanOptions = new()
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		ReturnSpecialDirectories = false,
	};

	private const int MaxRecoveryAttempts = 5;
	private readonly object _gate = new();
	private readonly object _flushDispatchLock = new();
	private readonly LocalChangeBatcher _batcher = new( Quiet );
	private readonly Timer _flush;
	private readonly Timer _burstSettle;
	private FileSystemWatcher? _watcher;
	private string? _folder;
	private long _watchGeneration;
	private bool _flushArmed;
	private bool _burstActive;
	private bool _trailingOverflowNeeded;
	private bool _disposed;
	private long _lastOverflowEmitTick = long.MinValue / 2;
	private int _consecutiveOverflows;
	private int _recoveryScheduled;
	private int _suspendCount;

	public event Action<IReadOnlyList<LocalFileChange>>? Changed;
	public event Action? Overflowed;

	public LocalFileWatcher()
	{
		_flush = new Timer( _ => Flush(), null, Timeout.Infinite, Timeout.Infinite );
		_burstSettle = new Timer( _ => EndBurst(), null, Timeout.Infinite, Timeout.Infinite );
	}

	public IDisposable Suspend()
	{
		lock( _gate )
		{
			if( Interlocked.Increment( ref _suspendCount ) == 1 )
			{
				if( _watcher is not null )
				{
					try { _watcher.EnableRaisingEvents = false; }
					catch( ObjectDisposedException ) { }
				}
				_batcher.Clear();
				_burstActive = false;
				_trailingOverflowNeeded = false;
				if( !_disposed )
				{
					if( _flushArmed )
					{
						_flushArmed = false;
						_flush.Change( Timeout.Infinite, Timeout.Infinite );
					}
					_burstSettle.Change( Timeout.Infinite, Timeout.Infinite );
				}
			}
		}
		return new SuspendScope( this );
	}

	private void ResumeFromSuspend()
	{
		lock( _gate )
		{
			if( Interlocked.Decrement( ref _suspendCount ) != 0 || _disposed )
				return;
			if( _watcher is not null )
			{
				try
				{
					_watcher.EnableRaisingEvents = true;
					return;
				}
				catch( Exception e )
				{
					HackLogger.Warn( "FileWatcher", $"Could not resume watcher for '{_folder}': {e.Message}", e );
					try { _watcher.Dispose(); } catch { }
					_watcher = null;
				}
			}
			if( !string.IsNullOrEmpty( _folder ) )
				StartWatchingLocked( _folder, createIfMissing: false );
		}
	}

	private sealed class SuspendScope( LocalFileWatcher owner ) : IDisposable
	{
		private int _disposed;
		public void Dispose()
		{
			if( Interlocked.Exchange( ref _disposed, 1 ) == 0 )
				owner.ResumeFromSuspend();
		}
	}

	public void Watch( string? folder )
	{
		string? normalized = null;
		if( !string.IsNullOrWhiteSpace( folder ) )
		{
			try
			{
				normalized = Path.GetFullPath( folder.Trim() );
			}
			catch( Exception e )
			{
				HackLogger.Warn( "FileWatcher", $"Invalid watch folder '{folder}': {e.Message}", e );
				return;
			}
		}

		lock( _gate )
		{
			if( _disposed )
				return;
			if( string.Equals( normalized, _folder, PwaPaths.PathComparison ) && _watcher is not null )
				return;
			StopWatching();
			if( string.IsNullOrEmpty( normalized ) )
				return;
			StartWatchingLocked( normalized, createIfMissing: true );
		}
	}

	private void StartWatchingLocked( string folder, bool createIfMissing )
	{
		FileSystemWatcher? watcher = null;
		try
		{
			if( createIfMissing )
				Directory.CreateDirectory( folder );
			else if( !Directory.Exists( folder ) )
				return;

			long generation = Interlocked.Increment( ref _watchGeneration );
			watcher = new( folder )
			{
				IncludeSubdirectories = true,
				NotifyFilter = NotifyFilters.FileName
					| NotifyFilters.DirectoryName
					| NotifyFilters.LastWrite
					| NotifyFilters.Size
					| NotifyFilters.Attributes
					| NotifyFilters.CreationTime,
				InternalBufferSize = 64 * 1024,
			};
			watcher.Created += ( _, e ) =>
			{
				if( Volatile.Read( ref _suspendCount ) > 0 || Volatile.Read( ref _watchGeneration ) != generation )
					return;
				bool isDir = Directory.Exists( e.FullPath );
				Add( e.FullPath, LocalChangeKind.Created, isDir, generation );
				if( isDir )
					_ = Task.Run( () => AddContentsOfNewFolder( e.FullPath, generation ) );
			};
			watcher.Changed += ( _, e ) =>
			{
				if( Volatile.Read( ref _suspendCount ) > 0 || Volatile.Read( ref _watchGeneration ) != generation )
					return;
				if( !Directory.Exists( e.FullPath ) )
					Add( e.FullPath, LocalChangeKind.Updated, isDirectory: false, generation );
			};
			watcher.Deleted += ( _, e ) =>
			{
				if( Volatile.Read( ref _suspendCount ) > 0 || Volatile.Read( ref _watchGeneration ) != generation )
					return;
				Add( e.FullPath, LocalChangeKind.Deleted, isDirectory: false, generation );
			};
			watcher.Renamed += ( _, e ) =>
			{
				if( Volatile.Read( ref _suspendCount ) > 0 || Volatile.Read( ref _watchGeneration ) != generation )
					return;
				bool isDir = Directory.Exists( e.FullPath );
				Add( e.OldFullPath, LocalChangeKind.Deleted, isDir, generation );
				Add( e.FullPath, LocalChangeKind.Created, isDir, generation );
				if( isDir )
					_ = Task.Run( () => AddContentsOfNewFolder( e.FullPath, generation ) );
			};
			watcher.Error += ( _, e ) => OnWatcherError( folder, watcher, e.GetException() );
			watcher.EnableRaisingEvents = Volatile.Read( ref _suspendCount ) == 0;
			_watcher = watcher;
			_folder = folder;
		}
		catch( Exception e )
		{
			watcher?.Dispose();
			HackLogger.Warn( "FileWatcher", $"File watcher could not watch '{folder}': {e.Message}", e );
		}
	}

	private void OnWatcherError( string folder, FileSystemWatcher failedWatcher, Exception ex )
	{
		bool suspended;
		bool shouldRecover = false;
		bool emitOverflowNow = false;
		int recoveryDelayMs = 1000;
		lock( _gate )
		{
			suspended = Volatile.Read( ref _suspendCount ) > 0;
			if( !_disposed && ReferenceEquals( _watcher, failedWatcher ) )
			{
				try { _watcher.Dispose(); } catch { }
				_watcher = null;
				shouldRecover = !suspended;
			}
			if( !suspended && !_disposed )
			{
				_batcher.Clear();
				if( _flushArmed )
				{
					_flushArmed = false;
					_flush.Change( Timeout.Infinite, Timeout.Infinite );
				}
				_burstActive = true;
				_consecutiveOverflows = Math.Min( _consecutiveOverflows + 1, 4 );
				recoveryDelayMs = Math.Min( 3000, 1000 * ( 1 << Math.Min( _consecutiveOverflows - 1, 2 ) ) );
				long now = Environment.TickCount64;
				if( now - _lastOverflowEmitTick >= OverflowCooldownMs )
				{
					_lastOverflowEmitTick = now;
					_trailingOverflowNeeded = false;
					emitOverflowNow = true;
				}
				else
				{
					_trailingOverflowNeeded = true;
				}
				_burstSettle.Change( Math.Max( BurstSettleMs, recoveryDelayMs + 250 ), Timeout.Infinite );
			}
		}

		if( suspended )
			return;

		if( emitOverflowNow )
		{
			HackLogger.Warn( "FileWatcher", $"File watcher error for '{folder}': {ex.Message}; triggering resync and restarting watch", ex );
			Overflowed?.Invoke();
		}

		if( shouldRecover && Interlocked.Exchange( ref _recoveryScheduled, 1 ) == 0 )
		{
			_ = Task.Run( async () =>
			{
				try
				{
					int delayMs = recoveryDelayMs;
					for( int attempt = 0; attempt < MaxRecoveryAttempts; attempt++ )
					{
						await Task.Delay( delayMs );
						bool recoveredOrStopped = false;
						lock( _gate )
						{
							if( _disposed || !string.Equals( _folder, folder, PwaPaths.PathComparison ) )
							{
								recoveredOrStopped = true;
							}
							else if( _watcher is not null )
							{
								recoveredOrStopped = true;
							}
							else if( Volatile.Read( ref _suspendCount ) == 0 )
							{
								StartWatchingLocked( folder, createIfMissing: false );
								recoveredOrStopped = _watcher is not null;
							}
						}
						if( recoveredOrStopped )
							break;
						delayMs = Math.Min( 3000, delayMs * 2 );
					}
				}
				finally
				{
					Interlocked.Exchange( ref _recoveryScheduled, 0 );
				}
			} );
		}
	}

	public static bool IsNoise( string path )
	{
		ReadOnlySpan<char> name = Path.GetFileName( path.AsSpan() );
		return name.IsEmpty
			|| name.StartsWith( "~$", StringComparison.Ordinal )
			|| name.StartsWith( ".~lock", StringComparison.Ordinal )
			|| name[ ^1 ] == '~'
			|| name.EndsWith( ".tmp", StringComparison.OrdinalIgnoreCase )
			|| name.EndsWith( ".swp", StringComparison.OrdinalIgnoreCase );
	}

	private void AddContentsOfNewFolder( string path, long generation )
	{
		if( Volatile.Read( ref _suspendCount ) > 0 || Volatile.Read( ref _watchGeneration ) != generation )
			return;
		try
		{
			DirectoryInfo dir = new( path );
			if( !dir.Exists )
				return;
			int scanned = 0;
			foreach( FileSystemInfo entry in dir.EnumerateFileSystemInfos( "*", NewFolderScanOptions ) )
			{
				if( Volatile.Read( ref _suspendCount ) > 0
					|| Volatile.Read( ref _watchGeneration ) != generation
					|| scanned++ >= MaxScannedInNewFolder )
					break;
				Add( entry.FullName, LocalChangeKind.Created, ( entry.Attributes & FileAttributes.Directory ) != 0, generation );
			}
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}

	internal void SimulateChangeForTest( string path, LocalChangeKind kind, bool isDirectory = false )
		=> Add( path, kind, isDirectory );

	private void Add( string path, LocalChangeKind kind, bool isDirectory, long? expectedGeneration = null )
	{
		if( Volatile.Read( ref _suspendCount ) > 0 || IsNoise( path ) )
			return;

		bool emitOverflowNow = false;
		lock( _gate )
		{
			if( _disposed || Volatile.Read( ref _suspendCount ) > 0 )
				return;
			if( expectedGeneration is long gen && Volatile.Read( ref _watchGeneration ) != gen )
				return;
			if( _burstActive )
			{
				_trailingOverflowNeeded = true;
				_burstSettle.Change( BurstSettleMs, Timeout.Infinite );
				return;
			}
			_batcher.Add( path, kind, DateTime.UtcNow, isDirectory );
			if( _batcher.PendingCount >= MaxPendingChanges )
			{
				_batcher.Clear();
				if( _flushArmed )
				{
					_flushArmed = false;
					_flush.Change( Timeout.Infinite, Timeout.Infinite );
				}
				_burstActive = true;
				long now = Environment.TickCount64;
				if( now - _lastOverflowEmitTick >= OverflowCooldownMs )
				{
					_lastOverflowEmitTick = now;
					_trailingOverflowNeeded = false;
					emitOverflowNow = true;
				}
				else
				{
					_trailingOverflowNeeded = true;
				}
				_burstSettle.Change( BurstSettleMs, Timeout.Infinite );
			}
			else if( !_flushArmed )
			{
				_flushArmed = true;
				_flush.Change( Quiet / 2, Quiet / 2 );
			}
		}

		if( emitOverflowNow )
			Overflowed?.Invoke();
	}

	private void EndBurst()
	{
		bool emitOverflow = false;
		lock( _gate )
		{
			if( _disposed || Volatile.Read( ref _suspendCount ) > 0 )
				return;
			_burstActive = false;
			_consecutiveOverflows = 0;
			if( _trailingOverflowNeeded )
			{
				_trailingOverflowNeeded = false;
				_lastOverflowEmitTick = Environment.TickCount64;
				emitOverflow = true;
			}
		}
		if( emitOverflow )
			Overflowed?.Invoke();
	}

	private void Flush()
	{
		lock( _flushDispatchLock )
		{
			List<LocalFileChange> due;
			lock( _gate )
			{
				if( _disposed || Volatile.Read( ref _suspendCount ) > 0 || _burstActive )
					return;
				due = _batcher.TakeDue( DateTime.UtcNow );
				if( _batcher.PendingCount == 0 )
				{
					_flushArmed = false;
					_flush.Change( Timeout.Infinite, Timeout.Infinite );
				}
				_consecutiveOverflows = 0;
			}
			if( due.Count > 0 )
				Changed?.Invoke( due );
		}
	}

	private void StopWatching()
	{
		Interlocked.Increment( ref _watchGeneration );
		_batcher.Clear();
		_watcher?.Dispose();
		_watcher = null;
		_folder = null;
	}

	public void Dispose()
	{
		lock( _gate )
		{
			if( _disposed )
				return;
			_disposed = true;
			StopWatching();
			_flushArmed = false;
			_burstActive = false;
			_trailingOverflowNeeded = false;
			_flush.Dispose();
			_burstSettle.Dispose();
		}
	}
}
