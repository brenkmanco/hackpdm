using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using HackPDM.Core.Theming;

using WyrmpdmSync.Penpot;

namespace WyrmpdmSync.Sync;

/// <summary>
/// New elements, both ways, inside elements whose children are synced as a whole (<see cref="ElementLink.Children"/>):
/// a layer added in Penpot becomes a marked XAML element, and a marked XAML element without a layer becomes one.
/// Removals are only reported.
/// </summary>
internal static partial class LayoutCreate
{
	/// <summary>A piece of new XAML and the links it brings.</summary>
	public sealed record NewXaml( string Description, XamlView.Edit Edit, List<ElementLink> Links );

	/// <summary>A new Penpot layer (Light and Dark) for a XAML element, and its link.</summary>
	public sealed record NewLayer( string Description, List<JsonObject> Changes, ElementLink Link );

	// ---------------------------------------------------------------- Penpot → XAML

	/// <summary>Layers in Penpot that synced containers don't have in XAML yet, as XAML to insert.</summary>
	public static List<NewXaml> FromPenpot( FrameContext ctx, PenpotFile file, Func<Guid, string?> styleOf, List<string> notes )
	{
		List<NewXaml> result = [];
		HashSet<string> linked = [ .. ctx.Link.Elements.Where( e => e.Shape is not null ).Select( e => e.Shape!.Value.ToString() ) ];
		HashSet<string> anchors = [ .. ctx.View.Anchors.Keys ];
		foreach( ElementLink container in ctx.Link.Elements.Where( e => e.Children ) )
		{
			if( !ctx.View.Anchors.TryGetValue( container.Anchor, out XamlElement? xaml ) || ctx.Shape( container.Shape ) is not { } shape )
				continue;
			List<JsonObject> children = ShapeTree.Children( ctx.Objects, shape );
			for( int i = 0; i < children.Count; i++ )
			{
				JsonObject child = children[ i ];
				if( linked.Contains( child[ "id" ]!.ToString() ) )
					continue;
				if( xaml.SelfClosing || ( xaml.Tag is "Border" or "Button" or "ScrollViewer" && xaml.ContentChildren.Any() ) )
				{
					notes.Add( $"{container.Anchor}: Penpot has a new layer \"{child[ "name" ]}\" but <{xaml.Tag}> holds a single child; add it in XAML by hand" );
					continue;
				}
				string indent = xaml.Indent + "\t";
				List<ElementLink> links = [];
				string? snippet = Generate( ctx, file, child, $"{container.Anchor}", indent, styleOf, anchors, links );
				if( snippet is null )
					continue;
				// before the XAML of the next linked layer, else at the end of the container
				int at = -1;
				foreach( JsonObject next in children.Skip( i + 1 ) )
				{
					ElementLink? nextLink = ctx.Link.Elements.FirstOrDefault( e => e.Shape?.ToString() == next[ "id" ]!.ToString() );
					if( nextLink is not null && ctx.View.Anchors.TryGetValue( nextLink.Anchor, out XamlElement? nextXaml ) )
					{
						at = nextXaml.Start - nextXaml.Indent.Length;
						break;
					}
				}
				if( at < 0 )
					at = ctx.View.Text.LastIndexOf( '\n', xaml.ContentEnd - 1 ) + 1;
				result.Add( new NewXaml( $"{container.Anchor}: new {child[ "type" ]} \"{child[ "name" ]}\" → {links.FirstOrDefault()?.Anchor}",
					new XamlView.Edit( at, 0, snippet ), links ) );
				foreach( ElementLink l in links )
					linked.Add( l.Shape!.Value.ToString() );
			}
		}
		return result;
	}

