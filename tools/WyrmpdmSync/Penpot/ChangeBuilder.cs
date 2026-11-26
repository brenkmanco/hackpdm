using System.Globalization;
using System.Text.Json.Nodes;

using WyrmpdmSync.Sync;

namespace WyrmpdmSync.Penpot;

/// <summary>A batch of changes for Penpot's <c>update-file</c>, with a line describing each for the user.</summary>
internal sealed class ChangeSet
{
	public List<JsonObject> Changes { get; } = [];
	public List<string> Descriptions { get; } = [];
	/// <summary>Ids the batch gives to new token sets and tokens, by link: written back to the manifest after a push.</summary>
	public Dictionary<TokenLink, (Guid SetId, Guid TokenId)> Ids { get; } = [];
	/// <summary>Component pushes that actually produced Penpot changes (excludes skipped ones).</summary>
	public HashSet<ComponentReport> AppliedComponents { get; } = [];
	/// <summary>View element pushes that actually produced Penpot changes (excludes skipped ones).</summary>
	public HashSet<ElementReport> AppliedElements { get; } = [];
	/// <summary>Latest <c>applied-tokens</c> map per shape across all builders sharing this batch.</summary>
	internal Dictionary<(Guid Page, string Id), JsonObject> AppliedTokensState { get; } = [];
	public bool IsEmpty => Changes.Count == 0;
}

/// <summary>
/// Builds the Penpot changes for a `push`: each token's new value (creating its set and the token when missing), the
/// matching library color, and every shape bound to the token. Penpot's API doesn't repaint bound shapes when a token
/// changes (the editor does that), so the shapes' fills, strokes and corners are written too.
/// </summary>
internal static class ChangeBuilder
{
	/// <summary>Shape attributes a token can be applied to, and how the token's value lands on the shape.</summary>
	private static readonly string[] RadiusAttrs = [ "r1", "r2", "r3", "r4" ];

	public static ChangeSet ForTokens( PenpotFile file, IReadOnlyList<TokenReport> pushes, Func<Guid>? newId = null )
	{
		newId ??= Guid.NewGuid;
		ChangeSet set = new();
		Dictionary<string, Guid> setsByName = file.TokenSets.ToDictionary( s => s.Name, s => s.Id, StringComparer.Ordinal );
		List<Guid> newSets = [];

		foreach( TokenReport push in pushes )
		{
			TokenLink link = push.Link;
			string value = push.Code!;

			// the token set
			Guid setId = push.Token?.SetId ?? ( setsByName.TryGetValue( link.SetName, out Guid known ) ? known : Guid.Empty );
			if( setId == Guid.Empty )
			{
				setId = newId();
				setsByName[ link.SetName ] = setId;
				newSets.Add( setId );
				set.Changes.Add( new JsonObject
				{
					[ "type" ] = "set-token-set", [ "id" ] = setId.ToString(),
					[ "attrs" ] = new JsonObject { [ "id" ] = setId.ToString(), [ "name" ] = link.SetName, [ "description" ] = "" },
				} );
				set.Descriptions.Add( $"create token set \"{link.SetName}\"" );
			}

			// the token
			Guid tokenId = push.Token?.Id ?? newId();
			string name = push.Token?.Name ?? link.CodeName;
			set.Changes.Add( new JsonObject
			{
				[ "type" ] = "set-token", [ "setId" ] = setId.ToString(), [ "tokenId" ] = tokenId.ToString(),
				[ "attrs" ] = new JsonObject
				{
					[ "id" ] = tokenId.ToString(), [ "name" ] = name, [ "type" ] = link.TokenType,
					[ "value" ] = value, [ "description" ] = "",
				},
			} );
			set.Descriptions.Add( push.Token is null ? $"create token {name} = {value}" : $"token {name}: {push.Penpot} → {value}" );
			set.Ids[ link ] = (setId, tokenId);

			if( link is ColorLink color )
				AddLibraryColor( file, color, value, set );
			AddBoundShapes( file, name, link, value, set );
		}

		// a new set does nothing until it is switched on: keep the active ones and add the new ones
		if( newSets.Count > 0 )
		{
			set.Changes.Add( new JsonObject
			{
				[ "type" ] = "set-tokens-status",
				[ "themeIds" ] = new JsonArray( [ .. file.ActiveThemeIds.Select( g => (JsonNode)JsonValue.Create( g.ToString() )! ) ] ),
				[ "setIds" ] = new JsonArray( [ .. file.ActiveSetIds.Concat( newSets ).Distinct().Select( g => (JsonNode)JsonValue.Create( g.ToString() )! ) ] ),
			} );
			set.Descriptions.Add( $"switch on {newSets.Count} new token set(s)" );
		}
		return set;
	}

