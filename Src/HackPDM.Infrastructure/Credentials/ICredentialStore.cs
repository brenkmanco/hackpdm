using System.Text.Json;

namespace HackPDM.Infrastructure.Credentials;

/// <summary>A saved user name and password.</summary>
public sealed record StoredCredential( string? UserName, string? Password );

/// <summary>A place to keep passwords, normally the operating system's keychain.</summary>
/// <remarks>
/// Implementations report failures through their return values and a note on standard error; they don't throw for
/// an unreachable store. <see cref="HackPDM.Infrastructure.Odoo.CrossPlatformCredentialManager"/> picks the store for
/// the platform and falls back to a file when a call fails.
/// </remarks>
public interface ICredentialStore
{
	/// <summary>A display name for messages: "Windows Credential Manager", "Secret Service", "macOS Keychain".</summary>
	string Name { get; }

	/// <summary>Looks up the credential saved under <paramref name="target"/>.</summary>
	/// <param name="target">The credential name (the Windows target name, the Secret Service <c>target</c> attribute, or the macOS account).</param>
	/// <returns>The saved credential; null when there is none, it can't be decoded, or the store cannot be reached.</returns>
	StoredCredential? Read( string target );

	/// <summary>Saves the credential under <paramref name="target"/>, replacing any existing one.</summary>
	/// <param name="target">The credential name.</param>
	/// <param name="userName">The user name to save.</param>
	/// <param name="password">The password to save.</param>
	/// <returns>True when saved; false when the store cannot be reached or refused the write.</returns>
	bool Write( string target, string userName, string password );

	/// <summary>Forgets the credential saved under <paramref name="target"/>.</summary>
	/// <param name="target">The credential name.</param>
	/// <returns>True when the credential is gone afterwards (including when there was none); false when the store cannot be reached.</returns>
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
