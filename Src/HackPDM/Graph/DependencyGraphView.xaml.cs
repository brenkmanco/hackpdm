using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Graph;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

using Windows.System;

namespace HackPDM.Graph;

/// <summary>
/// Folders on the left, the files of the selected folder in the middle (several can be selected) and
/// the dependency graph of the selected files on the right: what they use (children, the selected files
/// on the left), what uses them (parents, on the right) or both (in the middle). Double-click a file in
/// the graph to show the graph around it.
/// </summary>
public sealed partial class DependencyGraphView : Page
{
	private const double DetailPanelSpace = 336;

	public DependencyGraphViewModel ViewModel { get; } = new( AppComposition.EntryDetails, new PreviewImageLoader() );

	private bool _quietSelection;
	private bool _themeSubscribed;
	private bool _suppressDetailTabLoad;
	private bool _left;
	private TreeViewNode? _selectedFolderNode;
	private TreeData? _selectedFolderData;

	private TreeViewNode? CurrentSelectedFolderNode => _selectedFolderNode ?? FolderTree.SelectedNode;

	public DependencyGraphView()
	{
		InitializeComponent();
		Graph.Theme = AppComposition.Theme.GraphTheme;
		ViewModel.SetCurrentForm( Graph.Theme.Form );
		GraphBranchLinesToggle.IsChecked = AppComposition.Theme.ShowTreeBranchLines;
		ApplyGraphPaneLayout();

		Unloaded += OnUnloaded;
		ViewModel.GraphArranged += OnGraphArranged;
		ViewModel.SelectEntryInListRequested += OnSelectEntryInListRequested;
		ViewModel.ClearEntryListSelectionRequested += OnClearEntryListSelectionRequested;
		ViewModel.ZoomRequested += factor => Graph.ZoomBy( factor );
		ViewModel.FitRequested += () => Graph.Fit();

		FolderTree.SelectionChanged += ( _, args ) =>
		{
			if( SelectedNodeFromArgs( args ) is not { } node || ReferenceEquals( node, _selectedFolderNode ) )
				return;
			SyncFolderSelection( node );
			ViewModel.PendingLoad = ViewModel.LoadEntriesAsync( node.LinkedData.DirectoryId, debounce: true );
		};
		FolderTree.Expanding += ( _, e ) =>
		{
			e.Node?.LinkedData.NotifyBranchChanged();
			TreeBranchGuides.RefreshTree( FolderTree, selectedNode: CurrentSelectedFolderNode, updateSelection: true );
		};
		FolderTree.Collapsed += ( _, e ) =>
		{
			e.Node?.LinkedData.NotifyBranchChanged();
			TreeBranchGuides.RefreshTree( FolderTree, selectedNode: CurrentSelectedFolderNode, updateSelection: true );
		};
		FolderBreadcrumb.ItemClicked += FolderBreadcrumb_ItemClicked;
		EntryList.SelectionChanged += ( _, _ ) => OnEntrySelectionChanged();
		SelectAll.Checked += ( _, _ ) => SetAllSelected( true );
		SelectAll.Unchecked += ( _, _ ) => SetAllSelected( false );
		Graph.ZoomChanged += ViewModel.UpdateZoom;
		Graph.NodeSelected += node =>
		{
			ViewModel.UpdateStatus( node );
			_ = ShowDetailsSafeAsync( node );
		};
		Graph.NodeInvoked += ViewModel.FocusNode;

		DetailTabs.SelectedIndex = 0;
		ViewModel.SetSelectedDetailTab( ResolveDetailTab( DetailTabs.SelectedItem as TabViewItem ), reloadIfOpen: false );
		DetailTabs.SelectionChanged += ( _, _ ) =>
		{
			if( !_suppressDetailTabLoad )
				ViewModel.SetSelectedDetailTab( ResolveDetailTab( DetailTabs.SelectedItem as TabViewItem ) );
		};

		KeyboardAccelerator escape = new() { Key = VirtualKey.Escape };
		escape.Invoked += ( _, e ) =>
		{
			if( ViewModel.IsDetailsOpen )
			{
				e.Handled = true;
				ViewModel.CloseDetails();
			}
		};
		KeyboardAccelerators.Add( escape );
	}

