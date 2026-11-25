using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HackPDM.Core.Theming;

/// <summary>
/// Zero-reflection JSON converter for enums that validates <see cref="Enum.IsDefined{TEnum}(TEnum)"/>
/// on both names and numeric values, falling back cleanly when a saved value is out of range or unknown.
/// </summary>
public class SafeEnumJsonConverter<TEnum>( TEnum fallback = default ) : JsonConverter<TEnum>
	where TEnum : struct, Enum
{
	public override TEnum Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		if( reader.TokenType == JsonTokenType.String )
		{
			string? text = reader.GetString();
			if( !string.IsNullOrWhiteSpace( text )
				&& !long.TryParse( text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _ )
				&& Enum.TryParse( text, ignoreCase: true, out TEnum parsed )
				&& Enum.IsDefined( parsed ) )
			{
				return parsed;
			}
			return fallback;
		}
		if( reader.TokenType == JsonTokenType.Number )
		{
			if( reader.TryGetInt64( out long num )
				&& Enum.TryParse( num.ToString( CultureInfo.InvariantCulture ), out TEnum parsed )
				&& Enum.IsDefined( parsed ) )
			{
				return parsed;
			}
			return fallback;
		}
		reader.Skip();
		return fallback;
	}

	public override void Write( Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options )
		=> writer.WriteStringValue( Enum.GetName( value ) ?? Enum.GetName( fallback ) );
}

internal sealed class AppThemeModeJsonConverter() : SafeEnumJsonConverter<AppThemeMode>( AppThemeMode.Dark );
internal sealed class PaletteNameJsonConverter() : SafeEnumJsonConverter<PaletteName>( PaletteName.Dark );
internal sealed class FormNameJsonConverter() : SafeEnumJsonConverter<FormName>( FormName.Default );

internal sealed class NullablePaletteNameJsonConverter : JsonConverter<PaletteName?>
{
	private static readonly PaletteNameJsonConverter Inner = new();

	public override PaletteName? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		if( reader.TokenType == JsonTokenType.Null )
			return null;
		if( reader.TokenType == JsonTokenType.String )
		{
			string? text = reader.GetString();
			if( !string.IsNullOrWhiteSpace( text )
				&& !long.TryParse( text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _ )
				&& Enum.TryParse( text, ignoreCase: true, out PaletteName parsed )
				&& Enum.IsDefined( parsed ) )
			{
				return parsed;
			}
			return null;
		}
		if( reader.TokenType == JsonTokenType.Number )
		{
			if( reader.TryGetInt64( out long num )
				&& Enum.TryParse( num.ToString( CultureInfo.InvariantCulture ), out PaletteName parsed )
				&& Enum.IsDefined( parsed ) )
			{
				return parsed;
			}
			return null;
		}
		reader.Skip();
		return null;
	}

	public override void Write( Utf8JsonWriter writer, PaletteName? value, JsonSerializerOptions options )
	{
		if( value is { } p && Enum.IsDefined( p ) )
			Inner.Write( writer, p, options );
		else
			writer.WriteNullValue();
	}
}

/// <summary>Contract for presets keyed by an enum value so lookup and parsing share one zero-reflection generic helper.</summary>
public interface INamedPreset<out TEnum> where TEnum : struct, Enum
{
	TEnum Name { get; }
}

/// <summary>Shared O(1) enum-indexed preset lookup and strict enum name parsing.</summary>
public static class EnumPresets
{
	/// <summary>Looks up a preset by its enum key in O(1) when ordered by enum value, falling back to a linear scan.</summary>
	public static TItem Find<TEnum, TItem>( IReadOnlyList<TItem> items, TEnum key, string kind )
		where TEnum : struct, Enum
		where TItem : INamedPreset<TEnum>
	{
		int idx = Unsafe.SizeOf<TEnum>() == sizeof( int ) ? Unsafe.As<TEnum, int>( ref key ) : -1;
		if( (uint)idx < (uint)items.Count && EqualityComparer<TEnum>.Default.Equals( items[ idx ].Name, key ) )
			return items[ idx ];
		for( int i = 0; i < items.Count; i++ )
		{
			if( EqualityComparer<TEnum>.Default.Equals( items[ i ].Name, key ) )
				return items[ i ];
		}
		throw new InvalidOperationException( $"Unknown {kind} '{key}'." );
	}

	/// <summary>Parses a saved enum name strictly (rejecting numeric values); unknown or missing names give <paramref name="fallback"/>.</summary>
	public static TEnum Parse<TEnum>( string? text, TEnum fallback )
		where TEnum : struct, Enum
		=> Enum.TryParse( text, out TEnum value ) && Enum.IsDefined( value ) && !int.TryParse( text, out _ )
			? value
			: fallback;
}

