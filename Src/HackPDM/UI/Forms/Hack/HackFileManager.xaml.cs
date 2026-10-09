using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Abstractions;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Helper;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.FormTransport;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Data;
using HackPDM.UI.Forms.FormTransport;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Forms.Odoo;
using HackPDM.UI.Forms.Settings;
using HackPDM.UI.Models;
using HackPDM.UI.Types;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

using SolidWorks.Interop.sldworks;

using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.WindowManagement;

using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;
using Directory = System.IO.Directory;
using EntryRow = HackPDM.UI.Types.EntryRow;
using Image = Microsoft.UI.Xaml.Controls.Image;
using ListViewItem = Microsoft.UI.Xaml.Controls.ListViewItem;
using OClient = HackPDM.Infrastructure.Odoo.OdooClient;
using Path = System.IO.Path;
using TreeData = HackPDM.UI.Types.TreeData;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using WindowHelper = HackPDM.UI.Controls.WindowHelper;


namespace HackPDM.UI.Forms.Hack;

// Form Control Constructions
public sealed partial class HackFileManager : Page
{
	private DataGrid OdooEntryList;
	private MenuFlyoutSubItem ListOpen;
	private MenuFlyoutItem ListPreview;
	private MenuFlyoutItem ListLocal;
	private MenuFlyoutItem ListFileDirectory;
	private MenuFlyoutItem ListGetLatest;
	private MenuFlyoutItem ListCheckout;
	private MenuFlyoutItem ListUndoCheckout;
	private MenuFlyoutSubItem ListGroup;
	private List<MenuFlyoutItem> GroupByProp;
	private MenuFlyoutItem ListCommit;
	private MenuFlyoutItem ListRestore;
	private MenuFlyoutItem SaveIcon;
	private MenuFlyoutSubItem ListDelete;
	private MenuFlyoutItem ListDeleteLocal;
	private MenuFlyoutItem ListDeleteLogical;
	private MenuFlyoutItem ListDeletePermanent;
	private void BuildDataGridProgrammatically()
	{
		var info = UIStorage.ColumnsInfo;
		// 1. Instantiate DataGrid
		OdooEntryList = new DataGrid
		{
			Name = "OdooEntryList",
			VerticalContentAlignment = VerticalAlignment.Stretch,
			AutoGenerateColumns = false,
			CanUserSortColumns = true,
			FontSize = info.FontSize,
			IsReadOnly = true,
			RowDetailsVisibilityMode = info.GroupCollapsed ? DataGridRowDetailsVisibilityMode.VisibleWhenSelected : DataGridRowDetailsVisibilityMode.Collapsed,
			RowHeight = info.RowHeight,
			SelectionMode = DataGridSelectionMode.Extended,
			VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
			ItemsSource = GroupedEntries
		};

		// Attach Events
		//OdooEntryList.LoadingRowGroup += OdooEntryDataGrid_LoadingRowGroup;
		//OdooEntryList.Sorting += OdooEntryDataGrid_Sorting;

		// 2. Fetch the Style from XAML and apply it
		var headerStyle = (Style)this.Resources["GroupHeaderStyle"];
		OdooEntryList.RowGroupHeaderStyles.Add( headerStyle );
		OdooEntryList.RowGroupHeaderPropertyNameAlternative = "Items";

		// fetch saved configurations
		var columns = UIStorage.Columns.Value;
		// 3. Build Columns Using XAML Templates

		// Column 1: Icon (Fetch template from Resources)
		var iconColumn = new DataGridTemplateColumn
		{
			Width = new DataGridLength(32, DataGridLengthUnitType.Pixel),
			CellTemplate = (DataTemplate)this.Resources["IconCellTemplate"],
			CanUserReorder = true,
			CanUserResize = true,
		};
		OdooEntryList.Columns.Add( iconColumn );

		// Column 2 & 3: Standard Text Columns
		OdooEntryList.Columns.Add( CreateTextColumn( columns[ nameof( EntryRow.Name ) ] ) ?? CreateTextColumn( "Name", "Name", 350 ) );
		OdooEntryList.Columns.Add( CreateTextColumn( columns[ nameof( EntryRow.Type ) ] ) ?? CreateTextColumn( "Type", "Type" ) );

		// Column 4: Size (Fetch template from Resources)
		var sCol = columns[ nameof( EntryRow.Size ) ];

		var sizeColumn = new DataGridTemplateColumn
		{
			Header = sCol?.Column.Value.Header ?? "Size",
			CellTemplate = ( DataTemplate )this.Resources[ "SizeCellTemplate" ],
			Width = sCol?.Column.Value.AutoSize is true
				? DataGridLength.Auto
				: new DataGridLength( sCol?.Column.Value.Width ?? 100, DataGridLengthUnitType.Pixel ),
			DisplayIndex = sCol?.Column.Value.DisplayIndex ?? -1,
		};
		OdooEntryList.Columns.Add( sizeColumn );

		// Columns 5 - 10
		// OdooEntryList.Columns.Add( CreateTextColumn( "Release", "LatestReleaseId" ) );
		OdooEntryList.Columns.Add( CreateTextColumn( columns[ nameof( EntryRow.Status ) ] ) ?? CreateTextColumn( "Status", "Status" ) );
		OdooEntryList.Columns.Add( CreateTextColumn( columns[ nameof( EntryRow.Checkout ) ] ) ?? CreateTextColumn( "Checkout", "Checkout" ) );
		OdooEntryList.Columns.Add( CreateTextColumn( columns[ nameof( EntryRow.LocalDate ) ] ) ?? CreateTextColumn( "Local Date", "LocalDate" ) );
		OdooEntryList.Columns.Add( CreateTextColumn( columns[ nameof( EntryRow.RemoteDate ) ] ) ?? CreateTextColumn( "Remote Date", "RemoteDate" ) );
		OdooEntryList.Columns.Add( CreateTextColumn( columns[ nameof( EntryRow.FullName ) ] ) ?? CreateTextColumn( "Full Name", "FullName" ) );

		// 4. Build Context Flyout
		BuildContextMenu();

		// 5. Add to UI
		RootEntryList.Content = OdooEntryList;
	}
	private void BuildContextMenu()
	{
		// 6. Build Context Flyout
		MenuFlyout flyout = new();

		// Open SubItem
		ListOpen = new MenuFlyoutSubItem { Name = "ListOpen", Text = "Open" };
		ListPreview = new MenuFlyoutItem { Name = "ListPreview", Text = "Preview Latest Remote" };
		ListLocal = new MenuFlyoutItem { Name = "ListLocal", Text = "Latest Local" };
		ListFileDirectory = new MenuFlyoutItem { Name = "ListFileDirectory", Text = "File Directory" };
		ListOpen.Items.Add( ListPreview );
		ListOpen.Items.Add( ListLocal );
		ListOpen.Items.Add( ListFileDirectory );

		ListGetLatest = new MenuFlyoutItem { Name = "ListGetLatest", Text = "Download" };
		ListCheckout = new MenuFlyoutItem { Name = "ListCheckout", Text = "Checkout" };
		ListUndoCheckout = new MenuFlyoutItem { Name = "ListUndoCheckout", Text = "Undo Checkout" };

		ListGroup = new MenuFlyoutSubItem { Name = "ListGroup", Text = "Group By" };
		GroupByProp = [];
		foreach( var prop in typeof( HackPDM.Domain.Representation.EntryRow ).GetProperties() )
		{
			var item = new MenuFlyoutItem { Name = $"Field_{prop.Name}", Text = prop.Name };
			item.Click += Group_Field_Menu_Item_Click;
			GroupByProp.Add( item );
			ListGroup.Items.Add( item );
		}

		ListCommit = new MenuFlyoutItem { Name = "ListCommit", Text = "Commit" };
		ListRestore = new MenuFlyoutItem { Name = "ListRestore", Text = "Restore" };
		SaveIcon = new MenuFlyoutItem { Name = "SaveIcon", Text = "Upload Icon" };

		// Delete SubItem
		ListDelete = new MenuFlyoutSubItem { Name = "ListDelete", Text = "Delete" };
		ListDeleteLocal = new MenuFlyoutItem { Name = "ListDeleteLocal", Text = "Local" };
		ListDeleteLogical = new MenuFlyoutItem { Name = "ListDeleteLogical", Text = "Logical" };
		ListDeletePermanent = new MenuFlyoutItem { Name = "ListDeletePermanent", Text = "Permanent" };
		ListDelete.Items.Add( ListDeleteLocal );
		ListDelete.Items.Add( ListDeleteLogical );
		ListDelete.Items.Add( ListDeletePermanent );

		// Add everything to flyout with separators
		flyout.Items.Add( ListOpen );
		flyout.Items.Add( ListGetLatest );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( ListCheckout );
		flyout.Items.Add( ListUndoCheckout );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( ListGroup );

		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( ListCommit );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( ListRestore );
		flyout.Items.Add( SaveIcon );
		flyout.Items.Add( new MenuFlyoutSeparator() );
		flyout.Items.Add( ListDelete );

		OdooEntryList.ContextFlyout = flyout;
	}
	// Helper method
	private static DataGridTextColumn CreateTextColumn( string header, string bindingPath, double? width = null )
	{
		var col = new DataGridTextColumn
		{
			Header = header,
			Binding = new Binding { Path = new PropertyPath(bindingPath) },
		};
		if( width.HasValue )
			col.Width = new DataGridLength( width.Value, DataGridLengthUnitType.Pixel );
		return col;
	}
	private static DataGridTextColumn? CreateTextColumn( OdooEntryColumnID? column )
	{
		if( column is null )
			return null;

		var columnConfig = column.Column.Value;
		var col = new DataGridTextColumn
		{
			Header = columnConfig.Header,
			DisplayIndex = columnConfig.DisplayIndex,
			Binding = new Binding { Path = new PropertyPath( column.BindingPath ) },
			Width = columnConfig.AutoSize
				? new DataGridLength( 1, DataGridLengthUnitType.Auto )
				: new DataGridLength( columnConfig.Width is 0D
					? 100
					: columnConfig.Width, DataGridLengthUnitType.Pixel )
		};

		return col;
	}
	private void ToggleGroups_Checked( object sender, RoutedEventArgs e )
	{
		GroupedEntries?.NoGrouping = !ToggleGroups.IsChecked ?? false;
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
}
public sealed partial class HackFileManager : Page
{
	#region Declarations
	public HFM_VM ViewModel => InstanceManager.HFM;

	public DynamicGroupCollection<EntryRow>? GroupedEntries => ViewModel.GroupedEntries;
	public ObservableCollection<HistoryRow> OHistories => ViewModel.OHistories;
	public ObservableCollection<ParentRow> OParents => ViewModel.OParents;
	public ObservableCollection<ChildrenRow> OChildren => ViewModel.OChildren;
	public ObservableCollection<PropertiesRow> OProperties => ViewModel.OProperties;
	public ObservableCollection<VersionRow> OVersions => ViewModel.OVersions;
	public ObservableCollection<TreeData> ONodes => ViewModel.ONodes;
	public ObservableCollection<TreeData>? LastSelectedNodePaths => ViewModel.LastSelectedNodePaths;