	/// <summary>The library color named after the role, in the folder named after the preset ("HackPDM colors / Light").</summary>
	private static void AddLibraryColor( PenpotFile file, ColorLink link, string hex, ChangeSet set )
	{
		string folder = link.SetName;
		PenpotLibraryColor? color = file.LibraryColors.FirstOrDefault( c =>
			c.Name == link.Role && NormalizePath( c.Path ) == folder );
		if( color is null )
			return;
		uint argb = Hex.ToArgb( hex )!.Value;
		string rgb = $"#{argb & 0xFFFFFF:x6}";
		double opacity = Math.Round( ( argb >> 24 ) / 255.0, 4 );
		if( Hex.Normalize( color.Color ) == Hex.Normalize( rgb ) && Math.Abs( color.Opacity - opacity ) <= 1e-4 )
			return;
		set.Changes.Add( new JsonObject
		{
			[ "type" ] = "mod-color",
			[ "color" ] = new JsonObject
			{
				[ "id" ] = color.Id.ToString(), [ "name" ] = color.Name, [ "path" ] = color.Path,
				[ "color" ] = rgb, [ "opacity" ] = opacity,
			},
		} );
		set.Descriptions.Add( $"library color {color.Path} / {color.Name} → {hex}" );
	}

	/// <summary>Every shape on every page with the token applied: its fills, strokes or corners take the new value.</summary>
	private static void AddBoundShapes( PenpotFile file, string tokenName, TokenLink link, string value, ChangeSet set )
	{
		int count = 0;
		foreach( PenpotPage page in file.Pages )
		{
			foreach( (string id, JsonNode? node) in page.Objects )
			{
				if( node is not JsonObject shape || ( shape[ "applied-tokens" ] ?? shape[ "appliedTokens" ] ) is not JsonObject applied )
					continue;
				JsonArray ops = [];
				foreach( (string attr, JsonNode? bound) in applied )
				{
					if( !string.Equals( bound?.ToString(), tokenName, StringComparison.OrdinalIgnoreCase ) )
						continue;
					switch( attr )
					{
						case "fill" when link is ColorLink:
							if( shape[ "type" ]?.ToString() == "text" )
							{
								if( shape[ "content" ] is JsonObject content && content.DeepClone() is JsonObject contentCopy
								    && RecolorTextContent( contentCopy, value ) )
									ops.Add( Set( "content", contentCopy ) );
								if( RecolorPositionData( shape[ "position-data" ], value ) is { } posData )
									ops.Add( Set( "position-data", posData ) );
							}
							else if( Recolor( shape[ "fills" ], "fill-color", "fill-opacity", value ) is { } fills )
							{
								ops.Add( Set( "fills", fills ) );
							}
							break;
						case "stroke-color" when link is ColorLink:
							if( Recolor( shape[ "strokes" ], "stroke-color", "stroke-opacity", value ) is { } strokes )
								ops.Add( Set( "strokes", strokes ) );
							break;
						case "stroke-width" when link is ShapeLink:
							if( Restroke( shape[ "strokes" ], value ) is { } widths )
								ops.Add( Set( "strokes", widths ) );
							break;
						case var r when RadiusAttrs.Contains( r ) && link is ShapeLink:
							ops.Add( Set( r, JsonValue.Create( double.Parse( value, CultureInfo.InvariantCulture ) ) ) );
							break;
					}
				}
				if( ops.Count == 0 )
					continue;
				set.Changes.Add( new JsonObject { [ "type" ] = "mod-obj", [ "id" ] = id, [ "pageId" ] = page.Id.ToString(), [ "operations" ] = ops } );
				count++;
			}
		}
		if( count > 0 )
			set.Descriptions.Add( $"  {count} shape(s) bound to {tokenName}" );
	}

