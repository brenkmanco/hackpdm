using System.Collections.Frozen;

namespace HackPDM.Core.Theming;

/// <summary>
/// The Material color roles a preset defines. They match the "HackPDM colors" variables in the Figma file
/// "HackPDM — Uno Material Design" (Colors page), one group per preset: "Light/Primary", "Navy blue/Surface", ….
/// </summary>
/// <remarks>
/// In the app each role is a pair of resources: <c>{Role}Color</c> and <c>{Role}Brush</c> from Uno Material, or, for the
/// surface-container roles Uno Material does not have, <c>Hack{Role}Color</c> and <c>Hack{Role}Brush</c> from App.xaml.
/// The Windows (Fluent) brushes that controls without a Material style still use are derived from the roles
/// (<see cref="FluentBrushes"/>), so one preset colors everything.
/// </remarks>
public static class MaterialRoles
{
	public const string Primary = nameof( Primary );
	public const string OnPrimary = nameof( OnPrimary );
	public const string PrimaryContainer = nameof( PrimaryContainer );
	public const string OnPrimaryContainer = nameof( OnPrimaryContainer );
	public const string PrimaryInverse = nameof( PrimaryInverse );
	public const string Secondary = nameof( Secondary );
	public const string OnSecondary = nameof( OnSecondary );
	public const string SecondaryContainer = nameof( SecondaryContainer );
	public const string OnSecondaryContainer = nameof( OnSecondaryContainer );
	public const string Tertiary = nameof( Tertiary );
	public const string OnTertiary = nameof( OnTertiary );
	public const string TertiaryContainer = nameof( TertiaryContainer );
	public const string OnTertiaryContainer = nameof( OnTertiaryContainer );
	public const string Error = nameof( Error );
	public const string OnError = nameof( OnError );
	public const string ErrorContainer = nameof( ErrorContainer );
	public const string OnErrorContainer = nameof( OnErrorContainer );
	public const string Background = nameof( Background );
	public const string OnBackground = nameof( OnBackground );
	public const string Surface = nameof( Surface );
	public const string OnSurface = nameof( OnSurface );
	public const string SurfaceVariant = nameof( SurfaceVariant );
	public const string OnSurfaceVariant = nameof( OnSurfaceVariant );
	public const string SurfaceInverse = nameof( SurfaceInverse );
	public const string OnSurfaceInverse = nameof( OnSurfaceInverse );
	public const string Outline = nameof( Outline );
	public const string OutlineVariant = nameof( OutlineVariant );
	public const string SurfaceContainerLow = nameof( SurfaceContainerLow );
	public const string SurfaceContainer = nameof( SurfaceContainer );
	public const string SurfaceContainerHigh = nameof( SurfaceContainerHigh );

	/// <summary>Every role, grouped the way the Theme Editor shows them.</summary>
	public static IReadOnlyList<ThemeColorKey> All { get; } =
	[
		new( Primary, "Primary", "Accent", "Filled buttons, the selected tab's underline, links, checked boxes and toggles" ),
		new( OnPrimary, "On primary", "Accent", "Text and icons on primary-colored buttons" ),
		new( PrimaryContainer, "Primary container", "Accent", "Softer accent backgrounds: tonal buttons, highlights" ),
		new( OnPrimaryContainer, "On primary container", "Accent", "Text on primary-container backgrounds" ),
		new( PrimaryInverse, "Primary (inverse)", "Accent", "Accent color on inverse surfaces such as snackbars" ),

		new( Secondary, "Secondary", "Secondary", "Less prominent accents" ),
		new( OnSecondary, "On secondary", "Secondary", "Text on secondary-colored backgrounds" ),
		new( SecondaryContainer, "Secondary container", "Secondary", "Selected rows, the selected folder, selected filter chips" ),
		new( OnSecondaryContainer, "On secondary container", "Secondary", "Text on selected rows and chips" ),

		new( Tertiary, "Tertiary", "Tertiary", "Contrasting accents that balance primary and secondary" ),
		new( OnTertiary, "On tertiary", "Tertiary", "Text on tertiary-colored backgrounds" ),
		new( TertiaryContainer, "Tertiary container", "Tertiary", "Soft contrasting backgrounds" ),
		new( OnTertiaryContainer, "On tertiary container", "Tertiary", "Text on tertiary-container backgrounds" ),

		new( Background, "Background", "Surfaces", "Behind every page and window" ),
		new( Surface, "Surface", "Surfaces", "The file list, inspector and other outlined panels" ),
		new( SurfaceContainerLow, "Surface container (low)", "Surfaces", "Tinted panes: folders, preview, banners, table headers" ),
		new( SurfaceContainer, "Surface container", "Surfaces", "The toolbar and other bars" ),
		new( SurfaceContainerHigh, "Surface container (high)", "Surfaces", "Image wells, hovered tabs, raised fills" ),
		new( SurfaceVariant, "Surface variant", "Surfaces", "Subtle fills: inactive inputs, hovered controls" ),
		new( SurfaceInverse, "Surface (inverse)", "Surfaces", "Snackbars and tooltips that stand out from the page" ),

		new( OnBackground, "On background", "Text", "Text placed directly on the page background" ),
		new( OnSurface, "On surface", "Text", "Most text: labels, list rows, titles" ),
		new( OnSurfaceVariant, "On surface variant", "Text", "Secondary text: hints, column headers, counts, icons" ),
		new( OnSurfaceInverse, "On surface (inverse)", "Text", "Text on inverse surfaces" ),

		new( Outline, "Outline", "Borders", "Outlined buttons and text fields" ),
		new( OutlineVariant, "Outline variant", "Borders", "Panel borders, dividers, grid lines, chip outlines" ),

		new( Error, "Error", "Status", "Errors and destructive actions" ),
		new( OnError, "On error", "Status", "Text on error-colored backgrounds" ),
		new( ErrorContainer, "Error container", "Status", "Error banners and highlights" ),
		new( OnErrorContainer, "On error container", "Status", "Text on error banners" ),
	];

