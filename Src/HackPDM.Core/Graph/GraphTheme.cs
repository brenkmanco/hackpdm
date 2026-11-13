namespace HackPDM.Core.Graph;

/// <summary>The color presets of the dependency graph.</summary>
public enum GraphPaletteName
{
	Dark,
	Light,
	NavyBlue,
}

/// <summary>The shape presets of the dependency graph: box sizes, spacing, corners, links and ports.</summary>
public enum GraphFormName
{
	Compact,
	Default,
	Modern,
	Fancy,
}

/// <summary>How the colored part of a box is drawn.</summary>
public enum HeaderStyle
{
	/// <summary>A filled band across the top holding the name.</summary>
	Band,
	/// <summary>A thin colored strip along the top; the name sits in the body.</summary>
	Strip,
	/// <summary>A band shaded from the type color to a darker tone, with the extension as a badge.</summary>
	Gradient,
}

/// <summary>How a link is drawn between a parent's output and a child's input.</summary>
public enum LinkStyle
{
	/// <summary>An S-shaped curve.</summary>
	Curve,
	/// <summary>Horizontal, vertical, horizontal, with rounded bends.</summary>
	Elbow,
	/// <summary>An S-shaped curve with a soft glow under it.</summary>
	GlowCurve,
}

/// <summary>A file's kind, for its color.</summary>
public enum NodeKind
{
	Assembly,
	Part,
	Drawing,
	Pdf,
	Other,
	Deleted,
}

/// <summary>The colors of the graph, as 0xAARRGGBB.</summary>
public sealed record GraphPalette(
	GraphPaletteName Name,
	string Title,
	uint Backdrop,
	uint GridLine,
	uint Body,
	uint BodyBorder,
	uint Text,
	uint HeaderText,
	uint SubText,
	uint Link,
	uint LinkHighlight,
	uint Port,
	uint RootBorder,
	uint SelectedBorder,
	uint Shadow,
	IReadOnlyDictionary<NodeKind, uint> Headers )
{
	/// <summary>The header color of a file of <paramref name="kind"/>.</summary>
	public uint Header( NodeKind kind ) => Headers[ kind ];
}

/// <summary>The shapes of the graph.</summary>
/// <param name="Layout">Box sizes and spacing.</param>
/// <param name="HeaderHeight">The height of the header band (or strip).</param>
/// <param name="CornerRadius">How round the box corners are.</param>
/// <param name="BorderThickness">The box border; the selected files' border is <see cref="RootBorderThickness"/>.</param>
/// <param name="TitleSize">The file name's font size for the smallest file; bigger files get up to 35% more.</param>
/// <param name="ShowFolder">Whether the folder line is shown when there is room.</param>
/// <param name="ShowSizeLine">Whether the size line is shown (compact boxes put the size in the title row).</param>
/// <param name="PortSize">The diameter of the port dots.</param>
/// <param name="HollowPorts">Ring ports instead of filled dots.</param>
/// <param name="LinkThickness">The link's stroke.</param>
/// <param name="ShadowDepth">How far the box's shadow falls; 0 for none.</param>
/// <param name="ShowGrid">Whether a grid is drawn behind the graph.</param>
public sealed record GraphForm(
	GraphFormName Name,
	string Title,
	LayoutOptions Layout,
	HeaderStyle Header,
	LinkStyle Links,
	double HeaderHeight,
	double CornerRadius,
	double BorderThickness,
	double RootBorderThickness,
	double TitleSize,
	bool ShowFolder,
	bool ShowSizeLine,
	double PortSize,
	bool HollowPorts,
	double LinkThickness,
	double ShadowDepth,
	bool ShowGrid );

/// <summary>A palette and a form: chosen separately, so any colors go with any shapes.</summary>
public sealed record GraphTheme( GraphPalette Palette, GraphForm Form )
{
	/// <summary>Dark colors, default shapes: what the graph looked like before themes.</summary>
	public static GraphTheme Default { get; } = new( GraphThemes.Palette( GraphPaletteName.Dark ), GraphThemes.Form( GraphFormName.Default ) );
}

