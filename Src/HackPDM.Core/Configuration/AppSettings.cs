using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HackPDM.Core.Configuration;

[JsonSerializable( typeof( string ) )]
[JsonSerializable( typeof( bool ) )]
[JsonSerializable( typeof( byte ) )]
[JsonSerializable( typeof( sbyte ) )]
[JsonSerializable( typeof( short ) )]
[JsonSerializable( typeof( ushort ) )]
[JsonSerializable( typeof( int ) )]
[JsonSerializable( typeof( uint ) )]
[JsonSerializable( typeof( long ) )]
[JsonSerializable( typeof( ulong ) )]
[JsonSerializable( typeof( float ) )]
[JsonSerializable( typeof( double ) )]
[JsonSerializable( typeof( decimal ) )]
[JsonSerializable( typeof( DateTime ) )]
[JsonSerializable( typeof( DateTimeOffset ) )]
[JsonSerializable( typeof( Guid ) )]
[JsonSerializable( typeof( bool? ) )]
[JsonSerializable( typeof( int? ) )]
[JsonSerializable( typeof( long? ) )]
[JsonSerializable( typeof( float? ) )]
[JsonSerializable( typeof( double? ) )]
[JsonSerializable( typeof( decimal? ) )]
[JsonSerializable( typeof( string[] ) )]
[JsonSerializable( typeof( JsonElement ) )]
[JsonSerializable( typeof( Dictionary<string, string> ) )]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;

/// <summary>Key/value persistence abstraction for application settings.</summary>
public interface ISettingsProvider
{
	T? Get<T>( string key, T? defaultValue = default );
	void Set<T>( string key, T value );
	bool Contains( string key );
	void Remove( string key );

	void SetMany( IEnumerable<KeyValuePair<string, object?>> values )
	{
		foreach( var (key, value) in values )
		{
			if( value is null )
				Remove( key );
			else
				Set( key, value );
		}
	}

	Task SetManyAsync( IEnumerable<KeyValuePair<string, object?>> values, CancellationToken token = default )
		=> Task.Run( () => SetMany( values ), token );
}

/// <summary>Thread-safe JSON file settings store written atomically via a temp file.</summary>
public sealed class JsonFileSettingsProvider : ISettingsProvider
{
	private static readonly JsonWriterOptions IndentedWriterOptions = new() { Indented = true };

	private readonly object _gate = new();
	private readonly SemaphoreSlim _writeGate = new( 1, 1 );
	private readonly Dictionary<string, JsonElement> _values;
	private readonly List<string> _errors = [];
	private long _stateVersion;
	private long _writtenVersion;

	public JsonFileSettingsProvider( string filePath )
	{
		FilePath = filePath;
		Existed = File.Exists( filePath );
		_values = Load( filePath, _errors );
	}

	public string FilePath { get; }
	public bool Existed { get; }

	public IReadOnlyList<string> Errors
	{
		get
		{
			lock( _gate )
				return [ .. _errors ];
		}
	}

	public event Action<string>? ErrorOccurred;

