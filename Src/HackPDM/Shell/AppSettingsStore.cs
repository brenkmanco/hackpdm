using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using HackPDM.Core.Configuration;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Shell;

/// <summary>
/// Where the app's settings (server address, port, database, ...) are kept: settings.json in
/// <see cref="AppDataPaths.Config"/>, on every platform and without Windows packaging.
/// </summary>
public static class AppSettingsStore
{
	/// <summary>The full path of settings.json.</summary>
	public static string FilePath => Path.Combine( AppDataPaths.Config, "settings.json" );

	/// <summary>The settings file; on its first use, the values earlier versions kept in the packaged app's LocalSettings are brought over.</summary>
	/// <returns>A settings provider backed by <see cref="FilePath"/>.</returns>
	/// <remarks>The import only happens when the file did not exist yet and the config folder was not overridden; it never
	/// overwrites values already in the file, and failures are only written to the debug output.</remarks>
	public static JsonFileSettingsProvider Open()
	{
		JsonFileSettingsProvider settings = new( FilePath );
		if( !settings.Existed && !AppDataPaths.IsConfigOverridden )
			ImportPackagedSettings( settings );
		return settings;
	}

	private static void ImportPackagedSettings( JsonFileSettingsProvider settings )
	{
		try
		{
			// throws when the app runs unpackaged on Windows: then earlier versions could not have saved anything there
			var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values
				.Select( v => new KeyValuePair<string, object?>( v.Key, v.Value ) )
				.ToList();
			int imported = settings.ImportMissing( values );
			if( imported > 0 )
				Console.Error.WriteLine( $"settings: brought {imported} value(s) over from the packaged app's settings to {settings.FilePath}" );
		}
		catch( Exception e )
		{
			System.Diagnostics.Debug.WriteLine( $"settings: no packaged settings to bring over ({e.Message})" );
		}
	}
}
