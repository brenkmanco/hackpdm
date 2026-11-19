using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace HackPDM.Core.Theming;

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
[JsonConverter( typeof( JsonStringEnumConverter<AppThemeMode> ) )]
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
	public AppThemeMode Mode { get; set; } = AppThemeMode.Dark;

	/// <summary>The color preset; null in settings saved before the presets, which then follow <see cref="Mode"/>.</summary>
	[JsonConverter( typeof( JsonStringEnumConverter<PaletteName> ) )]
	public PaletteName? Palette { get; set; }

	/// <summary>The shape preset.</summary>
	[JsonConverter( typeof( JsonStringEnumConverter<FormName> ) )]
	public FormName Form { get; set; } = FormName.Default;

	/// <summary>Whether folder trees draw branching guide lines connecting parent and child folders.</summary>
	public bool ShowTreeBranchLines { get; set; } = true;

	/// <summary>The color preset in use: <see cref="Palette"/>, or for older settings Light when the mode was Light, else Dark.</summary>
	[JsonIgnore]
	public PaletteName EffectivePalette => Palette ?? ( Mode == AppThemeMode.Light ? PaletteName.Light : PaletteName.Dark );

	/// <summary>Theme resource key → color as "#AARRGGBB", used while the light theme is active.</summary>
	public Dictionary<string, string> Light { get; set; } = [];

	/// <summary>Theme resource key → color as "#AARRGGBB", used while the dark theme is active.</summary>
	public Dictionary<string, string> Dark { get; set; } = [];

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

	public uint ToPacked() => ( (uint)A << 24 ) | ( (uint)R << 16 ) | ( (uint)G << 8 ) | B;

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
/// <param name="Key">The Fluent theme resource key (a SolidColorBrush).</param>
/// <param name="Label">Name shown in the editor.</param>
/// <param name="Group">Section in the editor.</param>
/// <param name="Description">What the color is used for.</param>
public sealed record ThemeColorKey( string Key, string Label, string Group, string Description );

/// <summary>
/// The theme colors exposed in the editor. These are the base Fluent brushes that control templates
/// share, so changing one recolors every control that uses it.
/// </summary>
public static class ThemeColorCatalog
{
	public static IReadOnlyList<ThemeColorKey> All { get; } =
	[
		new( "TextFillColorPrimaryBrush", "Primary text", "Text", "Most text: labels, list rows, button captions" ),
		new( "TextFillColorSecondaryBrush", "Secondary text", "Text", "Captions, hints, column headers" ),
		new( "TextFillColorTertiaryBrush", "Tertiary text", "Text", "Placeholder text, de-emphasized labels" ),
		new( "TextFillColorDisabledBrush", "Disabled text", "Text", "Text on disabled controls" ),
		new( "TextOnAccentFillColorPrimaryBrush", "Text on accent", "Text", "Text on accent buttons and selected items" ),

		new( "AccentFillColorDefaultBrush", "Accent", "Accent", "Accent buttons, checked boxes, toggles, progress" ),
		new( "AccentFillColorSecondaryBrush", "Accent (hover)", "Accent", "Accent controls under the pointer" ),
		new( "AccentFillColorTertiaryBrush", "Accent (pressed)", "Accent", "Accent controls while pressed" ),
		new( "AccentTextFillColorPrimaryBrush", "Accent text", "Accent", "Hyperlinks and accent-colored text" ),

		new( "ApplicationPageBackgroundThemeBrush", "Page background", "Surfaces", "Behind every page and window" ),
		new( "SolidBackgroundFillColorBaseBrush", "Base background", "Surfaces", "Pane and flyout backgrounds" ),
		new( "LayerFillColorDefaultBrush", "Layer", "Surfaces", "Content layers on top of the page" ),
		new( "CardBackgroundFillColorDefaultBrush", "Card", "Surfaces", "Cards, grouped panels, info bars" ),
		new( "SubtleFillColorSecondaryBrush", "Subtle hover", "Surfaces", "List rows and menu items under the pointer" ),

		new( "ControlFillColorDefaultBrush", "Control", "Controls", "Buttons, text boxes, combo boxes at rest" ),
		new( "ControlFillColorSecondaryBrush", "Control (hover)", "Controls", "Controls under the pointer" ),
		new( "ControlFillColorTertiaryBrush", "Control (pressed)", "Controls", "Controls while pressed" ),
		new( "ControlFillColorDisabledBrush", "Control (disabled)", "Controls", "Disabled controls" ),
		new( "ControlFillColorInputActiveBrush", "Input (focused)", "Controls", "Text boxes while typing" ),

		new( "ControlStrokeColorDefaultBrush", "Control border", "Borders", "Outline of buttons and inputs" ),
		new( "ControlStrongStrokeColorDefaultBrush", "Strong border", "Borders", "Check box and radio outlines, input underline" ),
		new( "CardStrokeColorDefaultBrush", "Card border", "Borders", "Outline of cards and panels" ),
		new( "DividerStrokeColorDefaultBrush", "Divider", "Borders", "Separators and grid lines" ),
		new( "SurfaceStrokeColorDefaultBrush", "Surface border", "Borders", "Outline of flyouts and dialogs" ),

		new( "SystemFillColorCriticalBrush", "Error", "Status", "Errors, error info bars" ),
		new( "SystemFillColorCautionBrush", "Warning", "Status", "Warnings" ),
		new( "SystemFillColorSuccessBrush", "Success", "Status", "Success messages" ),
		new( "SystemFillColorAttentionBrush", "Attention", "Status", "Informational messages, badges" ),
	];
}
