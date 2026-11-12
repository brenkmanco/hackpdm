using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Mvvm.Models.Common;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Types;


using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.ViewModels.Hack;

/// <summary>
/// State and commands for the Hack file manager page. Holds no references to controls:
/// the view pushes selection in, invokes commands, and reacts to <see cref="RefreshRequested"/>
/// and <see cref="GroupingChanged"/>.
/// <para>
/// One instance per visit: created when the page is entered, restored from the session's
/// <see cref="FileManagerState"/>, which it keeps up to date, and disposed when the page is left.
/// Operations started from it (commits, downloads) keep running after that; when they finish they
/// mark the state stale, so the next visit fetches what they changed.
/// </para>
/// </summary>
public partial class HackFileManagerViewModel : OperationViewModel, IDisposable
{
	private readonly FileManagerState _state;
	private readonly IPdmWorkflowService _workflow;
	private readonly IEntryFileService _files;
	private readonly IEntryDetailsService _details;
	private readonly IDirectoryTreeService _directoryTree;
	private readonly IEntryListService _entryList;
	private readonly ILocalFileWatcher _localFiles;
	/// <summary>One batch of local file changes at a time, in the order they came.</summary>
	private readonly SemaphoreSlim _localChangesGate = new( 1, 1 );
	/// <summary>More changed files than this in the shown folder at once (a whole download) reload the list instead.</summary>
	private const int MaxSingleRowUpdates = 25;
	private bool _active;

	private CancellationTokenSource _detailsCancellation = new();
	/// <summary>Cancelled when the page is left: the loads that only fill this page.</summary>
	private readonly CancellationTokenSource _pageCancellation = new();
	/// <summary>Set while the constructor copies the kept state in, so the change hooks don't write it back or reload.</summary>
	private readonly bool _restoring;
	/// <summary>Rows that were selected when the page was last left; reselected once their folder is listed.</summary>
	private IReadOnlyList<string> _selectionToRestore;
	/// <summary>The folder those rows are in.</summary>
	private readonly string? _selectionFolder;

	/// <summary>Creates the page's view model and restores the list settings and selection kept in <paramref name="state"/>.</summary>
	/// <param name="state">The run-wide file manager state this visit restores from and writes back to.</param>
	/// <param name="workflow">Runs the PDM operations (get latest, checkout, commit, delete).</param>
	/// <param name="files">Local file work: opening, deleting, history version downloads.</param>
	/// <param name="details">Fills the detail tabs and the preview.</param>
	/// <param name="dialogs">Shows messages and confirmations.</param>
	/// <param name="reporter">The status window operations log into.</param>
	/// <param name="directoryTree">Loads the directory tree.</param>
	/// <param name="entryList">Lists a folder's rows and rebuilds single rows.</param>
	/// <param name="localFiles">Reports local file changes; subscribed to by <see cref="Activate"/>.</param>
	/// <remarks>Nothing is loaded and no events are subscribed until <see cref="Activate"/>, <see cref="LoadDirectoryTreeAsync"/>
	/// and <see cref="LoadEntriesAsync"/> are called.</remarks>
	public HackFileManagerViewModel(
		FileManagerState state,
		IPdmWorkflowService workflow,
		IEntryFileService files,
		IEntryDetailsService details,
		IDialogService dialogs,
		IOperationReporter reporter,
		IDirectoryTreeService directoryTree,
		IEntryListService entryList,
		ILocalFileWatcher localFiles ) : base( dialogs, reporter )
	{
		_state = state;
		_localFiles = localFiles;
		_workflow = workflow;
		_files = files;
		_details = details;
		_directoryTree = directoryTree;
		_entryList = entryList;
		_detailTabs = new()
		{
			[ StorageBox.HISTORY_TAB ] = async entry => OHistories.ReplaceAll( await _details.GetHistoryAsync( entry ) ),
			[ StorageBox.PARENT_TAB ] = async entry => OParents.ReplaceAll( await _details.GetParentsAsync( entry ) ),
			[ StorageBox.CHILD_TAB ] = async entry => OChildren.ReplaceAll( await _details.GetChildrenAsync( entry ) ),
			[ StorageBox.PROPERTIES_TAB ] = async entry => OProperties.ReplaceAll( await _details.GetPropertiesAsync( entry ) ),
			[ StorageBox.INFO_TAB ] = async entry => OVersions.ReplaceAll( await _details.GetVersionInfoAsync( entry ) ),
		};

		_restoring = true;
		ShowInactive = state.ShowInactive;
		IsGroupingEnabled = state.IsGroupingEnabled;
		if( EntryRowKeys.TryGetGroupKey( state.GroupField, out var groupKey ) )
			_groupKey = groupKey;
		_selectionToRestore = state.SelectedEntryNames;
		_selectionFolder = state.SelectedPath;
		_restoring = false;
	}

	/// <summary>Starts taking locate requests (search window, dependency links); call once the view handles <see cref="LocateRequested"/>.</summary>
	/// <remarks>The first call also subscribes to local file changes and starts watching the vault folder; later calls only take the pending request.</remarks>
	/// <returns>The request made while the page was not shown, if any: the view should run it once the tree is up.</returns>
	public (IReadOnlyList<string> Segments, string? FileName)? Activate()
	{
		if( !_active && !IsDisposed )
		{
			_active = true;
			_state.LivePages++;
			_state.LocateRequested += ForwardLocateAsync;
			_localFiles.Changed += OnLocalFilesChanged;
			_localFiles.Overflowed += OnLocalChangesLost;
			_localFiles.Watch( PwaPaths.Vault );
		}
		return _state.TakePendingLocate();
	}

