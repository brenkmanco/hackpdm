using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Credentials;

/// <summary>
/// Saves and reads the login in the operating system's keychain:
/// <list type="bullet">
/// <item>Windows: Credential Manager (same targets as before, so saved logins keep working).</item>
/// <item>Linux: the Secret Service (GNOME Keyring, KWallet, ...) through libsecret.</item>
/// <item>macOS: the login keychain.</item>
/// </list>
/// When there is no keychain (headless machines, CI, no Secret Service running), or <c>HACKPDM_CREDENTIAL_STORE=file</c>,
/// the login goes to a fallback file instead (<see cref="FallbackFile"/>, readable by the user only). That file is
/// protected via DPAPI and user-only ACL on Windows, and obscured with 0600 permissions on Unix.
/// <para>A login found in the fallback file while a keychain is available is moved into the keychain.</para>
/// </summary>
public static unsafe partial class CrossPlatformCredentialManager
{
	/// <summary>"file" uses the fallback file only (tests, machines without a keychain); anything else, or unset, uses the keychain.</summary>
	public const string StoreVariable = "HACKPDM_CREDENTIAL_STORE";

	private static readonly Lock FallbackGate = new();
	private static readonly string LegacyDir = Path.Combine( ResolveUserRoot(), ".hackpdm" );
	private static readonly byte[] DpapiHeader = "HPDMDP1\0"u8.ToArray();

	private static string ResolveUserRoot()
	{
		string profile = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
		if( !string.IsNullOrEmpty( profile ) )
			return profile;
		string? home = Environment.GetEnvironmentVariable( "HOME" );
		if( !string.IsNullOrEmpty( home ) )
			return home;
		return Path.GetTempPath();
	}

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

	/// <summary>Reads the login saved under <paramref name="target"/>, from the platform keychain first and then from the fallback file.</summary>
	/// <param name="target">The credential name, such as the profile's OdooCredentialTarget.</param>
	/// <returns>The saved user name and password, or null when nothing is saved under <paramref name="target"/> (or the fallback file can't be read).</returns>
	/// <remarks>
	/// Side effect: a login found only in the fallback file while a keychain is available is written to the keychain
	/// and removed from the file. Keychain and file errors are swallowed, not thrown.
	/// </remarks>
	public static (string? UserName, string? Password)? ReadCredential( string target )
		=> ReadCredential( target, forceFallback: false );

	internal static (string? UserName, string? Password)? ReadCredential( string target, bool forceFallback, string? customFilePath = null )
	{
		if( string.IsNullOrEmpty( target ) )
			return null;
		ICredentialStore? store = forceFallback ? null : PlatformStore;
		if( store?.Read( target ) is { } saved )
			return (saved.UserName, saved.Password);

		var fromFile = ReadFallback( target, customFilePath );
		// saved by an older version, or while the keychain was unreachable: move it into the keychain
		if( fromFile is { UserName: { } user, Password: { } password } && store is not null && store.Write( target, user, password ) )
			RemoveFallback( target, customFilePath );
		return fromFile;
	}

	/// <summary>Saves a login under <paramref name="target"/> in the platform keychain, or in the fallback file when there is none.</summary>
	/// <param name="target">The credential name, such as the profile's OdooCredentialTarget.</param>
	/// <param name="username">The Odoo user name.</param>
	/// <param name="password">The Odoo password.</param>
	/// <remarks>
	/// After a successful keychain write, any copy in the fallback file is removed. If the keychain write fails, a note goes
	/// to standard error and the login is written to the fallback file (created readable and writable by the user only on Unix).
	/// A failure to write the fallback file is also only reported on standard error; nothing is thrown.
	/// </remarks>
	public static void WriteCredential( string target, string username, string password )
		=> WriteCredential( target, username, password, forceFallback: false );

	internal static void WriteCredential( string target, string username, string password, bool forceFallback, string? customFilePath = null )
	{
		if( string.IsNullOrEmpty( target ) || username is null || password is null )
			return;
		ICredentialStore? store = forceFallback ? null : PlatformStore;
		if( store is not null && store.Write( target, username, password ) )
		{
			RemoveFallback( target, customFilePath ); // no stale copy left behind
			return;
		}
		if( store is not null )
			HackLogger.Warn( "Credentials", $"{store.Name} unavailable, saving login to fallback store '{customFilePath ?? FallbackFile}'" );
		WriteFallback( target, username, password, customFilePath );
	}

	/// <summary>Forgets the login saved under <paramref name="target"/>, in the keychain and in the fallback file.</summary>
	/// <param name="target">The credential name to delete.</param>
	/// <remarks>Deleting a login that isn't saved does nothing. The fallback file is deleted once its last login is removed.</remarks>
	public static void DeleteCredential( string target )
	{
		if( string.IsNullOrEmpty( target ) )
			return;
		PlatformStore?.Delete( target );
		RemoveFallback( target );
	}

	// ---------------------------------------------------------------- fallback file

