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
	private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds( 400 );
	private static readonly EnumerationOptions NewFolderScanOptions = new()
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		ReturnSpecialDirectories = false,
	};

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
			if( string.Equals( normalized, _folder, PwaPaths.PathComparison ) && _watcher is not null )
				return;
			StopWatching();
			if( string.IsNullOrEmpty( normalized ) )
				return;
			StartWatchingLocked( normalized );
		}
	}

	private void StartWatchingLocked( string folder )
	{
		FileSystemWatcher? watcher = null;
		try
		{
			Directory.CreateDirectory( folder );
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
			watcher?.Dispose();
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
						if( !_disposed && _watcher is null && string.Equals( _folder, folder, PwaPaths.PathComparison ) )
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
		ReadOnlySpan<char> name = Path.GetFileName( path.AsSpan() );
		return name.IsEmpty
			|| name.StartsWith( "~$", StringComparison.Ordinal )
			|| name.StartsWith( ".~lock", StringComparison.Ordinal )
			|| name[ ^1 ] == '~'
			|| name.EndsWith( ".tmp", StringComparison.OrdinalIgnoreCase )
			|| name.EndsWith( ".swp", StringComparison.OrdinalIgnoreCase );
	}

	private void AddContentsOfNewFolder( string path )
	{
		try
		{
			DirectoryInfo dir = new( path );
			if( !dir.Exists )
				return;
			int scanned = 0;
			foreach( FileSystemInfo entry in dir.EnumerateFileSystemInfos( "*", NewFolderScanOptions ) )
			{
				if( scanned++ >= MaxScannedInNewFolder )
					break;
				Add( entry.FullName, LocalChangeKind.Created, ( entry.Attributes & FileAttributes.Directory ) != 0 );
			}
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