	private Task ForwardLocateAsync( IReadOnlyList<string> segments, string? fileName )
		=> LocateRequested?.Invoke( segments, fileName ) ?? Task.CompletedTask;

	public bool IsDisposed { get; private set; }

	/// <summary>A directory tree from an earlier visit can be shown without asking the server.</summary>
	public bool HasKeptTree => _state.Tree is not null && !_state.Stale.HasFlag( FileManagerRefresh.Tree );

	/// <summary>The page was left: stops the loads that only served it and lets go of the session state's events.</summary>
	public void Dispose()
	{
		if( IsDisposed )
			return;
		IsDisposed = true;
		if( _active )
		{
			_active = false;
			_state.LivePages--;
			_state.LocateRequested -= ForwardLocateAsync;
			_localFiles.Changed -= OnLocalFilesChanged;
			_localFiles.Overflowed -= OnLocalChangesLost;
		}
		_pageCancellation.Cancel();
		_detailsCancellation.Cancel();
		GC.SuppressFinalize( this );
	}

	/// <summary>How each detail tab is filled for an entry.</summary>
	private readonly Dictionary<string, Func<EntryRow, Task>> _detailTabs;

	#region Events for the view

	/// <summary>Raised when the tree and/or entry list must be rebuilt from the server.</summary>
	public event Action<FileManagerRefresh>? RefreshRequested;
	/// <summary>Raised after the entry grouping or sort order changed, so the view can rebind the grid.</summary>
	public event Action? GroupingChanged;
	/// <summary>Raised with the freshly loaded directory tree; the view turns it into tree nodes.</summary>
	public event Func<DirectoryNode, Task>? TreeLoaded;
	/// <summary>Raised when another page (search) asks to show a file: tree segments below root, then the file name.</summary>
	public event Func<IReadOnlyList<string>, string?, Task>? LocateRequested;

	/// <summary>Something changed on the server: the kept copy is stale, and the page (if still shown) reloads it.</summary>
	private void Refresh( FileManagerRefresh what )
	{
		_state.MarkStale( what );
		RefreshRequested?.Invoke( what );
	}

	/// <summary>The toolbar's refresh: fetches the tree, and the selected folder's rows, from the server again.</summary>
	/// <remarks>Marks everything stale and raises <see cref="RefreshRequested"/> for the tree; the view's rebuild then reloads the rows.</remarks>
	public void RefreshFromServer()
	{
		_state.MarkStale( FileManagerRefresh.All );
		// rebuilding the tree reselects the folder, which lists its (now stale, so fetched) rows
		RefreshRequested?.Invoke( FileManagerRefresh.Tree );
	}

	/// <summary>A file changed in a folder other than the one listed: its tree path, and how (for the tree's badge).</summary>
	public event Action<string, LocalChangeKind>? FolderChanged;
	/// <summary>A row of the list was rebuilt (old, new; either null for an added or removed row), so the view can keep it selected.</summary>
	public event Action<EntryRow?, EntryRow?>? RowReplaced;

	#endregion
	#region Live updates from the local disk

	private void OnLocalFilesChanged( IReadOnlyList<LocalFileChange> batch )
		=> UiDispatcher.Post( () => _ = ApplyLocalChangesAsync( batch ) );

	private void OnLocalChangesLost()
		=> UiDispatcher.Post( () =>
		{
			if( !IsDisposed )
				Refresh( FileManagerRefresh.Entries );
		} );

	/// <summary>
	/// Updates just the rows of changed files in the listed folder; for other folders raises
	/// <see cref="FolderChanged"/> once per folder ("updated" when its files changed in different ways).
	/// </summary>
	/// <param name="batch">The changes reported by the local file watcher.</param>
	/// <remarks>
	/// Call on the UI thread. Batches are applied one at a time, in order. When more than 25 files of the listed folder
	/// changed at once the whole list is reloaded instead. Updated rows become the kept rows in <see cref="FileManagerState"/>.
	/// Errors are written to standard error rather than thrown.
	/// </remarks>
	public async Task ApplyLocalChangesAsync( IReadOnlyList<LocalFileChange> batch )
	{
		if( IsDisposed )
			return;
		await _localChangesGate.WaitAsync();
		try
		{
			string? listed = IsListLoaded ? LastSelectedNodePath : null;
			List<LocalFileChange> here = [];
			Dictionary<string, LocalChangeKind> elsewhere = new( StringComparer.OrdinalIgnoreCase );
			foreach( LocalFileChange change in batch )
			{
				if( PwaPaths.ToServerName( Path.GetDirectoryName( change.FullPath ) ) is not { } folder )
					continue;
				if( listed is not null && string.Equals( folder, listed, StringComparison.OrdinalIgnoreCase ) )
				{
					// the list shows files only; a folder created here appears in the tree after a refresh
					if( !Directory.Exists( change.FullPath ) )
						here.Add( change );
				}
				else
					elsewhere[ folder ] = elsewhere.TryGetValue( folder, out var earlier ) && earlier != change.Kind
						? LocalChangeKind.Updated
						: change.Kind;
			}

			foreach( var (folder, kind) in elsewhere )
				FolderChanged?.Invoke( folder, kind );

			if( here.Count > MaxSingleRowUpdates )
			{
				Refresh( FileManagerRefresh.Entries );
				return;
			}
			bool changed = false;
			foreach( LocalFileChange change in here )
				changed |= await RefreshRowAsync( change.FullPath, listed! );
			if( changed && !IsDisposed )
				KeepListedRows();
		}
		catch( Exception e )
		{
			Console.Error.WriteLine( $"live update failed: {e}" );
		}
		finally
		{
			_localChangesGate.Release();
		}
	}