	public T? Get<T>( string key, T? defaultValue = default )
	{
		lock( _gate )
		{
			if( !_values.TryGetValue( key, out JsonElement value ) || value.ValueKind == JsonValueKind.Null )
				return defaultValue;

			if( typeof( T ) == typeof( JsonElement ) )
			{
				JsonElement cloned = value.Clone();
				return Unsafe.As<JsonElement, T>( ref cloned );
			}
			if( typeof( T ) == typeof( JsonElement? ) )
			{
				JsonElement? cloned = value.Clone();
				return Unsafe.As<JsonElement?, T?>( ref cloned );
			}
			if( typeof( T ) == typeof( string ) )
			{
				if( value.ValueKind != JsonValueKind.String )
					return defaultValue;
				string? s = value.GetString();
				return Unsafe.As<string?, T?>( ref s );
			}
			if( typeof( T ) == typeof( bool ) )
			{
				if( value.ValueKind is not ( JsonValueKind.True or JsonValueKind.False ) )
					return defaultValue;
				bool b = value.ValueKind == JsonValueKind.True;
				return Unsafe.As<bool, T>( ref b );
			}
			if( typeof( T ) == typeof( bool? ) )
			{
				if( value.ValueKind is not ( JsonValueKind.True or JsonValueKind.False ) )
					return defaultValue;
				bool? nb = value.ValueKind == JsonValueKind.True;
				return Unsafe.As<bool?, T?>( ref nb );
			}
			if( typeof( T ) == typeof( int ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetInt32( out int i ) )
					return defaultValue;
				return Unsafe.As<int, T>( ref i );
			}
			if( typeof( T ) == typeof( int? ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetInt32( out int i ) )
					return defaultValue;
				int? ni = i;
				return Unsafe.As<int?, T?>( ref ni );
			}
			if( typeof( T ) == typeof( long ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetInt64( out long l ) )
					return defaultValue;
				return Unsafe.As<long, T>( ref l );
			}
			if( typeof( T ) == typeof( long? ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetInt64( out long l ) )
					return defaultValue;
				long? nl = l;
				return Unsafe.As<long?, T?>( ref nl );
			}
			if( typeof( T ) == typeof( float ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetSingle( out float f ) )
					return defaultValue;
				return Unsafe.As<float, T>( ref f );
			}
			if( typeof( T ) == typeof( float? ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetSingle( out float f ) )
					return defaultValue;
				float? nf = f;
				return Unsafe.As<float?, T?>( ref nf );
			}
			if( typeof( T ) == typeof( double ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetDouble( out double d ) )
					return defaultValue;
				return Unsafe.As<double, T>( ref d );
			}
			if( typeof( T ) == typeof( double? ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetDouble( out double d ) )
					return defaultValue;
				double? nd = d;
				return Unsafe.As<double?, T?>( ref nd );
			}
			if( typeof( T ) == typeof( decimal ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal( out decimal m ) )
					return defaultValue;
				return Unsafe.As<decimal, T>( ref m );
			}
			if( typeof( T ) == typeof( decimal? ) )
			{
				if( value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal( out decimal m ) )
					return defaultValue;
				decimal? nm = m;
				return Unsafe.As<decimal?, T?>( ref nm );
			}

			try
			{
				if( typeof( T ) == typeof( AppUiConfig ) )
				{
					AppUiConfig? ui = JsonSerializer.Deserialize( value, AppUiConfigJsonContext.Default.AppUiConfig );
					return Unsafe.As<AppUiConfig?, T?>( ref ui );
				}
				if( AppSettingsJsonContext.Default.GetTypeInfo( typeof( T ) ) is JsonTypeInfo<T> typeInfo )
					return JsonSerializer.Deserialize( value, typeInfo );
				return defaultValue;
			}
			catch( Exception e ) when( e is JsonException or NotSupportedException or InvalidOperationException )
			{
				return defaultValue;
			}
		}
	}

	public void Set<T>( string key, T value )
	{
		if( value is null || ( value is JsonElement je && je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ) )
		{
			Remove( key );
			return;
		}
		JsonElement element = SerializeToJsonElement( value );
		long version;
		byte[] payload;
		lock( _gate )
		{
			_values[ key ] = element;
			(version, payload) = SnapshotLocked();
		}
		string? error = WriteSnapshot( version, payload );
		if( error is not null )
			ErrorOccurred?.Invoke( error );
	}

	public Task SetAsync<T>( string key, T value, CancellationToken token = default )
	{
		if( value is null || ( value is JsonElement je && je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ) )
			return RemoveAsync( key, token );
		JsonElement element = SerializeToJsonElement( value );
		long version;
		byte[] payload;
		lock( _gate )
		{
			_values[ key ] = element;
			(version, payload) = SnapshotLocked();
		}
		return WriteSnapshotAndNotifyAsync( version, payload, token );
	}

