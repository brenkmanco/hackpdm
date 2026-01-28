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

using HackPDM.UI.Forms.Odoo;
using HackPDM.UI.Forms.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;


using Directory = System.IO.Directory;
using Path = System.IO.Path;
using Image = Microsoft.UI.Xaml.Controls.Image;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Helper;
using HackPDM.UI.Controls;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.FormTransport;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Data;
using HackPDM.UI.Models;
using HackPDM.UI.Types;

using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;
using EntryRow = HackPDM.UI.Types.EntryRow;
using ListViewItem = Microsoft.UI.Xaml.Controls.ListViewItem;
using NotifyIcon = HackPDM.UI.Types.NotifyIcon;
using TreeData = HackPDM.UI.Types.TreeData;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using WindowHelper = HackPDM.UI.Controls.WindowHelper;
using OClient = HackPDM.Infrastructure.Odoo.OdooClient;
using HackPDM.Abstractions;
using HackPDM.UI.Forms.FormTransport;
using HackPDM.UI.Forms.Helper;
using Microsoft.Extensions.DependencyInjection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Hack;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>


public sealed partial class HackFileManager : Page
{
	#region Declarations
	public ObservableCollection<TreeData>? LastSelectedNodePaths { get; set; } = [];
	public ObservableCollection<EntryRow> OEntries { get; internal set; } = [];
	public ObservableCollection<HistoryRow> OHistories { get; internal set; } = [];
	public ObservableCollection<ParentRow> OParents { get; internal set; } = [];
	public ObservableCollection<ChildrenRow> OChildren { get; internal set; } = [];
	public ObservableCollection<PropertiesRow> OProperties { get; internal set; } = [];
	public ObservableCollection<VersionRow> OVersions { get; internal set; } = [];
	public ObservableCollection<TreeData> ONodes { get; internal set; } = [];

	public static ConcurrentQueue<(StatusMessage action, string description)> QueueAsyncStatus = new();

	public static NotifyIcon Notify { get; } = Notifier.Notify;
	public static StatusDialog? Dialog { get; set; }
	public static ListDetail ActiveList { get; set; }
	public static readonly Dictionary<object, TreeViewNode> ItemToContainerMap = new();
	private static Task? _entryListChange;
	private static Task? _treeItemChange;
	private static (object? sender, SelectionChangedEventArgs? e) _queuedEntryChange = (null, null);
	private static (TreeView? sender, TreeViewSelectionChangedEventArgs? args) _queuedTreeChange = (null, null);

	private static readonly BackgroundWorker _backgroundWorker = new()
	{
		WorkerSupportsCancellation = true
	};
	private static CancellationTokenSource? _cSource = new();
	private static CancellationTokenSource? _cTreeSource = new();
	public static CancellationTokenSource? statusToken = new();

	private static ImageSource? _previewImage = null;

	public static int DownloadBatchSize
	{
		get => OdooDefaults.Instance.DownloadBatchSize;
		set => OdooDefaults.Instance.DownloadBatchSize = value;
	}
	public static int SkipCounter { get; private set; }
    internal static long Downloaded { get; set; }
	internal static long SessionDownloaded { get; set; }
    internal static int TotalProcessed { get; set; }
	internal static int ProcessCounter { get; set; }
    internal static int MaxCount { get; set; }
    public bool IsActive { get; set; } = false;
    public bool IsFiltered { get; set; } = true;
	public bool HackLoaded { get; private set; }

	private TreeHelp _treeHelper { get; set; }
	private GridHelp _gridHelper { get; set; }
	internal HackLists _hackLists { get; set; }
	public TreeViewNode? LastSelectedNode { get; set; } = null;
	public string? LastSelectedNodePath { get; set; } = null;
	// if EntryPollingMs is set to less than or equal to 0 then it will not poll for changes
	public int EntryPollingMs { get; set; } = 5000;

	public bool IsTreeLoaded { get; set; } = false;
	public bool IsListLoaded { get; set; } = false;

	public HpDirectory root;
	public static bool IsClosing = false;
	public string SWKey;
	internal delegate void BackgroundMethodDel(object sender, DoWorkEventArgs e);
	internal delegate void BackgroundCompleteDel(object sender, RunWorkerCompletedEventArgs e);
	public static DispatcherQueue HackDispatcherQueue;
	internal TabViewItem? LowerTabIndex
	{
		get => VersionTabs.SelectedItem as TabViewItem;
		set => VersionTabs.SelectedItem = value;
	}

	// temp
	//public ListView OdooEntryList = new();
	#endregion
	#region Initializers

	public HackFileManager() 
	{ 
		InitializeComponent();
		HackLoaded = false;
#if DEBUG
		DebugTest();
#endif
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
		_treeHelper.InjectHFM(this);
		_gridHelper = HackApp.Services.GetRequiredService<GridHelp>();
		_gridHelper.InjectHFM(this);
		
		
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
		GridHelp.ResetListViews(_hackLists.AllLists);
		OdooDirectoryTree.LostFocus += (s, e) =>
		{
			if (OdooDirectoryTree.SelectedNode is null) return;
			LastSelectedNode = OdooDirectoryTree.SelectedNode;
			LastSelectedNodePath = LastSelectedNode?.LinkedData.FullPath;
		};
		this.Unloaded += (s, e) =>
		{
			IsClosing = true;
			_cSource.Cancel();
			_cTreeSource.Cancel();
			_backgroundWorker.CancelAsync();
		};
		if (IsLoaded)
		{
			Task.Run(HackFileManager_Load);
			return;
		}
		
		this.Loaded += (_, _) => Task.Run(HackFileManager_Load);
		HackLoaded = true;
	}
#if DEBUG
	private static async Task DebugTest()
	{

	}
#endif
	private void AssignCollections()
	{
		OdooDirectoryTree.ItemsSource = ONodes;
		OdooEntryList.ItemsSource = OEntries;
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
			{ OdooEntryList, OEntries },
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

		OdooEntryList.SelectionChanged	+= OdooEntryList_SelectionChanged;
		OdooEntryList.Sorting			+= List_ColumnClick;
		OdooEntryList.LoadingRow		+= OdooEntryList_LoadingRow;

		// tree events
		TreeAnalyze.Click				+= (sender, args) => { };
		TreeCheckout.Click				+= Tree_Click_Checkout;
		TreeCommit.Click				+= Tree_Click_Commit;
		TreeDownload.DoubleTapped		+= Tree_Click_GetLatest;
		TreeDownloadAll.Click			+= Tree_Click_GetLatestAll;
		TreeDownloadTop.Click			+= Tree_Click_GetLatestTop;
		TreeOpenDirectory.Click			+= Tree_Click_OpenDirectory;
		TreeUndoCheckout.Click			+= Tree_Click_UndoCheckout;
		TreeLogicalDelete.DoubleTapped	+= Tree_Click_LogicalDelete;
		TreeLocalDelete.Click			+= Tree_Click_LocalDelete;
		TreePermanentDelete.Click		+= Tree_Click_PermanentDelete;
		TreeUndelete.DoubleTapped		+= Tree_Click_Restore;
		TreeRestoreAll.Click			+= Tree_Click_RestoreAll;
		TreeRestoreTop.Click			+= Tree_Click_RestoreTop;
		
		// entry datagrid events
		ListCheckout.Click				+= List_Click_Checkout;
		ListCommit.Click				+= List_Click_Commit;
		ListDelete.DoubleTapped			+= ListDelete_DoubleClicked;
		ListDeleteLocal.Click			+= List_Click_LocalDelete;
		ListDeleteLogical.Click			+= List_Click_LogicalDelete;
		ListDeletePermanent.Click		+= List_Click_PermanentDelete;
		ListGetLatest.Click				+= List_Click_GetLatest;
		ListLocal.Click					+= List_Click_OpenLatestLocal;
		ListUndoCheckout.Click			+= List_Click_UndoCheckout;
		ListPreview.Click				+= List_Click_OpenLatestRemote;
		ListFileDirectory.Click			+= List_Click_OpenDirectory;
		ListRestore.Click				+= List_Click_Restore;
		SaveIcon.Click					+= List_Click_SaveIcon;	
		ListOpen.DoubleTapped			+= List_Click_Open;

		// additional toolbar
		OdooRefreshDropdown.Click		+= AdditionalTools_Click_Refresh;
		OdooSearchDropdown.Click		+= AdditionalTools_Click_Search;
		OdooManageTypesDropdown.Click	+= AdditionalTools_Click_ManageTypes;

		// tabbed datagrids
		OdooHistory.SelectionChanged	+= OdooHistory_ItemSelectionChanged;
		OdooHistory.DoubleTapped		+= History_DoubleClick;
		OdooParents.SelectionChanged	+= OdooParents_ItemSelectionChanged;
		OdooParents.DoubleTapped		+= OdooParents_DoubleClick;
		OdooChildren.SelectionChanged	+= OdooChildren_ItemSelectionChanged;
		OdooChildren.DoubleTapped		+= OdooChildren_DoubleClick;

		// history datagrid
		HistoryDownload.DoubleTapped	+= History_Click_Download;
		HistoryDownloadTemp.Click		+= History_Click_TemporaryDownload;
		HistoryDownloadOverwrite.Click	+= History_Click_OverwriteDownload;
		HistoryOpen.DoubleTapped		+= History_Click_Open;
		HistoryOpenTemp.Click			+= History_Click_TemporaryOpen;
		HistoryOpenOverwrite.Click		+= History_Click_OverwriteOpen;
		HistoryMove.DoubleTapped		+= History_Click_TemporaryMove;
		HistoryMoveTemp.Click			+= History_Click_TemporaryMove;
		HistoryMoveOverwrite.Click		+= History_Click_OverwriteMove;
	}

