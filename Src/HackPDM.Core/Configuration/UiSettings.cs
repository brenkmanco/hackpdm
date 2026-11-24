using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.Core.Theming;

namespace HackPDM.Core.Configuration;

/// <summary>Four-component vector (e.g., window bounds as x, y, width, height).</summary>
public struct Vector4<T>( T x, T y, T z, T w )
{
	public T x { get; set; } = x;
	public T y { get; set; } = y;
	public T z { get; set; } = z;
	public T w { get; set; } = w;
	public readonly int Length => 4;

	public T this[ int index ]
	{
		readonly get => index switch
		{
			0 => x,
			1 => y,
			2 => z,
			3 => w,
			_ => throw new IndexOutOfRangeException(),
		};
		set => _ = index switch
		{
			0 => x = value,
			1 => y = value,
			2 => z = value,
			3 => w = value,
			_ => throw new IndexOutOfRangeException(),
		};
	}

	public static implicit operator Vector4<T>( (T, T, T, T) v ) => new( v.Item1, v.Item2, v.Item3, v.Item4 );
	public override readonly string ToString() => $"({x}, {y}, {z}, {w})";
}

/// <summary>JSON converter reading/writing <see cref="Vector4{Int32}"/> as "[x,y,z,w]" or object.</summary>
public sealed class Vector4IntJsonConverter : JsonConverter<Vector4<int>>
{
	public override Vector4<int> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		if( reader.TokenType == JsonTokenType.String )
		{
			string? raw = reader.GetString();
			if( !string.IsNullOrWhiteSpace( raw ) )
			{
				ReadOnlySpan<char> span = raw.AsSpan().Trim( "()[] " );
				Span<int> coords = stackalloc int[ 4 ];
				int count = 0;
				bool complete = false;
				while( count < 4 )
				{
					int comma = span.IndexOf( ',' );
					ReadOnlySpan<char> part = ( comma >= 0 ? span[ ..comma ] : span ).Trim();
					if( !int.TryParse( part, NumberStyles.Integer, CultureInfo.InvariantCulture, out coords[ count ] ) )
						break;
					count++;
					if( comma < 0 )
					{
						complete = count == 4;
						break;
					}
					span = span[ ( comma + 1 ).. ];
				}
				if( complete )
					return new( coords[ 0 ], coords[ 1 ], coords[ 2 ], coords[ 3 ] );
			}
			return default;
		}

		if( reader.TokenType == JsonTokenType.StartObject )
		{
			int x = 0, y = 0, z = 0, w = 0;
			while( reader.Read() && reader.TokenType != JsonTokenType.EndObject )
			{
				if( reader.TokenType != JsonTokenType.PropertyName )
					continue;
				string? prop = reader.GetString();
				if( !reader.Read() )
					break;
				int val = 0;
				if( reader.TokenType == JsonTokenType.Number )
					reader.TryGetInt32( out val );
				else
					reader.Skip();
				switch( prop?.ToLowerInvariant() )
				{
					case "x": x = val; break;
					case "y": y = val; break;
					case "z": z = val; break;
					case "w": w = val; break;
				}
			}
			return new( x, y, z, w );
		}

		reader.Skip();
		return default;
	}

	public override void Write( Utf8JsonWriter writer, Vector4<int> value, JsonSerializerOptions options )
		=> writer.WriteStringValue( $"({value.x}, {value.y}, {value.z}, {value.w})" );
}

/// <summary>JSON converter reading/writing <see cref="ArgbColor"/> from "#AARRGGBB", named colors, or legacy {A,R,G,B} objects.</summary>
public sealed class ArgbColorJsonConverter : JsonConverter<ArgbColor>
{
	public override ArgbColor Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		if( reader.TokenType == JsonTokenType.String )
		{
			string? raw = reader.GetString();
			if( ArgbColor.TryParse( raw, out ArgbColor parsed ) )
				return parsed;
			if( ColorNames.TryParse( raw, out uint argb ) )
				return ArgbColor.FromPacked( argb );
			return default;
		}