	/// <summary>
	/// The changes for a component `push`: each pushed setter is written to the component's main shape in every theme
	/// (with that theme's token for colors), to every copy that hasn't overridden that property, and to linked detached
	/// styled view layers that inherit that property from the style.
	/// </summary>
	public static ChangeSet ForComponents( PenpotFile file, IReadOnlyList<ComponentReport> pushes, IEnumerable<FrameContext>? frames = null, ChangeSet? into = null )
	{
		ChangeSet set = into ?? new();
		IReadOnlyList<FrameContext>? frameList = frames is null ? null : frames as IReadOnlyList<FrameContext> ?? [ .. frames ];
		foreach( IGrouping<ComponentLink, ComponentReport> byLink in pushes.GroupBy( p => p.Link ) )
		{
			ComponentLink link = byLink.Key;
			List<string> themes = [ .. ComponentSync.Themes( file, link ) ];
			foreach( ComponentReport push in byLink )
			{
				if( themes.Count == 0 )
				{
					set.Descriptions.Add( $"  skipped {link.Label} {push.Property}: no component in Penpot" );
					continue;
				}
				bool sourceApplied = false;
				bool anyThemeFailed = false;
				string group = OverrideGroup( push.Property );
				foreach( string theme in themes )
				{
					PenpotComponent? component = ComponentSync.Component( file, link, theme );
					JsonObject? main = component is null ? null : file.MainShape( component );
					if( component is null || main is null )
					{
						anyThemeFailed = true;
						set.Descriptions.Add( $"  skipped {link.Label} [{theme}] {push.Property}: missing main shape in Penpot" );
						continue;
					}
					string mainId = component.MainInstanceId.ToString();
					List<(Guid Page, string Id, JsonObject Shape, bool IsDetached)> targets = [ (component.MainInstancePage, mainId, main, false) ];
					HashSet<(Guid Page, string Id)> seen = [ (component.MainInstancePage, mainId) ];
					foreach( (Guid page, string id, JsonObject copy) in Copies( file, component.MainInstanceId, group ) )
						if( seen.Add( (page, id) ) )
							targets.Add( (page, id, copy, false) );
					if( frameList is not null )
					{
						foreach( FrameContext ctx in frameList )
						{
							foreach( ElementLink el in ctx.Link.Elements )
							{
								if( !ctx.View.Anchors.TryGetValue( el.Anchor, out XamlElement? xaml )
								    || ElementValues.FromXaml( xaml, ElementValues.Style ) != link.Style
								    || xaml.Attr( ElementValues.AttributeName( xaml.Tag, push.Property ) ) is not null )
									continue;
								Guid? targetId = theme == "Dark" ? el.DarkShape : theme == ComponentSync.SourceTheme ? el.Shape : null;
								if( ctx.Shape( targetId ) is not { } layerShape || ShapeTree.ComponentOf( layerShape ) is not null )
									continue;
								string layerId = layerShape[ "id" ]?.ToString() ?? targetId!.Value.ToString();
								if( seen.Add( (ctx.Page.Id, layerId) ) )
									targets.Add( (ctx.Page.Id, layerId, layerShape, true) );
							}
						}
					}

					int emitted = 0;
					JsonArray? mainStrokes = main[ "strokes" ] as JsonArray;
					foreach( (Guid page, string id, JsonObject targetShape, bool isDetached) in targets )
					{
						JsonArray? fallbackStrokes = isDetached ? mainStrokes : null;
						if( ShapeOps( file, targetShape, push.Property, push.Code!, theme, set, pageId: page, fallbackStrokes: fallbackStrokes ) is not { } op )
						{
							if( ReferenceEquals( targetShape, main ) )
							{
								anyThemeFailed = true;
								break;
							}
							continue;
						}
						set.Changes.Add( new JsonObject
						{
							[ "type" ] = "mod-obj", [ "id" ] = id, [ "pageId" ] = page.ToString(),
							[ "operations" ] = op.Ops,
						} );
						emitted++;
					}
					if( emitted > 0 )
					{
						if( theme == ComponentSync.SourceTheme )
							sourceApplied = true;
						set.Descriptions.Add( $"{link.Label} [{theme}] {push.Property}: {push.Penpot ?? "—"} → {push.Code}" +
						                      ( emitted > 1 ? $" (+{emitted - 1} copies)" : "" ) );
					}
				}
				if( sourceApplied && !anyThemeFailed )
					set.AppliedComponents.Add( push );
			}
		}
		return set;
	}