	/// <summary>Rebuilds, adds or removes the row of one file of the listed folder; false when the folder changed meanwhile.</summary>
	private async Task<bool> RefreshRowAsync( string localPath, string listedFolder )
	{
		string name = Path.GetFileName( localPath );
		EntryRow? old = Entries.FirstOrDefault( r => string.Equals( r.Name, name, StringComparison.OrdinalIgnoreCase ) );
		EntryRow? row = await _entryList.RefreshRowAsync( old, localPath, IsFiltered );
		if( IsDisposed || !IsListLoaded || !string.Equals( LastSelectedNodePath, listedFolder, StringComparison.OrdinalIgnoreCase ) )
			return false;

		// look again: the list may have changed while the file was read
		int index = old is null ? -1 : Entries.IndexOf( old );
		if( index < 0 )
			index = Entries.ToList().FindIndex( r => string.Equals( r.Name, name, StringComparison.OrdinalIgnoreCase ) );
		EntryRow? replaced = index >= 0 ? Entries[ index ] : null;
		if( row is null )
		{
			if( index < 0 )
				return false;
			Entries.RemoveAt( index );
		}
		else if( index >= 0 )
			Entries[ index ] = row;
		else
			Entries.Add( row );
		RowReplaced?.Invoke( replaced, row );
		return true;
	}

	/// <summary>The listed rows (with their live updates) become the kept rows; the counts follow them.</summary>
	private void KeepListedRows()
	{
		List<EntryRow> rows = [ .. Entries ];
		EntryListResult result = new(
			rows,
			RemoteCount: rows.Count( r => r.Id is > 0 ),
			LocalOnlyCount: rows.Count( r => r.ReprType == EntryReprType.Local ),
			LocalAndRemoteCount: rows.Count( r => r.ReprType == EntryReprType.Both ) );
		_state.RememberEntries( LastSelectedNodePath, ShowInactive, result );
		UpdateEntryLabels( result );
	}

	#endregion
	#region Load state (read by the view, SearchOdoo and the smoke tests)

	public bool IsLoaded { get; set; }
	public bool IsTreeLoaded { get; set; }
	public bool IsListLoaded { get; set; }
	public bool IsFiltered { get; set; } = true;


	/// <summary>True while the directory tree is being fetched; drives the loading ring.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsPreviewVisible ) )]
	public partial bool IsTreeLoading { get; set; }

	/// <summary>The preview image shares its slot with the loading ring.</summary>
	public bool IsPreviewVisible => !IsTreeLoading;

	#endregion
	#region Bindable state

	[ObservableProperty] public partial string EntriesLabel { get; set; } = "Entries: None";
	[ObservableProperty] public partial string EntriesLabelLocal { get; set; } = "LO: None";
	[ObservableProperty] public partial string EntriesLabelRemote { get; set; } = "RO: None";

	/// <summary>Preview of the selected entry or version; the view turns it into an image.</summary>
	[ObservableProperty] public partial EntryPreview? Preview { get; set; }

	/// <summary>Show deleted ("inactive") entries in the list.</summary>
	[ObservableProperty] public partial bool ShowInactive { get; set; }
	partial void OnShowInactiveChanged( bool value )
	{
		if( _restoring )
			return;
		_state.ShowInactive = value;
		Entries.Clear();
		RefreshRequested?.Invoke( FileManagerRefresh.Entries );
	}

	/// <summary>Group the entry list (by status, or by the field picked in "Group By").</summary>
	[ObservableProperty] public partial bool IsGroupingEnabled { get; set; }
	partial void OnIsGroupingEnabledChanged( bool value )
	{
		if( _restoring )
			return;
		_state.IsGroupingEnabled = value;
		GroupingChanged?.Invoke();
	}

	/// <summary>Rows of the selected directory.</summary>
	public RangeObservableCollection<EntryRow> Entries { get; } = new();

	private static readonly Func<EntryRow, IComparable?> GroupByStatus = row => row.Status;
	private Func<EntryRow, IComparable?> _groupKey = GroupByStatus;

	/// <summary>Key the entry list is grouped by, or null when grouping is off.</summary>
	public Func<EntryRow, IComparable?>? ActiveGroupKey => IsGroupingEnabled ? _groupKey : null;

	public RangeObservableCollection<HistoryRow> OHistories { get; } = [];
	public RangeObservableCollection<ParentRow> OParents { get; } = [];
	public RangeObservableCollection<ChildrenRow> OChildren { get; } = [];
	public RangeObservableCollection<PropertiesRow> OProperties { get; } = [];
	public RangeObservableCollection<VersionRow> OVersions { get; } = [];

	#endregion
	#region Selection (pushed in by the view)

