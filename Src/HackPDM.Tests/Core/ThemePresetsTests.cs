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
		Assert.True( ThemePresets.Contrast( p.Text, p.PageBackground ) >= 7, "text on the page" );
		Assert.True( ThemePresets.Contrast( p.PanelStroke, p.PageBackground ) >= 1.4, "panel borders show" );
		Assert.True( ThemePresets.Contrast( p.FancyPanelStroke, p.PageBackground ) >= 3, "accent panel borders" );
		if( p.Brushes.Count == 0 )
			return;
		// a recolored theme keeps text, hints and accent buttons readable on its own backgrounds
		uint page = p.Brushes[ "ApplicationPageBackgroundThemeBrush" ];
		Assert.Equal( page, p.PageBackground );
		Assert.Equal( p.Text, p.Brushes[ "TextFillColorPrimaryBrush" ] );
		Assert.True( ThemePresets.Contrast( p.Brushes[ "TextFillColorSecondaryBrush" ], page ) >= 4.5, "secondary text" );
		Assert.True( ThemePresets.Contrast( p.Brushes[ "TextFillColorPrimaryBrush" ], p.Brushes[ "SolidBackgroundFillColorBaseBrush" ] ) >= 7, "text on panes" );
		Assert.True( ThemePresets.Contrast( p.Brushes[ "TextOnAccentFillColorPrimaryBrush" ], p.Brushes[ "AccentFillColorDefaultBrush" ] ) >= 4.5, "text on accent buttons" );
		Assert.True( ThemePresets.Contrast( p.Brushes[ "AccentFillColorDefaultBrush" ], page ) >= 3, "accent on the page" );
		Assert.True( ThemePresets.Contrast( p.Brushes[ "AccentTextFillColorPrimaryBrush" ], page ) >= 4.5, "links" );
		Assert.All( p.Brushes.Keys, key => Assert.EndsWith( "Brush", key ) );
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
	public void Forms_GetRounderInOrder_AndDefaultIsTodaysLook()
	{
		double[] corners = [ .. ThemePresets.Forms.Select( f => f.ControlCornerRadius ) ];
		Assert.Equal( corners.Order(), corners );
		double[] panels = [ .. ThemePresets.Forms.Select( f => f.PanelCornerRadius ) ];
		Assert.Equal( panels.Order(), panels );
		Assert.All( ThemePresets.Forms, f => Assert.True( f.OverlayCornerRadius >= f.ControlCornerRadius ) );

		// Default is what the app looked like before the presets: Fluent's corners, compact density, 3px / 10px panels
		AppForm normal = ThemePresets.Form( FormName.Default );
		Assert.Equal( (4.0, 8.0, true, 10.0, 3.0, false),
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
}
