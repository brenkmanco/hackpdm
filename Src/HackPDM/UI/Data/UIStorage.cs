using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Forms.Odoo;
using HackPDM.UI.Forms.Settings;
using HackPDM.UI.Types;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;
using EntryRow = HackPDM.UI.Types.EntryRow;

namespace HackPDM.UI.Data;

public static class UIStorage
{
	

	#region Color Settings

	public static readonly Lazy<SolidColorBrush> BrushWhite				= new(() => new( ColorData.White.Value));
		public static readonly Lazy<SolidColorBrush> BrushBlack				= new(() => new( ColorData.Black.Value));
		public static readonly Lazy<SolidColorBrush> BrushLightGray			= new(() => new( ColorData.LightGray.Value));
		public static readonly Lazy<SolidColorBrush> BrushGray				= new(() => new( ColorData.Gray.Value));
		public static readonly Lazy<SolidColorBrush> BrushMustardYellow		= new(() => new( ColorData.MustardYellow.Value));
		public static readonly Lazy<SolidColorBrush> BrushDarkGray			= new(() => new( ColorData.DarkGray.Value));
		public static readonly Lazy<SolidColorBrush> BrushDarkOliveGreen	= new(() => new( ColorData.DarkOliveGreen.Value));
		public static readonly Lazy<SolidColorBrush> BrushDarkBlue			= new(() => new( ColorData.DarkBlue.Value));
		public static readonly Lazy<SolidColorBrush> BrushDarkRed			= new(() => new( ColorData.DarkRed.Value));

		public static readonly Lazy<LinearGradientBrush> OrangeBrush = new(()=>FormHelper.EZGradient(
			FlowDirection.LeftToRight, 
			[ GlobalData.EColors.OrangeEntry.Value.ModifyColor(40), GlobalData.EColors.OrangeEntry.Value.ModifyColor(10), GlobalData.EColors.OrangeEntry.Value.ModifyColor(0)]));

		public static readonly Lazy<LinearGradientBrush> BlueBrush = new(()=>FormHelper.EZGradient(
			FlowDirection.LeftToRight,
			[ GlobalData.EColors.BlueEntry.Value.ModifyColor(40), GlobalData.EColors.BlueEntry.Value.ModifyColor(10), GlobalData.EColors.BlueEntry.Value.ModifyColor(0)]));

		public static readonly Lazy<LinearGradientBrush> RedBrush = new(()=>FormHelper.EZGradient(
			FlowDirection.LeftToRight,
			[ GlobalData.EColors.DarkRed.Value.ModifyColor(55), GlobalData.EColors.DarkRed.Value.ModifyColor(10), GlobalData.EColors.DarkRed.Value.ModifyColor(0)]));	

		public static readonly Lazy<LinearGradientBrush> GreenBrush = new(()=>FormHelper.EZGradient(
			FlowDirection.LeftToRight,
			[ GlobalData.EColors.DarkOliveGreen.Value.ModifyColor(40), GlobalData.EColors.DarkOliveGreen.Value.ModifyColor(10), GlobalData.EColors.DarkOliveGreen.Value.ModifyColor(0)]));