	/// <summary>The directory selected in the tree.</summary>
	public DirectoryRef? SelectedDirectory
	{
		get => _state.SelectedDirectory;
		set
		{
			_state.SelectedDirectory = value;
			LastSelectedNodePath = value?.TreePath;
		}
	}

	/// <summary>Tree path ("root\a\b") of the selected directory, used to restore selection after a reload.</summary>
	public string? LastSelectedNodePath
	{
		get => _state.SelectedPath;
		set => _state.SelectedPath = value;
	}

	/// <summary>Rows selected in the entry list.</summary>
	public IReadOnlyList<EntryRow> SelectedEntries
	{
		get;
		set
		{
			field = value;
			// the list empties its selection while it is refilled; that is not the user's choice
			if( IsListLoaded && !IsDisposed )
				_state.SelectedEntryNames = [ .. value.Select( r => r.Name ).OfType<string>() ];
		}
	} = [];

	/// <summary>The rows to reselect after the folder's rows are listed again (the selection the page was left with); asked once.</summary>
	/// <returns>The rows of <see cref="Entries"/> whose names were selected; empty after the first call or when another folder is listed.</returns>
	public IReadOnlyList<EntryRow> TakeSelectionToRestore()
	{
		IReadOnlyList<string> names = _selectionToRestore;
		_selectionToRestore = [];
		// only in the folder they were selected in (a locate request may have opened another one)
		if( names.Count == 0 || !string.Equals( LastSelectedNodePath, _selectionFolder, StringComparison.OrdinalIgnoreCase ) )
			return [];
		return [ .. Entries.Where( r => r.Name is { } name && names.Contains( name ) ) ];
	}

	/// <summary>Tree paths of the folders to expand when the tree is rebuilt.</summary>
	public IReadOnlyCollection<string> ExpandedPaths => _state.ExpandedPaths;

	/// <summary>The view reports a folder being expanded or collapsed.</summary>
	/// <param name="treePath">The folder's tree path ("root\a\b"); null or empty is ignored.</param>
	/// <param name="expanded">Whether the folder is now expanded.</param>
	public void SetExpanded( string? treePath, bool expanded )
	{
		if( treePath is null or "" || IsDisposed )
			return;
		if( expanded )
			_state.ExpandedPaths.Add( treePath );
		else
			_state.ExpandedPaths.Remove( treePath );
	}

	/// <summary>The kept sort of the entry list (field, descending), for the column header arrow.</summary>
	public (string Field, bool Descending)? EntrySort => _state.Sort;

	/// <summary>Version selected in the History tab.</summary>
	public HistoryRow? SelectedHistory { get; set; }

	/// <summary>Name of the visible detail tab (one of the StorageBox *_TAB constants).</summary>
	public string? SelectedDetailTab
	{
		get => _state.SelectedDetailTab;
		set => _state.SelectedDetailTab = value;
	}

	private string SelectedLocalDirectory => _files.ResolveLocalDirectory( LastSelectedNodePath );
	private int SelectedDirectoryId => SelectedDirectory?.DirectoryId ?? 0;

	private static int[] RemoteIds( IEnumerable<EntryRow> rows ) => RemoteIds( rows.Select( r => r.Id ) );

	#endregion
	/// <summary>Loads the Odoo metadata the page needs before the tree is built.</summary>
	/// <remarks>Blocks on server calls the first time; call it off the UI thread.</remarks>
	public void Initialize() => _workflow.PreloadMetadata();

	#region Loading the tree and the entry list

	/// <summary>
	/// Raises <see cref="TreeLoaded"/> with the directory tree: the one kept from an earlier visit when there
	/// is one and nothing marked it stale, otherwise (or with <paramref name="fromServer"/>) a fresh one.
	/// </summary>
	/// <param name="fromServer">Always fetch the tree from the server, ignoring the kept one.</param>
	/// <remarks>Sets <see cref="IsTreeLoading"/> while it runs and <see cref="IsTreeLoaded"/> when done, even if loading failed.
	/// A fresh tree is stored in <see cref="FileManagerState"/>.</remarks>
	public async Task LoadDirectoryTreeAsync( bool fromServer = false )
	{
		IsTreeLoaded = false;
		IsTreeLoading = true;
		try
		{
			DirectoryNode? root = !fromServer && !_state.Stale.HasFlag( FileManagerRefresh.Tree ) && _state.Tree is { } kept
				? kept
				: await _directoryTree.LoadAsync();
			if( root is not null && !ReferenceEquals( root, _state.Tree ) )
				_state.TreeRefreshed( root );
			if( root is not null && !IsDisposed && TreeLoaded is { } handler )
				await handler( root );
		}
		finally
		{
			IsTreeLoading = false;
			IsTreeLoaded = true;
		}
	}