	/// <summary>
	/// The role names in scheme order: the order <see cref="Scheme"/> takes colors in, matching the Figma Colors page
	/// (primary, secondary, tertiary, error, background and surfaces, outlines, surface containers).
	/// </summary>
	public static IReadOnlyList<string> Names { get; } =
	[
		Primary, OnPrimary, PrimaryContainer, OnPrimaryContainer, PrimaryInverse,
		Secondary, OnSecondary, SecondaryContainer, OnSecondaryContainer,
		Tertiary, OnTertiary, TertiaryContainer, OnTertiaryContainer,
		Error, OnError, ErrorContainer, OnErrorContainer,
		Background, OnBackground, Surface, OnSurface, SurfaceVariant, OnSurfaceVariant, SurfaceInverse, OnSurfaceInverse,
		Outline, OutlineVariant,
		SurfaceContainerLow, SurfaceContainer, SurfaceContainerHigh,
	];

	private static readonly FrozenSet<string> NameSet = Names.ToFrozenSet( StringComparer.Ordinal );

	/// <summary>Whether <paramref name="role"/> is one of the roles in <see cref="All"/>.</summary>
	public static bool IsRole( string? role ) => role is not null && NameSet.Contains( role );

	/// <summary>Whether the role comes from App.xaml (Hack-prefixed) rather than Uno Material.</summary>
	public static bool IsAppRole( string role ) => role.StartsWith( "SurfaceContainer", StringComparison.Ordinal );

	/// <summary>The Color resource key of a role: <c>PrimaryColor</c>, or <c>HackSurfaceContainerLowColor</c> for the app's own roles.</summary>
	public static string ColorKey( string role ) => IsAppRole( role ) ? $"Hack{role}Color" : $"{role}Color";

	/// <summary>The Brush resource key of a role: <c>PrimaryBrush</c>, or <c>HackSurfaceContainerLowBrush</c> for the app's own roles.</summary>
	public static string BrushKey( string role ) => IsAppRole( role ) ? $"Hack{role}Brush" : $"{role}Brush";

	/// <summary>Builds a scheme from colors listed in <see cref="Names"/> order.</summary>
	/// <exception cref="ArgumentException">When the count does not match the number of roles.</exception>
	public static FrozenDictionary<string, uint> Scheme( params uint[] colors )
	{
		if( colors.Length != Names.Count )
			throw new ArgumentException( $"A scheme needs {Names.Count} colors, got {colors.Length}.", nameof( colors ) );
		Dictionary<string, uint> scheme = new( StringComparer.Ordinal );
		for( int i = 0; i < colors.Length; i++ )
			scheme[ Names[ i ] ] = colors[ i ];
		return scheme.ToFrozenDictionary( StringComparer.Ordinal );
	}

