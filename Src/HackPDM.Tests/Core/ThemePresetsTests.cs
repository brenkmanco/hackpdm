using System;
using System.Linq;

using HackPDM.Core.Graph;
using HackPDM.Core.Theming;

using Xunit;

namespace HackPDM.Tests.Core;

public class ThemePresetsTests
{
	public static TheoryData<PaletteName> PaletteNames => [ .. Enum.GetValues<PaletteName>() ];

	[Fact]
	public void TheChosenPresetsExist_ForTheAppAndTheGraph()
	{
		Assert.Equal( [ "Dark", "Light", "Navy blue" ], ThemePresets.Palettes.Select( p => p.Title ) );
		Assert.Equal( [ "Compact", "Default", "Modern", "Fancy" ], ThemePresets.Forms.Select( f => f.Title ) );
		// every app preset has a graph counterpart with the same name
		Assert.Equal( ThemePresets.Palettes.Select( p => p.Name ), GraphThemes.Palettes.Select( p => p.Name ) );
		Assert.Equal( ThemePresets.Forms.Select( f => f.Name ), GraphThemes.Forms.Select( f => f.Name ) );
	}

	[Theory]
	[MemberData( nameof( PaletteNames ) )]
	public void Palette_TextAndAccentAreReadable( PaletteName name )
	{
		AppPalette p = ThemePresets.Palette( name );
		uint R( string role ) => p.Scheme[ role ];
		Assert.True( ThemePresets.Contrast( p.Text, p.PageBackground ) >= 7, "text on the page" );
		Assert.True( ThemePresets.Contrast( p.PanelStroke, p.PageBackground ) >= 1.4, "panel borders show" );
		Assert.True( ThemePresets.Contrast( p.FancyPanelStroke, p.PageBackground ) >= 3, "accent panel borders" );
		Assert.True( ThemePresets.Contrast( R( MaterialRoles.OnSurface ), R( MaterialRoles.Surface ) ) >= 7, "text on panes" );
		Assert.True( ThemePresets.Contrast( R( MaterialRoles.OnSurface ), R( MaterialRoles.SurfaceContainerHigh ) ) >= 7, "text on raised fills" );
		Assert.True( ThemePresets.Contrast( R( MaterialRoles.OnSurfaceVariant ), R( MaterialRoles.SurfaceContainerLow ) ) >= 4.5, "secondary text on tinted panes" );
		Assert.True( ThemePresets.Contrast( R( MaterialRoles.OnPrimary ), R( MaterialRoles.Primary ) ) >= 4.5, "text on filled buttons" );
		Assert.True( ThemePresets.Contrast( R( MaterialRoles.OnSecondaryContainer ), R( MaterialRoles.SecondaryContainer ) ) >= 4.5, "text on selected rows" );
		Assert.True( ThemePresets.Contrast( R( MaterialRoles.OnPrimaryContainer ), R( MaterialRoles.PrimaryContainer ) ) >= 4.5, "text on tonal buttons" );
		Assert.True( ThemePresets.Contrast( R( MaterialRoles.OnErrorContainer ), R( MaterialRoles.ErrorContainer ) ) >= 4.5, "text on error banners" );
		Assert.True( ThemePresets.Contrast( R( MaterialRoles.Primary ), p.PageBackground ) >= 4.5, "links and the selected tab" );
	}

	[Theory]
	[MemberData( nameof( PaletteNames ) )]
	public void Palette_DefinesEveryRole_AndDrivesTheFluentBrushes( PaletteName name )
	{
		AppPalette p = ThemePresets.Palette( name );
		Assert.Equal( MaterialRoles.Names.Order(), p.Scheme.Keys.Order() );
		Assert.All( p.Scheme.Values, argb => Assert.Equal( 0xFFu, argb >> 24 ) );
		// the Windows-style brushes follow the scheme, so controls without a Material style match the preset
		Assert.Equal( p.Scheme[ MaterialRoles.Background ], p.Brushes[ "ApplicationPageBackgroundThemeBrush" ] );
		Assert.Equal( p.Scheme[ MaterialRoles.OnSurface ], p.Brushes[ "TextFillColorPrimaryBrush" ] );
		Assert.Equal( p.Scheme[ MaterialRoles.Primary ], p.Brushes[ "AccentFillColorDefaultBrush" ] );
		Assert.True( ThemePresets.Contrast( p.Brushes[ "TextOnAccentFillColorPrimaryBrush" ], p.Brushes[ "AccentFillColorDefaultBrush" ] ) >= 4.5, "text on accent buttons" );
		Assert.All( p.Brushes.Keys, key => Assert.EndsWith( "Brush", key ) );
	}

	[Fact]
	public void Palettes_MatchTheFigmaColors()
	{
		// spot checks against the "HackPDM colors" variables in Figma, so a value can't drift onto the wrong role
		IReadOnlyDictionary<string, uint> light = ThemePresets.Palette( PaletteName.Light ).Scheme;
		IReadOnlyDictionary<string, uint> dark = ThemePresets.Palette( PaletteName.Dark ).Scheme;
		IReadOnlyDictionary<string, uint> navy = ThemePresets.Palette( PaletteName.NavyBlue ).Scheme;
		Assert.Equal( 0xFF485E92u, light[ MaterialRoles.Primary ] );
		Assert.Equal( 0xFFC5C6D0u, light[ MaterialRoles.OutlineVariant ] );
		Assert.Equal( 0xFFF4F3FAu, light[ MaterialRoles.SurfaceContainerLow ] );
		Assert.Equal( 0xFF141218u, dark[ MaterialRoles.Background ] );
		Assert.Equal( 0xFF282A2Fu, dark[ MaterialRoles.SurfaceContainerHigh ] );
		Assert.Equal( 0xFFF2B134u, navy[ MaterialRoles.Primary ] );
		Assert.Equal( 0xFF2A4570u, navy[ MaterialRoles.OutlineVariant ] );
		Assert.Equal( 0xFF0F2240u, navy[ MaterialRoles.SurfaceContainerLow ] );
	}

