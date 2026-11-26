using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HackPDM.Core.Credentials;

/// <summary>
/// The freedesktop Secret Service on Linux (GNOME Keyring, KWallet, KeePassXC, ...) through libsecret, which
/// desktop Linux installs ship with. Items use the schema <c>org.hackpdm.Credential</c> with one attribute,
/// <c>target</c>; the secret is the user name and password as JSON.
/// <para>
/// Unavailable without libsecret or without a running Secret Service (headless machines, CI): the first
/// such failure is remembered for the run, so the caller falls back once instead of waiting on every call.
/// </para>
/// </summary>
[SupportedOSPlatform( "linux" )]
[SupportedOSPlatform( "freebsd" )]
internal sealed unsafe partial class SecretServiceStore : ICredentialStore
{
	private const string LibSecret = "libsecret-1.so.0";
	private const string LibGLib = "libglib-2.0.so.0";
	private const string SchemaName = "org.hackpdm.Credential";
	private const string TargetAttribute = "target";

	private static readonly object Gate = new();
	private static IntPtr _schema;
	private static IntPtr _strHash, _strEqual;
	private static bool _unavailable;

	public string Name => "Secret Service";

	public StoredCredential? Read( string target )
	{
		if( string.IsNullOrEmpty( target ) )
			return null;
		string? secret = null;
		bool ok = Call( "read", target, ( schema, attributes, error ) =>
		{
			IntPtr found = secret_password_lookupv_sync( schema, attributes, IntPtr.Zero, error );
			if( found != IntPtr.Zero )
			{
				secret = Marshal.PtrToStringUTF8( found );
				secret_password_free( found ); // wipes the copy libsecret made
			}
			return true;
		} );
		return ok ? CredentialSecret.Decode( secret ) : null;
	}

	public bool Write( string target, string userName, string password )
	{
		if( string.IsNullOrEmpty( target ) || userName is null || password is null )
			return false;
		string secret = CredentialSecret.Encode( userName, password );
		return Call( "write", target, ( schema, attributes, error ) =>
		{
			IntPtr label = Marshal.StringToCoTaskMemUTF8( $"HackPDM ({target})" );
			IntPtr value = Marshal.StringToCoTaskMemUTF8( secret );
			try
			{
				if( label == IntPtr.Zero || value == IntPtr.Zero )
					return false;
				return secret_password_storev_sync( schema, attributes, IntPtr.Zero, label, value, IntPtr.Zero, error ) != 0;
			}
			finally
			{
				if( value != IntPtr.Zero )
				{
					NativeMemory.Clear( (void*)value, (nuint)System.Text.Encoding.UTF8.GetByteCount( secret ) );
					Marshal.FreeCoTaskMem( value );
				}
				if( label != IntPtr.Zero )
					Marshal.FreeCoTaskMem( label );
			}
		} );
	}

	public bool Delete( string target )
	{
		if( string.IsNullOrEmpty( target ) )
			return false;
		return Call( "delete", target, ( schema, attributes, error ) =>
		{
			_ = secret_password_clearv_sync( schema, attributes, IntPtr.Zero, error ); // false when there was nothing to clear
			return true;
		} );
	}

	private delegate bool SecretCall( IntPtr schema, IntPtr attributes, IntPtr* error );

	/// <summary>Runs <paramref name="action"/> with the schema and a { target } attribute table; false when the service cannot be used.</summary>
	private bool Call( string what, string target, SecretCall action )
	{
		if( string.IsNullOrEmpty( target ) )
			return false;
		lock( Gate )
		{
			if( _unavailable || !EnsureLoaded() )
				return false;

			IntPtr attributes = g_hash_table_new( _strHash, _strEqual ); // keys and values stay ours
			IntPtr key = Marshal.StringToCoTaskMemUTF8( TargetAttribute );
			IntPtr value = Marshal.StringToCoTaskMemUTF8( target );
			IntPtr error = IntPtr.Zero;
			try
			{
				if( attributes == IntPtr.Zero || key == IntPtr.Zero || value == IntPtr.Zero )
					return false;
				_ = g_hash_table_insert( attributes, key, value );
				bool done = action( _schema, attributes, &error );
				if( error != IntPtr.Zero )
				{
					string? message = Marshal.PtrToStringUTF8( Marshal.ReadIntPtr( error, 8 ) ); // GError { GQuark domain; gint code; gchar *message; }
					g_error_free( error );
					// only remember unavailability when the session bus / Secret Service daemon itself is missing;
					// transient failures (locked keyring, cancelled prompt, timeouts) can succeed on retry
					if( IsServiceUnavailableError( message ) )
						_unavailable = true;
					CredentialLog.Warn( Name, what, message ?? "unknown error" );
					return false;
				}
				return done;
			}
			catch( Exception e ) when( e is DllNotFoundException or EntryPointNotFoundException )
			{
				_unavailable = true;
				CredentialLog.Warn( Name, what, e.Message );
				return false;
			}
			finally
			{
				if( attributes != IntPtr.Zero )
					g_hash_table_unref( attributes );
				if( key != IntPtr.Zero )
					Marshal.FreeCoTaskMem( key );
				if( value != IntPtr.Zero )
					Marshal.FreeCoTaskMem( value );
			}
		}
	}

