using System.Text.Json;

namespace HackPDM.Infrastructure.Credentials;

/// <summary>A saved user name and password.</summary>
public sealed record StoredCredential( string? UserName, string? Password );

/// <summary>A place to keep passwords, normally the operating system's keychain.</summary>
public interface ICredentialStore
{
	/// <summary>For messages: "Windows Credential Manager", "Secret Service", ...</summary>
	string Name { get; }

	/// <summary>The credential saved under <paramref name="target"/>; null when there is none or the store cannot be reached.</summary>
	StoredCredential? Read( string target );

	/// <summary>Saves (or replaces) the credential under <paramref name="target"/>; false when the store cannot be reached.</summary>
	bool Write( string target, string userName, string password );

	/// <summary>Forgets the credential under <paramref name="target"/>; false when the store cannot be reached.</summary>
	bool Delete( string target );
}

/// <summary>Stores that hold one secret string per item keep the user name and password in it as JSON.</summary>
internal static class CredentialSecret
{
	private sealed record Secret( string? U, string? P );

	public static string Encode( string userName, string password ) => JsonSerializer.Serialize( new Secret( userName, password ) );

	public static StoredCredential? Decode( string? secret )
	{
		if( string.IsNullOrEmpty( secret ) )
			return null;
		try
		{
			return JsonSerializer.Deserialize<Secret>( secret ) is { } s ? new( s.U, s.P ) : null;
		}
		catch( JsonException )
		{
			return null;
		}
	}
}
