using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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
				while( count < 4 )
				{
					int comma = span.IndexOf( ',' );
					ReadOnlySpan<char> part = ( comma >= 0 ? span[ ..comma ] : span ).Trim();
					if( !int.TryParse( part, NumberStyles.Integer, CultureInfo.InvariantCulture, out coords[ count ] ) )
						break;
					count++;
					if( comma < 0 )
						break;
					span = span[ ( comma + 1 ).. ];
				}
				if( count == 4 && span.IndexOf( ',' ) < 0 )
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
				reader.Read();
				int val = reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : 0;
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
			{
				var (a, r, g, b) = ColorNames.Unpack( argb );
				return new( a, r, g, b );
			}
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
				reader.Read();
				byte val = reader.TokenType == JsonTokenType.Number ? reader.GetByte() : (byte)0;
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

public enum EntrySortDirection
{
	Ascending,
	Descending,
}

public enum WindowKind
{
	Default = 0,
	CompactOverlay = 1,
	FullScreen = 2,
	Overlapped = 3,
}

[JsonConverter( typeof( JsonStringEnumConverter<FolderDockSide> ) )]
public enum FolderDockSide { Left, Right }

[JsonConverter( typeof( JsonStringEnumConverter<DetailsDockPosition> ) )]
public enum DetailsDockPosition { Bottom, Right }

[JsonConverter( typeof( JsonStringEnumConverter<PreviewDockPlacement> ) )]
public enum PreviewDockPlacement { AfterDetails, BeforeDetails }

public enum WorkspaceLayoutPreset { Balanced, RightInspector, RightSidebar, FocusEntries }

public sealed class GroupByField
{
	public string FieldName { get; set; } = string.Empty;
	public EntrySortDirection SortDirection { get; set; } = EntrySortDirection.Ascending;
}

public sealed class OdooEntryColumn
{
	public int DisplayIndex { get; set; } = -1;
	public string Header { get; set; } = string.Empty;
	public double Width { get; set; } = 100;
	public bool AutoSize { get; set; }
}

public sealed class OdooEntryColumnID
{
	public string BindingPath { get; set; } = string.Empty;
	public OdooEntryColumn Column { get; set; } = new();
}

public sealed class OdooEntryColumns
{
	public double RowHeight { get; set; } = 26;
	public double FontSize { get; set; } = 11;
	public bool GroupingEnabled { get; set; } = true;
	public bool GroupCollapsed { get; set; }
	public GroupByField GroupBy { get; set; } = new() { FieldName = "Status", SortDirection = EntrySortDirection.Ascending };

	public Dictionary<string, OdooEntryColumnID?> Columns { get; set; } = new()
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
	public ArgbColor OrangeEntry { get; set; } = FromPacked( ColorNames.OrangeBlast );
	public ArgbColor BlueEntry { get; set; } = FromPacked( ColorNames.BluishWater );

	private static ArgbColor FromPacked( uint packed )
	{
		var (a, r, g, b) = ColorNames.Unpack( packed );
		return new( a, r, g, b );
	}
}

public sealed class WindowConfigData
{
	public string Title { get; set; } = "Window";
	public Vector4<int> PositionAndSize { get; set; } = ( 0, 0, 500, 350 );
	public WindowKind WindowKind { get; set; } = WindowKind.Overlapped;
}

public sealed class WindowSettingsData
{
	[JsonPropertyName( "Windows" )]
	public Dictionary<string, WindowConfigData> PresetWindowConfig { get; set; } = new()
	{
		[ "ProfileManager" ] = new() { Title = "Profile Manager", PositionAndSize = ( 0, 0, 820, 580 ) },
		[ "OdooSettings" ] = new() { Title = "Odoo Settings", PositionAndSize = ( 0, 0, 620, 520 ) },
		[ "HackSettings" ] = new() { Title = "Hack Settings", PositionAndSize = ( 0, 0, 560, 320 ) },
		[ "ConfigSettings" ] = new() { Title = "Configuration Settings", PositionAndSize = ( 0, 0, 820, 780 ) },
		[ "HackFileManager" ] = new() { Title = "Hack File Manager", PositionAndSize = ( 0, 0, 1480, 880 ) },
		[ "DependencyGraph" ] = new() { Title = "Dependency Graph", PositionAndSize = ( 0, 0, 1480, 880 ) },
		[ "NotLoggedIn" ] = new() { Title = "Not Logged In", PositionAndSize = ( 0, 0, 900, 560 ) },
		[ "MessageBox" ] = new() { Title = "Info", PositionAndSize = ( 0, 0, 450, 240 ), WindowKind = WindowKind.CompactOverlay },
		[ "StatusDialogView" ] = new() { Title = "Status", PositionAndSize = ( 0, 0, 1180, 700 ) },
		[ "SearchOdooView" ] = new() { Title = "Search Files", PositionAndSize = ( 0, 0, 1160, 760 ) },
		[ "OdooFileTypeManagerView" ] = new() { Title = "File Types & Filters", PositionAndSize = ( 0, 0, 920, 620 ) },
		[ "ApplicationSettingsView" ] = new() { Title = "Settings", PositionAndSize = ( 0, 0, 960, 680 ) },
		[ "AppSettings" ] = new() { Title = "Settings", PositionAndSize = ( 0, 0, 1280, 820 ) },
	};
}

/// <summary>Saved docking and pane sizing configuration for file manager and graph workspaces.</summary>
public sealed class WorkspaceLayoutSettings
{
	public bool ShowFoldersPane { get; set; } = true;
	public FolderDockSide FoldersDock { get; set; } = FolderDockSide.Left;
	public double FoldersPaneWidth { get; set; } = 278;

	public bool ShowDetailsPane { get; set; } = true;
	public DetailsDockPosition DetailsDock { get; set; } = DetailsDockPosition.Bottom;
	public double DetailsPaneHeight { get; set; } = 250;
	public double DetailsRightWidth { get; set; } = 460;

	public bool ShowPreviewPane { get; set; } = true;
	public PreviewDockPlacement PreviewPlacement { get; set; } = PreviewDockPlacement.AfterDetails;
	public double PreviewPaneSize { get; set; } = 264;

	public double GraphFoldersWidth { get; set; } = 264;
	public double GraphFilesWidth { get; set; } = 260;

	public FolderDockSide SearchDock { get; set; } = FolderDockSide.Right;
	public double SearchDrawerWidth { get; set; } = 460;
	public double StatusDrawerHeight { get; set; } = 280;
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

/// <summary>Unified UI settings section persisted inside <c>settings.json</c> under <c>UiConfig</c>.</summary>
public sealed class AppUiConfig
{
	public static JsonSerializerOptions JsonOptions { get; } = new()
	{
		PropertyNameCaseInsensitive = true,
		Converters =
		{
			new ArgbColorJsonConverter(),
			new Vector4IntJsonConverter(),
			new JsonStringEnumConverter(),
		},
	};

	[JsonIgnore]
	public bool IsLoadedFromFile { get; set; }

	[JsonPropertyName( "WindowSettings" )]
	public WindowSettingsData Win { get; set; } = new();

	[JsonPropertyName( "OdooEntryColors" )]
	public ColorSettingsData EColors { get; set; } = new();

	[JsonPropertyName( "ColumnSettings" )]
	public OdooEntryColumns ColumnsSettings { get; set; } = new();

	[JsonPropertyName( "Theme" )]
	public ThemeSettings Theme { get; set; } = new();

	[JsonPropertyName( "WorkspaceLayout" )]
	public WorkspaceLayoutSettings WorkspaceLayout { get; set; } = new();
}