		if( reader.TokenType == JsonTokenType.StartObject )
		{
			byte a = 255, r = 0, g = 0, b = 0;
			while( reader.Read() && reader.TokenType != JsonTokenType.EndObject )
			{
				if( reader.TokenType != JsonTokenType.PropertyName )
					continue;
				string? prop = reader.GetString();
				if( !reader.Read() )
					break;
				byte val = 0;
				if( reader.TokenType == JsonTokenType.Number )
					reader.TryGetByte( out val );
				else
					reader.Skip();
				switch( prop )
				{
					case "A" or "a": a = val; break;
					case "R" or "r": r = val; break;
					case "G" or "g": g = val; break;
					case "B" or "b": b = val; break;
				}
			}
			return new( a, r, g, b );
		}

		reader.Skip();
		return default;
	}

	public override void Write( Utf8JsonWriter writer, ArgbColor value, JsonSerializerOptions options )
		=> writer.WriteStringValue( value.ToString() );
}

internal sealed class EntrySortDirectionJsonConverter() : SafeEnumJsonConverter<EntrySortDirection>( EntrySortDirection.Ascending );
internal sealed class WindowKindJsonConverter() : SafeEnumJsonConverter<WindowKind>( WindowKind.Overlapped );
internal sealed class FolderDockSideJsonConverter() : SafeEnumJsonConverter<FolderDockSide>( FolderDockSide.Left );
internal sealed class DetailsDockPositionJsonConverter() : SafeEnumJsonConverter<DetailsDockPosition>( DetailsDockPosition.Bottom );
internal sealed class PreviewDockPlacementJsonConverter() : SafeEnumJsonConverter<PreviewDockPlacement>( PreviewDockPlacement.AfterDetails );
internal sealed class WorkspaceLayoutPresetJsonConverter() : SafeEnumJsonConverter<WorkspaceLayoutPreset>( WorkspaceLayoutPreset.Balanced );

[JsonConverter( typeof( EntrySortDirectionJsonConverter ) )]
public enum EntrySortDirection
{
	Ascending,
	Descending,
}

[JsonConverter( typeof( WindowKindJsonConverter ) )]
public enum WindowKind
{
	Default = 0,
	CompactOverlay = 1,
	FullScreen = 2,
	Overlapped = 3,
}

[JsonConverter( typeof( FolderDockSideJsonConverter ) )]
public enum FolderDockSide { Left, Right }

[JsonConverter( typeof( DetailsDockPositionJsonConverter ) )]
public enum DetailsDockPosition { Bottom, Right }

[JsonConverter( typeof( PreviewDockPlacementJsonConverter ) )]
public enum PreviewDockPlacement { AfterDetails, BeforeDetails }

[JsonConverter( typeof( WorkspaceLayoutPresetJsonConverter ) )]
public enum WorkspaceLayoutPreset { Balanced, RightInspector, RightSidebar, FocusEntries }

/// <summary>Canonical keys for <see cref="WindowSettingsData.PresetWindowConfig"/>.</summary>
public static class WindowPresetKeys
{
	public const string MainWindow = nameof( MainWindow );
	public const string ProfileManager = nameof( NavigatePageMenu.ProfileManager );
	public const string HackFileManager = nameof( NavigatePageMenu.HackFileManager );
	public const string DependencyGraph = nameof( NavigatePageMenu.DependencyGraph );
	public const string OdooSettings = nameof( OdooSettings );
	public const string HackSettings = nameof( HackSettings );
	public const string OdooSettingsView = nameof( OdooSettingsView );
	public const string HackSettingsView = nameof( HackSettingsView );
	public const string ConfigSettings = nameof( ConfigSettings );
	public const string NotLoggedIn = nameof( NotLoggedIn );
	public const string MessageBox = nameof( MessageBox );
	public const string StatusDialogView = nameof( StatusDialogView );
	public const string StatusDialogViewMinimal = nameof( StatusDialogViewMinimal );
	public const string SearchOdooView = nameof( SearchOdooView );
	public const string SearchOdooViewMinimal = nameof( SearchOdooViewMinimal );
	public const string OdooFileTypeManagerView = nameof( OdooFileTypeManagerView );
	public const string ApplicationSettingsView = nameof( ApplicationSettingsView );
	public const string AppSettings = nameof( AppSettings );
	public const string PipWindow = nameof( PipWindow );
}

