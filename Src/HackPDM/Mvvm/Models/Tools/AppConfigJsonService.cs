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

	/// <summary>The saved settings file's top-level object, or a sample when there is no file.</summary>
	public static JsonObject Load()
	{
		string path = AppConfigStore.FilePath;
		string json = File.Exists( path ) ? File.ReadAllText( path ) : SampleJson;
		return JsonNode.Parse( json ) as JsonObject ?? [];
	}

	public static string Format( JsonNode node ) => node.ToJsonString( Indented );

	/// <summary>Saves the JSON as the app's settings (fields the app doesn't know are dropped).</summary>
	public static void Save( string json ) => AppConfigStore.Save( AppConfigStore.Parse( json ) );
}
