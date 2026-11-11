using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Types;

using EntryRow = HackPDM.UI.Types.EntryRow;
using TreeData = HackPDM.UI.Types.TreeData;

namespace HackPDM.ViewModels;

public partial class HackFileManagerViewModel : ObservableObject
{
	public bool IsLoaded { get; set; } = false;
	public bool IsTreeLoaded { get; set; } = false;
	public bool IsListLoaded { get; set; } = false;
	public string? LastSelectedNodePath { get; set; }
	public bool IsActive { get; set; } = false;
	public bool IsFiltered { get; set; } = true;
	public bool HackLoaded { get; set; }

	public int SkipCounter { get; set; }
	public long Downloaded { get; set; }
	public long SessionDownloaded { get; set; }
	public int TotalProcessed { get; set; }
	public int ProcessCounter { get; set; }
	public int MaxCount { get; set; }
	public int EntryPollingMs { get; set; } = 5000;

	public string EntriesLabel { get; set; } = "Entries: None";
	public string EntriesLabelLocal { get; set; } = "LO: None";
	public string EntriesLabelRemote { get; set; } = "RO: None";

	public ObservableCollection<TreeData>? LastSelectedNodePaths { get; set; } = [];
	public DynamicGroupCollection<EntryRow>? GroupedEntries { get; set; } = new( [
		new("abc", [
			new() {
				Id=123,
				Name="Test Name 1",
				Type="abc",
				Status=FileStatus.Lo,
			},
			new() {
				Id=124,
				Name="Test Name 2",
				Type="abc",
				Status=FileStatus.Lo,
			},
			new() {
				Id=125,
				Name="Test Name 3",
				Type="abc",
				Status=FileStatus.Cm,
			},
		]),
		new("cba", [
			new() {
				Id=126,
				Name="Test Name 4",
				Type="cba",
				Status=FileStatus.Lo,
			},
			new() {
				Id=127,
				Name="Test Name 5",
				Type="cba",
				Status=FileStatus.Ro,
			}
		]),
		new("def", [
			new() {
				Id=128,
				Name="Test Name 6",
				Type="def",
				Status=FileStatus.Ok,
			},
		]),
		new("zzz", []),
	], e => e.Type ?? "" );

	public ObservableCollection<HistoryRow> OHistories { get; internal set; } = [];
	public ObservableCollection<ParentRow> OParents { get; internal set; } = [];
	public ObservableCollection<ChildrenRow> OChildren { get; internal set; } = [];
	public ObservableCollection<PropertiesRow> OProperties { get; internal set; } = [];
	public ObservableCollection<VersionRow> OVersions { get; internal set; } = [];
	public ObservableCollection<TreeData> ONodes { get; internal set; } = [];

	public ConcurrentQueue<(StatusMessage action, string description)> QueueAsyncStatus = new();
	
	internal Task? _entryListChange;
	internal Task? _treeItemChange;
	
	internal CancellationTokenSource? _cSource = new();
	internal CancellationTokenSource? _cTreeSource = new();
	public CancellationTokenSource? statusToken = new();
	
	public bool IsClosing { get; set; }
}
