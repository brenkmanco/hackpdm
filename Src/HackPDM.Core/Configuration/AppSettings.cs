using System.Text.Json;

namespace HackPDM.Core.Configuration;

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
	private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

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
			try
			{
				return value.Deserialize<T>();
			}
			catch( Exception e ) when( e is JsonException or NotSupportedException or InvalidOperationException )
			{
				return defaultValue;
			}
		}
	}

	public void Set<T>( string key, T value )
	{
		if( value is null )
		{
			Remove( key );
			return;
		}
		string? error;
		lock( _gate )
		{
			_values[ key ] = JsonSerializer.SerializeToElement( value );
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
				try
				{
					_values[ key ] = JsonSerializer.SerializeToElement( value, value.GetType() );
					added++;
				}
				catch( Exception e ) when( e is NotSupportedException or JsonException )
				{
				}
			}
			if( added > 0 )
				error = Save();
		}
		if( error is not null )
			ErrorOccurred?.Invoke( error );
		return added;
	}

	private static Dictionary<string, JsonElement> Load( string filePath, List<string> errors )
	{
		try
		{
			if( File.Exists( filePath ) )
			{
				using FileStream stream = File.OpenRead( filePath );
				if( JsonSerializer.Deserialize<Dictionary<string, JsonElement>>( stream ) is { } values )
					return new( values, StringComparer.Ordinal );
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
			File.Copy( filePath, $"{filePath}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: false );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
		}
	}

	private string? Save()
	{
		try
		{
			if( Path.GetDirectoryName( FilePath ) is { Length: > 0 } dir )
				Directory.CreateDirectory( dir );
			string temp = $"{FilePath}.tmp";
			var sorted = _values
				.OrderBy( v => v.Key, StringComparer.Ordinal )
				.ToDictionary( v => v.Key, v => v.Value );
			File.WriteAllText( temp, JsonSerializer.Serialize( sorted, WriteOptions ) );
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
	}
}
