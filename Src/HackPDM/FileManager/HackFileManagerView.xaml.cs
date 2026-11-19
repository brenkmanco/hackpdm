using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Settings;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using WinUI.TableView;

using Image = Microsoft.UI.Xaml.Controls.Image;
using TableView = WinUI.TableView.TableView;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.FileManager;

/// <summary>
/// View for <see cref="HackFileManagerViewModel"/>. Only builds and drives controls: every PDM
/// operation is a view-model command, and the view reacts to the view model's refresh requests.
/// <para>
/// Each visit gets a new page and a new view model (disposed in <see cref="OnNavigatedFrom"/>); what the
/// user was looking at comes back from the session's FileManagerState: the tree with its expanded
/// folders, the selected folder and rows, the open detail tab and the sort.
/// </para>
/// </summary>
public sealed partial class HackFileManagerView : Page
{
	public HackFileManagerViewModel ViewModel { get; } = AppComposition.CreateFileManager();

	private bool _loaded;
	private bool _left;
	/// <summary>The first listing of this visit restores the kept row selection and sort arrow.</summary>
	private bool _restorePending = true;


	/// <summary>Tree node currently selected (the smoke tests read it).</summary>
	public TreeViewNode? LastSelectedNode { get; set; }

	/// <summary>The in-flight directory selection (smoke tests wait on it).</summary>
	public Task? TreeSelectionTask { get; private set; }
	/// <summary>The tree node whose entries are loading or loaded (see OdooDirectoryTree_SelectionChanged).</summary>
	private TreeViewNode? _loadingNode;

	public readonly Dictionary<object, TreeViewNode> ItemToContainerMap = new();

	private readonly IPreviewImageLoader _previewLoader = new PreviewImageLoader();
	private int _previewRequest;

	/// <summary>Folders from the root to the selected one, shown in the breadcrumb bar.</summary>
	public ObservableCollection<TreeData> BreadcrumbPath { get; } = [];
	// TreeView items are built as nodes (see DirectoryTreeBuilder); this source stays empty
	private readonly ObservableCollection<TreeData> _treeItems = [];
	private CancellationTokenSource _treeSelectionCancellation = new();
	private PipWindow? _pipWindow;
	private DirectoryNode? _fullTreeRoot;
	private int _totalTreeFolderCount;
	private bool _isFilteringTree;
	private TreeData? _selectedTreeData;
	private int _copyFeedbackVersion;

	private ColumnDefinition? _foldersCol;
	private ColumnDefinition? _inspectorRightCol;
	private RowDefinition? _inspectorBottomRow;
	private ColumnDefinition? _previewBottomCol;
	private RowDefinition? _previewRightRow;

	private string? _draggingPaneTag;
	private bool _isDraggingGrip;
	private string? _activeDropZone;

	private readonly RangeObservableCollection<object> _groupedItems = [];
	private List<EntryGroupHeader> _groups = [];
	private readonly HashSet<string> _collapsedGroupKeys = new( StringComparer.Ordinal );
	private bool _syncingGroupedSelection;
	private bool _pendingHeaderToggle;
	private bool IsUsingGroupedList => GroupedEntryContainer.Visibility == Visibility.Visible;

	private static readonly SolidColorBrush SyncedDotBrush = new( Windows.UI.Color.FromArgb( 255, 0xC6, 0x97, 0x49 ) );
	private static readonly SolidColorBrush LocalDotBrush = new( Windows.UI.Color.FromArgb( 255, 0x3F, 0xB9, 0x50 ) );
	private static readonly SolidColorBrush RemoteDotBrush = new( Windows.UI.Color.FromArgb( 255, 0x58, 0xB4, 0xFF ) );

	#region Construction and loading

	public HackFileManagerView()
	{
		InitializeComponent();
		ApplyColumnSettings();
		Loaded += ( _, _ ) => LoadHackMan();
	}

	protected override void OnNavigatedTo( NavigationEventArgs e )
	{
		base.OnNavigatedTo( e );
		LoadHackMan();
	}

	/// <summary>The page is left: its view model goes; the session state keeps what it showed.</summary>
	protected override void OnNavigatedFrom( NavigationEventArgs e )
	{
		base.OnNavigatedFrom( e );
		Leave();
	}

	private void Leave()
	{
		if( _left )
			return;
		_left = true;
		_treeSelectionCancellation.Cancel();
		ViewModel.RefreshRequested -= OnRefreshRequested;
		ViewModel.GroupingChanged -= OnGroupingChanged;
		ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
		ViewModel.TreeLoaded -= OnTreeLoadedAsync;
		ViewModel.LocateRequested -= OnLocateRequestedAsync;
		ViewModel.FolderChanged -= OnFolderChanged;
		ViewModel.RowReplaced -= OnRowReplaced;
		AppComposition.Theme.ThemeChanged -= OnThemeChanged;
		ViewModel.Dispose();
		_pipWindow?.Close();
	}

	private void LoadHackMan()
	{
		if( _loaded || _left )
			return;
		_loaded = true;
		ViewModel.Initialize();

		OdooDirectoryTree.ItemsSource = _treeItems;
		OnGroupingChanged();
		// the tab open when the page was last left, else the default one
		if( VersionTabs.TabItems.OfType<TabViewItem>().FirstOrDefault( t => t.Name == ViewModel.SelectedDetailTab ) is { } keptTab )
			VersionTabs.SelectedItem = keptTab;
		else
			ViewModel.SelectedDetailTab = ( VersionTabs.SelectedItem as TabViewItem )?.Name;

		ViewModel.RefreshRequested += OnRefreshRequested;
		ViewModel.GroupingChanged += OnGroupingChanged;
		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		ViewModel.TreeLoaded += OnTreeLoadedAsync;
		ViewModel.LocateRequested += OnLocateRequestedAsync;
		ViewModel.FolderChanged += OnFolderChanged;
		ViewModel.RowReplaced += OnRowReplaced;

		OdooDirectoryTree.SelectionChanged += OdooDirectoryTree_SelectionChanged;
		OdooDirectoryTree.Expanding += ( _, e ) =>
		{
			if( !_isFilteringTree )
				ViewModel.SetExpanded( e.Node?.LinkedData.FullPath, true );
			e.Node?.LinkedData.NotifyBranchChanged();
			TreeBranchGuides.RefreshTree( OdooDirectoryTree, selectedNode: OdooDirectoryTree.SelectedNode, updateSelection: true );
		};
		OdooDirectoryTree.Collapsed += ( _, e ) =>
		{
			if( !_isFilteringTree )
				ViewModel.SetExpanded( e.Node?.LinkedData.FullPath, false );
			e.Node?.LinkedData.NotifyBranchChanged();
			TreeBranchGuides.RefreshTree( OdooDirectoryTree, selectedNode: OdooDirectoryTree.SelectedNode, updateSelection: true );
		};
		OdooDirectoryTree.RightTapped += OdooDirectoryTree_RightTapped;
		OdooDirectoryBreadcrumb.ItemClicked += OdooDirectoryBreadcrumb_ItemClicked;
		BranchLinesToggleButton.IsChecked = AppComposition.Theme.ShowTreeBranchLines;
		AppComposition.Theme.ThemeChanged += OnThemeChanged;
		TreeBranchGuides.RefreshTree( OdooDirectoryTree, showLines: AppComposition.Theme.ShowTreeBranchLines );
		ApplyWorkspaceLayout();
		OdooEntryList.SelectionChanged += OdooEntryList_SelectionChanged;
		OdooEntryList.Sorting += OdooEntryList_Sorting;
		OdooEntryList.DoubleTapped += ( _, _ ) => ViewModel.OpenSelectedCommand.Execute( null );
		OdooEntryList.ContainerContentChanging += EntryTable_ContainerContentChanging;
		HackChangesList.ContainerContentChanging += EntryTable_ContainerContentChanging;
		OdooChangesList.ContainerContentChanging += EntryTable_ContainerContentChanging;
		GroupedEntryList.ItemsSource = _groupedItems;
		GroupedEntryList.SelectionChanged += GroupedEntryList_SelectionChanged;
		GroupedEntryList.ItemClick += GroupedEntryList_ItemClick;
		GroupedEntryList.DoubleTapped += ( _, _ ) =>
		{
			if( ViewModel.SelectedEntries.Count > 0 )
				ViewModel.OpenSelectedCommand.Execute( null );
		};
		GroupedEntryList.RightTapped += GroupedEntryList_RightTapped;
		HackChangesList.DoubleTapped += ( _, _ ) => FocusEntryFromChanges( HackChangesList.SelectedItem as EntryRow );
		OdooChangesList.DoubleTapped += ( _, _ ) => FocusEntryFromChanges( OdooChangesList.SelectedItem as EntryRow );
		OdooHistory.SelectionChanged += OdooHistory_SelectionChanged;
		OdooHistory.DoubleTapped += ( _, _ ) => ViewModel.DownloadHistoryWithDependenciesCommand.Execute( null );
		OdooParents.SelectionChanged += ( _, e ) => PreviewVersion( ( e.AddedItems.FirstOrDefault() as ParentRow )?.Version );
		OdooParents.DoubleTapped += OdooParents_DoubleTapped;
		OdooChildren.SelectionChanged += ( _, e ) => PreviewVersion( ( e.AddedItems.FirstOrDefault() as ChildrenRow )?.Version );
		OdooChildren.DoubleTapped += OdooChildren_DoubleTapped;
		KeyDown += HackFileManagerView_KeyDown;

		// submenu headers can't hold a command, so double-tapping one runs its default action
		EntryOpen.DoubleTapped += ( _, _ ) => ViewModel.OpenSelectedCommand.Execute( null );
		EntryDelete.DoubleTapped += ( _, _ ) => ViewModel.LocalDeleteSelectedCommand.Execute( null );
		TreeDownload.DoubleTapped += ( _, _ ) => ViewModel.GetLatestDirectoryCommand.Execute( null );
		TreeUndelete.DoubleTapped += ( _, _ ) => ViewModel.RestoreDirectoryCommand.Execute( null );
		HistoryDownload.DoubleTapped += ( _, _ ) => ViewModel.DownloadHistoryCommand.Execute( null );
		HistoryMove.DoubleTapped += ( _, _ ) => ViewModel.MoveHistoryToTempCommand.Execute( null );

		OdooDirectoryTree.LostFocus += ( _, _ ) =>
		{
			if( OdooDirectoryTree.SelectedNode is { } node )
				SelectDirectory( node );
		};

		// a search made while this page was not shown waits until the tree is up
		ViewModel.IsTreeLoading = true;
		var pendingLocate = ViewModel.Activate();
		_ = LoadTreeAfterLayoutAsync( pendingLocate );
	}