	/// <summary>
	/// Loads the rows of <see cref="SelectedDirectory"/> into <see cref="Entries"/>: the rows kept from an
	/// earlier visit when they are for this folder and not stale, otherwise fresh ones (which are then kept).
	/// </summary>
	/// <param name="token">Cancels the load; it is also cancelled when the page is left.</param>
	/// <remarks>
	/// Waits for <see cref="IsTreeLoaded"/> first, then applies the kept sort and raises <see cref="GroupingChanged"/>.
	/// Sets <see cref="IsListLoaded"/> when done unless cancelled. Load errors are written to standard error, not thrown.
	/// </remarks>
	public async Task LoadEntriesAsync( CancellationToken token = default )
	{
		IsListLoaded = false;
		using var linked = CancellationTokenSource.CreateLinkedTokenSource( token, _pageCancellation.Token );
		token = linked.Token;
		try
		{
			await AsyncHelper.WaitUntil( () => IsTreeLoaded, 100, -1, token );
			Entries.Clear();
			if( SelectedDirectory is not { } directory )
				return;

			bool includeDeleted = ShowInactive;
			if( !_state.TryGetEntries( directory.TreePath, includeDeleted, out EntryListResult result ) )
			{
				result = await _entryList.LoadAsync( directory.DirectoryId, directory.TreePath, includeDeleted, IsFiltered, token );
				token.ThrowIfCancellationRequested();
				_state.RememberEntries( directory.TreePath, includeDeleted, result );
			}

			UpdateEntryLabels( result );
			Entries.ReplaceAll( result.Rows );
			if( _state.Sort is var (field, descending) && EntryRowKeys.TryGetSortKey( field, out var sortKey ) )
				Entries.Sort( sortKey, descending );
			GroupingChanged?.Invoke();
		}
		catch( OperationCanceledException ) { }
		catch( Exception e )
		{
			// stderr too: Debug output is invisible outside a debugger (e.g. the smoke-test log)
			Console.Error.WriteLine( $"loading entries failed: {e}" );
		}
		finally
		{
			// RestartEntries, SearchOdoo and the smoke tests wait on this flag
			if( !linked.IsCancellationRequested )
				IsListLoaded = true;
		}
	}

	private void UpdateEntryLabels( EntryListResult result )
	{
		static string Count( int count ) => count == 0 ? "None" : count.ToString();
		EntriesLabel = $"Entries: {Count( result.RemoteCount + result.LocalOnlyCount )}";
		EntriesLabelLocal = $"LO: {Count( result.LocalOnlyCount )}";
		EntriesLabelRemote = $"RO: {Count( result.RemoteCount - result.LocalAndRemoteCount )}";
	}

	#endregion

	#region Entry details and preview

	/// <summary>Empties every detail tab (history, parents, children, properties, info).</summary>
	public void ClearDetails()
	{
		OHistories.Clear();
		OParents.Clear();
		OChildren.Clear();
		OProperties.Clear();
		OVersions.Clear();
	}

	/// <summary>Loads the visible detail tab and the preview for <paramref name="entry"/>; a newer call cancels an older one.</summary>
	/// <param name="entry">The selected row; null just clears the details and the preview.</param>
	/// <remarks>
	/// Only the tab named by <see cref="SelectedDetailTab"/> is filled. The preview is the server's image of the latest version,
	/// else the preview stored in the local SolidWorks file. Errors go to the debug output.
	/// </remarks>
	public async Task ShowEntryDetailsAsync( EntryRow? entry )
	{
		await _detailsCancellation.CancelAsync();
		_detailsCancellation = new();
		CancellationToken token = _detailsCancellation.Token;

		ClearDetails();
		Preview = null;
		if( entry is null )
			return;

		try
		{
			await LoadDetailTabAsync( entry, token );

			byte[]? image = entry.LatestId is int versionId ? await _details.GetPreviewImageAsync( versionId ) : null;
			// a local-only file, or a version committed without a preview: use the local file's own preview
			string? localPath = entry.LocalFile is { Exists: true } local ? local.FullName : null;
			if( image is not { Length: > 0 } && localPath is not null )
				image = await _details.GetLocalPreviewImageAsync( localPath );

			EntryPreview? preview = image is { Length: > 0 }
				? new EntryPreview( image, null )
				: entry.Status is FileStatus.Lo
					? new EntryPreview( null, localPath )
					: null;

			if( !token.IsCancellationRequested && preview is not null )
				Preview = preview;
		}
		catch( OperationCanceledException ) { }
		catch( Exception e )
		{
			System.Diagnostics.Debug.WriteLine( $"loading {SelectedDetailTab} for {entry.Name} failed: {e}" );
		}
	}

	private async Task LoadDetailTabAsync( EntryRow entry, CancellationToken token )
	{
		if( SelectedDetailTab is { } tab && _detailTabs.TryGetValue( tab, out var fill ) )
			await fill( entry );
		token.ThrowIfCancellationRequested();
	}

	/// <summary>Shows the preview image of a specific version (history/parent/child row selection).</summary>
	/// <param name="versionId">The Odoo version id; null or 0 does nothing.</param>
	/// <remarks>The current preview is kept when the version has no image.</remarks>
	public async Task PreviewVersionAsync( int? versionId )
	{
		if( versionId is null or 0 )
			return;
		if( await _details.GetPreviewImageAsync( versionId ) is { Length: > 0 } bytes )
			Preview = new EntryPreview( bytes, null );
	}

	#endregion
	#region Entry list: grouping and sorting

	/// <summary>Regroups the entry list by one of <see cref="EntryRowKeys.Names"/>.</summary>
	[RelayCommand]
	private void GroupBy( string? propertyName )
	{
		if( !EntryRowKeys.TryGetGroupKey( propertyName, out var key ) )
			return;
		_groupKey = key;
		_state.GroupField = propertyName;
		GroupingChanged?.Invoke();
	}

