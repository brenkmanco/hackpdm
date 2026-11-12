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
public sealed class LocalChangeBatcher( TimeSpan quiet )
{
	private readonly Dictionary<string, (LocalChangeKind? Kind, DateTime Due)> _pending = new( StringComparer.Ordinal );

	public int PendingCount => _pending.Count;

	public void Add( string path, LocalChangeKind kind, DateTime now )
	{
		LocalChangeKind? merged = _pending.TryGetValue( path, out var earlier ) ? Merge( earlier.Kind, kind ) : kind;
		_pending[ path ] = ( merged, now + quiet );
	}

	/// <summary>What the file amounts to after <paramref name="earlier"/> then <paramref name="later"/>; null when nothing (created, then deleted again).</summary>
	public static LocalChangeKind? Merge( LocalChangeKind? earlier, LocalChangeKind later ) => ( earlier, later ) switch
	{
		(null, _ ) => later,
		(LocalChangeKind.Created, LocalChangeKind.Deleted ) => null,
		(LocalChangeKind.Created, _ ) => LocalChangeKind.Created,
		(LocalChangeKind.Deleted, LocalChangeKind.Created ) => LocalChangeKind.Updated, // replaced (save via rename)
		(_, LocalChangeKind.Deleted ) => LocalChangeKind.Deleted,
		_ => LocalChangeKind.Updated,
	};

	/// <summary>The paths quiet since their last event, removed from the batch (paths that came to nothing are dropped).</summary>
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

	/// <summary>Watches <paramref name="folder"/> and everything below it, creating it when missing (again only if it is another folder than now); null stops watching.</summary>
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

	public event Action<IReadOnlyList<LocalFileChange>>? Changed;
	public event Action? Overflowed;

	public LocalFileWatcher() => _flush = new Timer( _ => Flush(), null, Timeout.Infinite, Timeout.Infinite );

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

	/// <summary>Lock files and temporary files programs write while saving.</summary>
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

	public void Dispose()
	{
		lock( _gate )
			StopWatching();
		_flush.Dispose();
	}
}
