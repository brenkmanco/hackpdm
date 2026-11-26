using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using HackPDM.Tests.Characterization;

using Xunit;

namespace HackPDM.Tests.Compatibility;

public sealed class UnixFactAttribute : FactAttribute
{
	public UnixFactAttribute()
	{
		if( OperatingSystem.IsWindows() )
			Skip = "Unix file permissions apply only on non-Windows platforms.";
	}
}

public sealed class SecretServiceFactAttribute : FactAttribute
{
	public SecretServiceFactAttribute()
	{
		if( !OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable( "HACKPDM_TEST_KEYRING" ) != "1" )
			Skip = "Set HACKPDM_TEST_KEYRING=1 on Linux with a running Secret Service.";
	}
}

/// <summary>
/// Which store the login goes to: the system keychain when there is one, the fallback file otherwise, and
/// logins found in the file moved into the keychain. The keychain is a fake here; the real Secret Service
/// is tried only when HACKPDM_TEST_KEYRING=1 (it writes to, then removes from, the user's keyring).
/// </summary>
[Collection( SharedStateCollection.Name )]
public sealed class CredentialStoreTests : IDisposable
{
	private readonly ICredentialStore? _previous = CrossPlatformCredentialManager.PlatformStore;
	private readonly string _file = Path.Combine( Path.GetTempPath(), $"hackpdm-cred-{Guid.NewGuid():N}.dat" );

	public void Dispose()
	{
		CrossPlatformCredentialManager.PlatformStore = _previous;
		if( File.Exists( _file ) )
			File.Delete( _file );
	}

	private sealed class FakeKeychain( bool reachable = true ) : ICredentialStore
	{
		public Dictionary<string, StoredCredential> Items { get; } = [];
		public string Name => "fake keychain";
		public StoredCredential? Read( string target ) => reachable && Items.TryGetValue( target, out var c ) ? c : null;
		public bool Write( string target, string userName, string password )
		{
			if( reachable )
				Items[ target ] = new( userName, password );
			return reachable;
		}
		public bool Delete( string target ) => reachable && Items.Remove( target );
	}

	[Fact]
	public void Login_GoesToTheKeychain_NotTheFile()
	{
		FakeKeychain keychain = new();
		CrossPlatformCredentialManager.PlatformStore = keychain;

		CrossPlatformCredentialManager.WriteCredential( "HackPDM-OdooUser", "engineer", "s3cret", forceFallback: false, customFilePath: _file );

		Assert.Equal( new StoredCredential( "engineer", "s3cret" ), keychain.Items[ "HackPDM-OdooUser" ] );
		Assert.False( File.Exists( _file ) );
		Assert.Equal( ("engineer", "s3cret"), CrossPlatformCredentialManager.ReadCredential( "HackPDM-OdooUser", false, _file ) );
	}

	[Fact]
	public void LoginInTheOldFile_IsMovedIntoTheKeychain()
	{
		CrossPlatformCredentialManager.WriteFallback( "HackPDM-OdooUser", "engineer", "old-pass", _file );
		CrossPlatformCredentialManager.WriteFallback( "other-target", "someone", "x", _file );
		FakeKeychain keychain = new();
		CrossPlatformCredentialManager.PlatformStore = keychain;

		var read = CrossPlatformCredentialManager.ReadCredential( "HackPDM-OdooUser", false, _file );

		Assert.Equal( ("engineer", "old-pass"), read );
		Assert.Equal( new StoredCredential( "engineer", "old-pass" ), keychain.Items[ "HackPDM-OdooUser" ] );
		Assert.Null( CrossPlatformCredentialManager.ReadFallback( "HackPDM-OdooUser", _file ) ); // gone from the file
		Assert.NotNull( CrossPlatformCredentialManager.ReadFallback( "other-target", _file ) );  // the rest stays
	}

	[Fact]
	public void UnreachableKeychain_FallsBackToTheFile()
	{
		CrossPlatformCredentialManager.PlatformStore = new FakeKeychain( reachable: false );

		CrossPlatformCredentialManager.WriteCredential( "HackPDM-OdooUser", "engineer", "s3cret", false, _file );

		Assert.Equal( ("engineer", "s3cret"), CrossPlatformCredentialManager.ReadFallback( "HackPDM-OdooUser", _file ) );
		Assert.Equal( ("engineer", "s3cret"), CrossPlatformCredentialManager.ReadCredential( "HackPDM-OdooUser", false, _file ) );
	}

	[UnixFact]
	public void FallbackFile_IsReadableByTheUserOnly()
	{
		if( OperatingSystem.IsWindows() )
			return;
		CrossPlatformCredentialManager.WriteFallback( "t", "u", "p", _file );
		Assert.Equal( UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode( _file ) );
	}

	[Fact]
	public void NoKeychain_UsesTheFile()
	{
		CrossPlatformCredentialManager.PlatformStore = null; // HACKPDM_CREDENTIAL_STORE=file, or no keychain on this platform

		CrossPlatformCredentialManager.WriteCredential( "t", "u", "p", false, _file );

		Assert.Equal( ("u", "p"), CrossPlatformCredentialManager.ReadCredential( "t", false, _file ) );
		Assert.Equal( "fallback file", CrossPlatformCredentialManager.StoreName );
	}