	/// <summary>Sorts the entry list by a field and remembers the sort for later visits.</summary>
	/// <param name="fieldName">One of <see cref="EntryRowKeys.Names"/>; anything else is ignored.</param>
	/// <param name="descending">Whether to sort from high to low.</param>
	/// <remarks>Raises <see cref="GroupingChanged"/> so the view rebinds the grid.</remarks>
	public void SortEntries( string? fieldName, bool descending )
	{
		if( !EntryRowKeys.TryGetSortKey( fieldName, out var key ) )
			return;

		Entries.Sort( key, descending );
		_state.Sort = ( fieldName!, descending );
		GroupingChanged?.Invoke();
	}

	#endregion
	#region Directory (tree) commands

	private async Task<IReadOnlyList<int>> SelectedDirectoryEntryIdsAsync( bool withSubdirectories, bool withDeleted = false )
		=> await _workflow.GetDirectoryEntryIdsAsync( SelectedDirectoryId, withSubdirectories, withDeleted );

	/// <summary>Runs <paramref name="operation"/> on the selected directory's entries; tells the user when no remote directory is selected.</summary>
	private async Task RunOnDirectoryAsync( string what, bool withSubdirectories, bool withDeleted, Func<IReadOnlyList<int>, Task> operation, string? completedMessage = null )
	{
		if( SelectedDirectory is not { } directory )
		{
			await Dialogs.ShowMessageAsync( "current directory doesn't exist remotely" );
			return;
		}
		await RunOperationAsync( async () =>
		{
			Reporter.Log( StatusMessage.PROCESSING, $"{what} all entries within directory ({directory.Name}, id: {SelectedDirectoryId})" );
			await operation( await SelectedDirectoryEntryIdsAsync( withSubdirectories, withDeleted ) );
		}, completedMessage );
	}

	/// <summary>Downloads the latest of every entry in the selected folder and its subfolders (deleted ones too while they are shown).</summary>
	[RelayCommand]
	private Task GetLatestDirectoryAsync() => RunOnDirectoryAsync( "Getting the latest of", true, ShowInactive, _workflow.GetLatestAsync );

	/// <summary>Downloads the latest of every entry directly in the selected folder (deleted ones too while they are shown).</summary>
	[RelayCommand]
	private Task GetLatestDirectoryTopAsync() => RunOnDirectoryAsync( "Getting the latest of", false, ShowInactive, _workflow.GetLatestAsync );

	/// <summary>Downloads the latest of every entry in the selected folder and its subfolders, then checks them out.</summary>
	[RelayCommand]
	private Task CheckoutDirectoryAsync() => RunOnDirectoryAsync( "Checking out", true, false, _workflow.CheckoutLatestAsync );

	/// <summary>Releases this user's checkout of every entry in the selected folder and its subfolders.</summary>
	[RelayCommand]
	private Task UndoCheckoutDirectoryAsync() => RunOnDirectoryAsync( "Undoing the check out of", true, false, _workflow.UnCheckoutAsync );

	/// <summary>Marks every entry in the selected folder and its subfolders deleted on the server, then reloads the page.</summary>
	[RelayCommand]
	private async Task LogicalDeleteDirectoryAsync()
	{
		await RunOnDirectoryAsync( "Deleting", true, false, _workflow.LogicalDeleteAsync, "Completed!" );
		Refresh( FileManagerRefresh.All );
	}

	/// <summary>Restores the deleted entries directly in the selected folder, then reloads the page.</summary>
	[RelayCommand]
	private async Task RestoreDirectoryAsync()
	{
		await RunOperationAsync( () => _workflow.RestoreDeletedInDirectoryAsync( SelectedDirectoryId ), "Completed!" );
		Refresh( FileManagerRefresh.All );
	}

	/// <summary>Not implemented: tells the user so.</summary>
	[RelayCommand]
	private Task RestoreDirectoryRecursiveAsync() => Dialogs.ShowMessageAsync( "Not Implemented Yet" );

	/// <summary>Commits every file directly in the selected local folder, with the files its SolidWorks documents reference.</summary>
	[RelayCommand]
	private async Task CommitDirectoryAsync()
	{
		if( SelectedDirectory?.IsRemoteOnly is true )
			return;
		await RunOperationAsync( async () =>
		{
			if( await _workflow.CollectFolderCommitFilesAsync( SelectedLocalDirectory ) is { Count: > 0 } files )
				await _workflow.CommitAsync( files );
		} );
	}

	/// <summary>Opens the selected folder in the system file browser.</summary>
	[RelayCommand]
	private void OpenDirectory() => _files.OpenFolder( SelectedLocalDirectory );

	/// <summary>After confirmation, deletes the selected local folder and everything in it (the server is not touched).</summary>
	[RelayCommand]
	private async Task LocalDeleteDirectoryAsync()
	{
		string directory = SelectedLocalDirectory;
		if( _files.CountFiles( directory ) is not int count )
			return;

		if( await Dialogs.ConfirmAsync( $"Are you sure you want to delete this directory and ({count}) files inside?", "Delete Directory", MessageBoxIcon.Warning ) )
			_files.DeleteDirectory( directory );
	}

	#endregion
	#region Entry list commands

	/// <summary>Downloads the latest of the selected entries and everything they depend on.</summary>
	[RelayCommand]
	private Task GetLatestSelectedAsync() => RunOnIdsAsync( RemoteIds( SelectedEntries ), _workflow.GetLatestAsync );

