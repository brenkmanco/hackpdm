using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace HackPDM.Mvvm.Models.Hack;

public enum LocalChangeKind
{
	Created,
	Updated,
	Deleted,
}

/// <summary>One file (or folder) in the working folder that was created, changed or deleted.</summary>
public sealed record LocalFileChange( string FullPath, LocalChangeKind Kind );

/// <summary>
/// Collects file events and hands each path out once it has been quiet for a while, as the one change
/// they add up to: saving a file raises several events (often delete + create + change, when the
/// program writes a temporary file and renames it), which should reach the page as one "updated".
/// Not thread-safe: the watcher locks around it.
/// </summary>
/// <param name="quiet">How long a path must go without events before it is handed out.</param>
public sealed class LocalChangeBatcher( TimeSpan quiet )
{
	private readonly Dictionary<string, (LocalChangeKind? Kind, DateTime Due)> _pending = new( StringComparer.Ordinal );

	public int PendingCount => _pending.Count;

	/// <summary>Records an event for a path, merging it with the path's pending change and restarting its quiet period.</summary>
	/// <param name="path">The full path of the file or folder (compared case-sensitively).</param>
	/// <param name="kind">What the event reported.</param>
	/// <param name="now">The current time; the path becomes due at <paramref name="now"/> plus the quiet period.</param>
	public void Add( string path, LocalChangeKind kind, DateTime now )
	{
		LocalChangeKind? merged = _pending.TryGetValue( path, out var earlier ) ? Merge( earlier.Kind, kind ) : kind;
		_pending[ path ] = ( merged, now + quiet );
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
		foreach( var (path, (kind, at)) in _pending.Where( p => p.Value.Due <= now ).ToList() )
		{
			_pending.Remove( path );
			if( kind is { } k )
				due.Add( new( path, k ) );
		}
		return due;
	}
}

/// <summary>Watches the working folder and reports changed files in batches.</summary>
public interface ILocalFileWatcher
{
	/// <summary>Files that changed, on a background thread; one batch per quiet period.</summary>
	event Action<IReadOnlyList<LocalFileChange>>? Changed;

	/// <summary>Too many events at once: some were lost, so whatever is shown should be reloaded. Background thread.</summary>
	event Action? Overflowed;

	/// <summary>Watches <paramref name="folder"/> and everything below it, creating it when missing; null stops watching.</summary>
	/// <param name="folder">The working folder to watch; null or empty stops watching.</param>
	/// <remarks>Calling it again with the folder already being watched does nothing; another folder replaces the current watch.
	/// When the watch cannot be set up (the inotify limit on Linux, for example) the error is written to standard error and
	/// nothing is watched; no exception is thrown.</remarks>
	void Watch( string? folder );
}

/// <summary>
/// One <see cref="FileSystemWatcher"/> over the whole working folder, for the whole run. Events are
/// batched per path by <see cref="LocalChangeBatcher"/>; editor and office lock/temporary files are ignored.
/// </summary>
public sealed class LocalFileWatcher : ILocalFileWatcher, IDisposable
{
	private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds( 400 );

	private readonly object _gate = new();
	private readonly LocalChangeBatcher _batcher = new( Quiet );
	private readonly Timer _flush;
	private FileSystemWatcher? _watcher;
	private string? _folder;

	/// <inheritdoc/>
	public event Action<IReadOnlyList<LocalFileChange>>? Changed;
	/// <inheritdoc/>
	public event Action? Overflowed;

	/// <summary>Creates a watcher that is not yet watching anything; call <see cref="Watch"/> to start.</summary>
	public LocalFileWatcher() => _flush = new Timer( _ => Flush(), null, Timeout.Infinite, Timeout.Infinite );

	/// <inheritdoc/>
	public void Watch( string? folder )
	{
		lock( _gate )
		{
			if( string.Equals( folder, _folder, StringComparison.Ordinal ) && _watcher is not null )
				return;
			StopWatching();
			if( string.IsNullOrEmpty( folder ) )
				return;
			try
			{
				// the working folder may not exist yet (nothing downloaded): it is ours, so make it and watch it
				Directory.CreateDirectory( folder );
				FileSystemWatcher watcher = new( folder )
				{
					IncludeSubdirectories = true,
					NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
					InternalBufferSize = 64 * 1024,
				};
				watcher.Created += ( _, e ) =>
				{
					Add( e.FullPath, LocalChangeKind.Created );
					AddContentsOfNewFolder( e.FullPath );
				};
				watcher.Changed += ( _, e ) => { if( !Directory.Exists( e.FullPath ) ) Add( e.FullPath, LocalChangeKind.Updated ); };
				watcher.Deleted += ( _, e ) => Add( e.FullPath, LocalChangeKind.Deleted );
				watcher.Renamed += ( _, e ) =>
				{
					Add( e.OldFullPath, LocalChangeKind.Deleted );
					Add( e.FullPath, LocalChangeKind.Created );
				};
				watcher.Error += ( _, e ) =>
				{
					System.Diagnostics.Debug.WriteLine( $"file watcher: {e.GetException().Message}" );
					Overflowed?.Invoke();
				};
				watcher.EnableRaisingEvents = true;
				_watcher = watcher;
				_folder = folder;
			}
			catch( Exception e )
			{
				// e.g. the inotify watch limit on Linux: the page still works, just without live updates
				Console.Error.WriteLine( $"file watcher could not watch {folder}: {e.Message}" );
			}
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
	/// Files already in a folder that was just created (made with its parents, copied or moved in): they
	/// can be written before the folder is watched, and then raise no event of their own.
	/// </summary>
	private void AddContentsOfNewFolder( string path )
	{
		try
		{
			if( !Directory.Exists( path ) )
				return;
			foreach( string entry in Directory.EnumerateFileSystemEntries( path, "*", SearchOption.AllDirectories ).Take( MaxScannedInNewFolder ) )
				Add( entry, LocalChangeKind.Created );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			// gone again or not readable: nothing to report
		}
	}

	private void Add( string path, LocalChangeKind kind )
	{
		if( IsNoise( path ) )
			return;
		lock( _gate )
		{
			_batcher.Add( path, kind, DateTime.UtcNow );
			_flush.Change( Quiet / 2, Quiet / 2 );
		}
	}

	private void Flush()
	{
		List<LocalFileChange> due;
		lock( _gate )
		{
			due = _batcher.TakeDue( DateTime.UtcNow );
			if( _batcher.PendingCount == 0 )
				_flush.Change( Timeout.Infinite, Timeout.Infinite );
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
			StopWatching();
		_flush.Dispose();
	}
}
