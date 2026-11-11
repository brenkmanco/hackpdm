using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using HackPDM.Abstractions.Structures;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;

namespace HackPDM.Mvvm.ViewModels.Settings;

public partial class StatusDialogViewModel : ObservableObject, IRecipient<BasicStatusMessage>
{
	public Guid SessionId { get; private set; }

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
		UiDispatcher.Post( () => GetCollection( message.Status ).Add( message ) );
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
