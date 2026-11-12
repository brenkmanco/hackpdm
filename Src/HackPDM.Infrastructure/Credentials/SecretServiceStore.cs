using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HackPDM.Infrastructure.Credentials;

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
		string secret = CredentialSecret.Encode( userName, password );
		return Call( "write", target, ( schema, attributes, error ) =>
		{
			IntPtr label = Marshal.StringToCoTaskMemUTF8( $"HackPDM ({target})" );
			IntPtr value = Marshal.StringToCoTaskMemUTF8( secret );
			try
			{
				return secret_password_storev_sync( schema, attributes, IntPtr.Zero, label, value, IntPtr.Zero, error ) != 0;
			}
			finally
			{
				NativeMemory.Clear( (void*)value, (nuint)System.Text.Encoding.UTF8.GetByteCount( secret ) );
				Marshal.FreeCoTaskMem( value );
				Marshal.FreeCoTaskMem( label );
			}
		} );
	}

	public bool Delete( string target )
		=> Call( "delete", target, ( schema, attributes, error ) =>
		{
			_ = secret_password_clearv_sync( schema, attributes, IntPtr.Zero, error ); // false when there was nothing to clear
			return true;
		} );

	private delegate bool SecretCall( IntPtr schema, IntPtr attributes, IntPtr* error );

	/// <summary>Runs <paramref name="action"/> with the schema and a { target } attribute table; false when the service cannot be used.</summary>
	private bool Call( string what, string target, SecretCall action )
	{
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
				_ = g_hash_table_insert( attributes, key, value );
				bool done = action( _schema, attributes, &error );
				if( error != IntPtr.Zero )
				{
					// e.g. no Secret Service on the session bus: stop trying for this run
					string? message = Marshal.PtrToStringUTF8( Marshal.ReadIntPtr( error, 8 ) ); // GError { GQuark domain; gint code; gchar *message; }
					g_error_free( error );
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
				g_hash_table_unref( attributes );
				Marshal.FreeCoTaskMem( key );
				Marshal.FreeCoTaskMem( value );
			}
		}
	}

	// under Gate
	private static bool EnsureLoaded()
	{
		if( _schema != IntPtr.Zero )
			return true;
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