/// <summary>The built-in palettes and forms.</summary>
public static class GraphThemes
{
	/// <summary>Every palette, in menu order.</summary>
	public static IReadOnlyList<GraphPalette> Palettes { get; } =
	[
		new( GraphPaletteName.Dark, "Dark",
			Backdrop: 0xFF1E1E20, GridLine: 0xFF2A2A2E, Body: 0xFF2E2E32, BodyBorder: 0xFF141416,
			Text: 0xFFDCDCE1, HeaderText: 0xFFFFFFFF, SubText: 0xFF96969E,
			Link: 0xC8C8C8CD, LinkHighlight: 0xFFFFAA3C, Port: 0xFFF08C1E,
			RootBorder: 0xFFFFA028, SelectedBorder: 0xFFFFFFFF, Shadow: 0x80000000,
			Headers: new Dictionary<NodeKind, uint>
			{
				[ NodeKind.Assembly ] = 0xFF2E7D46, [ NodeKind.Part ] = 0xFF285FAA, [ NodeKind.Drawing ] = 0xFF783C96,
				[ NodeKind.Pdf ] = 0xFFAA4632, [ NodeKind.Other ] = 0xFF555F69, [ NodeKind.Deleted ] = 0xFF8C2828,
			} ),
		new( GraphPaletteName.Light, "Light",
			Backdrop: 0xFFF3F4F6, GridLine: 0xFFE3E5E9, Body: 0xFFFFFFFF, BodyBorder: 0xFFC9CED6,
			Text: 0xFF1F2328, HeaderText: 0xFFFFFFFF, SubText: 0xFF59636E,
			Link: 0xFF8C959F, LinkHighlight: 0xFFD9630B, Port: 0xFFE07B12,
			RootBorder: 0xFFE07B12, SelectedBorder: 0xFF0969DA, Shadow: 0x30000000,
			Headers: new Dictionary<NodeKind, uint>
			{
				[ NodeKind.Assembly ] = 0xFF2D7A47, [ NodeKind.Part ] = 0xFF2563B8, [ NodeKind.Drawing ] = 0xFF7B4BA6,
				[ NodeKind.Pdf ] = 0xFFB4462F, [ NodeKind.Other ] = 0xFF5B6470, [ NodeKind.Deleted ] = 0xFFA3302F,
			} ),
		new( GraphPaletteName.NavyBlue, "Navy blue",
			Backdrop: 0xFF0B1A30, GridLine: 0xFF14274A, Body: 0xFF13294B, BodyBorder: 0xFF07111F,
			Text: 0xFFE6EDF7, HeaderText: 0xFFFFFFFF, SubText: 0xFF9FB3CF,
			Link: 0xFF6F8DB8, LinkHighlight: 0xFFF2B134, Port: 0xFFF2B134,
			RootBorder: 0xFFF2B134, SelectedBorder: 0xFFFFFFFF, Shadow: 0x90000814,
			Headers: new Dictionary<NodeKind, uint>
			{
				[ NodeKind.Assembly ] = 0xFF1C7A63, [ NodeKind.Part ] = 0xFF2F62C8, [ NodeKind.Drawing ] = 0xFF7D52BE,
				[ NodeKind.Pdf ] = 0xFFB04E39, [ NodeKind.Other ] = 0xFF4B6584, [ NodeKind.Deleted ] = 0xFF9E2A2A,
			} ),
	];

