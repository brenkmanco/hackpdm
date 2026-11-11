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
/// </summary>
public sealed partial class HackFileManagerView : Page
{
	public HackFileManagerViewModel ViewModel => InstanceManager.HFM;


	/// <summary>Tree node currently selected (the smoke tests read it).</summary>
	public TreeViewNode? LastSelectedNode { get; set; }

	/// <summary>The in-flight directory selection (smoke tests wait on it).</summary>
	public Task? TreeSelectionTask { get; private set; }
	/// <summary>The tree node whose entries are loading or loaded (see OdooDirectoryTree_SelectionChanged).</summary>
	private TreeViewNode? _loadingNode;

	public readonly Dictionary<object, TreeViewNode> ItemToContainerMap = new();

	private TableView OdooEntryList = null!;
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
		BuildEntryList();
		ViewModel.HackLoaded = false;
		Loaded += ( _, _ ) =>
		{
			if( !ViewModel.HackLoaded )
				LoadHackMan();
		};
	}

	protected override void OnNavigatedTo( NavigationEventArgs e )
	{
		base.OnNavigatedTo( e );
		if( !ViewModel.HackLoaded )
			LoadHackMan();
	}

	private void LoadHackMan()
	{
		ViewModel.HackLoaded = true;
		ViewModel.Initialize();

		OdooDirectoryTree.ItemsSource = _treeItems;
		OnGroupingChanged();
		ViewModel.SelectedDetailTab = ( VersionTabs.SelectedItem as TabViewItem )?.Name;

		ViewModel.RefreshRequested += OnRefreshRequested;
		ViewModel.GroupingChanged += OnGroupingChanged;
		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		ViewModel.TreeLoaded += OnTreeLoadedAsync;
		ViewModel.LocateRequested += OnLocateRequestedAsync;

		OdooDirectoryTree.SelectionChanged += OdooDirectoryTree_SelectionChanged;
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
		TreeDownload.DoubleTapped += ( _, _ ) => ViewModel.GetLatestDirectoryCommand.Execute( null );
		TreeUndelete.DoubleTapped += ( _, _ ) => ViewModel.RestoreDirectoryCommand.Execute( null );
		HistoryDownload.DoubleTapped += ( _, _ ) => ViewModel.DownloadHistoryCommand.Execute( null );
		HistoryMove.DoubleTapped += ( _, _ ) => ViewModel.MoveHistoryToTempCommand.Execute( null );

		OdooDirectoryTree.LostFocus += ( _, _ ) =>
		{
			if( OdooDirectoryTree.SelectedNode is { } node )
				SelectDirectory( node );
		};
		Unloaded += ( _, _ ) =>
		{
			_treeSelectionCancellation.Cancel();
			ViewModel.RefreshRequested -= OnRefreshRequested;
			ViewModel.GroupingChanged -= OnGroupingChanged;
			ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
			ViewModel.TreeLoaded -= OnTreeLoadedAsync;
			ViewModel.LocateRequested -= OnLocateRequestedAsync;
		};

		_ = LoadTreeAfterLayoutAsync();
	}

	private async Task LoadTreeAfterLayoutAsync()
	{
		// let the page render before the (slow) tree request starts
		await Task.Delay( 500 );
		await ViewModel.LoadDirectoryTreeAsync();
	}

	#endregion
	#region Entry list construction

	/// <summary>Builds the entry TableView: its columns come from the user's saved column settings.</summary>
	private void BuildEntryList()
	{
		var info = UIStorage.ColumnsInfo;
		OdooEntryList = new TableView
		{
			Name = "OdooEntryList",
			VerticalContentAlignment = VerticalAlignment.Stretch,
			AutoGenerateColumns = false,
			CanSortColumns = true,
			FontSize = info.FontSize,
			IsReadOnly = true,
			RowDetailsVisibilityMode = info.GroupCollapsed ? TableViewRowDetailsVisibilityMode.VisibleWhenSelected : TableViewRowDetailsVisibilityMode.Collapsed,
			RowHeight = info.RowHeight,
			SelectionMode = ListViewSelectionMode.Extended,
		};
		var columns = UIStorage.Columns.Value;
		OdooEntryList.Columns.Add( new TableViewTemplateColumn
		{
			Width = new GridLength( 32 ),
			CellTemplate = (DataTemplate)Resources[ "IconCellTemplate" ],
			CanReorder = true,
			CanResize = true,
		} );
		OdooEntryList.Columns.Add( TextColumn( columns[ nameof( EntryRow.Name ) ] ) ?? TextColumn( "Name", "Name", 350 ) );
		OdooEntryList.Columns.Add( TextColumn( columns[ nameof( EntryRow.Type ) ] ) ?? TextColumn( "Type", "Type" ) );

		var size = columns[ nameof( EntryRow.Size ) ];
		OdooEntryList.Columns.Add( new TableViewTemplateColumn
		{
			Header = size?.Column.Value.Header ?? "Size",
			CellTemplate = (DataTemplate)Resources[ "SizeCellTemplate" ],
			Width = size?.Column.Value.AutoSize is true ? GridLength.Auto : new GridLength( size?.Column.Value.Width ?? 100 ),
		} );

		OdooEntryList.Columns.Add( TextColumn( columns[ nameof( EntryRow.Status ) ] ) ?? TextColumn( "Status", "Status" ) );
		OdooEntryList.Columns.Add( TextColumn( columns[ nameof( EntryRow.Checkout ) ] ) ?? TextColumn( "Checkout", "Checkout" ) );
		OdooEntryList.Columns.Add( TextColumn( columns[ nameof( EntryRow.LocalDate ) ] ) ?? TextColumn( "Local Date", "LocalDate" ) );
		OdooEntryList.Columns.Add( TextColumn( columns[ nameof( EntryRow.RemoteDate ) ] ) ?? TextColumn( "Remote Date", "RemoteDate" ) );
		OdooEntryList.Columns.Add( TextColumn( columns[ nameof( EntryRow.FullName ) ] ) ?? TextColumn( "Full Name", "FullName" ) );

		OdooEntryList.ContextFlyout = BuildEntryContextMenu();
		RootEntryList.Content = OdooEntryList;
	}

	private MenuFlyout BuildEntryContextMenu()
	{
		var vm = ViewModel;
		static MenuFlyoutItem Item( string text, System.Windows.Input.ICommand command, object? parameter = null )
			=> new() { Text = text, Command = command, CommandParameter = parameter };

		var open = new MenuFlyoutSubItem { Text = "Open" };
		open.Items.Add( Item( "Preview Latest Remote", vm.OpenSelectedLatestRemoteCommand ) );
		open.Items.Add( Item( "Latest Local", vm.OpenSelectedLatestLocalCommand ) );
		open.Items.Add( Item( "File Directory", vm.OpenSelectedFoldersCommand ) );
		open.DoubleTapped += ( _, _ ) => vm.OpenSelectedCommand.Execute( null );

		var groupBy = new MenuFlyoutSubItem { Text = "Group By" };
		foreach( var property in typeof( Domain.Representation.EntryRow ).GetProperties() )
			groupBy.Items.Add( Item( property.Name, vm.GroupByCommand, property.Name ) );

		var delete = new MenuFlyoutSubItem { Text = "Delete" };
		delete.Items.Add( Item( "Local", vm.LocalDeleteSelectedCommand ) );
		delete.Items.Add( Item( "Logical", vm.LogicalDeleteSelectedCommand ) );
		delete.Items.Add( Item( "Permanent", vm.PermanentDeleteSelectedCommand ) );
		delete.DoubleTapped += ( _, _ ) => vm.LocalDeleteSelectedCommand.Execute( null );

		MenuFlyout flyout = new();
		flyout.Items.Add( open );
		flyout.Items.Add( Item( "Download", vm.GetLatestSelectedCommand ) );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( Item( "Checkout", vm.CheckoutSelectedCommand ) );
		flyout.Items.Add( Item( "Undo Checkout", vm.UndoCheckoutSelectedCommand ) );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( groupBy );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( Item( "Commit", vm.CommitSelectedCommand ) );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( new MenuFlyoutItem { Text = "Restore" } );
		flyout.Items.Add( Item( "Upload Icon", vm.UploadIconCommand ) );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( delete );
		return flyout;
	}

	private static TableViewTextColumn TextColumn( string header, string bindingPath, double? width = null )
	{
		var column = new TableViewTextColumn { Header = header, Binding = new Binding { Path = new PropertyPath( bindingPath ) } };
		if( width.HasValue )
			column.Width = new GridLength( width.Value );
		return column;
	}

	private static TableViewTextColumn? TextColumn( OdooEntryColumnID? column )
	{
		if( column is null )
			return null;

		var config = column.Column.Value;
		return new TableViewTextColumn
		{
			Header = config.Header,
			Binding = new Binding { Path = new PropertyPath( column.BindingPath ) },
			Width = config.AutoSize ? GridLength.Auto : new GridLength( config.Width is 0D ? 100 : config.Width ),
		};
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
		return TreeSelectionTask = ViewModel.LoadEntriesAsync( _treeSelectionCancellation.Token );
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
			item.FontSize = UIStorage.ColumnsInfo.FontSize;
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
		=> OnLocateRequestedAsync( pwaPath.Split( '\\' ), fileName );

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
		=> OnRefreshRequested( FileManagerRefresh.Tree );

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

	public TreeView GetOdooDirectoryTree() => OdooDirectoryTree;
	public TableView GetOdooEntryList() => OdooEntryList;
	internal TabViewItem HistoryTabItem => HistoryTab;
	internal TabViewItem? LowerTabIndex
	{
		get => VersionTabs.SelectedItem as TabViewItem;
		set => VersionTabs.SelectedItem = value;
	}

	#endregion
}
