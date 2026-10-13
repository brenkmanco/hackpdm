using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;

using HackPDM.Core.General;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;

namespace HackPDM.UI.Forms.FormTransport;

public class StatusLogSession
{
	public static Dictionary<Guid, WeakReference<StatusLogSession>> ActiveSessions { get; } = [];
	public static KeyValuePair<Guid, WeakReference<StatusLogSession>> Current { get => ActiveSessions.FirstOrDefault(); }
	public Guid SessionId { get; } = Guid.NewGuid();

	public StatusLogSession()
	{
		ActiveSessions.TryAdd( SessionId, new( this ) );
	}
	~StatusLogSession()
	{
		ActiveSessions.Remove( SessionId );
	}
	public void Log( StatusMessage status, string message ) => Log( new() { Status = status, Message = message } );
	public void Log( BasicStatusMessage message )
	{
		// Pass SessionId as the token parameter to route only to matching subscribers
		WeakReferenceMessenger.Default.Send( message: message, token: SessionId );
	}
}
