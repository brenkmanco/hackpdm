using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using HackPDM.Abstractions.Structures;
using HackPDM.Core.Hack;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.FormTransport;

namespace HackPDM.ViewModels;

public partial class StatusDialogViewModel : ObservableObject, IRecipient<BasicStatusMessage>
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
		
	public void AddLog( BasicStatusMessage message )
	{
		SafeHelper.SafeInvokerAsync( () 
			=> GetCollection( message.Status ).Add( message ) );
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

	public static async Task SetTotalDownloaded( long sessionDownloadBytes )
	{
		SessionDownloaded = sessionDownloadBytes;
	}

	internal StatusLogSession CreateSession()
	{
		var session = new StatusLogSession();
		Register( session.SessionId );
		return session;
	}

	internal static (StatusDialogViewModel, StatusLogSession) CreateSessionPair()
	{
		var vm = new StatusDialogViewModel();
		var session = vm.CreateSession();
		return (vm, session);
	}
}
