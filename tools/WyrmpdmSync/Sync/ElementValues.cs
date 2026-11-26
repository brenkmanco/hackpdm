using System.Text.Json.Nodes;

using WyrmpdmSync.Penpot;

namespace WyrmpdmSync.Sync;

/// <summary>
/// The properties synced between a marked XAML element and its Penpot layer, read on both sides in the same form as
/// <see cref="StyleValues"/>. Bindings (<c>{x:Bind …}</c>) and resources other than brushes are left alone: they
/// are wiring, not design.
/// </summary>
internal static class ElementValues
{
	public const string Spacing = nameof( Spacing ), RowSpacing = nameof( RowSpacing ), ColumnSpacing = nameof( ColumnSpacing ),
		Text = nameof( Text ), Style = nameof( Style );

	public static readonly IReadOnlyList<string> All =
	[
		StyleValues.Padding, Spacing, RowSpacing, ColumnSpacing, StyleValues.CornerRadius, StyleValues.Width, StyleValues.Height,
		StyleValues.Background, StyleValues.BorderBrush, Text, Style,
	];

	/// <summary>
	/// Properties `push` doesn't write: sizes (Penpot's API doesn't re-run geometry) and the style (changing it means
	/// swapping the component in Penpot).
	/// </summary>
	public static readonly IReadOnlySet<string> PullOnly = new HashSet<string> { StyleValues.Width, StyleValues.Height, Style };

	/// <summary>The XAML attribute that holds a property on this element ("Text" on a TextBlock, "Label" on a NavRailItem, "Content" on a Button).</summary>
	public static string AttributeName( string tag, string property )
	{
		if( property != Text )
			return property;
		if( tag == "TextBlock" )
			return "Text";
		if( tag == "NavRailItem" || tag.EndsWith( ":NavRailItem", StringComparison.Ordinal ) )
			return "Label";
		return "Content";
	}

	// ---- code ----

	public static string? FromXaml( XamlElement element, string property )
	{
		string? raw = element.Attr( AttributeName( element.Tag, property ) );
		if( raw is null )
			return null;
		string text = XamlView.Unescape( raw ).Trim();
		bool markup = text.StartsWith( '{' );
		return property switch
		{
			Text => markup ? null : XamlView.Unescape( raw ),
			Style => markup && text.StartsWith( "{StaticResource ", StringComparison.Ordinal ) ? text[ 16..^1 ].Trim() : null,
			Spacing or RowSpacing or ColumnSpacing => markup ? null : Number.Normalize( text ),
			StyleValues.Background or StyleValues.BorderBrush => markup && !text.Contains( "Resource " ) ? null : StyleValues.FromXaml( property, text ),
			_ => markup ? null : StyleValues.FromXaml( property, text ),
		};
	}

	/// <summary>The attribute text that writes <paramref name="value"/>, keeping <paramref name="current"/> when it means the same.</summary>
	public static string ToXaml( string property, string value, string? current )
		=> property switch
		{
			Text => value,
			Style => $"{{StaticResource {value}}}",
			Spacing or RowSpacing or ColumnSpacing => value,
			_ => StyleValues.ToXaml( property, value, current ?? "" ),
		};

	// ---- Penpot ----

	/// <summary>A layer's value in the shared form; <paramref name="styleOf"/> names the XAML style a component stands for.</summary>
	public static string? FromShape( string property, JsonObject shape, JsonObject objects, string theme, Func<Guid, string?> styleOf )
	{
		switch( property )
		{
			case Spacing:
				return Gap( shape, shape[ "layout-flex-dir" ]?.ToString() is "row" or "row-reverse" ? "column-gap" : "row-gap" );
			case RowSpacing:
				return Gap( shape, "row-gap" );
			case ColumnSpacing:
				return Gap( shape, "column-gap" );
			case Text:
				return ShapeTree.Text( shape ) ?? ShapeTree.Descendants( objects, shape ).Select( ShapeTree.Text ).FirstOrDefault( t => t is not null );
			case Style:
				return ShapeTree.ComponentOf( shape ) is { } component ? styleOf( component ) : null;
			default:
				return StyleValues.FromShape( property, shape, theme );
		}
	}

	private static string? Gap( JsonObject shape, string key )
		=> shape[ "layout" ] is null ? null : Number.Normalize( shape[ "layout-gap" ]?[ key ]?.ToString() ?? "0" );

	/// <summary>The text layer a Text property writes to: the layer itself, or its first text layer (a button's label).</summary>
	public static JsonObject? TextLayer( JsonObject shape, JsonObject objects )
		=> shape[ "type" ]?.ToString() == "text" ? shape : ShapeTree.Descendants( objects, shape ).FirstOrDefault( s => s[ "type" ]?.ToString() == "text" );
}