	/// <summary>The XAML for a layer (and its children), each element marked and linked; null for layers with no XAML counterpart.</summary>
	private static string? Generate( FrameContext ctx, PenpotFile file, JsonObject shape, string parentAnchor, string indent,
		Func<Guid, string?> styleOf, HashSet<string> anchors, List<ElementLink> links )
	{
		string name = shape[ "name" ]?.ToString() ?? "";
		string type = shape[ "type" ]?.ToString() ?? "";
		string anchor = UniqueAnchor( $"{parentAnchor}.{Slug( name )}", anchors );
		List<(string Name, string Value)> attrs = [ (XamlView.AnchorAttribute, anchor) ];
		string tag;
		List<JsonObject> children = [];
		string? style = ShapeTree.ComponentOf( shape ) is { } component ? styleOf( component ) : null;

		if( ShapeTree.ComponentOf( shape ) is not null )
		{
			if( style is null )
				return $"{indent}<!--  penpot: \"{name}\" is a component without a linked XAML style  -->\n";
			tag = style.Contains( "Pane" ) || style.Contains( "Toolbar" ) || style.Contains( "Banner" ) ? "Border"
				: style.Contains( "Toggle" ) ? "ToggleButton" : "Button";
			attrs.Add( ("Style", $"{{StaticResource {style}}}") );
			if( tag != "Border" && ElementValues.FromShape( ElementValues.Text, shape, ctx.Objects, LayoutSync.SourceTheme, styleOf ) is { } label )
				attrs.Add( ("Content", label) );
		}
		else if( type == "text" )
		{
			tag = "TextBlock";
			if( TypographyStyle( file, shape ) is { } typo )
				attrs.Add( ("Style", $"{{StaticResource {typo}}}") );
			if( Role( shape, "fill" ) is { } fg )
				attrs.Add( ("Foreground", $"{{ThemeResource {MaterialRoles.BrushKey( fg )}}}") );
			attrs.Add( ("Text", ShapeTree.Text( shape ) ?? "") );
		}
		else if( type == "frame" && name.StartsWith( "icon", StringComparison.Ordinal ) )
		{
			tag = "FontIcon";
			attrs.Add( ("FontSize", Number.Normalize( shape[ "width" ]?.ToString() ) ?? "16") );
			attrs.Add( ("Glyph", IconGlyph( name )) );
		}
		else if( type == "frame" && shape[ "layout" ]?.ToString() == "flex" )
		{
			tag = "StackPanel";
			bool row = shape[ "layout-flex-dir" ]?.ToString() is "row" or "row-reverse";
			if( row )
				attrs.Add( ("Orientation", "Horizontal") );
			AddIf( attrs, "Spacing", ElementValues.FromShape( ElementValues.Spacing, shape, ctx.Objects, LayoutSync.SourceTheme, styleOf ), "0" );
			AddIf( attrs, "Padding", StyleValues.FromShape( StyleValues.Padding, shape, LayoutSync.SourceTheme ) is { } p ? StyleValues.ToXaml( StyleValues.Padding, p, "" ) : null, "0" );
			AddBrushes( shape, attrs );
			children = ShapeTree.Children( ctx.Objects, shape );
		}
		else if( type is "frame" or "rect" )
		{
			tag = type == "rect" || ShapeTree.Children( ctx.Objects, shape ).Count == 0 ? "Border" : "Grid";
			if( shape[ "layout-item-h-sizing" ]?.ToString() != "fill" )
				attrs.Add( ("Width", Number.Normalize( shape[ "width" ]?.ToString() ) ?? "0") );
			if( shape[ "layout-item-v-sizing" ]?.ToString() != "fill" )
				attrs.Add( ("Height", Number.Normalize( shape[ "height" ]?.ToString() ) ?? "0") );
			AddBrushes( shape, attrs );
			children = tag == "Grid" ? ShapeTree.Children( ctx.Objects, shape ) : [];
		}
		else
			return $"{indent}<!--  penpot: \"{name}\" ({type}) has no XAML counterpart  -->\n";

		ElementLink link = new() { Anchor = anchor, Shape = Guid.Parse( shape[ "id" ]!.ToString() ), Children = children.Count > 0 };
		link.Path = ShapeTree.PathOf( ctx.Objects, ctx.Frame, shape );
		if( ctx.DarkFrame is not null && link.Path is not null && ShapeTree.Resolve( ctx.Objects, ctx.DarkFrame, link.Path ) is { } dark )
			link.DarkShape = Guid.Parse( dark[ "id" ]!.ToString() );
		links.Add( link );

		StringBuilder sb = new();
		sb.Append( indent ).Append( '<' ).Append( tag );
		foreach( (string n, string v) in attrs )
			// glyphs are XAML character references ("&#xE721;"): written as they are
			sb.Append( ' ' ).Append( n ).Append( "=\"" ).Append( n == "Glyph" ? v : XamlView.Escape( v ) ).Append( '"' );
		if( children.Count == 0 )
			return sb.Append( " />\n" ).ToString();
		sb.Append( ">\n" );
		foreach( JsonObject child in children )
			sb.Append( Generate( ctx, file, child, anchor, indent + "\t", styleOf, anchors, links ) );
		return sb.Append( indent ).Append( "</" ).Append( tag ).Append( ">\n" ).ToString();
	}

	private static void AddIf( List<(string, string)> attrs, string name, string? value, string skip )
	{
		if( value is not null && value != skip )
			attrs.Add( (name, value) );
	}

