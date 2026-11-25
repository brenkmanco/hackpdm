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

	private static JsonObject Set( string attr, JsonNode? val ) => new() { [ "type" ] = "set", [ "attr" ] = attr, [ "val" ] = val };

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
