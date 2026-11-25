using System.Collections.Frozen;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HackPDM.Core.Theming;

/// <summary>The color presets: they color every window, page, dialog and the dependency graph.</summary>
[JsonConverter( typeof( PaletteNameJsonConverter ) )]
public enum PaletteName
{
	Dark,
	Light,
	NavyBlue,
}

/// <summary>The shape presets: control corners and density, panel borders, and the dependency graph's boxes and links.</summary>
[JsonConverter( typeof( FormNameJsonConverter ) )]
public enum FormName
{
	Compact,
	Default,
	Modern,
	Fancy,
}

/// <summary>A color preset for the whole app: a Material color scheme.</summary>
/// <param name="Name">The preset.</param>
/// <param name="Title">The name shown in the menu.</param>
/// <param name="IsDark">Whether it runs on the dark base theme (else the light one).</param>
/// <param name="Scheme">
/// Material role (<see cref="MaterialRoles"/>) → 0xAARRGGBB. Matches the preset's group in the Figma variables
/// "HackPDM colors" (Colors page of "HackPDM — Uno Material Design").
/// </param>
[Bindable( true )]
public sealed record AppPalette( PaletteName Name, string Title, bool IsDark, IReadOnlyDictionary<string, uint> Scheme ) : INamedPreset<PaletteName>
{
	/// <summary>The Windows (Fluent) brushes derived from <see cref="Scheme"/>, for controls without a Material style.</summary>
	public IReadOnlyDictionary<string, uint> Brushes => field ??= MaterialRoles.FluentBrushes( Scheme );

	/// <summary>The border of the app's panels (folder tree, lists, graph).</summary>
	public uint PanelStroke => Scheme[ MaterialRoles.OutlineVariant ];

	/// <summary>The panel border in the Fancy style: the accent, softened.</summary>
	public uint FancyPanelStroke => 0xC0000000 | ( Scheme[ MaterialRoles.Primary ] & 0x00FFFFFF );

	/// <summary>The page background (for contrast checks).</summary>
	public uint PageBackground => Scheme[ MaterialRoles.Background ];

	/// <summary>The main text color (for contrast checks).</summary>
	public uint Text => Scheme[ MaterialRoles.OnSurface ];
}

/// <summary>A shape preset for the whole app.</summary>
/// <param name="CompactDensity">Fluent's compact sizing (shorter controls and rows); else the standard sizing.</param>
/// <param name="ControlCornerRadius">The corners of buttons, inputs and list items.</param>
/// <param name="OverlayCornerRadius">The corners of flyouts, menus and dialogs.</param>
/// <param name="PanelCornerRadius">The corners of the app's panels.</param>
/// <param name="PanelBorderThickness">The border of the app's panels.</param>
/// <param name="AccentPanels">Panel borders take the accent color (<see cref="AppPalette.FancyPanelStroke"/>).</param>
[Bindable( true )]
public sealed record AppForm( FormName Name, string Title, string Description, bool CompactDensity, double ControlCornerRadius,
	double OverlayCornerRadius, double PanelCornerRadius, double PanelBorderThickness, bool AccentPanels ) : INamedPreset<FormName>;