	/// <summary>Every form, in menu order.</summary>
	public static IReadOnlyList<GraphForm> Forms { get; } =
	[
		new( GraphFormName.Compact, "Compact",
			new LayoutOptions { ColumnGap = 70, RowGap = 12, Margin = 24, MinWidth = 130, MinHeight = 30, MaxWidth = 200, MaxHeight = 52 },
			HeaderStyle.Band, LinkStyle.Curve, HeaderHeight: 30, CornerRadius: 3, BorderThickness: 1, RootBorderThickness: 2,
			TitleSize: 11, ShowFolder: false, ShowSizeLine: false, PortSize: 7, HollowPorts: false, LinkThickness: 1.5,
			ShadowDepth: 0, ShowGrid: false ),
		new( GraphFormName.Default, "Default",
			new LayoutOptions(),
			HeaderStyle.Band, LinkStyle.Curve, HeaderHeight: 26, CornerRadius: 6, BorderThickness: 1.5, RootBorderThickness: 2.5,
			TitleSize: 12.5, ShowFolder: true, ShowSizeLine: true, PortSize: 11, HollowPorts: false, LinkThickness: 2,
			ShadowDepth: 0, ShowGrid: false ),
		new( GraphFormName.Modern, "Modern",
			new LayoutOptions { ColumnGap = 140, RowGap = 32, Margin = 48, MinWidth = 180, MinHeight = 64, MaxWidth = 300, MaxHeight = 128 },
			HeaderStyle.Strip, LinkStyle.Elbow, HeaderHeight: 5, CornerRadius: 12, BorderThickness: 1, RootBorderThickness: 2,
			TitleSize: 13, ShowFolder: true, ShowSizeLine: true, PortSize: 10, HollowPorts: true, LinkThickness: 1.6,
			ShadowDepth: 3, ShowGrid: true ),
		new( GraphFormName.Fancy, "Fancy",
			new LayoutOptions { ColumnGap = 150, RowGap = 36, Margin = 56, MinWidth = 190, MinHeight = 72, MaxWidth = 310, MaxHeight = 140 },
			HeaderStyle.Gradient, LinkStyle.GlowCurve, HeaderHeight: 30, CornerRadius: 9, BorderThickness: 1.5, RootBorderThickness: 3,
			TitleSize: 13, ShowFolder: true, ShowSizeLine: true, PortSize: 13, HollowPorts: false, LinkThickness: 2.4,
			ShadowDepth: 6, ShowGrid: true ),
	];

	/// <summary>The palette named <paramref name="name"/>.</summary>
	public static GraphPalette Palette( GraphPaletteName name ) => Palettes.First( p => p.Name == name );

	/// <summary>The form named <paramref name="name"/>.</summary>
	public static GraphForm Form( GraphFormName name ) => Forms.First( f => f.Name == name );

	/// <summary>The theme for two saved names; an unknown or missing name falls back to Dark / Default.</summary>
	public static GraphTheme From( string? palette, string? form ) => new(
		Palette( Enum.TryParse( palette, out GraphPaletteName p ) && Enum.IsDefined( p ) ? p : GraphPaletteName.Dark ),
		Form( Enum.TryParse( form, out GraphFormName f ) && Enum.IsDefined( f ) ? f : GraphFormName.Default ) );

	/// <summary>The kind of a file, from its extension and whether it is deleted.</summary>
	public static NodeKind KindOf( GraphNode node ) => node.Deleted
		? NodeKind.Deleted
		: node.Ext switch
		{
			"sldasm" or "asm" or "iam" => NodeKind.Assembly,
			"sldprt" or "prt" or "ipt" => NodeKind.Part,
			"slddrw" or "drw" or "idw" or "dwg" or "dxf" => NodeKind.Drawing,
			"pdf" => NodeKind.Pdf,
			_ => NodeKind.Other,
		};

	/// <summary>The WCAG contrast ratio of two opaque colors (1 to 21).</summary>
	public static double Contrast( uint a, uint b )
	{
		double la = Luminance( a ), lb = Luminance( b );
		return ( Math.Max( la, lb ) + 0.05 ) / ( Math.Min( la, lb ) + 0.05 );
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
