namespace HackPDM.Core.Theming;

/// <summary>The color presets: they color every window, page, dialog and the dependency graph.</summary>
public enum PaletteName
{
	Dark,
	Light,
	NavyBlue,
}

/// <summary>The shape presets: control corners and density, panel borders, and the dependency graph's boxes and links.</summary>
public enum FormName
{
	Compact,
	Default,
	Modern,
	Fancy,
}

/// <summary>A color preset for the whole app.</summary>
/// <param name="Name">The preset.</param>
/// <param name="Title">The name shown in the menu.</param>
/// <param name="IsDark">Whether it builds on the dark Fluent theme (else the light one).</param>
/// <param name="Brushes">Fluent theme brush key → 0xAARRGGBB, applied over the base theme; empty for the stock themes.</param>
/// <param name="PanelStroke">The border of the app's panels (folder tree, lists, graph).</param>
/// <param name="FancyPanelStroke">The panel border in the Fancy style: the accent, softened.</param>
/// <param name="PageBackground">The page background this preset shows (for contrast checks).</param>
/// <param name="Text">The main text color this preset shows (for contrast checks).</param>
public sealed record AppPalette( PaletteName Name, string Title, bool IsDark, IReadOnlyDictionary<string, uint> Brushes,
	uint PanelStroke, uint FancyPanelStroke, uint PageBackground, uint Text );

/// <summary>A shape preset for the whole app.</summary>
/// <param name="CompactDensity">Fluent's compact sizing (shorter controls and rows); else the standard sizing.</param>
/// <param name="ControlCornerRadius">The corners of buttons, inputs and list items.</param>
/// <param name="OverlayCornerRadius">The corners of flyouts, menus and dialogs.</param>
/// <param name="PanelCornerRadius">The corners of the app's panels.</param>
/// <param name="PanelBorderThickness">The border of the app's panels.</param>
/// <param name="AccentPanels">Panel borders take the accent color (<see cref="AppPalette.FancyPanelStroke"/>).</param>
public sealed record AppForm( FormName Name, string Title, string Description, bool CompactDensity, double ControlCornerRadius,
	double OverlayCornerRadius, double PanelCornerRadius, double PanelBorderThickness, bool AccentPanels );

/// <summary>The built-in color and shape presets; any palette goes with any form.</summary>
public static class ThemePresets
{
	// the dark Fluent page and text colors, for the contrast checks of the stock dark theme
	private const uint FluentDarkPage = 0xFF202020, FluentDarkText = 0xFFFFFFFF;
	private const uint FluentLightPage = 0xFFF3F3F3, FluentLightText = 0xE4000000;

	/// <summary>Every palette, in menu order.</summary>
	public static IReadOnlyList<AppPalette> Palettes { get; } =
	[
		new( PaletteName.Dark, "Dark", IsDark: true, new Dictionary<string, uint>(),
			PanelStroke: 0xFF696969, FancyPanelStroke: 0xB060CDFF, PageBackground: FluentDarkPage, Text: FluentDarkText ),
		new( PaletteName.Light, "Light", IsDark: false, new Dictionary<string, uint>(),
			PanelStroke: 0xFFC4C9D0, FancyPanelStroke: 0xB0005FB8, PageBackground: FluentLightPage, Text: FluentLightText ),
		new( PaletteName.NavyBlue, "Navy blue", IsDark: true, new Dictionary<string, uint>
			{
				[ "ApplicationPageBackgroundThemeBrush" ] = 0xFF0B1A30,
				[ "SolidBackgroundFillColorBaseBrush" ] = 0xFF0E1F3A,
				[ "LayerFillColorDefaultBrush" ] = 0x4C14284A,
				[ "CardBackgroundFillColorDefaultBrush" ] = 0x14B4D2FF,
				[ "SubtleFillColorSecondaryBrush" ] = 0x1AB4D2FF,
				[ "ControlFillColorDefaultBrush" ] = 0x17B4D2FF,
				[ "ControlFillColorSecondaryBrush" ] = 0x24B4D2FF,
				[ "ControlFillColorTertiaryBrush" ] = 0x0FB4D2FF,
				[ "ControlFillColorInputActiveBrush" ] = 0xFF0E1F3A,
				[ "ControlStrokeColorDefaultBrush" ] = 0x269FC3FF,
				[ "DividerStrokeColorDefaultBrush" ] = 0x339FC3FF,
				[ "SurfaceStrokeColorDefaultBrush" ] = 0x80345A8C,
				[ "TextFillColorPrimaryBrush" ] = 0xFFE6EDF7,
				[ "TextFillColorSecondaryBrush" ] = 0xCCC3D3E8,
				[ "TextFillColorTertiaryBrush" ] = 0x99A7BCD8,
				[ "AccentFillColorDefaultBrush" ] = 0xFFF2B134,
				[ "AccentFillColorSecondaryBrush" ] = 0xE6F2B134,
				[ "AccentFillColorTertiaryBrush" ] = 0xCCF2B134,
				[ "AccentTextFillColorPrimaryBrush" ] = 0xFFF5C566,
				[ "TextOnAccentFillColorPrimaryBrush" ] = 0xFF0B1A30,
				// older system brushes some controls still use (the file manager's side pane, list highlights)
				[ "SystemControlPageBackgroundChromeLowBrush" ] = 0xFF0A1628,
				[ "SystemControlBackgroundChromeMediumLowBrush" ] = 0xFF13294B,
				[ "SystemControlHighlightAccentBrush" ] = 0xFFF2B134,
				[ "SystemControlHighlightListAccentLowBrush" ] = 0xFFF2B134,
				[ "SystemControlHighlightListAccentMediumBrush" ] = 0xFFF2B134,
			},
			PanelStroke: 0xFF2A4570, FancyPanelStroke: 0xC0F2B134, PageBackground: 0xFF0B1A30, Text: 0xFFE6EDF7 ),
	];