/// <summary>The built-in color and shape presets; any palette goes with any form.</summary>
public static class ThemePresets
{
	/// <summary>
	/// Every palette, in menu order. Light and Dark are the Material 3 Design Kit's "Blue" schemes; Navy blue keeps
	/// the navy and amber of the original preset. Colors are listed in <see cref="MaterialRoles.Names"/> order.
	/// </summary>
	public static IReadOnlyList<AppPalette> Palettes { get; } =
	[
		new( PaletteName.Dark, nameof( PaletteName.Dark ), IsDark: true, MaterialRoles.Scheme(
			0xFFB0C6FF, 0xFF152E60, 0xFF2E4578, 0xFFD9E2FF, 0xFF485E92,   // primary
			0xFFC0C6DC, 0xFF293042, 0xFF404659, 0xFFDCE2F9,               // secondary
			0xFFE0BBDD, 0xFF412742, 0xFF593D5A, 0xFFFDD7FA,               // tertiary
			0xFFF2B8B5, 0xFF601410, 0xFF8C1D18, 0xFFF9DEDC,               // error
			0xFF141218, 0xFFE6E0E9, 0xFF121318, 0xFFE2E2E9, 0xFF33343A, 0xFFC5C6D0, 0xFFE2E2E9, 0xFF33343A, // background, surface
			0xFF8F909A, 0xFF44464F,                                       // outline
			0xFF1A1B21, 0xFF1E1F25, 0xFF282A2F ) ),                       // surface containers
		new( PaletteName.Light, nameof( PaletteName.Light ), IsDark: false, MaterialRoles.Scheme(
			0xFF485E92, 0xFFFFFFFF, 0xFFADC3FE, 0xFF324574, 0xFFB0C6FF,
			0xFF585F72, 0xFFFFFFFF, 0xFFD9DFF6, 0xFF404659,
			0xFF735573, 0xFFFFFFFF, 0xFFFBD4F7, 0xFF593D5A,
			0xFFB3261E, 0xFFFFFFFF, 0xFFF9DEDC, 0xFF8C1D18,
			0xFFFDF7FF, 0xFF1D1B20, 0xFFFAF8FF, 0xFF1A1B21, 0xFFE2E2E9, 0xFF44464F, 0xFF282A2F, 0xFFEEEDF4,
			0xFF757780, 0xFFC5C6D0,
			0xFFF4F3FA, 0xFFEEEDF4, 0xFFE8E7EF ) ),
		new( PaletteName.NavyBlue, "Navy blue", IsDark: true, MaterialRoles.Scheme(
			0xFFF2B134, 0xFF0B1A30, 0xFF6B4E00, 0xFFFFDEA6, 0xFF7A5900,
			0xFF9FC3FF, 0xFF0B1A30, 0xFF24406A, 0xFFD6E3FF,
			0xFFF5C566, 0xFF0B1A30, 0xFF5A4500, 0xFFFFE2A8,
			0xFFF2B8B5, 0xFF601410, 0xFF8C1D18, 0xFFF9DEDC,
			0xFF0B1A30, 0xFFE6EDF7, 0xFF0E1F3A, 0xFFE6EDF7, 0xFF1B3253, 0xFFC3D3E8, 0xFFE6EDF7, 0xFF0B1A30,
			0xFF6F86A8, 0xFF2A4570,
			0xFF0F2240, 0xFF13294B, 0xFF1A3358 ) ),
	];

	/// <summary>Every form, in menu order.</summary>
	public static IReadOnlyList<AppForm> Forms { get; } =
	[
		new( FormName.Compact, nameof( FormName.Compact ), "Small corners, tight rows and boxes: the most on screen",
			CompactDensity: true, ControlCornerRadius: 2, OverlayCornerRadius: 4, PanelCornerRadius: 6, PanelBorderThickness: 1, AccentPanels: false ),
		new( FormName.Default, nameof( FormName.Default ), "The standard HackPDM look, as designed in Penpot",
			CompactDensity: false, ControlCornerRadius: 4, OverlayCornerRadius: 8, PanelCornerRadius: 16, PanelBorderThickness: 1, AccentPanels: false ),
		new( FormName.Modern, nameof( FormName.Modern ), "Roomy controls, rounder corners, thin borders",
			CompactDensity: false, ControlCornerRadius: 8, OverlayCornerRadius: 12, PanelCornerRadius: 20, PanelBorderThickness: 1, AccentPanels: false ),
		new( FormName.Fancy, nameof( FormName.Fancy ), "Very round corners, accent-colored panels, shaded graph boxes with shadows",
			CompactDensity: false, ControlCornerRadius: 12, OverlayCornerRadius: 16, PanelCornerRadius: 24, PanelBorderThickness: 2, AccentPanels: true ),
	];

	/// <summary>The palette named <paramref name="name"/>.</summary>
	public static AppPalette Palette( PaletteName name )
		=> EnumPresets.Find( Palettes, Enum.IsDefined( name ) ? name : PaletteName.Dark, "palette" );

	/// <summary>The form named <paramref name="name"/>.</summary>
	public static AppForm Form( FormName name )
		=> EnumPresets.Find( Forms, Enum.IsDefined( name ) ? name : FormName.Default, "form" );

	/// <summary>The panel border color for a palette and form.</summary>
	public static uint PanelStroke( AppPalette palette, AppForm form ) => form.AccentPanels ? palette.FancyPanelStroke : palette.PanelStroke;

	/// <summary>Reads a saved palette name; unknown, numeric or missing names give <paramref name="fallback"/>.</summary>
	public static PaletteName ParsePalette( string? text, PaletteName fallback = PaletteName.Dark ) => EnumPresets.Parse( text, fallback );

	/// <summary>Reads a saved form name; unknown, numeric or missing names give <paramref name="fallback"/>.</summary>
	public static FormName ParseForm( string? text, FormName fallback = FormName.Default ) => EnumPresets.Parse( text, fallback );

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
