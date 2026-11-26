using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WyrmpdmSync.Penpot;

/// <summary>
/// Writes plain JSON as Transit JSON for Penpot's API. Penpot's JSON input can't express Clojure keywords inside
/// free-form values (a shape's <c>strokes</c> need <c>:stroke-style :solid</c>, not the string "solid"), so changes
/// are sent in Transit, the encoding the editor itself uses.
/// </summary>
/// <remarks>
/// Rules: object keys become keywords (camelCase keys are turned into Penpot's kebab-case); strings that are
/// UUIDs become UUIDs; the values of <see cref="KeywordValueKeys"/> become keywords; the values of
/// <see cref="SetKeys"/> become sets. No caching is used, which Transit readers accept.
/// </remarks>
internal static partial class TransitWriter
{
	/// <summary>Keys whose string values are keywords in Penpot's data.</summary>
	public static readonly HashSet<string> KeywordValueKeys =
	[
		"type", "attr", "style", "touched", "grow-type", "constraints-h", "constraints-v",
		"stroke-style", "stroke-alignment", "stroke-cap-start", "stroke-cap-end",
		"layout", "layout-flex-dir", "layout-wrap-type", "layout-gap-type", "layout-padding-type", "layout-item-margin-type",
		"layout-align-items", "layout-align-content", "layout-justify-content", "layout-justify-items", "layout-item-align-self",
		"layout-item-h-sizing", "layout-item-v-sizing",
	];

	/// <summary>Keys whose array values are sets.</summary>
	public static readonly HashSet<string> SetKeys = [ "features", "set-ids", "theme-ids", "touched" ];

	public static string Encode( JsonNode? node ) => Write( node, parentKey: null, inText: false )?.ToJsonString() ?? "null";

	/// <param name="inText">
	/// Inside a text shape's <c>content</c> tree values are plain strings (<c>type: "paragraph"</c>, font settings),
	/// never keywords.
	/// </param>
	private static JsonNode? Write( JsonNode? node, string? parentKey, bool inText ) => node switch
	{
		JsonObject obj when obj[ "~tag" ] is JsonValue tag => WriteRecord( obj, tag.ToString(), inText ),
		JsonObject obj => WriteMap( obj, inText ),
		JsonArray arr when parentKey is not null && SetKeys.Contains( parentKey ) =>
			new JsonArray( "~#set", new JsonArray( [ .. arr.Select( v => Write( v, parentKey, inText ) ) ] ) ),
		JsonArray arr => new JsonArray( [ .. arr.Select( v => Write( v, parentKey, inText ) ) ] ),
		JsonValue v when v.TryGetValue( out string? s ) => JsonValue.Create( WriteString( s, parentKey, inText ) ),
		_ => node?.DeepClone(),
	};

	/// <summary>
	/// A Penpot record the reader marked with "~tag" (shape, point, matrix, rect, path data) goes back out tagged, so
	/// Penpot reads it as that type again.
	/// </summary>
	private static JsonArray WriteRecord( JsonObject obj, string tag, bool inText )
	{
		JsonNode? rep = obj.Count == 2 && obj.ContainsKey( "value" )
			? Write( obj[ "value" ], null, inText )
			: WriteMap( obj, inText );
		return new JsonArray( $"~#{tag}", rep );
	}

	private static JsonArray WriteMap( JsonObject obj, bool inText )
	{
		JsonArray map = [ "^ " ];
		// in a { type: set, attr: X, val: … } operation the value belongs to attribute X
		string? attr = obj[ "attr" ] is JsonValue a && a.TryGetValue( out string? name ) ? Kebab( name ) : null;
		foreach( (string key, JsonNode? value ) in obj )
		{
			if( key == "~tag" )
				continue; // the reader's note of a Penpot record type, not data
			string kebab = Kebab( key );
			string valueKey = kebab == "val" && attr is not null ? attr : kebab;
			map.Add( $"~:{kebab}" );
			map.Add( Write( value, valueKey, inText || valueKey == "content" ) );
		}
		return map;
	}

	private static string WriteString( string s, string? parentKey, bool inText )
	{
		if( UuidRegex().IsMatch( s ) )
			return $"~u{s}";
		if( !inText && parentKey is not null && KeywordValueKeys.Contains( parentKey ) )
			return $"~:{s}";
		return Escape( s );
	}

	/// <summary>Strings that start with a Transit marker are escaped with a leading "~".</summary>
	private static string Escape( string s ) => s.Length > 0 && s[ 0 ] is '~' or '^' or '`' ? "~" + s : s;

	/// <summary>"setId" → "set-id"; kebab-case names are returned as they are.</summary>
	public static string Kebab( string name ) => CamelBoundaryRegex().Replace( name, "$1-$2" ).ToLowerInvariant();

	[GeneratedRegex( @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$" )]
	private static partial Regex UuidRegex();

	[GeneratedRegex( @"([a-z0-9])([A-Z])" )]
	private static partial Regex CamelBoundaryRegex();
}