[Bindable( true )]
public sealed class GroupByField
{
	public string FieldName { get; set => field = value ?? string.Empty; } = string.Empty;
	public EntrySortDirection SortDirection
	{
		get;
		set => field = Enum.IsDefined( value ) ? value : EntrySortDirection.Ascending;
	} = EntrySortDirection.Ascending;
}

[Bindable( true )]
public sealed class OdooEntryColumn
{
	public int DisplayIndex { get; set; } = -1;
	public string Header { get; set => field = value ?? string.Empty; } = string.Empty;
	public double Width { get; set; } = 100;
	public bool AutoSize { get; set; }
}

[Bindable( true )]
public sealed class OdooEntryColumnID
{
	public string BindingPath { get; set => field = value ?? string.Empty; } = string.Empty;
	public OdooEntryColumn Column { get; set => field = value ?? new(); } = new();
}

[Bindable( true )]
public sealed class OdooEntryColumns
{
	public double RowHeight { get; set; } = 26;
	public double FontSize { get; set; } = 11;
	public bool GroupingEnabled { get; set; } = true;
	public bool GroupCollapsed { get; set; }
	public GroupByField GroupBy { get; set => field = value ?? DefaultGroupBy(); } = DefaultGroupBy();

	public Dictionary<string, OdooEntryColumnID?> Columns { get; set => field = value ?? DefaultColumns(); } = DefaultColumns();

	private static GroupByField DefaultGroupBy()
		=> new() { FieldName = nameof( EntryRow.Status ), SortDirection = EntrySortDirection.Ascending };

	private static Dictionary<string, OdooEntryColumnID?> DefaultColumns() => new()
	{
		[ nameof( EntryRow.Name ) ] = Column( nameof( EntryRow.Name ), 0, "Name", 200 ),
		[ nameof( EntryRow.Type ) ] = Column( nameof( EntryRow.Type ), 1, "Type", 70 ),
		[ nameof( EntryRow.Size ) ] = Column( nameof( EntryRow.Size ), 2, "Size", 85 ),
		[ nameof( EntryRow.Status ) ] = Column( nameof( EntryRow.Status ), 3, "Status", 75 ),
		[ nameof( EntryRow.Checkout ) ] = Column( nameof( EntryRow.Checkout ), 4, "Checkout", 105 ),
		[ nameof( EntryRow.LocalDate ) ] = Column( nameof( EntryRow.LocalDate ), 5, "Local Date", 150 ),
		[ nameof( EntryRow.RemoteDate ) ] = Column( nameof( EntryRow.RemoteDate ), 6, "Remote Date", 150 ),
		[ nameof( EntryRow.FullName ) ] = Column( nameof( EntryRow.FullName ), 7, "Full Name", 240, autoSize: true ),
	};

	private static OdooEntryColumnID Column( string path, int index, string header, double width = 100, bool autoSize = false )
		=> new() { BindingPath = path, Column = new() { DisplayIndex = index, Header = header, Width = width, AutoSize = autoSize } };
}

public sealed class ColorSettingsData
{
	public ArgbColor White { get; set; } = new( 255, 255, 255, 255 );
	public ArgbColor Black { get; set; } = new( 255, 0, 0, 0 );
	public ArgbColor LightGray { get; set; } = new( 255, 211, 211, 211 );
	public ArgbColor Gray { get; set; } = new( 255, 128, 128, 128 );
	public ArgbColor MustardYellow { get; set; } = new( 255, 150, 150, 0 );
	public ArgbColor DarkGray { get; set; } = new( 255, 64, 64, 64 );
	public ArgbColor DarkRed { get; set; } = new( 255, 139, 0, 0 );
	public ArgbColor DarkBlue { get; set; } = new( 255, 0, 0, 139 );
	public ArgbColor DarkOliveGreen { get; set; } = new( 255, 85, 107, 47 );
	public ArgbColor OrangeEntry { get; set; } = ArgbColor.FromPacked( ColorNames.OrangeBlast );
	public ArgbColor BlueEntry { get; set; } = ArgbColor.FromPacked( ColorNames.BluishWater );
}