	[SecretServiceFact]
	public void SecretService_RoundTrip_WhenAskedFor()
	{
		if( !OperatingSystem.IsLinux() )
			return;
		SecretServiceStore store = new();
		string target = $"hackpdm-test-{Guid.NewGuid():N}";
		try
		{
			Assert.True( store.Write( target, "engineer", "pässwörd \"quoted\"" ) );
			Assert.Equal( new StoredCredential( "engineer", "pässwörd \"quoted\"" ), store.Read( target ) );
			Assert.True( store.Write( target, "engineer", "changed" ) ); // replaces, not a second item
			Assert.Equal( "changed", store.Read( target )?.Password );
		}
		finally
		{
			Assert.True( store.Delete( target ) );
		}
		Assert.Null( store.Read( target ) );
	}

#pragma warning disable CA1416
	[Fact]
	public void CredentialStores_GuardNullAndEmptyInputsWithoutThrowing()
	{
		MacKeychainStore mac = new();
		Assert.Null( mac.Read( null! ) );
		Assert.Null( mac.Read( "" ) );
		Assert.False( mac.Write( null!, "u", "p" ) );
		Assert.False( mac.Write( "", "u", "p" ) );
		Assert.False( mac.Write( "t", null!, "p" ) );
		Assert.False( mac.Write( "t", "u", null! ) );
		Assert.False( mac.Delete( null! ) );
		Assert.False( mac.Delete( "" ) );

		SecretServiceStore secretService = new();
		Assert.Null( secretService.Read( null! ) );
		Assert.Null( secretService.Read( "" ) );
		Assert.False( secretService.Write( null!, "u", "p" ) );
		Assert.False( secretService.Write( "", "u", "p" ) );
		Assert.False( secretService.Write( "t", null!, "p" ) );
		Assert.False( secretService.Write( "t", "u", null! ) );
		Assert.False( secretService.Delete( null! ) );
		Assert.False( secretService.Delete( "" ) );

		WindowsCredentialStore win = new();
		Assert.Null( win.Read( null! ) );
		Assert.Null( win.Read( "" ) );
		Assert.False( win.Write( null!, "u", "p" ) );
		Assert.False( win.Write( "", "u", "p" ) );
		Assert.False( win.Write( "t", null!, "p" ) );
		Assert.False( win.Write( "t", "u", null! ) );
		Assert.False( win.Delete( null! ) );
		Assert.False( win.Delete( "" ) );
	}

	[Theory]
	[InlineData( "The name org.freedesktop.secrets was not provided by any .service files", true )]
	[InlineData( "Cannot autolaunch D-Bus without X11 $DISPLAY", true )]
	[InlineData( "Failed to connect to socket /run/user/1000/bus: Connection refused", true )]
	[InlineData( "Operation was cancelled by user", false )]
	[InlineData( "Cannot create an item in a locked collection", false )]
	[InlineData( "Prompt dismissed", false )]
	[InlineData( null, false )]
	public void SecretService_IsServiceUnavailableError_DistinguishesMissingDaemonFromTransientFailure( string? message, bool expectedUnavailable )
		=> Assert.Equal( expectedUnavailable, SecretServiceStore.IsServiceUnavailableError( message ) );
#pragma warning restore CA1416

	[Fact]
	public void CredentialSecret_EncodesAndDecodesWithoutReflection()
	{
		string encoded = CredentialSecret.Encode( "alice", "p@ss\"1" );
		Assert.Equal( new StoredCredential( "alice", "p@ss\"1" ), CredentialSecret.Decode( encoded ) );
		Assert.Null( CredentialSecret.Decode( null ) );
		Assert.Null( CredentialSecret.Decode( "" ) );
		Assert.Null( CredentialSecret.Decode( "null" ) );
		Assert.Null( CredentialSecret.Decode( "{bad json" ) );
	}

	[Fact]
	public async Task FallbackStore_ConcurrentWritesAndReads_AreSerializedAndAtomic()
	{
		Task[] workers = Enumerable.Range( 0, 12 ).Select( i => Task.Run( () =>
		{
			string target = $"worker-{i}";
			CrossPlatformCredentialManager.WriteFallback( target, $"user-{i}", $"pass-{i}", _file );
			var read = CrossPlatformCredentialManager.ReadFallback( target, _file );
			Assert.Equal( ($"user-{i}", $"pass-{i}"), read );
		} ) ).ToArray();

		await Task.WhenAll( workers );

		for( int i = 0; i < 12; i++ )
			Assert.Equal( ($"user-{i}", $"pass-{i}"), CrossPlatformCredentialManager.ReadFallback( $"worker-{i}", _file ) );

		string dir = Path.GetDirectoryName( _file )!;
		string name = Path.GetFileName( _file );
		Assert.Empty( Directory.GetFiles( dir, $"{name}.*.tmp" ) );
	}
}