	public readonly Dictionary<object, TreeViewNode> ItemToContainerMap = new();
	public StatusDialog? Dialog { get; set; }

	private TreeHelp _treeHelper { get; set; }
	private GridHelp _gridHelper { get; set; }
	internal HackLists _hackLists { get; set; }


	private (object? sender, SelectionChangedEventArgs? e) _queuedEntryChange = (null, null);
	private (TreeView? sender, TreeViewSelectionChangedEventArgs? args) _queuedTreeChange = (null, null);
	public TreeViewNode? LastSelectedNode { get; set; } = null;

	// if EntryPollingMs is set to less than or equal to 0 then it will not poll for changes
	
	private ImageSource? _previewImage = null;
	
	public static DispatcherQueue HackDispatcherQueue;
	internal TabViewItem? LowerTabIndex
	{
		get => VersionTabs.SelectedItem as TabViewItem;
		set => VersionTabs.SelectedItem = value;
	}

	#endregion
	#region Initializers

	public HackFileManager()
	{
		InitializeComponent();
		BuildDataGridProgrammatically();
		ViewModel.HackLoaded = false;
		Loaded += ( _, _ ) =>
		{
			if( !ViewModel.HackLoaded )
				LoadHackMan();
		};
#if DEBUG
		//DebugTest();
		//DebugTest2();
#endif
	}

	protected override void OnNavigatedTo( NavigationEventArgs e )
	{
		base.OnNavigatedTo( e );
		if( !ViewModel.HackLoaded )
		{
			LoadHackMan();
		}
	}
	public static async Task LoadOdooDefaults()
	{
		_ = OdooDefaults.Instance?.HpNodes;
		_ = OdooDefaults.Instance?.MyNode;
		_ = OdooDefaults.Instance?.HpSettings;
		_ = OdooDefaults.Instance?.HpDirectoryRoot;
		//_ = OdooDefaults.Instance?.HpEntryNameFilters;
		//_ = OdooDefaults.Instance?.HpTypes;
		//_ = OdooDefaults.Instance?.HpProperties;
		//_ = OdooDefaults.Instance?.HpCategories;
		//_ = OdooDefaults.Instance?.HpUsers;

		_ = OdooDefaults.Instance?.ExtToFilter; //
		_ = OdooDefaults.Instance?.ExtToType; //
		_ = OdooDefaults.Instance?.ExtToProp;
		_ = OdooDefaults.Instance?.ExtToCat; //
		_ = OdooDefaults.Instance?.IdToProp;
		_ = OdooDefaults.Instance?.IdToUser; //
	}
	public async void LoadHackMan()
	{
		await LoadOdooDefaults();
		_treeHelper = HackApp.Services.GetRequiredService<TreeHelp>();
		_treeHelper.InjectHFM( this );
		_gridHelper = HackApp.Services.GetRequiredService<GridHelp>();
		_gridHelper.InjectHFM( this );


		_hackLists = new()
		{
			Entry = OdooEntryList,
			History = OdooHistory,
			Parents = OdooParents,
			Children = OdooChildren,
			Properties = OdooProperties,
			Versions = OdooVersionInfoList
		};
		// SizeColumn.Binding.Converter = new FileSizeConverter();
		HackDispatcherQueue = DispatcherQueue.GetForCurrentThread();
		// DesignTheme();
		AssignCollections();
		AssignGridAndCollectionsMap();
		InitializeEvents();
		// this.SetFormTheme(StorageBox.MyTheme ?? ThemePreset.DefaultTheme);
		GridHelp.ResetListViews( _hackLists.AllLists );
		OdooDirectoryTree.LostFocus += ( s, e ) =>
		{
			if( OdooDirectoryTree.SelectedNode is null )
				return;
			LastSelectedNode = OdooDirectoryTree.SelectedNode;
			ViewModel.LastSelectedNodePath = LastSelectedNode?.LinkedData.FullPath;
		};
		this.Unloaded += ( s, e ) =>
		{
			ViewModel.IsClosing = true;
			ViewModel._cSource?.Cancel();
			ViewModel._cTreeSource?.Cancel();
		};
		if( IsLoaded )
		{
			Task.Run( HackFileManager_Load );
			return;
		}

		this.Loaded += ( _, _ ) => Task.Run( HackFileManager_Load );
		ViewModel.HackLoaded = true;
	}
#if DEBUG
	private static async Task DebugTest2()
	{
		var create_test = new HpTest
		{
			id = 12
		};
		await HpTest.GetAllRecordsAsync();
	}
	private static async Task DebugTest()
	{
		var create_test = new HpTest()
		{
			binary = Encoding.UTF8.GetBytes("This is a binary field"),
			boolean = true,
			character = "Test Character Field",
			dates = DateTime.Now,
			datetimes = DateTime.Now,
			floats = 12345.6789f,
			integer = 123456,
			image = FileOperations.ConvertFromBase64(@"iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAEJ0lEQVR4AW1W3W/TVRjuXzRHVislmG20bCBbV9YsltpuI251EzDuYhcCYSR6YWJiJIoukPgVhAvuBFG4kCgkmJgYF1txY7Ubo+6zdhuUFde9j+d5256clP6SJ+/O2e88z/t13l895XIZfGgJEeFS7daTB1idO43cnyHkUnUwe2vZCZSKWdQ4LI/D6XH+Ye1/W3OWaGU6jPJqDNhMAIWYgn+XV2JYng7b97ZLOTR63AjU/pubtMTYiNeIRZGPVqDrquh63Ao9Xr6iPC5cAZOOk/piafGI6zFJ0QDiRvTknz48SkWwljlDuudTtLF4geQMvUoerUGstXD2ic2YRlvKBDD/UxPys+dsPSlgc66eb8Tw+y8JXL/Wj+vfxgmps6hff3ftKKZudwEPvFj/bZeKbG89tDXAYipcyXkxgY8nB9H+6ijaY8kaxLHiroPxUQReG4G/J4bmQAQfvd8JZH3I3X0BC3eaKhGwFek9Q6T3nfERBGOjBkM1CK2LjniSkAP9I9jbE4WvI2QQRmvXYY1CZnZrFFuP/4ZnNXNSvde8b0SxP+YKuCJJizpyK/DyKxUBzPo1itX0GDy5VLXPCwbFKHqHhhoIJBUH+4dxIDECl9wV6OrrAbJe7Ez7sJXyMgoVENvrm1EcP5WkgDjpUa87Ta4LD4/IUmZA9obiYkgJqFWE5diJLmDWK0ZATBQyf6dZVEC9z0cpIJcuD6BdBdRrWq5x8fPXWSdBLoj3JoJo6YiQFAa0uv5msl0Ftu8zTT4YAbgC2uM7a1H59MKgHD+dxLFTSRk/O4yrVweAQgIyF5Qdc5iY/DAgQ292YSDZLYNvdAs7SP5ielRAjICNwKbIvVBMl4GgqJeI5DAHKUASYJok9FStrrlfgU8LXRUISXnFHQlWBIZcmBZ6zrApUAHJCApYYvsOa/A05RVTZPHk5yd0WFWjqAng5o1+vPPuMJ5lOkhCAhe693jKh6RJ060rrbpHAXpv2/T+ODyc57l0SKeinZqbMbl546j4DiawP9yNrz9rw+xtP56mX0TRYOZHPy6eC8i+0GE0t0Xww+VWW1wK7FQvWqmYg0dEzKgI1aKggOac3fLJB/vMCOhFSyBiEK5DRHYFe/Hl+TbgQSVFrvfzd72w03T7WU7HxZOFPua81i1avKnv98iJtw7JSx29QlJid2cv3h47JOlbe8TWoNr7+V9bRIddaYXUGoEqrS9d4jyXUqYdqBywxePtLM94ZeGeH4/u+WWH4yCrxK7nUvyjBSzs5sJXz3/R+KzNnqG6mJFLb3jQFWpgfQqmpeZ5fuasuB8xFXBRyJ6nF5pHTkUephBRbUO75v9YUL7LM4W5LyyxFcDzDz8WPGCFOLhI5hKbPrfE8z8b4dKSS9z4o1//84PzfDk9Zm5kU0OspsfZiu5PHZdH7f8j48WuF/x78gAAAABJRU5ErkJggg=="),
			html = """
<h1>Welcome to My Simple Page</h1>
<p>This is a paragraph of text on my page.</p>
""",
			json = new Hashtable()
			{
				{ "name", "Test JSON" },
				{ "value", 12345 },
				{ "isActive", true }
			},
			monetary = 99.987654321M,
			text = "This is a longer block of text that can span multiple lines.\nIt is used to store larger amounts of textual data.",
			selection = "option_a",

			many2one = 1,
			one2many = [1, 2],
			many2many = [1, 2],
		};

		var id = await create_test.CreateAsync();
		Debug.WriteLine( $"Created HpTest with ID: {id}" );
	}
#endif
	private void AssignCollections()
	{
		OdooDirectoryTree.ItemsSource = ONodes;
		//OdooEntryList.ItemsSource = OEntries;
		//< CollectionViewSource
		//	x:Name = "GroupedEntriesViewSource"
		//	IsSourceGrouped = "True"
		//	ItemsPath = "Items" />

		GroupedEntries?.Regroup( entryrow => entryrow.Status );
		OdooEntryList.ItemsSource = GroupedEntries?.ViewSource.View;

		OdooHistory.ItemsSource = OHistories;
		OdooParents.ItemsSource = OParents;
		OdooChildren.ItemsSource = OChildren;
		OdooProperties.ItemsSource = OProperties;
		OdooVersionInfoList.ItemsSource = OVersions;
		OdooDirectoryBreadcrumb.ItemsSource = LastSelectedNodePaths;
	}