	/// <summary>
	/// The app's own control brushes (Styles/HackControls.xaml) and the role each takes: tab strips, table headers,
	/// tree selection. They are recolored with the preset, like the role brushes.
	/// </summary>
	public static FrozenDictionary<string, string> AppBrushes { get; } = new Dictionary<string, string>( StringComparer.Ordinal )
	{
		[ "TabViewItemHeaderBackgroundPointerOver" ] = SurfaceContainerHigh,
		[ "TabViewItemHeaderBackgroundPressed" ] = SurfaceContainerHigh,
		[ "TabViewItemHeaderForeground" ] = OnSurfaceVariant,
		[ "TabViewItemHeaderForegroundPointerOver" ] = OnSurface,
		[ "TabViewItemHeaderForegroundPressed" ] = OnSurface,
		[ "TabViewItemHeaderForegroundSelected" ] = Primary,
		[ "HackSecondaryTabForegroundSelectedBrush" ] = OnSurface,
		[ "TabViewBorderBrush" ] = OutlineVariant,
		[ "TableViewHeaderRowBackground" ] = SurfaceContainerLow,
		[ "TableViewHeaderForeground" ] = OnSurfaceVariant,
		[ "TreeViewItemBackgroundSelected" ] = SecondaryContainer,
		[ "TreeViewItemBackgroundSelectedPointerOver" ] = SecondaryContainer,
		[ "TreeViewItemBackgroundSelectedPressed" ] = SecondaryContainer,
		[ "TreeViewItemForegroundSelected" ] = OnSecondaryContainer,
		[ "TreeViewItemForegroundSelectedPointerOver" ] = OnSecondaryContainer,
		[ "TreeViewItemForegroundSelectedPressed" ] = OnSecondaryContainer,
	}.ToFrozenDictionary( StringComparer.Ordinal );

	/// <summary>The gradient brush whose last two stops draw the selected tab's underline (set to <see cref="Primary"/>).</summary>
	public const string TabUnderlineBrushKey = "TabViewItemHeaderBackgroundSelected";

	/// <summary>
	/// The Windows (Fluent) brushes, as 0xAARRGGBB, that follow a scheme: tree views, tab views, tables, menus,
	/// breadcrumbs and any page not yet restyled still draw with these.
	/// </summary>
	/// <param name="scheme">Role → color; every role in <see cref="Names"/> must be present.</param>
	public static FrozenDictionary<string, uint> FluentBrushes( IReadOnlyDictionary<string, uint> scheme )
	{
		uint R( string role ) => scheme[ role ];
		static uint Alpha( uint color, byte alpha ) => (uint)alpha << 24 | ( color & 0x00FFFFFF );
		return new Dictionary<string, uint>( StringComparer.Ordinal )
		{
			[ "TextFillColorPrimaryBrush" ] = R( OnSurface ),
			[ "TextFillColorSecondaryBrush" ] = R( OnSurfaceVariant ),
			[ "TextFillColorTertiaryBrush" ] = R( Outline ),
			[ "TextFillColorDisabledBrush" ] = Alpha( R( OnSurface ), 0x61 ),
			[ "TextOnAccentFillColorPrimaryBrush" ] = R( OnPrimary ),
			[ "AccentFillColorDefaultBrush" ] = R( Primary ),
			[ "AccentFillColorSecondaryBrush" ] = Alpha( R( Primary ), 0xE6 ),
			[ "AccentFillColorTertiaryBrush" ] = Alpha( R( Primary ), 0xCC ),
			[ "AccentTextFillColorPrimaryBrush" ] = R( Primary ),
			[ "ApplicationPageBackgroundThemeBrush" ] = R( Background ),
			[ "SolidBackgroundFillColorBaseBrush" ] = R( Surface ),
			[ "LayerFillColorDefaultBrush" ] = R( SurfaceContainerLow ),
			[ "CardBackgroundFillColorDefaultBrush" ] = R( SurfaceContainerLow ),
			[ "SubtleFillColorSecondaryBrush" ] = Alpha( R( OnSurface ), 0x14 ),
			[ "ControlFillColorDefaultBrush" ] = R( SurfaceContainerHigh ),
			[ "ControlFillColorSecondaryBrush" ] = R( SurfaceVariant ),
			[ "ControlFillColorTertiaryBrush" ] = R( SurfaceContainer ),
			[ "ControlFillColorDisabledBrush" ] = Alpha( R( OnSurface ), 0x1F ),
			[ "ControlFillColorInputActiveBrush" ] = R( Surface ),
			[ "ControlStrokeColorDefaultBrush" ] = R( OutlineVariant ),
			[ "ControlStrongStrokeColorDefaultBrush" ] = R( Outline ),
			[ "CardStrokeColorDefaultBrush" ] = R( OutlineVariant ),
			[ "DividerStrokeColorDefaultBrush" ] = R( OutlineVariant ),
			[ "SurfaceStrokeColorDefaultBrush" ] = R( OutlineVariant ),
			// older system brushes some controls still use (side panes, list highlights)
			[ "SystemControlPageBackgroundChromeLowBrush" ] = R( SurfaceContainerLow ),
			[ "SystemControlBackgroundChromeMediumLowBrush" ] = R( SurfaceContainer ),
			[ "SystemControlHighlightAccentBrush" ] = R( Primary ),
			[ "SystemControlHighlightListAccentLowBrush" ] = R( SecondaryContainer ),
			[ "SystemControlHighlightListAccentMediumBrush" ] = R( SecondaryContainer ),
		}.ToFrozenDictionary( StringComparer.Ordinal );
	}
}