	/// <summary>Downloads the latest of the selected entries that nobody has checked out, then checks them out.</summary>
	[RelayCommand]
	private Task CheckoutSelectedAsync()
		=> RunOnIdsAsync( RemoteIds( SelectedEntries.Where( e => e.Checkout is null ) ), _workflow.CheckoutLatestAsync );

	/// <summary>Releases this user's checkout of the selected checked-out entries.</summary>
	[RelayCommand]
	private Task UndoCheckoutSelectedAsync()
		=> RunOnIdsAsync( RemoteIds( SelectedEntries.Where( e => e.Checkout is not null ) ), _workflow.UnCheckoutAsync );

	/// <summary>Marks the selected entries deleted on the server, then reloads the page.</summary>
	[RelayCommand]
	private async Task LogicalDeleteSelectedAsync()
	{
		await RunOnIdsAsync( RemoteIds( SelectedEntries ), _workflow.LogicalDeleteAsync, "Completed!" );
		Refresh( FileManagerRefresh.All );
	}

	/// <summary>Commits the selected rows' local files, with the files their SolidWorks documents reference.</summary>
	[RelayCommand]
	private Task CommitSelectedAsync() => RunOperationAsync( async () =>
	{
		HashSet<HackFile> files = await _workflow.CollectCommitFilesAsync( SelectedEntries );
		if( files.Count > 0 )
			await _workflow.CommitAsync( files );
	} );

	/// <summary>Opens the selected entries with whichever copy fits their status.</summary>
	[RelayCommand]
	private Task OpenSelectedAsync() => OpenSelectedAsync( EntryOpenMode.Open );

	/// <summary>Downloads the latest version of the selected entries to the temp folder and opens it.</summary>
	[RelayCommand]
	private Task OpenSelectedLatestRemoteAsync() => OpenSelectedAsync( EntryOpenMode.LatestRemote );

	/// <summary>Opens the local copies of the selected entries.</summary>
	[RelayCommand]
	private Task OpenSelectedLatestLocalAsync() => OpenSelectedAsync( EntryOpenMode.LatestLocal );

	private async Task OpenSelectedAsync( EntryOpenMode mode )
	{
		IReadOnlyList<string> errors = await _files.OpenEntriesAsync( SelectedEntries, mode );
		if( errors.Count > 0 )
			await Dialogs.ShowMessageAsync( string.Join( Environment.NewLine, errors ) );
	}

	/// <summary>Opens the folders holding the selected entries' local files.</summary>
	[RelayCommand]
	private void OpenSelectedFolders() => _files.OpenContainingFolders( SelectedEntries );

	/// <summary>After confirmation, deletes the selected rows' local files (the server is not touched), then reloads the list.</summary>
	[RelayCommand]
	private async Task LocalDeleteSelectedAsync()
	{
		List<FileInfo> files = _files.FindLocalFiles( SelectedLocalDirectory, SelectedEntries );
		if( files.Count == 0 )
			return;

		string message = files.Count > 10
			? $"Are you sure you want to delete ({files.Count}) files?"
			: $"Are you sure you want to delete these files?\nfiles:\n{string.Join( Environment.NewLine, files.Select( f => f.FullName ) )}";

		if( await Dialogs.ConfirmAsync( message, "Delete Files", MessageBoxIcon.Warning ) )
			_files.DeleteFiles( files );
		Refresh( FileManagerRefresh.Entries );
	}

	/// <summary>Debug builds only: after confirmation, permanently deletes the selected entries and all their versions from the server.</summary>
	[RelayCommand]
	private async Task PermanentDeleteSelectedAsync()
	{
#if DEBUG
		IReadOnlyList<int> ids = RemoteIds( SelectedEntries );
		if( ids.Count == 0 )
		{
			await Dialogs.ShowMessageAsync( "No entries to delete" );
			return;
		}

		bool confirmed = await Dialogs.ConfirmAsync(
			$"Are you sure you want to permanently delete {ids.Count} entries from the database?\n" +
			"This will also permanently delete all associative versions, version properties, and version relationships",
			"Delete Entries and Other Records?", MessageBoxIcon.Warning );
		if( !confirmed )
			return;

		bool deleted = false;
		await RunOperationAsync( async () => deleted = await _workflow.PermanentDeleteAsync( ids ) );
		if( !deleted )
		{
			await Dialogs.ShowMessageAsync( "Was unable to delete entries", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error );
			return;
		}
		await Dialogs.ShowMessageAsync( "Completed!" );
		Refresh( FileManagerRefresh.All );
#else
		await Dialogs.ShowMessageAsync( "Permanent delete is only available in debug builds" );
#endif
	}

	/// <summary>Not implemented: tells the user so when one eligible row is selected.</summary>
	[RelayCommand]
	private async Task UploadIconAsync()
	{
		if( SelectedEntries is not [ { FullName: not null } entry ] || !FileStatusPolicy.CanUploadIcon( entry.Status ) )
			return;
		await Dialogs.ShowMessageAsync( "Uploading icons is not implemented yet" );
	}

	#endregion
	#region History commands

	/// <summary>Runs <paramref name="act"/> on the version selected in the History tab, when there is one.</summary>
	private async Task WithHistoryVersionAsync( Func<HpVersion, Task> act )
	{
		if( await _files.GetVersionAsync( SelectedHistory?.Version ?? 0 ) is { } version )
			await act( version );
	}

