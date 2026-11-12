using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using HackPDM.Abstractions;

namespace HackPDM.Core.Configuration;

/// <summary>
/// Settings kept as a JSON object in one file (settings.json in <see cref="HackPDM.Shared.GlobalData.AppDataPaths.Config"/>),
/// the same on every platform. Replaces <c>ApplicationData.Current.LocalSettings</c>, which needs Windows packaging and
/// only holds WinRT primitive types.
/// <para>Every change is written at once, through a temporary file, so a crash never leaves a half-written file. Thread-safe.</para>
/// </summary>
public sealed class JsonFileSettingsProvider : ISettingsProvider
{
	private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

	private readonly object _gate = new();
	private readonly Dictionary<string, JsonElement> _values;

	/// <summary>Opens the settings file, loading its values.</summary>
	/// <param name="filePath">The JSON file; it need not exist yet and is created on the first change.</param>
	/// <remarks>
	/// An unreadable or invalid file is copied to <c>&lt;file&gt;.unreadable-&lt;timestamp&gt;</c>, a message is written to
	/// standard error, and the provider starts empty; nothing is thrown.
	/// </remarks>
	public JsonFileSettingsProvider( string filePath )
	{
		FilePath = filePath;
		Existed = File.Exists( filePath );
		_values = Load( filePath );
	}

	public string FilePath { get; }

	/// <summary>The file was there when this provider was made (false on the first run).</summary>
	public bool Existed { get; }

	/// <summary>Reads a setting.</summary>
	/// <typeparam name="T">The type to deserialize the stored JSON value to.</typeparam>
	/// <param name="key">The setting name (case-sensitive).</param>
	/// <param name="defaultValue">Returned when the key is missing, null, or stored as an incompatible type.</param>
	/// <returns>The stored value, or <paramref name="defaultValue"/>.</returns>
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
				// saved as another type (e.g. by an older version): treat as not set
				return defaultValue;
			}
		}
	}

	/// <summary>Saves <paramref name="value"/>; null removes the key.</summary>
	/// <typeparam name="T">The value's type; it must be JSON-serializable.</typeparam>
	/// <param name="key">The setting name (case-sensitive).</param>
	/// <param name="value">The value to store.</param>
	/// <remarks>The whole file is rewritten at once (through a <c>.tmp</c> file); a failed save is only reported on standard error.</remarks>
	/// <exception cref="System.NotSupportedException"><paramref name="value"/>'s type cannot be serialized to JSON.</exception>
	public void Set<T>( string key, T value )
	{
		if( value is null )
		{
			Remove( key );
			return;
		}
		lock( _gate )
		{
			_values[ key ] = JsonSerializer.SerializeToElement( value );
			Save();
		}
	}

	/// <summary>Tells whether a setting is stored.</summary>
	/// <param name="key">The setting name (case-sensitive).</param>
	/// <returns><see langword="true"/> when the key exists.</returns>
	public bool Contains( string key )
	{
		lock( _gate )
			return _values.ContainsKey( key );
	}

	/// <summary>Deletes a setting and saves the file if it existed.</summary>
	/// <param name="key">The setting name (case-sensitive).</param>
	public void Remove( string key )
	{
		lock( _gate )
		{
			if( _values.Remove( key ) )
				Save();
		}
	}

	/// <summary>Adds the values whose keys are not set yet (bringing settings over from an older store).</summary>
	/// <param name="values">The key/value pairs; null values, existing keys and values JSON can't hold are skipped.</param>
	/// <returns>How many values were added; the file is saved once when any were.</returns>
	public int ImportMissing( IEnumerable<KeyValuePair<string, object?>> values )
	{
		lock( _gate )
		{
			int added = 0;
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
					// a value JSON cannot hold: leave it behind
				}
			}
			if( added > 0 )
				Save();
			return added;
		}
	}

	private static Dictionary<string, JsonElement> Load( string filePath )
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
			// unreadable: keep a copy for the user instead of overwriting it with the next change
			Console.Error.WriteLine( $"settings file {filePath} could not be read ({e.Message}); starting with defaults" );
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

	// under _gate
	private void Save()
	{
		try
		{
			if( Path.GetDirectoryName( FilePath ) is { Length: > 0 } dir )
				Directory.CreateDirectory( dir );
			string temp = $"{FilePath}.tmp";
			var sorted = _values.OrderBy( v => v.Key, StringComparer.Ordinal ).ToDictionary( v => v.Key, v => v.Value );
			File.WriteAllText( temp, JsonSerializer.Serialize( sorted, WriteOptions ) );
			File.Move( temp, FilePath, overwrite: true );
		}
		catch( Exception e ) when( e is IOException or UnauthorizedAccessException )
		{
			Console.Error.WriteLine( $"settings could not be saved to {FilePath}: {e.Message}" );
		}
	}
}
