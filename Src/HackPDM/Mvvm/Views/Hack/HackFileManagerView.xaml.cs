using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Core;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Views.Dialogs;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Settings;
using HackPDM.UI.Types;
using HackPDM.Mvvm.Views.Common.Types;
using HackPDM.Mvvm.ViewModels.Hack;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using WinUI.TableView;
using HackPDM.Shell;
using HackPDM.Mvvm.Views.Odoo;

using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;
using Image = Microsoft.UI.Xaml.Controls.Image;
using TableView = WinUI.TableView.TableView;
using TreeData = HackPDM.Mvvm.Views.Common.Types.TreeData;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using WindowHelper = HackPDM.Mvvm.Views.Common.Controls.WindowHelper;

namespace HackPDM.Mvvm.Views.Hack;

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
		HackChangesList.DoubleTapped += ( _, _ ) => FocusEntryFromChanges( HackChangesList.SelectedItem as EntryRow );
		OdooChangesList.DoubleTapped += ( _, _ ) => FocusEntryFromChanges( OdooChangesList.SelectedItem as EntryRow );
		OdooHistory.SelectionChanged += OdooHistory_SelectionChanged;
		OdooHistory.DoubleTapped += ( _, _ ) => ViewModel.DownloadHistoryWithDependenciesCommand.Execute( null );
		OdooParents.SelectionChanged += ( _, e ) => PreviewVersion( ( e.AddedItems.FirstOrDefault() as Domain.Representation.ParentRow )?.Version );
		OdooParents.DoubleTapped += OdooParents_DoubleTapped;
		OdooChildren.SelectionChanged += ( _, e ) => PreviewVersion( ( e.AddedItems.FirstOrDefault() as Domain.Representation.ChildrenRow )?.Version );
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
		OdooEntryList.FontSize = info.FontSize;
		OdooEntryList.RowHeight = info.RowHeight;
		OdooEntryList.RowDetailsVisibilityMode = info.GroupCollapsed
			? TableViewRowDetailsVisibilityMode.VisibleWhenSelected
			: TableViewRowDetailsVisibilityMode.Collapsed;

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
			EntryGroupBy.Items.Add( new MenuFlyoutItem { Text = field, Command = ViewModel.GroupByCommand, CommandParameter = field } );
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
		if( OdooEntryList.SelectedItems is { Count: > 0 } items && items[ 0 ] is EntryRow entry )
			OdooEntryList.ScrollIntoView( entry );
	}

	private void OnGroupingChanged() => EntryListGrouping.Apply( OdooEntryList, ViewModel.Entries, ViewModel.ActiveGroupKey );

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
			HackPDM.Core.Logging.HackLogger.Warn( "FileManager", $"Preview failed to load: {ex.Message}", ex );
		}
		if( request == _previewRequest )
		{
			OdooEntryImage.Source = image;
			NoPreviewPanel.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
		}
	}

	/// <summary>A row was rebuilt from its changed file: if it was selected, the new one is (which also refreshes the details and preview).</summary>
	private void OnRowReplaced( EntryRow? old, EntryRow? row )
	{
		if( old is null || row is null || ViewModel.SelectedEntries is not { Count: > 0 } selected || !selected.Contains( old ) )
			return;
		if( selected.Count == 1 )
			OdooEntryList.SelectedItem = row;
		else
			OdooEntryList.SelectedItems.Add( row );
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
		string[] parts = treePath.Split( PwaPaths.NameSeparator, StringSplitOptions.RemoveEmptyEntries );
		TreeViewNode? node = null;
		IEnumerable<TreeViewNode> level = OdooDirectoryTree.RootNodes;
		foreach( string part in parts )
		{
			TreeViewNode? next = level.FirstOrDefault( n => string.Equals( n.LinkedData.Name, part, StringComparison.OrdinalIgnoreCase ) );
			if( next is null )
				break;
			node = next;
			if( !node.IsExpanded )
				break; // its children are not shown
			level = node.Children;
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
		if( rows.Count == 1 )
			OdooEntryList.SelectedItem = rows[ 0 ];
		else
			foreach( EntryRow row in rows )
				OdooEntryList.SelectedItems.Add( row );
		OdooEntryList.ScrollIntoView( rows[ 0 ] );
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
		if( e.AddedItems.Count > 0 && OdooEntryList.SelectedItem is EntryRow entry )
			_ = ViewModel.ShowEntryDetailsAsync( entry );
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
		var row = e.AddedItems.FirstOrDefault() as Domain.Representation.HistoryRow;
		ViewModel.SelectedHistory = row;
		PreviewVersion( row?.Version );
	}

	private void PreviewVersion( int? versionId ) => _ = ViewModel.PreviewVersionAsync( versionId );

	private async void OdooParents_DoubleTapped( object sender, DoubleTappedRoutedEventArgs e )
	{
		if( OdooParents.SelectedItems is [ Domain.Representation.ParentRow row ] )
			await FindSearchSelectionAsync( row.BasePath ?? "", row.Name ?? "" );
	}

	private async void OdooChildren_DoubleTapped( object sender, DoubleTappedRoutedEventArgs e )
	{
		if( OdooChildren.SelectedItems is [ Domain.Representation.ChildrenRow row ] )
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
			OdooEntryList.SelectedItem = entry;
			OdooEntryList.Focus( FocusState.Programmatic );
			OdooEntryList.ScrollIntoView( entry );
		}
		catch( Exception ex )
		{
			HackPDM.Core.Logging.HackLogger.Warn( "FileManager", $"Unable to find search selection '{fileName}': {ex.Message}", ex );
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
		OdooEntryList.SelectedItem = entry;
		OdooEntryList.Focus( FocusState.Programmatic );
		OdooEntryList.ScrollIntoView( entry );
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

		ImageSlot.Children.Remove( OdooEntryImage );
		_pipWindow = new PipWindow();
		_pipWindow.HostComponent( OdooEntryImage );
		_pipWindow.ReturnRequested += ( _, _ ) => ReturnPreviewToPage();
		_pipWindow.Closed += OnPipWindowClosed;
		_pipWindow.Activate();
	}

	private void ReturnPreviewToPage()
	{
		if( _pipWindow is null )
			return;

		if( _pipWindow.ReleaseComponent() is { } component && !ImageSlot.Children.Contains( component ) )
			ImageSlot.Children.Add( component );
		_pipWindow.Closed -= OnPipWindowClosed;
		_pipWindow.Close();
		_pipWindow = null;
	}

	private void OnPipWindowClosed( object sender, WindowEventArgs args )
	{
		_pipWindow?.Closed -= OnPipWindowClosed;
		_pipWindow = null;

		// put the preview back once the window is gone (on the UI thread, avoiding duplicates)
		UiDispatcher.Post( () =>
		{
			if( ImageSlot.Children.Contains( OdooEntryImage ) )
				return;
			if( OdooEntryImage.Parent is Panel parent )
				parent.Children.Remove( OdooEntryImage );
			ImageSlot.Children.Add( OdooEntryImage );
		} );
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

		FoldersPaneCard.Visibility = showFolders ? Visibility.Visible : Visibility.Collapsed;
		FoldersSplitter.Visibility = showFolders ? Visibility.Visible : Visibility.Collapsed;
		DetailsAndPreviewHost.Visibility = hasInspector ? Visibility.Visible : Visibility.Collapsed;
		InspectorSplitter.Visibility = hasInspector ? Visibility.Visible : Visibility.Collapsed;
		DetailsPaneCard.Visibility = showDetails ? Visibility.Visible : Visibility.Collapsed;
		PreviewPaneCard.Visibility = showPreview ? Visibility.Visible : Visibility.Collapsed;
		DetailsPreviewSplitter.Visibility = showDetails && showPreview ? Visibility.Visible : Visibility.Collapsed;

		WorkspaceGrid.RowDefinitions.Clear();
		WorkspaceGrid.ColumnDefinitions.Clear();
		_foldersCol = null;
		_inspectorRightCol = null;
		_inspectorBottomRow = null;

		if( layout.DetailsDock == DetailsDockPosition.Bottom )
		{
			int col = 0;
			int mainCol;
			if( showFolders && layout.FoldersDock == FolderDockSide.Left )
			{
				_foldersCol = new ColumnDefinition { Width = new GridLength( Math.Clamp( layout.FoldersPaneWidth, 190, 520 ) ) };
				WorkspaceGrid.ColumnDefinitions.Add( _foldersCol );
				Grid.SetColumn( FoldersPaneCard, col );
				Grid.SetColumn( FoldersSplitter, col + 1 );
				WorkspaceGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
				col += 2;
			}

			mainCol = col++;
			WorkspaceGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );

			if( showFolders && layout.FoldersDock == FolderDockSide.Right )
			{
				WorkspaceGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
				Grid.SetColumn( FoldersSplitter, col++ );
				_foldersCol = new ColumnDefinition { Width = new GridLength( Math.Clamp( layout.FoldersPaneWidth, 190, 520 ) ) };
				WorkspaceGrid.ColumnDefinitions.Add( _foldersCol );
				Grid.SetColumn( FoldersPaneCard, col++ );
			}

			WorkspaceGrid.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
			int rowSpan = 1;
			if( hasInspector )
			{
				WorkspaceGrid.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
				_inspectorBottomRow = new RowDefinition { Height = new GridLength( Math.Clamp( layout.DetailsPaneHeight, 140, 520 ) ) };
				WorkspaceGrid.RowDefinitions.Add( _inspectorBottomRow );
				rowSpan = 3;

				InspectorSplitter.Orientation = Orientation.Horizontal;
				Grid.SetRow( InspectorSplitter, 1 );
				Grid.SetColumn( InspectorSplitter, mainCol );
				Grid.SetColumnSpan( InspectorSplitter, 1 );
				Grid.SetRowSpan( InspectorSplitter, 1 );

				Grid.SetRow( DetailsAndPreviewHost, 2 );
				Grid.SetColumn( DetailsAndPreviewHost, mainCol );
				Grid.SetRowSpan( DetailsAndPreviewHost, 1 );
				Grid.SetColumnSpan( DetailsAndPreviewHost, 1 );
			}

			Grid.SetRow( EntriesPaneCard, 0 );
			Grid.SetColumn( EntriesPaneCard, mainCol );
			Grid.SetRowSpan( EntriesPaneCard, 1 );
			Grid.SetColumnSpan( EntriesPaneCard, 1 );

			if( showFolders )
			{
				Grid.SetRow( FoldersPaneCard, 0 );
				Grid.SetRowSpan( FoldersPaneCard, rowSpan );
				Grid.SetRow( FoldersSplitter, 0 );
				Grid.SetRowSpan( FoldersSplitter, rowSpan );
			}
		}
		else
		{
			WorkspaceGrid.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
			int col = 0;

			if( showFolders && layout.FoldersDock == FolderDockSide.Left )
			{
				_foldersCol = new ColumnDefinition { Width = new GridLength( Math.Clamp( layout.FoldersPaneWidth, 190, 520 ) ) };
				WorkspaceGrid.ColumnDefinitions.Add( _foldersCol );
				Grid.SetRow( FoldersPaneCard, 0 );
				Grid.SetRowSpan( FoldersPaneCard, 1 );
				Grid.SetColumn( FoldersPaneCard, col++ );

				WorkspaceGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
				Grid.SetRow( FoldersSplitter, 0 );
				Grid.SetRowSpan( FoldersSplitter, 1 );
				Grid.SetColumn( FoldersSplitter, col++ );
			}

			WorkspaceGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
			Grid.SetRow( EntriesPaneCard, 0 );
			Grid.SetRowSpan( EntriesPaneCard, 1 );
			Grid.SetColumn( EntriesPaneCard, col++ );

			if( hasInspector )
			{
				WorkspaceGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
				InspectorSplitter.Orientation = Orientation.Vertical;
				Grid.SetRow( InspectorSplitter, 0 );
				Grid.SetRowSpan( InspectorSplitter, 1 );
				Grid.SetColumn( InspectorSplitter, col++ );

				_inspectorRightCol = new ColumnDefinition { Width = new GridLength( Math.Clamp( layout.DetailsRightWidth, 260, 720 ) ) };
				WorkspaceGrid.ColumnDefinitions.Add( _inspectorRightCol );
				Grid.SetRow( DetailsAndPreviewHost, 0 );
				Grid.SetRowSpan( DetailsAndPreviewHost, 1 );
				Grid.SetColumn( DetailsAndPreviewHost, col++ );
			}

			if( showFolders && layout.FoldersDock == FolderDockSide.Right )
			{
				WorkspaceGrid.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
				Grid.SetRow( FoldersSplitter, 0 );
				Grid.SetRowSpan( FoldersSplitter, 1 );
				Grid.SetColumn( FoldersSplitter, col++ );

				_foldersCol = new ColumnDefinition { Width = new GridLength( Math.Clamp( layout.FoldersPaneWidth, 190, 520 ) ) };
				WorkspaceGrid.ColumnDefinitions.Add( _foldersCol );
				Grid.SetRow( FoldersPaneCard, 0 );
				Grid.SetRowSpan( FoldersPaneCard, 1 );
				Grid.SetColumn( FoldersPaneCard, col++ );
			}
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

		bool previewFirst = layout.PreviewPlacement == PreviewDockPlacement.BeforeDetails;

		if( layout.DetailsDock == DetailsDockPosition.Bottom )
		{
			DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
			DetailsPreviewSplitter.Orientation = Orientation.Vertical;

			if( showDetails && showPreview )
			{
				double previewWidth = Math.Clamp( layout.PreviewPaneSize, 180, 560 );
				if( previewFirst )
				{
					_previewBottomCol = new ColumnDefinition { Width = new GridLength( previewWidth ) };
					DetailsAndPreviewHost.ColumnDefinitions.Add( _previewBottomCol );
					DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
					DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
					SetHostCell( PreviewPaneCard, 0, 0 );
					SetHostCell( DetailsPreviewSplitter, 0, 1 );
					SetHostCell( DetailsPaneCard, 0, 2 );
				}
				else
				{
					DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
					DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
					_previewBottomCol = new ColumnDefinition { Width = new GridLength( previewWidth ) };
					DetailsAndPreviewHost.ColumnDefinitions.Add( _previewBottomCol );
					SetHostCell( DetailsPaneCard, 0, 0 );
					SetHostCell( DetailsPreviewSplitter, 0, 1 );
					SetHostCell( PreviewPaneCard, 0, 2 );
				}
			}
			else
			{
				DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
				SetHostCell( showDetails ? DetailsPaneCard : PreviewPaneCard, 0, 0 );
			}
		}
		else
		{
			DetailsAndPreviewHost.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
			DetailsPreviewSplitter.Orientation = Orientation.Horizontal;

			if( showDetails && showPreview )
			{
				double previewHeight = Math.Clamp( layout.PreviewPaneSize, 150, 460 );
				if( previewFirst )
				{
					_previewRightRow = new RowDefinition { Height = new GridLength( previewHeight ) };
					DetailsAndPreviewHost.RowDefinitions.Add( _previewRightRow );
					DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
					DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
					SetHostCell( PreviewPaneCard, 0, 0 );
					SetHostCell( DetailsPreviewSplitter, 1, 0 );
					SetHostCell( DetailsPaneCard, 2, 0 );
				}
				else
				{
					DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
					DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = GridLength.Auto } );
					_previewRightRow = new RowDefinition { Height = new GridLength( previewHeight ) };
					DetailsAndPreviewHost.RowDefinitions.Add( _previewRightRow );
					SetHostCell( DetailsPaneCard, 0, 0 );
					SetHostCell( DetailsPreviewSplitter, 1, 0 );
					SetHostCell( PreviewPaneCard, 2, 0 );
				}
			}
			else
			{
				DetailsAndPreviewHost.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
				SetHostCell( showDetails ? DetailsPaneCard : PreviewPaneCard, 0, 0 );
			}
		}
	}

	private static void SetHostCell( FrameworkElement element, int row, int col )
	{
		Grid.SetRow( element, row );
		Grid.SetColumn( element, col );
		Grid.SetRowSpan( element, 1 );
		Grid.SetColumnSpan( element, 1 );
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

	private void OnSplitterDragCompleted() => SaveWorkspaceLayout();

	private static void SaveWorkspaceLayout()
	{
		try
		{
			AppConfigStore.Save( UIStorage.GlobalData );
		}
		catch
		{
			// Non-fatal in smoke / read-only config directories
		}
	}

	private void ToggleFolderPane_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ShowFoldersPane = ToggleFolderPaneButton.IsChecked == true;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void PresetBalanced_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ApplyPreset( WorkspaceLayoutPreset.Balanced );
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void PresetRightInspector_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ApplyPreset( WorkspaceLayoutPreset.RightInspector );
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void PresetRightSidebar_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ApplyPreset( WorkspaceLayoutPreset.RightSidebar );
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void PresetFocusEntries_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ApplyPreset( WorkspaceLayoutPreset.FocusEntries );
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void MenuTogglePane_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ShowFoldersPane = MenuShowFolders.IsChecked;
		LayoutSettings.ShowDetailsPane = MenuShowDetails.IsChecked;
		LayoutSettings.ShowPreviewPane = MenuShowPreview.IsChecked;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void DockFoldersLeft_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.FoldersDock = FolderDockSide.Left;
		LayoutSettings.ShowFoldersPane = true;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void DockFoldersRight_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.FoldersDock = FolderDockSide.Right;
		LayoutSettings.ShowFoldersPane = true;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void HideFoldersPane_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ShowFoldersPane = false;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void ToggleInspectorDock_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.DetailsDock = LayoutSettings.DetailsDock == DetailsDockPosition.Bottom
			? DetailsDockPosition.Right
			: DetailsDockPosition.Bottom;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void DockInspectorBottom_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.DetailsDock = DetailsDockPosition.Bottom;
		LayoutSettings.ShowDetailsPane = true;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void DockInspectorRight_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.DetailsDock = DetailsDockPosition.Right;
		LayoutSettings.ShowDetailsPane = true;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void SwapPreviewPlacement_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.PreviewPlacement = LayoutSettings.PreviewPlacement == PreviewDockPlacement.AfterDetails
			? PreviewDockPlacement.BeforeDetails
			: PreviewDockPlacement.AfterDetails;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void HideDetailsPane_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ShowDetailsPane = false;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void HidePreviewPane_Click( object sender, RoutedEventArgs e )
	{
		LayoutSettings.ShowPreviewPane = false;
		ApplyWorkspaceLayout();
		SaveWorkspaceLayout();
	}

	private void FoldersGrip_PointerPressed( object sender, PointerRoutedEventArgs e )
		=> StartPaneDrag( "Folders", FoldersDragGrip, e );

	private void DetailsGrip_PointerPressed( object sender, PointerRoutedEventArgs e )
		=> StartPaneDrag( "Details", DetailsDragGrip, e );

	private void PreviewGrip_PointerPressed( object sender, PointerRoutedEventArgs e )
		=> StartPaneDrag( "Preview", PreviewDragGrip, e );

	private void StartPaneDrag( string paneTag, UIElement grip, PointerRoutedEventArgs e )
	{
		_draggingPaneTag = paneTag;
		_isDraggingGrip = true;
		_activeDropZone = null;
		grip.CapturePointer( e.Pointer );

		ZoneLeft.Visibility = paneTag == "Folders" ? Visibility.Visible : Visibility.Collapsed;
		ZoneRight.Visibility = paneTag is "Folders" or "Details" ? Visibility.Visible : Visibility.Collapsed;
		ZoneBottom.Visibility = paneTag == "Details" ? Visibility.Visible : Visibility.Collapsed;
		ZoneSwapPreview.Visibility = paneTag is "Preview" or "Details" ? Visibility.Visible : Visibility.Collapsed;

		UpdateDropZoneHighlights( null );
		DockDropOverlay.Visibility = Visibility.Visible;
		e.Handled = true;
	}

	private void PaneGrip_PointerMoved( object sender, PointerRoutedEventArgs e )
	{
		if( !_isDraggingGrip || _draggingPaneTag is null )
			return;
		var pos = e.GetCurrentPoint( WorkspaceGrid ).Position;
		double w = Math.Max( 1, WorkspaceGrid.ActualWidth );
		double h = Math.Max( 1, WorkspaceGrid.ActualHeight );
		double nx = pos.X / w;
		double ny = pos.Y / h;

		string? zone = _draggingPaneTag switch
		{
			"Folders" => nx < 0.45 ? "Left" : "Right",
			"Details" => nx > 0.65 && ny < 0.72 ? "Right" : ny > 0.55 ? "Bottom" : null,
			"Preview" => ny > 0.35 ? "SwapPreview" : null,
			_ => null,
		};

		if( zone != _activeDropZone )
		{
			_activeDropZone = zone;
			UpdateDropZoneHighlights( zone );
		}
	}

	private void PaneGrip_PointerReleased( object sender, PointerRoutedEventArgs e )
	{
		if( !_isDraggingGrip )
			return;
		if( sender is UIElement grip )
			grip.ReleasePointerCaptures();

		string? pane = _draggingPaneTag;
		string? zone = _activeDropZone;
		_draggingPaneTag = null;
		_isDraggingGrip = false;
		_activeDropZone = null;
		DockDropOverlay.Visibility = Visibility.Collapsed;

		if( pane == "Folders" && zone is "Left" or "Right" )
		{
			LayoutSettings.FoldersDock = zone == "Left" ? FolderDockSide.Left : FolderDockSide.Right;
			ApplyWorkspaceLayout();
			SaveWorkspaceLayout();
		}
		else if( pane == "Details" && zone is "Right" or "Bottom" )
		{
			LayoutSettings.DetailsDock = zone == "Right" ? DetailsDockPosition.Right : DetailsDockPosition.Bottom;
			ApplyWorkspaceLayout();
			SaveWorkspaceLayout();
		}
		else if( pane == "Preview" && zone == "SwapPreview" )
		{
			SwapPreviewPlacement_Click( sender, e );
		}
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