	internal static bool IsServiceUnavailableError( string? message )
	{
		if( string.IsNullOrWhiteSpace( message ) )
			return false;
		if( message.Contains( "cancel", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "locked", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "dismiss", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "denied", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "timeout", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "timed out", StringComparison.OrdinalIgnoreCase ) )
			return false;

		return message.Contains( "was not provided by any .service files", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "ServiceUnknown", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "NameHasNoOwner", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "No such service", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "autolaunch", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "dbus-launch", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "dbus-daemon", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "DBUS_SESSION_BUS_ADDRESS", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "No session bus", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "Cannot connect to", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "Failed to connect to socket", StringComparison.OrdinalIgnoreCase )
			|| message.Contains( "Connection refused", StringComparison.OrdinalIgnoreCase );
	}

	// under Gate
	private static bool EnsureLoaded()
	{
		if( _schema != IntPtr.Zero )
			return true;
		if( IntPtr.Size != 8 )
		{
			_unavailable = true;
			return false;
		}
		try
		{
			if( !NativeLibrary.TryLoad( LibSecret, out _ ) || !NativeLibrary.TryLoad( LibGLib, out IntPtr glib ) )
			{
				_unavailable = true;
				return false;
			}
			_strHash = NativeLibrary.GetExport( glib, "g_str_hash" );
			_strEqual = NativeLibrary.GetExport( glib, "g_str_equal" );
			_schema = BuildSchema();
			return true;
		}
		catch( Exception e ) when( e is DllNotFoundException or EntryPointNotFoundException )
		{
			_unavailable = true;
			return false;
		}
	}

	/// <summary>
	/// A SecretSchema, laid out as libsecret declares it (64-bit):
	/// <c>{ const gchar *name; SecretSchemaFlags flags; SecretSchemaAttribute attributes[32]; gint reserved; gpointer reserved1..7; }</c>
	/// with <c>SecretSchemaAttribute { const gchar *name; SecretSchemaAttributeType type; }</c>. Made once and kept for the run.
	/// </summary>
	private static IntPtr BuildSchema()
	{
		const int attributesOffset = 16; // name (8) + flags (4) + padding (4)
		const int attributeSize = 16;    // name (8) + type (4) + padding (4)
		const int size = attributesOffset + 32 * attributeSize + 8 + 7 * 8;

		IntPtr schema = (IntPtr)NativeMemory.AllocZeroed( size );
		Marshal.WriteIntPtr( schema, 0, Marshal.StringToCoTaskMemUTF8( SchemaName ) );
		Marshal.WriteInt32( schema, 8, 0 ); // SECRET_SCHEMA_NONE: items must carry this schema's name
		Marshal.WriteIntPtr( schema, attributesOffset, Marshal.StringToCoTaskMemUTF8( TargetAttribute ) );
		Marshal.WriteInt32( schema, attributesOffset + 8, 0 ); // SECRET_SCHEMA_ATTRIBUTE_STRING
		// the next attribute's name stays NULL, which ends the list
		return schema;
	}

	[LibraryImport( LibSecret )]
	private static partial int secret_password_storev_sync( IntPtr schema, IntPtr attributes, IntPtr collection, IntPtr label, IntPtr password, IntPtr cancellable, IntPtr* error );

	[LibraryImport( LibSecret )]
	private static partial IntPtr secret_password_lookupv_sync( IntPtr schema, IntPtr attributes, IntPtr cancellable, IntPtr* error );

	[LibraryImport( LibSecret )]
	private static partial int secret_password_clearv_sync( IntPtr schema, IntPtr attributes, IntPtr cancellable, IntPtr* error );

	[LibraryImport( LibSecret )]
	private static partial void secret_password_free( IntPtr password );

	[LibraryImport( LibGLib )]
	private static partial IntPtr g_hash_table_new( IntPtr hashFunc, IntPtr equalFunc );

	[LibraryImport( LibGLib )]
	private static partial int g_hash_table_insert( IntPtr table, IntPtr key, IntPtr value );

	[LibraryImport( LibGLib )]
	private static partial void g_hash_table_unref( IntPtr table );

	[LibraryImport( LibGLib )]
	private static partial void g_error_free( IntPtr error );
}