	private static void AddBrushes( JsonObject shape, List<(string, string)> attrs )
	{
		if( Role( shape, "fill" ) is { } bg )
			attrs.Add( ("Background", $"{{ThemeResource {MaterialRoles.BrushKey( bg )}}}") );
		if( Role( shape, "stroke-color" ) is { } border )
		{
			attrs.Add( ("BorderBrush", $"{{ThemeResource {MaterialRoles.BrushKey( border )}}}") );
			attrs.Add( ("BorderThickness", Number.Normalize( shape[ "strokes" ]?[ 0 ]?[ "stroke-width" ]?.ToString() ) ?? "1") );
		}
		if( StyleValues.FromShape( StyleValues.CornerRadius, shape, LayoutSync.SourceTheme ) is { } r && r != "0" )
			attrs.Add( ("CornerRadius", r) );
	}

	/// <summary>The Material role of the color token applied to a paint ("Light.OnSurface" → "OnSurface").</summary>
	private static string? Role( JsonObject shape, string attr )
	{
		string? token = shape[ "applied-tokens" ]?[ attr ]?.ToString();
		if( token is null )
			return null;
		string role = token[ ( token.IndexOf( '.' ) + 1 ).. ];
		return MaterialRoles.IsRole( role ) ? role : null;
	}

	/// <summary>The Uno Material text style for a text layer's typography ("Title medium" → TitleMedium).</summary>
	private static string? TypographyStyle( PenpotFile file, JsonObject text )
	{
		string? id = FirstLeaf( text )?[ "typography-ref-id" ]?.ToString();
		string? name = id is null ? null : file.Raw[ "data" ]?[ "typographies" ]?[ id ]?[ "name" ]?.ToString();
		return name is null ? null : string.Concat( name.Split( ' ', StringSplitOptions.RemoveEmptyEntries ).Select( w => char.ToUpperInvariant( w[ 0 ] ) + w[ 1.. ] ) );
	}

	private static JsonObject? FirstLeaf( JsonObject text )
		=> text[ "content" ]?[ "children" ]?[ 0 ]?[ "children" ]?[ 0 ]?[ "children" ]?[ 0 ] as JsonObject;

	/// <summary>Segoe Fluent glyphs for the icons the design uses (layers named "icon / search").</summary>
	private static string IconGlyph( string layerName ) => layerName.Split( '/' ).Last().Trim() switch
	{
		"search" => "&#xE721;", "refresh" => "&#xE72C;", "folder" => "&#xE8B7;", "list" => "&#xE8FD;", "tune" => "&#xE9E9;",
		"close" => "&#xE711;", "more" => "&#xE712;", "chevron" => "&#xE76C;", "check" => "&#xE73E;", "info" => "&#xE946;",
		"warning" => "&#xE7BA;", "error" => "&#xE783;", "file" => "&#xE8A5;", "graph" => "&#xE71D;", "person" => "&#xE77B;",
		"dropdown" => "&#xE70D;", "zoomIn" => "&#xE710;", "zoomOut" => "&#xE738;",
		_ => "&#xE7C3;",
	};

	private static string UniqueAnchor( string wanted, HashSet<string> taken )
	{
		string anchor = wanted;
		for( int n = 2; !taken.Add( anchor ); n++ )
			anchor = $"{wanted}-{n}";
		return anchor;
	}

	private static string Slug( string name )
	{
		string slug = SlugRegex().Replace( name.ToLowerInvariant(), "-" ).Trim( '-' );
		return slug.Length == 0 ? "item" : slug;
	}

	[GeneratedRegex( "[^a-z0-9]+" )]
	private static partial Regex SlugRegex();

	// ---------------------------------------------------------------- XAML → Penpot