/// <summary>Which base theme the app uses.</summary>
[JsonConverter( typeof( AppThemeModeJsonConverter ) )]
public enum AppThemeMode
{
	/// <summary>Follow the operating system's light/dark setting.</summary>
	System,
	Light,
	Dark,
}

/// <summary>
/// The saved theme: the base mode plus per-color overrides for each base theme. Stored in
/// AppConfig.json under "Theme" and applied at startup.
/// </summary>
public sealed class ThemeSettings
{
	/// <summary>The base light/dark theme; it follows <see cref="Palette"/> (kept for settings saved before the presets).</summary>
	public AppThemeMode Mode
	{
		get;
		set => field = Enum.IsDefined( value ) ? value : AppThemeMode.Dark;
	} = AppThemeMode.Dark;

	/// <summary>The color preset; null in settings saved before the presets, which then follow <see cref="Mode"/>.</summary>
	[JsonConverter( typeof( NullablePaletteNameJsonConverter ) )]
	public PaletteName? Palette
	{
		get;
		set => field = value is { } p && Enum.IsDefined( p ) ? p : null;
	}

	/// <summary>The shape preset.</summary>
	[JsonConverter( typeof( FormNameJsonConverter ) )]
	public FormName Form
	{
		get;
		set => field = Enum.IsDefined( value ) ? value : FormName.Default;
	} = FormName.Default;

	/// <summary>Whether folder trees draw branching guide lines connecting parent and child folders.</summary>
	public bool ShowTreeBranchLines { get; set; } = true;

	/// <summary>The color preset in use: <see cref="Palette"/>, or for older settings Light when the mode was Light, else Dark.</summary>
	[JsonIgnore]
	public PaletteName EffectivePalette => Palette is { } p && Enum.IsDefined( p ) ? p : ( Mode == AppThemeMode.Light ? PaletteName.Light : PaletteName.Dark );

	/// <summary>Material color role → color as "#AARRGGBB", used while the light theme is active.</summary>
	public Dictionary<string, string> Light { get; set => field = value ?? []; } = [];

	/// <summary>Material color role → color as "#AARRGGBB", used while the dark theme is active.</summary>
	public Dictionary<string, string> Dark { get; set => field = value ?? []; } = [];

	/// <summary>Picks the color overrides for one base theme.</summary>
	/// <param name="dark">Whether the dark theme is active.</param>
	/// <returns>The live <see cref="Dark"/> or <see cref="Light"/> dictionary (not a copy), so changes to it are saved with the settings.</returns>
	public Dictionary<string, string> OverridesFor( bool dark ) => dark ? Dark : Light;
}

/// <summary>An ARGB color without any UI dependency, with "#AARRGGBB" / "#RRGGBB" parsing.</summary>
public readonly record struct ArgbColor( byte A, byte R, byte G, byte B )
{
	public static ArgbColor FromPacked( uint argb )
		=> new( (byte)( argb >> 24 ), (byte)( argb >> 16 ), (byte)( argb >> 8 ), (byte)argb );

	public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

	/// <summary>Parses a hex color such as "#AARRGGBB" or "#RRGGBB"; one leading "#" and surrounding spaces are optional.</summary>
	/// <param name="text">The text to parse.</param>
	/// <param name="color">The parsed color, fully opaque when no alpha was given; <see langword="default"/> when parsing fails.</param>
	/// <returns><see langword="true"/> when <paramref name="text"/> held 6 or 8 hex digits.</returns>
	public static bool TryParse( [NotNullWhen( true )] string? text, out ArgbColor color )
	{
		color = default;
		if( string.IsNullOrWhiteSpace( text ) )
			return false;
		ReadOnlySpan<char> hex = text.AsSpan().Trim();
		if( hex.Length > 0 && hex[ 0 ] == '#' )
			hex = hex[ 1.. ];
		if( hex.Length is not ( 6 or 8 ) || !uint.TryParse( hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value ) )
			return false;
		if( hex.Length == 6 )
			value |= 0xFF000000;
		color = FromPacked( value );
		return true;
	}
}

/// <summary>One theme color the user can change.</summary>
/// <param name="Key">The Material color role (<see cref="MaterialRoles"/>), e.g. "Primary" or "SurfaceContainerLow".</param>
/// <param name="Label">Name shown in the editor.</param>
/// <param name="Group">Section in the editor.</param>
/// <param name="Description">What the color is used for.</param>
[Bindable( true )]
public sealed record ThemeColorKey( string Key, string Label, string Group, string Description );

/// <summary>
/// The theme colors exposed in the editor: the Material color roles. Changing one recolors every control that uses it,
/// and the Windows (Fluent) brushes derived from it (<see cref="MaterialRoles.FluentBrushes"/>).
/// </summary>
public static class ThemeColorCatalog
{
	public static IReadOnlyList<ThemeColorKey> All => MaterialRoles.All;
}