	/// <summary>Which dependencies the switch asks for.</summary>
	public GraphDirection Direction => ViewModel.Direction;

	/// <summary>The graph on screen (the smoke tests read it).</summary>
	internal NodeGraphView GraphView => Graph;

	/// <summary>The files listed for the selected folder.</summary>
	internal IReadOnlyList<GraphEntryItem> Entries => ViewModel.Entries;

	/// <summary>The folder tree (the smoke tests pick folders in it).</summary>
	internal TreeView Folders => FolderTree;

	/// <summary>Opens <paramref name="node"/>'s folder (the smoke tests).</summary>
	internal Task OpenFolderAsync( TreeViewNode node )
	{
		SyncFolderSelection( node );
		node.LinkedData.EnsureVisible( FolderTree );
		FolderTree.SelectedNode = node;
		return ViewModel.PendingLoad = ViewModel.LoadEntriesAsync( node.LinkedData.DirectoryId );
	}

	/// <summary>Selects the listed files named <paramref name="names"/> (the smoke tests).</summary>
	internal void SelectEntries( params string[] names )
	{
		EntryList.SelectedItems.Clear();
		foreach( object item in EntryList.Items )
			if( item is GraphEntryItem e && names.Contains( e.Name, StringComparer.OrdinalIgnoreCase ) )
				EntryList.SelectedItems.Add( item );
	}

	/// <summary>Ticks or clears "Select all" (the smoke tests).</summary>
	internal void SetSelectAll( bool selected ) => SelectAll.IsChecked = selected;

	/// <summary>Flips the Children / Parents / Both switch (the smoke tests).</summary>
	internal void SetDirection( GraphDirection direction ) => ViewModel.Direction = direction;

	/// <summary>The files the graph was last drawn around.</summary>
	internal IReadOnlyList<int> Roots => ViewModel.Roots;

	/// <summary>The load in progress (the smoke tests wait on it).</summary>
	internal Task? PendingLoad => ViewModel.PendingLoad;

	/// <summary>Closes the details panel, as its close button does (the smoke tests).</summary>
	internal void CloseDetailsForTest() => ViewModel.CloseDetails();

	/// <summary>Whether the details panel is open (the smoke tests).</summary>
	internal bool DetailsOpen => ViewModel.IsDetailsOpen;

	/// <summary>The details load in progress (the smoke tests wait on it).</summary>
	internal Task? DetailsLoad => ViewModel.DetailsLoad;

	/// <summary>The info card's lines (the smoke tests).</summary>
	internal string DetailsText => ViewModel.DetailsText;

	/// <summary>Detail tables read by smoke tests.</summary>
	internal RangeObservableCollection<HistoryRow> HistoryRows => ViewModel.HistoryRows;
	internal RangeObservableCollection<ParentRow> ParentRows => ViewModel.ParentRows;
	internal RangeObservableCollection<ChildrenRow> ChildRows => ViewModel.ChildRows;

	/// <summary>Shows a detail tab ("History", "Where Used", "Dependents", "Properties", "Info") (the smoke tests).</summary>
	internal void ShowDetailTab( string header )
		=> DetailTabs.SelectedItem = DetailTabs.TabItems.OfType<TabViewItem>().First( t => t.Header as string == header );

	protected override void OnNavigatedTo( NavigationEventArgs e )
	{
		base.OnNavigatedTo( e );
		_left = false;
		SubscribeTheme();
		Loaded -= OnLoaded;
		Loaded += OnLoaded;
	}

