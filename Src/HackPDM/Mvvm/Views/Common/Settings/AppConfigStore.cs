using System;
using System.IO;
using System.Text.Json;


namespace HackPDM.Mvvm.Views.Common.Settings;

/// <summary>
/// Reading and writing <see cref="GlobalSerializedData"/> as AppConfig.json in the user's documents
/// folder. Needs no services, so it works from the App constructor on.
/// </summary>
public static class AppConfigStore
{
	public static string FilePath { get; } = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Personal ), "AppConfig.json" );

	public static JsonSerializerOptions Options { get; } = new()
	{
		Converters =
		{
			new CustomSerializers.ColorSerializer(),
			new CustomSerializers.Vector4Serializer(),
		},
	};

	/// <summary>Reads the saved settings.</summary>
	/// <param name="path">The settings file; <see cref="FilePath"/> when <see langword="null"/>.</param>
	/// <returns>The saved settings; defaults when the file doesn't exist or holds the JSON literal <c>null</c>.</returns>
	/// <exception cref="System.Text.Json.JsonException">The file is not valid settings JSON.</exception>
	/// <exception cref="System.IO.IOException">The file exists but cannot be read.</exception>
	public static GlobalSerializedData Load( string? path = null )
	{
		path ??= FilePath;
		return File.Exists( path ) ? Parse( File.ReadAllText( path ) ) : new GlobalSerializedData();
	}

	/// <summary>Settings from JSON; defaults when it holds none.</summary>
	/// <param name="json">The settings document.</param>
	/// <returns>The parsed settings with <see cref="GlobalSerializedData.IsLoadedFromFile"/> set; new defaults when <paramref name="json"/> is the literal <c>null</c>.</returns>
	/// <exception cref="System.Text.Json.JsonException"><paramref name="json"/> is not valid settings JSON.</exception>
	public static GlobalSerializedData Parse( string json )
	{
		GlobalSerializedData? data = JsonSerializer.Deserialize<GlobalSerializedData>( json, Options );
		if( data is null )
			return new GlobalSerializedData();
		data.IsLoadedFromFile = true;
		return data;
	}

	/// <summary>Converts settings to JSON using the app's color and vector converters.</summary>
	/// <param name="data">The settings to convert.</param>
	/// <returns>The settings as compact JSON.</returns>
	public static string Serialize( GlobalSerializedData data ) => JsonSerializer.Serialize( data, Options );

	/// <summary>Writes settings to disk, replacing the file.</summary>
	/// <param name="data">The settings to save.</param>
	/// <param name="path">The settings file; <see cref="FilePath"/> when <see langword="null"/>.</param>
	/// <exception cref="System.IO.IOException">The file cannot be written.</exception>
	/// <exception cref="System.UnauthorizedAccessException">The path is not writable by this user.</exception>
	public static void Save( GlobalSerializedData data, string? path = null )
		=> File.WriteAllText( path ?? FilePath, Serialize( data ) );
}
