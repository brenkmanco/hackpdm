using HackPDM.Core.Pdm;

namespace HackPDM.Core.Services;

/// <summary>Tracks login state, version mismatch lock, and run-wide profile/connection status log.</summary>
public sealed class LoginSession
{
	private const int MaxMessages = 200;
	private readonly Lock _gate = new();
	private readonly List<BasicStatusMessage> _messages = [];

	private bool _isLoggedIn;
	private string? _versionMismatchMessage;
	private string? _lastReadinessFingerprint;
	private string? _account;

	public bool IsLoggedIn
	{
		get { lock( _gate ) return _isLoggedIn; }
		set { lock( _gate ) _isLoggedIn = value; }
	}

	public string? VersionMismatchMessage
	{
		get { lock( _gate ) return _versionMismatchMessage; }
		set { lock( _gate ) _versionMismatchMessage = value; }
	}

	public bool IsVersionMismatch
	{
		get { lock( _gate ) return !string.IsNullOrEmpty( _versionMismatchMessage ); }
	}

	public string? LastReadinessFingerprint
	{
		get { lock( _gate ) return _lastReadinessFingerprint; }
		set { lock( _gate ) _lastReadinessFingerprint = value; }
	}

	public string? Account
	{
		get { lock( _gate ) return _account; }
		private set { lock( _gate ) _account = value; }
	}

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
		lock( _gate )
		{
			_isLoggedIn = true;
			bool changed = _account is not null && !string.Equals( _account, account, StringComparison.OrdinalIgnoreCase );
			_account = account;
			return changed;
		}
	}
}
