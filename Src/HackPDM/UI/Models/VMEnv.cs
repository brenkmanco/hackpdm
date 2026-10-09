using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

using HackPDM.Abstractions.Structures;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.FormTransport;
using HackPDM.UI.Types;

using EntryRow = HackPDM.UI.Types.EntryRow;
using TreeData = HackPDM.UI.Types.TreeData;

namespace HackPDM.UI.Models;

public class VMEnv
{
	public HFM_VM HFM { get; set; } = new();
	public PM_VM PM { get; set; } = new();
	public SearchOdoo_VM SearchOdoo { get; set; } = new();
	public (StatusDialog_VM StatusDialog, StatusLogSession Session) Logger { get; set; } = StatusDialog_VM.CreateSessionPair();
	public OdooSettings_VM OdooSettings { get; set; } = new();
	public HackSettings_VM HackSettings { get; set; } = new();
	public AppSettings_VM AppSettings { get; set; } = new();
	public Home_VM Home { get; set; } = new();
	public NotLoggedIn_VM NotLoggedIn { get; set; } = new();
}

public class HFM_VM 
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

public class PM_VM
{
	public ObservableCollection<BasicStatusMessage> OStatus { get; } = [];
	public bool IsLoggedIn { get; set; } = false;
	public bool IsLoggingIn { get; set; } = false;
}

public class SearchOdoo_VM
{
	public ObservableCollection<SearchRow> SearchResultsList { get; } = [];
	public ObservableCollection<SearchPropertiesRow> PropertiesActive { get; } = [];
	public ObservableCollection<OperatorsRow> OperatorList { get; } = [];
	public ObservableCollection<SearchPropertiesRow> PropertiesSearchable { get; } = [];

	public string FileName { get; set; } = string.Empty;
	public bool CheckedOutMe { get; set; } = false;
	public bool LocalOnly { get; set; } = false;
	public bool DeletedRemotely { get; set; } = false;
	public string MaxResults { get; set; } = "100";
	public string PropertyValue { get; set; } = string.Empty;
}

public partial class StatusDialog_VM : ObservableObject, IRecipient<BasicStatusMessage>
{
	public Guid SessionId { get; private set; }

	public ConcurrentQueue<BasicStatusMessage> MessageQueue { get; } = new();
	public ObservableRingBuffer<BasicStatusMessage> OStatus { get; } = new( HistoryLength );
	public ObservableRingBuffer<BasicStatusMessage> OInfo { get; } = new( HistoryLength );
	public ObservableRingBuffer<BasicStatusMessage> OError { get; } = new( HistoryLength );
	public int ErrorCount { get; set; } = 0;
	public bool Canceled { get; set; } = false;
	public bool HasLoaded { get; set; } = false;
	public bool IsInProcess { get; set; } = false;

	public int SkipCounter { get; internal set; }
	internal long Downloaded { get; set; }
	internal static long SessionDownloaded { get; set; }
	internal int TotalProcessed { get; set; }
	internal int ProcessCounter { get; set; }
	internal int MaxCount { get; set; }

	public bool? SkipText {
		get;
		set {
			field = value;
			HackDefaults.Instance?.SettingsProvider?.Set( "SkipText", field );
		}
	} = HackDefaults.Instance?.SettingsProvider?.Get( "SkipText", false ) ?? false;
	public static int HistoryLength {
		get;
		set {
			field = value;
			HackDefaults.Instance?.SettingsProvider?.Set( "HistoryLength", field );
		}
	} = HackDefaults.Instance?.SettingsProvider?.Get( "HistoryLength", 10000 ) ?? 10000;
	public bool DoubleBuff { get; set; } = true;


	public void Register(Guid sessionId)
	{
		SessionId = sessionId;
		
		WeakReferenceMessenger.Default.Register(
			this,
			SessionId);
	}

	public void Receive( BasicStatusMessage message )
	{
		// Automatically filtered by token — messages from other tasks will never hit this method
		//string formattedEntry = $"[{message.Timestamp:HH:mm:ss}] {message.Message}";

		AddLog( message );
	}
	public ObservableRingBuffer<BasicStatusMessage> GetCollection( StatusMessage action )
	{
		return action switch {
			StatusMessage.PROCESSING => OStatus,
			StatusMessage.SUCCESS => OStatus,
			StatusMessage.SKIP => OInfo,
			StatusMessage.FOUND => OInfo,
			StatusMessage.INFO => OInfo,
			StatusMessage.OTHER => OInfo,
			StatusMessage.WARNING => OError,
			StatusMessage.ERROR => OError,
			_ => OInfo,
		};
	}
	public async Task AddStatusLine( BasicStatusMessage message )
	{
		
	}
	public void AddLog( BasicStatusMessage message )
	{
		//MessageQueue.Enqueue( message );
		GetCollection( message.Status).Add( message );
	}
	public async Task AddStatusLines( BasicStatusMessage[] messages )
	{
		foreach (var message in messages ) {
			AddLog( message );
		}
	}
	public async Task SetProgressBar( int value, int max )
	{
		ProcessCounter = value;
		TotalProcessed = ProcessCounter + SkipCounter;
		MaxCount = max;
	}
	public async Task SetDownloaded( long downloadBytes )
	{
		Downloaded = downloadBytes;
	}
	public async Task SetTotalDownloaded( long sessionDownloadBytes )
	{
		SessionDownloaded = sessionDownloadBytes;
	}
	internal StatusLogSession CreateSession()
	{
		var session = new StatusLogSession();
		Register( session.SessionId );
		return session;
	}
	internal static (StatusDialog_VM, StatusLogSession) CreateSessionPair()
	{
		var vm = new StatusDialog_VM();
		var session = vm.CreateSession();
		return (vm, session);
	}
}

public class HackSettings_VM
{
	public string PwaPath { get; set; } = string.Empty;
	public string TempFolderPath { get; set; } = string.Empty;
}

public class OdooSettings_VM
{
	public string OdooAddress { get; set; } = string.Empty;
	public string OdooPort { get; set; } = string.Empty;
	public string OdooDb { get; set; } = string.Empty;
	public string OdooSwKey { get; set; } = string.Empty;
	public string OdooAreaFactor { get; set; } = "1";
	public string OdooUser { get; set; } = string.Empty;
	public string OdooPass { get; set; } = string.Empty;
	public string OdooUrl { get; set; } = string.Empty;
}

public class AppSettings_VM
{
	public Windows.UI.Color SelectedColor { get; set; } = Windows.UI.Color.FromArgb(255, 0, 120, 215);
}

public class Home_VM
{
	public string SelectedMenuItemTag { get; set; } = "ProfileManager";
}

public class NotLoggedIn_VM
{
	public string Message { get; set; } = "You are not logged in. Please log in through the Profile Manager.";
}
