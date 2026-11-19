using System;
using System.Collections.Generic;
using System.Threading;

using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.Models.Settings;

/// <summary>
/// Whether this session is logged in to Odoo and the run-wide status log shown in Profile Manager,
/// kept for the whole run (one instance, held by the composition root) while pages and view models come and go.
/// </summary>
public sealed class LoginSession
{
	private const int MaxMessages = 200;
	private readonly Lock _gate = new();
	private readonly List<BasicStatusMessage> _messages = [];

	public bool IsLoggedIn { get; set; }

	/// <summary>Non-null when the server denied login due to a client version mismatch; locks all pages until updated.</summary>
	public string? VersionMismatchMessage { get; set; }

	/// <summary>True when access is currently blocked by a server/client version mismatch.</summary>
	public bool IsVersionMismatch => !string.IsNullOrEmpty( VersionMismatchMessage );

	/// <summary>Run-wide status messages (login, connection, configurations, settings); shown again when Profile Manager is reopened.</summary>
	public IReadOnlyList<BasicStatusMessage> LastErrors
	{
		get
		{
			lock( _gate )
				return [ .. _messages ];
		}
		set
		{
			lock( _gate )
			{
				_messages.Clear();
				if( value is { Count: > 0 } )
				{
					int start = Math.Max( 0, value.Count - MaxMessages );
					for( int i = start; i < value.Count; i++ )
						_messages.Add( value[ i ] );
				}
			}
		}
	}

	/// <summary>Tracks the last configuration/settings snapshot logged so revisiting Profile Manager avoids duplicate entries.</summary>
	public string? LastReadinessFingerprint { get; set; }

	/// <summary>The server, database and user of the last successful login.</summary>
	public string? Account { get; private set; }

	/// <summary>Raised whenever a status message is added from login, connection, configuration, or settings flows.</summary>
	public event Action<BasicStatusMessage>? MessageLogged;

	/// <summary>Raised when <see cref="ClearMessages"/> clears all session status messages.</summary>
	public event Action? MessagesCleared;

	/// <summary>Adds a typed status message to the session log and notifies subscribers.</summary>
	public BasicStatusMessage AddStatus( StatusMessage status, string category, string message )
	{
		var entry = new BasicStatusMessage
		{
			Status = status,
			Category = category,
			Message = message,
			Timestamp = DateTime.Now,
		};
		lock( _gate )
		{
			if( _messages.Count >= MaxMessages )
				_messages.RemoveAt( 0 );
			_messages.Add( entry );
		}
		MessageLogged?.Invoke( entry );
		return entry;
	}

	public BasicStatusMessage LogInfo( string category, string message ) => AddStatus( StatusMessage.INFO, category, message );
	public BasicStatusMessage LogSuccess( string category, string message ) => AddStatus( StatusMessage.SUCCESS, category, message );
	public BasicStatusMessage LogWarning( string category, string message ) => AddStatus( StatusMessage.WARNING, category, message );
	public BasicStatusMessage LogError( string category, string message ) => AddStatus( StatusMessage.ERROR, category, message );

	/// <summary>Clears all accumulated status messages and notifies subscribers.</summary>
	public void ClearMessages()
	{
		lock( _gate )
			_messages.Clear();
		MessagesCleared?.Invoke();
	}

	/// <summary>Records a successful login and remembers which account it was.</summary>
	/// <param name="account">Identifies the account logged in to (server, database and user).</param>
	/// <returns><see langword="true"/> when an earlier login in this run was to a different account (compared ignoring case),
	/// meaning what was kept for that account no longer applies; <see langword="false"/> on the first login or the same account.</returns>
	/// <remarks>Sets <see cref="IsLoggedIn"/>; it does not clear <see cref="LastErrors"/>.</remarks>
	public bool LoggedInAs( string account )
	{
		IsLoggedIn = true;
		bool changed = Account is not null && !string.Equals( Account, account, StringComparison.OrdinalIgnoreCase );
		Account = account;
		return changed;
	}
}
