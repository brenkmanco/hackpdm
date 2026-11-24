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
}

/// <summary>Thread-safe JSON file settings store written atomically via a temp file.</summary>
public sealed class JsonFileSettingsProvider : ISettingsProvider
{
	private static readonly JsonWriterOptions IndentedWriterOptions = new() { Indented = true };

	private readonly object _gate = new();
	private readonly Dictionary<string, JsonElement> _values;
	private readonly List<string> _errors = [];

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
		if( !TryToJsonElement( value, out JsonElement element ) )
		{
			if( AppSettingsJsonContext.Default.GetTypeInfo( typeof( T ) ) is not JsonTypeInfo<T> typeInfo )
				throw new NotSupportedException( $"Settings type '{typeof( T ).Name}' is not supported without reflection." );
			element = JsonSerializer.SerializeToElement( value, typeInfo );
		}
		string? error;
		lock( _gate )
		{
			_values[ key ] = element;
			error = Save();
		}
		if( error is not null )
			ErrorOccurred?.Invoke( error );
	}

	public bool Contains( string key )
	{
		lock( _gate )
			return _values.ContainsKey( key );
	}

	public void Remove( string key )
	{
		string? error = null;
		lock( _gate )
		{
			if( _values.Remove( key ) )
				error = Save();
		}
		if( error is not null )
			ErrorOccurred?.Invoke( error );
	}

	public int ImportMissing( IEnumerable<KeyValuePair<string, object?>> values )
	{
		int added = 0;
		string? error = null;
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
				error = Save();
		}
		if( error is not null )
			ErrorOccurred?.Invoke( error );
		return added;
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

	private string? Save()
	{
		string temp = $"{FilePath}.{Guid.NewGuid():N}.tmp";
		try
		{
			if( Path.GetDirectoryName( FilePath ) is { Length: > 0 } dir )
				Directory.CreateDirectory( dir );
			string[] keys = new string[ _values.Count ];
			_values.Keys.CopyTo( keys, 0 );
			Array.Sort( keys, StringComparer.Ordinal );
			using( FileStream stream = new( temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096 ) )
			{
				using( Utf8JsonWriter writer = new( stream, IndentedWriterOptions ) )
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
				stream.Flush( flushToDisk: true );
			}
			File.Move( temp, FilePath, overwrite: true );
			return null;
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			string msg = $"Settings could not be saved to '{FilePath}': {e.Message}";
			_errors.Add( msg );
			HackLogger.Error( "Settings", msg, e );
			return msg;
		}
		finally
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
}