	public void SetMany( IEnumerable<KeyValuePair<string, object?>> values )
	{
		ArgumentNullException.ThrowIfNull( values );
		List<(string Key, JsonElement? Element)> updates = PrepareBatchUpdates( values );
		if( updates.Count == 0 )
			return;
		long version = 0;
		byte[]? payload = null;
		lock( _gate )
		{
			if( ApplyBatchUpdatesLocked( updates ) )
				(version, payload) = SnapshotLocked();
		}
		if( payload is null )
			return;
		string? error = WriteSnapshot( version, payload );
		if( error is not null )
			ErrorOccurred?.Invoke( error );
	}

	public Task SetManyAsync( IEnumerable<KeyValuePair<string, object?>> values, CancellationToken token = default )
	{
		ArgumentNullException.ThrowIfNull( values );
		List<(string Key, JsonElement? Element)> updates = PrepareBatchUpdates( values );
		if( updates.Count == 0 )
			return Task.CompletedTask;
		long version = 0;
		byte[]? payload = null;
		lock( _gate )
		{
			if( ApplyBatchUpdatesLocked( updates ) )
				(version, payload) = SnapshotLocked();
		}
		return payload is null ? Task.CompletedTask : WriteSnapshotAndNotifyAsync( version, payload, token );
	}

	public bool Contains( string key )
	{
		lock( _gate )
			return _values.ContainsKey( key );
	}

	public void Remove( string key )
	{
		long version = 0;
		byte[]? payload = null;
		lock( _gate )
		{
			if( _values.Remove( key ) )
				(version, payload) = SnapshotLocked();
		}
		if( payload is null )
			return;
		string? error = WriteSnapshot( version, payload );
		if( error is not null )
			ErrorOccurred?.Invoke( error );
	}

	public Task RemoveAsync( string key, CancellationToken token = default )
	{
		long version = 0;
		byte[]? payload = null;
		lock( _gate )
		{
			if( _values.Remove( key ) )
				(version, payload) = SnapshotLocked();
		}
		return payload is null ? Task.CompletedTask : WriteSnapshotAndNotifyAsync( version, payload, token );
	}

	public int ImportMissing( IEnumerable<KeyValuePair<string, object?>> values )
	{
		int added = 0;
		long version = 0;
		byte[]? payload = null;
		lock( _gate )
		{
			foreach( var (key, value) in values )
			{
				if( value is null || _values.ContainsKey( key ) )
					continue;
				if( TryToJsonElement( value, out JsonElement element ) )
				{
					_values[ key ] = element;
					added++;
				}
			}
			if( added > 0 )
				(version, payload) = SnapshotLocked();
		}
		if( payload is not null && WriteSnapshot( version, payload ) is { } error )
			ErrorOccurred?.Invoke( error );
		return added;
	}

	private static JsonElement SerializeToJsonElement<T>( T value )
	{
		if( TryToJsonElement( value!, out JsonElement element ) )
			return element;
		if( AppSettingsJsonContext.Default.GetTypeInfo( typeof( T ) ) is not JsonTypeInfo<T> typeInfo )
			throw new NotSupportedException( $"Settings type '{typeof( T ).Name}' is not supported without reflection." );
		return JsonSerializer.SerializeToElement( value, typeInfo );
	}

	private static List<(string Key, JsonElement? Element)> PrepareBatchUpdates( IEnumerable<KeyValuePair<string, object?>> values )
	{
		List<(string Key, JsonElement? Element)> updates = [];
		foreach( var (key, value) in values )
		{
			if( value is null || ( value is JsonElement je && je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ) )
			{
				updates.Add( ( key, null ) );
				continue;
			}
			if( !TryToJsonElement( value, out JsonElement element ) )
				throw new NotSupportedException( $"Settings type '{value.GetType().Name}' is not supported without reflection." );
			updates.Add( ( key, element ) );
		}
		return updates;
	}

	private bool ApplyBatchUpdatesLocked( List<(string Key, JsonElement? Element)> updates )
	{
		bool changed = false;
		for( int i = 0; i < updates.Count; i++ )
		{
			var (key, element) = updates[ i ];
			if( element is { } value )
			{
				_values[ key ] = value;
				changed = true;
			}
			else if( _values.Remove( key ) )
			{
				changed = true;
			}
		}
		return changed;
	}