	/// <summary>Marked XAML elements inside synced containers that have no Penpot layer yet, as layers to add (Light and Dark).</summary>
	public static List<NewLayer> FromCode( FrameContext ctx, PenpotFile file, SyncManifest manifest, List<string> notes )
	{
		List<NewLayer> result = [];
		Dictionary<string, ElementLink> byAnchor = ctx.Link.Elements.ToDictionary( e => e.Anchor, StringComparer.Ordinal );
		// new layers' ids, so children of a new element go into its new layer
		Dictionary<string, (JsonObject Light, JsonObject? Dark)> created = [];
		foreach( XamlElement element in ctx.View.Elements.Where( e => e.Anchor is not null && !byAnchor.ContainsKey( e.Anchor ) ) )
		{
			XamlElement? parentXaml = element.AnchoredParent;
			if( parentXaml?.Anchor is null )
				continue;
			(JsonObject Light, JsonObject? Dark) parent;
			if( created.TryGetValue( parentXaml.Anchor, out (JsonObject, JsonObject?) made ) )
				parent = made;
			else if( byAnchor.TryGetValue( parentXaml.Anchor, out ElementLink? parentLink ) && parentLink.Children
			         && ctx.Shape( parentLink.Shape ) is { } light )
				parent = (light, ctx.Shape( parentLink.DarkShape ));
			else
			{
				if( byAnchor.ContainsKey( parentXaml.Anchor ) )
					notes.Add( $"{element.Anchor}: new in XAML, but {parentXaml.Anchor} doesn't sync its children (set \"children\": true to add layers)" );
				continue;
			}

			List<JsonObject> changes = [];
			ShapeFactory.NewLayer? lightLayer = Build( ctx, file, manifest, element, parent.Light, LayoutSync.SourceTheme, notes );
			if( lightLayer is null )
				continue;
			int index = InsertIndex( ctx, element, parent.Light );
			changes.AddRange( ShapeFactory.AddChanges( lightLayer, ctx.Page.Id, index ) );
			ShapeFactory.NewLayer? darkLayer = parent.Dark is null ? null : Build( ctx, file, manifest, element, parent.Dark, "Dark", notes );
			if( darkLayer is not null )
				changes.AddRange( ShapeFactory.AddChanges( darkLayer, ctx.Page.Id, InsertIndex( ctx, element, parent.Dark! ) ) );

			ElementLink link = new()
			{
				Anchor = element.Anchor!, Shape = lightLayer.Id, DarkShape = darkLayer?.Id,
				Children = element.ContentChildren.Any( c => c.Anchor is not null ),
			};
			foreach( string property in ElementValues.All )
				if( ElementValues.FromXaml( element, property ) is { } value )
					link.Properties[ property ] = value;
			created[ element.Anchor! ] = (lightLayer.Objects[ 0 ], darkLayer?.Objects[ 0 ]);
			result.Add( new NewLayer( $"{parentXaml.Anchor}: new <{element.Tag}> {element.Anchor} → Penpot layer", changes, link ) );
		}
		return result;
	}

	/// <summary>The layer for one XAML element in one theme.</summary>
	private static ShapeFactory.NewLayer? Build( FrameContext ctx, PenpotFile file, SyncManifest manifest, XamlElement element,
		JsonObject parent, string theme, List<string> notes )
	{
		string name = element.Attr( "x:Name" ) ?? element.Anchor!.Split( '.' ).Last();
		string? style = ElementValues.FromXaml( element, ElementValues.Style );
		ComponentLink? componentLink = style is null ? null : manifest.Components.FirstOrDefault( c => c.Style == style );
		if( componentLink is not null && ComponentSync.Component( file, componentLink, theme ) is { } component && file.MainShape( component ) is { } main )
		{
			PenpotPage mainPage = file.Pages.First( p => p.Id == component.MainInstancePage );
			ShapeFactory.NewLayer copy = ShapeFactory.Copy( mainPage.Objects, main, file.Id, parent );
			if( ElementValues.FromXaml( element, ElementValues.Text ) is { } label )
				foreach( JsonObject layer in copy.Objects.Where( o => o[ "type" ]?.ToString() == "text" ).Take( 1 ) )
					if( ShapeTree.WithText( layer, label ) is { } content )
						layer[ "content" ] = content;
			return copy;
		}
		if( element.Tag == "TextBlock" )
		{
			string text = ElementValues.FromXaml( element, ElementValues.Text ) ?? "";
			JsonObject? template = TextTemplate( ctx, file, element );
			if( template is null || ShapeTree.WithText( template, text ) is not { } content )
			{
				notes.Add( $"{element.Anchor}: no single-style text layer in \"{ctx.Link.Frame}\" to base the new text on" );
				return null;
			}
			JsonObject applied = [];
			if( Brush( element.Attr( "Foreground" ) ) is { } fg && Token( file, theme, fg ) is { } token )
			{
				applied[ "fill" ] = token.Name;
				Recolor( content, Hex.Normalize( token.Value )! );
			}
			return ShapeFactory.Text( template, parent, name, content, applied );
		}
		if( element.Tag is "StackPanel" or "Grid" or "Border" )
		{
			string? flex = element.Tag == "StackPanel" ? element.Attr( "Orientation" ) == "Horizontal" ? "row" : "column" : null;
			string[] pad = ( StyleValues.FromXaml( StyleValues.Padding, element.Attr( "Padding" ) ) ?? "0,0,0,0" ).Split( ',' );
			double gap = double.TryParse( element.Attr( "Spacing" ), NumberStyles.Float, CultureInfo.InvariantCulture, out double g ) ? g : 0;
			double radius = double.TryParse( StyleValues.FromXaml( StyleValues.CornerRadius, element.Attr( "CornerRadius" ) ), NumberStyles.Float, CultureInfo.InvariantCulture, out double r ) ? r : 0;
			JsonArray fills = [];
			JsonObject applied = [];
			if( Brush( element.Attr( "Background" ) ) is { } bg && Token( file, theme, bg ) is { } token )
			{
				applied[ "fill" ] = token.Name;
				fills.Add( new JsonObject { [ "fill-color" ] = Hex.Normalize( token.Value )!.ToLowerInvariant(), [ "fill-opacity" ] = 1 } );
			}
			double w = double.TryParse( element.Attr( "Width" ), NumberStyles.Float, CultureInfo.InvariantCulture, out double ww ) ? ww : 100;
			double h = double.TryParse( element.Attr( "Height" ), NumberStyles.Float, CultureInfo.InvariantCulture, out double hh ) ? hh : 40;
			return ShapeFactory.Frame( name, parent, w, h, flex, gap, (P( pad[ 0 ] ), P( pad[ 1 ] ), P( pad[ 2 ] ), P( pad[ 3 ] )), radius, fills, applied );
		}
		notes.Add( $"{element.Anchor}: <{element.Tag}> has no Penpot counterpart the sync can build; draw it in Penpot and link it" );
		return null;

		static double P( string s ) => double.Parse( s, CultureInfo.InvariantCulture );
	}