	[Fact]
	public void EditorCatalog_IsTheMaterialRoles()
	{
		Assert.Equal( MaterialRoles.Names.Order(), ThemeColorCatalog.All.Select( k => k.Key ).Order() );
		// each editor section is listed together, so it gets one heading
		string[] groups = [ .. ThemeColorCatalog.All.Select( k => k.Group ) ];
		Assert.Equal( groups.Distinct().Count(), groups.Where( ( g, i ) => i == 0 || groups[ i - 1 ] != g ).Count() );
		Assert.Equal( MaterialRoles.Names.Count, MaterialRoles.Names.Distinct().Count() );
		Assert.Equal( "HackSurfaceContainerLowBrush", MaterialRoles.BrushKey( MaterialRoles.SurfaceContainerLow ) );
		Assert.Equal( "PrimaryColor", MaterialRoles.ColorKey( MaterialRoles.Primary ) );
		Assert.False( MaterialRoles.IsRole( "TextFillColorPrimaryBrush" ) );
		// every shared control brush in Styles/HackControls.xaml maps to a real role
		Assert.All( MaterialRoles.AppBrushes.Values, role => Assert.True( MaterialRoles.IsRole( role ), role ) );
	}

	[Fact]
	public void Palette_MatchesItsGraphColors()
	{
		// the page and the graph share a background family: dark palettes draw dark graphs, light ones light graphs
		foreach( AppPalette p in ThemePresets.Palettes )
		{
			GraphPalette g = GraphThemes.Palette( p.Name );
			Assert.Equal( p.IsDark, ThemePresets.Contrast( g.Backdrop, 0xFF000000 ) < ThemePresets.Contrast( g.Backdrop, 0xFFFFFFFF ) );
		}
	}

	[Fact]
	public void Forms_GetRounderInOrder_AndDefaultIsTheFigmaLook()
	{
		double[] corners = [ .. ThemePresets.Forms.Select( f => f.ControlCornerRadius ) ];
		Assert.Equal( corners.Order(), corners );
		double[] panels = [ .. ThemePresets.Forms.Select( f => f.PanelCornerRadius ) ];
		Assert.Equal( panels.Order(), panels );
		Assert.All( ThemePresets.Forms, f => Assert.True( f.OverlayCornerRadius >= f.ControlCornerRadius ) );

		// Default is the Penpot design: standard Material density, 16px panel corners and a 1px panel border
		AppForm normal = ThemePresets.Form( FormName.Default );
		Assert.Equal( (4.0, 8.0, false, 16.0, 1.0, false),
			(normal.ControlCornerRadius, normal.OverlayCornerRadius, normal.CompactDensity, normal.PanelCornerRadius, normal.PanelBorderThickness, normal.AccentPanels) );
		Assert.True( ThemePresets.Form( FormName.Compact ).CompactDensity );
		Assert.False( ThemePresets.Form( FormName.Modern ).CompactDensity );
		Assert.True( ThemePresets.Form( FormName.Fancy ).AccentPanels );
	}

	[Fact]
	public void PanelStroke_TakesTheAccentOnlyInFancy()
	{
		AppPalette navy = ThemePresets.Palette( PaletteName.NavyBlue );
		Assert.Equal( navy.FancyPanelStroke, ThemePresets.PanelStroke( navy, ThemePresets.Form( FormName.Fancy ) ) );
		Assert.Equal( navy.PanelStroke, ThemePresets.PanelStroke( navy, ThemePresets.Form( FormName.Modern ) ) );
	}

	[Theory]
	[InlineData( "NavyBlue", PaletteName.NavyBlue )]
	[InlineData( "light", PaletteName.Dark )]
	[InlineData( "2", PaletteName.Dark )]
	[InlineData( "", PaletteName.Dark )]
	[InlineData( null, PaletteName.Dark )]
	public void ParsePalette( string? text, PaletteName expected ) => Assert.Equal( expected, ThemePresets.ParsePalette( text ) );

	[Theory]
	[InlineData( "Fancy", FormName.Fancy )]
	[InlineData( "Huge", FormName.Default )]
	[InlineData( "0", FormName.Default )]
	public void ParseForm( string? text, FormName expected ) => Assert.Equal( expected, ThemePresets.ParseForm( text ) );

	[Fact]
	public void Blend_AndContrast()
	{
		Assert.Equal( 0xFF808080u, ThemePresets.Blend( 0x80FFFFFF, 0xFF000000 ) );
		Assert.Equal( 0xFF123456u, ThemePresets.Blend( 0xFF123456, 0xFFFFFFFF ) );
		Assert.Equal( 21, ThemePresets.Contrast( 0xFF000000, 0xFFFFFFFF ), 3 );
	}

	[Fact]
	public void OutOfRangeEnumCasts_FallBackToDefaultPresets()
	{
		Assert.Equal( PaletteName.Dark, ThemePresets.Palette( (PaletteName)99 ).Name );
		Assert.Equal( FormName.Default, ThemePresets.Form( (FormName)99 ).Name );
	}
}