	private void AssignGridAndCollectionsMap()
	{
		GridMap.Map = new()
		{
			{ OdooEntryList, GroupedEntries?.Master },
			{ OdooHistory, OHistories },
			{ OdooParents, OParents },
			{ OdooChildren, OChildren },
			{ OdooProperties, OProperties },
			{ OdooVersionInfoList, OVersions },
		};
	}
	private void InitializeEvents()
	{
		OdooDirectoryBreadcrumb.ItemClicked += OdooDirectoryBreadcrumb_ItemClicked;

		OdooDirectoryTree.SelectionChanged += OdooDirectoryTree_SelectionChanged;
		OdooDirectoryTree.RightTapped += OdooDirectoryTree_RightTapped;

		OdooEntryList.SelectionChanged += OdooEntryList_SelectionChanged;
		OdooEntryList.Sorting += List_ColumnClick;
		OdooEntryList.LoadingRow += OdooEntryList_LoadingRow;
		OdooEntryList.LoadingRowGroup += OdooEntryList_LoadingRowGroup;

		// tree events
		TreeAnalyze.Click += ( sender, args ) => { };
		TreeCheckout.Click += Tree_Click_Checkout;
		TreeCommit.Click += Tree_Click_Commit;
		TreeDownload.DoubleTapped += Tree_Click_GetLatest;
		TreeDownloadAll.Click += Tree_Click_GetLatestAll;
		TreeDownloadTop.Click += Tree_Click_GetLatestTop;
		TreeOpenDirectory.Click += Tree_Click_OpenDirectory;
		TreeUndoCheckout.Click += Tree_Click_UndoCheckout;
		TreeLogicalDelete.DoubleTapped += Tree_Click_LogicalDelete;
		TreeLocalDelete.Click += Tree_Click_LocalDelete;
		TreePermanentDelete.Click += Tree_Click_PermanentDelete;
		TreeUndelete.DoubleTapped += Tree_Click_Restore;
		TreeRestoreAll.Click += Tree_Click_RestoreAll;
		TreeRestoreTop.Click += Tree_Click_RestoreTop;

		// entry datagrid events
		ListCheckout.Click += List_Click_Checkout;
		ListCommit.Click += List_Click_Commit;
		ListDelete.DoubleTapped += ListDelete_DoubleClicked;
		ListDeleteLocal.Click += List_Click_LocalDelete;
		ListDeleteLogical.Click += List_Click_LogicalDelete;
		ListDeletePermanent.Click += List_Click_PermanentDelete;
		ListGetLatest.Click += List_Click_GetLatest;
		ListLocal.Click += List_Click_OpenLatestLocal;
		ListUndoCheckout.Click += List_Click_UndoCheckout;
		ListPreview.Click += List_Click_OpenLatestRemote;
		ListFileDirectory.Click += List_Click_OpenDirectory;
		ListRestore.Click += List_Click_Restore;
		SaveIcon.Click += List_Click_SaveIcon;
		ListOpen.DoubleTapped += List_Click_Open;

		// additional toolbar
		OdooRefreshDropdown.Click += AdditionalTools_Click_Refresh;
		OdooSearchDropdown.Click += AdditionalTools_Click_Search;
		OdooManageTypesDropdown.Click += AdditionalTools_Click_ManageTypes;

		// tabbed datagrids
		OdooHistory.SelectionChanged += OdooHistory_ItemSelectionChanged;
		OdooHistory.DoubleTapped += History_DoubleClick;
		OdooParents.SelectionChanged += OdooParents_ItemSelectionChanged;
		OdooParents.DoubleTapped += OdooParents_DoubleClick;
		OdooChildren.SelectionChanged += OdooChildren_ItemSelectionChanged;
		OdooChildren.DoubleTapped += OdooChildren_DoubleClick;

		// history datagrid
		HistoryDownload.DoubleTapped += History_Click_Download;
		HistoryDownloadTemp.Click += History_Click_TemporaryDownload;
		HistoryDownloadOverwrite.Click += History_Click_OverwriteDownload;
		HistoryOpen.DoubleTapped += History_Click_Open;
		HistoryOpenTemp.Click += History_Click_TemporaryOpen;
		HistoryOpenOverwrite.Click += History_Click_OverwriteOpen;
		HistoryMove.DoubleTapped += History_Click_TemporaryMove;
		HistoryMoveTemp.Click += History_Click_TemporaryMove;
		HistoryMoveOverwrite.Click += History_Click_OverwriteMove;
	}
	private void Group_Field_Menu_Item_Click( object? sender, RoutedEventArgs e )
	{
		var item = sender as MenuFlyoutItem;
		if( item is null )
			return;

		// property name is stored in Text or encoded in Name as "Field_<Prop>"
		var propName = !string.IsNullOrEmpty(item.Text) ? item.Text : item.Name?.Replace("Field_", "");

		if( string.IsNullOrEmpty( propName ) || GroupedEntries is null )
			return;

		// Use reflection to read the EntryRow property value and group by its string representation
		GroupedEntries.Regroup( entry =>
		{
			var pi = typeof(EntryRow).GetProperty(propName);
			if( pi is null )
				return string.Empty;
			var v = pi.GetValue(entry);
			return v is DateTime dt
				? dt.ToShortDateString()
				: v?.ToString() ?? string.Empty;
		} );

		// Refresh ItemsSource to reflect grouping change
		OdooEntryList.ItemsSource = GroupedEntries.NoGrouping ? GroupedEntries.Master : GroupedEntries.ViewSource.View;
	}
	private void OdooEntryList_LoadingRowGroup( object? sender, DataGridRowGroupHeaderEventArgs e )
	{
		// Access the underlying grouping data
		ICollectionViewGroup groupData = e.RowGroupHeader.CollectionViewGroup;

		// Cast the group back to your specific group class

		if( groupData.Group is GroupInfoList<EntryRow> myGroup )
		{
			// Override the text displayed in the label
			e.RowGroupHeader.PropertyValue = $"{myGroup.Key}";
		}
	}
	private void OdooEntryList_LoadingRow( object? sender, DataGridRowEventArgs e )
	{
		var row = e.Row;
		var item = row.DataContext as EntryRow;
		switch( item?.Status )
		{
			case FileStatus.Lo:
			{
				row.Background = UIStorage.OrangeBrush.Value;
				break;
			}
			case FileStatus.Ro:
			{
				row.Background = UIStorage.BlueBrush.Value;
				break;
			}
			case FileStatus.Ok:
			goto default;
			case FileStatus.Nv:
			goto default;
			case FileStatus.Lm:
			goto default;
			case FileStatus.Dt:
			{
				row.Background = UIStorage.RedBrush.Value;
				break;
			}
			case FileStatus.Ds:
			{
				row.Background = UIStorage.RedBrush.Value;
				break;
			}
			case FileStatus.If:
			goto default;
			case FileStatus.Ft:
			goto default;
			case FileStatus.Cm:
			{
				row.Background = UIStorage.GreenBrush.Value;
				break;
			}
			case FileStatus.Co:
			{
				row.Background = UIStorage.GreenBrush.Value;
				break;
			}
			default:
			{
				row.Background = null;
				break;
			}
		}
	}
	private void List_Click_SaveIcon( object sender, RoutedEventArgs e )
	{
		throw new NotImplementedException();
		if( OdooEntryList.SelectedItem is not EntryRow entry
			|| entry.Status is not ( FileStatus.Lo or FileStatus.Ft or FileStatus.If )
			|| entry.FullName is null )
			return;

		//var icon = Icon.ExtractAssociatedIcon(Path.Combine(entry.FullName));

		//var bitmap = icon?.ToBitmap();
	}
	private void OdooDirectoryTree_RightTapped( object sender, RightTappedRoutedEventArgs e )
	{
		var tree = sender as TreeView;
		var elem = e.OriginalSource is FrameworkElement ui ? ui.DataContext as TreeData : null;
		tree?.SelectedNode = elem?.Node;
		ODT_SetLastSelected( elem );
	}
	private async Task HackFileManager_Load()
	{
		await Task.Delay( 500 );
		await _treeHelper.CreateTreeViewBackground( OdooDirectoryTree );
	}
	public TreeView GetOdooDirectoryTree()
		=> OdooDirectoryTree;
	public DataGrid GetOdooEntryList()
			=> OdooEntryList;
	public Image GetOdooEntryImage() => OdooEntryImage;
	public TextBlock GetEntriesLabel() => EntryListStatus;
	public TextBlock GetEntriesLocalLabel() => EntryListLocalOnly;
	public TextBlock GetEntriesRemoteLabel() => EntryListRemoteOnly;
	public (Image, ProgressRing) GetVisualizer() => (OdooEntryImage, LoadRing);
	#endregion
	#region TEST_VARIABLES
#if DEBUG
	public Stopwatch TimerStopwatch;
#endif
	#endregion
}
public sealed partial class HackFileManager : Page
{
	private bool _isComponentInPip = false;
	private PipWindow? _pipWindow { get; set; }

	private void OdooDirectoryBreadcrumb_ItemClicked( BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args )
	{
		var tData = args.Item as TreeData;
		if( tData is null or { Node: null } )
			return;

		LastSelectedNode = tData.Node;
		OdooDirectoryTree.SelectedNode = tData.Node;
		tData.EnsureVisible( OdooDirectoryTree );
		ViewModel.LastSelectedNodePath = tData.Node?.LinkedData.FullPath;
		LastSelectedNode?.UpdateBreadCrumbCollection( LastSelectedNodePaths );

		foreach( var child in tData.Node!.Children )
		{
			child.IsExpanded = false;
		}
	}
	private async void Tree_Click_Undelete( object sender, RoutedEventArgs e )
	{
		await UnDeleteInternal();
	}
	private void ListDelete_DoubleClicked( object sender, DoubleTappedRoutedEventArgs e )
		=> List_Click_LocalDelete( sender, e );
	private void OnTogglePipClicked( object sender, RoutedEventArgs e )
	{
		if( !_isComponentInPip )
		{
			ImageSlot.Children.Remove( OdooEntryImage );

			_pipWindow = new PipWindow();
			_pipWindow.HostComponent( OdooEntryImage );

			// Wire up your custom return callback event here!
			_pipWindow.ReturnRequested += ( s, args ) => ReturnComponentToMain();
			_pipWindow.Closed += OnPipWindowClosed;

			_pipWindow.Activate();
			_isComponentInPip = true;
		}
		else
		{
			ReturnComponentToMain();
		}
	}

	private void ReturnComponentToMain()
	{
		if( _pipWindow != null )
		{
			var component = _pipWindow.ReleaseComponent();
			if( component != null )
			{
				ImageSlot.Children.Add( component );
			}

			_pipWindow.Closed -= OnPipWindowClosed;
			_pipWindow.Close();
			_pipWindow = null;
		}
		_isComponentInPip = false;
	}

	private void OnPipWindowClosed( object sender, WindowEventArgs args )
	{
		// 1. Break the closed window reference immediately to avoid memory leaks
		// Unsubscribe from our custom callback to allow the window to be garbage collected
		_pipWindow?.Closed -= OnPipWindowClosed;
		_pipWindow = null;

		// 2. Safely rescue your UI component and re-anchor it to MainWindow
		// Note: We use the UI Dispatcher to prevent threading exceptions during window destruction
		this.DispatcherQueue.TryEnqueue( () =>
		{
			// Check if the component was already reclaimed (to prevent duplication)
			if( !ImageSlot.Children.Contains( OdooEntryImage ) )
			{
				// If MyVideoPlayer still has a parent element, clear it out first
				if( OdooEntryImage.Parent is Panel parentPanel )
				{
					parentPanel.Children.Remove( OdooEntryImage );
				}

				// Snap it back home into your main layout
				ImageSlot.Children.Add( OdooEntryImage );
			}

			// 3. Reset the structural state variable
			_isComponentInPip = false;
		} );
	}

