using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using HackPDM.Infrastructure.Credentials;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// Saves and reads the login in the operating system's keychain:
/// <list type="bullet">
/// <item>Windows: Credential Manager (same targets as before, so saved logins keep working).</item>
/// <item>Linux: the Secret Service (GNOME Keyring, KWallet, ...) through libsecret.</item>
/// <item>macOS: the login keychain.</item>
/// </list>
/// When there is no keychain (headless machines, CI, no Secret Service running), or <c>HACKPDM_CREDENTIAL_STORE=file</c>,
/// the login goes to a fallback file instead (<see cref="FallbackFile"/>, readable by the user only). That file is
/// only obscured, not protected: its key is in the program.
/// <para>A login found in the fallback file while a keychain is available is moved into the keychain.</para>
/// </summary>
public static class CrossPlatformCredentialManager
{
	/// <summary>"file" uses the fallback file only (tests, machines without a keychain); anything else, or unset, uses the keychain.</summary>
	public const string StoreVariable = "HACKPDM_CREDENTIAL_STORE";

	private static readonly string LegacyDir = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), ".hackpdm" );

	/// <summary>Where the fallback file is: in <see cref="AppDataPaths.Config"/> when that is replaced (tests), else ~/.hackpdm/credentials.dat as before.</summary>
	internal static string FallbackFile => AppDataPaths.IsConfigOverridden
		? Path.Combine( AppDataPaths.Config, "credentials.dat" )
		: Path.Combine( LegacyDir, "credentials.dat" );

	/// <summary>The keychain of this platform; null when the fallback file is to be used.</summary>
	internal static ICredentialStore? PlatformStore { get; set; } = CreatePlatformStore();

	/// <summary>Which store saves logins, for messages.</summary>
	public static string StoreName => PlatformStore?.Name ?? "fallback file";

	private static ICredentialStore? CreatePlatformStore()
	{
		if( string.Equals( Environment.GetEnvironmentVariable( StoreVariable ), "file", StringComparison.OrdinalIgnoreCase ) )
			return null;
		if( OperatingSystem.IsWindowsVersionAtLeast( 5, 1, 2600 ) )
			return new WindowsCredentialStore();
		if( OperatingSystem.IsMacOS() )
			return new MacKeychainStore();
		if( OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD() )
			return new SecretServiceStore();
		return null;
	}

	public static (string? UserName, string? Password)? ReadCredential( string target )
		=> ReadCredential( target, forceFallback: false );

	internal static (string? UserName, string? Password)? ReadCredential( string target, bool forceFallback, string? customFilePath = null )
	{
		ICredentialStore? store = forceFallback ? null : PlatformStore;
		if( store?.Read( target ) is { } saved )
			return (saved.UserName, saved.Password);

		var fromFile = ReadFallback( target, customFilePath );
		// saved by an older version, or while the keychain was unreachable: move it into the keychain
		if( fromFile is { UserName: { } user, Password: { } password } && store is not null && store.Write( target, user, password ) )
			RemoveFallback( target, customFilePath );
		return fromFile;
	}

	public static void WriteCredential( string target, string username, string password )
		=> WriteCredential( target, username, password, forceFallback: false );

	internal static void WriteCredential( string target, string username, string password, bool forceFallback, string? customFilePath = null )
	{
		ICredentialStore? store = forceFallback ? null : PlatformStore;
		if( store is not null && store.Write( target, username, password ) )
		{
			RemoveFallback( target, customFilePath ); // no stale copy left behind
			return;
		}
		if( store is not null )
			Console.Error.WriteLine( $"credentials: {store.Name} unavailable, saving the login to {customFilePath ?? FallbackFile} instead" );
		WriteFallback( target, username, password, customFilePath );
	}

	/// <summary>Forgets the login saved under <paramref name="target"/>, in the keychain and in the fallback file.</summary>
	public static void DeleteCredential( string target )
	{
		PlatformStore?.Delete( target );
		RemoveFallback( target );
	}

	// ---------------------------------------------------------------- fallback file

	internal static (string? UserName, string? Password)? ReadFallback( string target, string? customFilePath = null )
		=> ReadFile( customFilePath ?? FallbackFile ) is { } dict && dict.TryGetValue( target, out var entry )
			? (entry.UserName, entry.Password)
			: null;

	internal static void WriteFallback( string target, string username, string password, string? customFilePath = null )
	{
		string filePath = customFilePath ?? FallbackFile;
		var dict = ReadFile( filePath ) ?? [];
		dict[ target ] = new CredentialEntry( username, password );
		WriteFile( filePath, dict );
	}

	internal static void RemoveFallback( string target, string? customFilePath = null )
	{
		string filePath = customFilePath ?? FallbackFile;
		if( ReadFile( filePath ) is not { } dict || !dict.Remove( target ) )
			return;
		if( dict.Count == 0 )
			TryDelete( filePath );
		else
			WriteFile( filePath, dict );
	}

	private static Dictionary<string, CredentialEntry>? ReadFile( string filePath )
	{
		try
		{
			if( !File.Exists( filePath ) )
				return null;
			return JsonSerializer.Deserialize<Dictionary<string, CredentialEntry>>( Decrypt( File.ReadAllBytes( filePath ) ) );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException or CryptographicException or JsonException or ArgumentException )
		{
			return null;
		}
	}

	private static void WriteFile( string filePath, Dictionary<string, CredentialEntry> dict )
	{
		try
		{
			if( Path.GetDirectoryName( filePath ) is { Length: > 0 } dir )
				Directory.CreateDirectory( dir );
			byte[] encrypted = Encrypt( JsonSerializer.Serialize( dict ) );
			if( OperatingSystem.IsWindows() )
				File.WriteAllBytes( filePath, encrypted );
			else
			{
				// readable and writable by the user only, from the moment it exists
				using FileStream stream = new( filePath, new FileStreamOptions
				{
					Mode = FileMode.Create,
					Access = FileAccess.Write,
					UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
				} );
				stream.Write( encrypted );
				File.SetUnixFileMode( filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite ); // an older file made with wider rights
			}
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			Console.Error.WriteLine( $"credentials: could not write {filePath}: {e.Message}" );
		}
	}

	private static void TryDelete( string filePath )
	{
		try
		{
			File.Delete( filePath );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}

	// obscures the fallback file only: the key is derived from a constant, so anyone with the program can read it
	internal static readonly byte[] Entropy = Encoding.UTF8.GetBytes( "HackPDM-Secure-Key-Entropy-v1" );

	internal static byte[] Encrypt( string plainText )
	{
		byte[] data = Encoding.UTF8.GetBytes( plainText );
		using var aes = Aes.Create();
		aes.Key = SHA256.HashData( Entropy );
		aes.GenerateIV();
		using var encryptor = aes.CreateEncryptor( aes.Key, aes.IV );
		byte[] encryptedData = encryptor.TransformFinalBlock( data, 0, data.Length );
		byte[] result = new byte[ aes.IV.Length + encryptedData.Length ];
		Buffer.BlockCopy( aes.IV, 0, result, 0, aes.IV.Length );
		Buffer.BlockCopy( encryptedData, 0, result, aes.IV.Length, encryptedData.Length );
		return result;
	}

	internal static string Decrypt( byte[] encrypted )
	{
		using var aes = Aes.Create();
		aes.Key = SHA256.HashData( Entropy );
		byte[] iv = new byte[ aes.BlockSize / 8 ];
		Buffer.BlockCopy( encrypted, 0, iv, 0, iv.Length );
		aes.IV = iv;
		using var decryptor = aes.CreateDecryptor( aes.Key, aes.IV );
		byte[] decrypted = decryptor.TransformFinalBlock( encrypted, iv.Length, encrypted.Length - iv.Length );
		return Encoding.UTF8.GetString( decrypted );
	}

	private sealed record CredentialEntry( string UserName, string Password );
}
