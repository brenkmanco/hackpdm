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
				[ "themeIds" ] = new JsonArray(),
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
		if( color is null || Hex.Normalize( color.Color ) == hex )
			return;
		uint argb = Hex.ToArgb( hex )!.Value;
		set.Changes.Add( new JsonObject
		{
			[ "type" ] = "mod-color",
			[ "color" ] = new JsonObject
			{
				[ "id" ] = color.Id.ToString(), [ "name" ] = color.Name, [ "path" ] = color.Path,
				[ "color" ] = $"#{argb & 0xFFFFFF:x6}", [ "opacity" ] = Math.Round( ( argb >> 24 ) / 255.0, 4 ),
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
							if( Recolor( shape[ "fills" ], "fill-color", "fill-opacity", value ) is { } fills )
								ops.Add( Set( "fills", fills ) );
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
	/// (with that theme's token for colors) and to every copy that hasn't overridden that property.
	/// </summary>
	public static ChangeSet ForComponents( PenpotFile file, IReadOnlyList<ComponentReport> pushes )
	{
		ChangeSet set = new();
		foreach( IGrouping<ComponentLink, ComponentReport> byLink in pushes.GroupBy( p => p.Link ) )
		{
			ComponentLink link = byLink.Key;
			foreach( string theme in ComponentSync.Themes( file, link ) )
			{
				PenpotComponent? component = ComponentSync.Component( file, link, theme );
				JsonObject? main = component is null ? null : file.MainShape( component );
				if( component is null || main is null )
					continue;
				foreach( ComponentReport push in byLink )
				{
					if( ShapeOps( file, main, push.Property, push.Code!, theme, set ) is not { } op )
						continue;
					List<(Guid Page, string Id)> targets = [ (component.MainInstancePage, component.MainInstanceId.ToString()) ];
					targets.AddRange( Copies( file, component.MainInstanceId, op.Group ) );
					foreach( (Guid page, string id) in targets )
						set.Changes.Add( new JsonObject
						{
							[ "type" ] = "mod-obj", [ "id" ] = id, [ "pageId" ] = page.ToString(),
							[ "operations" ] = new JsonArray( [ .. op.Ops.Select( o => o?.DeepClone() ) ] ),
						} );
					set.Descriptions.Add( $"{link.Label} [{theme}] {push.Property}: {push.Penpot ?? "—"} → {push.Code}" +
					                      ( targets.Count > 1 ? $" (+{targets.Count - 1} copies)" : "" ) );
				}
			}
		}
		return set;
	}

	/// <summary>Operations that set one style property on a shape, and the override group Penpot tracks it in.</summary>
	internal static (JsonArray Ops, string Group)? ShapeOps( PenpotFile file, JsonObject shape, string property, string value, string theme, ChangeSet set )
	{
		switch( property )
		{
			case StyleValues.Background or StyleValues.BorderBrush:
				bool fill = property == StyleValues.Background;
				string tokenAttr = fill ? "fill" : "stroke-color", listKey = fill ? "fills" : "strokes";
				JsonObject applied = ( shape[ "applied-tokens" ] as JsonObject )?.DeepClone() as JsonObject ?? [];
				JsonNode? paints;
				if( value == "Transparent" )
				{
					applied.Remove( tokenAttr );
					paints = fill ? new JsonArray() : null;
				}
				else
				{
					PenpotToken? token = file.Tokens.FirstOrDefault( t => t.Type == "color" && string.Equals( t.Name, $"{theme}.{value}", StringComparison.OrdinalIgnoreCase ) );
					if( token is null || Hex.Normalize( token.Value ) is not { } hex )
					{
						set.Descriptions.Add( $"  skipped {property} on a {theme} shape: no color token {theme}.{value}" );
						return null;
					}
					applied[ tokenAttr ] = token.Name;
					paints = Recolor( shape[ listKey ], fill ? "fill-color" : "stroke-color", fill ? "fill-opacity" : "stroke-opacity", hex )
					         ?? shape[ listKey ]?.DeepClone();
					if( fill && paints is JsonArray { Count: 0 } )
						paints = new JsonArray( new JsonObject { [ "fill-color" ] = $"#{Hex.ToArgb( hex )!.Value & 0xFFFFFF:x6}", [ "fill-opacity" ] = 1 } );
					if( !fill && paints is not JsonArray { Count: > 0 } )
					{
						set.Descriptions.Add( $"  skipped {property} on a {theme} shape: it has no border to recolor" );
						return null;
					}
				}
				JsonArray ops = [ Set( "applied-tokens", applied ) ];
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

	/// <summary>Copies of a main shape (on any page) that haven't overridden <paramref name="group"/>.</summary>
	private static IEnumerable<(Guid Page, string Id)> Copies( PenpotFile file, Guid mainId, string group )
	{
		string main = mainId.ToString();
		foreach( PenpotPage page in file.Pages )
			foreach( (string id, JsonNode? node) in page.Objects )
				if( node is JsonObject shape && shape[ "shape-ref" ]?.ToString() == main
				    && !( shape[ "touched" ] is JsonArray touched && touched.Any( t => t?.ToString() == group ) ) )
					yield return (page.Id, id);
	}

	private static JsonObject Set( string attr, JsonNode? val ) => new() { [ "type" ] = "set", [ "attr" ] = attr, [ "val" ] = val };

	private static JsonObject Set( string attr, string val ) => Set( attr, JsonValue.Create( val ) );

	/// <summary>A "set this attribute" operation for a mod-obj change.</summary>
	internal static JsonObject SetOp( string attr, JsonNode? val ) => Set( attr, val );

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
				changed |= Hex.Normalize( current.ToString() ) != Hex.Normalize( rgb );
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
