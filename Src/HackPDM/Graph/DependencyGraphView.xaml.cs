using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Graph;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Core.Theming;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.System;

using DetailRow = HackPDM.Core.Services.EntryRow;

namespace HackPDM.Graph;

/// <summary>A file in the dependency page's list.</summary>
/// <param name="Id">The hp.entry id.</param>
/// <param name="Name">The file name with its extension.</param>
public sealed record GraphEntryItem( int Id, string Name )
{
	public override string ToString() => Name;
}

/// <summary>
/// Folders on the left, the files of the selected folder in the middle (several can be selected) and
/// the dependency graph of the selected files on the right: what they use (children, the selected files
/// on the left), what uses them (parents, on the right) or both (in the middle). Double-click a file in
/// the graph to show the graph around it.
/// </summary>
public sealed partial class DependencyGraphView : Page
{
	private readonly List<GraphEntryItem> _entries = [];
	private readonly Dictionary<int, GraphNode> _nodesById = [];
	private readonly SolidColorBrush _infoHeaderBrush = new();
	private CancellationTokenSource? _entriesLoad;
	private CancellationTokenSource? _graphLoad;
	private bool _quietSelection;
	private bool _themeSubscribed;
	private DependencyGraph _graph = DependencyGraph.Empty;
	/// <summary>The files the graph is drawn around: the list's selection, or a file double-clicked in the graph.</summary>
	private IReadOnlyList<int> _roots = [];

	private readonly IEntryDetailsService _details = new EntryDetailsService();
	private readonly IPreviewImageLoader _previews = new PreviewImageLoader();
	private CancellationTokenSource? _detailLoad;
	private CancellationTokenSource? _previewLoad;
	/// <summary>The file whose details are open; null when the panel is closed.</summary>
	private GraphNode? _detailNode;
	private const double DetailPanelSpace = 336;

	// the file manager's detail tables, for the clicked file
	public ObservableCollection<TreeData> BreadcrumbPath { get; } = [];
	private TreeViewNode? _selectedFolderNode;
	private TreeData? _selectedFolderData;
	private TreeViewNode? CurrentSelectedFolderNode => _selectedFolderNode ?? FolderTree.SelectedNode;
	public RangeObservableCollection<HistoryRow> HistoryRows { get; } = [];
	public RangeObservableCollection<ParentRow> ParentRows { get; } = [];
	public RangeObservableCollection<ChildrenRow> ChildRows { get; } = [];
	public RangeObservableCollection<PropertiesRow> PropertyRows { get; } = [];
	public RangeObservableCollection<VersionRow> VersionRows { get; } = [];