	private async Task LoadTreeAfterLayoutAsync( (IReadOnlyList<string> Segments, string? FileName)? pendingLocate )
	{
		// Yield briefly so the page entrance transition starts cleanly before building the tree
		await Task.Delay( ViewModel.HasKeptTree ? 16 : 30 );
		if( _left )
			return;
		await ViewModel.LoadDirectoryTreeAsync();
		if( pendingLocate is var (segments, fileName) && !_left )
			await OnLocateRequestedAsync( segments, fileName );
	}

	#endregion
	#region Entry list settings

	/// <summary>
	/// Applies the user's saved list settings: row size and font, and each column's header and width
	/// (columns are matched to their saved settings by <c>SortMemberPath</c>, the <see cref="EntryRow"/> property).
	/// Also fills the Group By menu.
	/// </summary>
	private void ApplyColumnSettings()
	{
		var info = UIStorage.ColumnsInfo;
		double fontSize = Math.Clamp( info.FontSize, 10, 14 );
		double rowHeight = Math.Clamp( info.RowHeight, 24, 48 );
		OdooEntryList.FontSize = fontSize;
		OdooEntryList.RowMinHeight = rowHeight;
		OdooEntryList.RowHeight = rowHeight;
		OdooEntryList.HeaderRowMinHeight = 24;
		OdooEntryList.HeaderRowHeight = 26;
		OdooEntryList.RowDetailsVisibilityMode = info.GroupCollapsed
			? TableViewRowDetailsVisibilityMode.VisibleWhenSelected
			: TableViewRowDetailsVisibilityMode.Collapsed;
#if WINDOWS
		OdooEntryList.DefaultGroupState = info.GroupCollapsed
			? TableViewGroupState.Collapsed
			: TableViewGroupState.Expanded;
#endif

		var defaults = new OdooEntryColumns().Columns;
		foreach( TableViewColumn column in OdooEntryList.Columns )
		{
			if( column.SortMemberPath is not { } property )
				continue;
			// a settings file from before a column existed gets that column's default
			OdooEntryColumn? saved = ( UIStorage.Columns.GetValueOrDefault( property ) ?? defaults.GetValueOrDefault( property ) )?.Column;
			column.Header = saved?.Header ?? property;
			if( saved?.AutoSize is true )
			{
				column.MinWidth = saved.Width > 0 ? saved.Width : 180;
				column.Width = new GridLength( 1, GridUnitType.Star );
			}
			else
			{
				double width = saved?.Width is > 0 ? saved.Width : 100;
				if( property is nameof( EntryRow.LocalDate ) or nameof( EntryRow.RemoteDate ) && width <= 100 )
					width = 155;
				column.Width = new GridLength( width );
			}
		}

		foreach( string field in EntryRowKeys.Names )
		{
			EntryGroupBy.Items.Add( new MenuFlyoutItem { Text = field, Command = ViewModel.GroupByCommand, CommandParameter = field } );
			GroupByToolbarFlyout.Items.Add( new MenuFlyoutItem { Text = field, Command = ViewModel.GroupByCommand, CommandParameter = field } );
		}
	}

	#endregion
	#region Reacting to the view model

	private void OnRefreshRequested( FileManagerRefresh what )
	{
		if( what.HasFlag( FileManagerRefresh.Tree ) )
			_ = ViewModel.LoadDirectoryTreeAsync();
		if( what.HasFlag( FileManagerRefresh.Entries ) )
			_ = ReloadEntriesAsync();
	}

	/// <summary>Rebuilds the tree nodes and reselects the directory that was selected before.</summary>
	private Task OnTreeLoadedAsync( DirectoryNode root )
	{
		_fullTreeRoot = root;
		_totalTreeFolderCount = root.CountRecursive();
		RebuildDisplayedTree( FolderFilterBox.Text );
		return Task.CompletedTask;
	}

	private void RebuildDisplayedTree( string? filterText )
	{
		if( _fullTreeRoot is null )
			return;

		string query = filterText?.Trim() ?? "";
		_isFilteringTree = query.Length > 0;
		DirectoryNode? displayRoot = _isFilteringTree ? _fullTreeRoot.Filter( query ) : _fullTreeRoot;

		if( displayRoot is null )
		{
			OdooDirectoryTree.RootNodes.Clear();
			EmptyTreeFilterPanel.Visibility = Visibility.Visible;
			TreeSummaryText.Text = $"0 / {_totalTreeFolderCount} folders matching \"{query}\"";
			return;
		}

		EmptyTreeFilterPanel.Visibility = Visibility.Collapsed;
		DirectoryTreeBuilder.Build( OdooDirectoryTree, displayRoot );

		if( _isFilteringTree )
		{
			OdooDirectoryTree.SetAllExpanded( true );
		}
		else if( ViewModel.ExpandedPaths.Count == 0 && OdooDirectoryTree.RootNodes.FirstOrDefault() is { } firstRoot )
		{
			firstRoot.LinkedData.IsExpanded = true;
			ViewModel.SetExpanded( firstRoot.LinkedData.FullPath, true );
		}
		else
		{
			// the folders that were open stay open
			foreach( string path in ViewModel.ExpandedPaths.ToList() )
				if( OdooDirectoryTree.FindTreeNode( path ) is { Node: { } open } )
					open.LinkedData.IsExpanded = true;
		}

		if( OdooDirectoryTree.FindTreeNode( ViewModel.LastSelectedNodePath ?? "" ) is { Node: { } node } data )
		{
			data.EnsureVisible( OdooDirectoryTree );
			OdooDirectoryTree.SelectedNode = node;
			SelectDirectory( node );
		}
		else if( !_isFilteringTree )
		{
			TreeBranchGuides.RefreshTree( OdooDirectoryTree, showLines: AppComposition.Theme.ShowTreeBranchLines, selectedNode: OdooDirectoryTree.SelectedNode, updateSelection: true );
		}

		int shownCount = displayRoot.CountRecursive();
		TreeSummaryText.Text = _isFilteringTree
			? $"{shownCount} / {_totalTreeFolderCount} folders matching \"{query}\""
			: $"{_totalTreeFolderCount} folder{(_totalTreeFolderCount == 1 ? "" : "s")}";
	}

