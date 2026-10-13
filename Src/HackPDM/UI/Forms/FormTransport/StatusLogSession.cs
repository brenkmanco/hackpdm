using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;

using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;

namespace HackPDM.UI.Forms.FormTransport;

public class StatusLogSession
{
	public static Dictionary<Guid, WeakReference<StatusLogSession>> ActiveSessions { get; } = [];
	public Guid SessionId { get; } = Guid.NewGuid();

	public StatusLogSession()
	{
		ActiveSessions.TryAdd( SessionId, new( this ) );
	}
	~StatusLogSession()
	{
		ActiveSessions.Remove( SessionId );
	}
	public void Logger( StatusMessage status, string message ) => Log( (status, message) );
	public void Log( BasicStatusMessage message )
	{
		// Pass SessionId as the token parameter to route only to matching subscribers
		WeakReferenceMessenger.Default.Send( message: message, token: SessionId );
	}
}
