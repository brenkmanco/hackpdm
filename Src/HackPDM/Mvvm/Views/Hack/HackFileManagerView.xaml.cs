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
		OdooDirectoryTree.Expanding += ( _, e ) => ViewModel.SetExpanded( e.Node?.LinkedData.FullPath, true );
		OdooDirectoryTree.Collapsed += ( _, e ) => ViewModel.SetExpanded( e.Node?.LinkedData.FullPath, false );
		OdooDirectoryTree.RightTapped += OdooDirectoryTree_RightTapped;
		OdooDirectoryBreadcrumb.ItemClicked += OdooDirectoryBreadcrumb_ItemClicked;
		OdooEntryList.SelectionChanged += OdooEntryList_SelectionChanged;
		OdooEntryList.Sorting += OdooEntryList_Sorting;
		OdooHistory.SelectionChanged += OdooHistory_SelectionChanged;
		OdooHistory.DoubleTapped += ( _, _ ) => ViewModel.DownloadHistoryWithDependenciesCommand.Execute( null );
		OdooParents.SelectionChanged += ( _, e ) => PreviewVersion( ( e.AddedItems.FirstOrDefault() as Domain.Representation.ParentRow )?.Version );
		OdooParents.DoubleTapped += OdooParents_DoubleTapped;
		OdooChildren.SelectionChanged += ( _, e ) => PreviewVersion( ( e.AddedItems.FirstOrDefault() as Domain.Representation.ChildrenRow )?.Version );
		OdooChildren.DoubleTapped += OdooChildren_DoubleTapped;

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
		var pendingLocate = ViewModel.Activate();
		_ = LoadTreeAfterLayoutAsync( pendingLocate );
	}

	private async Task LoadTreeAfterLayoutAsync( (IReadOnlyList<string> Segments, string? FileName)? pendingLocate )
	{
		// let the page render before the tree is built (fetching it is slow; the kept one is not)
		await Task.Delay( ViewModel.HasKeptTree ? 50 : 500 );
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
			column.Width = saved?.AutoSize is true ? GridLength.Auto : new GridLength( saved?.Width is > 0 ? saved.Width : 100 );
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
		DirectoryTreeBuilder.Build( OdooDirectoryTree, root );

		// the folders that were open stay open
		foreach( string path in ViewModel.ExpandedPaths.ToList() )
			if( OdooDirectoryTree.FindTreeNode( path ) is { Node: { } open } )
				open.IsExpanded = true;

		if( OdooDirectoryTree.FindTreeNode( ViewModel.LastSelectedNodePath ?? "" ) is { Node: { } node } data )
		{
			data.EnsureVisible( OdooDirectoryTree );
			OdooDirectoryTree.SelectedNode = node;
			SelectDirectory( node );
		}
		return Task.CompletedTask;
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
			Debug.WriteLine( $"preview failed to load: {ex.Message}" );
		}
		if( request == _previewRequest )
			OdooEntryImage.Source = image;
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
		ViewModel.SelectedDirectory = node?.LinkedData is { } data
			? new DirectoryRef( data.Name ?? "", data.DirectoryId ?? 0, data.FullPath ?? data.Name ?? "", data.IsRemoteOnly is true )
			: null;
		node?.UpdateBreadCrumbCollection( BreadcrumbPath );
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
		if( args.Item is not TreeData { Node: { } node } data )
			return;

		OdooDirectoryTree.SelectedNode = node;
		data.EnsureVisible( OdooDirectoryTree );
		SelectDirectory( node );
		foreach( var child in node.Children )
			child.IsExpanded = false;
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
			Debug.WriteLine( $"Unable to find search selection: {ex.Message}" );
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

	private void Search_Click( object sender, RoutedEventArgs e )
	{
		WindowHelper.CreateWindowAndPage<SearchOdooView>( out var page, out var window );
		window.Title = "Search Files";
		page.StoreWindowInstance( window );
	}

	private void ManageTypes_Click( object sender, RoutedEventArgs e )
		=> WindowHelper.CreateWindowPage<OdooFileTypeManagerView>().Title = "Manage Types";

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

		if( _pipWindow.ReleaseComponent() is { } component )
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
	#region Accessors used by the smoke tests

	/// <summary>The directory tree control, for the smoke tests.</summary>
	/// <returns>The page's directory tree view.</returns>
	public TreeView GetOdooDirectoryTree() => OdooDirectoryTree;
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