	/// <summary>Operations that set one style property on a shape, and the override group Penpot tracks it in.</summary>
	internal static (JsonArray Ops, string Group)? ShapeOps(
		PenpotFile file,
		JsonObject shape,
		string property,
		string value,
		string theme,
		ChangeSet set,
		Dictionary<(Guid Page, string Id), JsonObject>? appliedState = null,
		Guid pageId = default,
		JsonArray? fallbackStrokes = null )
	{
		switch( property )
		{
			case StyleValues.Background or StyleValues.BorderBrush:
				bool fill = property == StyleValues.Background;
				string tokenAttr = fill ? "fill" : "stroke-color", listKey = fill ? "fills" : "strokes";
				JsonNode? paints;
				string? tokenName = null;
				if( value == "Transparent" )
				{
					paints = new JsonArray();
				}
				else
				{
					PenpotToken? token = file.Tokens.FirstOrDefault( t => t.Type == "color" && string.Equals( t.Name, $"{theme}.{value}", StringComparison.OrdinalIgnoreCase ) );
					if( token is null || Hex.Normalize( token.Value ) is not { } hex )
					{
						set.Descriptions.Add( $"  skipped {property} on a {theme} shape: no color token {theme}.{value}" );
						return null;
					}
					tokenName = token.Name;
					paints = Recolor( shape[ listKey ], fill ? "fill-color" : "stroke-color", fill ? "fill-opacity" : "stroke-opacity", hex )
					         ?? shape[ listKey ]?.DeepClone();
					if( fill && paints is not JsonArray { Count: > 0 } )
						paints = new JsonArray( SolidFill( hex ) );
					if( !fill && paints is not JsonArray { Count: > 0 } && fallbackStrokes is { Count: > 0 } )
						paints = Recolor( fallbackStrokes, "stroke-color", "stroke-opacity", hex ) ?? fallbackStrokes.DeepClone();
					if( !fill && paints is not JsonArray { Count: > 0 } )
					{
						set.Descriptions.Add( $"  skipped {property} on a {theme} shape: it has no border to recolor" );
						return null;
					}
				}
				Dictionary<(Guid Page, string Id), JsonObject>? state = appliedState ?? ( pageId != default ? set.AppliedTokensState : null );
				JsonObject applied;
				if( state is not null && shape[ "id" ]?.ToString() is { Length: > 0 } shapeId )
				{
					(Guid Page, string Id) key = (pageId, shapeId);
					if( !state.TryGetValue( key, out JsonObject? cached ) )
					{
						cached = ( ( shape[ "applied-tokens" ] ?? shape[ "appliedTokens" ] ) as JsonObject )?.DeepClone() as JsonObject ?? [];
						state[ key ] = cached;
					}
					applied = cached;
				}
				else
				{
					applied = ( ( shape[ "applied-tokens" ] ?? shape[ "appliedTokens" ] ) as JsonObject )?.DeepClone() as JsonObject ?? [];
				}
				if( tokenName is null )
					applied.Remove( tokenAttr );
				else
					applied[ tokenAttr ] = tokenName;
				JsonArray ops = [ Set( "applied-tokens", state is not null ? applied.DeepClone() : applied ) ];
				if( paints is not null )
					ops.Add( Set( listKey, paints ) );
				return (ops, fill ? "fill-group" : "stroke-group");
			case StyleValues.Padding:
				string[] ltrb = value.Split( ',' );
				JsonObject padding = new()
				{
					[ "p1" ] = StyleValues.Json( ltrb[ 1 ] ), [ "p2" ] = StyleValues.Json( ltrb[ 2 ] ),
					[ "p3" ] = StyleValues.Json( ltrb[ 3 ] ), [ "p4" ] = StyleValues.Json( ltrb[ 0 ] ),
				};
				return ([ Set( "layout-padding", padding ), Set( "layout-padding-type", "multiple" ) ], "layout-padding");
			case StyleValues.CornerRadius:
				return ([ .. RadiusAttrs.Select( r => (JsonNode)Set( r, StyleValues.Json( value ) ) ) ], "radius-group");
			default:
				return null;
		}
	}

	/// <summary>The Penpot override group name tracked on copies for a style property.</summary>
	private static string OverrideGroup( string property ) => property switch
	{
		StyleValues.Background => "fill-group",
		StyleValues.BorderBrush => "stroke-group",
		StyleValues.Padding => "layout-padding",
		StyleValues.CornerRadius => "radius-group",
		_ => "",
	};

	/// <summary>Copies of a main shape (on any page) that haven't overridden <paramref name="group"/>.</summary>
	private static IEnumerable<(Guid Page, string Id, JsonObject Shape)> Copies( PenpotFile file, Guid mainId, string group )
	{
		string main = mainId.ToString();
		foreach( PenpotPage page in file.Pages )
			foreach( (string id, JsonNode? node) in page.Objects )
				if( node is JsonObject shape && shape[ "shape-ref" ]?.ToString() == main && !IsTouched( shape, group ) )
					yield return (page.Id, id, shape);
	}

	private static bool IsTouched( JsonObject shape, string group )
		=> group.Length > 0 && shape[ "touched" ] is JsonArray touched && touched.Any( t => t?.ToString() == group );