	private void OdooEntryList_LoadingRow( object? sender, DataGridRowEventArgs e )
	{
		var row = e.Row;
		var item = row.DataContext as EntryRow;
		switch( item?.Status )
		{
			case FileStatus.Lo:
			{
				row.Background = UIStorage.OrangeBrush;
				break;
			}
			case FileStatus.Ro:
			{
				row.Background = UIStorage.BlueBrush;
				break;
			}
			case FileStatus.Ok:
			{
				goto default;
				break;
			}
			case FileStatus.Nv:
			{
				goto default;
				break;
			}
			case FileStatus.Lm:
			{
				goto default;
				break;
			}
			case FileStatus.Dt:
			{
				row.Background = UIStorage.RedBrush;
				break;
			}
			case FileStatus.Ds:
			{
				row.Background = UIStorage.RedBrush;
				break;
			}
			case FileStatus.If:
			{
				goto default;
				break;
			}
			case FileStatus.Ft:
			{
				goto default;
				break;
			}
			case FileStatus.Cm:
			{
				row.Background = UIStorage.GreenBrush;
				break;
			}
			case FileStatus.Co:
			{
				row.Background = UIStorage.GreenBrush;
				break;
			}
			default:
			{
				row.Background = null;
				break;
			}
		}
	}

	private void List_Click_SaveIcon(object sender, RoutedEventArgs e)
	{
		throw new NotImplementedException();
		if (OdooEntryList.SelectedItem is not EntryRow entry 
			|| entry.Status is not (FileStatus.Lo or FileStatus.Ft or FileStatus.If)
			|| entry.FullName is null) return;

		//var icon = Icon.ExtractAssociatedIcon(Path.Combine(entry.FullName));
		
		//var bitmap = icon?.ToBitmap();
	}
	private void OdooDirectoryTree_RightTapped(object sender, RightTappedRoutedEventArgs e)
	{
		var tree = sender as TreeView;
		var elem = e.OriginalSource is FrameworkElement ui ? ui.DataContext as TreeData : null;
		tree?.SelectedNode = elem?.Node;
		ODT_SetLastSelected(elem);
	}
	private async Task HackFileManager_Load()
	{
		await Task.Delay(500);
		await _treeHelper.CreateTreeViewBackground(OdooDirectoryTree);
	}
	public TreeView GetOdooDirectoryTree()
		=> OdooDirectoryTree;
	public DataGrid GetOdooEntryList()
			=> OdooEntryList;
	public Image GetOdooEntryImage() => OdooEntryImage;
	public ProgressRing GetProgressRing() => LoadRing;
	public TextBlock GetEntriesLabel() => EntryListStatus;
	public TextBlock GetEntriesLocalLabel() => EntryListLocalOnly;
	public TextBlock GetEntriesRemoteLabel() => EntryListRemoteOnly;
	public (Image, ProgressRing) GetVisualizer() => (OdooEntryImage, LoadRing);
	internal void RestartEntries() => _treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	internal async Task RestartTree() => await _treeHelper.RestartTree(OdooDirectoryTree);
#endregion
	#region TEST_VARIABLES
#if DEBUG
	public Stopwatch TimerStopwatch;
#endif
	#endregion
}
public sealed partial class HackFileManager : Page
{
	private void OdooDirectoryBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
	{
		var tData = args.Item as TreeData;
		if (tData is null or { Node: null}) return;

		LastSelectedNode = tData.Node;
		OdooDirectoryTree.SelectedNode = tData.Node;
		tData.EnsureVisible(OdooDirectoryTree);
		LastSelectedNodePath = tData.Node?.LinkedData.FullPath;
		LastSelectedNode?.UpdateBreadCrumbCollection(LastSelectedNodePaths);
		
		foreach (var child in tData.Node!.Children)
		{
			child.IsExpanded = false;
		}
	}
	private async void Tree_Click_Undelete(object sender, RoutedEventArgs e)
	{
		await UnDeleteInternal();
	}
	private void ListDelete_DoubleClicked(object sender, DoubleTappedRoutedEventArgs e)
		=> List_Click_LocalDelete(sender, e);
	#region Background Worker functions
	private async Task Async_GetLatest((ArrayList, CancellationToken) arguements)
	{
		object lockObject = new();
		ArrayList entryIDs = arguements.Item1;

		// add status lines for entry id and upcoming versions
		lock (lockObject)
		{
			Dialog?.AddStatusLine(StatusMessage.FOUND, $"{entryIDs.Count} entries");
			Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Retrieving all latest versions associated with entries...");
		}

		var versions = GetLatestVersions(entryIDs, ["preview_image", "entry_id", "node_id", "file_modify_stamp", "attachment_id", "file_contents"]);

		IEnumerable<IEnumerable<HpVersion>>? versionBatches = Help.BatchArray(versions, DownloadBatchSize);

		MaxCount = versions.Length;
		SkipCounter = 0;
		ProcessCounter = 0;
		Downloaded = 0;

		if (versionBatches is null)
		{
			MessageBox.ShowAsync("Cancelled Download... No Versions to Process");
			return;
		}
		try
		{
			await ProcessDownloadsAsync(versionBatches, arguements.Item2, OdooDefaults.Instance.ConcurrencySize);
		}
		catch
		{
			await MessageBox.ShowAsync("Cancelled Download");
		}

		Dialog?.SetProgressBar(versions.Length, versions.Length);
		
		await MessageBox.ShowAsync("Completed!");
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private async Task Async_Commit(List<HackFile>? hacks)
	{
		object lockObject = new();
		// section for checking if the existing remote file already has a version with the same checksum 
		// or possibly an entry that has a newer version from that which is downloaded locally

		ConcurrentSet<HackFile> hackFiles;
		ConcurrentSet<HackFile> hackFilesInOdoo;
		// testing filter hacks..
		// entries = entries is not null && !entries.IsEmpty ? await Commit.FilterCommitEntries(entries) : [];

		// section for checking if hack files have a checksum that matches the fullpath
		(hackFiles, hackFilesInOdoo) = hacks is not null && hacks.Count > 0 ? await FilterCommitHackFiles(hacks) : ([], []);


		//HpVersion[] localConversions = new HpVersion[hackFiles.Count];
		List<HpVersion> localConversions = [];
		int index = 0;
		while (hackFiles.TryTake(out HackFile result))
		{
			(EntryReturnType entryReturn, HpVersion? newVersion) = await ConvertHackFile(result);
			if ((entryReturn 
				is EntryReturnType.Created 
				or EntryReturnType.GotExisting)
				&& newVersion is { }) localConversions.Add(newVersion);
		}
		var localVersions = Help.BatchArray(localConversions, DownloadBatchSize);

		
		ProcessCounter = 0;
		SkipCounter = 0;

		statusToken = await statusToken.RenewTokenSourceAsync();
		MaxCount = localVersions?.Length ?? 0;
		Dialog?.IsInProcess = true;

		if (localVersions is not null)
		{
			Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"--- Preparing to commit versions ---");
			for (int i = 0; i < localVersions.Length; i++)
			{
				Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Commiting local version batch {i + 1}/{localVersions.Length}...");
				HpVersionRelationship.Create(localVersions[i]);
				Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Commiting local version relationships ...");
				HpVersionProperty.Create(localVersions[i]);
				Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Commiting local version properties ...");
				Dialog?.SetProgressBar((SkipCounter + ProcessCounter) / 3, MaxCount);
				ProcessCounter += 1;
			}
		}
		// create new parent, child hp_version_relationship's for versions
		Dialog?.SetProgressBar(MaxCount, MaxCount);

		await MessageBox.ShowAsync($"Completed!");
		await _treeHelper.RestartTree(OdooDirectoryTree);
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private async Task Async_CheckOut(HpEntry[] entries)
	{
		object lockObject = new();
		entries = [.. FilterCheckoutEntries(entries)];
		
		ProcessCounter = 0;
		SkipCounter = 0;
		MaxCount = entries.Length;
		Dialog?.AddStatusLine(StatusMessage.INFO, $"{MaxCount} check outs");
		for (int i = 0; i < entries.Length; i++)
		{
			HpEntry entryModel = entries[i];

			lock (lockObject)
			{
				Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Checking out {entryModel.name} ({entryModel.id})");
			}
			await CheckOutEntry(entryModel);

			lock (lockObject)
			{
				ProcessCounter += 1;
				Dialog?.SetProgressBar((SkipCounter + ProcessCounter), MaxCount);
			}
		}

		Dialog?.SetProgressBar(MaxCount, MaxCount);
		await MessageBox.ShowAsync($"Completed!");
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private async Task Async_UnCheckOut(HpEntry[] entries)
	{
		object lockObject = new();

		ProcessCounter = 0;
		SkipCounter = 0;
		MaxCount = entries.Length;
		Dialog?.AddStatusLine(StatusMessage.INFO, $"{MaxCount} uncheck outs");
		for (int i = 0; i < entries.Length; i++)
		{
			HpEntry entryModel = entries[i];

			lock (lockObject)
			{
				Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Unchecking out {entryModel.name} ({entryModel.id})");
			}
			await UnCheckOutEntry(entryModel);

			lock (lockObject)
			{
				ProcessCounter += 1;
				Dialog?.SetProgressBar((SkipCounter + ProcessCounter), MaxCount);
			}
		}

		Dialog?.SetProgressBar(MaxCount, MaxCount);
		await MessageBox.ShowAsync($"Completed!");
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private async Task Async_PermDelete(HpEntry[] entries)
	{
		ArrayList ids = entries.Select(e => e.id).ToArrayList();
		bool vDeleted = false;

		// using DeleteEntry also deletes entries, versions, version props, version relationships, and ir attachment records
		DialogResult result = await MessageBox.ShowAsync($"Are you sure you want to permanently delete {ids.Count} entries from the database?\n" +
											  $"This will also permanently delete all associative versions, version properties, and version relationships", "Delete Entries and Other Records?", MessageBoxButtons.YesNoCancel);

		if (result is not DialogResult.Yes and not DialogResult.OK) return;

		vDeleted = await PermanentDeleteEntry(ids);

		if (vDeleted)
		{
			Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Completed permanent delete");
		}
		else
		{
			MessageBox.ShowAsync("Was unable to delete entries", "Error", buttons: MessageBoxButtons.OKCancel, icon: MessageBoxIcon.Error);
			return;
		}

		await MessageBox.ShowAsync($"Completed!");
		await _treeHelper.RestartTree(OdooDirectoryTree);
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private async Task Async_LogicalDelete(HpEntry[] entries)
	{
		object lockObject = new();
		foreach (var entry in entries)
		{
			lock (lockObject)
			{
				Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Setting InActive {entry.name}: {entry.id}");
			}
			await entry.LogicalDelete();

		}

		await MessageBox.ShowAsync($"Completed!");
		await _treeHelper.RestartTree(OdooDirectoryTree);
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private async Task Async_LogicalUnDelete(HpEntry[] entries)
	{
		object lockObject = new();
		foreach (var entry in entries)
		{
			lock (lockObject)
			{
				Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Setting Active {entry.name}: {entry.id}");
			}
			await entry.LogicalUnDelete();
		}

		Dialog?.SetProgressBar(5, 5);
		await MessageBox.ShowAsync($"Completed!");
		await _treeHelper.RestartTree(OdooDirectoryTree);
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private async Task Async_ListItemChange(EntryRow item, CancellationToken token)
	{
		try
		{
			await ProcessEntrySelectionAsync(item, token);
		}
		catch (Exception) { }
	}
	internal async Task ProcessEntrySelectionAsync(EntryRow? entry, CancellationToken token, bool listLatestVersionInfo = false)
	{
		if (entry is null) return;

		switch (LowerTabIndex?.Name)
		{
			case StorageBox.HISTORY_TAB:
				
				await SafeHelper.SafeInvokerAsync(async () =>
				{
					await _gridHelper.ProcessHistorySelectAsync(OdooHistory, entry, token);
					OdooHistory.UpdateLayout();
				});
				break;
			case StorageBox.PARENT_TAB:
				await SafeHelper.SafeInvokerAsync(async () =>
				{
					await _gridHelper.ProcessParentSelectAsync(OdooParents, entry, token);
					OdooParents.UpdateLayout();
				});
				break;
			case StorageBox.CHILD_TAB:
				await SafeHelper.SafeInvokerAsync(async () =>
				{
					await _gridHelper.ProcessChildSelectAsync(OdooChildren, entry, token);
					OdooChildren.UpdateLayout();
				});
				break;
			case StorageBox.PROPERTIES_TAB:
				await SafeHelper.SafeInvokerAsync(async () =>
				{
					await _gridHelper.ProcessPropertiesSelectAsync(OdooProperties, entry, token);
					OdooProperties.UpdateLayout();
				});
				break;
			case StorageBox.INFO_TAB:
				await SafeHelper.SafeInvokerAsync(async () =>
				{
					await _gridHelper.ProcessInfoSelectAsync(OdooVersionInfoList, entry, token);
					OdooVersionInfoList.UpdateLayout();
				});
				break;
		}

		token.ThrowIfCancellationRequested();

		if (entry.LatestId is int id)
		{
			await _gridHelper.PreviewImage(id);
		}
	}
	
	#endregion
public async static Task<(EntryReturnType, HpVersion?)> ConvertHackFile(HackFile hackFile)
    {
        Hashtable ht = [];
            
        ArrayList paths = hackFile.RelativePath.Split<ArrayList>("\\", StringSplitOptions.RemoveEmptyEntries);

		EntryReturnType entryReturn = EntryReturnType.Failed;
		try
        {
			// create directories that don't exist in odoo
			HpDirectory[] directories = await HpDirectory.CreateNew(paths);
			HpDirectory lastDirectoryModel = directories.Last() ?? throw new Exception($"{HpDirectory.GetHpModel()} didn't create any records");
            // create an HpEntry that doesn't exist in odoo
            (entryReturn, HpEntry? entry) = await HpEntry.GetFallbackCreateEntryAsync(hackFile, lastDirectoryModel.id ?? 0);

			switch (entryReturn)
			{
				case EntryReturnType.Created:
				{
					HackFileManager.Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Created new entry for {hackFile.Name}"); 
					break;
				}
				case EntryReturnType.GotExisting:
				{
					HackFileManager.Dialog?.AddStatusLine(StatusMessage.FOUND, $"Found existing entry for {hackFile.Name}"); 
					break;
				}
				case EntryReturnType.Failed:
				{
					HackFileManager.Dialog?.AddStatusLine(StatusMessage.ERROR, $"Failed to create entry for {hackFile.Name}"); 
					throw new Exception($"{hackFile.Name} was unable to create or get record");
				}
				case EntryReturnType.InvalidType:
				{
					if (OdooDefaults.Instance.RestrictTypes is true)
					{
						HackFileManager.Dialog?.AddStatusLine(StatusMessage.ERROR, $"Found invalid type for {hackFile.Name}, file extension {hackFile.TypeExt}");
						throw new Exception($"found invalid type for {hackFile.Name}, file extension {hackFile.TypeExt}");
					}
					else
						HackFileManager.Dialog?.AddStatusLine(StatusMessage.WARNING, $"Found invalid type for file extension {hackFile.TypeExt}, but continuing due to unrestricted types");
					break;
				}
			}

			// create an HpVersion that doesn't exist in odoo
			HpVersion version = await OdooDefaults.CreateNewVersion(hackFile, entry);
			if (version.id is 0) entryReturn = EntryReturnType.Failed;
			return (entryReturn, version);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
        }
        return (entryReturn, null);
    }
	#region CheckOut Functions
	private static IEnumerable<HpEntry> FilterCheckoutEntries(HpEntry[] entries)
	{
		foreach (HpEntry entry in entries)
		{
			if (entry.checkout_user?.id is null or 0)
			{
				yield return entry;
			}
		}
	}
	private static IEnumerable<HpEntry> FilterUnCheckoutEntries(HpEntry[] entries)
	{
		foreach (HpEntry entry in entries)
		{
			if (entry.checkout_user is not null && entry.checkout_user == OdooDefaults.Instance.OdooId)
			{
				yield return entry;
			}
		}
	}
	private async Task CheckOutEntry(HpEntry? entry)
	{
		if (entry == null)
			return;

		await entry.CheckOut();
	}
	private async Task UnCheckOutEntry(HpEntry entryModel)
	{
		if (entryModel == null)
			return;

		await entryModel.UnCheckOut();
	}
	#endregion

	#region Commit Functions
	private static async Task<(ConcurrentSet<HackFile>, ConcurrentSet<HackFile>)> FilterCommitHackFiles(ConcurrentSet<HackFile> hackFiles)
	{
		List<Task<HackFile>> tasks = [];
		object lockObject = new();
		string combinedPattern = string.Join("|", OdooDefaults.Instance.EntryFilterPatterns);
		var regex = new Regex(combinedPattern, RegexOptions.IgnoreCase);
		//string[] filePaths = hackFiles.Select(hack => hack.FullPath).ToArray();

		List<HackFile> hacks = [];
		foreach (HackFile hack in hackFiles)
		{
			regex = new Regex(combinedPattern, RegexOptions.IgnoreCase);
			if (!regex.IsMatch($".{hack.TypeExt.ToLower()}"))
			{
				hacks.Add(hack);
			}
		}
		return await FilesNotInOdooSegmented(hacks);
	}
	public async static Task<(HackFile[], HackFile[])> FilesNotInOdooSegmented(IEnumerable<HackFile> hackFiles)
	{        
		HackFile[] hackArr = [.. hackFiles];
		List<HackFile> hacks = [];
		List<HackFile> hacksFound = [];

		ArrayList[] arrayList = new ArrayList[hackArr.Length];


		for (int i = 0; i < hackArr.Length; i++)
		{
			bool isFound = false;
			string filepath = hackArr[i].TypeExt.ToLower();
			if (OdooDefaults.Instance.RestrictTypes is true && !OdooDefaults.Instance.ExtToType.ContainsKey(filepath ?? "-=-=-")) continue;

			string filePath = FileOperations.WindowsToOdooPath(hackArr[i].RelativePath);
			ArrayList arrList =
			[

				new ArrayList() { "name", "=", hackArr[i].Name },
				new ArrayList() { "directory_complete_name", "=", filePath },
                
			];
			HpEntry? entry = (await HpEntry.GetRecordsBySmartSearchAsync(searchFilter: arrList, includedFields: ["name", "dir_id"], insertFields: ["version_ids.checksum"]))?.FirstOrDefault();
			ArrayList fields = [];

			if (entry is not null && entry.HashedValues.TryGetValue("checksum", out ArrayList? arr))
			{
				// this means that this hackFile is in the database so it can be skipped
				if (arr.FirstOrDefault<string>(x => x.ToString() == hackArr[i].Checksum) is string checksum)
				{
					HackFileManager.Dialog?.AddStatusLine(StatusMessage.FOUND, $"checksum found remotely ({checksum}) for: {filePath}");
					hacksFound.Add(hackArr[i]);
					continue;
				}
			}

			HackFileManager.Dialog?.AddStatusLine(StatusMessage.INFO, $"Queued commit for {hackArr[i].Name} (Checksum: {hackArr [ i ].Checksum}) for: {filePath}" );
			hacks.Add( hackArr [ i ] );
		}
		return ([.. hacks], [.. hacksFound]);
	}
	#endregion

	#region Latest Functions
	private HpVersion[] GetLatestVersions(ArrayList entryIDs, string[]? excludedFields = null)
	{
		if (excludedFields == null) excludedFields = ["preview_image", "file_contents"];
		return HpEntry.GetRelatedRecordByIds<HpVersion>(entryIDs, "latest_version_id", excludedFields);
	}
	private async Task ProcessVersionBatchAsync(IEnumerable<HpVersion> batchVersions)
	{
		object lockObject = new();
		ConcurrentBag<HpVersion> processVersions = [];
		ConcurrentBag<int> unprocessedVersions = [];
		List<Task> tasks = [];

        foreach (HpVersion version in batchVersions)
		{
			bool willProcess = true;

			// ==============================================================
			// check to see if the version has a checksum and if it is the
			// same as the one locally; if not don't download
			// ==============================================================
			if (version.checksum == null || version.checksum.Length == 0 || version.checksum == "False")
			{
				QueueAsyncStatus.Enqueue((StatusMessage.ERROR, $"Checksum not found for version: {version.name}"));
				SkipCounter++;
				willProcess = false;
			}
			if (willProcess && FileOperations.SameChecksum(version, ChecksumType.Sha1))
			{

				//unprocessedVersions.Add(version.ID);
				QueueAsyncStatus.Enqueue((StatusMessage.FOUND, $"Skipping version download: {version.name}"));
				SkipCounter++;
				willProcess = false;
			}
			// ==============================================================
			if (willProcess)
			{
				string fileName = Path.Combine(version.WinPathway, version.name);
				processVersions.Add(version);

				QueueAsyncStatus.Enqueue((StatusMessage.PROCESSING, $"Downloading latest version: {fileName}"));
				ProcessCounter++;
			}
			TotalProcessed = SkipCounter + ProcessCounter;
		}

		await Task.Run(async () =>
		{
			if (!processVersions.IsEmpty)
			{
				Task<(int, long)?[]> finishSuccesses = Task.WhenAll(HpVersion.BatchDownloadFiles([.. processVersions]));
				await finishSuccesses;
				//return finishSuccesses.Result[0];
			}
			return 0;
		}, statusToken.Token);
	}
	public async Task ProcessDownloadsAsync(IEnumerable<IEnumerable<HpVersion>> versionBatches, CancellationToken cToken, int maxConcurrency = 2)
	{
		SemaphoreSlim throttler = new(maxConcurrency);
        int size = versionBatches.Count();
        List<Task> allTasks = new(size);

		Dialog?.UpdateStatusDialogLoop(cToken);

		foreach (var batch in versionBatches ?? [])
		{
			Task task = Task.Run(async () =>
			{
				await throttler.WaitAsync(cToken);
				cToken.ThrowIfCancellationRequested();
				try
				{
					await ProcessVersionBatchAsync(batch);
				}
				finally
				{
					throttler.Release();
				}
			}, cToken);

			allTasks.Add(task);
		}

		await Task.WhenAll(allTasks);
		Dialog?.EndStatusDialogLoop();
	}
	private async void GetLatestFromTreeNode(bool withSubdirectories = false)
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;

		statusToken = await statusToken.RenewTokenSourceAsync();
		object lockObject = new();

		TreeViewNode? tnCurrent = LastSelectedNode;
		TreeData? data = LastSelectedNode?.LinkedData;

		if (tnCurrent == null)
		{
			MessageBox.ShowAsync("current directory doesn't exist remotely");
			return;
		}

		// directory only needs ID set to find that record's entries
		HpDirectory directoryModel = new()
		{
			id = data?.DirectoryId ?? 0,
			name = data?.Name ?? "",			
		};

		lock (lockObject)
		{
			Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Retrieving all entries and their and their associated dependencies within directory ({directoryModel.name}, id: {directoryModel.id})");
		}

		Dialog?.IsInProcess = true;

		ArrayList? entryIDs = await directoryModel.GetDirectoryEntryIDsAsync(withSubdirectories, ShowInactive.IsChecked ?? false);
		statusToken.Token.Register(CancelledOperation);

		await GetLatestInternal(entryIDs);
	}
	#endregion

	#region Form Event Handlers
    // after select events
    private async void ODT_SetLastSelected(TreeData? tData)
	{
		
		LastSelectedNode = tData?.Node;
		LastSelectedNodePath = LastSelectedNode?.LinkedData.FullPath;
		LastSelectedNode?.UpdateBreadCrumbCollection(LastSelectedNodePaths);
		
		IsListLoaded = false;
		//if (LastSelectedNode is not null)
		//{
		//	_treeItemChange = _treeHelper.TreeSelectItem(OdooDirectoryTree, LastSelectedNode, OdooEntryList, _cTreeSource.Token);
		//	await _treeItemChange;
		//}

	}
	private async void OdooDirectoryTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
	{
		_queuedTreeChange = (null, null);
		if (_treeItemChange is not null and { IsCompleted: false })
		{
			//_queuedTreeChange = (sender, args);
			return;
		}

		if (_treeItemChange is null or { IsCompleted: true })
		{
			_cSource.Cancel();
			_cTreeSource = new();

			// Store the currently selected node
			if (args.AddedItems.Count > 0)
			{
				LastSelectedNode = (args.AddedItems.First() as TreeData)?.Node;
				LastSelectedNodePath = LastSelectedNode?.LinkedData.FullPath;
				LastSelectedNode?.UpdateBreadCrumbCollection(LastSelectedNodePaths);
			}

			IsListLoaded = false;
			if (LastSelectedNode is not null)
			{
				_treeItemChange = _treeHelper.TreeSelectItem(sender, LastSelectedNode, OdooEntryList, _cTreeSource.Token);
				await _treeItemChange;
			}

			if (_queuedTreeChange.sender != null && _queuedTreeChange.args != null)
			{
				OdooDirectoryTree_SelectionChanged(sender, args);
			}
		}
	}
	// item selection change events
	private async void VersionTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count == 0) return;
		if (OdooEntryList.SelectedItem is not EntryRow entry) return;
		if (_cSource is not null) await _cSource.CancelAsync();
		_cSource = new();
		_ = ProcessEntrySelectionAsync(entry, _cSource.Token);
	}
	private async void OdooEntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (OdooEntryList.SelectedItems.Count > 1 || e.AddedItems.Count == 0)
			return;

		_queuedEntryChange = (null, null);
		if (_entryListChange is not null and { IsCompleted: false })
		{
			_queuedEntryChange = (sender, e);
			return;
		}
		GridHelp.ResetListViews(_hackLists.SubLists);
		if (OdooEntryList.SelectedItems.Count == 0)
			return;

		OdooEntryImage.Source = null;
		if (_entryListChange is not (null or { IsCompleted: true })) return;

		_cSource = new();
		var listViewItem = e.AddedItems.First() as EntryRow;
		if (listViewItem != null)
		{
			_entryListChange = Async_ListItemChange(listViewItem, _cSource.Token);
			await _entryListChange;
		}
		if (_queuedEntryChange.sender != null && _queuedEntryChange.e != null)
		{
			OdooEntryList_SelectionChanged(_queuedEntryChange.sender, _queuedEntryChange.e);
		}
	}
	private void OdooHistory_ItemSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count == 0) return;
		PreviewImageSelection((e.AddedItems.First() as HistoryRow)); //, NameConfig.HistoryVersion.Name);
	}
	private void OdooParents_ItemSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count == 0) return;
		PreviewImageSelection((e.AddedItems.First() as ParentRow)); //, NameConfig.ParentVersion.Name);
	}
	private void OdooChildren_ItemSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count == 0) return;
		PreviewImageSelection((e.AddedItems.First() as ChildrenRow)); //, NameConfig.ChildrenVersion.Name);
	}
	// change events
	private async void ShowInactive_Checked(object sender, RoutedEventArgs e)
	{
		IsActive = ShowInactive.IsChecked ?? false;
		if (LastSelectedNode is not null)
		{
			await _treeHelper.TreeSelectItem(OdooDirectoryTree, LastSelectedNode!, OdooEntryList);
		}
	}
	private void ShowHidden_Checked(object sender, RoutedEventArgs e)
	{

	}
	// tree open events
	private void OdooCMSTree_Opening(object sender, CancelEventArgs e)
	{
		string pathway = LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, LastSelectedNodePath[5..]);
		if (Directory.Exists(pathway))
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
	private void List_ColumnClick(object? sender, DataGridColumnEventArgs e)
	{
		var grid = sender as DataGrid;
		var column = e.Column;
		foreach (var col in grid?.Columns ?? [])
		{
			if (e.Column == col) continue;
			col.SortDirection = null;
		}
		var modelField = column.ClipboardContentBinding.Path.Path;
		bool isDesc = false;
		(column.SortDirection, isDesc) = column.SortDirection is not null && column.SortDirection == DataGridSortDirection.Ascending
			? (DataGridSortDirection.Descending, true)
			: (DataGridSortDirection.Ascending, false);
		
		switch (modelField)
		{
			case null: return;
			case nameof(EntryRow.Name):
			{
				OEntries.Sort((s, o) 
					=> string.Compare(s.Name, o.Name, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase), isDesc);
				break;
			}
			case nameof(EntryRow.Id):
			{
				OEntries.Sort((s, o) 
					=> s.Id.Compare(o.Id), isDesc);
				break;
			}
			case nameof(EntryRow.Checkout):
			{
				OEntries.Sort((s, o) 
					=> string.Compare(s.Checkout?.name, o.Checkout?.name, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase), isDesc);
				break;	
			}
			case nameof(EntryRow.Size):
			{
				OEntries.Sort((s, o) 
					=> Nullable.Compare(s.Size, o.Size), isDesc);
				break;	
			}
			case nameof(EntryRow.Type):
			{
				OEntries.Sort((s, o) 
					=> string.Compare(s.Type, o.Type, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase), isDesc);
				break;	
			}
			case nameof(EntryRow.Status):
			{
				OEntries.Sort((s, o) 
					=> string.Compare(Enum.GetName(s.Status), Enum.GetName(o.Status), CultureInfo.InvariantCulture, CompareOptions.IgnoreCase), isDesc);
				break;	
			}
			case nameof(EntryRow.LatestId):
			{
				OEntries.Sort((s, o) 
					=>  Nullable.Compare(s.LatestId, o.LatestId), isDesc);
				break;	
			}
			case nameof(EntryRow.RemoteDate):
			{
				OEntries.Sort((s, o) 
					=> Nullable.Compare(s.RemoteDate , o.RemoteDate), isDesc);
				break;	
			}
			case nameof(EntryRow.LocalDate):
			{
				OEntries.Sort((s, o) 
					=> Nullable.Compare(s.LocalDate, o.LocalDate), isDesc);
				break;	
			}
			case nameof(EntryRow.Category):
			{
				OEntries.Sort((s, o) 
					=> string.Compare(s.Category?.name, o.Category?.name, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase), isDesc);
				break;	
			}
			case nameof(EntryRow.FullName):
			{
				OEntries.Sort((s, o) 
					=> string.Compare(s.FullName, o.FullName, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase), isDesc);
				break;	
			}
			default: return;
		}
		
		// e.Column.SortDirection = e.Column.SortDirection == DataGridSortDirection.Ascending 
		// 	? DataGridSortDirection.Descending
		// 	: DataGridSortDirection.Ascending;
	}

	//
	private void Tree_Click_GetLatest(object sender, RoutedEventArgs e)
		=> GetLatestFromTreeNode(true);
	private void Tree_Click_GetLatestAll(object sender, RoutedEventArgs e)
		=> Tree_Click_GetLatest(sender, e);
	private void Tree_Click_GetLatestTop(object sender, RoutedEventArgs e)
		=> GetLatestFromTreeNode(false);
	private async void Tree_Click_Commit(object sender, RoutedEventArgs e)
	{
		string pathway = LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, LastSelectedNodePath?[5..] ?? "");
		//HpDirectory hpDirectory;
		TreeData? dat = LastSelectedNode?.LinkedData;
		if (dat?.IsRemoteOnly is true) return;
		
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;

		statusToken = await statusToken.RenewTokenSourceAsync();
		//ArrayList? entryIDs = await HpDirectory.GetDirectoryEntryIDsAsync(dat?.DirectoryId ?? 0, true);
		if (statusToken.IsCancellationRequested) return;
		
		HackFile.GetHackFolderWithDependencies(pathway, true, out List<HackFile> hf);
		await CommitInternal(hf);
	}
	private async void Tree_Click_Checkout(object sender, RoutedEventArgs e)
	{
		GetLatestFromTreeNode(true);
		ArrayList? entryIDs = await HpDirectory.GetDirectoryEntryIDsAsync(LastSelectedNode?.LinkedData.DirectoryId ?? 0, true);
		await CheckoutInternal(entryIDs);
	}
	private async void Tree_Click_UndoCheckout(object sender, RoutedEventArgs e)
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;
		ArrayList? entryIDs = await HpDirectory.GetDirectoryEntryIDsAsync(LastSelectedNode?.LinkedData.DirectoryId ?? 0, true);

		await UnCheckoutInternal(entryIDs);
	}
	private void Tree_Click_OpenDirectory(object sender, RoutedEventArgs e)
	{
		string pathway = LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, LastSelectedNodePath[5..]);
		if (Directory.Exists(pathway))
		{
			Process.Start("explorer.exe", pathway);
		}
	}
	private async void Tree_Click_Restore(object sender, RoutedEventArgs e)
		=> await UnDeleteInternal(false);
	private async void Tree_Click_RestoreTop(object sender, RoutedEventArgs e)
		=> await UnDeleteInternal(false);
	private void Tree_Click_RestoreAll(object sender, RoutedEventArgs e)
		=> MessageBox.ShowAsync("Not Implemented Yet");
	private async void Tree_Click_LocalDelete(object sender, RoutedEventArgs e)
	{
		string pathway = LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, LastSelectedNodePath[5..]);
		DirectoryInfo directory = new(pathway);
		if (directory.Exists)
		{
			if (await MessageBox.ShowAsync($"Are you sure you want to delete this directory and ({directory.EnumerateFiles().Count()}) files inside?",
					"Delete Directory",
					buttons: MessageBoxButtons.YesNoCancel,
					icon: MessageBoxIcon.Warning) == DialogResult.Yes)
			{
				directory.Delete(true);
			}
		}
	}
	private async void Tree_Click_LogicalDelete(object sender, RoutedEventArgs e)
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;

		ArrayList? entryIDs = await HpDirectory.GetDirectoryEntryIDsAsync(LastSelectedNode?.LinkedData.DirectoryId ?? 0, true);

		await LogicalDeleteInternal(entryIDs);
	}
	private void Tree_Click_PermanentDelete(object sender, RoutedEventArgs e)
	{
#if DEBUG

#endif
	}
	//
	internal async void List_Click_GetLatest(object sender, RoutedEventArgs e)
		=> await ListClickGetLatest(sender, e);
	internal async Task ListClickGetLatest(object sender, RoutedEventArgs e)
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;
		statusToken = await statusToken.RenewTokenSourceAsync();

		var entryItem = OdooEntryList.SelectedItems;

		ArrayList entryIDs = [];

		foreach (EntryRow item in entryItem)
		{
			if (item.Id is not null)
			{
				entryIDs.Add(item.Id);
			}
		}

		await GetLatestInternal(entryIDs);
	}
	private async void List_Click_Commit(object sender, RoutedEventArgs e)
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;

		var entryItem = (OdooEntryList.SelectedItems as IList)?.Cast<EntryRow>().ToList() ?? [];
		HashSet<HackFile> hackFiles = [];
		hackFiles.AddAll(ProcessHacks(entryItem));
		await CommitInternal(hackFiles);
	}
	private static HashSet<HackFile> ProcessHacks(List<EntryRow>? entries)
	{
		HashSet<HackFile> hackFiles = [];
		if (entries is null) return hackFiles;
		
        foreach (var item in entries)
        {
        	string? file = item.FullName;
        	if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(item.Type)) continue;

        	if (OdooDefaultsConstants.DependentExt.Contains($".{item.Type.ToUpper()}"))
        	{
        		hackFiles.AddAll(HackFile.GetHackFileWithDependencies(item, true, out List<HackFile> hf) ? hf : []);
        	}
        	else
        	{
        		HackFile? hack = HackFile.GetFromPath(item.FullName)!; 
        		if (hack is {Exists: true}) hackFiles.Add(hack);
        	}
        }
        
		return hackFiles;
	}
	internal async void List_Click_Checkout(object sender, RoutedEventArgs e)
	{
		await ListClickGetLatest(sender, e);
		var entryItem = OdooEntryList.SelectedItems;

		ArrayList entryIDs = new(entryItem.Count);

		foreach (EntryRow item in entryItem)
		{
			if (item is not { Checkout: null }) continue;
			entryIDs.Add(item.Id);
		}

		if (entryIDs.Count < 1) return;

		await CheckoutInternal(entryIDs);
	}
	internal async void List_Click_UndoCheckout(object sender, RoutedEventArgs e)
	{
		var entryItem = OdooEntryList.SelectedItems;

		ArrayList entryIDs = new(entryItem.Count);

		foreach (EntryRow item in entryItem)
		{
			if (item is not { Checkout: null }) continue;
			entryIDs.Add(item.Id);
		}

		if (entryIDs.Count < 1) return;
		await UnCheckoutInternal(entryIDs);
	}
	private void List_Click_Open(object sender, RoutedEventArgs e)
	{
		// open local if lm, co
		// open remote if ro, dt
		foreach (EntryRow viewItem in OdooEntryList.SelectedItems)
		{
			string? path = viewItem.FullName;
			int? idStr = viewItem.Id;
			if (path is null) continue;
			if (idStr is null or 0)
			{
				OpenLocalFile(path);
				continue;
			}
			FileStatus status = viewItem.Status;
			switch (status)
			{
				case FileStatus.Ro:
				case FileStatus.Nv:
					{
						OpenRemoteFile(viewItem.Id ?? 0);
						continue;
					}

				case FileStatus.Lm:
				case FileStatus.Ok:
				case FileStatus.Co:
				case FileStatus.Ft:
				case FileStatus.If:
				case FileStatus.Cm:
					{
						OpenLocalFile(FileOperations.ConvertToWindowsPath(path, true));
						continue;
					}

				default:
					continue;
			}

		}
	}
	private void List_Click_OpenLatestRemote(object sender, RoutedEventArgs e)
	{
		StringBuilder errors = new();
		foreach (EntryRow viewItem in OdooEntryList.SelectedItems)
		{
			if (viewItem.Id is null or 0)
			{
				errors.AppendLine($"can't open local only file remotely {viewItem.Name}");
				continue;
			}
			string? path = viewItem.FullName;
			FileStatus status = viewItem.Status;

			switch (status)
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
						OpenRemoteFile(viewItem.Id ?? 0);
						continue;
					}

				default:
					{
						errors.AppendLine($"can't open local only file remotely {viewItem.Name}");
						continue;
					}
			}
		}
		if (errors.Length > 0) MessageBox.ShowAsync(errors.ToString());
	}
	private void List_Click_OpenLatestLocal(object sender, RoutedEventArgs e)
	{
		StringBuilder errors = new();
		foreach (EntryRow viewItem in OdooEntryList.SelectedItems)
		{
			string? path = viewItem.FullName;

			if (viewItem.Id is null or 0)
			{
				OpenLocalFile(path);
				continue;
			}

			FileStatus status = viewItem.Status;

			switch (status)
			{
				case FileStatus.Nv:
				case FileStatus.Lm:
				case FileStatus.Ok:
				case FileStatus.Co:
				case FileStatus.Ft:
				case FileStatus.If:
				case FileStatus.Cm:
					{
						OpenLocalFile(FileOperations.ConvertToWindowsPath(path, true));
						continue;
					}

				case FileStatus.Ro:
				default:
					{
						errors.AppendLine($"can't open remote only file locally {viewItem.Name}");
						continue;
					}
			}
		}
		if (errors.Length > 0) MessageBox.ShowAsync(errors.ToString());
	}
	private void List_Click_OpenDirectory(object sender, RoutedEventArgs e)
	{
		List<string?> openedDirectory = [];
		foreach (EntryRow item in OdooEntryList.SelectedItems)
		{
			string? path = item.FullName;

			try
			{
				// remote file path
				if (item.Id is not null and not 0)
				{
					path = FileOperations.ConvertToWindowsPath(path, true);
				}
				FileInfo file = new FileInfo(path);
				if (!file.Exists) continue;

				if(!openedDirectory.Any(s=> file.DirectoryName?.Equals(s) ?? true))
				{
					openedDirectory.Add(file.DirectoryName);
					FileOperations.OpenFolder(file.DirectoryName!);
				}
			}
			catch
			{
				continue;
			}
			//FileOperations.OpenFile(  );
		}
	}
	private void List_Click_Restore(object sender, RoutedEventArgs e)
	{

	}
	private async void List_Click_LocalDelete(object sender, RoutedEventArgs e)
	{
		string pathway = LastSelectedNodePath?.Length < 5 ? HackDefaults.Instance.PwaPathAbsolute : Path.Combine(HackDefaults.Instance.PwaPathAbsolute, LastSelectedNodePath?[5..]);
		DirectoryInfo directory = new(pathway);
		if (!directory.Exists) return;

		var sb = new StringBuilder();
		var files = new List<FileInfo>();

		OdooEntryList.SelectedItems.Cast<ListViewItem>().ToList().ForEach(item =>
		{
			string filepath = Path.Combine(pathway, (item.Content as EntryRow)?.Name ?? "");
			FileInfo file = new(filepath);
			if (file.Exists)
			{
				sb.AppendLine(file.FullName);
				files.Add(file);
			}
		});
		bool tooMany = files.Count > 10;
		string message = tooMany ? $"Are you sure you want to delete ({files.Count}) files?" : $"Are you sure you want to delete these files?\nfiles:\n{sb}";
		if (await MessageBox.ShowAsync(message,
				"Delete Directory",
				buttons: MessageBoxButtons.YesNoCancel,
				icon: MessageBoxIcon.Warning) == DialogResult.Yes)
		{
			files.ForEach(f => f.Delete());
		}
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private async void List_Click_LogicalDelete(object sender, RoutedEventArgs e)
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;

		var entryItem = OdooEntryList.SelectedItems;
		//var directory = HackDefaults.DefaultPath(lastSelectedNode.FullPath, true);

		ArrayList entryIDs = [];
		foreach (EntryRow item in entryItem)
		{
			if (item.Id is not null and not 0)
			{
				entryIDs.Add(item.Id);
			}
		}

		await LogicalDeleteInternal(entryIDs);
	}
	private async void List_Click_PermanentDelete(object sender, RoutedEventArgs e)
	{
#if DEBUG
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;

		var entryItem = OdooEntryList.SelectedItems;

		ArrayList entryIDs = new(entryItem.Count);

		foreach (EntryRow item in entryItem)
		{
			if (item.Id is not null and not 0)
			{
				entryIDs.Add(item.Id);
			}
		}

		HpEntry[] entries = HpEntry.GetRecordsByIds(entryIDs, excludedFields: ["type_id", "cat_id", "checkout_node"]);
		if (entries is null || entries.Length == 0)
		{
			MessageBox.ShowAsync("No entries to delete");
			return;
		}

		await AsyncHelper.AsyncRunner(() => Async_PermDelete(entries), "Permanently Delete Files");
#endif
	}
	//
	private async void AdditionalTools_Click_Refresh(object sender, RoutedEventArgs e)
	{
		OdooEntryImage.Source = _previewImage;
		await _treeHelper.RestartTree(OdooDirectoryTree);
	}
	private void AdditionalTools_Click_Search(object sender, RoutedEventArgs e)
	{
		WindowHelper.CreateWindowAndPage<SearchOdoo>(out var page, out var window);
		window.Title = "Search Files";
		page.SetHackInstance(this);
		page.StoreWindowInstance(window);
	}
	private void AdditionalTools_Click_ManageTypes(object sender, RoutedEventArgs e)
		=> WindowHelper.CreateWindowPage<OdooFileTypeManager>().Title = "Manage Types";
	//
	private async void History_Click_Download(object sender, DoubleTappedRoutedEventArgs e)
	{
		var version = GetVersionFromHistory();
		FileInfo file = new(Path.Combine(version.WinPathway, version.name));
		if (FileOperations.SameChecksum(file, version.checksum))
		{
			if (file.Exists)
			{
				var response = MessageBox.ShowAsync("File exists as a different version.\n" +
											   "Retry:\tDownload in the Temporary Folder\n" +
											   "Ignore:\tOverwrite the current version\n" +
											   "Abort:\tCancel download", "File Version Conflict", buttons: MessageBoxButtons.AbortRetryIgnore, icon: MessageBoxIcon.Warning);

				switch (await response)
				{
					case DialogResult.Ignore:
						version.DownloadFile(version.WinPathway);
						break;
					case DialogResult.Yes:
						version.DownloadFile(Path.GetTempPath());
						break;
				}
			}
		}
		else
		{
			version.DownloadFile(version.WinPathway);
		}
	}
	private void History_Click_TemporaryDownload(object sender, RoutedEventArgs e)
		=> DownloadHistory(true);
	private void History_Click_OverwriteDownload(object sender, RoutedEventArgs e)
		=> DownloadHistory(false);
	private void History_Click_Open(object sender, DoubleTappedRoutedEventArgs e)
	{

	}
	private void History_Click_OverwriteOpen(object sender, RoutedEventArgs e)
		=> DownloadOpen(false);
	private void History_Click_TemporaryOpen(object sender, RoutedEventArgs e)
		=> DownloadOpen(true);
	private void History_Click_OverwriteMove(object sender, RoutedEventArgs e)
		=> LocalMoveEntry(false);
	private void History_Click_TemporaryMove(object sender, RoutedEventArgs e)
		=> LocalMoveEntry(true);
	private async void History_DoubleClick(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (OdooHistory.SelectedItems?[0] is not HistoryRow item) return;
		if (item.Version is 0) return;

		HpVersion versionModel = (await HpVersion.GetRecordsByIdsAsync([item.Version])).First();
		HpEntry entryModel = (await HpEntry.GetRecordsByIdsAsync([versionModel.entry_id])).First();
		ArrayList versions = await GetVersionList(item.Version);
		HashSet<int> vIds = versions.ToHashSet<int>();
		vIds.Add(versionModel.id ?? 0);
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
		if (await response != DialogResult.Yes) return;
		HpVersion[] downVersions = await HpVersion.GetRecordsByIdsAsync(versions);
		if (downVersions.DownloadAll(out List<HpVersion> failed)) return;

		ArrayList fIDs = failed.GetIDs();
		MessageBox.ShowAsync($"failed to download version ids: {string.Join(", ", fIDs.ToArray<int>())}");
	}
	private async void OdooParents_DoubleClick(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (OdooParents.SelectedItems is not [ParentRow item]) return;

		string? pwaPath = item.BasePath;
		string? fileName = item.Name;
		await FindSearchSelectionAsync(pwaPath, fileName);
	}
	private async void OdooChildren_DoubleClick(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (OdooChildren.SelectedItems is not [ChildrenRow item]) return;
		
		string? pwaPath = item.BasePath;
		string fileName = item.Name;
		await FindSearchSelectionAsync(pwaPath, fileName);
	}


	#endregion

	#region Form Helper Functions
	private void OpenLocalFile(string path)
	{
		FileOperations.OpenFile(path);
	}
	private void OpenRemoteFile(int entryId)
	{
		const string latestVersion = "latest_version_id";
		HpVersion versionModel = HpEntry.GetRelatedRecordByIds<HpVersion>([entryId], latestVersion, excludedFields: ["preview_image"]).First();
		if (versionModel == null)
			return;

		// download version data and place into temporary folder
		versionModel.DownloadFile(Path.GetTempPath());
		FileOperations.OpenFile(Path.Combine(versionModel.WinPathway, versionModel.name));
	}
	private async void PreviewImageSelection<T>(T? item)
	{
		switch (item)
		{
			case null: break;
			case EntryRow er: if (er.Id is not null) await _gridHelper.PreviewImage(er.Id); break;
			case ChildrenRow cr: await _gridHelper.PreviewImage(cr.Version); break;
			case ParentRow pr: await _gridHelper.PreviewImage(pr.Version); break;
			default: break;
		}
	}
	public async Task FindSearchSelectionAsync(string pwaPath, string fileName, string delimiter = "\\")
	{
		// first select the treeview node
		// then select the listview item
		string[] paths = pwaPath.Split([delimiter], StringSplitOptions.None);

		var nodes = OdooDirectoryTree.RootNodes;
		TreeViewNode node = nodes[0];

		try
		{
			for (int i = 0; i < paths.Length; i++)
			{
				nodes = node.Children;

				bool wasFound = false;
				foreach (TreeViewNode n in nodes)
				{
					if (n.LinkedData.Name != paths[i]) continue;
					wasFound = true;
					node = n;
					break;
				}
				if (!wasFound) throw new ArgumentException();
			}
			foreach (var treeViewNode in OdooDirectoryTree.RootNodes)
			{
				treeViewNode.IsExpanded = false;
			}
			LastSelectedNode = node;
			LastSelectedNode.LinkedData.EnsureVisible(OdooDirectoryTree);
			OdooDirectoryTree.SelectedNode = LastSelectedNode;

			while (!IsListLoaded)
			{
				await Task.Delay(100);
			}
			EntryRow? entryItem = OEntries.FirstOrDefault(entryItem => entryItem.Name == fileName);
			if (entryItem == null) throw new ArgumentException("entry doesn't exist", nameof(fileName));

			OdooEntryList.SelectedItem = entryItem;
			OdooEntryList.Focus(FocusState.Programmatic);
			OdooEntryList.ScrollIntoView(entryItem, null);
		}
		catch
		{
			Debug.WriteLine("Unable to find search selection");
		}
	}
	private void DownloadOpen(bool toTemp = false)
	{
		var version = DownloadHistory(toTemp);
		if (version == null)
			return;

		OpenLocalFile(Path.Combine(version.WinPathway, version.name));
	}
	private HpVersion? DownloadHistory(bool toTemp = false)
	{
		var version = GetVersionFromHistory();
		if (version is null) return null;

		if (toTemp)
		{
			string path = version.HashedValues.TryGetValue<string, ArrayList>("dir_id", out var arr)
				&& arr?[1] is string str ? string.Join("\\", str.Split(" / ")[1..])
				: "";
			
			string tempPath = Path.Combine(StorageBox.TemporaryPath, path);
			version.DownloadFile(tempPath);
			if (version.FileTypeExt != SolidWorks.Interop.swdocumentmgr.SwDmDocumentType.swDmDocumentUnknown)
			{
#if Debug
				HackDefaults.DocMgr.GetDependencies(path);
				HackDefaults.DocMgr.ReplaceDependencies(version.WinPathway, tempPath, version.FileTypeExt);
				HackDefaults.DocMgr.GetDependencies(path);
#endif
			}
		}
		else
			version.DownloadFile(version.WinPathway);

		return version;
	}
	private async void LocalMoveEntry(bool toTemp = false)
	{
		var version = GetVersionFromHistory();
		if (version == null) return;

		string tempFilePath = Path.Combine(StorageBox.TemporaryPath ?? "", version.name ?? "");
		string mainFilePath = Path.Combine(version.WinPathway ?? "", version.name ?? "");

		FileInfo fileFrom = new FileInfo(!toTemp ? tempFilePath : mainFilePath);
		FileInfo fileTo = new FileInfo(toTemp ? tempFilePath : mainFilePath);

		string message = "";
		string caption = "";
		string boolReplace = toTemp ? "temporary" : "current";

		var icon = MessageBoxIcon.None;
		// if the file doesn't exist in temporary folder, download it an place it in current path.
		if (fileFrom.Exists)
		{
			if (fileTo.Exists)
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
			if (DialogResult.Yes == await MessageBox.ShowAsync(message, caption, buttons: MessageBoxButtons.YesNoCancel, icon: icon))
			{
				fileFrom.MoveFile(fileTo.DirectoryName);
			}
		}
		else
		{
			if (fileTo.Exists)
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
			if (DialogResult.Yes == await MessageBox.ShowAsync(message, caption, buttons: MessageBoxButtons.YesNoCancel, icon: icon))
			{
				version.DownloadFile(fileTo.DirectoryName);
			}
		}
		_treeHelper.RestartEntries(OdooDirectoryTree, OdooEntryList);
	}
	private HpVersion? GetVersionFromHistory()
	{
		if (OdooHistory.SelectedItems.Count < 1)
			return null;

		HistoryRow? item = OdooHistory.SelectedItems[0] as HistoryRow;

		if (item?.Version is null or 0) return null;

		var version = HpVersion.GetRecordById(item!.Version, HpVersion.UsualExcludedFields);
		version.WinPathway = Path.Combine(HackDefaults.Instance.PwaPathAbsolute, version.WinPathway);
		return version;
	}
	private void EndNodePaths(TreeViewNode node, in List<string> paths)
	{
		if (node.Children.Count == 0)
		{
			paths.Add(node.LinkedData.FullPath ?? "");
		}
		else
		{
			foreach (TreeViewNode cNode in node.Children)
			{
				EndNodePaths(cNode, paths);
			}
		}
	}
	internal static async Task<ArrayList> GetAllEntriesAndDependenciesList(int[] entryIds, bool update = false)
	{
		ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_entries", [entryIds.ToArrayList()], 1000000);
		return arr;
	}
	private async Task<ArrayList> GetVersionList(params int[] versionIds)
	{
		ArrayList arr = await OClient.CommandAsync<ArrayList>(HpVersion.GetHpModel(), "get_recursive_dependency_versions", [versionIds.ToArrayList()], 1000000);
		return arr;
	}
	// private PointF ScalePoint(PointF p1, PointF p2, double desiredDistance)
	// {
	// 	PointF p3 = new(
	// 		p2.X - p1.X,
	// 		p2.Y - p1.Y
	// 	);
	//
	// 	double currentDist = Math.Sqrt(p3.X * p3.X + p3.Y * p3.Y);
	// 	double scaleFactor = desiredDistance / currentDist;
	// 	p3.X = p2.X - Convert.ToSingle(scaleFactor) * p3.X;
	// 	p3.Y = p2.Y - Convert.ToSingle(scaleFactor) * p3.Y;
	//
	// 	return p3;
	// }
	private async Task<bool> PermanentDeleteVersionProperty(ArrayList ids)
	{
		if (ids is null || ids.Count < 1) return false;

		HpVersionProperty[] vProps = null;
		bool deletedVersionProps = false;

		vProps = HpVersionProperty.GetRecordsBySearch([new ArrayList() { "version_id", "in", ids }]);
		if (vProps is not null
			&& vProps.Count() > 0)
		{
			ArrayList newIds = vProps.GetIDs();
			Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Deleting version properties...");
			deletedVersionProps = await OClient.DeleteAsync(HpVersionProperty.GetHpModel(), [newIds], 100000);
			if (deletedVersionProps)
			{
				Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Deleted version properties: {string.Join(", ", newIds.ToArray())}");
			}
			else
			{
				Dialog?.AddStatusLine(StatusMessage.ERROR, $"Unable to delete version properties");
			}
		}
		else
		{
			deletedVersionProps = true;
			Dialog?.AddStatusLine(StatusMessage.SKIP, $"No version properties to delete");
		}
#if DEBUG
		Debug.WriteLine($"version properties deleted = {deletedVersionProps}");
#endif
		return deletedVersionProps;
	}
	private async Task<bool> PermanentDeletedVersionRelationships(ArrayList ids)
	{
		if (ids is null || ids.Count < 1) return false;

		HpVersionRelationship[] vRelationsParent = null;
		HpVersionRelationship[] vRelationsChild = null;

		bool deletedVersionRelParent = false;
		bool deletedVersionRelChild = false;

		vRelationsParent = HpVersionRelationship.GetRecordsBySearch([new ArrayList() { "parent_id", "in", ids }]);
		vRelationsChild = HpVersionRelationship.GetRecordsBySearch([new ArrayList() { "child_id", "in", ids }]);

		if (vRelationsParent is not null
			&& vRelationsParent.Count() > 0)
		{
			ArrayList newIds = vRelationsParent.GetIDs();
			Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Deleting parent version relationships...");
			deletedVersionRelParent = OClient.Delete(HpVersionRelationship.GetHpModel(), [newIds], 100000);
			if (deletedVersionRelParent)
			{
				Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Deleted parent version relationships: {string.Join(", ", newIds.ToArray())}");
			}
			else
			{
				Dialog?.AddStatusLine(StatusMessage.ERROR, $"Unable to delete parent version relationships");
			}
		}
		else
		{
			deletedVersionRelParent = true;
			Dialog?.AddStatusLine(StatusMessage.SKIP, $"No version relationship parents to delete");
		}

		if (vRelationsChild is not null
			&& vRelationsChild.Any())
		{
			ArrayList newIds = vRelationsChild.GetIDs();
			Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Deleting child version relationships...");
			deletedVersionRelChild = await OClient.DeleteAsync(HpVersionRelationship.GetHpModel(), [newIds], 100000);
			if (deletedVersionRelChild)
			{
				Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Deleted child version relationships: {string.Join(", ", newIds.ToArray())}");
			}
			else
			{
				Dialog?.AddStatusLine(StatusMessage.ERROR, $"Unable to delete child version relationships");
			}
		}
		else
		{
			deletedVersionRelChild = true;
			Dialog?.AddStatusLine(StatusMessage.SKIP, $"No version relationship children to delete");
		}

#if DEBUG
		Debug.WriteLine($"version parents deleted = {deletedVersionRelParent}");
		Debug.WriteLine($"version child deleted = {deletedVersionRelChild}");
#endif

		return deletedVersionRelChild && deletedVersionRelParent;
	}
	private async Task<bool> PermanentDeleteEntry(ArrayList ids)
	{
		if (ids is null || ids.Count < 1) return false;

		bool deletedVersions = await PermanentDeleteVersions(ids);
		Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Deleting entries...");
		bool deletedEntries = deletedVersions && OClient.Delete(HpEntry.GetHpModel(), [ids]);
		if (deletedEntries)
		{
			Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Deleted entries");
		}
		else
		{
			Dialog?.AddStatusLine(StatusMessage.ERROR, $"Unable to delete entries");
		}
#if DEBUG
		Debug.WriteLine($"Entries deleted = {deletedEntries}");
#endif
		return deletedVersions && deletedEntries;
	}
	private async Task<bool> PermanentDeleteVersions(ArrayList ids)
	{
		if (ids is null || ids.Count < 1) return false;

		HpVersion[] versions = HpEntry.GetRelatedRecordByIds<HpVersion>(ids, "version_ids", includedFields: ["ID"]);
		IrAttachment[] irAttachments = null;

		ArrayList vIds = versions?.Select(v => v.id).ToArrayList() ?? [];

		bool deletedIrAttachments = false;
		bool deletedVersions = false;
		bool deletedVersionsProps = false;
		bool deletedVersionsRel = false;

		if (vIds.Count > 0)
		{
			deletedVersionsProps = await PermanentDeleteVersionProperty(vIds);
			deletedVersionsRel = await PermanentDeletedVersionRelationships(vIds);
			irAttachments = IrAttachment.GetRecordsBySearch(
			[
				new ArrayList() { "res_id", "in", vIds },
				new ArrayList() { "res_model", "=", HpVersion.GetHpModel()},
				new ArrayList() { "res_field", "=", "file_contents"},
			]);
		}
		Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Deleting IR Attachments...");
		deletedIrAttachments = deletedVersionsProps
							   && deletedVersionsRel
							   && (irAttachments is null
								   || !irAttachments.Any()
								   || await OClient.DeleteAsync(IrAttachment.GetHpModel(), [irAttachments.GetIDs()], 100000));

		if (deletedIrAttachments)
		{
			Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Deleted IR Attachments");
		}
		else
		{
			Dialog?.AddStatusLine(StatusMessage.INFO, $"unable to delete IR Attachments");
		}
		Dialog?.AddStatusLine(StatusMessage.PROCESSING, $"Deleting versions...");
		deletedVersions = deletedIrAttachments
						  && (vIds.Count <= 0
							   || await OClient.DeleteAsync(HpVersion.GetHpModel(), [vIds], 100000));

		if (deletedVersions)
		{
			Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Deleted versions");
		}
		else
		{
			Dialog?.AddStatusLine(StatusMessage.ERROR, $"Unable to delete versions");
		}

#if DEBUG
		Debug.WriteLine($"ir attachments deleted = {deletedIrAttachments}");
		Debug.WriteLine($"versions deleted = {deletedVersions}");
#endif
		return deletedIrAttachments && deletedVersions;
	}
	//
	internal async Task GetLatestInternal(ArrayList entryIDs)
	{	
		Dialog?.AddStatusLine(StatusMessage.INFO, "Finding Entry Dependencies...");
		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: ["latest_version_id"]);
		//HpEntry[] entries = HpEntry.GetRecordsByIDS(entryIDs, includedFields: ["latest_version_id"]);

		if (entries is null || entries.Length < 1) return;
		ArrayList newIds = await GetAllEntriesAndDependenciesList([.. entries.Select(entry => entry.latest_version_id?.id ?? 0)]);

		newIds.AddRange(entryIDs);
		newIds = newIds.ToHashSet<int>().ToArrayList();
		
		(ArrayList, CancellationToken) arguments = (newIds, statusToken.Token);

        statusToken.Token.Register(CancelledOperation);
        await AsyncHelper.AsyncRunner(() => Async_GetLatest(arguments), "Get Latest", statusToken);
	}
    internal static void CancelledOperation()
    {
        HackFileManager.Dialog?.IsInProcess = false;
        MessageBox.ShowAsync("Cancelled Operation");
    }
    internal async Task CommitInternal(IEnumerable<HackFile> hackFiles)
		=> await AsyncHelper.AsyncRunner(() => Async_Commit([.. hackFiles]), "Commit Files");
	
	internal async Task CheckoutInternal(ArrayList entryIDs)
	{
		HpEntry[]? entriesTemp = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: ["latest_version_id"]);

		ArrayList newIds = await GetAllEntriesAndDependenciesList([.. entriesTemp.Select(e => e.latest_version_id?.id ?? 0)]);

		newIds.AddRange(entryIDs);
		newIds = newIds.ToHashSet<int>().ToArrayList();

		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(newIds, excludedFields: ["type_id", "cat_id"]);

		if (entries is null || entries.Length < 1) return;
		if (Dialog is null) 
		{
			WindowHelper.CreateWindowAndPage<StatusDialog>(out var newDialog, out _);
			Dialog = newDialog;
		}
		
		await AsyncHelper.AsyncRunner(() => Async_CheckOut(entries), "Checkout Files");
	}
	internal async Task UnCheckoutInternal(ArrayList entryIDs)
	{
		if (entryIDs is null or { Count: < 1 }) return;

		var entriesTemp = await HpEntry.GetRecordsByIdsAsync(entryIDs, includedFields: ["latest_version_id"]);
		ArrayList newIds = await GetAllEntriesAndDependenciesList([.. entriesTemp?.Select(e => e.latest_version_id?.id ?? 0) ?? []]);

		newIds.AddRange(entryIDs);
		newIds = newIds.ToHashSet<int>().ToArrayList();

		var entries = await HpEntry.GetRecordsByIdsAsync(newIds, [
			new ArrayList() 
			{ 
				"checkout_user", 
				OperatorConverter.OperatorToString(Operators.Equal), 
				OdooDefaults.Instance.OdooId, 
			}
		], excludedFields: ["type_id", "cat_id"]);

		if (entries is null || entries.Length < 1)
			return;
		
		if (Dialog is null) 
		{
			WindowHelper.CreateWindowAndPage<StatusDialog>(out var newDialog, out _);
			Dialog = newDialog;
		}

		// filter out entries that are already checked out
		entries = [.. FilterUnCheckoutEntries(entries)];

		await AsyncHelper.AsyncRunner(() => Async_UnCheckOut(entries), "UnCheckout Files");
	}
	internal async Task LogicalDeleteInternal(ArrayList entryIDs)
	{
		HpEntry[]? entriesTemp = HpEntry.GetRecordsByIds(entryIDs, includedFields: ["latest_version_id"]);

		ArrayList newIds = await GetAllEntriesAndDependenciesList([.. entriesTemp.Select(e => e.latest_version_id?.id ?? 0)]);

		newIds.AddRange(entryIDs);
		newIds = newIds.ToHashSet<int>().ToArrayList();

		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(newIds, excludedFields: ["type_id", "cat_id", "checkout_node"]);

		await AsyncHelper.AsyncRunner(() => Async_LogicalDelete(entries), "Logically Delete Files");
	}
	internal async Task UnDeleteInternal(bool withSubdirectories = false)
	{
		WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
		HackFileManager.Dialog = Dialog;

		HpEntry[]? entries = await HpEntry.GetRecordsByIdsAsync(null, searchFilters: [new ArrayList() { "deleted", "=", true }, new ArrayList() { "dir_id", "=", LastSelectedNode?.LinkedData.DirectoryId ?? 0 }], excludedFields: ["type_id", "cat_id", "checkout_node"]);
		await AsyncHelper.AsyncRunner(() => Async_LogicalUnDelete(entries), "Logically UnDelete Files");
	}

#endregion

	private void TreeViewItem_Loaded(object sender, RoutedEventArgs e)
	{
		var tvi = sender as TreeViewItem;
		var data = tvi?.DataContext as TreeData;
	}

	private void TreeViewItem_Unloaded(object sender, RoutedEventArgs e)
	{
		var tvi = sender as TreeViewItem;
		var data = tvi?.DataContext;
		ItemToContainerMap.Remove(data);
	}

	private void OdooEntryDataGrid_LoadingRowGroup(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowGroupHeaderEventArgs e)
	{

	}

	private void OdooEntryDataGrid_Sorting(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridColumnEventArgs e)
	{

	}

	
}