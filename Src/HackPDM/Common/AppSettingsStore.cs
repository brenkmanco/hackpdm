using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core.Configuration;

namespace HackPDM.Common;

/// <summary>Opens unified settings.json in <see cref="AppDataPaths.Config"/>, importing legacy packaged settings and AppConfig.json on first run.</summary>
public static class AppSettingsStore
{
	public const string UiConfigKey = "UiConfig";

	private static readonly Lock SaveGate = new();
	private static readonly Lock WriteLock = new();
	private static JsonElement? _pendingUiConfig;
	private static long _latestSaveSeq;
	private static int _saveScheduled;

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
		lock( SaveGate )
		{
			_latestSaveSeq++;
			_pendingUiConfig = null;
			config.IsLoadedFromFile = true;
		}
		lock( WriteLock )
			AppComposition.Settings.Set( UiConfigKey, element );
	}

	internal static void QueueSaveUiConfig( AppUiConfig config )
	{
		JsonElement element = JsonSerializer.SerializeToElement( config, AppUiConfig.JsonOptions );
		bool startWorker;
		lock( SaveGate )
		{
			_latestSaveSeq++;
			_pendingUiConfig = element;
			config.IsLoadedFromFile = true;
			startWorker = Interlocked.CompareExchange( ref _saveScheduled, 1, 0 ) == 0;
		}
		if( startWorker )
			_ = Task.Run( FlushQueuedUiConfigAsync );
	}

	private static async Task FlushQueuedUiConfigAsync()
	{
		try
		{
			await Task.Delay( 50 ).ConfigureAwait( false );
			while( true )
			{
				JsonElement element;
				long seq;
				lock( SaveGate )
				{
					if( _pendingUiConfig is not { } pending )
					{
						Volatile.Write( ref _saveScheduled, 0 );
						return;
					}
					element = pending;
					seq = _latestSaveSeq;
					_pendingUiConfig = null;
				}

				try
				{
					lock( WriteLock )
					{
						if( seq >= Volatile.Read( ref _latestSaveSeq ) )
							AppComposition.Settings.Set( UiConfigKey, element );
					}
				}
				catch( Exception e )
				{
					HackLogger.Warn( "Settings", $"Could not save UI config: {e.Message}", e );
				}
			}
		}
		catch( Exception e )
		{
			Volatile.Write( ref _saveScheduled, 0 );
			HackLogger.Warn( "Settings", $"Could not flush queued UI config: {e.Message}", e );
		}
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
		try { AppSettingsStore.QueueSaveUiConfig( GlobalData ); }
		catch( Exception e ) { HackLogger.Warn( "Settings", $"Could not save UI config: {e.Message}", e ); }
	}
}