	/// <summary>Downloads the selected history version, asking where to put it if a different local copy exists.</summary>
	[RelayCommand]
	private Task DownloadHistoryAsync() => WithHistoryVersionAsync( async version =>
	{
		if( !_files.HasConflictingLocalCopy( version ) )
		{
			await _files.DownloadVersionAsync( version, toTemp: false );
			return;
		}

		DialogResult answer = await Dialogs.ShowMessageAsync(
			"File exists as a different version.\n" +
			"Retry:\tDownload in the Temporary Folder\n" +
			"Ignore:\tOverwrite the current version\n" +
			"Abort:\tCancel download",
			"File Version Conflict", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning );

		if( answer is DialogResult.Retry or DialogResult.Ignore )
			await _files.DownloadVersionAsync( version, toTemp: answer is DialogResult.Retry );
	} );

	/// <summary>Downloads the selected history version into the temp folder.</summary>
	[RelayCommand] private Task DownloadHistoryToTempAsync() => WithHistoryVersionAsync( version => _files.DownloadVersionAsync( version, toTemp: true ) );
	/// <summary>Downloads the selected history version into the working folder, overwriting the local copy.</summary>
	[RelayCommand] private Task DownloadHistoryOverwriteAsync() => WithHistoryVersionAsync( version => _files.DownloadVersionAsync( version, toTemp: false ) );
	/// <summary>Downloads the selected history version into the temp folder and opens it.</summary>
	[RelayCommand] private Task OpenHistoryFromTempAsync() => WithHistoryVersionAsync( version => OpenHistoryAsync( version, toTemp: true ) );
	/// <summary>Downloads the selected history version into the working folder, overwriting the local copy, and opens it.</summary>
	[RelayCommand] private Task OpenHistoryOverwriteAsync() => WithHistoryVersionAsync( version => OpenHistoryAsync( version, toTemp: false ) );
	/// <summary>After confirmation, moves (or downloads) the selected history version from the working folder to the temp folder.</summary>
	[RelayCommand] private Task MoveHistoryToTempAsync() => WithHistoryVersionAsync( version => MoveHistoryAsync( version, toTemp: true ) );
	/// <summary>After confirmation, moves (or downloads) the selected history version from the temp folder to the working folder.</summary>
	[RelayCommand] private Task MoveHistoryToWorkingAsync() => WithHistoryVersionAsync( version => MoveHistoryAsync( version, toTemp: false ) );

	private async Task OpenHistoryAsync( HpVersion version, bool toTemp )
	{
		string folder = await _files.DownloadVersionAsync( version, toTemp );
		_files.OpenFile( Path.Combine( folder, version.name ?? "" ) );
	}

	private async Task MoveHistoryAsync( HpVersion version, bool toTemp )
	{
		VersionMovePlan plan = _files.PlanMove( version, toTemp );
		string target = toTemp ? "temporary" : "current";
		string prefix = plan.From.Exists ? "" : $"file doesn't exist in {plan.From.DirectoryName}.\n";
		string verb = plan.From.Exists ? "move" : "download";
		string message = plan.To.Exists
			? $"{prefix}Would you like to {verb} this version to {target} and overwrite that version?"
			: $"{prefix}Would you like to {verb} this version to {target}?";
		string caption = ( plan.From.Exists ? "Move" : "Download" ) + ( plan.To.Exists ? " & Overwrite" : "" );

		if( await Dialogs.ConfirmAsync( message, caption, plan.To.Exists ? MessageBoxIcon.Warning : MessageBoxIcon.Question ) )
			await _files.ExecuteMoveAsync( version, plan );
		Refresh( FileManagerRefresh.Entries );
	}

	/// <summary>Downloads the selected history version together with its dependencies.</summary>
	[RelayCommand]
	private async Task DownloadHistoryWithDependenciesAsync()
	{
		if( SelectedHistory is not { Version: not 0 } row )
			return;
		if( await _workflow.PlanVersionDownloadAsync( row.Version ) is not { } plan )
			return;

		HpVersion v = plan.Version;
		string intro = plan.IsLatest
			? "You are trying to download the latest version and dependencies. Continue?"
			: "You are trying to download a previous version and dependencies. Continue?";
		StringBuilder details = new StringBuilder()
			.AppendLine( "version:" )
			.AppendLine( $"\tName = {v.name}" )
			.AppendLine( $"\tID = {v.id}" )
			.AppendLine( $"\tSize = {v.file_size}" )
			.AppendLine( $"\tChecksum = {v.checksum}" )
			.AppendLine( $"\tAttachID = {v.attachment_id}" )
			.AppendLine( $"\tMod Date = {v.file_modify_stamp}" )
			.AppendLine( $"\tNode ID = {v.node_id}" )
			.AppendLine( $"\tDir ID = {v.dir_id}" )
			.Append( $"\tWin DL Path = {v.WinPathway}" );

		string ids = string.Join( ", ", plan.VersionIds );
		if( await Dialogs.ShowMessageAsync( $"{intro}\n this will download version ids: {ids}\n{details}", "Version Download", MessageBoxButtons.YesNoCancel ) != DialogResult.Yes )
			return;

		IReadOnlyList<int> failed = await _workflow.DownloadVersionsAsync( plan.VersionIds );
		if( failed.Count > 0 )
			await Dialogs.ShowMessageAsync( $"failed to download version ids: {string.Join( ", ", failed )}" );
	}

	#endregion
}
