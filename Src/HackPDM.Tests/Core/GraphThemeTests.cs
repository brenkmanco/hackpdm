using System;
using System.Linq;

using HackPDM.Core.Graph;
using HackPDM.Core.Theming;

using Xunit;

namespace HackPDM.Tests.Core;

public class GraphThemeTests
{
	public static TheoryData<PaletteName> PaletteNames => [ .. Enum.GetValues<PaletteName>() ];
	public static TheoryData<FormName> FormNames => [ .. Enum.GetValues<FormName>() ];

	[Fact]
	public void TheChosenPresetsExist()
	{
		Assert.Equal( [ "Dark", "Light", "Navy blue" ], GraphThemes.Palettes.Select( p => p.Title ) );
		Assert.Equal( [ "Compact", "Default", "Modern", "Fancy" ], GraphThemes.Forms.Select( f => f.Title ) );
	}

	[Theory]
	[MemberData( nameof( PaletteNames ) )]
	public void Palette_IsReadable( PaletteName name )
	{
		GraphPalette p = GraphThemes.Palette( name );
		foreach( NodeKind kind in Enum.GetValues<NodeKind>() )
			Assert.True( GraphThemes.Contrast( p.HeaderText, p.Header( kind ) ) >= 4.5, $"{name}: header text on {kind}" );
		Assert.True( GraphThemes.Contrast( p.Text, p.Body ) >= 7, $"{name}: text" );
		Assert.True( GraphThemes.Contrast( p.SubText, p.Body ) >= 4.5, $"{name}: folder line" );
		Assert.True( GraphThemes.Contrast( p.Link | 0xFF000000, p.Backdrop ) >= 3, $"{name}: links" );
		Assert.True( GraphThemes.Contrast( p.LinkHighlight, p.Backdrop ) >= 3, $"{name}: highlighted links" );
		Assert.True( GraphThemes.Contrast( p.Port, p.Backdrop ) >= 2.5, $"{name}: ports" );
		Assert.True( GraphThemes.Contrast( p.SelectedBorder, p.Body ) >= 3, $"{name}: selected border" );
		Assert.True( GraphThemes.Contrast( p.RootBorder, p.Body ) >= 2.5, $"{name}: selected-file border" );
		Assert.True( GraphThemes.Contrast( p.GridLine, p.Backdrop ) < 1.5, $"{name}: the grid stays in the background" );
	}

	[Theory]
	[MemberData( nameof( FormNames ) )]
	public void Form_WorksWithEveryPalette_AndLaysOutWithoutOverlaps( FormName name )
	{
		GraphForm form = GraphThemes.Form( name );
		Assert.True( form.HeaderHeight <= form.Layout.MinHeight, "the header fits the smallest box" );
		Assert.True( form.Layout.MinWidth < form.Layout.MaxWidth && form.Layout.MinHeight < form.Layout.MaxHeight );
		foreach( GraphPalette palette in GraphThemes.Palettes )
			Assert.Equal( (palette, form), (new GraphTheme( palette, form ).Palette, new GraphTheme( palette, form ).Form) );

		GraphNode Node( int id, long size ) => new( id, $"f{id}.sldprt", "sldprt", size, "", id, false, false, $@"root\f{id}.sldprt" );
		var graph = new DependencyGraph( [ 1 ], [ Node( 1, 5_000_000 ), Node( 2, 200 ), Node( 3, 90_000_000 ), Node( 4, 40_000 ) ],
			[ new( 1, 2 ), new( 1, 3 ), new( 3, 4 ), new( 1, 4 ) ], false );
		var r = GraphLayout.Arrange( graph, GraphDirection.Children, form.Layout );
		foreach( var a in r.Nodes )
			foreach( var b in r.Nodes.Where( b => b != a ) )
				Assert.False( a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height );
	}

	[Fact]
	public void Compact_IsSmallerThanDefault_AndFancyRoomier()
	{
		LayoutOptions compact = GraphThemes.Form( FormName.Compact ).Layout;
		LayoutOptions normal = GraphThemes.Form( FormName.Default ).Layout;
		LayoutOptions fancy = GraphThemes.Form( FormName.Fancy ).Layout;
		Assert.True( compact.MaxWidth < normal.MaxWidth && compact.MaxHeight < normal.MaxHeight && compact.ColumnGap < normal.ColumnGap );
		Assert.True( fancy.ColumnGap > normal.ColumnGap && fancy.MinHeight > normal.MinHeight );
		Assert.Equal( new LayoutOptions(), normal );
	}

	[Theory]
	[InlineData( "NavyBlue", "Fancy", PaletteName.NavyBlue, FormName.Fancy )]
	[InlineData( "Light", "Compact", PaletteName.Light, FormName.Compact )]
	[InlineData( null, null, PaletteName.Dark, FormName.Default )]
	[InlineData( "Purple", "Huge", PaletteName.Dark, FormName.Default )]
	[InlineData( "7", "-1", PaletteName.Dark, FormName.Default )]
	public void From_SavedNames_FallsBackToDarkDefault( string? palette, string? form, PaletteName p, FormName f )
	{
		GraphTheme theme = GraphThemes.From( palette, form );
		Assert.Equal( (p, f), (theme.Palette.Name, theme.Form.Name) );
	}

	[Fact]
	public void Kinds_FromExtension()
	{
		GraphNode Node( string ext, bool deleted = false ) => new( 1, "x." + ext, ext, 1, "", 1, deleted, false, "" );
		Assert.Equal( NodeKind.Assembly, GraphThemes.KindOf( Node( "sldasm" ) ) );
		Assert.Equal( NodeKind.Part, GraphThemes.KindOf( Node( "sldprt" ) ) );
		Assert.Equal( NodeKind.Drawing, GraphThemes.KindOf( Node( "slddrw" ) ) );
		Assert.Equal( NodeKind.Pdf, GraphThemes.KindOf( Node( "pdf" ) ) );
		Assert.Equal( NodeKind.Other, GraphThemes.KindOf( Node( "txt" ) ) );
		Assert.Equal( NodeKind.Deleted, GraphThemes.KindOf( Node( "sldprt", deleted: true ) ) );
	}

	[Fact]
	public void Contrast_KnownValues()
	{
		Assert.Equal( 21, GraphThemes.Contrast( 0xFF000000, 0xFFFFFFFF ), 3 );
		Assert.Equal( 1, GraphThemes.Contrast( 0xFF336699, 0xFF336699 ), 6 );
	}
}
