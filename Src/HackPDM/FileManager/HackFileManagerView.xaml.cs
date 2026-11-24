using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Settings;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using WinUI.TableView;

using TableView = WinUI.TableView.TableView;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.FileManager;

/// <summary>
/// View for <see cref="HackFileManagerViewModel"/>. Only builds and drives controls: every PDM
/// operation is a view-model command, and the view reacts to the view model's refresh requests.
/// <para>
/// Each visit gets a new page and a new view model (disposed in <see cref="OnNavigatedFrom"/> or <see cref="OnUnloaded"/>);
/// what the user was looking at comes back from the session's FileManagerState: the tree with its expanded
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
	private string? _loadingTreePath;
	private int? _loadingDirectoryId;

	private readonly IPreviewImageLoader _previewLoader = new PreviewImageLoader();
	private int _previewRequest;

	/// <summary>Folders from the root to the selected one, shown in the breadcrumb bar.</summary>
	public RangeObservableCollection<TreeData> BreadcrumbPath { get; } = [];
	// TreeView items are built as nodes (see DirectoryTreeBuilder); this source stays empty
	private readonly ObservableCollection<TreeData> _treeItems = [];
	private const int TreeRebuildCooldownMs = 50;
	private const int FolderFilterDebounceMs = 150;
	private const int TreeSelectionDebounceMs = 75;
	private const int BadgeCooldownMs = 150;
	private const int BadgeMinIntervalMs = 50;
	private const int MaxBadgesPerWindow = 12;

	private readonly CancellationTokenSource _leaveCts = new();
	private CancellationTokenSource? _treeSelectionCancellation;
	private CancellationTokenSource? _treeRebuildCts;
	private CancellationTokenSource? _groupingCts;
	private CancellationTokenSource? _previewDecodeCts;
	private Task? _treeFilterTask;
#if !WINDOWS
	private Task? _groupingTask;
