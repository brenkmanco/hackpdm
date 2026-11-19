using System.Diagnostics.CodeAnalysis;

using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

[Flags]
public enum FileManagerRefresh
{
	None = 0,
	Entries = 1,
	Tree = 2,
	All = Entries | Tree,
}

/// <summary>Session-scoped file manager context preserved across page navigation.</summary>
public sealed class FileManagerState
{
	public const int MaxCachedEntries = 5000;

	public DirectoryNode? Tree { get; set; }
	public DirectoryRef? SelectedDirectory { get; set; }
	public string? SelectedPath { get; set; }
	public HashSet<string> ExpandedPaths { get; } = new( StringComparer.OrdinalIgnoreCase );

	private EntryListResult? _entries;
	private string? _entriesPath;
	private bool _entriesIncludeDeleted;

	public IReadOnlyList<string> SelectedEntryNames { get; set; } = [];
	public string? SelectedDetailTab { get; set; }
	public bool ShowInactive { get; set; }
	public bool IsGroupingEnabled { get; set; }
	public string? GroupField { get; set; }
	public (string Field, bool Descending)? Sort { get; set; }
	public FileManagerRefresh Stale { get; private set; }
	public int LivePages { get; set; }

	public event Func<IReadOnlyList<string>, string?, Task>? LocateRequested;
	public (IReadOnlyList<string> Segments, string? FileName)? PendingLocate { get; private set; }

	public void RememberEntries( string? treePath, bool includeDeleted, EntryListResult result )
	{
		bool small = result.Rows.Count <= MaxCachedEntries;
		_entries = small ? result : null;
		_entriesPath = small ? treePath : null;
		_entriesIncludeDeleted = includeDeleted;
		Stale &= ~FileManagerRefresh.Entries;
	}

	public bool TryGetEntries( string? treePath, bool includeDeleted, [MaybeNullWhen( false )] out EntryListResult result )
	{
		result = _entries;
		return _entries is not null
			&& !Stale.HasFlag( FileManagerRefresh.Entries )
			&& _entriesIncludeDeleted == includeDeleted
			&& string.Equals( _entriesPath, treePath, StringComparison.OrdinalIgnoreCase );
	}

	public void MarkStale( FileManagerRefresh what )
	{
		Stale |= what;
		if( what.HasFlag( FileManagerRefresh.Entries ) )
			_entries = null;
	}

	public void TreeRefreshed( DirectoryNode tree )
	{
		Tree = tree;
		Stale &= ~FileManagerRefresh.Tree;
	}

	public void LocalFilesChanged( IEnumerable<string?>? treeFolders )
	{
		if( LivePages > 0 )
			return;
		if( treeFolders is null )
		{
			MarkStale( FileManagerRefresh.All );
			return;
		}
		if( _entriesPath is not { } kept )
			return;
		if( treeFolders.Any( folder => PwaPaths.SameTreePath( folder, kept ) ) )
			MarkStale( FileManagerRefresh.Entries );
	}

	public void LocalFilesChanged( IReadOnlyList<LocalFileChange>? changes )
	{
		if( LivePages > 0 )
			return;
		if( changes is null )
		{
			MarkStale( FileManagerRefresh.All );
			return;
		}

		bool treeStale = false;
		bool entriesStale = false;
		foreach( LocalFileChange change in changes )
		{
			string? itemPath = PwaPaths.ToServerName( change.FullPath );
			string? parentPath = PwaPaths.ToServerName( Path.GetDirectoryName( change.FullPath ) );
			string[] itemSegments = PwaPaths.Segments( itemPath );
			bool isDir = change.IsDirectory
				|| Directory.Exists( change.FullPath )
				|| ( change.Kind == LocalChangeKind.Deleted && itemSegments.Length > 0 && Tree?.FindBySegments( itemSegments ) is not null );

			if( isDir )
			{
				treeStale = true;
				if( _entriesPath is not null && PwaPaths.IsSameOrAncestorTreePath( itemPath, _entriesPath ) )
					entriesStale = true;
			}
			else if( _entriesPath is not null && PwaPaths.SameTreePath( parentPath, _entriesPath ) )
			{
				entriesStale = true;
			}
		}

		FileManagerRefresh what = ( treeStale ? FileManagerRefresh.Tree : FileManagerRefresh.None )
			| ( entriesStale ? FileManagerRefresh.Entries : FileManagerRefresh.None );
		if( what != FileManagerRefresh.None )
			MarkStale( what );
	}

	public void Reset()
	{
		Tree = null;
		SelectedDirectory = null;
		SelectedPath = null;
		ExpandedPaths.Clear();
		_entries = null;
		_entriesPath = null;
		SelectedEntryNames = [];
		PendingLocate = null;
		Stale = FileManagerRefresh.None;
	}

	public Task RequestLocateAsync( IReadOnlyList<string> segmentsBelowRoot, string? fileName )
	{
		if( LocateRequested is { } handler )
			return handler( segmentsBelowRoot, fileName );
		PendingLocate = ( segmentsBelowRoot, fileName );
		return Task.CompletedTask;
	}

	public (IReadOnlyList<string> Segments, string? FileName)? TakePendingLocate()
	{
		var pending = PendingLocate;
		PendingLocate = null;
		return pending;
	}
}
