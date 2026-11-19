using HackPDM.Core.Configuration;

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
	private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	private readonly Dictionary<string, (LocalChangeKind? Kind, DateTime Due, bool IsDirectory)> _pending = new( PathComparer );

	public int PendingCount => _pending.Count;

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
		List<LocalFileChange> due = [];
		foreach( var (path, (kind, _, isDir)) in _pending.Where( p => p.Value.Due <= now ).ToList() )
		{
			_pending.Remove( path );
			if( kind is { } k )
				due.Add( new( path, k, isDir ) );
		}
		return due;
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
	private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds( 400 );
	private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	private readonly object _gate = new();
	private readonly LocalChangeBatcher _batcher = new( Quiet );
	private readonly Timer _flush;
	private FileSystemWatcher? _watcher;
	private string? _folder;
	private bool _flushArmed;
	private bool _disposed;
	private int _recoveryScheduled;
	private int _suspendCount;

	public event Action<IReadOnlyList<LocalFileChange>>? Changed;
	public event Action? Overflowed;

	public LocalFileWatcher() => _flush = new Timer( _ => Flush(), null, Timeout.Infinite, Timeout.Infinite );

	public IDisposable Suspend()
	{
		Interlocked.Increment( ref _suspendCount );
		return new SuspendScope( this );
	}

	private sealed class SuspendScope( LocalFileWatcher owner ) : IDisposable
	{
		private int _disposed;
		public void Dispose()
		{
			if( Interlocked.Exchange( ref _disposed, 1 ) == 0 )
				Interlocked.Decrement( ref owner._suspendCount );
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
			if( string.Equals( normalized, _folder, PathComparison ) && _watcher is not null )
				return;
			StopWatching();
			if( string.IsNullOrEmpty( normalized ) )
				return;
			StartWatchingLocked( normalized );
		}
	}

	private void StartWatchingLocked( string folder )
	{
		try
		{
			Directory.CreateDirectory( folder );
			FileSystemWatcher watcher = new( folder )
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
				bool isDir = Directory.Exists( e.FullPath );
				Add( e.FullPath, LocalChangeKind.Created, isDir );
				if( isDir )
					_ = Task.Run( () => AddContentsOfNewFolder( e.FullPath ) );
			};
			watcher.Changed += ( _, e ) =>
			{
				if( !Directory.Exists( e.FullPath ) )
					Add( e.FullPath, LocalChangeKind.Updated, isDirectory: false );
			};
			watcher.Deleted += ( _, e ) => Add( e.FullPath, LocalChangeKind.Deleted, isDirectory: false );
			watcher.Renamed += ( _, e ) =>
			{
				bool isDir = Directory.Exists( e.FullPath );
				Add( e.OldFullPath, LocalChangeKind.Deleted, isDir );
				Add( e.FullPath, LocalChangeKind.Created, isDir );
				if( isDir )
					_ = Task.Run( () => AddContentsOfNewFolder( e.FullPath ) );
			};
			watcher.Error += ( _, e ) => OnWatcherError( folder, watcher, e.GetException() );
			watcher.EnableRaisingEvents = true;
			_watcher = watcher;
			_folder = folder;
		}
		catch( Exception e )
		{
			HackLogger.Warn( "FileWatcher", $"File watcher could not watch '{folder}': {e.Message}", e );
		}
	}

	private void OnWatcherError( string folder, FileSystemWatcher failedWatcher, Exception ex )
	{
		HackLogger.Warn( "FileWatcher", $"File watcher error for '{folder}': {ex.Message}; triggering resync and restarting watch", ex );
		bool shouldRecover = false;
		lock( _gate )
		{
			if( !_disposed && ReferenceEquals( _watcher, failedWatcher ) )
			{
				try { _watcher.Dispose(); } catch { }
				_watcher = null;
				shouldRecover = true;
			}
		}

		Overflowed?.Invoke();

		if( shouldRecover && Interlocked.Exchange( ref _recoveryScheduled, 1 ) == 0 )
		{
			_ = Task.Run( async () =>
			{
				try
				{
					await Task.Delay( 1000 );
					lock( _gate )
					{
						if( !_disposed && _watcher is null && string.Equals( _folder, folder, PathComparison ) )
							StartWatchingLocked( folder );
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
		string name = Path.GetFileName( path );
		return name.Length == 0
			|| name.StartsWith( "~$", StringComparison.Ordinal )
			|| name.StartsWith( ".~lock", StringComparison.Ordinal )
			|| name.EndsWith( '~' )
			|| name.EndsWith( ".tmp", StringComparison.OrdinalIgnoreCase )
			|| name.EndsWith( ".swp", StringComparison.OrdinalIgnoreCase );
	}

	private void AddContentsOfNewFolder( string path )
	{
		try
		{
			if( !Directory.Exists( path ) )
				return;
			foreach( string entry in Directory.EnumerateFileSystemEntries( path, "*", SearchOption.AllDirectories ).Take( MaxScannedInNewFolder ) )
				Add( entry, LocalChangeKind.Created, Directory.Exists( entry ) );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}

	private void Add( string path, LocalChangeKind kind, bool isDirectory )
	{
		if( Volatile.Read( ref _suspendCount ) > 0 || IsNoise( path ) )
			return;
		lock( _gate )
		{
			if( _disposed )
				return;
			_batcher.Add( path, kind, DateTime.UtcNow, isDirectory );
			if( !_flushArmed )
			{
				_flushArmed = true;
				_flush.Change( Quiet / 2, Quiet / 2 );
			}
		}
	}

	private void Flush()
	{
		List<LocalFileChange> due;
		lock( _gate )
		{
			if( _disposed )
				return;
			due = _batcher.TakeDue( DateTime.UtcNow );
			if( _batcher.PendingCount == 0 )
			{
				_flushArmed = false;
				_flush.Change( Timeout.Infinite, Timeout.Infinite );
			}
		}
		if( due.Count > 0 )
			Changed?.Invoke( due );
	}

	private void StopWatching()
	{
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
			_flush.Dispose();
		}
	}
}