	protected override void OnNavigatedFrom( NavigationEventArgs e )
	{
		Leave();
		base.OnNavigatedFrom( e );
	}

	private void OnUnloaded( object sender, RoutedEventArgs e ) => Leave();

	private void Leave()
	{
		if( _left )
			return;
		_left = true;
		Loaded -= OnLoaded;
		Unloaded -= OnUnloaded;
		UnsubscribeTheme();
		ViewModel.GraphArranged -= OnGraphArranged;
		ViewModel.SelectEntryInListRequested -= OnSelectEntryInListRequested;
		ViewModel.ClearEntryListSelectionRequested -= OnClearEntryListSelectionRequested;
		ViewModel.Dispose();
	}

	private void SubscribeTheme()
	{
		if( _themeSubscribed )
			return;
		_themeSubscribed = true;
		AppComposition.Theme.ThemeChanged += OnThemeChanged;
	}

	private void UnsubscribeTheme()
	{
		if( !_themeSubscribed )
			return;
		_themeSubscribed = false;
		AppComposition.Theme.ThemeChanged -= OnThemeChanged;
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		Loaded -= OnLoaded;
		// as in the file manager: the items are nodes (DirectoryTreeBuilder), but Uno binds the item
		// template to them only when the tree has an (empty) items source, set once the page is loaded;
		// the nodes are added after the first layout or the folders opened below stay closed
		FolderTree.ItemsSource = new ObservableCollection<TreeData>();
		ViewModel.PendingLoad = LoadTreeAfterLayoutAsync();
	}

