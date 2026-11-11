using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace HackPDM.Mvvm.Models.Settings;

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
	public AppThemeMode Mode { get; set; } = AppThemeMode.Dark;

	/// <summary>Theme resource key → color as "#AARRGGBB", used while the light theme is active.</summary>
	public Dictionary<string, string> Light { get; set; } = [];

	/// <summary>Theme resource key → color as "#AARRGGBB", used while the dark theme is active.</summary>
	public Dictionary<string, string> Dark { get; set; } = [];

	public Dictionary<string, string> OverridesFor( bool dark ) => dark ? Dark : Light;
}

/// <summary>An ARGB color without any UI dependency, with "#AARRGGBB" / "#RRGGBB" parsing.</summary>
public readonly record struct ArgbColor( byte A, byte R, byte G, byte B )
{
	public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

	public static bool TryParse( string? text, out ArgbColor color )
	{
		color = default;
		if( string.IsNullOrWhiteSpace( text ) )
			return false;
		ReadOnlySpan<char> hex = text.AsSpan().Trim().TrimStart( '#' );
		if( hex.Length is not ( 6 or 8 ) || !uint.TryParse( hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value ) )
			return false;
		if( hex.Length == 6 )
			value |= 0xFF000000;
		color = new( (byte)( value >> 24 ), (byte)( value >> 16 ), (byte)( value >> 8 ), (byte)value );
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