	internal static (string? UserName, string? Password)? ReadFallback( string target, string? customFilePath = null )
	{
		if( string.IsNullOrEmpty( target ) )
			return null;
		lock( FallbackGate )
		{
			return ReadFile( customFilePath ?? FallbackFile ) is { } dict && dict.TryGetValue( target, out var entry )
				? (entry.UserName, entry.Password)
				: null;
		}
	}

	internal static void WriteFallback( string target, string username, string password, string? customFilePath = null )
	{
		if( string.IsNullOrEmpty( target ) || username is null || password is null )
			return;
		string filePath = customFilePath ?? FallbackFile;
		lock( FallbackGate )
		{
			var dict = ReadFile( filePath ) ?? [];
			dict[ target ] = new CredentialEntry( username, password );
			WriteFile( filePath, dict );
		}
	}

	internal static void RemoveFallback( string target, string? customFilePath = null )
	{
		if( string.IsNullOrEmpty( target ) )
			return;
		string filePath = customFilePath ?? FallbackFile;
		lock( FallbackGate )
		{
			if( ReadFile( filePath ) is not { } dict || !dict.Remove( target ) )
				return;
			if( dict.Count == 0 )
				FileOps.TryDelete( filePath );
			else
				WriteFile( filePath, dict );
		}
	}

	private static Dictionary<string, CredentialEntry>? ReadFile( string filePath )
	{
		try
		{
			if( !File.Exists( filePath ) )
				return null;
			byte[] raw = File.ReadAllBytes( filePath );
			string json = OperatingSystem.IsWindows() && raw.Length >= DpapiHeader.Length && raw.AsSpan( 0, DpapiHeader.Length ).SequenceEqual( DpapiHeader )
				? UnprotectWindows( raw.AsSpan( DpapiHeader.Length ) )
				: Decrypt( raw );
			return JsonSerializer.Deserialize( json, CredentialJsonContext.Default.DictionaryStringCredentialEntry );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException or CryptographicException or JsonException or ArgumentException )
		{
			return null;
		}
	}

	private static void WriteFile( string filePath, Dictionary<string, CredentialEntry> dict )
	{
		string? dir = Path.GetDirectoryName( filePath );
		string tempPath = string.IsNullOrEmpty( dir )
			? $"{filePath}.{Guid.NewGuid():N}.tmp"
			: Path.Combine( dir, $"{Path.GetFileName( filePath )}.{Guid.NewGuid():N}.tmp" );
		try
		{
			if( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );
			string json = JsonSerializer.Serialize( dict, CredentialJsonContext.Default.DictionaryStringCredentialEntry );
			byte[] payload = OperatingSystem.IsWindows()
				? ProtectWindowsOrFallback( json )
				: Encrypt( json );

			FileStreamOptions options = new()
			{
				Mode = FileMode.CreateNew,
				Access = FileAccess.Write,
				Share = FileShare.None,
			};
			if( !OperatingSystem.IsWindows() )
				options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

			using( FileStream stream = new( tempPath, options ) )
			{
				stream.Write( payload );
				stream.Flush( flushToDisk: true );
			}

			if( OperatingSystem.IsWindows() )
				TrySetWindowsUserOnlyAcl( tempPath );
			else
				File.SetUnixFileMode( tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite );

			File.Move( tempPath, filePath, overwrite: true );

			if( OperatingSystem.IsWindows() )
				TrySetWindowsUserOnlyAcl( filePath );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			HackLogger.Error( "Credentials", $"Could not write fallback credential store '{filePath}': {e.Message}", e );
		}
		finally
		{
			FileOps.TryDelete( tempPath );
		}
	}

	[SupportedOSPlatform( "windows" )]
	private static byte[] ProtectWindowsOrFallback( string plainText )
	{
		byte[] plainBytes = Encoding.UTF8.GetBytes( plainText );
		try
		{
			DataBlob dataIn = default;
			DataBlob entropyBlob = default;
			DataBlob dataOut = default;
			fixed( byte* pPlain = plainBytes )
			fixed( byte* pEntropy = Entropy )
			{
				dataIn.cbData = (uint)plainBytes.Length;
				dataIn.pbData = (IntPtr)pPlain;
				entropyBlob.cbData = (uint)Entropy.Length;
				entropyBlob.pbData = (IntPtr)pEntropy;
				const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;
				if( CryptProtectData( ref dataIn, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out dataOut ) )
				{
					try
					{
						byte[] result = new byte[ DpapiHeader.Length + (int)dataOut.cbData ];
						Buffer.BlockCopy( DpapiHeader, 0, result, 0, DpapiHeader.Length );
						if( dataOut.cbData > 0 )
							Marshal.Copy( dataOut.pbData, result, DpapiHeader.Length, (int)dataOut.cbData );
						return result;
					}
					finally
					{
						if( dataOut.pbData != IntPtr.Zero )
							_ = LocalFree( dataOut.pbData );
					}
				}
			}
		}
		catch
		{
		}
		finally
		{
			Array.Clear( plainBytes );
		}
		return Encrypt( plainText );
	}