	#endregion
	#region AppSettings
		public static GlobalSerializedData GlobalData { get => field ??= GlobalSerializedData.LoadFromFile( GlobalSerializedData.FilePath ); set; } 
		public static ColorSettings ColorData => GlobalData.EColors;
		public static OdooEntryColumns ColumnsInfo => GlobalData.ColumnsSettings;
		public static Lazy<Dictionary<string, OdooEntryColumnID?>> Columns => GlobalData.ColumnsSettings.Columns;
	#endregion
	public static Color ToColor((byte, byte, byte, byte) argb) => Color.FromArgb(argb.Item1, argb.Item2, argb.Item3, argb.Item4);
	public static (byte, byte, byte, byte)  TransformUIntToRGB(ColorNames names)
		=> (
				(byte)(((uint)names >> 24) & 0xFF),
				(byte)(((uint)names >> 16) & 0xFF),
				(byte)(((uint)names >> 8) & 0xFF),
				(byte)(((uint)names) & 0xFF)
		);
}
public static class CustomSerializers
{
	public class ColorSerializer : JsonConverter<Color>
	{
		public override Color Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			string colorString = reader.GetString() ?? throw new JsonException("Color string is null");
			if( colorString.StartsWith( "#" ) )
			{
				colorString = colorString.Substring( 1 );
			}
			if( colorString.Length == 6 )
			{
				colorString = "FF" + colorString; // Add alpha channel if not present
			}
			if( uint.TryParse( colorString, System.Globalization.NumberStyles.HexNumber, null, out uint argb ) )
			{
				return Color.FromArgb(
					( byte )( ( argb >> 24 ) & 0xFF ),
					( byte )( ( argb >> 16 ) & 0xFF ),
					( byte )( ( argb >> 8 ) & 0xFF ),
					( byte )( argb & 0xFF )
				);
			}
			else
			{
				throw new JsonException( $"Invalid color string: {colorString}" );
			}
		}
		public override void Write( Utf8JsonWriter writer, Color value, JsonSerializerOptions options )
		{
			string colorString = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
			writer.WriteStringValue( colorString );
		}
	}
	public class Vector4Serializer : JsonConverter<Vector4<int>>
	{
		public override Vector4<int> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			if( reader.TokenType != JsonTokenType.StartArray )
			{
				throw new JsonException( "Expected StartArray token" );
			}
			reader.Read();
			int x = reader.GetInt32();
			reader.Read();
			int y = reader.GetInt32();
			reader.Read();
			int z = reader.GetInt32();
			reader.Read();
			int w = reader.GetInt32();
			reader.Read();
			return  reader.TokenType != JsonTokenType.EndArray 
				? throw new JsonException( "Expected EndArray token" )
				:  new Vector4<int>( x, y, z, w );
		}
		public override void Write( Utf8JsonWriter writer, Vector4<int> value, JsonSerializerOptions options )
		{
			writer.WriteStartArray();
			writer.WriteNumberValue( value.x );
			writer.WriteNumberValue( value.y );
			writer.WriteNumberValue( value.z );
			writer.WriteNumberValue( value.w );
			writer.WriteEndArray();
		}
	}
}
public class GlobalSerializedData
{
	private const string DefaultFileName = "AppConfig.json";
	[JsonIgnore]
	private static readonly string DefaultFilePath = TryHelper
			.TryGet(() => Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.Personal),
					DefaultFileName), out string? path) 
						? path ?? ""
						: "";
	[JsonIgnore]
	public static JsonSerializerOptions Options = new()
	{
		Converters =
		{
			new CustomSerializers.ColorSerializer(),
			new CustomSerializers.Vector4Serializer(),
		}
	};

	[JsonIgnore]
	public static string? FilePath { get; set; } = GetFilePathDirectory();

	[JsonIgnore]
	public bool IsLoadedFromFile { get; set; } = false;

	[ JsonPropertyName( "WindowSettings" )]
	public WindowSettings Win	{ get; set; } = new();
	[JsonPropertyName( "OdooEntryColors" )]
	public ColorSettings EColors	{ get; set; } = new();
	[JsonPropertyName( "ColumnSettings" )]
	public OdooEntryColumns ColumnsSettings { get; set; } = new();

	public static string? GetFilePathDirectory()
	{
		FileInfo file;
		try
		{
			if (FilePath is null)
			{
				;
				if (!TryHelper.Try(() => SetFilePath(FilePath = DefaultFilePath)))
				{
					HackApp.CoreSettings.Set("AppConfigFilePath", FilePath);
				}
				return FilePath;
			}

			file = new(string.IsNullOrEmpty(FilePath) 
				? HackApp.CoreSettings.Get("AppConfigFilePath", "") ?? DefaultFilePath 
				: FilePath);
		}
		finally
		{
			FilePath = DefaultFilePath;
			file = new(FilePath);
		}
		
		if (file is { Exists: false } or { Extension: not ".json" })
			SetFilePath(file);

		return FilePath;
	}
	public static void SetFilePath(FileInfo fileInfo)
	{
		if (fileInfo is { Exists: false } or { Extension: not ".json" })
			FilePath = DefaultFilePath;

		HackApp.CoreSettings.Set("AppConfigFilePath", FilePath);
	}
	public static void SetFilePath(string? filePath)
		=> SetFilePath(new FileInfo(string.IsNullOrEmpty(filePath) ? DefaultFilePath : filePath));
	
	public static GlobalSerializedData LoadFromFile( string filePath )
	{
		if( !File.Exists( filePath ) )
		{
			return new GlobalSerializedData();
		}
		string json = File.ReadAllText( filePath );
		return LoadFromJson( json );
	}
	public static GlobalSerializedData LoadFromJson( string json )
	{
		var result = JsonSerializer.Deserialize<GlobalSerializedData>( json, Options );
		if( result == null )
		{
			result = new GlobalSerializedData();
		}
		else
		{
			result.IsLoadedFromFile = true;
		}
		return result;
	}
	public void SaveToFile( )
	{
		string json = JsonSerializer.Serialize( this, Options );
		File.WriteAllText( FilePath, json );
		HackApp.CoreSettings.Set("AppConfigFilePath", FilePath);
	}
}
public class WindowSettings
{
	[JsonPropertyName( "Windows" )]
	public Dictionary<string, WindowConfig> PresetWindowConfig { get; set; } = new ()
	{
		{ nameof(ProfileManager),		new () { Title = "Profile Manager",     PositionAndSize = (0, 0, 500, 350) }},
		{ nameof(OdooSettings),			new () { Title = "Odoo Settings",		PositionAndSize = (0, 0, 600, 500) }},
		{ nameof(HackSettings),			new () { Title = "Hack Settings",		PositionAndSize = (0, 0, 500, 500) }},
		{ nameof(DynamicJsonForm),      new () { Title = "Json Editor",			PositionAndSize = (0, 0, 1600, 900) }},
		{ "ConfigSettings",				new () { Title = "Configuration Settings", PositionAndSize = (0, 0, 700, 400) }},
		{ nameof(HackFileManager),		new () { Title = "Hack File Manager",	PositionAndSize = (0, 0, 1600, 900) }},
		{ nameof(NotLoggedIn),			new () { Title = "Not Logged In",		PositionAndSize = (0, 0, 1600, 900) }},
		{ nameof(MessageBox),			new () { Title = "Info",				PositionAndSize = (0, 0, 450, 250), WindowKind = AppWindowPresenterKind.CompactOverlay }},
		{ "TemplateWindow",				new () { Title = "Template Window",		PositionAndSize = (0, 0, 500, 300) }},
	};
}
public class ColorSettings
{
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> White				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 255, 255, 255 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> Black				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 0, 0, 0 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> LightGray			{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 211, 211, 211 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> Gray					{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 128, 128, 128 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> MustardYellow		{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 150, 150, 0 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> DarkGray				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 64, 64, 64 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> DarkRed				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 139, 0, 0 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> DarkBlue				{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 0, 0, 139 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> DarkOliveGreen		{ get; set; } = new Lazy<Color>(() => Color.FromArgb( 255, 85, 107, 47 ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> OrangeEntry			{ get; set; } = new Lazy<Color>(() => UIStorage.ToColor( UIStorage.TransformUIntToRGB( ColorNames.OrangeBlast ) ));
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Color> BlueEntry			{ get; set; } = new Lazy<Color>(() => UIStorage.ToColor( UIStorage.TransformUIntToRGB( ColorNames.BluishWater ) ));
}
public class OdooEntryColumns
{
	public double RowHeight { get; set; } = 18;
	public double FontSize { get; set; } = 10;
	public bool GroupingEnabled { get; set; } = true;
	public bool GroupCollapsed { get; set; } = false;
	public GroupByField GroupBy { get; set; } = new()
	{
		FieldName = "Status",
		SortDirection = DataGridSortDirection.Ascending
	};
	public bool InactivesEnabled { get; set; } = false;
	public bool IgnoreFilters { get; set; } = false;
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<Dictionary<string, OdooEntryColumnID?>> Columns { get; set; } = new( () => new()
	{
		{ nameof(EntryRow.Name),		new OdooEntryColumnID() 
			{ BindingPath = nameof(EntryRow.Name), Column = new Lazy<OdooEntryColumn>( () 
				=> new OdooEntryColumn() { DisplayIndex = 0, Header = "Name", Width = 350, AutoSize = false } ) }},

		{ nameof(EntryRow.Type),		new OdooEntryColumnID() 
			{ BindingPath = nameof(EntryRow.Type), Column = new Lazy<OdooEntryColumn>( () 
				=> new OdooEntryColumn() { DisplayIndex = 1, Header = "Type", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.Size),		new OdooEntryColumnID() 
			{ BindingPath = nameof(EntryRow.Size), Column = new Lazy<OdooEntryColumn>( () 
				=> new OdooEntryColumn() { DisplayIndex = 2, Header = "Size", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.Status),		new OdooEntryColumnID() 
			{ BindingPath = nameof(EntryRow.Status)	, Column = new Lazy<OdooEntryColumn>( () 
				=> new OdooEntryColumn() { DisplayIndex = 3, Header = "Status", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.Checkout),	new OdooEntryColumnID() 
			{ BindingPath = nameof(EntryRow.Checkout), Column = new Lazy<OdooEntryColumn>( () 
				=> new OdooEntryColumn() { DisplayIndex = 4, Header = "Checkout", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.LocalDate),	new OdooEntryColumnID() 
			{ BindingPath = nameof(EntryRow.LocalDate), Column = new Lazy<OdooEntryColumn>( () 
				=> new OdooEntryColumn() { DisplayIndex = 5, Header = "Local Date", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.RemoteDate),	new OdooEntryColumnID() 
			{ BindingPath = nameof(EntryRow.RemoteDate), Column = new Lazy<OdooEntryColumn>( () 
				=> new OdooEntryColumn() { DisplayIndex = 6, Header = "Remote Date", Width = 100, AutoSize = false } ) }},

		{ nameof(EntryRow.FullName),	new OdooEntryColumnID() 
			{ BindingPath = nameof(EntryRow.FullName), Column = new Lazy<OdooEntryColumn>( () 
				=> new OdooEntryColumn() { DisplayIndex = 7, Header = "Full Name", Width = 200, AutoSize = true } ) }},

	} );
}
public class OdooEntryColumnID
{
	public string BindingPath		{ get; set; } = string.Empty;
	[JsonConverter( typeof( LazyConverterFactory ) )]
	public Lazy<OdooEntryColumn> Column	{ get; set; } = new( () => new OdooEntryColumn() );
}
public class OdooEntryColumn
{
	public int DisplayIndex			{ get; set; } = -1;
	public string Header			{ get; set; } = string.Empty;
	public double Width				{ get; set; } = 100;
	public bool AutoSize			{ get; set; } = false;
}
public class GroupByField
{
	public string FieldName						{ get; set; } = string.Empty;
	public DataGridSortDirection SortDirection	{ get; set; } = DataGridSortDirection.Ascending;
}

public class LazyConverterFactory : JsonConverterFactory
{
	public override bool CanConvert( Type typeToConvert )
	{
		// Checks if the property type is Lazy<T>
		return typeToConvert.IsGenericType &&
			   typeToConvert.GetGenericTypeDefinition() == typeof( Lazy<> );
	}

	public override JsonConverter CreateConverter( Type typeToConvert, JsonSerializerOptions options )
	{
		Type valueType = typeToConvert.GetGenericArguments()[0];

		// Dynamically instantiate the generic LazyConverter<T>
		return ( JsonConverter )Activator.CreateInstance(
			typeof( LazyConverter<> ).MakeGenericType( valueType ) );
	}
}
public class LazyConverter<T> : JsonConverter<Lazy<T>>
{
	public override Lazy<T> Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		// Deserialize the JSON value natively into T first
		T value = JsonSerializer.Deserialize<T>(ref reader, options);

		// Return it wrapped in a Lazy<T> container
		return new Lazy<T>( () => value );
	}

	public override void Write( Utf8JsonWriter writer, Lazy<T> value, JsonSerializerOptions options )
	{
		if( value == null )
		{
			writer.WriteNullValue();
		}
		else
		{
			// Serialize the evaluated inner value T to the JSON string
			JsonSerializer.Serialize( writer, value.Value, options );
		}
	}
}