	/// <summary>Every form, in menu order.</summary>
	public static IReadOnlyList<AppForm> Forms { get; } =
	[
		new( FormName.Compact, "Compact", "Small corners, tight rows and boxes: the most on screen",
			CompactDensity: true, ControlCornerRadius: 2, OverlayCornerRadius: 4, PanelCornerRadius: 4, PanelBorderThickness: 1, AccentPanels: false ),
		new( FormName.Default, "Default", "The standard HackPDM look",
			CompactDensity: true, ControlCornerRadius: 4, OverlayCornerRadius: 8, PanelCornerRadius: 10, PanelBorderThickness: 3, AccentPanels: false ),
		new( FormName.Modern, "Modern", "Roomy controls, round corners, thin borders",
			CompactDensity: false, ControlCornerRadius: 8, OverlayCornerRadius: 12, PanelCornerRadius: 14, PanelBorderThickness: 1, AccentPanels: false ),
		new( FormName.Fancy, "Fancy", "Very round corners, accent-colored panels, shaded graph boxes with shadows",
			CompactDensity: false, ControlCornerRadius: 12, OverlayCornerRadius: 16, PanelCornerRadius: 18, PanelBorderThickness: 2, AccentPanels: true ),
	];

	/// <summary>The palette named <paramref name="name"/>.</summary>
	public static AppPalette Palette( PaletteName name ) => Palettes.First( p => p.Name == name );

	/// <summary>The form named <paramref name="name"/>.</summary>
	public static AppForm Form( FormName name ) => Forms.First( f => f.Name == name );

	/// <summary>The panel border color for a palette and form.</summary>
	public static uint PanelStroke( AppPalette palette, AppForm form ) => form.AccentPanels ? palette.FancyPanelStroke : palette.PanelStroke;

	/// <summary>Reads a saved palette name; unknown, numeric or missing names give <paramref name="fallback"/>.</summary>
	public static PaletteName ParsePalette( string? text, PaletteName fallback = PaletteName.Dark )
		=> Enum.TryParse( text, out PaletteName p ) && Enum.IsDefined( p ) && !int.TryParse( text, out _ ) ? p : fallback;

	/// <summary>Reads a saved form name; unknown, numeric or missing names give <paramref name="fallback"/>.</summary>
	public static FormName ParseForm( string? text, FormName fallback = FormName.Default )
		=> Enum.TryParse( text, out FormName f ) && Enum.IsDefined( f ) && !int.TryParse( text, out _ ) ? f : fallback;

	/// <summary>The WCAG contrast ratio of two colors (1 to 21); a translucent <paramref name="front"/> is blended over <paramref name="back"/>.</summary>
	public static double Contrast( uint front, uint back )
	{
		uint blended = Blend( front, back );
		double la = Luminance( blended ), lb = Luminance( back );
		return ( Math.Max( la, lb ) + 0.05 ) / ( Math.Min( la, lb ) + 0.05 );
	}

	/// <summary><paramref name="front"/> drawn over the opaque <paramref name="back"/>.</summary>
	public static uint Blend( uint front, uint back )
	{
		double a = ( front >> 24 ) / 255.0;
		uint Channel( int shift ) => (uint)Math.Round( ( ( front >> shift ) & 0xFF ) * a + ( ( back >> shift ) & 0xFF ) * ( 1 - a ) ) << shift;
		return 0xFF000000 | Channel( 16 ) | Channel( 8 ) | Channel( 0 );
	}

	private static double Luminance( uint argb )
	{
		static double Channel( uint c )
		{
			double s = c / 255.0;
			return s <= 0.03928 ? s / 12.92 : Math.Pow( ( s + 0.055 ) / 1.055, 2.4 );
		}
		return 0.2126 * Channel( ( argb >> 16 ) & 0xFF ) + 0.7152 * Channel( ( argb >> 8 ) & 0xFF ) + 0.0722 * Channel( argb & 0xFF );
	}
}