	private async Task ReloadEntriesAsync()
	{
		if( OdooDirectoryTree.SelectedNode is null )
			return;

		await LoadSelectedEntriesAsync();
		if( IsUsingGroupedList )
		{
			if( GroupedEntryList.SelectedItems.OfType<EntryRow>().FirstOrDefault() is { } groupedEntry )
				GroupedEntryList.ScrollIntoView( groupedEntry );
		}
		else if( OdooEntryList.SelectedItems is { Count: > 0 } items && items[ 0 ] is EntryRow entry )
			OdooEntryList.ScrollIntoView( entry );
	}

	private void OnGroupingChanged()
	{
		EntryListGrouping.Apply( OdooEntryList, ViewModel.Entries, ViewModel.ActiveGroupKey );
#if !WINDOWS
		bool showGrouped = ViewModel.ActiveGroupKey is not null;
		OdooEntryList.Visibility = showGrouped ? Visibility.Collapsed : Visibility.Visible;
		GroupedEntryContainer.Visibility = showGrouped ? Visibility.Visible : Visibility.Collapsed;
		if( showGrouped )
		{
			GroupedByLabel.Text = $"Grouped by {ViewModel.ActiveGroupFieldName}";
			RebuildGroupedList();
		}
#endif
	}

	private void RebuildGroupedList()
	{
		if( ViewModel.ActiveGroupKey is not { } groupKey )
		{
			_groups.Clear();
			_groupedItems.Clear();
			return;
		}
		_groups = EntryListGrouping.BuildGroups(
			ViewModel.Entries,
			groupKey,
			_collapsedGroupKeys.Count > 0 ? _collapsedGroupKeys : null,
			UIStorage.ColumnsInfo.GroupCollapsed );
		RefreshGroupedFlatList();
	}

	private void RefreshGroupedFlatList( IReadOnlyCollection<EntryRow>? selectionToKeep = null )
	{
		HashSet<EntryRow> previousSelection = [ .. ( selectionToKeep ?? ViewModel.SelectedEntries ) ];
		_syncingGroupedSelection = true;
		try
		{
			_groupedItems.ReplaceAll( EntryListGrouping.FlattenVisible( _groups ) );
			GroupedEntryList.SelectedItems.Clear();
			List<EntryRow> restored = [];
			if( previousSelection.Count > 0 )
			{
				foreach( object item in _groupedItems )
				{
					if( item is EntryRow row && previousSelection.Contains( row ) )
					{
						GroupedEntryList.SelectedItems.Add( row );
						restored.Add( row );
					}
				}
			}
			ViewModel.SelectedEntries = restored;
		}
		finally
		{
			_syncingGroupedSelection = false;
		}
	}

	private void GroupedEntryList_ItemClick( object sender, ItemClickEventArgs e )
	{
		if( e.ClickedItem is not EntryGroupHeader header )
			return;

		// Defer rebuilding _groupedItems until after ListViewBase.OnItemClicked finishes its
		// own ExtendedSelectionCase pass, and keep the user's current row selection intact.
		List<EntryRow> keptSelection = [ .. ViewModel.SelectedEntries ];
		_pendingHeaderToggle = true;
		void ApplyToggle()
		{
			_pendingHeaderToggle = false;
			if( _left )
				return;
			header.IsExpanded = !header.IsExpanded;
			if( header.IsExpanded )
				_collapsedGroupKeys.Remove( header.DisplayKey );
			else
				_collapsedGroupKeys.Add( header.DisplayKey );
			RefreshGroupedFlatList( keptSelection );
		}

		if( DispatcherQueue is { } queue )
			queue.TryEnqueue( ApplyToggle );
		else
			ApplyToggle();
	}

