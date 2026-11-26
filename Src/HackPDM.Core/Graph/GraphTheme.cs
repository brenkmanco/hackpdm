using System.Collections.Frozen;

using HackPDM.Core.Theming;

namespace HackPDM.Core.Graph;

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
	PaletteName Name,
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
	IReadOnlyDictionary<NodeKind, uint> Headers ) : INamedPreset<PaletteName>
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
	FormName Name,
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
	bool ShowGrid ) : INamedPreset<FormName>;

/// <summary>A palette and a form: chosen separately, so any colors go with any shapes.</summary>
public sealed record GraphTheme( GraphPalette Palette, GraphForm Form )
{
	/// <summary>Dark colors, default shapes: what the graph looked like before themes.</summary>
	public static GraphTheme Default { get; } = new( GraphThemes.Palette( PaletteName.Dark ), GraphThemes.Form( FormName.Default ) );
}

/// <summary>The built-in palettes and forms.</summary>
public static class GraphThemes
{
	/// <summary>
	/// The colors that mark a file's kind and the boxes' shadow, per palette. They carry meaning, so they stay fixed
	/// while everything else follows the Material scheme (see <see cref="FromScheme"/>).
	/// </summary>
	private static readonly FrozenDictionary<PaletteName, (IReadOnlyDictionary<NodeKind, uint> Headers, uint Shadow)> KindColors =
		new Dictionary<PaletteName, (IReadOnlyDictionary<NodeKind, uint>, uint)>
		{
			[ PaletteName.Dark ] = ( new Dictionary<NodeKind, uint>
			{
				[ NodeKind.Assembly ] = 0xFF2E7D46, [ NodeKind.Part ] = 0xFF285FAA, [ NodeKind.Drawing ] = 0xFF783C96,
				[ NodeKind.Pdf ] = 0xFFAA4632, [ NodeKind.Other ] = 0xFF555F69, [ NodeKind.Deleted ] = 0xFF8C2828,
			}.ToFrozenDictionary(), 0x80000000 ),
			[ PaletteName.Light ] = ( new Dictionary<NodeKind, uint>
			{
				[ NodeKind.Assembly ] = 0xFF2D7A47, [ NodeKind.Part ] = 0xFF2563B8, [ NodeKind.Drawing ] = 0xFF7B4BA6,
				[ NodeKind.Pdf ] = 0xFFB4462F, [ NodeKind.Other ] = 0xFF5B6470, [ NodeKind.Deleted ] = 0xFFA3302F,
			}.ToFrozenDictionary(), 0x30000000 ),
			[ PaletteName.NavyBlue ] = ( new Dictionary<NodeKind, uint>
			{
				[ NodeKind.Assembly ] = 0xFF1C7A63, [ NodeKind.Part ] = 0xFF2F62C8, [ NodeKind.Drawing ] = 0xFF7D52BE,
				[ NodeKind.Pdf ] = 0xFFB04E39, [ NodeKind.Other ] = 0xFF4B6584, [ NodeKind.Deleted ] = 0xFF9E2A2A,
			}.ToFrozenDictionary(), 0x90000814 ),
		}.ToFrozenDictionary();

	/// <summary>Every palette, in menu order: each app palette's Material scheme as graph colors.</summary>
	public static IReadOnlyList<GraphPalette> Palettes { get; } =
		[ .. ThemePresets.Palettes.Select( p => FromScheme( p.Name, p.Title, p.Scheme ) ) ];

	/// <summary>
	/// The graph colors for a Material scheme (role → 0xAARRGGBB, see <see cref="MaterialRoles"/>): the canvas is the
	/// Surface, boxes are Surface Container with an Outline Variant border, links are Outline, highlighted links, ports
	/// and the clicked box use Primary, and the chosen files' border is Tertiary. The kind colors stay fixed.
	/// </summary>
	public static GraphPalette FromScheme( PaletteName name, string title, IReadOnlyDictionary<string, uint> scheme )
	{
		(IReadOnlyDictionary<NodeKind, uint> headers, uint shadow) = KindColors[ Enum.IsDefined( name ) ? name : PaletteName.Dark ];
		uint R( string role ) => scheme[ role ];
		return new GraphPalette( name, title,
			Backdrop: R( MaterialRoles.Surface ), GridLine: R( MaterialRoles.SurfaceContainerLow ),
			Body: R( MaterialRoles.SurfaceContainer ), BodyBorder: R( MaterialRoles.OutlineVariant ),
			Text: R( MaterialRoles.OnSurface ), HeaderText: 0xFFFFFFFF, SubText: R( MaterialRoles.OnSurfaceVariant ),
			Link: R( MaterialRoles.Outline ), LinkHighlight: R( MaterialRoles.Primary ), Port: R( MaterialRoles.Primary ),
			RootBorder: R( MaterialRoles.Tertiary ), SelectedBorder: R( MaterialRoles.Primary ), Shadow: shadow,
			Headers: headers );
	}

