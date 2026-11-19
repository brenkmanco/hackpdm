using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HackPDM.Mvvm.Models.Hack;

public enum LocalChangeKind
{
	Created,
	Updated,
	Deleted,
}

/// <summary>One file (or folder) in the working folder that was created, changed or deleted.</summary>
public sealed record LocalFileChange( string FullPath, LocalChangeKind Kind, bool IsDirectory = false );

/// <summary>
/// Collects file events and hands each path out once it has been quiet for a while, as the one change
/// they add up to: saving a file raises several events (often delete + create + change, when the
/// program writes a temporary file and renames it), which should reach the page as one "updated".
/// Not thread-safe: the watcher locks around it.
/// </summary>
/// <param name="quiet">How long a path must go without events before it is handed out.</param>
public sealed class LocalChangeBatcher( TimeSpan quiet )
{
	private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	private readonly Dictionary<string, (LocalChangeKind? Kind, DateTime Due, bool IsDirectory)> _pending = new( PathComparer );

	public int PendingCount => _pending.Count;

	/// <summary>Records an event for a path, merging it with the path's pending change and restarting its quiet period.</summary>
	/// <param name="path">The full path of the file or folder.</param>
	/// <param name="kind">What the event reported.</param>
	/// <param name="now">The current time; the path becomes due at <paramref name="now"/> plus the quiet period.</param>
	/// <param name="isDirectory">Whether the path is known to be a directory.</param>
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

	/// <summary>Combines two successive changes to one path into the change they add up to.</summary>
	/// <param name="earlier">The pending change; null when there is none (or the earlier events came to nothing).</param>
	/// <param name="later">The new event.</param>
	/// <returns>The combined change, e.g. deleted then created is <see cref="LocalChangeKind.Updated"/> (a save via rename);
	/// null when the events cancel out (created, then deleted again).</returns>
	public static LocalChangeKind? Merge( LocalChangeKind? earlier, LocalChangeKind later ) => ( earlier, later ) switch
	{
		(null, _ ) => later,
		(LocalChangeKind.Created, LocalChangeKind.Deleted ) => null,
		(LocalChangeKind.Created, _ ) => LocalChangeKind.Created,
		(LocalChangeKind.Deleted, LocalChangeKind.Created ) => LocalChangeKind.Updated, // replaced (save via rename)
		(_, LocalChangeKind.Deleted ) => LocalChangeKind.Deleted,
		_ => LocalChangeKind.Updated,
	};

	/// <summary>Removes and returns the paths that have been quiet since their last event.</summary>
	/// <param name="now">The current time, compared with each path's due time.</param>
	/// <returns>The due changes; paths whose events came to nothing are removed but not returned.</returns>
	public List<LocalFileChange> TakeDue( DateTime now )
	{
		List<LocalFileChange> due = [];
		foreach( var (path, (kind, at, isDir)) in _pending.Where( p => p.Value.Due <= now ).ToList() )
		{
			_pending.Remove( path );
			if( kind is { } k )
				due.Add( new( path, k, isDir ) );
		}
		return due;
	}
}

/// <summary>Watches the working folder and reports changed files and folders in batches.</summary>
public interface ILocalFileWatcher
{
	/// <summary>Files or folders that changed, on a background thread; one batch per quiet period.</summary>
	event Action<IReadOnlyList<LocalFileChange>>? Changed;

	/// <summary>Too many events at once or a watcher error: some were lost, so whatever is shown should be reloaded. Background thread.</summary>
	event Action? Overflowed;

	/// <summary>Watches <paramref name="folder"/> and everything below it, creating it when missing; null stops watching.</summary>
	/// <param name="folder">The working folder to watch; null or empty stops watching.</param>
	void Watch( string? folder );

	/// <summary>
	/// Temporarily suppresses watcher notifications (e.g. during bulk downloads) until the returned handle is disposed.
	/// </summary>
	IDisposable Suspend() => NoOpDisposable.Instance;
}

internal sealed class NoOpDisposable : IDisposable
{
	public static readonly NoOpDisposable Instance = new();
	public void Dispose() { }
}

/// <summary>
/// One <see cref="FileSystemWatcher"/> over the whole working folder, for the whole run. Events are
/// batched per path by <see cref="LocalChangeBatcher"/>; editor and office lock/temporary files are ignored.
/// </summary>
public sealed class LocalFileWatcher : ILocalFileWatcher, IDisposable
{
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

	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public event Action<IReadOnlyList<LocalFileChange>>? Changed;
	/// <inheritdoc/>
	public event Action? Overflowed;

	/// <summary>Creates a watcher that is not yet watching anything; call <see cref="Watch"/> to start.</summary>
	public LocalFileWatcher() => _flush = new Timer( _ => Flush(), null, Timeout.Infinite, Timeout.Infinite );

	/// <inheritdoc/>
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
				HackPDM.Core.Logging.HackLogger.Warn( "FileWatcher", $"Invalid watch folder '{folder}': {e.Message}", e );
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
			// the working folder may not exist yet (nothing downloaded): it is ours, so make it and watch it
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
			// e.g. the inotify watch limit on Linux: the page still works, just without live updates
			HackPDM.Core.Logging.HackLogger.Warn( "FileWatcher", $"File watcher could not watch '{folder}': {e.Message}", e );
		}
	}

	private void OnWatcherError( string folder, FileSystemWatcher failedWatcher, Exception ex )
	{
		HackPDM.Core.Logging.HackLogger.Warn( "FileWatcher", $"File watcher error for '{folder}': {ex.Message}; triggering resync and restarting watch", ex );
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

	/// <summary>Whether a path is a lock file or temporary file that programs write while saving, and so is ignored.</summary>
	/// <param name="path">The full path or file name.</param>
	/// <returns><see langword="true"/> for "~$" and ".~lock" prefixes, a trailing "~", ".tmp" and ".swp" files, and paths with no file name.</returns>
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

	/// <summary>Most entries to report from a folder that just appeared.</summary>
	private const int MaxScannedInNewFolder = 1000;

	/// <summary>
	/// Files and subfolders already in a folder that was just created (made with its parents, copied or moved in):
	/// they can be written before the folder is watched, and then raise no event of their own.
	/// </summary>
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
			// gone again or not readable: nothing to report
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
			// Arm once rather than resetting on every event so continuous vault writes cannot starve already-due paths
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

	/// <summary>Stops watching and stops the flush timer; pending changes are not reported.</summary>
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