	private async Task LoadTreeAfterLayoutAsync()
	{
		try
		{
			ViewModel.IsTreeLoading = true;
			await Task.Delay( AppComposition.FileManagerState.Tree is null ? 30 : 16 );
			if( _left )
				return;
			await LoadTreeAsync();
		}
		catch( OperationCanceledException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"Loading dependency graph tree failed: {ex.Message}", ex );
		}
		finally
		{
			if( !_left )
				ViewModel.IsTreeLoading = false;
		}
	}

	private async Task LoadTreeAsync()
	{
		if( _left )
			return;
		DirectoryNode? root = await ViewModel.LoadDirectoryTreeAsync();
		if( _left || root is null )
			return;

		DirectoryTreeBuilder.Build( FolderTree, root );
		if( FolderTree.RootNodes.Count == 0 )
			return;

		TreeViewNode first = FolderTree.RootNodes[ 0 ];
		first.LinkedData.IsExpanded = true;
		// start in the folder the file manager has open
		string? path = AppComposition.FileManagerState.SelectedPath;
		TreeViewNode target = ( path is null ? null : FolderTree.FindTreeNode( path )?.Node ) ?? first;
		SyncFolderSelection( target );
		FolderTree.SelectedNode = target;
		target.LinkedData.EnsureVisible( FolderTree );
		await ViewModel.LoadEntriesAsync( target.LinkedData.DirectoryId );
	}

	private void SetAllSelected( bool selected )
	{
		if( _quietSelection )
			return;
		// ListView.SelectAll is not implemented on Uno: add the rows one by one, then redraw once
		_quietSelection = true;
		EntryList.SelectedItems.Clear();
		if( selected )
			foreach( object item in EntryList.Items )
				EntryList.SelectedItems.Add( item );
		_quietSelection = false;
		OnEntrySelectionChanged();
	}

	private void OnEntrySelectionChanged()
	{
		if( _quietSelection )
			return;
		List<GraphEntryItem> selected = EntryList.SelectedItems.ExtractSelected<GraphEntryItem>();
		List<int> ids = new( selected.Count );
		for( int i = 0; i < selected.Count; i++ )
			ids.Add( selected[ i ].Id );
		_quietSelection = true;
		SelectAll.IsChecked = ids.Count > 0 && ids.Count == ViewModel.Entries.Count;
		_quietSelection = false;
		ViewModel.SetRoots( ids, debounce: true );
	}

	private void OnSelectEntryInListRequested( GraphEntryItem listed )
	{
		EntryList.SelectedItems.Clear();
		EntryList.SelectedItem = listed;
		EntryList.ScrollIntoView( listed );
	}

	private void OnClearEntryListSelectionRequested()
	{
		_quietSelection = true;
		EntryList.SelectedItems.Clear();
		SelectAll.IsChecked = false;
		_quietSelection = false;
	}

	private void OnGraphArranged( GraphLayoutResult arranged )
	{
		if( _left )
			return;
		Graph.Show( arranged );
		// the open details stay open when their file is still in the graph
		if( ViewModel.DetailNode is { } open && Graph.SelectNode( open.Id ) )
			Graph.EnsureVisible( open.Id, DetailPanelSpace );
		else
			ViewModel.CloseDetails();
		ViewModel.UpdateStatus( Graph.SelectedNode );
	}

	private async Task ShowDetailsSafeAsync( GraphNode? node )
	{
		try
		{
			if( _left )
				return;
			if( node is null )
			{
				ViewModel.CloseDetails();
				return;
			}
			if( DetailTabs.SelectedItem is null )
			{
				_suppressDetailTabLoad = true;
				try { DetailTabs.SelectedIndex = 0; }
				finally { _suppressDetailTabLoad = false; }
				ViewModel.SetSelectedDetailTab( ResolveDetailTab( DetailTabs.SelectedItem as TabViewItem ), reloadIfOpen: false );
			}
			Task detailsTask = ViewModel.ShowDetailsAsync( node, Graph.HeaderColor( node ) );
			Graph.EnsureVisible( node.Id, DetailPanelSpace );
			await detailsTask;
		}
		catch( OperationCanceledException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"Showing node details failed: {ex.Message}", ex );
		}
	}

	/// <summary>The app's theme changed (Settings -> Theme): redraw the graph in its colors and shapes, no server call.</summary>
	private async void OnThemeChanged()
	{
		try
		{
			if( _left )
				return;
			bool show = AppComposition.Theme.ShowTreeBranchLines;
			if( GraphBranchLinesToggle.IsChecked != show )
				GraphBranchLinesToggle.IsChecked = show;
			TreeBranchGuides.RefreshTree( FolderTree, showLines: show, selectedNode: CurrentSelectedFolderNode, updateSelection: true );

			GraphTheme theme = AppComposition.Theme.GraphTheme;
			GraphTheme prev = Graph.Theme;
			if( theme == prev )
				return;
			Graph.Theme = theme;
			if( ViewModel.DetailNode is { } shown )
				ViewModel.InfoHeaderBrush.Color = Graph.HeaderColor( shown );

			GraphLayoutResult? arranged = await ViewModel.ArrangeForThemeAsync( prev, theme, Graph.Layout );
			if( _left || arranged is null || Graph.Theme != theme )
				return;

			Graph.Show( arranged );
			if( ViewModel.DetailNode is { } open && Graph.SelectNode( open.Id ) )
				Graph.EnsureVisible( open.Id, DetailPanelSpace );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"Refreshing graph theme failed: {ex.Message}", ex );
		}
	}

	private GraphDetailTab ResolveDetailTab( TabViewItem? tab ) => tab?.Name switch
	{
		nameof( ParentTab ) => GraphDetailTab.WhereUsed,
		nameof( ChildTab ) => GraphDetailTab.Dependents,
		nameof( PropertiesTab ) => GraphDetailTab.Properties,
		nameof( InfoTab ) => GraphDetailTab.Info,
		_ => GraphDetailTab.History,
	};

	private static TreeViewNode? SelectedNodeFromArgs( TreeViewSelectionChangedEventArgs args )
		=> args.AddedItems.FirstOrDefault() switch
		{
			TreeViewNode node => node,
			TreeData data => data.Node,
			_ => null,
		};

	private void SyncFolderSelection( TreeViewNode? node = null )
	{
		node ??= CurrentSelectedFolderNode;
		_selectedFolderNode = node;
		if( _selectedFolderData is not null && !ReferenceEquals( _selectedFolderData.Node, node ) )
			_selectedFolderData.IsSelectedInTree = false;
		_selectedFolderData = node?.LinkedData;
		if( _selectedFolderData is not null )
			_selectedFolderData.IsSelectedInTree = true;
		node?.UpdateBreadCrumbCollection( ViewModel.BreadcrumbPath );
		TreeBranchGuides.RefreshTree( FolderTree, showLines: AppComposition.Theme.ShowTreeBranchLines, selectedNode: node, updateSelection: true );
	}

	private void FolderBreadcrumb_ItemClicked( BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args )
	{
		if( args.Item is not TreeData { Node: { } node } data )
			return;
		if( ReferenceEquals( node, _selectedFolderNode ) )
			return;
		SyncFolderSelection( node );
		FolderTree.SelectedNode = node;
		data.EnsureVisible( FolderTree );
		ViewModel.PendingLoad = ViewModel.LoadEntriesAsync( node.LinkedData.DirectoryId );
	}

	private void BranchLinesToggle_Click( object sender, RoutedEventArgs e )
	{
		bool show = GraphBranchLinesToggle.IsChecked == true;
		AppComposition.Theme.SetShowTreeBranchLines( show );
		UIStorage.SaveSafe();
		TreeBranchGuides.RefreshTree( FolderTree, showLines: show, selectedNode: CurrentSelectedFolderNode, updateSelection: true );
	}

	private void ExpandAllFolders_Click( object sender, RoutedEventArgs e )
	{
		FolderTree.SetAllExpanded( true );
		TreeBranchGuides.RefreshTree( FolderTree, selectedNode: CurrentSelectedFolderNode, updateSelection: true );
	}

	private void CollapseAllFolders_Click( object sender, RoutedEventArgs e )
	{
		FolderTree.SetAllExpanded( false );
		TreeBranchGuides.RefreshTree( FolderTree, selectedNode: CurrentSelectedFolderNode, updateSelection: true );
	}

	private void ApplyGraphPaneLayout()
	{
		var layout = UIStorage.WorkspaceLayout;
		bool showFolders = ToggleFoldersPaneBtn.IsChecked == true;
		bool showFiles = ToggleFilesPaneBtn.IsChecked == true;

		GraphFoldersCard.Visibility = GraphFoldersSplitter.Visibility = showFolders.ToVisibility();
		GraphFoldersCol.Width = showFolders ? new GridLength( Math.Clamp( layout.GraphFoldersWidth, 180, 480 ) ) : new GridLength( 0 );

		GraphFilesCard.Visibility = GraphFilesSplitter.Visibility = showFiles.ToVisibility();
		GraphFilesCol.Width = showFiles ? new GridLength( Math.Clamp( layout.GraphFilesWidth, 180, 480 ) ) : new GridLength( 0 );
	}

	private void ToggleSidePanes_Click( object sender, RoutedEventArgs e )
		=> ApplyGraphPaneLayout();

	private void GraphFoldersSplitter_DragDelta( double delta )
	{
		double next = Math.Clamp( GraphFoldersCol.Width.Value + delta, 180, 480 );
		GraphFoldersCol.Width = new GridLength( next );
		UIStorage.WorkspaceLayout.GraphFoldersWidth = next;
	}

	private void GraphFilesSplitter_DragDelta( double delta )
	{
		double next = Math.Clamp( GraphFilesCol.Width.Value + delta, 180, 480 );
		GraphFilesCol.Width = new GridLength( next );
		UIStorage.WorkspaceLayout.GraphFilesWidth = next;
	}

	private void GraphSplitter_DragCompleted() => UIStorage.SaveSafe();
}
