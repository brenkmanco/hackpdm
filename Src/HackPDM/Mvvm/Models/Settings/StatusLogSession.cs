using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;

using HackPDM.Core.General;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.Models.Settings;

/// <summary>
/// One status log stream: lines logged here are sent through <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default"/>
/// with this session's <see cref="SessionId"/> as the token, so only the window subscribed to it receives them.
/// </summary>
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
	/// <summary>Sends a log line to the subscribers of this session.</summary>
	/// <param name="status">The kind of line (info, error, warning...).</param>
	/// <param name="message">The text of the line.</param>
	public void Log( StatusMessage status, string message ) => Log( new() { Status = status, Message = message } );
	/// <summary>Sends a log line to the subscribers of this session.</summary>
	/// <param name="message">The line to send.</param>
	/// <remarks>Can be called from any thread; receivers are invoked synchronously on the calling thread.</remarks>
	public void Log( BasicStatusMessage message )
	{
		// Pass SessionId as the token parameter to route only to matching subscribers
		WeakReferenceMessenger.Default.Send( message: message, token: SessionId );
	}
}
