using System;
using System.Collections.Generic;
using System.IO;

using HackPDM.Infrastructure.Credentials;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Tests.Characterization;

using Xunit;

namespace HackPDM.Tests.Compatibility;

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

	[Fact]
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

	[Fact]
	public void SecretService_RoundTrip_WhenAskedFor()
	{
		if( !OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable( "HACKPDM_TEST_KEYRING" ) != "1" )
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
}
