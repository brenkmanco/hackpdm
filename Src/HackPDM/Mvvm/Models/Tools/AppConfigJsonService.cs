using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using HackPDM.Mvvm.Views.Common.Settings;

namespace HackPDM.Mvvm.Models.Tools;

/// <summary>AppConfig.json as raw JSON, for the JSON editor.</summary>
public static class AppConfigJsonService
{
	private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

	/// <summary>Shown when there is no settings file yet.</summary>
	private const string SampleJson = """
		{
		  "appName": "Automation Engine",
		  "maxRetries": 5,
		  "isProduction": true,
		  "dbConfig": {
		    "host": "localhost",
		    "port": 5432
		  },
		  "allowedPorts": [80, 443, 8080]
		}
		""";

	/// <summary>Reads the saved settings file as an editable JSON tree.</summary>
	/// <returns>The file's top-level object; an unrelated sample object when there is no file; an empty object when the top level is not an object.</returns>
	/// <exception cref="System.Text.Json.JsonException">The file is not valid JSON.</exception>
	/// <exception cref="System.IO.IOException">The file exists but cannot be read.</exception>
	public static JsonObject Load()
	{
		string path = AppConfigStore.FilePath;
		string json = File.Exists( path ) ? File.ReadAllText( path ) : SampleJson;
		return JsonNode.Parse( json ) as JsonObject ?? [];
	}

	/// <summary>Writes a JSON node as indented text.</summary>
	/// <param name="node">The node to format.</param>
	/// <returns>The node as indented JSON.</returns>
	public static string Format( JsonNode node ) => node.ToJsonString( Indented );

	/// <summary>Saves the JSON as the app's settings (fields the app doesn't know are dropped).</summary>
	/// <param name="json">The full settings document.</param>
	/// <remarks>Round-trips through <see cref="HackPDM.Mvvm.Views.Common.Settings.GlobalSerializedData"/> before writing,
	/// so the in-memory settings are not reloaded by this call.</remarks>
	/// <exception cref="System.Text.Json.JsonException"><paramref name="json"/> is not valid settings JSON.</exception>
	/// <exception cref="System.IO.IOException">The settings file cannot be written.</exception>
	public static void Save( string json ) => AppConfigStore.Save( AppConfigStore.Parse( json ) );
}
