using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

/// <summary>Tracks login state, version mismatch lock, and run-wide profile/connection status log.</summary>
public sealed class LoginSession
{
	private const int MaxMessages = 200;
	private readonly Lock _gate = new();
	private readonly List<BasicStatusMessage> _messages = [];

	public bool IsLoggedIn { get; set; }
	public string? VersionMismatchMessage { get; set; }
	public bool IsVersionMismatch => !string.IsNullOrEmpty( VersionMismatchMessage );
	public string? LastReadinessFingerprint { get; set; }
	public string? Account { get; private set; }

	public event Action<BasicStatusMessage>? MessageLogged;
	public event Action? MessagesCleared;

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

	public BasicStatusMessage AddStatus( StatusMessage status, string category, string message )
	{
		BasicStatusMessage entry = new()
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

	public void ClearMessages()
	{
		lock( _gate )
			_messages.Clear();
		MessagesCleared?.Invoke();
	}

	public bool LoggedInAs( string account )
	{
		IsLoggedIn = true;
		bool changed = Account is not null && !string.Equals( Account, account, StringComparison.OrdinalIgnoreCase );
		Account = account;
		return changed;
	}
}