	private static bool TryToJsonElement( object value, out JsonElement element )
	{
		switch( value )
		{
			case JsonElement je:
				if( je.ValueKind == JsonValueKind.Undefined )
				{
					element = default;
					return false;
				}
				element = je.Clone();
				return true;
			case string s:
				element = JsonSerializer.SerializeToElement( s, AppSettingsJsonContext.Default.String );
				return true;
			case bool b:
				element = JsonSerializer.SerializeToElement( b, AppSettingsJsonContext.Default.Boolean );
				return true;
			case byte u8:
				element = JsonSerializer.SerializeToElement( u8, AppSettingsJsonContext.Default.Byte );
				return true;
			case sbyte i8:
				element = JsonSerializer.SerializeToElement( i8, AppSettingsJsonContext.Default.SByte );
				return true;
			case short i16:
				element = JsonSerializer.SerializeToElement( i16, AppSettingsJsonContext.Default.Int16 );
				return true;
			case ushort u16:
				element = JsonSerializer.SerializeToElement( u16, AppSettingsJsonContext.Default.UInt16 );
				return true;
			case int i32:
				element = JsonSerializer.SerializeToElement( i32, AppSettingsJsonContext.Default.Int32 );
				return true;
			case uint u32:
				element = JsonSerializer.SerializeToElement( u32, AppSettingsJsonContext.Default.UInt32 );
				return true;
			case long i64:
				element = JsonSerializer.SerializeToElement( i64, AppSettingsJsonContext.Default.Int64 );
				return true;
			case ulong u64:
				element = JsonSerializer.SerializeToElement( u64, AppSettingsJsonContext.Default.UInt64 );
				return true;
			case float f when float.IsFinite( f ):
				element = JsonSerializer.SerializeToElement( f, AppSettingsJsonContext.Default.Single );
				return true;
			case double d when double.IsFinite( d ):
				element = JsonSerializer.SerializeToElement( d, AppSettingsJsonContext.Default.Double );
				return true;
			case decimal m:
				element = JsonSerializer.SerializeToElement( m, AppSettingsJsonContext.Default.Decimal );
				return true;
			case DateTime dt:
				element = JsonSerializer.SerializeToElement( dt, AppSettingsJsonContext.Default.DateTime );
				return true;
			case DateTimeOffset dto:
				element = JsonSerializer.SerializeToElement( dto, AppSettingsJsonContext.Default.DateTimeOffset );
				return true;
			case Guid g:
				element = JsonSerializer.SerializeToElement( g, AppSettingsJsonContext.Default.Guid );
				return true;
			case string[] arr:
				element = JsonSerializer.SerializeToElement( arr, AppSettingsJsonContext.Default.StringArray );
				return true;
			case Dictionary<string, string> dict:
				element = JsonSerializer.SerializeToElement( dict, AppSettingsJsonContext.Default.DictionaryStringString );
				return true;
			case AppUiConfig ui:
				element = JsonSerializer.SerializeToElement( ui, AppUiConfigJsonContext.Default.AppUiConfig );
				return true;
			default:
				element = default;
				return false;
		}
	}

	private static Dictionary<string, JsonElement> Load( string filePath, List<string> errors )
	{
		try
		{
			if( File.Exists( filePath ) )
			{
				using FileStream stream = File.OpenRead( filePath );
				using JsonDocument doc = JsonDocument.Parse( stream );
				if( doc.RootElement.ValueKind != JsonValueKind.Object )
					throw new JsonException( $"Expected JSON object root, found {doc.RootElement.ValueKind}." );

				Dictionary<string, JsonElement> values = new( StringComparer.Ordinal );
				foreach( JsonProperty prop in doc.RootElement.EnumerateObject() )
					values[ prop.Name ] = prop.Value.Clone();
				return values;
			}
		}
		catch( Exception e ) when( e is JsonException or IOException or UnauthorizedAccessException )
		{
			string msg = $"Settings file '{filePath}' could not be read ({e.Message}); starting with defaults.";
			errors.Add( msg );
			HackLogger.Warn( "Settings", msg, e );
			TryBackUp( filePath );
		}
		return new( StringComparer.Ordinal );
	}