	public DependencyGraphView()
	{
		InitializeComponent();
		InfoHeader.Background = _infoHeaderBrush;
		Graph.Theme = AppComposition.Theme.GraphTheme;
		GraphBranchLinesToggle.IsChecked = AppComposition.Theme.ShowTreeBranchLines;
		ApplyGraphPaneLayout();
		Unloaded += ( _, _ ) => UnsubscribeTheme();
		FolderTree.SelectionChanged += ( sender, args ) =>
		{
			if( SelectedNodeFromArgs( args ) is not { } node || ReferenceEquals( node, _selectedFolderNode ) )
				return;
			SyncFolderSelection( node );
			_ = LoadEntriesAsync( node );
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
		ShowChildren.Checked += ( _, _ ) => _ = LoadGraphAsync();
		ShowParents.Checked += ( _, _ ) => _ = LoadGraphAsync();
		ShowBoth.Checked += ( _, _ ) => _ = LoadGraphAsync();
		ZoomIn.Click += ( _, _ ) => Graph.ZoomBy( 1.25 );
		ZoomOut.Click += ( _, _ ) => Graph.ZoomBy( 0.8 );
		FitButton.Click += ( _, _ ) => Graph.Fit();
		Graph.ZoomChanged += zoom => ZoomText.Text = $"{zoom * 100:0}%";
		Graph.NodeSelected += node =>
		{
			ShowStatus();
			_ = ShowDetailsAsync( node );
		};
		Graph.NodeInvoked += Focus;
		DetailClose.Click += ( _, _ ) => CloseDetails();
		DetailTabs.SelectionChanged += ( _, _ ) => _ = LoadDetailTabAsync();
		InfoFocus.Click += ( _, _ ) =>
		{
			if( _detailNode is { } node )
				Focus( node );
		};
		InfoLocate.Click += ( _, _ ) => _ = LocateAsync();
		KeyboardAccelerator escape = new() { Key = VirtualKey.Escape };
		escape.Invoked += ( _, e ) =>
		{
			if( DetailPanel.Visibility == Visibility.Visible )
			{
				e.Handled = true;
				CloseDetails();
			}
		};
		KeyboardAccelerators.Add( escape );
	}

	/// <summary>Which dependencies the switch asks for.</summary>
	public GraphDirection Direction => ShowParents.IsChecked == true ? GraphDirection.Parents
		: ShowBoth.IsChecked == true ? GraphDirection.Both
		: GraphDirection.Children;

	/// <summary>The graph on screen (the smoke tests read it).</summary>
	internal NodeGraphView GraphView => Graph;

	/// <summary>The files listed for the selected folder.</summary>
	internal IReadOnlyList<GraphEntryItem> Entries => _entries;

	/// <summary>The folder tree (the smoke tests pick folders in it).</summary>
	internal TreeView Folders => FolderTree;

	/// <summary>Opens <paramref name="node"/>'s folder (the smoke tests).</summary>
	internal Task OpenFolderAsync( TreeViewNode node )
	{
		SyncFolderSelection( node );
		node.LinkedData.EnsureVisible( FolderTree );
		FolderTree.SelectedNode = node;
		return PendingLoad = LoadEntriesAsync( node );
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
	internal void SetDirection( GraphDirection direction )
		=> ( direction switch { GraphDirection.Parents => ShowParents, GraphDirection.Both => ShowBoth, _ => ShowChildren } ).IsChecked = true;

	/// <summary>The files the graph was last drawn around.</summary>
	internal IReadOnlyList<int> Roots => _graph.Roots;

	/// <summary>The load in progress (the smoke tests wait on it).</summary>
	internal Task? PendingLoad { get; private set; }

	protected override void OnNavigatedTo( NavigationEventArgs e )
	{
		base.OnNavigatedTo( e );
		SubscribeTheme();
		Loaded += OnLoaded;
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

	private static CancellationTokenSource ReplaceCts( ref CancellationTokenSource? field )
	{
		CancellationTokenSource? prev = field;
		CancellationTokenSource next = new();
		field = next;
		if( prev is not null )
		{
			try { prev.Cancel(); }
			finally { prev.Dispose(); }
		}
		return next;
	}

	private static void CancelAndDispose( ref CancellationTokenSource? field )
	{
		CancellationTokenSource? prev = field;
		field = null;
		if( prev is not null )
		{
			try { prev.Cancel(); }
			finally { prev.Dispose(); }
		}
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		Loaded -= OnLoaded;
		// as in the file manager: the items are nodes (DirectoryTreeBuilder), but Uno binds the item
		// template to them only when the tree has an (empty) items source, set once the page is loaded;
		// the nodes are added after the first layout or the folders opened below stay closed
		FolderTree.ItemsSource = new ObservableCollection<TreeData>();
		PendingLoad = LoadTreeAfterLayoutAsync();
	}

	private async Task LoadTreeAfterLayoutAsync()
	{
		TreeLoadingOverlay.Visibility = Visibility.Visible;
		TreeBusyRing.IsActive = true;
		await Task.Delay( AppComposition.FileManagerState.Tree is null ? 30 : 16 );
		await LoadTreeAsync();
	}

	protected override void OnNavigatedFrom( NavigationEventArgs e )
	{
		UnsubscribeTheme();
		CancelAndDispose( ref _detailLoad );
		CancelAndDispose( ref _previewLoad );
		CancelAndDispose( ref _entriesLoad );
		CancelAndDispose( ref _graphLoad );
		base.OnNavigatedFrom( e );
	}

	// ------------------------------------------------------------------ folders and files

	private async Task LoadTreeAsync()
	{
		try
		{
			TreeLoadingOverlay.Visibility = Visibility.Visible;
			TreeBusyRing.IsActive = true;
			DirectoryNode? root = AppComposition.FileManagerState.Tree ?? await new DirectoryTreeService().LoadAsync();
			if( root is null )
			{
				Status.Text = "The folder tree could not be read from the server.";
				return;
			}
			AppComposition.FileManagerState.Tree ??= root;
			DirectoryTreeBuilder.Build( FolderTree, root );
			TreeViewNode first = FolderTree.RootNodes[ 0 ];
			first.LinkedData.IsExpanded = true;
			// start in the folder the file manager has open
			string? path = AppComposition.FileManagerState.SelectedPath;
			TreeViewNode target = ( path is null ? null : FolderTree.FindTreeNode( path )?.Node ) ?? first;
			SyncFolderSelection( target );
			FolderTree.SelectedNode = target;
			target.LinkedData.EnsureVisible( FolderTree );
			await LoadEntriesAsync( target );
		}
		finally
		{
			TreeBusyRing.IsActive = false;
			TreeLoadingOverlay.Visibility = Visibility.Collapsed;
		}
	}

	private async Task LoadEntriesAsync( TreeViewNode? node = null )
	{
		CancellationTokenSource load = ReplaceCts( ref _entriesLoad );
		CancellationToken token = load.Token;
		int? directoryId = ( node ?? CurrentSelectedFolderNode )?.LinkedData.DirectoryId;

		_quietSelection = true;
		_entries.Clear();
		EntryList.ItemsSource = null;
		SelectAll.IsChecked = false;
		_quietSelection = false;
		EntryCount.Text = "";
		if( directoryId is not int id || id == 0 )
		{
			EntryCount.Text = "This folder is not on the server.";
			return;
		}

		Busy.IsActive = true;
		EntryBusyBar.Visibility = Visibility.Visible;
		EntryBusyOverlay.Visibility = Visibility.Visible;
		EntryBusyRing.IsActive = true;
		try
		{
			Result<IReadOnlyList<RemoteEntryDto>> rows = await HpDirectory.GetEntriesAsync( id );
			if( token.IsCancellationRequested )
				return;
			if( !rows.Ok )
			{
				EntryCount.Text = $"The files could not be read: {rows.Error}";
				return;
			}
			_entries.AddRange( rows.Value!.Where( static r => !r.Deleted ).OrderBy( static r => r.Name, StringComparer.OrdinalIgnoreCase )
				.Select( static r => new GraphEntryItem( r.Id, r.Name ) ) );
			EntryList.ItemsSource = _entries;
			EntryCount.Text = _entries.Count == 1 ? "1 file" : $"{_entries.Count} files";
		}
		finally
		{
			if( ReferenceEquals( _entriesLoad, load ) )
			{
				Busy.IsActive = false;
				EntryBusyRing.IsActive = false;
				EntryBusyBar.Visibility = Visibility.Collapsed;
				EntryBusyOverlay.Visibility = Visibility.Collapsed;
			}
		}
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
		List<int> ids = [ .. EntryList.SelectedItems.OfType<GraphEntryItem>().Select( i => i.Id ) ];
		_quietSelection = true;
		SelectAll.IsChecked = ids.Count > 0 && ids.Count == _entries.Count;
		_quietSelection = false;
		_roots = ids;
		PendingLoad = LoadGraphAsync( debounce: true );
	}

	/// <summary>Draws the graph around one file (double-clicked in the graph).</summary>
	private void Focus( GraphNode node )
	{
		GraphEntryItem? listed = null;
		for( int i = 0; i < _entries.Count; i++ )
		{
			if( _entries[ i ].Id == node.Id )
			{
				listed = _entries[ i ];
				break;
			}
		}
		if( listed is not null )
		{
			// in this folder: make it the list's only selection, which redraws the graph
			EntryList.SelectedItems.Clear();
			EntryList.SelectedItem = listed;
			EntryList.ScrollIntoView( listed );
			return;
		}
		_quietSelection = true;
		EntryList.SelectedItems.Clear();
		SelectAll.IsChecked = false;
		_quietSelection = false;
		_roots = new[] { node.Id };
		PendingLoad = LoadGraphAsync();
	}

	// ------------------------------------------------------------------ graph

	private async Task LoadGraphAsync( bool debounce = false )
	{
		CancellationTokenSource load = ReplaceCts( ref _graphLoad );
		CancellationToken token = load.Token;
		IReadOnlyList<int> roots = _roots;
		GraphDirection direction = Direction;
		try
		{
			// clicking through the list or shift-selecting fires several changes in a row
			if( debounce )
				await Task.Delay( 150, token );
			Busy.IsActive = true;
			GraphBusyOverlay.Visibility = roots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
			GraphBusyRing.IsActive = roots.Count > 0;
			Result<DependencyGraph> graph = await DependencyGraphLoader.LoadAsync( roots, direction, token );
			if( token.IsCancellationRequested )
				return;
			if( !graph.Ok )
			{
				Status.Text = $"The dependencies could not be read: {graph.Error}";
				return;
			}
			_graph = graph.Value!;
			_nodesById.Clear();
			for( int i = 0; i < _graph.Nodes.Count; i++ )
				_nodesById[ _graph.Nodes[ i ].Id ] = _graph.Nodes[ i ];
			GraphForm form = Graph.Theme.Form;
			GraphLayoutResult arranged = await Task.Run( () => GraphLayout.Arrange( _graph, direction, form.Layout ), token );
			if( token.IsCancellationRequested )
				return;
			Graph.Show( arranged );
			// the open details stay open when their file is still in the graph
			if( _detailNode is { } open && Graph.SelectNode( open.Id ) )
				Graph.EnsureVisible( open.Id, DetailPanelSpace );
			else
				CloseDetails();
			Hint.Visibility = _graph.Nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
			ZoomText.Text = $"{Graph.Zoom * 100:0}%";
			ShowStatus();
		}
		catch( OperationCanceledException )
		{
		}
		finally
		{
			if( ReferenceEquals( _graphLoad, load ) )
			{
				Busy.IsActive = false;
				GraphBusyRing.IsActive = false;
				GraphBusyOverlay.Visibility = Visibility.Collapsed;
			}
		}
	}

	private void ShowStatus()
	{
		if( _graph.Nodes.Count == 0 )
		{
			Status.Text = "";
			return;
		}
		int others = _graph.Nodes.Count - _graph.Roots.Count;
		string what = Direction switch
		{
			GraphDirection.Children => others == 1 ? "1 file used" : $"{others} files used",
			GraphDirection.Parents => others == 1 ? "used by 1 file" : $"used by {others} files",
			_ => others == 1 ? "1 related file" : $"{others} related files",
		};
		string text = $"{_graph.Roots.Count} selected · {what} · {_graph.Edges.Count} links";
		if( _graph.Truncated )
			text += " · stopped early (too many files), not everything is shown";
		if( Graph.SelectedNode is { } node )
			text += $"   |   {node.Path}  ({ByteSize.Format( node.Size, " " )})";
		Status.Text = text;
	}

	// ------------------------------------------------------------------ theme

	/// <summary>The app's theme changed (Settings → Theme): redraw the graph in its colors and shapes, no server call.</summary>
	private async void OnThemeChanged()
	{
		bool show = AppComposition.Theme.ShowTreeBranchLines;
		if( GraphBranchLinesToggle.IsChecked != show )
			GraphBranchLinesToggle.IsChecked = show;
		TreeBranchGuides.RefreshTree( FolderTree, showLines: show, selectedNode: CurrentSelectedFolderNode, updateSelection: true );

		GraphTheme theme = AppComposition.Theme.GraphTheme;
		GraphTheme prev = Graph.Theme;
		if( theme == prev )
			return;
		Graph.Theme = theme;
		if( _detailNode is { } shown )
			_infoHeaderBrush.Color = Graph.HeaderColor( shown );
		if( _graph.Nodes.Count == 0 )
			return;

		// Palette-only changes keep identical box coordinates — skip layout completely
		GraphLayoutResult arranged;
		if( theme.Form.Layout == prev.Form.Layout && Graph.Layout.Nodes.Count > 0 )
		{
			arranged = Graph.Layout;
		}
		else
		{
			DependencyGraph snapshot = _graph;
			GraphDirection dir = Direction;
			arranged = await Task.Run( () => GraphLayout.Arrange( snapshot, dir, theme.Form.Layout ) );
			if( !ReferenceEquals( _graph, snapshot ) || Graph.Theme != theme )
				return;
		}
		Graph.Show( arranged );
		// the open details stay on their file
		if( _detailNode is { } open && Graph.SelectNode( open.Id ) )
			Graph.EnsureVisible( open.Id, DetailPanelSpace );
	}

	// ------------------------------------------------------------------ details of the clicked file

	/// <summary>Closes the details panel, as its close button does (the smoke tests).</summary>
	internal void CloseDetailsForTest() => CloseDetails();

	/// <summary>Whether the details panel is open (the smoke tests).</summary>
	internal bool DetailsOpen => DetailPanel.Visibility == Visibility.Visible;

	/// <summary>The details load in progress (the smoke tests wait on it).</summary>
	internal Task? DetailsLoad { get; private set; }

	/// <summary>The info card's lines (the smoke tests).</summary>
	internal string DetailsText => string.Join( " | ", new[] { InfoPreviewNone, InfoName, InfoType, InfoSize, InfoVersions, InfoState, InfoFolder, InfoModified, InfoUses, InfoUsedBy }
		.Select( t => t.Text ) );

	/// <summary>Shows a detail tab ("History", "Where Used", "Dependents", "Properties", "Info") (the smoke tests).</summary>
	internal void ShowDetailTab( string header )
		=> DetailTabs.SelectedItem = DetailTabs.TabItems.OfType<TabViewItem>().First( t => t.Header as string == header );

	private Task ShowDetailsAsync( GraphNode? node )
	{
		if( node is null )
		{
			CloseDetails();
			return Task.CompletedTask;
		}
		bool same = _detailNode?.Id == node.Id && DetailsOpen;
		_detailNode = node;
		FillInfoCard( node, keepPreview: same );
		DetailPanel.Visibility = Visibility.Visible;
		if( DetailTabs.SelectedItem is null )
			DetailTabs.SelectedIndex = 0;
		Graph.EnsureVisible( node.Id, DetailPanelSpace );
		if( same )
			return DetailsLoad ?? Task.CompletedTask;
		return DetailsLoad = LoadDetailsAsync( node );
	}

	private void CloseDetails()
	{
		CancelAndDispose( ref _detailLoad );
		CancelAndDispose( ref _previewLoad );
		_detailNode = null;
		DetailPanel.Visibility = Visibility.Collapsed;
	}

	private void FillInfoCard( GraphNode node, bool keepPreview = false )
	{
		_infoHeaderBrush.Color = Graph.HeaderColor( node );
		InfoName.Text = node.Name;
		string type = node.TypeName.Length > 0 ? node.TypeName : node.Ext.ToUpperInvariant() + " file";
		InfoType.Text = node.Category.Length > 0 ? $"{type} · {node.Category}" : type;
		InfoSize.Text = ByteSize.Format( node.Size, " " );
		InfoVersions.Text = node.Versions switch
		{
			0 => "no versions",
			1 => $"1 version (id {node.VersionId})",
			_ => $"{node.Versions} versions, latest id {node.VersionId}",
		};
		InfoState.Text = node.Deleted ? "Deleted"
			: node.CheckedOut ? $"Checked out by {( node.CheckoutUser.Length > 0 ? node.CheckoutUser : "someone" )}"
			: "Not checked out";

		int cut = node.Path.LastIndexOf( '\\' );
		InfoFolder.Text = cut > 0 ? node.Path[ ..cut ] : node.Path;
		ToolTipService.SetToolTip( InfoFolder, node.Path );
		InfoModified.Text = DateTime.TryParse( node.Modified, System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime changed ) ? changed.ToLocalTime().ToString( "g" ) : "unknown";

		InfoUses.Text = Related( _graph.Edges.Where( e => e.Parent == node.Id ).Select( e => e.Child ), GraphDirection.Parents );
		InfoUsedBy.Text = Related( _graph.Edges.Where( e => e.Child == node.Id ).Select( e => e.Parent ), GraphDirection.Children );
		InfoLocate.IsEnabled = !node.Deleted && node.Path.Contains( '\\' );

		if( keepPreview )
			return;
		InfoPreview.Source = null;
		InfoPreviewNone.Text = "loading…";
		InfoPreviewNone.Visibility = Visibility.Visible;
	}

	/// <summary>"2 files: a, b" for the linked files in the graph; a hint when <paramref name="hiddenIn"/> does not show them.</summary>
	private string Related( IEnumerable<int> ids, GraphDirection hiddenIn )
	{
		HashSet<int> seen = [];
		List<string> names = [];
		foreach( int id in ids )
		{
			if( seen.Add( id ) && _nodesById.TryGetValue( id, out GraphNode? found ) )
				names.Add( found.Name );
		}
		if( names.Count == 0 )
			return Direction == hiddenIn ? "not shown in this view" : "nothing";
		names.Sort( StringComparer.OrdinalIgnoreCase );
		string list = string.Join( ", ", names.Take( 4 ) ) + ( names.Count > 4 ? $", +{names.Count - 4} more" : "" );
		return names.Count == 1 ? list : $"{names.Count} files: {list}";
	}

	private async Task LoadDetailsAsync( GraphNode node )
	{
		CancellationTokenSource preview = ReplaceCts( ref _previewLoad );
		CancellationToken token = preview.Token;
		await LoadDetailTabAsync();
		try
		{
			byte[]? bytes = node.VersionId > 0 ? await _details.GetPreviewImageAsync( node.VersionId ) : null;
			if( token.IsCancellationRequested || _detailNode?.Id != node.Id )
				return;
			ImageSource? image = await _previews.FromBytesAsync( bytes );
			if( token.IsCancellationRequested || _detailNode?.Id != node.Id )
				return;
			InfoPreview.Source = image;
			InfoPreviewNone.Text = "no preview";
			InfoPreviewNone.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
		}
		catch( Exception e )
		{
			HackLogger.Warn( "DependencyGraph", $"Preview of '{node.Name}' failed: {e.Message}", e );
			if( _detailNode?.Id == node.Id )
				InfoPreviewNone.Text = "no preview";
		}
	}

	/// <summary>Fills the visible detail table for the open file (one server call or two, like the file manager).</summary>
	private Task LoadDetailTabAsync()
	{
		CancellationTokenSource load = ReplaceCts( ref _detailLoad );
		CancellationToken token = load.Token;
		HistoryRows.ClearIfNonEmpty();
		ParentRows.ClearIfNonEmpty();
		ChildRows.ClearIfNonEmpty();
		PropertyRows.ClearIfNonEmpty();
		VersionRows.ClearIfNonEmpty();
		if( _detailNode is not { } node || DetailTabs.SelectedItem is not TabViewItem tab )
			return Task.CompletedTask;
		DetailRow row = new() { Id = node.Id, LatestId = node.VersionId > 0 ? node.VersionId : null };
		return DetailsLoad = FillAsync();

		async Task FillAsync()
		{
			try
			{
				switch( tab.Name )
				{
					case nameof( HistoryTab ):
						Apply( HistoryRows, await _details.GetHistoryAsync( row ) );
						break;
					case nameof( ParentTab ):
						Apply( ParentRows, await _details.GetParentsAsync( row ) );
						break;
					case nameof( ChildTab ):
						Apply( ChildRows, await _details.GetChildrenAsync( row ) );
						break;
					case nameof( PropertiesTab ):
						Apply( PropertyRows, await _details.GetPropertiesAsync( row ) );
						break;
					case nameof( InfoTab ):
						Apply( VersionRows, await _details.GetVersionInfoAsync( row ) );
						break;
				}
			}
			catch( Exception e )
			{
				HackLogger.Warn( "DependencyGraph", $"{tab.Name} of '{node.Name}' failed: {e.Message}", e );
			}
		}

		void Apply<T>( RangeObservableCollection<T> target, List<T> rows )
		{
			if( !token.IsCancellationRequested )
				target.ReplaceAll( rows );
		}
	}

	/// <summary>Opens the file manager on the open file.</summary>
	private Task LocateAsync()
	{
		if( _detailNode is not { } node )
			return Task.CompletedTask;
		string[] parts = node.Path.Split( '\\', StringSplitOptions.RemoveEmptyEntries );
		// the path starts with the root folder and ends with the file
		return parts.Length < 2 ? Task.CompletedTask : AppComposition.LocateInFileManagerAsync( parts[ 1..^1 ], node.Name );
	}

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
		node?.UpdateBreadCrumbCollection( BreadcrumbPath );
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
		_ = LoadEntriesAsync( node );
	}

	private void BranchLinesToggle_Click( object sender, RoutedEventArgs e )
	{
		bool show = GraphBranchLinesToggle.IsChecked == true;
		AppComposition.Theme.SetShowTreeBranchLines( show );
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

		GraphFoldersCard.Visibility = showFolders ? Visibility.Visible : Visibility.Collapsed;
		GraphFoldersSplitter.Visibility = showFolders ? Visibility.Visible : Visibility.Collapsed;
		GraphFoldersCol.Width = showFolders ? new GridLength( Math.Clamp( layout.GraphFoldersWidth, 180, 480 ) ) : new GridLength( 0 );

		GraphFilesCard.Visibility = showFiles ? Visibility.Visible : Visibility.Collapsed;
		GraphFilesSplitter.Visibility = showFiles ? Visibility.Visible : Visibility.Collapsed;
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

	private void GraphSplitter_DragCompleted()
	{
		try
		{
			UIStorage.Save();
		}
		catch { }
	}
}