	/// <summary>A text layer to copy for a new TextBlock: one with the same typography if there is one, else any single-run text.</summary>
	private static JsonObject? TextTemplate( FrameContext ctx, PenpotFile file, XamlElement element )
	{
		List<JsonObject> texts = [ .. ShapeTree.Descendants( ctx.Objects, ctx.Frame ).Where( s => s[ "type" ]?.ToString() == "text" && ShapeTree.WithText( s, "x" ) is not null ) ];
		string? wanted = ElementValues.FromXaml( element, ElementValues.Style );
		return texts.FirstOrDefault( t => wanted is not null && TypographyStyle( file, t ) == wanted ) ?? texts.FirstOrDefault();
	}

	private static string? Brush( string? xaml ) => xaml is null ? null : StyleValues.FromXaml( StyleValues.Background, xaml ) is { } role && MaterialRoles.IsRole( role ) ? role : null;

	private static PenpotToken? Token( PenpotFile file, string theme, string role )
		=> file.Tokens.FirstOrDefault( t => t.Type == "color" && string.Equals( t.Name, $"{theme}.{role}", StringComparison.OrdinalIgnoreCase ) );

	private static void Recolor( JsonObject content, string hex )
	{
		foreach( JsonNode? set in content[ "children" ] as JsonArray ?? [] )
			foreach( JsonNode? paragraph in set?[ "children" ] as JsonArray ?? [] )
				foreach( JsonNode? leaf in paragraph?[ "children" ] as JsonArray ?? [] )
					if( leaf is JsonObject l )
						l[ "fills" ] = new JsonArray( new JsonObject { [ "fill-color" ] = hex.ToLowerInvariant(), [ "fill-opacity" ] = 1 } );
	}

	/// <summary>
	/// Where a new layer goes in its parent's layer list so it shows after the layer of the XAML element before it
	/// (Penpot lists layers bottom first, so the visual position counts from the end).
	/// </summary>
	private static int InsertIndex( FrameContext ctx, XamlElement element, JsonObject parent )
	{
		List<JsonObject> visual = ShapeTree.Children( ctx.Objects, parent );
		int after = -1;
		XamlElement? container = element.Parent;
		if( container is not null )
		{
			foreach( XamlElement sibling in container.ContentChildren )
			{
				if( sibling == element )
					break;
				ElementLink? link = ctx.Link.Elements.FirstOrDefault( e => e.Anchor == sibling.Anchor );
				int position = link is null ? -1 : visual.FindIndex( v => v[ "id" ]!.ToString() == link.Shape?.ToString() || v[ "id" ]!.ToString() == link.DarkShape?.ToString() );
				if( position >= 0 )
					after = position;
			}
		}
		int visualIndex = after + 1;
		return Math.Max( 0, visual.Count - visualIndex );
	}
}