public sealed class WindowConfigData
{
	public string Title { get; set => field = value ?? "Window"; } = "Window";
	public Vector4<int> PositionAndSize { get; set; } = ( 0, 0, 900, 620 );
	public bool HasSavedPosition { get; set; }
	public bool HasSavedSize { get; set; }
	public bool IsMaximized { get; set; }
	public WindowKind WindowKind
	{
		get;
		set => field = Enum.IsDefined( value ) ? value : WindowKind.Overlapped;
	} = WindowKind.Overlapped;
}

public sealed class WindowSettingsData
{
	[JsonPropertyName( "Windows" )]
	public Dictionary<string, WindowConfigData> PresetWindowConfig
	{
		get;
		set => field = SanitizePresets( value );
	} = DefaultPresets();

	private static Dictionary<string, WindowConfigData> SanitizePresets( Dictionary<string, WindowConfigData>? value )
	{
		Dictionary<string, WindowConfigData> defaults = DefaultPresets();
		if( value is null )
			return defaults;

		List<string>? nullKeys = null;
		foreach( var kv in value )
		{
			if( kv.Value is null )
				( nullKeys ??= [] ).Add( kv.Key );
		}
		if( nullKeys is not null )
		{
			foreach( string key in nullKeys )
				value.Remove( key );
		}

		foreach( var (key, def) in defaults )
		{
			if( !value.ContainsKey( key ) )
				value[ key ] = def;
		}

		foreach( var (key, cfg) in value )
		{
			defaults.TryGetValue( key, out WindowConfigData? fallback );
			if( cfg.PositionAndSize.z <= 0 || cfg.PositionAndSize.w <= 0 )
			{
				int repairedW = cfg.PositionAndSize.z > 0 ? cfg.PositionAndSize.z : ( fallback?.PositionAndSize.z ?? 900 );
				int repairedH = cfg.PositionAndSize.w > 0 ? cfg.PositionAndSize.w : ( fallback?.PositionAndSize.w ?? 620 );
				cfg.PositionAndSize = ( cfg.PositionAndSize.x, cfg.PositionAndSize.y, repairedW, repairedH );
				cfg.HasSavedSize = false;
				cfg.HasSavedPosition = false;
				cfg.IsMaximized = false;
				if( fallback is not null )
					cfg.WindowKind = fallback.WindowKind;
			}
			if( string.IsNullOrWhiteSpace( cfg.Title ) && fallback is not null )
				cfg.Title = fallback.Title;
		}

		return value;
	}