	private static async Task Async_GetLatest( (ArrayList, CancellationToken?) arguements )
	{
		object lockObject = new();
		ArrayList entryIDs = arguements.Item1;

		// add status lines for entry id and upcoming versions
		lock( lockObject )
		{
			Dialog?.AddStatusLine( StatusMessage.FOUND, $"{entryIDs.Count} entries" );
			Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Retrieving all latest versions associated with entries..." );
		}

		var versions = await Latest.GetLatestVersions(entryIDs, ["preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents"]);

		IEnumerable<IEnumerable<HpVersion>>? versionBatches = Help.BatchArray(versions, Latest.DownloadBatchSize);
		Latest.
				MaxCount = versions.Length;
		Latest.SkipCounter = 0;
		Latest.ProcessCounter = 0;
		Latest.Downloaded = 0;

		if( versionBatches is null )
		{
			MessageBox.ShowAsync( "Cancelled Download... No Versions to Process" );
			return;
		}
		try
		{
			await Latest.ProcessDownloadsAsync( versionBatches, arguements.Item2, OdooDefaults.Instance.ConcurrencySize );
		}
		catch
		{
			await MessageBox.ShowAsync( "Cancelled Download" );
		}

		Dialog?.SetProgressBar( versions.Length, versions.Length );

		await MessageBox.ShowAsync( "Completed!" );
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async Task Async_Commit( List<HackFile>? hacks )
	{
		if( hacks is null or { Count: 0 } )
			return;

		HpPDMCommit startCommit = new()
		{ node_by = OdooDefaults.Instance?.MyNode?.id };
		await startCommit.CreateCommitAsync();

		var hfs = hacks is not null && hacks.Count > 0 ? await Commit.FilterCommitHackFiles( hacks ) : ([], []);

		List<HpVersion> localConversions = [];
		Latest.ProcessCounter = 0;
		Latest.SkipCounter = 0;
		Latest.MaxCount = hfs.Item1.Length + hfs.Item2.Length;// localVersions?.Length ?? 0;
		Dialog?.IsInProcess = true;

		ViewModel.statusToken = await ViewModel.statusToken.RenewTokenSourceAsync();
		Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"--- Preparing to stage records ---" );

		foreach( var hack in hfs.Item1 )
		{ if( !await Commit.CommitRecord( hack, startCommit, false ) ) return; }
		foreach( var hack in hfs.Item2 )
		{ if( !await Commit.CommitRecord( hack, startCommit, true ) ) return; }

		await startCommit.ServerCommit();
		MessageBox.ShowAsync( $"Completed!" );
		await _treeHelper.RestartTree( OdooDirectoryTree );
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async Task Async_CheckOut( HpEntry[] entries )
	{
		object lockObject = new();
		entries = [ .. CheckOut.FilterCheckoutEntries( entries ) ];
		Latest.
				ProcessCounter = 0;
		Latest.SkipCounter = 0;
		Latest.MaxCount = entries.Length;
		Dialog?.AddStatusLine( StatusMessage.INFO, $"{Latest.MaxCount} check outs" );
		for( int i = 0; i < entries.Length; i++ )
		{
			HpEntry entryModel = entries[i];

			lock( lockObject )
			{
				Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Checking out {entryModel.name} ({entryModel.id})" );
			}
			await CheckOut.CheckOutEntry( entryModel );

			lock( lockObject )
			{
				Latest.ProcessCounter += 1;
				Dialog?.SetProgressBar( ( Latest.SkipCounter + Latest.ProcessCounter ), Latest.MaxCount );
			}
		}

		Dialog?.SetProgressBar( Latest.MaxCount, Latest.MaxCount );
		await MessageBox.ShowAsync( $"Completed!" );
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async Task Async_UnCheckOut( HpEntry[] entries )
	{
		object lockObject = new();
		Latest.
				ProcessCounter = 0;
		Latest.SkipCounter = 0;
		Latest.MaxCount = entries.Length;
		Dialog?.AddStatusLine( StatusMessage.INFO, $"{Latest.MaxCount} uncheck outs" );
		for( int i = 0; i < entries.Length; i++ )
		{
			HpEntry entryModel = entries[i];

			lock( lockObject )
			{
				Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Unchecking out {entryModel.name} ({entryModel.id})" );
			}
			await CheckOut.UnCheckOutEntry( entryModel );

			lock( lockObject )
			{
				Latest.ProcessCounter += 1;
				Dialog?.SetProgressBar( ( Latest.SkipCounter + Latest.ProcessCounter ), Latest.MaxCount );
			}
		}

		Dialog?.SetProgressBar( Latest.MaxCount, Latest.MaxCount );
		await MessageBox.ShowAsync( $"Completed!" );
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async Task Async_PermDelete( HpEntry[] entries )
	{
		ArrayList ids = entries.Select(e => e.id).ToArrayList();
		bool vDeleted = false;

		// using DeleteEntry also deletes entries, versions, version props, version relationships, and ir attachment records
		DialogResult result = await MessageBox.ShowAsync($"Are you sure you want to permanently delete {ids.Count} entries from the database?\n" +
											  $"This will also permanently delete all associative versions, version properties, and version relationships", "Delete Entries and Other Records?", MessageBoxButtons.YesNoCancel);

		if( result is not DialogResult.Yes and not DialogResult.OK )
			return;

		vDeleted = await PermanentDeleteEntry( ids );

		if( vDeleted )
		{
			Dialog?.AddStatusLine( StatusMessage.SUCCESS, $"Completed permanent delete" );
		}
		else
		{
			MessageBox.ShowAsync( "Was unable to delete entries", "Error", buttons: MessageBoxButtons.OKCancel, icon: MessageBoxIcon.Error );
			return;
		}

		await MessageBox.ShowAsync( $"Completed!" );
		await _treeHelper.RestartTree( OdooDirectoryTree );
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async Task Async_LogicalDelete( HpEntry[] entries )
	{
		object lockObject = new();
		foreach( var entry in entries )
		{
			lock( lockObject )
			{
				Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Setting InActive {entry.name}: {entry.id}" );
			}
			await entry.LogicalDelete();

		}

		await MessageBox.ShowAsync( $"Completed!" );
		await _treeHelper.RestartTree( OdooDirectoryTree );
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async Task Async_LogicalUnDelete( HpEntry[] entries )
	{
		object lockObject = new();
		foreach( var entry in entries )
		{
			lock( lockObject )
			{
				Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Setting Active {entry.name}: {entry.id}" );
			}
			await entry.LogicalUnDelete();
		}

		Dialog?.SetProgressBar( 5, 5 );
		await MessageBox.ShowAsync( $"Completed!" );
		await _treeHelper.RestartTree( OdooDirectoryTree );
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async Task Async_ListItemChange( EntryRow item, CancellationToken token )
	{
		await ProcessEntrySelectionAsync( item, token );
	}
	private async void GetLatestFromTreeNode( bool withSubdirectories = false )
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;

		ViewModel.statusToken = await ViewModel.statusToken.RenewTokenSourceAsync();
		object lockObject = new();

		TreeViewNode? tnCurrent = LastSelectedNode;
		TreeData? data = LastSelectedNode?.LinkedData;

		if( tnCurrent == null )
		{
			MessageBox.ShowAsync( "current directory doesn't exist remotely" );
			return;
		}

		// directory only needs ID set to find that record's entries
		HpDirectory directoryModel = new()
		{
			id = data?.DirectoryId ?? 0,
			name = data?.Name ?? "",
		};

		lock( lockObject )
		{
			Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Retrieving all entries and their and their associated dependencies within directory ({directoryModel.name}, id: {directoryModel.id})" );
		}

		Dialog?.IsInProcess = true;

		ArrayList? entryIDs = await directoryModel.GetDirectoryEntryIDsAsync(withSubdirectories, ShowInactive.IsChecked ?? false);
		ViewModel.statusToken.Token.Register( CancelledOperation );

		await GetLatestInternal( entryIDs );
	}
	internal async Task ProcessEntrySelectionAsync( EntryRow? entry, CancellationToken token, bool listLatestVersionInfo = false )
	{
		if( entry is null )
			return;

		await SafeHelper.SafeInvokerAsync( async () =>
		{
			try
			{
				switch( LowerTabIndex?.Name )
				{
					case StorageBox.HISTORY_TAB:
					await _gridHelper.ProcessHistorySelectAsync( OdooHistory, entry, token );
					OdooHistory.UpdateLayout();
					break;
					case StorageBox.PARENT_TAB:
					await SafeHelper.SafeInvokerAsync( async () =>
					{
						await _gridHelper.ProcessParentSelectAsync( OdooParents, entry, token );
						OdooParents.UpdateLayout();
					} );
					break;
					case StorageBox.CHILD_TAB:
					await SafeHelper.SafeInvokerAsync( async () =>
					{
						await _gridHelper.ProcessChildSelectAsync( OdooChildren, entry, token );
						OdooChildren.UpdateLayout();
					} );
					break;
					case StorageBox.PROPERTIES_TAB:
					await SafeHelper.SafeInvokerAsync( async () =>
					{
						await _gridHelper.ProcessPropertiesSelectAsync( OdooProperties, entry, token );
						OdooProperties.UpdateLayout();
					} );
					break;
					case StorageBox.INFO_TAB:
					await SafeHelper.SafeInvokerAsync( async () =>
					{
						await _gridHelper.ProcessInfoSelectAsync( OdooVersionInfoList, entry, token );
						OdooVersionInfoList.UpdateLayout();
					} );
					break;
				}
			}
			catch( OperationCanceledException e )
			{
				Debug.WriteLine( $"Operation cancelled: {e.Message}" );
			}
		} );

		if( entry.LatestId is int id )
		{
			await _gridHelper.PreviewImage( id );
		}
		else if( entry.Status is FileStatus.Lo )
		{
			BitmapImage? icon = await GetFileIconAsync( entry.LocalFile?.FullName ?? "", 512 );
			if( icon != null )
			{
				_gridHelper.SetEntryImageView( icon );
			}
		}
	}
	public static async Task<BitmapImage?> GetFileIconAsync( string filePath, uint dynamicSize = 64 )
	{
		try
		{
			if( !File.Exists( filePath ) )
				return null;

			// 1. Get the file handle using WinRT Storage API
			StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);

			// 2. Extract the system thumbnail icon (32x32 requested here)
			// Returns a crisp asset using a modern system scaling strategy
			using var thumbnail = await file.GetThumbnailAsync(
				ThumbnailMode.SingleItem,
				dynamicSize,
				ThumbnailOptions.ResizeThumbnail ); // Ensures Windows targets exact scale bounding
			if( thumbnail != null )
			{
				// 3. Convert it directly into a WinUI 3 compatible BitmapImage
				BitmapImage bitmapImage = new();
				await bitmapImage.SetSourceAsync( thumbnail );
				return bitmapImage;
			}
		}
		catch( Exception ex )
		{
			// Handle file missing or access restriction exceptions
			System.Diagnostics.Debug.WriteLine( $"Failed to get icon: {ex.Message}" );
		}

		return null;
	}

	#region Form Event Handlers
	// after select events
	private async void ODT_SetLastSelected( TreeData? tData )
	{

		LastSelectedNode = tData?.Node;
		ViewModel.LastSelectedNodePath = LastSelectedNode?.LinkedData.FullPath;
		LastSelectedNode?.UpdateBreadCrumbCollection( LastSelectedNodePaths );

		ViewModel.IsListLoaded = false;
	}
	private async void OdooDirectoryTree_SelectionChanged( TreeView sender, TreeViewSelectionChangedEventArgs args )
	{
		_queuedTreeChange = (null, null);
		if( ViewModel._treeItemChange is not null and { IsCompleted: false } )
		{
			//_queuedTreeChange = (sender, args);
			return;
		}

		if( ViewModel._treeItemChange is null or { IsCompleted: true } )
		{
			ViewModel._cSource?.Cancel();
			ViewModel._cTreeSource = new();

			// Store the currently selected node
			if( args.AddedItems.Count > 0 )
			{
				LastSelectedNode = ( args.AddedItems.First() as TreeData )?.Node;
				ViewModel.LastSelectedNodePath = LastSelectedNode?.LinkedData.FullPath;
				LastSelectedNode?.UpdateBreadCrumbCollection( LastSelectedNodePaths );
			}

			ViewModel.IsListLoaded = false;
			if( LastSelectedNode is not null )
			{
				ViewModel._treeItemChange = _treeHelper.TreeSelectItem( sender, LastSelectedNode, OdooEntryList, ViewModel._cTreeSource.Token );
				await ViewModel._treeItemChange;
			}

			if( _queuedTreeChange.sender != null && _queuedTreeChange.args != null )
			{
				OdooDirectoryTree_SelectionChanged( sender, args );
			}
		}
	}
	// item selection change events
	private async void VersionTabs_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( e.AddedItems.Count == 0 )
			return;
		if( OdooEntryList.SelectedItem is not EntryRow entry )
			return;
		if( ViewModel._cSource is not null )
			await ViewModel._cSource.CancelAsync();
		ViewModel._cSource = new();
		_ = ProcessEntrySelectionAsync( entry, ViewModel._cSource.Token );
	}
	private async void OdooEntryList_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( OdooEntryList.SelectedItems.Count > 1 || e.AddedItems.Count == 0 )
			return;

		_queuedEntryChange = (null, null);
		if( ViewModel._entryListChange is not null and { IsCompleted: false } )
		{
			_queuedEntryChange = (sender, e);
			return;
		}
		GridHelp.ResetListViews( _hackLists.SubLists );
		if( OdooEntryList.SelectedItems.Count == 0 )
			return;

		OdooEntryImage.Source = null;
		if( ViewModel._entryListChange is not ( null or { IsCompleted: true } ) )
			return;

		ViewModel._cSource = new();
		var listViewItem = e.AddedItems.First() as EntryRow;
		if( listViewItem != null )
		{
			ViewModel._entryListChange = Async_ListItemChange( listViewItem, ViewModel._cSource.Token );
			await ViewModel._entryListChange;
		}
		if( _queuedEntryChange.sender != null && _queuedEntryChange.e != null )
		{
			OdooEntryList_SelectionChanged( _queuedEntryChange.sender, _queuedEntryChange.e );
		}
	}
	private void OdooHistory_ItemSelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( e.AddedItems.Count == 0 )
			return;
		PreviewImageSelection( ( e.AddedItems.First() as HistoryRow ) ); //, NameConfig.HistoryVersion.Name);
	}
	private void OdooParents_ItemSelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( e.AddedItems.Count == 0 )
			return;
		PreviewImageSelection( ( e.AddedItems.First() as ParentRow ) ); //, NameConfig.ParentVersion.Name);
	}
	private void OdooChildren_ItemSelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( e.AddedItems.Count == 0 )
			return;
		PreviewImageSelection( ( e.AddedItems.First() as ChildrenRow ) ); //, NameConfig.ChildrenVersion.Name);
	}
	// change events
	private async void ShowInactive_Checked( object sender, RoutedEventArgs e )
	{
		ViewModel.IsActive = ShowInactive.IsChecked ?? false;
		if( LastSelectedNode is not null )
		{
			GroupedEntries?.ClearAll();
			await _treeHelper.TreeSelectItem( OdooDirectoryTree, LastSelectedNode!, OdooEntryList );
		}
	}
	private async void Anchor_Checked( object sender, RoutedEventArgs e )
	{
		if( HackApp.Window?.AppWindow.Presenter is OverlappedPresenter op )
		{
			op.IsAlwaysOnTop = Anchor.IsChecked ?? false;
		}
	}
	private void ShowHidden_Checked( object sender, RoutedEventArgs e )
	{

	}
	// tree open events
	private void OdooCMSTree_Opening( object sender, CancelEventArgs e )
	{
		string pathway = ViewModel.LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, ViewModel.LastSelectedNodePath[5..]);
		if( Directory.Exists( pathway ) )
		{
			// TreeOpenDirectory.Enabled = true;
			// TreeLocalDelete.Enabled = true;
		}
		else
		{
			// TreeOpenDirectory.Enabled = false;
			// TreeLocalDelete.Enabled = false;
		}
	}
	// click events
	private void List_ColumnClick( object? sender, DataGridColumnEventArgs e )
	{
		var grid = sender as DataGrid;
		var column = e.Column;
		foreach( var col in grid?.Columns ?? [] )
		{
			if( e.Column == col )
				continue;
			col.SortDirection = null;
		}
		var modelField = column.ClipboardContentBinding?.Path.Path ?? column.Header;
		bool isDesc = false;
		(column.SortDirection, isDesc) = column.SortDirection is not null
			and DataGridSortDirection.Ascending
				? (DataGridSortDirection.Descending, true)
				: (DataGridSortDirection.Ascending, false);

		SortLogic<string>( modelField as string ?? "", isDesc );
		GroupedEntries?.Regroup();
		grid?.ItemsSource = GroupedEntries?.NoGrouping is true
							? GroupedEntries?.Master
							: GroupedEntries?.ViewSource.View;
		// e.Column.SortDirection = e.Column.SortDirection == DataGridSortDirection.Ascending 
		// 	? DataGridSortDirection.Descending
		// 	: DataGridSortDirection.Ascending;
	}
	private void SortLogic<T>( string fieldName, bool isDesc )
	{
		switch( fieldName )
		{
			case null:
			return;

			case nameof( EntryRow.Name ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.Name ?? "", isDesc );
				break;
			}
			case nameof( EntryRow.Id ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.Id ?? 0, isDesc );
				break;
			}
			case nameof( EntryRow.Checkout ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.Checkout?.name ?? "", isDesc );
				break;
			}
			case nameof( EntryRow.Size ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.Size, isDesc );
				break;
			}
			case nameof( EntryRow.Type ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.Type ?? "", isDesc );
				break;
			}
			case nameof( EntryRow.Status ):
			{
				GroupedEntries?.Master.Sort( ( item ) => Enum.GetName( item.Status ) ?? "", isDesc );
				break;
			}
			case nameof( EntryRow.LatestId ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.LatestId, isDesc );
				break;
			}
			case nameof( EntryRow.RemoteDate ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.RemoteDate, isDesc );
				break;
			}
			case nameof( EntryRow.LocalDate ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.LocalDate, isDesc );
				break;
			}
			case nameof( EntryRow.Category ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.Category?.name ?? "", isDesc );
				break;
			}
			case nameof( EntryRow.FullName ):
			{
				GroupedEntries?.Master.Sort( ( item ) => item.FullName ?? "", isDesc );
				break;
			}
			default:
			return;
		}
	}
	//
	private void Tree_Click_GetLatest( object sender, RoutedEventArgs e )
		=> GetLatestFromTreeNode( true );
	private void Tree_Click_GetLatestAll( object sender, RoutedEventArgs e )
		=> Tree_Click_GetLatest( sender, e );
	private void Tree_Click_GetLatestTop( object sender, RoutedEventArgs e )
		=> GetLatestFromTreeNode( false );
	private async void Tree_Click_Commit( object sender, RoutedEventArgs e )
	{
		string pathway = ViewModel.LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, ViewModel.LastSelectedNodePath?[5..] ?? "");
		//HpDirectory hpDirectory;
		TreeData? dat = LastSelectedNode?.LinkedData;
		if( dat?.IsRemoteOnly is true )
			return;

		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;

		ViewModel.statusToken = await ViewModel.statusToken.RenewTokenSourceAsync();
		//ArrayList? entryIDs = await HpDirectory.GetDirectoryEntryIDsAsync(dat?.DirectoryId ?? 0, true);
		if( ViewModel.statusToken.IsCancellationRequested )
			return;

		HackFile.GetHackFolderWithDependencies( pathway, true, out List<HackFile> hf );
		await CommitInternal( hf );
	}
	private async void Tree_Click_Checkout( object sender, RoutedEventArgs e )
	{
		GetLatestFromTreeNode( true );
		ArrayList? entryIDs = await HpDirectory.GetDirectoryEntryIDsAsync(LastSelectedNode?.LinkedData.DirectoryId ?? 0, true);
		await CheckoutInternal( entryIDs );
	}
	private async void Tree_Click_UndoCheckout( object sender, RoutedEventArgs e )
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;
		ArrayList? entryIDs = await HpDirectory.GetDirectoryEntryIDsAsync(LastSelectedNode?.LinkedData.DirectoryId ?? 0, true);

		await UnCheckoutInternal( entryIDs );
	}
	private void Tree_Click_OpenDirectory( object sender, RoutedEventArgs e )
	{
		string pathway = ViewModel.LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, ViewModel.LastSelectedNodePath[5..]);
		if( Directory.Exists( pathway ) )
		{
			Process.Start( "explorer.exe", pathway );
		}
	}
	private async void Tree_Click_Restore( object sender, RoutedEventArgs e )
		=> await UnDeleteInternal( false );
	private async void Tree_Click_RestoreTop( object sender, RoutedEventArgs e )
		=> await UnDeleteInternal( false );
	private void Tree_Click_RestoreAll( object sender, RoutedEventArgs e )
		=> MessageBox.ShowAsync( "Not Implemented Yet" );
	private async void Tree_Click_LocalDelete( object sender, RoutedEventArgs e )
	{
		string pathway = ViewModel.LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, ViewModel.LastSelectedNodePath[5..]);
		DirectoryInfo directory = new(pathway);
		if( directory.Exists )
		{
			if( await MessageBox.ShowAsync( $"Are you sure you want to delete this directory and ({directory.EnumerateFiles().Count()}) files inside?",
					"Delete Directory",
					buttons: MessageBoxButtons.YesNoCancel,
					icon: MessageBoxIcon.Warning ) == DialogResult.Yes )
			{
				directory.Delete( true );
			}
		}
	}
	private async void Tree_Click_LogicalDelete( object sender, RoutedEventArgs e )
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;

		ArrayList? entryIDs = await HpDirectory.GetDirectoryEntryIDsAsync(LastSelectedNode?.LinkedData.DirectoryId ?? 0, true);

		await LogicalDeleteInternal( entryIDs );
	}
	private void Tree_Click_PermanentDelete( object sender, RoutedEventArgs e )
	{
#if DEBUG

#endif
	}
	//
	internal async void List_Click_GetLatest( object sender, RoutedEventArgs e )
		=> await ListClickGetLatest( sender, e );
	internal async Task ListClickGetLatest( object sender, RoutedEventArgs e )
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;
		ViewModel.statusToken = await ViewModel.statusToken.RenewTokenSourceAsync();

		var entryItem = OdooEntryList.SelectedItems;

		ArrayList entryIDs = [];

		foreach( EntryRow item in entryItem )
		{
			if( item.Id is not null )
			{
				entryIDs.Add( item.Id );
			}
		}

		await GetLatestInternal( entryIDs );
	}
	private async void List_Click_Commit( object sender, RoutedEventArgs e )
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;

		this.Dialog?.AddStatusLine( StatusMessage.PROCESSING, "checking entries and dependencies" );
		var entryItem = (OdooEntryList.SelectedItems as IList)?.Cast<EntryRow>().ToList() ?? [];
		HashSet<HackFile> hackFiles = [];
		hackFiles.AddAll( await ProcessHacks( entryItem ) );
		if( hackFiles.Count > 0 )
			await CommitInternal( hackFiles );
		else
			this.Dialog?.AddStatusLine( StatusMessage.INFO, "no valid entries to process" );
	}
	private static async Task<HashSet<HackFile>> ProcessHacks( List<EntryRow>? entries )
	{
		HashSet<HackFile> hackFiles = [];
		if( entries is null )
			return hackFiles;

		foreach( var item in entries )
		{
			string? file = item.FullName;
			if( string.IsNullOrEmpty( file ) || string.IsNullOrEmpty( item.Type ) )
				continue;

			if( OdooDefaultsConstants.DependentExt.Contains( $".{item.Type.ToUpper()}" ) )
			{
				var (success, hf, results) = await HackFile.GetHackFileWithDependencies( item, true );
				if( success )
				{
					hackFiles.AddAll( hf ?? [] );
				}
				else
				{
					foreach( var result in results )
					{
						HackFileManager.Dialog?.AddStatusLine( StatusMessage.ERROR, $"({result.Value?.Result}) file: {result.Value?.Hack?.FullPath}" );
					}
					Dialog?.AddStatusLine( StatusMessage.ERROR, $"Commit Terminated" );
				}
			}
			else
			{
				HackFile? hack = HackFile.GetFromPath(item.FullName)!;
				if( hack is { Exists: true } )
					hackFiles.Add( hack );
			}
		}

		return hackFiles;
	}
	internal async void List_Click_Checkout( object sender, RoutedEventArgs e )
	{
		await ListClickGetLatest( sender, e );
		var entryItem = OdooEntryList.SelectedItems;

		ArrayList entryIDs = new(entryItem.Count);

		foreach( EntryRow item in entryItem )
		{
			if( item is not { Checkout: null } )
				continue;
			entryIDs.Add( item.Id );
		}

		if( entryIDs.Count < 1 )
			return;

		await CheckoutInternal( entryIDs );
	}
	internal async void List_Click_UndoCheckout( object sender, RoutedEventArgs e )
	{
		var entryItem = OdooEntryList.SelectedItems;

		ArrayList entryIDs = new(entryItem.Count);

		foreach( EntryRow item in entryItem )
		{
			if( item is not { Checkout: null } )
				continue;
			entryIDs.Add( item.Id );
		}

		if( entryIDs.Count < 1 )
			return;
		await UnCheckoutInternal( entryIDs );
	}
	private async void List_Click_Open( object sender, RoutedEventArgs e )
	{
		// open local if lm, co
		// open remote if ro, dt
		foreach( EntryRow viewItem in OdooEntryList.SelectedItems )
		{
			string? path = viewItem.FullName;
			int? idStr = viewItem.Id;
			if( path is null )
				continue;
			if( idStr is null or 0 )
			{
				OpenLocalFile( path );
				continue;
			}
			FileStatus status = viewItem.Status;
			switch( status )
			{
				case FileStatus.Ro:
				case FileStatus.Nv:
				{
					await OpenRemoteFile( viewItem.Id ?? 0 );
					continue;
				}

				case FileStatus.Lm:
				case FileStatus.Ok:
				case FileStatus.Co:
				case FileStatus.Ft:
				case FileStatus.If:
				case FileStatus.Cm:
				{
					OpenLocalFile( FileOperations.ConvertToWindowsPath( path, true ) );
					continue;
				}

				default:
				continue;
			}

		}
	}
	private async void List_Click_OpenLatestRemote( object sender, RoutedEventArgs e )
	{
		StringBuilder errors = new();
		foreach( EntryRow viewItem in OdooEntryList.SelectedItems )
		{
			if( viewItem.Id is null or 0 )
			{
				errors.AppendLine( $"can't open local only file remotely {viewItem.Name}" );
				continue;
			}
			string? path = viewItem.FullName;
			FileStatus status = viewItem.Status;

			switch( status )
			{
				case FileStatus.Ro:
				case FileStatus.Nv:
				case FileStatus.Lm:
				case FileStatus.Ok:
				case FileStatus.Co:
				case FileStatus.Ft:
				case FileStatus.If:
				case FileStatus.Cm:
				{
					await OpenRemoteFile( viewItem.Id ?? 0 );
					continue;
				}

				default:
				{
					errors.AppendLine( $"can't open local only file remotely {viewItem.Name}" );
					continue;
				}
			}
		}
		if( errors.Length > 0 )
			MessageBox.ShowAsync( errors.ToString() );
	}
	private void List_Click_OpenLatestLocal( object sender, RoutedEventArgs e )
	{
		StringBuilder errors = new();
		foreach( EntryRow viewItem in OdooEntryList.SelectedItems )
		{
			string? path = viewItem.FullName;

			if( viewItem.Id is null or 0 )
			{
				OpenLocalFile( path );
				continue;
			}

			FileStatus status = viewItem.Status;

			switch( status )
			{
				case FileStatus.Nv:
				case FileStatus.Lm:
				case FileStatus.Ok:
				case FileStatus.Co:
				case FileStatus.Ft:
				case FileStatus.If:
				case FileStatus.Cm:
				{
					OpenLocalFile( FileOperations.ConvertToWindowsPath( path, true ) );
					continue;
				}

				case FileStatus.Ro:
				default:
				{
					errors.AppendLine( $"can't open remote only file locally {viewItem.Name}" );
					continue;
				}
			}
		}
		if( errors.Length > 0 )
			MessageBox.ShowAsync( errors.ToString() );
	}
	private void List_Click_OpenDirectory( object sender, RoutedEventArgs e )
	{
		List<string?> openedDirectory = [];
		foreach( EntryRow item in OdooEntryList.SelectedItems )
		{
			string? path = item.FullName;

			try
			{
				// remote file path
				if( item.Id is not null and not 0 )
				{
					path = FileOperations.ConvertToWindowsPath( path, true );
				}
				FileInfo file = new FileInfo(path);
				if( !file.Exists )
					continue;

				if( !openedDirectory.Any( s => file.DirectoryName?.Equals( s ) ?? true ) )
				{
					openedDirectory.Add( file.DirectoryName );
					FileOperations.OpenFolder( file.DirectoryName! );
				}
			}
			catch
			{
				continue;
			}
			//FileOperations.OpenFile(  );
		}
	}
	private void List_Click_Restore( object sender, RoutedEventArgs e )
	{

	}
	private async void List_Click_LocalDelete( object sender, RoutedEventArgs e )
	{
		string pathway = ViewModel.LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, ViewModel.LastSelectedNodePath?[5..] ?? "");
		DirectoryInfo directory = new(pathway);
		if( !directory.Exists )
			return;

		var sb = new StringBuilder();
		var files = new List<FileInfo>();

		OdooEntryList.SelectedItems.Cast<ListViewItem>().ToList().ForEach( item =>
		{
			string filepath = Path.Combine(pathway, (item.Content as EntryRow)?.Name ?? "");
			FileInfo file = new(filepath);
			if( file.Exists )
			{
				sb.AppendLine( file.FullName );
				files.Add( file );
			}
		} );
		bool tooMany = files.Count > 10;
		string message = tooMany ? $"Are you sure you want to delete ({files.Count}) files?" : $"Are you sure you want to delete these files?\nfiles:\n{sb}";
		if( await MessageBox.ShowAsync( message,
				"Delete Directory",
				buttons: MessageBoxButtons.YesNoCancel,
				icon: MessageBoxIcon.Warning ) == DialogResult.Yes )
		{
			files.ForEach( f => f.Delete() );
		}
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async void List_Click_LogicalDelete( object sender, RoutedEventArgs e )
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;

		var entryItem = OdooEntryList.SelectedItems;
		//var directory = HackDefaults.DefaultPath(lastSelectedNode.FullPath, true);

		ArrayList entryIDs = [];
		foreach( EntryRow item in entryItem )
		{
			if( item.Id is not null and not 0 )
			{
				entryIDs.Add( item.Id );
			}
		}

		await LogicalDeleteInternal( entryIDs );
	}
	private async void List_Click_PermanentDelete( object sender, RoutedEventArgs e )
	{
#if DEBUG
		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;

		var entryItem = OdooEntryList.SelectedItems;

		ArrayList entryIDs = new(entryItem.Count);

		foreach( EntryRow item in entryItem )
		{
			if( item.Id is not null and not 0 )
			{
				entryIDs.Add( item.Id );
			}
		}

		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(entryIDs, excludedFields: ["type_id", "cat_id", "checkout_node"]);
		if( entries is null || entries.Length == 0 )
		{
			MessageBox.ShowAsync( "No entries to delete" );
			return;
		}

		await AsyncHelper.AsyncRunner( () => Async_PermDelete( entries ), "Permanently Delete Files" );
#endif
	}
	//
	private async void AdditionalTools_Click_Refresh( object sender, RoutedEventArgs e )
	{
		OdooEntryImage.Source = _previewImage;
		await _treeHelper.RestartTree( OdooDirectoryTree );
	}
	private void AdditionalTools_Click_Search( object sender, RoutedEventArgs e )
	{
		WindowHelper.CreateWindowAndPage<SearchOdoo>( out var page, out var window );
		window.Title = "Search Files";
		page.SetHackInstance( this );
		page.StoreWindowInstance( window );
	}
	private void AdditionalTools_Click_ManageTypes( object sender, RoutedEventArgs e )
		=> WindowHelper.CreateWindowPage<OdooFileTypeManager>().Title = "Manage Types";
	//
	private async void History_Click_Download( object sender, DoubleTappedRoutedEventArgs e )
	{
		var version = await GetVersionFromHistory();
		FileInfo file = new(Path.Combine(version.WinPathway, version.name));
		if( FileOperations.SameChecksum( file, version.checksum ) )
		{
			if( file.Exists )
			{
				var response = MessageBox.ShowAsync("File exists as a different version.\n" +
											   "Retry:\tDownload in the Temporary Folder\n" +
											   "Ignore:\tOverwrite the current version\n" +
											   "Abort:\tCancel download", "File Version Conflict", buttons: MessageBoxButtons.AbortRetryIgnore, icon: MessageBoxIcon.Warning);

				switch( await response )
				{
					case DialogResult.Ignore:
					version.DownloadFile( version.WinPathway );
					break;
					case DialogResult.Yes:
					version.DownloadFile( Path.GetTempPath() );
					break;
				}
			}
		}
		else
		{
			version.DownloadFile( version.WinPathway );
		}
	}
	private void History_Click_TemporaryDownload( object sender, RoutedEventArgs e )
		=> DownloadHistory( true );
	private void History_Click_OverwriteDownload( object sender, RoutedEventArgs e )
		=> DownloadHistory( false );
	private void History_Click_Open( object sender, DoubleTappedRoutedEventArgs e )
	{

	}
	private void History_Click_OverwriteOpen( object sender, RoutedEventArgs e )
		=> DownloadOpen( false );
	private void History_Click_TemporaryOpen( object sender, RoutedEventArgs e )
		=> DownloadOpen( true );
	private void History_Click_OverwriteMove( object sender, RoutedEventArgs e )
		=> LocalMoveEntry( false );
	private void History_Click_TemporaryMove( object sender, RoutedEventArgs e )
		=> LocalMoveEntry( true );
	private async void History_DoubleClick( object sender, DoubleTappedRoutedEventArgs e )
	{
		if( OdooHistory.SelectedItems?[ 0 ] is not HistoryRow item )
			return;
		if( item.Version is 0 )
			return;

		HpVersion versionModel = (await HpVersion.GetRecordsByIdsAsync([item.Version])).First();
		HpEntry entryModel = (await HpEntry.GetRecordsByIdsAsync([versionModel.entry_id])).First();
		ArrayList versions = await Latest.GetVersionList(item.Version);
		HashSet<int> vIds = versions.ToHashSet<int>();
		vIds.Add( versionModel.id ?? 0 );
		string vIdsText = string.Join(", ", vIds);
		string eText = entryModel.latest_version_id == item.Version ? $"You are trying to download the latest version and dependencies. Continue?" : "You are trying to download a previous version and dependencies. Continue?";
		string vText = $"version:\n" +
					   $"\tName = {versionModel.name}\n" +
					   $"\tID = {versionModel.id}\n" +
					   $"\tSize = {versionModel.file_size}\n" +
					   $"\tChecksum = {versionModel.checksum}\n" +
					   $"\tAttachID = {versionModel.attachment_id}\n" +
					   $"\tMod Date = {versionModel.file_modify_stamp}\n" +
					   $"\tNode ID	= {versionModel.node_id}\n" +
					   $"\tDir ID = {versionModel.dir_id}\n" +
					   $"\tWin DL Path = {versionModel.WinPathway}";

		var response = MessageBox.ShowAsync($"{eText}\n this will download version ids: {vIdsText}\n{vText}", "Version Download", buttons: MessageBoxButtons.YesNoCancel);
		if( await response != DialogResult.Yes )
			return;
		HpVersion[] downVersions = await HpVersion.GetRecordsByIdsAsync(versions);
		if( downVersions.DownloadAll( out List<HpVersion> failed ) )
			return;

		ArrayList fIDs = failed.GetIDs();
		MessageBox.ShowAsync( $"failed to download version ids: {string.Join( ", ", fIDs.ToArray<int>() )}" );
	}
	private async void OdooParents_DoubleClick( object sender, DoubleTappedRoutedEventArgs e )
	{
		if( OdooParents.SelectedItems is not [ ParentRow item ] )
			return;

		string? pwaPath = item.BasePath;
		string? fileName = item.Name;
		await FindSearchSelectionAsync( pwaPath, fileName );
	}
	private async void OdooChildren_DoubleClick( object sender, DoubleTappedRoutedEventArgs e )
	{
		if( OdooChildren.SelectedItems is not [ ChildrenRow item ] )
			return;

		string? pwaPath = item.BasePath;
		string fileName = item.Name;
		await FindSearchSelectionAsync( pwaPath, fileName );
	}


	#endregion
	#region Form Helper Functions
	private static void OpenLocalFile( string path )
	{
		FileOperations.OpenFile( path );
	}
	private static async Task OpenRemoteFile( int entryId )
	{
		const string latestVersion = nameof(HpEntry.latest_version_id);
		HpVersion? versionModel = (await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>([entryId], latestVersion, excludedFields: ["preview_image"])).FirstOrDefault();
		if( versionModel == null )
			return;

		// download version data and place into temporary folder
		versionModel.DownloadFile( Path.GetTempPath() );
		FileOperations.OpenFile( Path.Combine( versionModel.WinPathway, versionModel.name ) );
	}
	private async void PreviewImageSelection<T>( T? item )
	{
		switch( item )
		{
			case null:
			break;
			case EntryRow er:
			if( er.LatestId is not null )
				await _gridHelper.PreviewImage( er.LatestId );
			break;
			case ChildrenRow cr:
			await _gridHelper.PreviewImage( cr.Version );
			break;
			case ParentRow pr:
			await _gridHelper.PreviewImage( pr.Version );
			break;
			default:
			break;
		}
	}
	public async Task FindSearchSelectionAsync( string pwaPath, string fileName, string delimiter = "\\" )
	{
		// first select the treeview node
		// then select the listview item
		string[] paths = pwaPath.Split([delimiter], StringSplitOptions.None);

		var nodes = OdooDirectoryTree.RootNodes;
		TreeViewNode node = nodes[0];

		try
		{
			for( int i = 0; i < paths.Length; i++ )
			{
				nodes = node.Children;

				bool wasFound = false;
				foreach( TreeViewNode n in nodes )
				{
					if( n.LinkedData.Name != paths[ i ] )
						continue;
					wasFound = true;
					node = n;
					break;
				}
				if( !wasFound )
					throw new ArgumentException();
			}
			foreach( var treeViewNode in OdooDirectoryTree.RootNodes )
			{
				treeViewNode.IsExpanded = false;
			}
			LastSelectedNode = node;
			LastSelectedNode.LinkedData.EnsureVisible( OdooDirectoryTree );
			OdooDirectoryTree.SelectedNode = LastSelectedNode;

			while( !ViewModel.IsListLoaded )
			{
				await Task.Delay( 100 );
			}
			EntryRow? entryItem = ( GroupedEntries?.Master.FirstOrDefault(entryItem => entryItem.Name == fileName) )
				?? throw new ArgumentException("entry doesn't exist", nameof(fileName));

			OdooEntryList.SelectedItem = entryItem;
			OdooEntryList.Focus( FocusState.Programmatic );
			OdooEntryList.ScrollIntoView( entryItem, null );
		}
		catch
		{
			Debug.WriteLine( "Unable to find search selection" );
		}
	}
	private async void DownloadOpen( bool toTemp = false )
	{
		var version = await DownloadHistory(toTemp);
		if( version == null )
			return;

		OpenLocalFile( Path.Combine( version.WinPathway, version.name ) );
	}
	private async Task<HpVersion?> DownloadHistory( bool toTemp = false )
	{
		var version = await GetVersionFromHistory();
		if( version is null )
			return null;

		if( toTemp )
		{
			string path = version.HashedValues.TryGetValue<string, ArrayList>("dir_id", out var arr)
				&& arr?[1] is string str ? string.Join("\\", str.Split(" / ")[1..])
				: "";

			string tempPath = Path.Combine(StorageBox.TemporaryPath, path);
			version.DownloadFile( tempPath );
			if( version.FileTypeExt != SolidWorks.Interop.swdocumentmgr.SwDmDocumentType.swDmDocumentUnknown )
			{
#if Debug
				HackDefaults.DocMgr.GetDependencies(path);
				HackDefaults.DocMgr.ReplaceDependencies(version.WinPathway, tempPath, version.FileTypeExt);
				HackDefaults.DocMgr.GetDependencies(path);
#endif
			}
		}
		else
			version.DownloadFile( version.WinPathway );

		return version;
	}
	private async void LocalMoveEntry( bool toTemp = false )
	{
		var version = await GetVersionFromHistory();
		if( version == null )
			return;

		string tempFilePath = Path.Combine(StorageBox.TemporaryPath ?? "", version.name ?? "");
		string mainFilePath = Path.Combine(version.WinPathway ?? "", version.name ?? "");

		FileInfo fileFrom = new FileInfo(!toTemp ? tempFilePath : mainFilePath);
		FileInfo fileTo = new FileInfo(toTemp ? tempFilePath : mainFilePath);

		string message = "";
		string caption = "";
		string boolReplace = toTemp ? "temporary" : "current";

		var icon = MessageBoxIcon.None;
		// if the file doesn't exist in temporary folder, download it an place it in current path.
		if( fileFrom.Exists )
		{
			if( fileTo.Exists )
			{
				message = $"Would you like to move this version to {boolReplace} and overwrite that version?";
				caption = "Move & Overwrite";
				icon = MessageBoxIcon.Warning;
			}
			else
			{
				// temporary version file and current version file don't exist
				message = $"Would you like to move this version to {boolReplace}?";
				caption = "Move";
				icon = MessageBoxIcon.Question;
			}
			// temporary version file doesn't exist but does exist in current
			if( DialogResult.Yes == await MessageBox.ShowAsync( message, caption, buttons: MessageBoxButtons.YesNoCancel, icon: icon ) )
			{
				fileFrom.MoveFile( fileTo.DirectoryName );
			}
		}
		else
		{
			if( fileTo.Exists )
			{
				message = $"file doesn't exist in {fileFrom.DirectoryName}.\nWould you like to download this version to {boolReplace} and overwrite that version?";
				caption = "Download & Overwrite";
				icon = MessageBoxIcon.Warning;
			}
			else
			{
				// temporary version file and current version file don't exist
				message = $"file doesn't exist in {fileFrom.DirectoryName}.\nWould you like to download this version to {boolReplace}?";
				caption = "Download";
				icon = MessageBoxIcon.Question;
			}
			// temporary version file doesn't exist but does exist in current
			if( DialogResult.Yes == await MessageBox.ShowAsync( message, caption, buttons: MessageBoxButtons.YesNoCancel, icon: icon ) )
			{
				version.DownloadFile( fileTo.DirectoryName );
			}
		}
		_treeHelper.RestartEntries( OdooDirectoryTree, OdooEntryList );
	}
	private async Task<HpVersion?> GetVersionFromHistory()
	{
		if( OdooHistory.SelectedItems.Count < 1 )
			return null;

		HistoryRow? item = OdooHistory.SelectedItems[0] as HistoryRow;

		if( item?.Version is null or 0 )
			return null;

		var version = await HpVersion.GetRecordByIdAsync(item!.Version, HpVersion.UsualExcludedFields);
		version.WinPathway = Path.Combine( HackDefaults.Instance.PwaPathAbsolute, version.WinPathway );
		return version;
	}
	private static void EndNodePaths( TreeViewNode node, in List<string> paths )
	{
		if( node.Children.Count == 0 )
		{
			paths.Add( node.LinkedData.FullPath ?? "" );
		}
		else
		{
			foreach( TreeViewNode cNode in node.Children )
			{
				EndNodePaths( cNode, paths );
			}
		}
	}

	private static async Task<bool> PermanentDeleteVersionProperty( ArrayList ids )
	{
		if( ids is null || ids.Count < 1 )
			return false;

		HpVersionProperty[] vProps = null;
		bool deletedVersionProps = false;

		vProps = await HpVersionProperty.GetRecordsBySearchAsync( [ new ArrayList() { "version_id", "in", ids } ] );
		if( vProps is not null
			&& vProps.Count() > 0 )
		{
			ArrayList newIds = vProps.GetIDs();
			Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Deleting version properties..." );
			deletedVersionProps = await OClient.DeleteAsync( HpVersionProperty.GetHpModel(), [ newIds ], 100000 );
			if( deletedVersionProps )
			{
				Dialog?.AddStatusLine( StatusMessage.SUCCESS, $"Deleted version properties: {string.Join( ", ", newIds.ToArray() )}" );
			}
			else
			{
				Dialog?.AddStatusLine( StatusMessage.ERROR, $"Unable to delete version properties" );
			}
		}
		else
		{
			deletedVersionProps = true;
			Dialog?.AddStatusLine( StatusMessage.SKIP, $"No version properties to delete" );
		}
#if DEBUG
		Debug.WriteLine( $"version properties deleted = {deletedVersionProps}" );
#endif
		return deletedVersionProps;
	}
	private static async Task<bool> PermanentDeletedVersionRelationships( ArrayList ids )
	{
		if( ids is null || ids.Count < 1 )
			return false;

		HpVersionRelationship[] vRelationsParent = null;
		HpVersionRelationship[] vRelationsChild = null;

		bool deletedVersionRelParent = false;
		bool deletedVersionRelChild = false;

		vRelationsParent = await HpVersionRelationship.GetRecordsBySearchAsync( [ new ArrayList() { "parent_id", "in", ids } ] );
		vRelationsChild = await HpVersionRelationship.GetRecordsBySearchAsync( [ new ArrayList() { "child_id", "in", ids } ] );

		if( vRelationsParent is not null
			&& vRelationsParent.Count() > 0 )
		{
			ArrayList newIds = vRelationsParent.GetIDs();
			Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Deleting parent version relationships..." );
			deletedVersionRelParent = await OdooClient<HpVersionRelationship>.DeleteAsync( [ newIds ], 100000 );
			if( deletedVersionRelParent )
			{
				Dialog?.AddStatusLine( StatusMessage.SUCCESS, $"Deleted parent version relationships: {string.Join( ", ", newIds.ToArray() )}" );
			}
			else
			{
				Dialog?.AddStatusLine( StatusMessage.ERROR, $"Unable to delete parent version relationships" );
			}
		}
		else
		{
			deletedVersionRelParent = true;
			Dialog?.AddStatusLine( StatusMessage.SKIP, $"No version relationship parents to delete" );
		}

		if( vRelationsChild is not null
			&& vRelationsChild.Any() )
		{
			ArrayList newIds = vRelationsChild.GetIDs();
			Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Deleting child version relationships..." );
			deletedVersionRelChild = await OClient.DeleteAsync( HpVersionRelationship.GetHpModel(), [ newIds ], 100000 );
			if( deletedVersionRelChild )
			{
				Dialog?.AddStatusLine( StatusMessage.SUCCESS, $"Deleted child version relationships: {string.Join( ", ", newIds.ToArray() )}" );
			}
			else
			{
				Dialog?.AddStatusLine( StatusMessage.ERROR, $"Unable to delete child version relationships" );
			}
		}
		else
		{
			deletedVersionRelChild = true;
			Dialog?.AddStatusLine( StatusMessage.SKIP, $"No version relationship children to delete" );
		}

#if DEBUG
		Debug.WriteLine( $"version parents deleted = {deletedVersionRelParent}" );
		Debug.WriteLine( $"version child deleted = {deletedVersionRelChild}" );
#endif

		return deletedVersionRelChild && deletedVersionRelParent;
	}
	private async Task<bool> PermanentDeleteEntry( ArrayList ids )
	{
		if( ids is null || ids.Count < 1 )
			return false;

		bool deletedVersions = await PermanentDeleteVersions(ids);
		Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Deleting entries..." );
		bool deletedEntries = deletedVersions && OClient.Delete(HpEntry.GetHpModel(), [ids]);
		if( deletedEntries )
		{
			Dialog?.AddStatusLine( StatusMessage.SUCCESS, $"Deleted entries" );
		}
		else
		{
			Dialog?.AddStatusLine( StatusMessage.ERROR, $"Unable to delete entries" );
		}
#if DEBUG
		Debug.WriteLine( $"Entries deleted = {deletedEntries}" );
#endif
		return deletedVersions && deletedEntries;
	}
	private async Task<bool> PermanentDeleteVersions( ArrayList ids )
	{
		if( ids is null || ids.Count < 1 )
			return false;

		HpVersion[]? versions = await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(ids, "version_ids", includedFields: ["ID"]);
		IrAttachment[] irAttachments = null;

		ArrayList vIds = versions?.Select(v => v.id).ToArrayList() ?? [];

		bool deletedIrAttachments = false;
		bool deletedVersions = false;
		bool deletedVersionsProps = false;
		bool deletedVersionsRel = false;

		if( vIds.Count > 0 )
		{
			deletedVersionsProps = await PermanentDeleteVersionProperty( vIds );
			deletedVersionsRel = await PermanentDeletedVersionRelationships( vIds );
			irAttachments = await IrAttachment.GetRecordsBySearchAsync(
			[
				new ArrayList() { "res_id", "in", vIds },
				new ArrayList() { "res_model", "=", HpVersion.GetHpModel()},
				new ArrayList() { "res_field", "=", "file_contents"},
			] );
		}
		Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Deleting IR Attachments..." );
		deletedIrAttachments = deletedVersionsProps
							   && deletedVersionsRel
							   && ( irAttachments is null
								   || !irAttachments.Any()
								   || await OClient.DeleteAsync( IrAttachment.GetHpModel(), [ irAttachments.GetIDs() ], 100000 ) );

		if( deletedIrAttachments )
		{
			Dialog?.AddStatusLine( StatusMessage.SUCCESS, $"Deleted IR Attachments" );
		}
		else
		{
			Dialog?.AddStatusLine( StatusMessage.INFO, $"unable to delete IR Attachments" );
		}
		Dialog?.AddStatusLine( StatusMessage.PROCESSING, $"Deleting versions..." );
		deletedVersions = deletedIrAttachments
						  && ( vIds.Count <= 0
							   || await OClient.DeleteAsync( HpVersion.GetHpModel(), [ vIds ], 100000 ) );

		if( deletedVersions )
		{
			Dialog?.AddStatusLine( StatusMessage.SUCCESS, $"Deleted versions" );
		}
		else
		{
			Dialog?.AddStatusLine( StatusMessage.ERROR, $"Unable to delete versions" );
		}

#if DEBUG
		Debug.WriteLine( $"ir attachments deleted = {deletedIrAttachments}" );
		Debug.WriteLine( $"versions deleted = {deletedVersions}" );
#endif
		return deletedIrAttachments && deletedVersions;
	}
	//
	internal async Task GetLatestInternal( ArrayList entryIDs )
	{
		Dialog?.AddStatusLine( StatusMessage.INFO, "Finding Entry Dependencies..." );
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: [nameof(HpEntry.latest_version_id)]);
		//HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, includedFields: [nameof(HpEntry.latest_version_id)]);

		if( entries is null || entries.Length < 1 )
			return;
		ArrayList newIds = await Latest.GetAllEntriesAndDependenciesList([.. entries.Select(entry => entry.latest_version_id?.id ?? 0)]);

		newIds.AddRange( entryIDs );
		newIds = newIds.ToHashSet<int>().ToArrayList();

		(ArrayList, CancellationToken?) arguments = (newIds, ViewModel.statusToken?.Token);

		ViewModel.statusToken.Token.Register( CancelledOperation );
		await AsyncHelper.AsyncRunner(() => Async_GetLatest( arguments ), "Get Latest", ViewModel.statusToken );
	}
	internal static void CancelledOperation()
	{
		this.Dialog?.IsInProcess = false;
		MessageBox.ShowAsync( "Cancelled Operation" );
	}
	internal async Task CommitInternal( IEnumerable<HackFile> hackFiles )
		=> await AsyncHelper.AsyncRunner( () => Async_Commit( [ .. hackFiles ] ), "Commit Files" );

	internal async Task CheckoutInternal( ArrayList entryIDs )
	{
		HpEntry[]? entriesTemp = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: [nameof(HpEntry.latest_version_id)]);

		ArrayList newIds = await Latest.GetAllEntriesAndDependenciesList([.. entriesTemp.Select(e => e.latest_version_id?.id ?? 0)]);

		newIds.AddRange( entryIDs );
		newIds = newIds.ToHashSet<int>().ToArrayList();

		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(newIds, excludedFields: ["type_id", "cat_id"]);

		if( entries is null || entries.Length < 1 )
			return;
		if( Dialog is null )
		{
			WindowHelper.CreateWindowAndPage<StatusDialog>( out var newDialog, out _ );
			Dialog = newDialog;
		}

		await AsyncHelper.AsyncRunner( () => Async_CheckOut( entries ), "Checkout Files" );
	}
	internal async Task UnCheckoutInternal( ArrayList entryIDs )
	{
		if( entryIDs is null or { Count: < 1 } )
			return;

		var entriesTemp = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: [nameof(HpEntry.latest_version_id)]);
		ArrayList newIds = await Latest.GetAllEntriesAndDependenciesList([.. entriesTemp?.Select(e => e.latest_version_id?.id ?? 0) ?? []]);

		newIds.AddRange( entryIDs );
		newIds = newIds.ToHashSet<int>().ToArrayList();

		var entries = await HpEntry.GetRecordsByIdsAsync(newIds, [
			new ArrayList()
			{
				"checkout_user",
				OperatorConverter.OperatorToString(Operators.Equal),
				OdooDefaults.Instance.OdooId,
			}
		], excludedFields: ["type_id", "cat_id"]);

		if( entries is null || entries.Length < 1 )
			return;

		if( Dialog is null )
		{
			WindowHelper.CreateWindowAndPage<StatusDialog>( out var newDialog, out _ );
			Dialog = newDialog;
		}

		// filter out entries that are already checked out
		entries = [ .. CheckOut.FilterUnCheckoutEntries( entries ) ];

		await AsyncHelper.AsyncRunner( () => Async_UnCheckOut( entries ), "UnCheckout Files" );
	}
	internal async Task LogicalDeleteInternal( ArrayList entryIDs )
	{
		HpEntry[]? entriesTemp = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: [nameof(HpEntry.latest_version_id)]);

		ArrayList newIds = await Latest.GetAllEntriesAndDependenciesList([.. entriesTemp?.Select(e => e.latest_version_id?.id ?? 0) ?? []]);

		newIds.AddRange( entryIDs );
		newIds = newIds.ToHashSet<int>().ToArrayList();

		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(newIds, excludedFields: ["type_id", "cat_id", "checkout_node"]);

		await AsyncHelper.AsyncRunner( () => Async_LogicalDelete( entries ), "Logically Delete Files" );
	}
	internal async Task UnDeleteInternal( bool withSubdirectories = false )
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>( out var Dialog, out _ );
		this.Dialog = Dialog;

		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(null, searchFilters: [new ArrayList() { "deleted", "=", true }, new ArrayList() { "dir_id", "=", LastSelectedNode?.LinkedData.DirectoryId ?? 0 }], excludedFields: ["type_id", "cat_id", "checkout_node"]);
		await AsyncHelper.AsyncRunner( () => Async_LogicalUnDelete( entries ), "Logically UnDelete Files" );
	}

	#endregion
	private void TreeViewItem_Loaded( object sender, RoutedEventArgs e )
	{
		var tvi = sender as TreeViewItem;
		var data = tvi?.DataContext as TreeData;
	}
	private void TreeViewItem_Unloaded( object sender, RoutedEventArgs e )
	{
		var tvi = sender as TreeViewItem;
		var data = tvi?.DataContext;
		ItemToContainerMap.Remove( data );
	}
	private void OdooEntryDataGrid_LoadingRowGroup( object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowGroupHeaderEventArgs e )
	{

	}
	private void OdooEntryDataGrid_Sorting( object sender, CommunityToolkit.WinUI.UI.Controls.DataGridColumnEventArgs e )
	{

	}


}