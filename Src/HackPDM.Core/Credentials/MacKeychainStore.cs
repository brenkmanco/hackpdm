using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace HackPDM.Core.Credentials;

/// <summary>
/// The macOS login keychain: one generic password per target (service "HackPDM", account = target), the
/// secret being the user name and password as JSON. Uses the SecKeychain generic-password calls of
/// Security.framework, which take plain C arguments.
/// </summary>
[SupportedOSPlatform( "macos" )]
internal sealed unsafe partial class MacKeychainStore : ICredentialStore
{
	private const string Security = "/System/Library/Frameworks/Security.framework/Security";
	private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
	private const int ErrSecItemNotFound = -25300;
	private static readonly byte[] Service = Encoding.UTF8.GetBytes( "HackPDM" );

	public string Name => "macOS Keychain";

	public StoredCredential? Read( string target )
	{
		if( string.IsNullOrEmpty( target ) )
			return null;
		try
		{
			byte[] account = Encoding.UTF8.GetBytes( target );
			IntPtr data = IntPtr.Zero;
			IntPtr item = IntPtr.Zero;
			try
			{
				int status = SecKeychainFindGenericPassword( IntPtr.Zero, (uint)Service.Length, Service, (uint)account.Length, account, out uint length, out data, out item );
				if( status == ErrSecItemNotFound )
					return null;
				if( status != 0 )
				{
					CredentialLog.Warn( Name, "read", $"OSStatus {status}" );
					return null;
				}
				if( data == IntPtr.Zero || length > int.MaxValue )
					return null;
				return CredentialSecret.Decode( Marshal.PtrToStringUTF8( data, (int)length ) );
			}
			finally
			{
				if( data != IntPtr.Zero )
					_ = SecKeychainItemFreeContent( IntPtr.Zero, data );
				if( item != IntPtr.Zero )
					CFRelease( item );
			}
		}
		catch( Exception e ) when( e is DllNotFoundException or EntryPointNotFoundException )
		{
			CredentialLog.Warn( Name, "read", e.Message );
			return null;
		}
	}

	public bool Write( string target, string userName, string password )
	{
		if( string.IsNullOrEmpty( target ) || userName is null || password is null )
			return false;
		try
		{
			byte[] account = Encoding.UTF8.GetBytes( target );
			byte[] secret = Encoding.UTF8.GetBytes( CredentialSecret.Encode( userName, password ) );
			IntPtr old = IntPtr.Zero;
			IntPtr item = IntPtr.Zero;
			try
			{
				int status = SecKeychainFindGenericPassword( IntPtr.Zero, (uint)Service.Length, Service, (uint)account.Length, account, out _, out old, out item );
				if( old != IntPtr.Zero )
				{
					_ = SecKeychainItemFreeContent( IntPtr.Zero, old );
					old = IntPtr.Zero;
				}
				if( status == 0 && item != IntPtr.Zero )
					status = SecKeychainItemModifyAttributesAndData( item, IntPtr.Zero, (uint)secret.Length, secret );
				else if( status == ErrSecItemNotFound )
					status = SecKeychainAddGenericPassword( IntPtr.Zero, (uint)Service.Length, Service, (uint)account.Length, account, (uint)secret.Length, secret, IntPtr.Zero );
				if( status != 0 )
					CredentialLog.Warn( Name, "write", $"OSStatus {status}" );
				return status == 0;
			}
			finally
			{
				if( old != IntPtr.Zero )
					_ = SecKeychainItemFreeContent( IntPtr.Zero, old );
				if( item != IntPtr.Zero )
					CFRelease( item );
				Array.Clear( secret );
			}
		}
		catch( Exception e ) when( e is DllNotFoundException or EntryPointNotFoundException )
		{
			CredentialLog.Warn( Name, "write", e.Message );
			return false;
		}
	}

	public bool Delete( string target )
	{
		if( string.IsNullOrEmpty( target ) )
			return false;
		try
		{
			byte[] account = Encoding.UTF8.GetBytes( target );
			IntPtr data = IntPtr.Zero;
			IntPtr item = IntPtr.Zero;
			try
			{
				int status = SecKeychainFindGenericPassword( IntPtr.Zero, (uint)Service.Length, Service, (uint)account.Length, account, out _, out data, out item );
				if( status == ErrSecItemNotFound )
					return true;
				if( status != 0 || item == IntPtr.Zero )
				{
					if( status != 0 )
						CredentialLog.Warn( Name, "delete", $"OSStatus {status}" );
					return false;
				}
				if( data != IntPtr.Zero )
				{
					_ = SecKeychainItemFreeContent( IntPtr.Zero, data );
					data = IntPtr.Zero;
				}
				status = SecKeychainItemDelete( item );
				if( status != 0 )
					CredentialLog.Warn( Name, "delete", $"OSStatus {status}" );
				return status == 0;
			}
			finally
			{
				if( data != IntPtr.Zero )
					_ = SecKeychainItemFreeContent( IntPtr.Zero, data );
				if( item != IntPtr.Zero )
					CFRelease( item );
			}
		}
		catch( Exception e ) when( e is DllNotFoundException or EntryPointNotFoundException )
		{
			CredentialLog.Warn( Name, "delete", e.Message );
			return false;
		}
	}

	[LibraryImport( Security )]
	private static partial int SecKeychainFindGenericPassword( IntPtr keychainOrArray, uint serviceNameLength, byte[] serviceName, uint accountNameLength, byte[] accountName, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef );

	[LibraryImport( Security )]
	private static partial int SecKeychainAddGenericPassword( IntPtr keychain, uint serviceNameLength, byte[] serviceName, uint accountNameLength, byte[] accountName, uint passwordLength, byte[] passwordData, IntPtr itemRef );

	[LibraryImport( Security )]
	private static partial int SecKeychainItemModifyAttributesAndData( IntPtr itemRef, IntPtr attrList, uint length, byte[] data );

	[LibraryImport( Security )]
	private static partial int SecKeychainItemFreeContent( IntPtr attrList, IntPtr data );

	[LibraryImport( Security )]
	private static partial int SecKeychainItemDelete( IntPtr itemRef );

	[LibraryImport( CoreFoundation )]
	private static partial void CFRelease( IntPtr cf );
}