	/// <summary>Every form, in menu order.</summary>
	public static IReadOnlyList<GraphForm> Forms { get; } =
	[
		new( FormName.Compact, nameof( FormName.Compact ),
			new LayoutOptions { ColumnGap = 70, RowGap = 12, Margin = 24, MinWidth = 130, MinHeight = 30, MaxWidth = 200, MaxHeight = 52 },
			HeaderStyle.Band, LinkStyle.Curve, HeaderHeight: 30, CornerRadius: 3, BorderThickness: 1, RootBorderThickness: 2,
			TitleSize: 11, ShowFolder: false, ShowSizeLine: false, PortSize: 7, HollowPorts: false, LinkThickness: 1.5,
			ShadowDepth: 0, ShowGrid: false ),
		new( FormName.Default, nameof( FormName.Default ),
			new LayoutOptions(),
			HeaderStyle.Band, LinkStyle.Curve, HeaderHeight: 26, CornerRadius: 6, BorderThickness: 1.5, RootBorderThickness: 2.5,
			TitleSize: 12.5, ShowFolder: true, ShowSizeLine: true, PortSize: 11, HollowPorts: false, LinkThickness: 2,
			ShadowDepth: 0, ShowGrid: false ),
		new( FormName.Modern, nameof( FormName.Modern ),
			new LayoutOptions { ColumnGap = 140, RowGap = 32, Margin = 48, MinWidth = 180, MinHeight = 64, MaxWidth = 300, MaxHeight = 128 },
			HeaderStyle.Strip, LinkStyle.Elbow, HeaderHeight: 5, CornerRadius: 12, BorderThickness: 1, RootBorderThickness: 2,
			TitleSize: 13, ShowFolder: true, ShowSizeLine: true, PortSize: 10, HollowPorts: true, LinkThickness: 1.6,
			ShadowDepth: 3, ShowGrid: true ),
		new( FormName.Fancy, nameof( FormName.Fancy ),
			new LayoutOptions { ColumnGap = 150, RowGap = 36, Margin = 56, MinWidth = 190, MinHeight = 72, MaxWidth = 310, MaxHeight = 140 },
			HeaderStyle.Gradient, LinkStyle.GlowCurve, HeaderHeight: 30, CornerRadius: 9, BorderThickness: 1.5, RootBorderThickness: 3,
			TitleSize: 13, ShowFolder: true, ShowSizeLine: true, PortSize: 13, HollowPorts: false, LinkThickness: 2.4,
			ShadowDepth: 6, ShowGrid: true ),
	];

	/// <summary>The palette named <paramref name="name"/>.</summary>
	public static GraphPalette Palette( PaletteName name )
		=> EnumPresets.Find( Palettes, Enum.IsDefined( name ) ? name : PaletteName.Dark, "palette" );

	/// <summary>The form named <paramref name="name"/>.</summary>
	public static GraphForm Form( FormName name )
		=> EnumPresets.Find( Forms, Enum.IsDefined( name ) ? name : FormName.Default, "form" );

	/// <summary>The theme for two saved names; an unknown or missing name falls back to Dark / Default.</summary>
	public static GraphTheme From( string? palette, string? form ) => From( ThemePresets.ParsePalette( palette ), ThemePresets.ParseForm( form ) );

	/// <summary>The graph's colors and shapes for the app's palette and form.</summary>
	public static GraphTheme From( PaletteName palette, FormName form ) => new( Palette( palette ), Form( form ) );

	/// <summary>
	/// The graph's colors and shapes for the app's palette and form, with the colors taken from <paramref name="scheme"/>
	/// (the palette's scheme with any Theme Editor changes).
	/// </summary>
	public static GraphTheme From( PaletteName palette, FormName form, IReadOnlyDictionary<string, uint> scheme )
	{
		GraphPalette preset = Palette( palette );
		GraphPalette colors = FromScheme( preset.Name, preset.Title, scheme );
		// keep the cached instance while nothing changed, so the view can tell a real change from a repeat
		return new( colors == preset ? preset : colors, Form( form ) );
	}

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

	/// <summary>The WCAG contrast ratio of two colors (1 to 21).</summary>
	public static double Contrast( uint front, uint back ) => ThemePresets.Contrast( front, back );
}