	private static Dictionary<string, WindowConfigData> DefaultPresets() => new()
	{
		[ WindowPresetKeys.MainWindow ] = new() { Title = "HackPDM", PositionAndSize = ( 0, 0, 1280, 820 ) },
		[ WindowPresetKeys.ProfileManager ] = new() { Title = "Profile Manager", PositionAndSize = ( 0, 0, 960, 660 ) },
		[ WindowPresetKeys.OdooSettings ] = new() { Title = "Odoo Settings", PositionAndSize = ( 0, 0, 660, 560 ) },
		[ WindowPresetKeys.OdooSettingsView ] = new() { Title = "Odoo Settings", PositionAndSize = ( 0, 0, 660, 560 ) },
		[ WindowPresetKeys.HackSettings ] = new() { Title = "Hack Settings", PositionAndSize = ( 0, 0, 620, 340 ) },
		[ WindowPresetKeys.HackSettingsView ] = new() { Title = "Hack Settings", PositionAndSize = ( 0, 0, 620, 340 ) },
		[ WindowPresetKeys.ConfigSettings ] = new() { Title = "Configuration Settings", PositionAndSize = ( 0, 0, 960, 800 ) },
		[ WindowPresetKeys.HackFileManager ] = new() { Title = "Hack File Manager", PositionAndSize = ( 0, 0, 1480, 880 ) },
		[ WindowPresetKeys.DependencyGraph ] = new() { Title = "Dependency Graph", PositionAndSize = ( 0, 0, 1480, 880 ) },
		[ WindowPresetKeys.NotLoggedIn ] = new() { Title = "Not Logged In", PositionAndSize = ( 0, 0, 900, 560 ) },
		[ WindowPresetKeys.MessageBox ] = new() { Title = "Info", PositionAndSize = ( 0, 0, 520, 280 ) },
		[ WindowPresetKeys.StatusDialogView ] = new() { Title = "Status", PositionAndSize = ( 0, 0, 1180, 700 ) },
		[ WindowPresetKeys.StatusDialogViewMinimal ] = new() { Title = "Status", PositionAndSize = ( 0, 0, 920, 170 ) },
		[ WindowPresetKeys.SearchOdooView ] = new() { Title = "Search Files", PositionAndSize = ( 0, 0, 1160, 760 ) },
		[ WindowPresetKeys.SearchOdooViewMinimal ] = new() { Title = "Search Files", PositionAndSize = ( 0, 0, 980, 320 ) },
		[ WindowPresetKeys.OdooFileTypeManagerView ] = new() { Title = "File Types & Filters", PositionAndSize = ( 0, 0, 960, 660 ) },
		[ WindowPresetKeys.ApplicationSettingsView ] = new() { Title = "Settings", PositionAndSize = ( 0, 0, 1280, 820 ) },
		[ WindowPresetKeys.AppSettings ] = new() { Title = "Settings", PositionAndSize = ( 0, 0, 1280, 820 ) },
		[ WindowPresetKeys.PipWindow ] = new() { Title = "Preview", PositionAndSize = ( 0, 0, 420, 320 ) },
	};
}

/// <summary>Saved docking and pane sizing configuration for file manager and graph workspaces.</summary>
public sealed class WorkspaceLayoutSettings
{
	public bool ShowFoldersPane { get; set; } = true;
	public FolderDockSide FoldersDock
	{
		get;
		set => field = Enum.IsDefined( value ) ? value : FolderDockSide.Left;
	} = FolderDockSide.Left;
	public double FoldersPaneWidth { get; set; } = 278;

	public bool ShowDetailsPane { get; set; } = true;
	public DetailsDockPosition DetailsDock
	{
		get;
		set => field = Enum.IsDefined( value ) ? value : DetailsDockPosition.Bottom;
	} = DetailsDockPosition.Bottom;
	public double DetailsPaneHeight { get; set; } = 250;
	public double DetailsRightWidth { get; set; } = 460;

	public bool ShowPreviewPane { get; set; } = true;
	public PreviewDockPlacement PreviewPlacement
	{
		get;
		set => field = Enum.IsDefined( value ) ? value : PreviewDockPlacement.AfterDetails;
	} = PreviewDockPlacement.AfterDetails;
	public double PreviewPaneSize { get; set; } = 264;

	public bool ShowGraphFoldersPane { get; set; } = true;
	public bool ShowGraphFilesPane { get; set; } = true;
	public double GraphFoldersWidth { get; set; } = 264;
	public double GraphFilesWidth { get; set; } = 260;

	public FolderDockSide SearchDock
	{
		get;
		set => field = Enum.IsDefined( value ) ? value : FolderDockSide.Right;
	} = FolderDockSide.Right;
	public double SearchDrawerWidth { get; set; } = 460;
	public double StatusDrawerHeight { get; set; } = 280;
	public double StatusTopSplitRatio
	{
		get;
		set => field = double.IsFinite( value ) ? Math.Clamp( value, 0.2, 0.8 ) : 0.6;
	} = 0.6;
	public double StatusLeftSplitRatio
	{
		get;
		set => field = double.IsFinite( value ) ? Math.Clamp( value, 0.2, 0.8 ) : 0.5;
	} = 0.5;
	public bool SearchMinimalMode { get; set; }
	public bool StatusMinimalMode { get; set; }