	private static JsonObject Set( string attr, JsonNode? val ) => new() { [ "type" ] = "set", [ "attr" ] = attr, [ "val" ] = val };

	private static JsonObject Set( string attr, string val ) => Set( attr, JsonValue.Create( val ) );

	/// <summary>A "set this attribute" operation for a mod-obj change.</summary>
	internal static JsonObject SetOp( string attr, JsonNode? val ) => Set( attr, val );

	/// <summary>Recolors every fill array inside a text shape's content tree (paragraphs and leaf runs); true when any changed.</summary>
	private static bool RecolorTextContent( JsonObject node, string hex )
	{
		bool changed = false;
		if( node[ "fills" ] is JsonArray { Count: > 0 } existing )
		{
			if( Recolor( existing, "fill-color", "fill-opacity", hex ) is { } fills )
			{
				node[ "fills" ] = fills;
				changed = true;
			}
		}
		else if( node.ContainsKey( "text" ) )
		{
			node[ "fills" ] = new JsonArray( SolidFill( hex ) );
			changed = true;
		}
		if( node[ "children" ] is JsonArray children )
			foreach( JsonNode? child in children )
				if( child is JsonObject childObj )
					changed |= RecolorTextContent( childObj, hex );
		return changed;
	}

	/// <summary>Recolors every glyph rect's fills in a text shape's cached position-data; null when nothing changes.</summary>
	private static JsonArray? RecolorPositionData( JsonNode? positionData, string hex )
	{
		if( positionData is not JsonArray list || list.DeepClone() is not JsonArray copy )
			return null;
		bool changed = false;
		foreach( JsonNode? entry in copy )
		{
			if( entry is not JsonObject rect )
				continue;
			if( rect[ "fills" ] is JsonArray { Count: > 0 } existing )
			{
				if( Recolor( existing, "fill-color", "fill-opacity", hex ) is { } fills )
				{
					rect[ "fills" ] = fills;
					changed = true;
				}
			}
			else
			{
				rect[ "fills" ] = new JsonArray( SolidFill( hex ) );
				changed = true;
			}
		}
		return changed ? copy : null;
	}

	private static JsonObject SolidFill( string hex )
	{
		uint argb = Hex.ToArgb( hex )!.Value;
		return new JsonObject
		{
			[ "fill-color" ] = $"#{argb & 0xFFFFFF:x6}",
			[ "fill-opacity" ] = Math.Round( ( argb >> 24 ) / 255.0, 4 ),
		};
	}

	/// <summary>The paint list with every solid color replaced; null when nothing changes.</summary>
	private static JsonArray? Recolor( JsonNode? paints, string colorKey, string opacityKey, string hex )
	{
		if( paints is not JsonArray list )
			return null;
		uint argb = Hex.ToArgb( hex )!.Value;
		string rgb = $"#{argb & 0xFFFFFF:x6}";
		double opacity = Math.Round( ( argb >> 24 ) / 255.0, 4 );
		JsonArray result = [];
		bool changed = false;
		foreach( JsonNode? paint in list )
		{
			JsonObject copy = paint?.DeepClone() as JsonObject ?? [];
			if( copy[ colorKey ] is JsonValue current )
			{
				double currentOpacity = copy[ opacityKey ] is JsonValue opVal
				                        && ( opVal.TryGetValue( out double op )
				                             || double.TryParse( opVal.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out op ) )
					? op : 1.0;
				changed |= Hex.Normalize( current.ToString() ) != Hex.Normalize( rgb )
				           || Math.Abs( currentOpacity - opacity ) > 1e-4;
				copy[ colorKey ] = rgb;
				copy[ opacityKey ] = opacity;
			}
			result.Add( copy );
		}
		return changed ? result : null;
	}

	private static JsonArray? Restroke( JsonNode? strokes, string width )
	{
		if( strokes is not JsonArray list || list.Count == 0 )
			return null;
		double w = double.Parse( width, CultureInfo.InvariantCulture );
		JsonArray result = [];
		foreach( JsonNode? stroke in list )
		{
			JsonObject copy = stroke?.DeepClone() as JsonObject ?? [];
			copy[ "stroke-width" ] = w;
			result.Add( copy );
		}
		return result;
	}

	/// <summary>"HackPDM colors / Light" → "HackPDM colors/Light" (the library shows folders with spaced slashes).</summary>
	private static string NormalizePath( string path ) => string.Join( "/", path.Split( '/' ).Select( p => p.Trim() ) );
}
