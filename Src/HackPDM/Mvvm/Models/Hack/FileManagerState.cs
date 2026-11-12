using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core.Hack;

namespace HackPDM.Mvvm.Models.Hack;

/// <summary>What the file manager should fetch again from the server.</summary>
[Flags]
public enum FileManagerRefresh
{
	None = 0,
	Entries = 1,
	Tree = 2,
	All = Entries | Tree,
}

/// <summary>
/// The file manager's context, kept for the whole session (one instance, held by the composition root)
/// so leaving the page and coming back shows the same thing without asking the server again: the
/// directory tree, the selected folder and its rows, the selected rows and the list settings.
/// <para>
/// The page's view model is created on every visit from this state and writes its changes back; it is
/// disposed when the page is left. Plain data only (no observable collections or events the page binds
/// to), so a page that is gone is never kept alive by this long-lived object.
/// </para>
/// <para>
/// Deliberately not kept, because it is large and cheap to fetch for one row: preview images and the
/// detail tabs (history, parents, children, properties), and an entry list longer than
/// <see cref="MaxCachedEntries"/>, which is reloaded instead.
/// </para>
/// </summary>
public sealed class FileManagerState
{
	/// <summary>Entry lists longer than this are not kept between visits.</summary>
	public const int MaxCachedEntries = 5000;

	// ---------------------------------------------------------------- tree and selection

	/// <summary>The last directory tree fetched from the server; null until the page first loads it.</summary>
	public DirectoryNode? Tree { get; set; }

	/// <summary>The folder selected in the tree.</summary>
	public DirectoryRef? SelectedDirectory { get; set; }

	/// <summary>Tree path ("root\a\b") of the selected folder; restores the selection after the tree is rebuilt.</summary>
	public string? SelectedPath { get; set; }

	/// <summary>Tree paths of the expanded folders.</summary>
	public HashSet<string> ExpandedPaths { get; } = new( StringComparer.OrdinalIgnoreCase );

	// ---------------------------------------------------------------- entry list

	private EntryListResult? _entries;
	private string? _entriesPath;
	private bool _entriesIncludeDeleted;

	/// <summary>Names of the rows selected in the entry list (names are unique within a folder).</summary>
	public IReadOnlyList<string> SelectedEntryNames { get; set; } = [];

	/// <summary>The visible detail tab (one of the StorageBox *_TAB names).</summary>
	public string? SelectedDetailTab { get; set; }

	/// <summary>List deleted ("inactive") entries too.</summary>
	public bool ShowInactive { get; set; }

	public bool IsGroupingEnabled { get; set; }

	/// <summary>The EntryRow property the list is grouped by; null for the default (status).</summary>
	public string? GroupField { get; set; }

	/// <summary>The EntryRow property the list is sorted by, and the direction; null when unsorted.</summary>
	public (string Field, bool Descending)? Sort { get; set; }

	/// <summary>Keeps the rows of <paramref name="treePath"/>, unless there are more than <see cref="MaxCachedEntries"/>.</summary>
	public void RememberEntries( string? treePath, bool includeDeleted, EntryListResult result )
	{
		bool small = result.Rows.Count <= MaxCachedEntries;
		_entries = small ? result : null;
		_entriesPath = small ? treePath : null;
		_entriesIncludeDeleted = includeDeleted;
		Stale &= ~FileManagerRefresh.Entries;
	}

	/// <summary>The kept rows of <paramref name="treePath"/>, when they were kept with the same filter and are not stale.</summary>
	public bool TryGetEntries( string? treePath, bool includeDeleted, out EntryListResult result )
	{
		result = _entries!;
		return _entries is not null
			&& !Stale.HasFlag( FileManagerRefresh.Entries )
			&& _entriesIncludeDeleted == includeDeleted
			&& string.Equals( _entriesPath, treePath, StringComparison.OrdinalIgnoreCase );
	}

	// ---------------------------------------------------------------- staleness

	/// <summary>
	/// What changed on the server since it was kept, e.g. an operation that finished after the page was left.
	/// The next visit fetches it again instead of showing the kept copy.
	/// </summary>
	public FileManagerRefresh Stale { get; private set; }

	public void MarkStale( FileManagerRefresh what )
	{
		Stale |= what;
		if( what.HasFlag( FileManagerRefresh.Entries ) )
			_entries = null;
	}

	/// <summary>The tree was fetched again.</summary>
	public void TreeRefreshed( DirectoryNode tree )
	{
		Tree = tree;
		Stale &= ~FileManagerRefresh.Tree;
	}

	/// <summary>File manager view models currently showing this state; while there is one, it applies local file changes itself.</summary>
	public int LivePages { get; set; }

	/// <summary>
	/// Files changed on disk while the page was not shown (<paramref name="treeFolders"/> are their folders'
	/// tree paths; null when changes were lost): the kept rows of a folder they touched are stale.
	/// </summary>
	public void LocalFilesChanged( IEnumerable<string?>? treeFolders )
	{
		if( LivePages > 0 || _entriesPath is not { } kept )
			return;
		if( treeFolders is null || treeFolders.Any( folder => string.Equals( folder, kept, StringComparison.OrdinalIgnoreCase ) ) )
			MarkStale( FileManagerRefresh.Entries );
	}

	/// <summary>Forgets everything (another server or user logged in).</summary>
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

	// ---------------------------------------------------------------- locate requests (search window, links)

	/// <summary>Handled by the file manager while its page is shown; requests made meanwhile wait in <see cref="PendingLocate"/>.</summary>
	public event Func<IReadOnlyList<string>, string?, Task>? LocateRequested;

	/// <summary>The folder (segments below root) and file a locate request asked for while the page was not shown.</summary>
	public (IReadOnlyList<string> Segments, string? FileName)? PendingLocate { get; private set; }

	/// <summary>Shows <paramref name="fileName"/> in the folder at <paramref name="segmentsBelowRoot"/>: now when the page is shown, else on the next visit.</summary>
	public Task RequestLocateAsync( IReadOnlyList<string> segmentsBelowRoot, string? fileName )
	{
		if( LocateRequested is { } handler )
			return handler( segmentsBelowRoot, fileName );
		PendingLocate = ( segmentsBelowRoot, fileName );
		return Task.CompletedTask;
	}

	/// <summary>The waiting locate request, which is then cleared.</summary>
	public (IReadOnlyList<string> Segments, string? FileName)? TakePendingLocate()
	{
		var pending = PendingLocate;
		PendingLocate = null;
		return pending;
	}
}
