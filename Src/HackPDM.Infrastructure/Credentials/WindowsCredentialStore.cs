using System;
using System.Runtime.Versioning;

using Meziantou.Framework.Win32;

namespace HackPDM.Infrastructure.Credentials;

/// <summary>
/// The Windows Credential Manager (generic credentials, target name as given). Works for packaged and
/// unpackaged apps; credentials saved by earlier versions are found under the same target.
/// </summary>
[SupportedOSPlatform( "windows5.1.2600" )]
internal sealed class WindowsCredentialStore : ICredentialStore
{
	public string Name => "Windows Credential Manager";

	public StoredCredential? Read( string target )
	{
		try
		{
			return CredentialManager.ReadCredential( target, CredentialType.Generic ) is { } c ? new( c.UserName, c.Password ) : null;
		}
		catch( Exception e )
		{
			CredentialLog.Warn( Name, "read", e.Message );
			return null;
		}
	}

	public bool Write( string target, string userName, string password )
	{
		try
		{
			CredentialManager.WriteCredential( target, userName, password, CredentialPersistence.LocalMachine );
			return true;
		}
		catch( Exception e )
		{
			CredentialLog.Warn( Name, "write", e.Message );
			return false;
		}
	}

	public bool Delete( string target )
	{
		try
		{
			if( CredentialManager.ReadCredential( target, CredentialType.Generic ) is not null )
				CredentialManager.DeleteCredential( target, CredentialType.Generic );
			return true;
		}
		catch( Exception e )
		{
			CredentialLog.Warn( Name, "delete", e.Message );
			return false;
		}
	}
}

internal static class CredentialLog
{
	public static void Warn( string store, string action, string message )
		=> Console.Error.WriteLine( $"credentials: {store} could not {action}: {message}" );
}
