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

	/// <summary>The saved settings; defaults when the file doesn't exist.</summary>
	public static GlobalSerializedData Load( string? path = null )
	{
		path ??= FilePath;
		return File.Exists( path ) ? Parse( File.ReadAllText( path ) ) : new GlobalSerializedData();
	}

	/// <summary>Settings from JSON; defaults when it holds none.</summary>
	public static GlobalSerializedData Parse( string json )
	{
		GlobalSerializedData? data = JsonSerializer.Deserialize<GlobalSerializedData>( json, Options );
		if( data is null )
			return new GlobalSerializedData();
		data.IsLoadedFromFile = true;
		return data;
	}

	public static string Serialize( GlobalSerializedData data ) => JsonSerializer.Serialize( data, Options );

	public static void Save( GlobalSerializedData data, string? path = null )
		=> File.WriteAllText( path ?? FilePath, Serialize( data ) );
}