	private void GroupedEntryList_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( _syncingGroupedSelection || _pendingHeaderToggle )
			return;
		List<EntryGroupHeader> selectedHeaders = [ .. GroupedEntryList.SelectedItems.OfType<EntryGroupHeader>() ];
		if( selectedHeaders.Count > 0 )
		{
			_syncingGroupedSelection = true;
			try
			{
				foreach( EntryGroupHeader header in selectedHeaders )
					GroupedEntryList.SelectedItems.Remove( header );
			}
			finally
			{
				_syncingGroupedSelection = false;
			}
		}
		ViewModel.SelectedEntries = [ .. GroupedEntryList.SelectedItems.OfType<EntryRow>() ];
		if( ViewModel.SelectedEntries is [ var entry ] )
			_ = ViewModel.ShowEntryDetailsAsync( entry );
	}

	private void GroupedEntryList_RightTapped( object sender, RightTappedRoutedEventArgs e )
	{
		if( FindGroupedEntryRow( e.OriginalSource as DependencyObject ) is { } row
			&& !GroupedEntryList.SelectedItems.Contains( row ) )
		{
			GroupedEntryList.SelectedItems.Clear();
			GroupedEntryList.SelectedItems.Add( row );
			ViewModel.SelectedEntries = [ .. GroupedEntryList.SelectedItems.OfType<EntryRow>() ];
		}
		OdooEntryList.ContextFlyout?.ShowAt( GroupedEntryList, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
		{
			Position = e.GetPosition( GroupedEntryList ),
		} );
	}

	private EntryRow? FindGroupedEntryRow( DependencyObject? source )
	{
		DependencyObject? current = source;
		while( current is not null && !ReferenceEquals( current, GroupedEntryList ) )
		{
			if( current is ListViewItem container )
				return GroupedEntryList.ItemFromContainer( container ) as EntryRow;
			if( current is FrameworkElement { DataContext: EntryRow row } )
				return row;
			current = VisualTreeHelper.GetParent( current );
		}
		return null;
	}

	private void ExpandAllGroups_Click( object sender, RoutedEventArgs e )
	{
		_collapsedGroupKeys.Clear();
		foreach( EntryGroupHeader group in _groups )
			group.IsExpanded = true;
		RefreshGroupedFlatList();
	}

	private void CollapseAllGroups_Click( object sender, RoutedEventArgs e )
	{
		foreach( EntryGroupHeader group in _groups )
		{
			group.IsExpanded = false;
			_collapsedGroupKeys.Add( group.DisplayKey );
		}
		RefreshGroupedFlatList();
	}

	private void GroupedSortColumn_Click( object sender, RoutedEventArgs e )
	{
		if( sender is not FrameworkElement { Tag: string field } )
			return;
		bool descending = ViewModel.EntrySort is var (currentField, currentDesc)
			&& string.Equals( currentField, field, StringComparison.Ordinal )
			&& !currentDesc;
		ViewModel.SortEntries( field, descending );
	}

	private void SelectEntryRows( IReadOnlyList<EntryRow> rows, bool focus = false, bool scroll = true )
	{
		if( rows.Count == 0 )
			return;
		if( IsUsingGroupedList )
		{
			bool expandedAny = false;
			foreach( EntryGroupHeader group in _groups )
			{
				if( !group.IsExpanded && group.Items.Any( rows.Contains ) )
				{
					group.IsExpanded = true;
					_collapsedGroupKeys.Remove( group.DisplayKey );
					expandedAny = true;
				}
			}
			if( expandedAny )
				RefreshGroupedFlatList();

			GroupedEntryList.SelectedItems.Clear();
			foreach( EntryRow row in rows )
				if( _groupedItems.Contains( row ) )
					GroupedEntryList.SelectedItems.Add( row );
			if( focus )
				GroupedEntryList.Focus( FocusState.Programmatic );
			if( scroll )
				GroupedEntryList.ScrollIntoView( rows[ 0 ] );
			return;
		}
		OdooEntryList.SelectedItems.Clear();
		if( rows.Count == 1 )
			OdooEntryList.SelectedItem = rows[ 0 ];
		else
			foreach( EntryRow row in rows )
				OdooEntryList.SelectedItems.Add( row );
		if( focus )
			OdooEntryList.Focus( FocusState.Programmatic );
		if( scroll )
			OdooEntryList.ScrollIntoView( rows[ 0 ] );
	}

	private void EntryTable_ContainerContentChanging( ListViewBase sender, ContainerContentChangingEventArgs args )
	{
		if( args.ItemContainer is not Control container )
			return;
		if( args.InRecycleQueue )
		{
			container.ClearValue( Control.BackgroundProperty );
			return;
		}
		if( args.Item is EntryRow entry && StatusBrushes.ForTableRow( entry.Status ) is { } brush )
			container.Background = brush;
		else
			container.ClearValue( Control.BackgroundProperty );
	}

	private void OnViewModelPropertyChanged( object? sender, PropertyChangedEventArgs e )
	{
		if( e.PropertyName == nameof( HackFileManagerViewModel.Preview ) )
			_ = ShowPreviewAsync( ViewModel.Preview );
	}

	/// <summary>Loads the preview image; a newer preview replaces one still loading.</summary>
	private async Task ShowPreviewAsync( EntryPreview? preview )
	{
		int request = ++_previewRequest;
		ImageSource? image = null;
		try
		{
			image = preview switch
			{
				{ ImageBytes: { Length: > 0 } bytes } => await _previewLoader.FromBytesAsync( bytes ),
				{ LocalFilePath: { } path } => await _previewLoader.FromFileIconAsync( path ),
				_ => null,
			};
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Preview failed to load: {ex.Message}", ex );
		}
		if( request == _previewRequest )
		{
			OdooEntryImage.Source = image;
			NoPreviewPanel.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
			SyncPipPreview();
		}
	}

	private void SyncPipPreview()
	{
		if( _pipWindow is null )
			return;
		string title = ViewModel.SelectedEntries is [ var single ] && !string.IsNullOrWhiteSpace( single.Name )
			? $"Preview — {single.Name}"
			: "Preview";
		_pipWindow.SetPreview( OdooEntryImage.Source, ViewModel.PreviewStatusText, title );
	}

	/// <summary>A row was added, deleted, or rebuilt from its changed file: updates grouped view and keeps selection intact.</summary>
	private void OnRowReplaced( EntryRow? old, EntryRow? row )
	{
		if( IsUsingGroupedList )
			RebuildGroupedList();

		if( old is null )
			return;

		var selected = ViewModel.SelectedEntries;
		if( selected is not { Count: > 0 } || !selected.Contains( old ) )
			return;

		if( row is null )
		{
			// A selected file was removed: refresh selection from remaining rows
			List<EntryRow> remaining = [ .. selected.Where( r => !ReferenceEquals( r, old ) && ViewModel.Entries.Contains( r ) ) ];
			ViewModel.SelectedEntries = remaining;
			if( remaining.Count == 0 )
				_ = ViewModel.ShowEntryDetailsAsync( null );
			else
				SelectEntryRows( remaining, scroll: false );
			return;
		}

		List<EntryRow> updated = [ .. selected.Select( r => ReferenceEquals( r, old ) ? row : r ).Where( ViewModel.Entries.Contains ) ];
		SelectEntryRows( updated.Count > 0 ? updated : [ row ], scroll: false );
	}

	#endregion
	#region Live changes in other folders (tree badges)

	private const string ChangeBadgeTag = "change-badge";

	/// <summary>
	/// Shows "( + ) created", "( . ) updated" or "( - ) deleted" next to the folder for a few seconds. A folder
	/// that isn't visible (inside a collapsed one, or not in the tree yet) badges its nearest visible parent.
	/// </summary>
	private void OnFolderChanged( string treePath, LocalChangeKind kind )
	{
		if( VisibleNodeFor( treePath ) is not { } node
			|| OdooDirectoryTree.ContainerFromNode( node ) is not TreeViewItem { Content: Panel panel } )
			return;

		RemoveBadges( panel );
		var (text, color) = kind switch
		{
			LocalChangeKind.Created => ( "( + ) created", Windows.UI.Color.FromArgb( 255, 0x2E, 0x9E, 0x44 ) ),
			LocalChangeKind.Deleted => ( "( - ) deleted", Windows.UI.Color.FromArgb( 255, 0xD1, 0x34, 0x38 ) ),
			_ => ( "( . ) updated", Windows.UI.Color.FromArgb( 255, 0xC7, 0x84, 0x00 ) ),
		};
		TextBlock badge = new()
		{
			Text = text,
			Tag = ChangeBadgeTag,
			FontSize = Math.Max( 9, UIStorage.ColumnsInfo.FontSize - 1 ),
			Margin = new Thickness( 8, 0, 0, 0 ),
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = new SolidColorBrush( color ),
			Opacity = 0,
		};
		panel.Children.Add( badge );

		// fade in, hold, fade out, then go
		var fade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
		fade.KeyFrames.Add( new Microsoft.UI.Xaml.Media.Animation.LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds( 200 ), Value = 1 } );
		fade.KeyFrames.Add( new Microsoft.UI.Xaml.Media.Animation.LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds( 2400 ), Value = 1 } );
		fade.KeyFrames.Add( new Microsoft.UI.Xaml.Media.Animation.LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds( 3200 ), Value = 0 } );
		Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget( fade, badge );
		Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty( fade, nameof( Opacity ) );
		var story = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
		story.Children.Add( fade );
		story.Completed += ( _, _ ) => panel.Children.Remove( badge );
		story.Begin();
		BadgesShown++;
	}

	/// <summary>How many change badges this page has shown (the smoke tests read it).</summary>
	internal int BadgesShown { get; private set; }

	/// <summary>Switches between the Entries and Changes tabs (for smoke visual checks).</summary>
	internal void ShowTopTab( bool changes ) => ListTabs.SelectedItem = changes ? ChangesTab : RootEntryList;

	/// <summary>The node of <paramref name="treePath"/>, or of its nearest parent that is in the tree and not inside a collapsed folder.</summary>
	private TreeViewNode? VisibleNodeFor( string treePath )
	{
		if( OdooDirectoryTree.RootNodes.FirstOrDefault() is not { } node )
			return null;
		string[] parts = PwaPaths.Segments( treePath );
		foreach( string part in parts )
		{
			if( !node.IsExpanded )
				break; // its children are not shown
			TreeViewNode? next = node.Children.FirstOrDefault( n => string.Equals( n.LinkedData.Name, part, StringComparison.OrdinalIgnoreCase ) );
			if( next is null )
				break;
			node = next;
		}
		return node;
	}

	private static void RemoveBadges( Panel panel )
	{
		foreach( var old in panel.Children.OfType<TextBlock>().Where( t => ChangeBadgeTag.Equals( t.Tag ) ).ToList() )
			panel.Children.Remove( old );
	}

	#endregion
	#region Directory tree

	private void SelectDirectory( TreeViewNode? node )
	{
		LastSelectedNode = node;
		if( _selectedTreeData is not null && !ReferenceEquals( _selectedTreeData.Node, node ) )
			_selectedTreeData.IsSelectedInTree = false;
		_selectedTreeData = node?.LinkedData;
		if( _selectedTreeData is not null )
			_selectedTreeData.IsSelectedInTree = true;

		ViewModel.SelectedDirectory = node?.LinkedData is { } data
			? new DirectoryRef( data.Name ?? "", data.DirectoryId ?? 0, data.FullPath ?? data.Name ?? "", data.IsRemoteOnly is true )
			: null;
		node?.UpdateBreadCrumbCollection( BreadcrumbPath );
		UpdateBreadcrumbBarControls( node );
		TreeBranchGuides.RefreshTree( OdooDirectoryTree, showLines: AppComposition.Theme.ShowTreeBranchLines, selectedNode: node, updateSelection: true );
	}

	private void UpdateBreadcrumbBarControls( TreeViewNode? node )
	{
		NavigateUpButton.IsEnabled = node?.Parent is { Depth: >= 0 };
		CopyPathButton.IsEnabled = node is not null;

		int childCount = node?.Children.Count ?? 0;
		if( childCount > 0 )
		{
			SubfolderMenuLabel.Text = $"{childCount} subfolder{(childCount == 1 ? "" : "s")}";
			SubfolderMenuButton.Visibility = Visibility.Visible;
		}
		else
		{
			SubfolderMenuButton.Visibility = Visibility.Collapsed;
		}

		if( node?.LinkedData is { } data )
		{
			CurrentFolderOriginText.Text = data.OriginLabel;
			CurrentFolderOriginDot.Fill = data.Origin switch
			{
				FolderOriginKind.LocalOnly => LocalDotBrush,
				FolderOriginKind.RemoteOnly => RemoteDotBrush,
				_ => SyncedDotBrush,
			};
			CurrentFolderOriginPill.Visibility = Visibility.Visible;
		}
		else
		{
			CurrentFolderOriginPill.Visibility = Visibility.Collapsed;
		}
	}

	private void OnThemeChanged()
	{
		bool show = AppComposition.Theme.ShowTreeBranchLines;
		if( BranchLinesToggleButton.IsChecked != show )
			BranchLinesToggleButton.IsChecked = show;
		TreeBranchGuides.RefreshTree( OdooDirectoryTree, showLines: show, selectedNode: LastSelectedNode, updateSelection: true );
	}

	private void BranchLinesToggle_Click( object sender, RoutedEventArgs e )
	{
		bool show = BranchLinesToggleButton.IsChecked == true;
		AppComposition.Theme.SetShowTreeBranchLines( show );
		TreeBranchGuides.RefreshTree( OdooDirectoryTree, showLines: show, selectedNode: LastSelectedNode, updateSelection: true );
	}

	private void ExpandAllFolders_Click( object sender, RoutedEventArgs e )
	{
		OdooDirectoryTree.SetAllExpanded( true );
		if( !_isFilteringTree )
			RecordExpandedState();
		TreeBranchGuides.RefreshTree( OdooDirectoryTree, selectedNode: LastSelectedNode, updateSelection: true );
	}

	private void CollapseAllFolders_Click( object sender, RoutedEventArgs e )
	{
		OdooDirectoryTree.SetAllExpanded( false );
		if( !_isFilteringTree )
			RecordExpandedState();
		TreeBranchGuides.RefreshTree( OdooDirectoryTree, selectedNode: LastSelectedNode, updateSelection: true );
	}

	private void RecordExpandedState()
	{
		foreach( TreeViewNode root in OdooDirectoryTree.RootNodes )
			RecordExpandedRecursive( root );
	}

	private void RecordExpandedRecursive( TreeViewNode node )
	{
		ViewModel.SetExpanded( node.LinkedData.FullPath, node.IsExpanded );
		foreach( TreeViewNode child in node.Children )
			RecordExpandedRecursive( child );
	}

	private void FolderFilterBox_TextChanged( object sender, TextChangedEventArgs e )
	{
		ClearFilterButton.Visibility = string.IsNullOrWhiteSpace( FolderFilterBox.Text )
			? Visibility.Collapsed
			: Visibility.Visible;
		RebuildDisplayedTree( FolderFilterBox.Text );
	}

	private void ClearFilterButton_Click( object sender, RoutedEventArgs e )
	{
		FolderFilterBox.Text = string.Empty;
		ClearFilterButton.Visibility = Visibility.Collapsed;
	}

	private void NavigateUp_Click( object sender, RoutedEventArgs e )
	{
		if( LastSelectedNode?.Parent is { Depth: >= 0 } parent )
			JumpToTreeNode( parent );
	}

	private void SubfolderMenuButton_Click( object sender, RoutedEventArgs e )
	{
		if( LastSelectedNode is not { Children.Count: > 0 } node )
			return;

		var flyout = new MenuFlyout();
		foreach( TreeViewNode child in node.Children )
		{
			TreeData data = child.LinkedData;
			string suffix = data.Origin == FolderOriginKind.Synced ? "" : $"  [{data.OriginLabel}]";
			var item = new MenuFlyoutItem
			{
				Text = $"{data.Name}{suffix}",
				Icon = new FontIcon { Glyph = "\uE8B7" },
			};
			TreeViewNode captured = child;
			item.Click += ( _, _ ) => JumpToTreeNode( captured );
			flyout.Items.Add( item );
		}
		flyout.ShowAt( SubfolderMenuButton );
	}

	private async void CopyPath_Click( object sender, RoutedEventArgs e )
	{
		string? path = ViewModel.LastSelectedNodePath ?? LastSelectedNode?.LinkedData.FullPath;
		if( string.IsNullOrWhiteSpace( path ) )
			return;
		try
		{
			var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
			package.SetText( path );
			Windows.ApplicationModel.DataTransfer.Clipboard.SetContent( package );
		}
		catch
		{
			// Headless environments without a clipboard service still show feedback without crashing
		}

		int version = ++_copyFeedbackVersion;
		CopyPathIcon.Glyph = "\uE8FB";
		CopyPathLabel.Text = "Copied";
		await Task.Delay( 1400 );
		if( _left || version != _copyFeedbackVersion )
			return;
		CopyPathIcon.Glyph = "\uE8C8";
		CopyPathLabel.Text = "Copy path";
	}

	private void JumpToTreeNode( TreeViewNode node )
	{
		node.LinkedData.EnsureVisible( OdooDirectoryTree );
		OdooDirectoryTree.SelectedNode = node;
		SelectDirectory( node );
	}

	private async void OdooDirectoryTree_SelectionChanged( TreeView sender, TreeViewSelectionChangedEventArgs args )
	{
		if( args.AddedItems.FirstOrDefault() is not TreeData data || data.Node is null )
			return;
		// EnsureVisible (below, and in the locate/refresh paths) sets SelectedNode, which raises
		// SelectionChanged again for the same node; reloading then would cancel the load in flight
		// and loop forever without ever listing the entries.
		if( data.Node == _loadingNode )
			return;

		SelectDirectory( data.Node );
		data.EnsureVisible( sender );
		await LoadSelectedEntriesAsync();
	}

	/// <summary>Loads the selected directory's entries; a newer load cancels an older one.</summary>
	private Task LoadSelectedEntriesAsync()
	{
		_loadingNode = LastSelectedNode;
		_treeSelectionCancellation.Cancel();
		_treeSelectionCancellation = new();
		ViewModel.IsListLoaded = false;
		return TreeSelectionTask = LoadAndRestoreAsync( _treeSelectionCancellation.Token );
	}

	private async Task LoadAndRestoreAsync( CancellationToken token )
	{
		await ViewModel.LoadEntriesAsync( token );
		if( !_restorePending || token.IsCancellationRequested || _left )
			return;
		_restorePending = false;
		RestoreSortArrow();
		IReadOnlyList<EntryRow> rows = ViewModel.TakeSelectionToRestore();
		if( rows.Count == 0 )
			return;
		SelectEntryRows( rows );
	}

	/// <summary>Shows the kept sort on its column header.</summary>
	private void RestoreSortArrow()
	{
		if( ViewModel.EntrySort is not var (field, descending) )
			return;
		foreach( var column in OdooEntryList.Columns )
			column.SortDirection = column.SortMemberPath == field
				? descending ? SortDirection.Descending : SortDirection.Ascending
				: null;
	}

	private void OdooDirectoryTree_RightTapped( object sender, RightTappedRoutedEventArgs e )
	{
		if( ( e.OriginalSource as FrameworkElement )?.DataContext is not TreeData data )
			return;
		OdooDirectoryTree.SelectedNode = data.Node;
		SelectDirectory( data.Node );
	}

	private void OdooDirectoryBreadcrumb_ItemClicked( BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args )
	{
		if( args.Item is not TreeData { Node: { } node } )
			return;

		JumpToTreeNode( node );
	}

	private void TreeViewItem_Loaded( object sender, RoutedEventArgs e )
	{
		if( sender is TreeViewItem item )
		{
			item.FontSize = UIStorage.ColumnsInfo.FontSize;
			// a recycled container must not carry another folder's badge
			if( item.Content is Panel panel )
				RemoveBadges( panel );
		}
	}

	private void TreeViewItem_Unloaded( object sender, RoutedEventArgs e )
	{
		// DataContext is null for recycled containers; Dictionary.Remove(null) throws on the UI thread
		if( ( sender as TreeViewItem )?.DataContext is { } data )
			ItemToContainerMap.Remove( data );
	}

	#endregion
	#region Entry list and detail tabs

	private void OdooEntryList_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		ViewModel.SelectedEntries = [ .. OdooEntryList.SelectedItems.OfType<EntryRow>() ];
		if( ViewModel.SelectedEntries is [ var entry ] )
			_ = ViewModel.ShowEntryDetailsAsync( entry );
	}

	private void VersionTabs_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		ViewModel.SelectedDetailTab = ( VersionTabs.SelectedItem as TabViewItem )?.Name;
		EntryRow? selected = IsUsingGroupedList
			? GroupedEntryList.SelectedItems.OfType<EntryRow>().FirstOrDefault()
			: OdooEntryList.SelectedItem as EntryRow;
		if( e.AddedItems.Count > 0 && selected is not null )
			_ = ViewModel.ShowEntryDetailsAsync( selected );
	}

	private void OdooEntryList_Sorting( object? sender, TableViewSortingEventArgs e )
	{
		// the view model sorts the source collection; stop TableView sorting it a second time
		e.Handled = true;
		foreach( var column in OdooEntryList.Columns.Where( c => c != e.Column ) )
			column.SortDirection = null;

		bool descending = e.Column.SortDirection is SortDirection.Ascending;
		e.Column.SortDirection = descending ? SortDirection.Descending : SortDirection.Ascending;

		string? field = e.Column.SortMemberPath
			?? ( e.Column as TableViewBoundColumn )?.Binding?.Path?.Path
			?? e.Column.Header?.ToString();
		ViewModel.SortEntries( field, descending );
	}

	private void OdooHistory_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		var row = e.AddedItems.FirstOrDefault() as HistoryRow;
		ViewModel.SelectedHistory = row;
		PreviewVersion( row?.Version );
	}

	private void PreviewVersion( int? versionId ) => _ = ViewModel.PreviewVersionAsync( versionId );

	private async void OdooParents_DoubleTapped( object sender, DoubleTappedRoutedEventArgs e )
	{
		if( OdooParents.SelectedItems is [ ParentRow row ] )
			await FindSearchSelectionAsync( row.BasePath ?? "", row.Name ?? "" );
	}

	private async void OdooChildren_DoubleTapped( object sender, DoubleTappedRoutedEventArgs e )
	{
		if( OdooChildren.SelectedItems is [ ChildrenRow row ] )
			await FindSearchSelectionAsync( row.BasePath ?? "", row.Name ?? "" );
	}

	/// <summary>Selects the directory at <paramref name="pwaPath"/> in the tree, then the entry named <paramref name="fileName"/>.</summary>
	private Task FindSearchSelectionAsync( string pwaPath, string fileName )
		=> OnLocateRequestedAsync( PwaPaths.Segments( pwaPath ), fileName );

	/// <summary>Selects the directory at <paramref name="segmentsBelowRoot"/>, then the entry named <paramref name="fileName"/>.</summary>
	private async Task OnLocateRequestedAsync( IReadOnlyList<string> segmentsBelowRoot, string? fileName )
	{
		try
		{
			await AsyncHelper.WaitUntil( () => ViewModel.IsTreeLoaded && OdooDirectoryTree.RootNodes.Count > 0, 100, 30_000 );
			TreeViewNode node = OdooDirectoryTree.RootNodes[ 0 ];
			foreach( string part in segmentsBelowRoot.Where( p => p.Length > 0 ) )
			{
				node = node.Children.FirstOrDefault( n => n.LinkedData.Name == part )
					?? throw new ArgumentException( $"directory '{part}' not found", nameof( segmentsBelowRoot ) );
			}

			foreach( var root in OdooDirectoryTree.RootNodes )
				root.IsExpanded = false;
			node.LinkedData.EnsureVisible( OdooDirectoryTree );
			OdooDirectoryTree.SelectedNode = node;
			SelectDirectory( node );

			await AsyncHelper.WaitUntil( () => ViewModel.IsListLoaded, 100, 60_000 );
			if( fileName is null )
				return;

			EntryRow entry = ViewModel.Entries.FirstOrDefault( e => e.Name == fileName )
				?? throw new ArgumentException( "entry doesn't exist", nameof( fileName ) );
			SelectEntryRows( [ entry ], focus: true, scroll: true );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Unable to find search selection '{fileName}': {ex.Message}", ex );
		}
	}

	#endregion
	#region Toolbar (window-level actions)

	private void Anchor_Checked( object sender, RoutedEventArgs e )
	{
		if( HackApp.Window?.AppWindow.Presenter is OverlappedPresenter presenter )
			presenter.IsAlwaysOnTop = Anchor.IsChecked ?? false;
	}

	private void Refresh_Click( object sender, RoutedEventArgs e )
		=> ViewModel.RefreshFromServer();

	private void FocusEntryFromChanges( EntryRow? entry )
	{
		if( entry is null )
			return;
		ListTabs.SelectedItem = RootEntryList;
		SelectEntryRows( [ entry ], focus: true, scroll: true );
	}

	private void HackFileManagerView_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		var ctrlDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread( Windows.System.VirtualKey.Control )
			.HasFlag( Windows.UI.Core.CoreVirtualKeyStates.Down );
		var altDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread( Windows.System.VirtualKey.Menu )
			.HasFlag( Windows.UI.Core.CoreVirtualKeyStates.Down );

		if( e.Key == Windows.System.VirtualKey.F5 )
		{
			e.Handled = true;
			ViewModel.RefreshFromServer();
		}
		else if( e.Key == Windows.System.VirtualKey.F && ctrlDown )
		{
			e.Handled = true;
			Search_Click( sender, e );
		}
		else if( e.Key == Windows.System.VirtualKey.Up && altDown )
		{
			if( LastSelectedNode?.Parent is { Depth: >= 0 } parent )
			{
				e.Handled = true;
				JumpToTreeNode( parent );
			}
		}
	}

	private void Search_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Dialogs.ShowSearchWindow();

	private void ManageTypes_Click( object sender, RoutedEventArgs e )
	{
		WindowHelper.CreateWindowAndPage<OdooFileTypeManagerView>( out var page, out var window );
		window.Title = "File Types & Filters";
		page.StoreWindowInstance( window );
	}

	private void StatusWindow_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Dialogs.ShowStatusWindow();

	private void OnTogglePipClicked( object sender, RoutedEventArgs e )
	{
		if( _pipWindow is not null )
		{
			ReturnPreviewToPage();
			return;
		}

		try
		{
			PipWindow pip = new();
			pip.ReturnRequested += ( _, _ ) => ReturnPreviewToPage();
			pip.Closed += OnPipWindowClosed;
			_pipWindow = pip;
			SyncPipPreview();
			pip.Activate();
		}
		catch( Exception ex )
		{
			_pipWindow = null;
			HackLogger.Error( "FileManager", $"Opening PiP preview window failed: {ex.Message}", ex );
		}
	}

	private void ReturnPreviewToPage()
	{
		if( _pipWindow is null )
			return;

		PipWindow pip = _pipWindow;
		_pipWindow = null;
		pip.Closed -= OnPipWindowClosed;
		try
		{
			pip.Close();
		}
		catch
		{
			// Window may already be closing
		}
	}

	private void OnPipWindowClosed( object sender, WindowEventArgs args )
	{
		if( _pipWindow is not null )
		{
			_pipWindow.Closed -= OnPipWindowClosed;
			_pipWindow = null;
		}
		if( _left )
			return;
	}

	#endregion
	#region Rearrangeable & Dockable Workspace Layout

	private static WorkspaceLayoutSettings LayoutSettings => UIStorage.WorkspaceLayout;

	private void ApplyWorkspaceLayout()
	{
		var layout = LayoutSettings;
		bool showFolders = layout.ShowFoldersPane;
		bool showDetails = layout.ShowDetailsPane;
		bool showPreview = layout.ShowPreviewPane;
		bool hasInspector = showDetails || showPreview;

		ToggleFolderPaneButton.IsChecked = showFolders;
		MenuShowFolders.IsChecked = showFolders;
		MenuShowDetails.IsChecked = showDetails;
		MenuShowPreview.IsChecked = showPreview;

		FoldersPaneCard.Visibility = FoldersSplitter.Visibility = Vis( showFolders );
		DetailsAndPreviewHost.Visibility = InspectorSplitter.Visibility = Vis( hasInspector );
		DetailsPaneCard.Visibility = Vis( showDetails );
		PreviewPaneCard.Visibility = Vis( showPreview );
		DetailsPreviewSplitter.Visibility = Vis( showDetails && showPreview );

		WorkspaceGrid.RowDefinitions.Clear();
		WorkspaceGrid.ColumnDefinitions.Clear();
		_foldersCol = null;
		_inspectorRightCol = null;
		_inspectorBottomRow = null;

		ColumnDefinition AddCol( GridLength length )
		{
			ColumnDefinition def = new() { Width = length };
			WorkspaceGrid.ColumnDefinitions.Add( def );
			return def;
		}
		RowDefinition AddRow( GridLength length )
		{
			RowDefinition def = new() { Height = length };
			WorkspaceGrid.RowDefinitions.Add( def );
			return def;
		}

		bool bottomDock = layout.DetailsDock == DetailsDockPosition.Bottom;
		int rowSpan = bottomDock && hasInspector ? 3 : 1;
		int col = 0;

		AddRow( new GridLength( 1, GridUnitType.Star ) );
		if( bottomDock && hasInspector )
		{
			AddRow( GridLength.Auto );
			_inspectorBottomRow = AddRow( new GridLength( Math.Clamp( layout.DetailsPaneHeight, 140, 520 ) ) );
		}

		if( showFolders && layout.FoldersDock == FolderDockSide.Left )
		{
			_foldersCol = AddCol( new GridLength( Math.Clamp( layout.FoldersPaneWidth, 190, 520 ) ) );
			FoldersPaneCard.SetGrid( 0, col++, rowSpan );
			AddCol( GridLength.Auto );
			FoldersSplitter.SetGrid( 0, col++, rowSpan );
		}

		int mainCol = col++;
		AddCol( new GridLength( 1, GridUnitType.Star ) );
		EntriesPaneCard.SetGrid( 0, mainCol );

		if( hasInspector )
		{
			if( bottomDock )
			{
				InspectorSplitter.Orientation = Orientation.Horizontal;
				InspectorSplitter.SetGrid( 1, mainCol );
				DetailsAndPreviewHost.SetGrid( 2, mainCol );
			}
			else
			{
				AddCol( GridLength.Auto );
				InspectorSplitter.Orientation = Orientation.Vertical;
				InspectorSplitter.SetGrid( 0, col++ );
				_inspectorRightCol = AddCol( new GridLength( Math.Clamp( layout.DetailsRightWidth, 260, 720 ) ) );
				DetailsAndPreviewHost.SetGrid( 0, col++ );
			}
		}

		if( showFolders && layout.FoldersDock == FolderDockSide.Right )
		{
			AddCol( GridLength.Auto );
			FoldersSplitter.SetGrid( 0, col++, rowSpan );
			_foldersCol = AddCol( new GridLength( Math.Clamp( layout.FoldersPaneWidth, 190, 520 ) ) );
			FoldersPaneCard.SetGrid( 0, col++, rowSpan );
		}

		ArrangeInspectorHost( layout, showDetails, showPreview );
	}

	private void ArrangeInspectorHost( WorkspaceLayoutSettings layout, bool showDetails, bool showPreview )
	{
		DetailsAndPreviewHost.RowDefinitions.Clear();
		DetailsAndPreviewHost.ColumnDefinitions.Clear();
		_previewBottomCol = null;
		_previewRightRow = null;

		if( !showDetails && !showPreview )
			return;

		bool bottomDock = layout.DetailsDock == DetailsDockPosition.Bottom;
		bool previewFirst = layout.PreviewPlacement == PreviewDockPlacement.BeforeDetails;
		DetailsPreviewSplitter.Orientation = bottomDock ? Orientation.Vertical : Orientation.Horizontal;

		FrameworkElement first = previewFirst ? PreviewPaneCard : DetailsPaneCard;
		FrameworkElement second = previewFirst ? DetailsPaneCard : PreviewPaneCard;

		if( bottomDock )
		{
			DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
			if( showDetails && showPreview )
			{
				GridLength previewLen = new( Math.Clamp( layout.PreviewPaneSize, 180, 560 ) );
				GridLength starLen = new( 1, GridUnitType.Star );
				ColumnDefinition c0 = new() { Width = previewFirst ? previewLen : starLen };
				ColumnDefinition c2 = new() { Width = previewFirst ? starLen : previewLen };
				_previewBottomCol = previewFirst ? c0 : c2;
				DetailsAndPreviewHost.ColumnDefinitions.Add( c0 );
				DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
				DetailsAndPreviewHost.ColumnDefinitions.Add( c2 );
				first.SetGrid( 0, 0 );
				DetailsPreviewSplitter.SetGrid( 0, 1 );
				second.SetGrid( 0, 2 );
			}
			else
			{
				DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
				( showDetails ? DetailsPaneCard : PreviewPaneCard ).SetGrid( 0, 0 );
			}
		}
		else
		{
			DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
			if( showDetails && showPreview )
			{
				GridLength previewLen = new( Math.Clamp( layout.PreviewPaneSize, 150, 460 ) );
				GridLength starLen = new( 1, GridUnitType.Star );
				RowDefinition r0 = new() { Height = previewFirst ? previewLen : starLen };
				RowDefinition r2 = new() { Height = previewFirst ? starLen : previewLen };
				_previewRightRow = previewFirst ? r0 : r2;
				DetailsAndPreviewHost.RowDefinitions.Add( r0 );
				DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
				DetailsAndPreviewHost.RowDefinitions.Add( r2 );
				first.SetGrid( 0, 0 );
				DetailsPreviewSplitter.SetGrid( 1, 0 );
				second.SetGrid( 2, 0 );
			}
			else
			{
				DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
				( showDetails ? DetailsPaneCard : PreviewPaneCard ).SetGrid( 0, 0 );
			}
		}
	}

	private static Visibility Vis( bool visible ) => visible ? Visibility.Visible : Visibility.Collapsed;

	private void UpdateLayout( Action<WorkspaceLayoutSettings> mutate )
	{
		mutate( LayoutSettings );
		ApplyWorkspaceLayout();
		UIStorage.SaveSafe();
	}

	private void FoldersSplitter_DragDelta( double delta )
	{
		if( _foldersCol is null )
			return;
		double sign = LayoutSettings.FoldersDock == FolderDockSide.Left ? 1 : -1;
		double next = Math.Clamp( _foldersCol.Width.Value + delta * sign, 190, 520 );
		_foldersCol.Width = new GridLength( next );
		LayoutSettings.FoldersPaneWidth = next;
	}

	private void InspectorSplitter_DragDelta( double delta )
	{
		if( LayoutSettings.DetailsDock == DetailsDockPosition.Bottom && _inspectorBottomRow is not null )
		{
			double next = Math.Clamp( _inspectorBottomRow.Height.Value - delta, 140, 520 );
			_inspectorBottomRow.Height = new GridLength( next );
			LayoutSettings.DetailsPaneHeight = next;
		}
		else if( LayoutSettings.DetailsDock == DetailsDockPosition.Right && _inspectorRightCol is not null )
		{
			double next = Math.Clamp( _inspectorRightCol.Width.Value - delta, 260, 720 );
			_inspectorRightCol.Width = new GridLength( next );
			LayoutSettings.DetailsRightWidth = next;
		}
	}

	private void DetailsPreviewSplitter_DragDelta( double delta )
	{
		double sign = LayoutSettings.PreviewPlacement == PreviewDockPlacement.BeforeDetails ? 1 : -1;
		if( LayoutSettings.DetailsDock == DetailsDockPosition.Bottom && _previewBottomCol is not null )
		{
			double next = Math.Clamp( _previewBottomCol.Width.Value + delta * sign, 180, 560 );
			_previewBottomCol.Width = new GridLength( next );
			LayoutSettings.PreviewPaneSize = next;
		}
		else if( LayoutSettings.DetailsDock == DetailsDockPosition.Right && _previewRightRow is not null )
		{
			double next = Math.Clamp( _previewRightRow.Height.Value + delta * sign, 150, 460 );
			_previewRightRow.Height = new GridLength( next );
			LayoutSettings.PreviewPaneSize = next;
		}
	}

	private void OnSplitterDragCompleted() => UIStorage.SaveSafe();

	private void ToggleFolderPane_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ShowFoldersPane = ToggleFolderPaneButton.IsChecked == true );
	private void PresetBalanced_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ApplyPreset( WorkspaceLayoutPreset.Balanced ) );
	private void PresetRightInspector_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ApplyPreset( WorkspaceLayoutPreset.RightInspector ) );
	private void PresetRightSidebar_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ApplyPreset( WorkspaceLayoutPreset.RightSidebar ) );
	private void PresetFocusEntries_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ApplyPreset( WorkspaceLayoutPreset.FocusEntries ) );
	private void MenuTogglePane_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => ( l.ShowFoldersPane, l.ShowDetailsPane, l.ShowPreviewPane ) = ( MenuShowFolders.IsChecked, MenuShowDetails.IsChecked, MenuShowPreview.IsChecked ) );
	private void DockFoldersLeft_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => ( l.FoldersDock, l.ShowFoldersPane ) = ( FolderDockSide.Left, true ) );
	private void DockFoldersRight_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => ( l.FoldersDock, l.ShowFoldersPane ) = ( FolderDockSide.Right, true ) );
	private void HideFoldersPane_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ShowFoldersPane = false );
	private void ToggleInspectorDock_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.DetailsDock = l.DetailsDock == DetailsDockPosition.Bottom ? DetailsDockPosition.Right : DetailsDockPosition.Bottom );
	private void DockInspectorBottom_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => ( l.DetailsDock, l.ShowDetailsPane ) = ( DetailsDockPosition.Bottom, true ) );
	private void DockInspectorRight_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => ( l.DetailsDock, l.ShowDetailsPane ) = ( DetailsDockPosition.Right, true ) );
	private void SwapPreviewPlacement_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.PreviewPlacement = l.PreviewPlacement == PreviewDockPlacement.AfterDetails ? PreviewDockPlacement.BeforeDetails : PreviewDockPlacement.AfterDetails );
	private void HideDetailsPane_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ShowDetailsPane = false );
	private void HidePreviewPane_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ShowPreviewPane = false );

	private void FoldersGrip_PointerPressed( object sender, PointerRoutedEventArgs e ) => StartPaneDrag( "Folders", FoldersDragGrip, e );
	private void DetailsGrip_PointerPressed( object sender, PointerRoutedEventArgs e ) => StartPaneDrag( "Details", DetailsDragGrip, e );
	private void PreviewGrip_PointerPressed( object sender, PointerRoutedEventArgs e ) => StartPaneDrag( "Preview", PreviewDragGrip, e );

	private void StartPaneDrag( string paneTag, UIElement grip, PointerRoutedEventArgs e )
	{
		if( !e.GetCurrentPoint( grip ).Properties.IsLeftButtonPressed )
			return;

		_draggingPaneTag = paneTag;
		_isDraggingGrip = true;
		_activeDropZone = null;
		grip.CapturePointer( e.Pointer );

		ZoneLeft.Visibility = Vis( paneTag == "Folders" );
		ZoneRight.Visibility = Vis( paneTag is "Folders" or "Details" );
		ZoneBottom.Visibility = Vis( paneTag == "Details" );
		ZoneSwapPreview.Visibility = Vis( paneTag == "Preview" );

		if( paneTag == "Details" )
			ZoneBottom.SetGrid( 1, 0, 1, 2 );
		else if( paneTag == "Preview" )
		{
			if( LayoutSettings.DetailsDock == DetailsDockPosition.Right )
				ZoneSwapPreview.SetGrid( 0, 2, 2, 1 );
			else
				ZoneSwapPreview.SetGrid( 1, 0, 1, 3 );
		}

		UpdateDropZoneHighlights( null );
		DockDropOverlay.Visibility = Visibility.Visible;
		e.Handled = true;
	}

	private void PaneGrip_PointerMoved( object sender, PointerRoutedEventArgs e )
	{
		if( !_isDraggingGrip || _draggingPaneTag is null )
			return;
		string? zone = HitTestDropZone( e.GetCurrentPoint( DockDropOverlay ).Position );
		if( zone == _activeDropZone )
			return;
		_activeDropZone = zone;
		UpdateDropZoneHighlights( zone );
	}

	private string? HitTestDropZone( Windows.Foundation.Point pos )
	{
		foreach( var (zone, name) in new[] { ( ZoneLeft, "Left" ), ( ZoneRight, "Right" ), ( ZoneBottom, "Bottom" ), ( ZoneSwapPreview, "SwapPreview" ) } )
		{
			if( zone.Visibility != Visibility.Visible || zone.ActualWidth <= 0 || zone.ActualHeight <= 0 )
				continue;
			try
			{
				var origin = zone.TransformToVisual( DockDropOverlay ).TransformPoint( new Windows.Foundation.Point( 0, 0 ) );
				if( pos.X >= origin.X && pos.X <= origin.X + zone.ActualWidth && pos.Y >= origin.Y && pos.Y <= origin.Y + zone.ActualHeight )
					return name;
			}
			catch { }
		}
		return null;
	}

	private void EndPaneDrag()
	{
		_draggingPaneTag = null;
		_isDraggingGrip = false;
		_activeDropZone = null;
		DockDropOverlay.Visibility = Visibility.Collapsed;
	}

	private void PaneGrip_PointerCaptureLost( object sender, PointerRoutedEventArgs e )
	{
		if( _isDraggingGrip )
			EndPaneDrag();
	}

	private void PaneGrip_PointerReleased( object sender, PointerRoutedEventArgs e )
	{
		if( !_isDraggingGrip )
			return;
		if( sender is UIElement grip )
			grip.ReleasePointerCaptures();

		string? pane = _draggingPaneTag;
		string? zone = _activeDropZone;
		EndPaneDrag();

		if( pane == "Folders" && zone is "Left" or "Right" )
			UpdateLayout( l => l.FoldersDock = zone == "Left" ? FolderDockSide.Left : FolderDockSide.Right );
		else if( pane == "Details" && zone is "Right" or "Bottom" )
			UpdateLayout( l => l.DetailsDock = zone == "Right" ? DetailsDockPosition.Right : DetailsDockPosition.Bottom );
		else if( pane == "Preview" && zone == "SwapPreview" )
			SwapPreviewPlacement_Click( sender, e );
	}

	private void UpdateDropZoneHighlights( string? activeZone )
	{
		ZoneLeft.Opacity = activeZone == "Left" ? 0.95 : 0.25;
		ZoneRight.Opacity = activeZone == "Right" ? 0.95 : 0.25;
		ZoneBottom.Opacity = activeZone == "Bottom" ? 0.95 : 0.25;
		ZoneSwapPreview.Opacity = activeZone == "SwapPreview" ? 0.95 : 0.25;
	}

	#endregion
	#region Accessors used by the smoke tests

	/// <summary>The directory tree control, for the smoke tests.</summary>
	/// <returns>The page's directory tree view.</returns>
	public TreeView GetOdooDirectoryTree() => OdooDirectoryTree;
	internal void ApplyLayoutPresetForTest( WorkspaceLayoutPreset preset )
	{
		LayoutSettings.ApplyPreset( preset );
		ApplyWorkspaceLayout();
	}
	/// <summary>The entry list control, for the smoke tests.</summary>
	/// <returns>The page's entry table.</returns>
	public TableView GetOdooEntryList() => OdooEntryList;
	internal TabViewItem HistoryTabItem => HistoryTab;
	internal TabViewItem? LowerTabIndex
	{
		get => VersionTabs.SelectedItem as TabViewItem;
		set => VersionTabs.SelectedItem = value;
	}

	#endregion
}