	private static void TryBackUp( string filePath )
	{
		try
		{
			string backupPath = $"{filePath}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}";
			if( File.Exists( backupPath ) )
				backupPath = $"{backupPath}-{Guid.NewGuid():N}";
			File.Copy( filePath, backupPath, overwrite: false );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}

	private (long Version, byte[] Payload) SnapshotLocked()
	{
		long version = ++_stateVersion;
		string[] keys = new string[ _values.Count ];
		_values.Keys.CopyTo( keys, 0 );
		Array.Sort( keys, StringComparer.Ordinal );
		using MemoryStream memory = new( Math.Max( 256, keys.Length * 48 ) );
		using( Utf8JsonWriter writer = new( memory, IndentedWriterOptions ) )
		{
			writer.WriteStartObject();
			for( int i = 0; i < keys.Length; i++ )
			{
				string k = keys[ i ];
				writer.WritePropertyName( k );
				_values[ k ].WriteTo( writer );
			}
			writer.WriteEndObject();
		}
		return ( version, memory.ToArray() );
	}

	private string? WriteSnapshot( long version, byte[] payload )
	{
		_writeGate.Wait();
		try
		{
			if( _writtenVersion >= version )
				return null;
			string? error = WritePayloadSync( payload );
			if( error is null )
				_writtenVersion = version;
			return error;
		}
		finally
		{
			_writeGate.Release();
		}
	}

	private async Task WriteSnapshotAndNotifyAsync( long version, byte[] payload, CancellationToken token )
	{
		string? error;
		await _writeGate.WaitAsync( token ).ConfigureAwait( false );
		try
		{
			if( _writtenVersion >= version )
				return;
			error = await WritePayloadAsync( payload, token ).ConfigureAwait( false );
			if( error is null )
				_writtenVersion = version;
		}
		finally
		{
			_writeGate.Release();
		}
		if( error is not null )
			ErrorOccurred?.Invoke( error );
	}

	private string? WritePayloadSync( byte[] payload )
	{
		string temp = $"{FilePath}.{Guid.NewGuid():N}.tmp";
		try
		{
			if( Path.GetDirectoryName( FilePath ) is { Length: > 0 } dir )
				Directory.CreateDirectory( dir );
			using( FileStream stream = new( temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096 ) )
			{
				stream.Write( payload );
				stream.Flush( flushToDisk: true );
			}
			File.Move( temp, FilePath, overwrite: true );
			return null;
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			return RecordWriteError( e );
		}
		finally
		{
			CleanupTemp( temp );
		}
	}

	private async Task<string?> WritePayloadAsync( byte[] payload, CancellationToken token )
	{
		string temp = $"{FilePath}.{Guid.NewGuid():N}.tmp";
		try
		{
			if( Path.GetDirectoryName( FilePath ) is { Length: > 0 } dir )
				Directory.CreateDirectory( dir );
			FileStream stream = new( temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true );
			await using( stream.ConfigureAwait( false ) )
			{
				await stream.WriteAsync( payload, token ).ConfigureAwait( false );
				await stream.FlushAsync( token ).ConfigureAwait( false );
			}
			File.Move( temp, FilePath, overwrite: true );
			return null;
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			return RecordWriteError( e );
		}
		finally
		{
			CleanupTemp( temp );
		}
	}

	private string RecordWriteError( Exception e )
	{
		string msg = $"Settings could not be saved to '{FilePath}': {e.Message}";
		lock( _gate )
			_errors.Add( msg );
		HackLogger.Error( "Settings", msg, e );
		return msg;
	}

	private static void CleanupTemp( string temp )
	{
		try
		{
			if( File.Exists( temp ) )
				File.Delete( temp );
		}
		catch
		{
		}
	}
}