#endif
	private long _lastTreeRebuildTick = long.MinValue / 2;
	private readonly Dictionary<TreeViewNode, (long Tick, LocalChangeKind Kind)> _recentBadges = [];
	private readonly List<(Storyboard Story, Panel Panel, TextBlock Badge, EventHandler<object> Completed)> _activeBadgeStoryboards = [];
	private long _badgeWindowStartTick;
	private int _badgesInWindow;
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
	private readonly HashSet<string> _expandedGroupKeys = new( StringComparer.Ordinal );
	private bool? _groupDefaultCollapsed;
	private bool _syncingGroupedSelection;
	private bool _suppressEntrySelectionSync;
	private bool _pendingHeaderToggle;
	private bool _applyingColumns;
	private bool _columnTrackingAttached;
	private readonly List<(TableViewColumn Column, long Token)> _columnWidthCallbacks = [];
	private bool IsUsingGroupedList => GroupedEntryContainer.Visibility == Visibility.Visible;

	private static readonly SolidColorBrush SyncedDotBrush = new( Windows.UI.Color.FromArgb( 255, 0xC6, 0x97, 0x49 ) );
	private static readonly SolidColorBrush LocalDotBrush = new( Windows.UI.Color.FromArgb( 255, 0x3F, 0xB9, 0x50 ) );
	private static readonly SolidColorBrush RemoteDotBrush = new( Windows.UI.Color.FromArgb( 255, 0x58, 0xB4, 0xFF ) );
	private static readonly SolidColorBrush CreatedBadgeBrush = new( Windows.UI.Color.FromArgb( 255, 0x2E, 0x9E, 0x44 ) );
	private static readonly SolidColorBrush DeletedBadgeBrush = new( Windows.UI.Color.FromArgb( 255, 0xD1, 0x34, 0x38 ) );
	private static readonly SolidColorBrush UpdatedBadgeBrush = new( Windows.UI.Color.FromArgb( 255, 0xC7, 0x84, 0x00 ) );

	#region Construction and loading

	public HackFileManagerView()
	{
		InitializeComponent();
		ApplyColumnSettings();
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	protected override void OnNavigatedTo( NavigationEventArgs e )
	{
		base.OnNavigatedTo( e );
		_left = false;
		if( !_loaded )
		{
			Loaded -= OnLoaded;
			Loaded += OnLoaded;
		}
	}

	/// <summary>The page is left: its view model goes; the session state keeps what it showed.</summary>
	protected override void OnNavigatedFrom( NavigationEventArgs e )
	{
		Leave();
		base.OnNavigatedFrom( e );
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		Loaded -= OnLoaded;
		LoadHackMan();
	}

	private void OnUnloaded( object sender, RoutedEventArgs e ) => Leave();

	private void Leave()
	{
		if( _left )
			return;
		SaveColumnSettings();
		UIStorage.SaveSafe();
		_left = true;
		Loaded -= OnLoaded;
		Unloaded -= OnUnloaded;
		KeyDown -= HackFileManagerView_KeyDown;
		if( _columnTrackingAttached )
		{
			OdooEntryList.ColumnReordered -= OnEntryColumnsReordered;
			_columnTrackingAttached = false;
		}
		for( int i = 0; i < _columnWidthCallbacks.Count; i++ )
		{
			var (col, token) = _columnWidthCallbacks[ i ];
			col.UnregisterPropertyChangedCallback( TableViewColumn.WidthProperty, token );
		}
		_columnWidthCallbacks.Clear();

		StopAllBadgeStoryboards();
		_recentBadges.Clear();
		try { _leaveCts.Cancel(); }
		finally { _leaveCts.Dispose(); }
		CtsHelper.CancelAndDispose( ref _treeRebuildCts );
		CtsHelper.CancelAndDispose( ref _treeSelectionCancellation );
		CtsHelper.CancelAndDispose( ref _groupingCts );
		CtsHelper.CancelAndDispose( ref _previewDecodeCts );

		ViewModel.RefreshRequested -= OnRefreshRequested;
		ViewModel.GroupingChanged -= OnGroupingChanged;
		ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
		ViewModel.TreeLoaded -= OnTreeLoadedAsync;
		ViewModel.LocateRequested -= OnLocateRequestedAsync;
		ViewModel.FolderChanged -= OnFolderChanged;
		ViewModel.LiveBatchApplying -= OnLiveBatchApplying;
		ViewModel.RowsReplaced -= OnRowsReplaced;
		AppComposition.Theme.ThemeChanged -= OnThemeChanged;
		ViewModel.Dispose();
		ReturnPreviewToPage();
	}

	private void LoadHackMan()
	{
		if( _loaded || _left )
			return;
		_loaded = true;

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
		ViewModel.LiveBatchApplying += OnLiveBatchApplying;
		ViewModel.RowsReplaced += OnRowsReplaced;

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
		if( HackApp.Window?.AppWindow.Presenter is OverlappedPresenter presenter )
			Anchor.IsChecked = presenter.IsAlwaysOnTop;
		AppComposition.Theme.ThemeChanged += OnThemeChanged;
		TreeBranchGuides.RefreshTree( OdooDirectoryTree, showLines: AppComposition.Theme.ShowTreeBranchLines );
		ApplyWorkspaceLayout();
		OdooEntryList.SelectionChanged += OdooEntryList_SelectionChanged;
		OdooEntryList.SelectionBatchCompleted += ( _, _ ) => SyncEntryListSelection();
		OdooEntryList.Sorting += OdooEntryList_Sorting;
		OdooEntryList.DoubleTapped += ( _, e ) =>
		{
			if( ( e.OriginalSource as DependencyObject ).FindAncestor<TableViewRow>( OdooEntryList ) is not null && ViewModel.SelectedEntries.Count > 0 )
				ViewModel.OpenSelectedCommand.Execute( null );
		};
		OdooEntryList.ContainerContentChanging += EntryTable_ContainerContentChanging;
		HackChangesList.ContainerContentChanging += EntryTable_ContainerContentChanging;
		OdooChangesList.ContainerContentChanging += EntryTable_ContainerContentChanging;
		GroupedEntryList.ItemsSource = _groupedItems;
		GroupedEntryList.SelectionChanged += GroupedEntryList_SelectionChanged;
		GroupedEntryList.ItemClick += GroupedEntryList_ItemClick;
		GroupedEntryList.DoubleTapped += ( _, e ) =>
		{
			if( FindGroupedEntryRow( e.OriginalSource as DependencyObject ) is not null && ViewModel.SelectedEntries.Count > 0 )
				ViewModel.OpenSelectedCommand.Execute( null );
		};
		GroupedEntryList.RightTapped += GroupedEntryList_RightTapped;
		HackChangesList.DoubleTapped += ( _, e ) =>
		{
			if( ( e.OriginalSource as DependencyObject ).FindAncestor<TableViewRow>( HackChangesList ) is not null )
				FocusEntryFromChanges( HackChangesList.SelectedItem as EntryRow );
		};
		OdooChangesList.DoubleTapped += ( _, e ) =>
		{
			if( ( e.OriginalSource as DependencyObject ).FindAncestor<TableViewRow>( OdooChangesList ) is not null )
				FocusEntryFromChanges( OdooChangesList.SelectedItem as EntryRow );
		};
		OdooHistory.SelectionChanged += ( _, _ ) => { if( !OdooHistory.IsUpdatingSelection ) SyncHistorySelection(); };
		OdooHistory.SelectionBatchCompleted += ( _, _ ) => SyncHistorySelection();
		OdooHistory.DoubleTapped += ( _, e ) =>
		{
			if( ( e.OriginalSource as DependencyObject ).FindAncestor<TableViewRow>( OdooHistory ) is not null )
				ViewModel.DownloadHistoryWithDependenciesCommand.Execute( null );
		};
		OdooParents.SelectionChanged += ( _, _ ) => { if( !OdooParents.IsUpdatingSelection ) SyncParentSelection(); };
		OdooParents.SelectionBatchCompleted += ( _, _ ) => SyncParentSelection();
		OdooParents.DoubleTapped += OdooParents_DoubleTapped;
		OdooChildren.SelectionChanged += ( _, _ ) => { if( !OdooChildren.IsUpdatingSelection ) SyncChildrenSelection(); };
		OdooChildren.SelectionBatchCompleted += ( _, _ ) => SyncChildrenSelection();
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
			if( OdooDirectoryTree.SelectedNode is { } node && !ReferenceEquals( LastSelectedNode, node ) )
				SelectDirectory( node );
		};

		// a search made while this page was not shown waits until the tree is up
		ViewModel.IsTreeLoading = true;
		var pendingLocate = ViewModel.Activate();
		_ = LoadTreeAfterLayoutAsync( pendingLocate );
	}

	private async Task LoadTreeAfterLayoutAsync( (IReadOnlyList<string> Segments, string? FileName)? pendingLocate )
	{
		try
		{
			// Yield briefly so the page entrance transition starts cleanly before building the tree
			await Task.Delay( ViewModel.HasKeptTree ? 16 : 30, _leaveCts.Token );
			if( _left )
				return;
			await ViewModel.LoadDirectoryTreeAsync();
			if( pendingLocate is var (segments, fileName) && !_left )
				await OnLocateRequestedAsync( segments, fileName );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Error( "FileManager", $"Initial tree load failed: {ex.Message}", ex );
		}
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

		_applyingColumns = true;
		try
		{
			var defaults = new OdooEntryColumns().Columns;
			foreach( TableViewColumn column in OdooEntryList.Columns )
			{
				if( column.SortMemberPath is not { } property )
					continue;
				// a settings file from before a column existed gets that column's default
				OdooEntryColumn? saved = ( UIStorage.Columns.GetValueOrDefault( property ) ?? defaults.GetValueOrDefault( property ) )?.Column;
				column.Header = saved?.Header ?? property;
				if( saved is { DisplayIndex: >= 0 } )
					column.Order = saved.DisplayIndex;
				if( saved is { AutoSize: true } )
				{
					column.MinWidth = saved.Width >= 48 ? saved.Width : 180;
					column.Width = new GridLength( 1, GridUnitType.Star );
				}
				else
				{
					double width = saved is { Width: >= 48 } ? saved.Width : 100;
					if( property is nameof( EntryRow.LocalDate ) or nameof( EntryRow.RemoteDate ) && width <= 100 )
						width = 155;
					column.Width = new GridLength( width );
				}
				long token = column.RegisterPropertyChangedCallback( TableViewColumn.WidthProperty, OnEntryColumnWidthChanged );
				_columnWidthCallbacks.Add( ( column, token ) );
			}
			if( !_columnTrackingAttached )
			{
				OdooEntryList.ColumnReordered += OnEntryColumnsReordered;
				_columnTrackingAttached = true;
			}
		}
		finally
		{
			_applyingColumns = false;
		}

		foreach( string field in EntryRowKeys.Names )
		{
			EntryGroupBy.Items.Add( new MenuFlyoutItem { Text = field, Command = ViewModel.GroupByCommand, CommandParameter = field } );
			GroupByToolbarFlyout.Items.Add( new MenuFlyoutItem { Text = field, Command = ViewModel.GroupByCommand, CommandParameter = field } );
		}
	}

	internal void CaptureLayoutState() => SaveColumnSettings();

	private void OnEntryColumnWidthChanged( DependencyObject sender, DependencyProperty dp )
	{
		if( _applyingColumns || _left )
			return;
		SaveColumnSettings();
		UIStorage.SaveSafe();
	}

	private void OnEntryColumnsReordered( object? sender, TableViewColumnReorderedEventArgs e )
	{
		if( _applyingColumns || _left )
			return;
		SaveColumnSettings();
		UIStorage.SaveSafe();
	}

	private void SaveColumnSettings()
	{
		if( _applyingColumns )
			return;
		Dictionary<string, OdooEntryColumnID?> map = UIStorage.Columns;
		for( int i = 0; i < OdooEntryList.Columns.Count; i++ )
		{
			TableViewColumn column = OdooEntryList.Columns[ i ];
			if( column.SortMemberPath is not { Length: > 0 } property )
				continue;

			if( !map.TryGetValue( property, out OdooEntryColumnID? entry ) || entry is null )
			{
				entry = new OdooEntryColumnID
				{
					BindingPath = property,
					Column = new OdooEntryColumn { Header = column.Header?.ToString() ?? property },
				};
				map[ property ] = entry;
			}

			OdooEntryColumn colCfg = entry.Column;
			if( column.Order is int order && order >= 0 )
				colCfg.DisplayIndex = order;
			else
				colCfg.DisplayIndex = i;

			bool isStar = column.Width.IsStar;
			double measuredWidth = column.Width.IsAbsolute && column.Width.Value >= 40
				? column.Width.Value
				: column.ActualWidth;
			if( !isStar && measuredWidth >= 40 )
			{
				colCfg.AutoSize = false;
				colCfg.Width = Math.Round( measuredWidth );
			}
			else if( isStar )
			{
				colCfg.AutoSize = true;
				if( colCfg.Width < 48 )
					colCfg.Width = Math.Max( 180.0, column.MinWidth ?? 180.0 );
			}
		}
	}

	#endregion
	#region Reacting to the view model

	private void OnRefreshRequested( FileManagerRefresh what )
	{
		if( ( what & FileManagerRefresh.Tree ) != 0 )
			_ = ViewModel.LoadDirectoryTreeAsync();
		if( ( what & FileManagerRefresh.Entries ) != 0 )
			_ = ReloadEntriesAsync();
	}

	/// <summary>Rebuilds the tree nodes and reselects the directory that was selected before (throttled by cooldown).</summary>
	private async Task OnTreeLoadedAsync( DirectoryNode root )
	{
		if( _left )
			return;

		(CancellationTokenSource _, CancellationTokenSource? linked, CancellationToken token) = BeginTreeRebuildOperation();
		using CancellationTokenSource? linkedGuard = linked;
		if( token.IsCancellationRequested )
			return;

		try
		{
			long elapsed = Environment.TickCount64 - _lastTreeRebuildTick;
			if( _fullTreeRoot is not null && elapsed < TreeRebuildCooldownMs && DispatcherQueue is not null )
			{
				int delayMs = (int)Math.Max( 10, TreeRebuildCooldownMs - elapsed );
				await Task.Delay( delayMs, token );
				if( _left || token.IsCancellationRequested )
					return;
			}

			_fullTreeRoot = root;
			_totalTreeFolderCount = await ViewModel.CountTreeAsync( root, token );
			if( _left || token.IsCancellationRequested )
				return;

			await RebuildDisplayedTreeCoreAsync( FolderFilterBox.Text, token );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Rebuilding directory tree failed: {ex.Message}", ex );
		}
	}

	private Task RebuildDisplayedTreeAsync( string? filterText, int debounceMs )
	{
		if( _left )
			return Task.CompletedTask;

		(CancellationTokenSource _, CancellationTokenSource? linked, CancellationToken token) = BeginTreeRebuildOperation();
		return RunAsync();

		async Task RunAsync()
		{
			using CancellationTokenSource? linkedGuard = linked;
			try
			{
				if( debounceMs > 0 )
					await Task.Delay( debounceMs, token );
				if( _left || token.IsCancellationRequested )
					return;
				await RebuildDisplayedTreeCoreAsync( filterText, token );
			}
			catch( OperationCanceledException ) { }
			catch( ObjectDisposedException ) { }
			catch( Exception ex )
			{
				HackLogger.Warn( "FileManager", $"Filtering directory tree failed: {ex.Message}", ex );
			}
		}
	}

	private async Task RebuildDisplayedTreeCoreAsync( string? filterText, CancellationToken token )
	{
		DirectoryNode? fullRoot = _fullTreeRoot;
		if( fullRoot is null || _left || token.IsCancellationRequested )
			return;

		string query = filterText?.Trim() ?? "";
		bool isFiltering = query.Length > 0;
		var (displayRoot, shownCount) = await ViewModel.FilterTreeAsync( fullRoot, query, token );
		if( _left || token.IsCancellationRequested )
			return;

		_lastTreeRebuildTick = Environment.TickCount64;
		_recentBadges.Clear();
		_isFilteringTree = isFiltering;

		if( displayRoot is null )
		{
			OdooDirectoryTree.RootNodes.Clear();
			EmptyTreeFilterPanel.Visibility = Visibility.Visible;
			TreeSummaryText.Text = $"0 / {_totalTreeFolderCount} folders matching \"{query}\"";
			return;
		}

		EmptyTreeFilterPanel.Visibility = Visibility.Collapsed;
		DirectoryTreeBuilder.Build(
			OdooDirectoryTree,
			displayRoot,
			expandedPaths: _isFilteringTree ? null : ViewModel.ExpandedPaths,
			expandAll: _isFilteringTree );

		if( !_isFilteringTree && ViewModel.ExpandedPaths.Count == 0 && OdooDirectoryTree.RootNodes.Count > 0 )
		{
			TreeViewNode firstRoot = OdooDirectoryTree.RootNodes[ 0 ];
			firstRoot.LinkedData.IsExpanded = true;
			ViewModel.SetExpanded( firstRoot.LinkedData.FullPath, true );
		}

		if( OdooDirectoryTree.FindTreeNode( ViewModel.LastSelectedNodePath ?? "" ) is { Node: { } node } data )
		{
			data.EnsureVisible( OdooDirectoryTree );
			if( !ReferenceEquals( LastSelectedNode, node ) )
				SelectDirectory( node );
		}
		else if( !_isFilteringTree )
		{
			TreeData? fallback = FindNearestAncestorNode( ViewModel.LastSelectedNodePath );
			_loadingNode = null;
			_loadingTreePath = null;
			_loadingDirectoryId = null;
			if( fallback is { Node: { } fallbackNode } )
			{
				SelectDirectory( fallbackNode );
				_ = LoadSelectedEntriesAsync();
				fallback.EnsureVisible( OdooDirectoryTree );
			}
			else
			{
				bool hadSelection = LastSelectedNode is not null || ViewModel.SelectedDirectory is not null;
				SelectDirectory( null );
				if( hadSelection )
				{
					CtsHelper.CancelAndDispose( ref _treeSelectionCancellation );
					ViewModel.Entries.ClearIfNonEmpty();
					ViewModel.SelectedEntries = [];
					ViewModel.IsEmptyDirectory = false;
					ViewModel.IsListLoaded = false;
					_ = ViewModel.ShowEntryDetailsAsync( null );
				}
			}
		}

		TreeSummaryText.Text = _isFilteringTree
			? $"{shownCount} / {_totalTreeFolderCount} folders matching \"{query}\""
			: $"{_totalTreeFolderCount} folder{( _totalTreeFolderCount == 1 ? "" : "s" )}";
	}

	private (CancellationTokenSource OpCts, CancellationTokenSource? LinkedCts, CancellationToken Token) BeginTreeRebuildOperation()
	{
		CancellationTokenSource opCts = CtsHelper.Replace( ref _treeRebuildCts );
		try
		{
			CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource( opCts.Token, _leaveCts.Token );
			return (opCts, linked, linked.Token);
		}
		catch( ObjectDisposedException )
		{
			return (opCts, null, new CancellationToken( canceled: true ));
		}
	}

	private TreeData? FindNearestAncestorNode( string? path )
	{
		if( string.IsNullOrEmpty( path ) || OdooDirectoryTree.RootNodes.Count == 0 )
			return null;
		string[] parts = PwaPaths.Segments( path );
		for( int len = parts.Length - 1; len >= 1; len-- )
		{
			string candidate = PwaPaths.ToServerName( parts[ ..len ] );
			if( OdooDirectoryTree.FindTreeNode( candidate ) is { } found )
				return found;
		}
		return OdooDirectoryTree.RootNodes[ 0 ].LinkedData;
	}

	private async Task ReloadEntriesAsync()
	{
		try
		{
			if( ViewModel.SelectedDirectory is null && OdooDirectoryTree.SelectedNode is null )
				return;
			if( LastSelectedNode is null && OdooDirectoryTree.SelectedNode is { } treeSelected )
				SelectDirectory( treeSelected );

			await LoadSelectedEntriesAsync();
			if( _left )
				return;
			if( IsUsingGroupedList )
			{
				if( GroupedEntryList.SelectedItems.FirstSelected<EntryRow>() is { } groupedEntry )
					GroupedEntryList.ScrollIntoView( groupedEntry );
			}
			else if( OdooEntryList.SelectedItems is { Count: > 0 } items && items[ 0 ] is EntryRow entry )
			{
				OdooEntryList.ScrollIntoView( entry );
			}
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Reloading entries failed: {ex.Message}", ex );
		}
	}

	private void OnGroupingChanged()
	{
		EntryListGrouping.Apply( OdooEntryList, ViewModel.Entries, ViewModel.ActiveGroupKey );
#if !WINDOWS
		bool showGrouped = ViewModel.ActiveGroupKey is not null;
		OdooEntryList.Visibility = ( !showGrouped ).ToVisibility();
		GroupedEntryContainer.Visibility = showGrouped.ToVisibility();
		if( showGrouped )
		{
			_groupingTask = RebuildGroupedListAsync();
		}
		else
		{
			CtsHelper.CancelAndDispose( ref _groupingCts );
			_groups.Clear();
			_groupedItems.ClearIfNonEmpty();
			OdooEntryList.SetSelectionBatch( ViewModel.SelectedEntries );
			RestoreSortArrow();
		}
#endif
	}

	private async Task RebuildGroupedListAsync()
	{
		if( _left )
			return;
		if( ViewModel.ActiveGroupKey is not { } groupKey )
		{
			CtsHelper.CancelAndDispose( ref _groupingCts );
			_groups.Clear();
			_groupedItems.ClearIfNonEmpty();
			return;
		}

		CancellationTokenSource opCts = CtsHelper.Replace( ref _groupingCts );
		CancellationTokenSource? linked = null;
		try
		{
			linked = CancellationTokenSource.CreateLinkedTokenSource( opCts.Token, _leaveCts.Token );
		}
		catch( ObjectDisposedException )
		{
			return;
		}
		using CancellationTokenSource linkedGuard = linked;
		CancellationToken token = linked.Token;

		List<EntryRow> entriesSnapshot = [ .. ViewModel.Entries ];
		HashSet<string> collapsedSnapshot = [ .. _collapsedGroupKeys ];
		HashSet<string> expandedSnapshot = [ .. _expandedGroupKeys ];
		bool defaultCollapsed = _groupDefaultCollapsed ?? UIStorage.ColumnsInfo.GroupCollapsed;

		try
		{
			var (groups, flat) = await Task.Run( () =>
			{
				token.ThrowIfCancellationRequested();
				List<EntryGroupHeader> built = EntryListGrouping.BuildGroups(
					entriesSnapshot,
					groupKey,
					collapsedSnapshot,
					defaultCollapsed,
					expandedSnapshot );
				token.ThrowIfCancellationRequested();
				List<object> flattened = EntryListGrouping.FlattenVisible( built );
				return (built, flattened);
			}, token );

			if( _left || token.IsCancellationRequested || !ReferenceEquals( ViewModel.ActiveGroupKey, groupKey ) )
				return;

			_groups = groups;
			ApplyGroupedFlatList( flat );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Grouping entries failed: {ex.Message}", ex );
		}
	}

	private void RefreshGroupedFlatList( IReadOnlyCollection<EntryRow>? selectionToKeep = null )
		=> ApplyGroupedFlatList( EntryListGrouping.FlattenVisible( _groups ), selectionToKeep );

	private void ApplyGroupedFlatList( IReadOnlyList<object> flatItems, IReadOnlyCollection<EntryRow>? selectionToKeep = null )
	{
		HashSet<EntryRow> previousSelection = [ .. ( selectionToKeep ?? ViewModel.SelectedEntries ) ];
		_syncingGroupedSelection = true;
		List<EntryRow> restored = [];
		try
		{
			_groupedItems.ReplaceAll( flatItems );
			GroupedEntryList.SelectedItems.Clear();
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
		bool selectionChanged = previousSelection.Count != restored.Count || !previousSelection.SetEquals( restored );
		if( restored.Count != 1 )
		{
			if( selectionChanged )
				_ = ViewModel.ShowEntryDetailsAsync( null );
		}
		else if( selectionChanged )
		{
			_ = ViewModel.ShowEntryDetailsAsync( restored[ 0 ] );
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
			{
				_collapsedGroupKeys.Remove( header.DisplayKey );
				_expandedGroupKeys.Add( header.DisplayKey );
			}
			else
			{
				_expandedGroupKeys.Remove( header.DisplayKey );
				_collapsedGroupKeys.Add( header.DisplayKey );
			}
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

		bool hasAddedHeader = false;
		for( int i = 0; i < e.AddedItems.Count; i++ )
		{
			if( e.AddedItems[ i ] is EntryGroupHeader )
			{
				hasAddedHeader = true;
				break;
			}
		}
		if( hasAddedHeader )
		{
			_syncingGroupedSelection = true;
			try
			{
				List<EntryGroupHeader> headers = GroupedEntryList.SelectedItems.ExtractSelected<EntryGroupHeader>();
				foreach( EntryGroupHeader header in headers )
					GroupedEntryList.SelectedItems.Remove( header );
				for( int i = 0; i < e.RemovedItems.Count; i++ )
				{
					if( e.RemovedItems[ i ] is EntryRow removedRow && !GroupedEntryList.SelectedItems.Contains( removedRow ) )
						GroupedEntryList.SelectedItems.Add( removedRow );
				}
			}
			finally
			{
				_syncingGroupedSelection = false;
			}
			return;
		}

		List<EntryGroupHeader> selectedHeaders = GroupedEntryList.SelectedItems.ExtractSelected<EntryGroupHeader>();
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
		List<EntryRow> selectedRows = GroupedEntryList.SelectedItems.ExtractSelected<EntryRow>();
		ViewModel.SelectedEntries = selectedRows;
		_ = ViewModel.ShowEntryDetailsAsync( selectedRows.Count == 1 ? selectedRows[ 0 ] : null );
	}

	private void GroupedEntryList_RightTapped( object sender, RightTappedRoutedEventArgs e )
	{
		if( FindGroupedEntryRow( e.OriginalSource as DependencyObject ) is { } row
			&& !GroupedEntryList.SelectedItems.Contains( row ) )
		{
			_syncingGroupedSelection = true;
			try
			{
				GroupedEntryList.SelectedItems.Clear();
				GroupedEntryList.SelectedItems.Add( row );
			}
			finally
			{
				_syncingGroupedSelection = false;
			}
			ViewModel.SelectedEntries = new[] { row };
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
		_groupDefaultCollapsed = false;
		_collapsedGroupKeys.Clear();
		_expandedGroupKeys.Clear();
		foreach( EntryGroupHeader group in _groups )
			group.IsExpanded = true;
		RefreshGroupedFlatList();
	}

	private void CollapseAllGroups_Click( object sender, RoutedEventArgs e )
	{
		_groupDefaultCollapsed = true;
		_collapsedGroupKeys.Clear();
		_expandedGroupKeys.Clear();
		foreach( EntryGroupHeader group in _groups )
			group.IsExpanded = false;
		RefreshGroupedFlatList();
	}

	private void GroupedSortColumn_Click( object sender, RoutedEventArgs e )
	{
		if( sender is not FrameworkElement { Tag: string field } )
			return;
		bool descending = ViewModel.EntrySort is var (currentField, currentDesc)
			&& string.Equals( currentField, field, StringComparison.Ordinal )
			&& !currentDesc;
		_ = ViewModel.SortEntriesAsync( field, descending );
	}

	private void SelectEntryRows( IReadOnlyList<EntryRow> rows, bool focus = false, bool scroll = true )
	{
		if( rows.Count == 0 )
			return;
		if( IsUsingGroupedList )
		{
			HashSet<EntryRow> targetSet = [ .. rows ];
			bool expandedAny = false;
			for( int g = 0; g < _groups.Count; g++ )
			{
				EntryGroupHeader group = _groups[ g ];
				if( group.IsExpanded )
					continue;
				for( int i = 0; i < group.Items.Count; i++ )
				{
					if( targetSet.Contains( group.Items[ i ] ) )
					{
						group.IsExpanded = true;
						_collapsedGroupKeys.Remove( group.DisplayKey );
						_expandedGroupKeys.Add( group.DisplayKey );
						expandedAny = true;
						break;
					}
				}
			}
			if( expandedAny )
			{
				RefreshGroupedFlatList( rows );
			}
			else
			{
				_syncingGroupedSelection = true;
				List<EntryRow> selected = new( rows.Count );
				try
				{
					GroupedEntryList.SelectedItems.Clear();
					for( int i = 0; i < _groupedItems.Count; i++ )
					{
						if( _groupedItems[ i ] is EntryRow row && targetSet.Contains( row ) )
						{
							GroupedEntryList.SelectedItems.Add( row );
							selected.Add( row );
						}
					}
				}
				finally
				{
					_syncingGroupedSelection = false;
				}
				ViewModel.SelectedEntries = selected;
			}
			if( ViewModel.SelectedEntries is [ var single ] )
				_ = ViewModel.ShowEntryDetailsAsync( single );
			else if( !expandedAny )
				_ = ViewModel.ShowEntryDetailsAsync( null );
			if( focus )
				GroupedEntryList.Focus( FocusState.Programmatic );
			if( scroll )
				GroupedEntryList.ScrollIntoView( rows[ 0 ] );
			return;
		}
		OdooEntryList.SetSelectionBatch( rows );
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
		else if( e.PropertyName == nameof( HackFileManagerViewModel.IsPreviewBusy ) )
			SyncPipPreview();
	}

	/// <summary>Loads the preview image; a newer preview replaces one still loading.</summary>
	private async Task ShowPreviewAsync( EntryPreview? preview )
	{
		CancellationTokenSource decodeCts = CtsHelper.Replace( ref _previewDecodeCts );
		int request = ++_previewRequest;
		if( preview is null )
		{
			OdooEntryImage.Source = null;
			ViewModel.HasPreviewImage = false;
			ViewModel.IsPreviewDecoding = false;
			SyncPipPreview();
			return;
		}

		ViewModel.HasPreviewImage = false;
		ViewModel.IsPreviewDecoding = true;
		SyncPipPreview();
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
		catch( OperationCanceledException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Preview failed to load: {ex.Message}", ex );
		}
		if( request == _previewRequest && !decodeCts.IsCancellationRequested && !_left )
		{
			OdooEntryImage.Source = image;
			ViewModel.HasPreviewImage = image is not null;
			ViewModel.IsPreviewDecoding = false;
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

	private void OnLiveBatchApplying( bool applying ) => _suppressEntrySelectionSync = applying;

	/// <summary>Re-applies selection after a batched watcher update or sort without rebuilding details unless the single selected row changed.</summary>
	private void OnRowsReplaced( IReadOnlyDictionary<EntryRow, EntryRow?> replacements )
	{
		var selected = ViewModel.SelectedEntries;
		if( selected is not { Count: > 0 } )
			return;

		HashSet<EntryRow> currentEntries = [ .. ViewModel.Entries ];
		List<EntryRow> target = new( selected.Count );
		for( int i = 0; i < selected.Count; i++ )
		{
			EntryRow candidate = selected[ i ];
			if( replacements.TryGetValue( candidate, out EntryRow? next ) )
			{
				if( next is not null && currentEntries.Contains( next ) )
					target.Add( next );
			}
			else if( currentEntries.Contains( candidate ) )
			{
				target.Add( candidate );
			}
		}

		if( IsUsingGroupedList )
		{
			bool changed = target.Count != selected.Count
				|| ( target.Count == 1 && !ReferenceEquals( target[ 0 ], selected[ 0 ] ) );
			ViewModel.SelectedEntries = target;
			if( target.Count == 0 )
				_ = ViewModel.ShowEntryDetailsAsync( null );
			else if( changed )
				_ = ViewModel.ShowEntryDetailsAsync( target.Count == 1 ? target[ 0 ] : null );
			return;
		}

		if( target.Count == 0 )
		{
			ViewModel.SelectedEntries = [];
			_ = ViewModel.ShowEntryDetailsAsync( null );
			return;
		}
		SelectEntryRows( target, scroll: false );
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
		if( _left
			|| VisibleNodeFor( treePath ) is not { } node
			|| OdooDirectoryTree.ContainerFromNode( node ) is not TreeViewItem { Content: Panel panel } )
			return;

		long now = Environment.TickCount64;
		if( _recentBadges.TryGetValue( node, out var recent ) )
		{
			long sinceLast = now - recent.Tick;
			if( sinceLast < BadgeMinIntervalMs || ( recent.Kind == kind && sinceLast < BadgeCooldownMs ) )
				return;
		}

		if( now - _badgeWindowStartTick >= BadgeCooldownMs )
		{
			_badgeWindowStartTick = now;
			_badgesInWindow = 0;
		}
		if( _badgesInWindow >= MaxBadgesPerWindow )
			return;

		_recentBadges[ node ] = (now, kind);
		_badgesInWindow++;

		RemoveBadges( panel );
		var (text, brush) = kind switch
		{
			LocalChangeKind.Created => ("( + ) created", CreatedBadgeBrush),
			LocalChangeKind.Deleted => ("( - ) deleted", DeletedBadgeBrush),
			_ => ("( . ) updated", UpdatedBadgeBrush),
		};
		TextBlock badge = new()
		{
			Text = text,
			Tag = ChangeBadgeTag,
			FontSize = Math.Max( 9, UIStorage.ColumnsInfo.FontSize - 1 ),
			Margin = new Thickness( 8, 0, 0, 0 ),
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = brush,
			Opacity = 0,
		};
		panel.Children.Add( badge );

		// fade in, hold, fade out, then go
		var fade = new DoubleAnimationUsingKeyFrames();
		fade.KeyFrames.Add( new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds( 200 ), Value = 1 } );
		fade.KeyFrames.Add( new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds( 2400 ), Value = 1 } );
		fade.KeyFrames.Add( new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds( 3200 ), Value = 0 } );
		Storyboard.SetTarget( fade, badge );
		Storyboard.SetTargetProperty( fade, nameof( Opacity ) );
		var story = new Storyboard();
		story.Children.Add( fade );
		EventHandler<object>? onCompleted = null;
		onCompleted = ( _, _ ) =>
		{
			story.Completed -= onCompleted;
			RemoveBadgeEntry( story );
			panel.Children.Remove( badge );
		};
		_activeBadgeStoryboards.Add( (story, panel, badge, onCompleted) );
		story.Completed += onCompleted;
		story.Begin();
		BadgesShown++;
	}

	private void RemoveBadgeEntry( Storyboard story )
	{
		for( int i = _activeBadgeStoryboards.Count - 1; i >= 0; i-- )
		{
			if( ReferenceEquals( _activeBadgeStoryboards[ i ].Story, story ) )
				_activeBadgeStoryboards.RemoveAt( i );
		}
	}

	private void StopAllBadgeStoryboards()
	{
		for( int i = _activeBadgeStoryboards.Count - 1; i >= 0; i-- )
		{
			var (story, panel, badge, completed) = _activeBadgeStoryboards[ i ];
			story.Completed -= completed;
			story.Stop();
			panel.Children.Remove( badge );
		}
		_activeBadgeStoryboards.Clear();
	}

	/// <summary>How many change badges this page has shown (the smoke tests read it).</summary>
	internal int BadgesShown { get; private set; }

	/// <summary>Switches between the Entries and Changes tabs (for smoke visual checks).</summary>
	internal void ShowTopTab( bool changes ) => ListTabs.SelectedItem = changes ? ChangesTab : RootEntryList;

	/// <summary>The node of <paramref name="treePath"/>, or of its nearest parent that is in the tree and not inside a collapsed folder.</summary>
	private TreeViewNode? VisibleNodeFor( string treePath )
	{
		if( OdooDirectoryTree.RootNodes.Count == 0 )
			return null;
		TreeViewNode node = OdooDirectoryTree.RootNodes[ 0 ];
		string[] parts = PwaPaths.Segments( treePath );
		for( int p = 0; p < parts.Length; p++ )
		{
			if( !node.IsExpanded )
				break; // its children are not shown
			string part = parts[ p ];
			TreeViewNode? next = null;
			for( int i = 0; i < node.Children.Count; i++ )
			{
				if( string.Equals( node.Children[ i ].LinkedData.Name, part, StringComparison.OrdinalIgnoreCase ) )
				{
					next = node.Children[ i ];
					break;
				}
			}
			if( next is null )
				break;
			node = next;
		}
		return node;
	}

	private void RemoveBadges( Panel panel )
	{
		for( int i = _activeBadgeStoryboards.Count - 1; i >= 0; i-- )
		{
			var (story, trackedPanel, badge, completed) = _activeBadgeStoryboards[ i ];
			if( ReferenceEquals( trackedPanel, panel ) )
			{
				story.Completed -= completed;
				story.Stop();
				panel.Children.Remove( badge );
				_activeBadgeStoryboards.RemoveAt( i );
			}
		}
		for( int i = panel.Children.Count - 1; i >= 0; i-- )
		{
			if( panel.Children[ i ] is TextBlock tb && ChangeBadgeTag.Equals( tb.Tag ) )
				panel.Children.RemoveAt( i );
		}
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
			? new DirectoryRef( data.Name ?? "", data.DirectoryId ?? 0, data.FullPath ?? data.Name ?? "", data.Origin == FolderOriginKind.RemoteOnly )
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
			SubfolderMenuLabel.Text = $"{childCount} subfolder{( childCount == 1 ? "" : "s" )}";
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
		UIStorage.SaveSafe();
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
		string text = FolderFilterBox.Text;
		bool empty = string.IsNullOrWhiteSpace( text );
		ClearFilterButton.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
		_treeFilterTask = RebuildDisplayedTreeAsync( text, debounceMs: empty ? 0 : FolderFilterDebounceMs );
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
		try
		{
			string? path = ViewModel.LastSelectedNodePath ?? LastSelectedNode?.LinkedData.FullPath;
			if( string.IsNullOrWhiteSpace( path ) )
				return;
			ClipboardHelper.TrySetText( path, "FileManager" );

			int version = ++_copyFeedbackVersion;
			CopyPathIcon.Glyph = "\uE8FB";
			CopyPathLabel.Text = "Copied";
			await Task.Delay( 1400, _leaveCts.Token );
			if( _left || version != _copyFeedbackVersion )
				return;
			CopyPathIcon.Glyph = "\uE8C8";
			CopyPathLabel.Text = "Copy path";
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Copy path failed: {ex.Message}", ex );
		}
	}

	private void JumpToTreeNode( TreeViewNode node )
	{
		node.LinkedData.EnsureVisible( OdooDirectoryTree );
		if( !ReferenceEquals( LastSelectedNode, node ) )
			SelectDirectory( node );
	}

	private async void OdooDirectoryTree_SelectionChanged( TreeView sender, TreeViewSelectionChangedEventArgs args )
	{
		try
		{
			if( args.AddedItems.FirstOrDefault() is not TreeData data || data.Node is null )
				return;
			// EnsureVisible (below, and in the locate/refresh paths) sets SelectedNode, which raises
			// SelectionChanged again for the same node; reloading then would cancel the load in flight
			// and loop forever without ever listing the entries.
			if( data.Node == _loadingNode )
				return;
			bool sameTarget = _loadingTreePath is not null
				&& PwaPaths.SameTreePath( data.FullPath, _loadingTreePath )
				&& data.DirectoryId == _loadingDirectoryId;
			if( sameTarget && ( TreeSelectionTask is { IsCompleted: false } || !ViewModel.IsEntriesStale ) )
			{
				_loadingNode = data.Node;
				if( !ReferenceEquals( LastSelectedNode, data.Node ) )
					SelectDirectory( data.Node );
				return;
			}

			SelectDirectory( data.Node );
			data.EnsureVisible( sender );
			await LoadSelectedEntriesAsync( debounceMs: TreeSelectionDebounceMs );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Tree selection failed: {ex.Message}", ex );
		}
	}

	/// <summary>Loads the selected directory's entries; a newer load cancels an older one.</summary>
	private Task LoadSelectedEntriesAsync( int debounceMs = 0 )
	{
		_loadingNode = LastSelectedNode;
		_loadingTreePath = ViewModel.LastSelectedNodePath;
		_loadingDirectoryId = ViewModel.SelectedDirectory?.DirectoryId;
		CancellationTokenSource next = CtsHelper.Replace( ref _treeSelectionCancellation );
		if( ViewModel.SelectedEntries.Count > 0 )
			ViewModel.SelectedEntries = [];
		ViewModel.IsListLoaded = false;
		_ = ViewModel.ShowEntryDetailsAsync( null );
		return TreeSelectionTask = LoadAndRestoreAsync( next.Token, debounceMs );
	}

	private async Task LoadAndRestoreAsync( CancellationToken token, int debounceMs )
	{
		try
		{
			if( debounceMs > 0 )
				await Task.Delay( debounceMs, token );
			await ViewModel.LoadEntriesAsync( token );
			if( !_restorePending || token.IsCancellationRequested || _left )
				return;
			_restorePending = false;
			RestoreSortArrow();
#if !WINDOWS
			if( IsUsingGroupedList && _groupingTask is { IsCompleted: false } gt )
				await gt;
#endif
			if( token.IsCancellationRequested || _left )
				return;
			IReadOnlyList<EntryRow> rows = ViewModel.TakeSelectionToRestore();
			if( rows.Count == 0 )
				return;
			SelectEntryRows( rows );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Loading folder entries failed: {ex.Message}", ex );
		}
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
		if( ( e.OriginalSource as FrameworkElement )?.DataContext is not TreeData data || data.Node is null )
			return;
		if( !ReferenceEquals( OdooDirectoryTree.SelectedNode, data.Node ) )
			OdooDirectoryTree.SelectedNode = data.Node;
		else if( !ReferenceEquals( LastSelectedNode, data.Node ) )
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
			item.FontSize = Math.Clamp( UIStorage.ColumnsInfo.FontSize, 10, 14 );
			// a recycled container must not carry another folder's badge
			if( item.Content is Panel panel )
				RemoveBadges( panel );
		}
	}

	#endregion
	#region Entry list and detail tabs

	private void OdooEntryList_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( OdooEntryList.IsUpdatingSelection || _suppressEntrySelectionSync )
			return;
		SyncEntryListSelection();
	}

	private void SyncEntryListSelection()
	{
		if( _suppressEntrySelectionSync )
			return;
		List<EntryRow> selected = OdooEntryList.SelectedItems.ExtractSelected<EntryRow>();
		IReadOnlyList<EntryRow> previous = ViewModel.SelectedEntries;
		bool sameSingle = selected.Count == 1 && previous is [ { } prev ] && ReferenceEquals( selected[ 0 ], prev );
		bool bothEmpty = selected.Count == 0 && previous.Count == 0;
		ViewModel.SelectedEntries = selected;
		if( !sameSingle && !bothEmpty )
			_ = ViewModel.ShowEntryDetailsAsync( selected.Count == 1 ? selected[ 0 ] : null );
	}

	private void VersionTabs_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		ViewModel.SelectedDetailTab = ( VersionTabs.SelectedItem as TabViewItem )?.Name;
		EntryRow? selected = ViewModel.SelectedEntries is [ { } single ] ? single : null;
		if( e.AddedItems.Count > 0 && selected is not null )
			_ = ViewModel.LoadSelectedDetailTabAsync( selected );
	}

	private void OdooEntryList_Sorting( object? sender, TableViewSortingEventArgs e )
	{
		// the view model sorts the source collection; stop TableView sorting it a second time
		e.Handled = true;
		if( !e.Column.CanSort )
			return;
		string? field = e.Column.SortMemberPath
			?? ( e.Column as TableViewBoundColumn )?.Binding?.Path?.Path
			?? e.Column.Header?.ToString();
		if( !EntryRowKeys.TryGetSortKey( field, out _ ) )
			return;

		foreach( var column in OdooEntryList.Columns.Where( c => c != e.Column ) )
			column.SortDirection = null;

		bool descending = e.Column.SortDirection is SortDirection.Ascending;
		e.Column.SortDirection = descending ? SortDirection.Descending : SortDirection.Ascending;

		_ = ViewModel.SortEntriesAsync( field, descending );
	}

	private void SyncHistorySelection()
	{
		HistoryRow? row = OdooHistory.SelectedItem as HistoryRow;
		ViewModel.SelectedHistory = row;
		PreviewVersion( row?.Version );
	}

	private void SyncParentSelection()
	{
		ParentRow? row = OdooParents.SelectedItem as ParentRow;
		PreviewVersion( row?.Version, ResolveLocalVaultPath( row?.BasePath, row?.Name ) );
	}

	private void SyncChildrenSelection()
	{
		ChildrenRow? row = OdooChildren.SelectedItem as ChildrenRow
			?? ( OdooChildren.SelectedItems.Count == 1 ? OdooChildren.SelectedItems[ 0 ] as ChildrenRow : null );
		PreviewVersion( row?.Version, ResolveLocalVaultPath( row?.BasePath, row?.Name ) );
	}

	private static string? ResolveLocalVaultPath( string? basePath, string? name )
	{
		if( string.IsNullOrEmpty( name ) )
			return null;
		string vault = PwaPaths.Vault;
		if( string.IsNullOrWhiteSpace( vault ) )
			return null;
		string relative = string.IsNullOrEmpty( basePath ) ? name : Path.Combine( basePath, name );
		return Path.Combine( vault, relative );
	}

	private void PreviewVersion( int? versionId, string? localPath = null )
		=> _ = ViewModel.PreviewVersionAsync( versionId, localPath );

	private async void OdooParents_DoubleTapped( object sender, DoubleTappedRoutedEventArgs e )
	{
		try
		{
			if( ( e.OriginalSource as DependencyObject ).FindAncestor<TableViewRow>( OdooParents ) is not null
				&& OdooParents.SelectedItems is [ ParentRow row ] )
				await FindSearchSelectionAsync( row.BasePath ?? "", row.Name ?? "" );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Locating parent entry failed: {ex.Message}", ex );
		}
	}

	private async void OdooChildren_DoubleTapped( object sender, DoubleTappedRoutedEventArgs e )
	{
		try
		{
			if( ( e.OriginalSource as DependencyObject ).FindAncestor<TableViewRow>( OdooChildren ) is not null
				&& OdooChildren.SelectedItems is [ ChildrenRow row ] )
				await FindSearchSelectionAsync( row.BasePath ?? "", row.Name ?? "" );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Locating child entry failed: {ex.Message}", ex );
		}
	}

	/// <summary>Selects the directory at <paramref name="pwaPath"/> in the tree, then the entry named <paramref name="fileName"/>.</summary>
	private Task FindSearchSelectionAsync( string pwaPath, string fileName )
		=> OnLocateRequestedAsync( PwaPaths.Segments( pwaPath ), fileName );

	/// <summary>Selects the directory at <paramref name="segmentsBelowRoot"/>, then the entry named <paramref name="fileName"/>.</summary>
	private async Task OnLocateRequestedAsync( IReadOnlyList<string> segmentsBelowRoot, string? fileName )
	{
		try
		{
			if( !string.IsNullOrEmpty( FolderFilterBox.Text ) )
				FolderFilterBox.Text = string.Empty;

			using( CancellationTokenSource treeTimeout = CancellationTokenSource.CreateLinkedTokenSource( _leaveCts.Token ) )
			{
				treeTimeout.CancelAfter( TimeSpan.FromSeconds( 30 ) );
				await ViewModel.WaitForTreeLoadedAsync( treeTimeout.Token );
			}
			if( _treeFilterTask is { IsCompleted: false } filterTask )
				await filterTask;
			if( _left || OdooDirectoryTree.RootNodes.Count == 0 )
				return;

			TreeViewNode node = OdooDirectoryTree.RootNodes[ 0 ];
			foreach( string part in segmentsBelowRoot.Where( p => p.Length > 0 ) )
			{
				node = node.Children.FirstOrDefault( n => string.Equals( n.LinkedData.Name, part, StringComparison.OrdinalIgnoreCase ) )
					?? throw new ArgumentException( $"directory '{part}' not found", nameof( segmentsBelowRoot ) );
			}

			bool changedFolder = !ReferenceEquals( LastSelectedNode, node );
			foreach( var root in OdooDirectoryTree.RootNodes )
				root.IsExpanded = false;
			if( changedFolder )
			{
				SelectDirectory( node );
				Task loadTask = LoadSelectedEntriesAsync();
				node.LinkedData.EnsureVisible( OdooDirectoryTree );
				await loadTask;
			}
			else
			{
				node.LinkedData.EnsureVisible( OdooDirectoryTree );
				if( TreeSelectionTask is { } inFlight )
					await inFlight;
				else if( !ViewModel.IsListLoaded )
					await LoadSelectedEntriesAsync();
			}

			using( CancellationTokenSource listTimeout = CancellationTokenSource.CreateLinkedTokenSource( _leaveCts.Token ) )
			{
				listTimeout.CancelAfter( TimeSpan.FromSeconds( 60 ) );
				await ViewModel.WaitForListLoadedAsync( listTimeout.Token );
			}
#if !WINDOWS
			if( IsUsingGroupedList && _groupingTask is { IsCompleted: false } gt )
				await gt;
#endif
			if( _left || fileName is null || !ReferenceEquals( LastSelectedNode, node ) )
				return;

			EntryRow entry = ViewModel.Entries.FirstOrDefault( e => string.Equals( e.Name, fileName, StringComparison.OrdinalIgnoreCase ) )
				?? throw new ArgumentException( "entry doesn't exist", nameof( fileName ) );
			SelectEntryRows( new[] { entry }, focus: true, scroll: true );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
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
		SelectEntryRows( new[] { entry }, focus: true, scroll: true );
	}

	private void HackFileManagerView_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		bool ctrlDown = KeyboardHelper.IsDown( Windows.System.VirtualKey.Control );
		bool altDown = KeyboardHelper.IsDown( Windows.System.VirtualKey.Menu );

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
		WindowHelper.CreateWindowAndPage<OdooFileTypeManagerView>( out var page, out var window, presetName: WindowHelper.Presets.OdooFileTypeManagerView );
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
			pip.ReturnRequested += OnPipReturnRequested;
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

	private void OnPipReturnRequested( object? sender, EventArgs e ) => ReturnPreviewToPage();

	private void ReturnPreviewToPage()
	{
		if( _pipWindow is null )
			return;

		PipWindow pip = _pipWindow;
		_pipWindow = null;
		pip.ReturnRequested -= OnPipReturnRequested;
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
		if( _pipWindow is { } pip )
		{
			_pipWindow = null;
			pip.ReturnRequested -= OnPipReturnRequested;
			pip.Closed -= OnPipWindowClosed;
		}
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

		FoldersPaneCard.Visibility = FoldersSplitter.Visibility = showFolders.ToVisibility();
		DetailsAndPreviewHost.Visibility = InspectorSplitter.Visibility = hasInspector.ToVisibility();
		DetailsPaneCard.Visibility = showDetails.ToVisibility();
		PreviewPaneCard.Visibility = showPreview.ToVisibility();
		DetailsPreviewSplitter.Visibility = ( showDetails && showPreview ).ToVisibility();

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
	private void MenuTogglePane_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => (l.ShowFoldersPane, l.ShowDetailsPane, l.ShowPreviewPane) = (MenuShowFolders.IsChecked, MenuShowDetails.IsChecked, MenuShowPreview.IsChecked) );
	private void DockFoldersLeft_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => (l.FoldersDock, l.ShowFoldersPane) = (FolderDockSide.Left, true) );
	private void DockFoldersRight_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => (l.FoldersDock, l.ShowFoldersPane) = (FolderDockSide.Right, true) );
	private void HideFoldersPane_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.ShowFoldersPane = false );
	private void ToggleInspectorDock_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => l.DetailsDock = l.DetailsDock == DetailsDockPosition.Bottom ? DetailsDockPosition.Right : DetailsDockPosition.Bottom );
	private void DockInspectorBottom_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => (l.DetailsDock, l.ShowDetailsPane) = (DetailsDockPosition.Bottom, true) );
	private void DockInspectorRight_Click( object sender, RoutedEventArgs e ) => UpdateLayout( l => (l.DetailsDock, l.ShowDetailsPane) = (DetailsDockPosition.Right, true) );
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

		ZoneLeft.Visibility = ( paneTag == "Folders" ).ToVisibility();
		ZoneRight.Visibility = ( paneTag is "Folders" or "Details" ).ToVisibility();
		ZoneBottom.Visibility = ( paneTag == "Details" ).ToVisibility();
		ZoneSwapPreview.Visibility = ( paneTag == "Preview" ).ToVisibility();

		if( paneTag == "Preview" )
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
		if( IsPointInsideDropZone( ZoneLeft, pos ) ) return nameof( FolderDockSide.Left );
		if( IsPointInsideDropZone( ZoneRight, pos ) ) return nameof( FolderDockSide.Right );
		if( IsPointInsideDropZone( ZoneBottom, pos ) ) return nameof( DetailsDockPosition.Bottom );
		if( IsPointInsideDropZone( ZoneSwapPreview, pos ) ) return "SwapPreview";
		return null;
	}

	private bool IsPointInsideDropZone( FrameworkElement zone, Windows.Foundation.Point pos )
	{
		if( zone.Visibility != Visibility.Visible || zone.ActualWidth <= 0 || zone.ActualHeight <= 0 )
			return false;
		try
		{
			var origin = zone.TransformToVisual( DockDropOverlay ).TransformPoint( new Windows.Foundation.Point( 0, 0 ) );
			return pos.X >= origin.X && pos.X <= origin.X + zone.ActualWidth && pos.Y >= origin.Y && pos.Y <= origin.Y + zone.ActualHeight;
		}
		catch
		{
			return false;
		}
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

		// Capture drag state and end the drag BEFORE ReleasePointerCaptures(), which synchronously
		// fires PointerCaptureLost -> EndPaneDrag() and would otherwise wipe pane/zone to null.
		string? pane = _draggingPaneTag;
		string? zone = _activeDropZone;
		EndPaneDrag();
		if( sender is UIElement grip )
			grip.ReleasePointerCaptures();

		if( pane == "Folders" && zone is nameof( FolderDockSide.Left ) or nameof( FolderDockSide.Right ) )
			UpdateLayout( l => l.FoldersDock = zone == nameof( FolderDockSide.Left ) ? FolderDockSide.Left : FolderDockSide.Right );
		else if( pane == "Details" && zone is nameof( DetailsDockPosition.Right ) or nameof( DetailsDockPosition.Bottom ) )
			UpdateLayout( l => l.DetailsDock = zone == nameof( DetailsDockPosition.Right ) ? DetailsDockPosition.Right : DetailsDockPosition.Bottom );
		else if( pane == "Preview" && zone == "SwapPreview" )
			SwapPreviewPlacement_Click( sender, e );
	}

	private void UpdateDropZoneHighlights( string? activeZone )
	{
		ZoneLeft.Opacity = activeZone == nameof( FolderDockSide.Left ) ? 0.95 : 0.25;
		ZoneRight.Opacity = activeZone == nameof( FolderDockSide.Right ) ? 0.95 : 0.25;
		ZoneBottom.Opacity = activeZone == nameof( DetailsDockPosition.Bottom ) ? 0.95 : 0.25;
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
	internal void SelectEntriesForTest( params EntryRow[] rows ) => SelectEntryRows( rows );
	internal TabViewItem HistoryTabItem => HistoryTab;
	internal TabViewItem? LowerTabIndex
	{
		get => VersionTabs.SelectedItem as TabViewItem;
		set => VersionTabs.SelectedItem = value;
	}

	#endregion
}
