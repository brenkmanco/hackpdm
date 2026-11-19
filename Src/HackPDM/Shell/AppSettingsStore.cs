using System;
using System.Collections.Generic;
using System.Globalization;
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
				.Where( v => !IsUnoInternalKey( v.Key ) )
				.Select( v => new KeyValuePair<string, object?>( v.Key, UnwrapLocalSettingValue( v.Value ) ) )
				.ToList();
			int imported = settings.ImportMissing( values );
			if( imported > 0 )
				HackPDM.Core.Logging.HackLogger.Info( "Settings", $"Imported {imported} value(s) from packaged app settings to '{settings.FilePath}'" );
		}
		catch( Exception e )
		{
			HackPDM.Core.Logging.HackLogger.Debug( "Settings", $"No packaged settings to import: {e.Message}" );
		}
	}

	private static bool IsUnoInternalKey( string key )
		=> key.StartsWith( "__UNO__", StringComparison.Ordinal ) || key.StartsWith( "__Uno.", StringComparison.Ordinal );

	/// <summary>
	/// Uno Platform's non-Windows LocalSettings enumerator yields raw "CLRType:value" strings (e.g. "System.String:localhost").
	/// Unwraps known primitive prefixes back to CLR values so settings.json stores plain JSON values.
	/// </summary>
	internal static object? UnwrapLocalSettingValue( object? value )
	{
		if( value is not string raw )
			return value;

		int sep = raw.IndexOf( ':' );
		if( sep <= 0 )
			return raw;

		ReadOnlySpan<char> typeName = raw.AsSpan( 0, sep );
		ReadOnlySpan<char> payload = raw.AsSpan( sep + 1 );
		return typeName switch
		{
			"System.String" => payload.ToString(),
			"System.Boolean" when bool.TryParse( payload, out bool b ) => b,
			"System.Int32" when int.TryParse( payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i ) => i,
			"System.Int64" when long.TryParse( payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l ) => l,
			"System.Double" when double.TryParse( payload, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double d ) => d,
			"System.Single" when float.TryParse( payload, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float f ) => f,
			"System.Decimal" when decimal.TryParse( payload, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal m ) => m,
			_ => raw,
		};
	}
}
