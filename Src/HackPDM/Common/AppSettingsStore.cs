using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

using HackPDM.Core.Configuration;
using HackPDM.Core.Theming;

using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace HackPDM.Common;

/// <summary>Opens unified settings.json in <see cref="AppDataPaths.Config"/>, importing legacy packaged settings and AppConfig.json on first run.</summary>
public static class AppSettingsStore
{
	public const string UiConfigKey = "UiConfig";

	public static string FilePath => Path.Combine( AppDataPaths.Config, "settings.json" );

	/// <summary>Legacy AppConfig.json location used for one-time migration into <see cref="FilePath"/>.</summary>
	private static string LegacyAppConfigPath => Path.Combine(
		AppDataPaths.IsConfigOverridden
			? AppDataPaths.Config
			: Environment.GetFolderPath( Environment.SpecialFolder.Personal ),
		"AppConfig.json" );

	public static JsonFileSettingsProvider Open()
	{
		JsonFileSettingsProvider settings = new( FilePath );
		if( !settings.Existed && !AppDataPaths.IsConfigOverridden )
			ImportPackagedSettings( settings );
		if( !settings.Contains( UiConfigKey ) )
			TryImportLegacyAppConfig( settings );
		return settings;
	}

	internal static AppUiConfig LoadUiConfig()
	{
		JsonElement raw = AppComposition.Settings.Get<JsonElement>( UiConfigKey );
		if( raw.ValueKind == JsonValueKind.Object )
		{
			try
			{
				AppUiConfig? parsed = raw.Deserialize<AppUiConfig>( AppUiConfig.JsonOptions );
				if( parsed is not null )
				{
					parsed.IsLoadedFromFile = true;
					return parsed;
				}
			}
			catch( Exception e ) when( e is JsonException or NotSupportedException )
			{
				HackLogger.Warn( "Settings", $"Could not deserialize {UiConfigKey}: {e.Message}", e );
			}
		}

		// Fallback if read before AppComposition.Settings was initialized (e.g., App constructor)
		if( File.Exists( FilePath ) )
		{
			try
			{
				using JsonDocument doc = JsonDocument.Parse( File.ReadAllText( FilePath ) );
				if( doc.RootElement.TryGetProperty( UiConfigKey, out JsonElement uiEl )
					&& uiEl.Deserialize<AppUiConfig>( AppUiConfig.JsonOptions ) is { } parsed )
				{
					parsed.IsLoadedFromFile = true;
					return parsed;
				}
			}
			catch( Exception e ) when( e is JsonException or IOException or UnauthorizedAccessException )
			{
			}
		}

		if( File.Exists( LegacyAppConfigPath ) )
		{
			try
			{
				if( JsonSerializer.Deserialize<AppUiConfig>( File.ReadAllText( LegacyAppConfigPath ), AppUiConfig.JsonOptions ) is { } legacy )
				{
					legacy.IsLoadedFromFile = true;
					return legacy;
				}
			}
			catch( Exception e ) when( e is JsonException or IOException or UnauthorizedAccessException )
			{
			}
		}

		return new AppUiConfig();
	}

	internal static void SaveUiConfig( AppUiConfig config )
	{
		JsonElement element = JsonSerializer.SerializeToElement( config, AppUiConfig.JsonOptions );
		AppComposition.Settings.Set( UiConfigKey, element );
		config.IsLoadedFromFile = true;
	}

	private static void TryImportLegacyAppConfig( JsonFileSettingsProvider settings )
	{
		try
		{
			if( !File.Exists( LegacyAppConfigPath ) )
				return;
			AppUiConfig? legacy = JsonSerializer.Deserialize<AppUiConfig>( File.ReadAllText( LegacyAppConfigPath ), AppUiConfig.JsonOptions );
			if( legacy is null )
				return;
			settings.Set( UiConfigKey, JsonSerializer.SerializeToElement( legacy, AppUiConfig.JsonOptions ) );
			HackLogger.Info( "Settings", $"Migrated legacy UI configuration from '{LegacyAppConfigPath}' into '{settings.FilePath}'." );
		}
		catch( Exception e ) when( e is JsonException or IOException or UnauthorizedAccessException )
		{
			HackLogger.Warn( "Settings", $"Legacy AppConfig.json could not be migrated: {e.Message}", e );
		}
	}

	private static void ImportPackagedSettings( JsonFileSettingsProvider settings )
	{
		try
		{
			List<KeyValuePair<string, object?>> values = Windows.Storage.ApplicationData.Current.LocalSettings.Values
				.Where( v => !v.Key.StartsWith( "__UNO__", StringComparison.Ordinal ) && !v.Key.StartsWith( "__Uno.", StringComparison.Ordinal ) )
				.Select( v => new KeyValuePair<string, object?>( v.Key, UnwrapLocalSettingValue( v.Value ) ) )
				.ToList();
			int imported = settings.ImportMissing( values );
			if( imported > 0 )
				HackLogger.Info( "Settings", $"Imported {imported} value(s) from packaged app settings to '{settings.FilePath}'" );
		}
		catch( Exception e )
		{
			HackLogger.Debug( "Settings", $"No packaged settings to import: {e.Message}" );
		}
	}

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

/// <summary>Process-wide access to the unified UI configuration stored in <c>settings.json</c>.</summary>
public static class UIStorage
{
	private static AppUiConfig? _globalData;

	public static AppUiConfig GlobalData
	{
		get => _globalData ??= AppSettingsStore.LoadUiConfig();
		set
		{
			_globalData = value;
			StatusBrushes.InvalidateCache();
		}
	}

	public static ColorSettingsData ColorData => GlobalData.EColors;
	public static OdooEntryColumns ColumnsInfo => GlobalData.ColumnsSettings;
	public static Dictionary<string, OdooEntryColumnID?> Columns => GlobalData.ColumnsSettings.Columns;
	public static WorkspaceLayoutSettings WorkspaceLayout => GlobalData.WorkspaceLayout;

	public static void Save() => AppSettingsStore.SaveUiConfig( GlobalData );

	public static void SaveSafe()
	{
		try { Save(); }
		catch( Exception e ) { HackLogger.Warn( "Settings", $"Could not save UI config: {e.Message}", e ); }
	}

	public static void Reload() => GlobalData = AppSettingsStore.LoadUiConfig();

	public static Color ToColor( ArgbColor c ) => Color.FromArgb( c.A, c.R, c.G, c.B );
	public static Color ToColor( (byte A, byte R, byte G, byte B) argb ) => Color.FromArgb( argb.A, argb.R, argb.G, argb.B );
	public static SolidColorBrush ToBrush( ArgbColor c ) => new( ToColor( c ) );
}