	[SupportedOSPlatform( "windows" )]
	private static string UnprotectWindows( ReadOnlySpan<byte> cipherText )
	{
		if( cipherText.IsEmpty )
			throw new CryptographicException( "Empty DPAPI credential payload." );
		byte[] cipherCopy = cipherText.ToArray();
		DataBlob dataIn = default;
		DataBlob entropyBlob = default;
		DataBlob dataOut = default;
		fixed( byte* pCipher = cipherCopy )
		fixed( byte* pEntropy = Entropy )
		{
			dataIn.cbData = (uint)cipherCopy.Length;
			dataIn.pbData = (IntPtr)pCipher;
			entropyBlob.cbData = (uint)Entropy.Length;
			entropyBlob.pbData = (IntPtr)pEntropy;
			const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;
			if( !CryptUnprotectData( ref dataIn, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out dataOut ) )
				throw new CryptographicException( Marshal.GetLastPInvokeError() );
			try
			{
				if( dataOut.cbData > int.MaxValue )
					throw new CryptographicException( "Invalid DPAPI payload size." );
				return dataOut.cbData == 0
					? string.Empty
					: Encoding.UTF8.GetString( (byte*)dataOut.pbData, (int)dataOut.cbData );
			}
			finally
			{
				if( dataOut.pbData != IntPtr.Zero )
				{
					NativeMemory.Clear( (void*)dataOut.pbData, dataOut.cbData );
					_ = LocalFree( dataOut.pbData );
				}
			}
		}
	}

	[SupportedOSPlatform( "windows" )]
	private static void TrySetWindowsUserOnlyAcl( string path )
	{
		try
		{
			using WindowsIdentity identity = WindowsIdentity.GetCurrent();
			if( identity.User is not { } userSid )
				return;
			DiscretionaryAcl dacl = new( isContainer: false, isDS: false, capacity: 1 );
			const int FileAllAccess = 0x001F01FF;
			dacl.AddAccess( AccessControlType.Allow, userSid, FileAllAccess, InheritanceFlags.None, PropagationFlags.None );
			byte[] daclBytes = new byte[ dacl.BinaryLength ];
			dacl.GetBinaryForm( daclBytes, 0 );
			fixed( byte* pDacl = daclBytes )
			{
				const uint SeFileObject = 1;
				const uint DaclSecurityInformation = 0x00000004;
				const uint ProtectedDaclSecurityInformation = 0x80000000;
				_ = SetNamedSecurityInfoW( path, SeFileObject, DaclSecurityInformation | ProtectedDaclSecurityInformation, IntPtr.Zero, IntPtr.Zero, (IntPtr)pDacl, IntPtr.Zero );
			}
		}
		catch
		{
		}
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct DataBlob
	{
		public uint cbData;
		public IntPtr pbData;
	}

	[LibraryImport( "crypt32.dll", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static partial bool CryptProtectData(
		ref DataBlob pDataIn,
		IntPtr szDataDescr,
		ref DataBlob pOptionalEntropy,
		IntPtr pvReserved,
		IntPtr pPromptStruct,
		uint dwFlags,
		out DataBlob pDataOut );

	[LibraryImport( "crypt32.dll", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static partial bool CryptUnprotectData(
		ref DataBlob pDataIn,
		IntPtr ppszDataDescr,
		ref DataBlob pOptionalEntropy,
		IntPtr pvReserved,
		IntPtr pPromptStruct,
		uint dwFlags,
		out DataBlob pDataOut );

	[LibraryImport( "kernel32.dll" )]
	private static partial IntPtr LocalFree( IntPtr hMem );

	[LibraryImport( "advapi32.dll", StringMarshalling = StringMarshalling.Utf16 )]
	private static partial uint SetNamedSecurityInfoW(
		string pObjectName,
		uint objectType,
		uint securityInfo,
		IntPtr psidOwner,
		IntPtr psidGroup,
		IntPtr pDacl,
		IntPtr pSacl );

	// obscures the fallback file only: the key is derived from a constant, so anyone with the program can read it
	internal static readonly byte[] Entropy = Encoding.UTF8.GetBytes( "HackPDM-Secure-Key-Entropy-v1" );
	private static readonly byte[] AesKey = SHA256.HashData( Entropy );

	internal static byte[] Encrypt( string plainText )
	{
		byte[] data = Encoding.UTF8.GetBytes( plainText );
		using var aes = Aes.Create();
		aes.Key = AesKey;
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
		aes.Key = AesKey;
		byte[] iv = new byte[ aes.BlockSize / 8 ];
		Buffer.BlockCopy( encrypted, 0, iv, 0, iv.Length );
		aes.IV = iv;
		using var decryptor = aes.CreateDecryptor( aes.Key, aes.IV );
		byte[] decrypted = decryptor.TransformFinalBlock( encrypted, iv.Length, encrypted.Length - iv.Length );
		return Encoding.UTF8.GetString( decrypted );
	}
}