	public void ApplyPreset( WorkspaceLayoutPreset preset )
	{
		switch( preset )
		{
			case WorkspaceLayoutPreset.Balanced:
				( ShowFoldersPane, FoldersDock, FoldersPaneWidth ) = ( true, FolderDockSide.Left, 278 );
				( ShowDetailsPane, DetailsDock, DetailsPaneHeight ) = ( true, DetailsDockPosition.Bottom, 250 );
				( ShowPreviewPane, PreviewPlacement, PreviewPaneSize ) = ( true, PreviewDockPlacement.AfterDetails, 264 );
				break;
			case WorkspaceLayoutPreset.RightInspector:
				( ShowFoldersPane, FoldersDock, FoldersPaneWidth ) = ( true, FolderDockSide.Left, 256 );
				( ShowDetailsPane, DetailsDock, DetailsRightWidth ) = ( true, DetailsDockPosition.Right, 460 );
				( ShowPreviewPane, PreviewPlacement, PreviewPaneSize ) = ( true, PreviewDockPlacement.BeforeDetails, 220 );
				break;
			case WorkspaceLayoutPreset.RightSidebar:
				( ShowFoldersPane, FoldersDock, FoldersPaneWidth ) = ( true, FolderDockSide.Right, 278 );
				( ShowDetailsPane, DetailsDock, DetailsPaneHeight ) = ( true, DetailsDockPosition.Bottom, 250 );
				( ShowPreviewPane, PreviewPlacement, PreviewPaneSize ) = ( true, PreviewDockPlacement.AfterDetails, 264 );
				break;
			case WorkspaceLayoutPreset.FocusEntries:
				ShowFoldersPane = false;
				( ShowDetailsPane, DetailsDock, DetailsPaneHeight ) = ( true, DetailsDockPosition.Bottom, 220 );
				ShowPreviewPane = false;
				break;
		}
	}
}

[JsonSourceGenerationOptions(
	PropertyNameCaseInsensitive = true,
	Converters = [ typeof( ArgbColorJsonConverter ), typeof( Vector4IntJsonConverter ) ] )]
[JsonSerializable( typeof( AppUiConfig ) )]
[JsonSerializable( typeof( WindowSettingsData ) )]
[JsonSerializable( typeof( WindowConfigData ) )]
[JsonSerializable( typeof( ColorSettingsData ) )]
[JsonSerializable( typeof( OdooEntryColumns ) )]
[JsonSerializable( typeof( OdooEntryColumnID ) )]
[JsonSerializable( typeof( OdooEntryColumn ) )]
[JsonSerializable( typeof( GroupByField ) )]
[JsonSerializable( typeof( ThemeSettings ) )]
[JsonSerializable( typeof( WorkspaceLayoutSettings ) )]
internal sealed partial class AppUiConfigJsonContext : JsonSerializerContext;

/// <summary>Unified UI settings section persisted inside <c>settings.json</c> under <c>UiConfig</c>.</summary>
public sealed class AppUiConfig
{
	public static JsonSerializerOptions JsonOptions => AppUiConfigJsonContext.Default.Options;

	[JsonIgnore]
	public bool IsLoadedFromFile { get; set; }

	[JsonPropertyName( "WindowSettings" )]
	public WindowSettingsData Win { get; set => field = value ?? new(); } = new();

	[JsonPropertyName( "OdooEntryColors" )]
	public ColorSettingsData EColors { get; set => field = value ?? new(); } = new();

	[JsonPropertyName( "ColumnSettings" )]
	public OdooEntryColumns ColumnsSettings { get; set => field = value ?? new(); } = new();

	[JsonPropertyName( nameof( Theme ) )]
	public ThemeSettings Theme { get; set => field = value ?? new(); } = new();

	[JsonPropertyName( nameof( WorkspaceLayout ) )]
	public WorkspaceLayoutSettings WorkspaceLayout { get; set => field = value ?? new(); } = new();
}
