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
		"type", "attr", "stroke-style", "stroke-alignment", "stroke-cap-start", "stroke-cap-end",
		"layout", "layout-flex-dir", "layout-align-items", "layout-justify-content", "layout-item-h-sizing", "layout-item-v-sizing",
	];

	/// <summary>Keys whose array values are sets.</summary>
	public static readonly HashSet<string> SetKeys = [ "features", "set-ids", "theme-ids" ];

	public static string Encode( JsonNode? node ) => Write( node, parentKey: null )?.ToJsonString() ?? "null";

	private static JsonNode? Write( JsonNode? node, string? parentKey ) => node switch
	{
		JsonObject obj => WriteMap( obj ),
		JsonArray arr when parentKey is not null && SetKeys.Contains( parentKey ) =>
			new JsonArray( "~#set", new JsonArray( [ .. arr.Select( v => Write( v, null ) ) ] ) ),
		JsonArray arr => new JsonArray( [ .. arr.Select( v => Write( v, null ) ) ] ),
		JsonValue v when v.TryGetValue( out string? s ) => JsonValue.Create( WriteString( s, parentKey ) ),
		_ => node?.DeepClone(),
	};

	private static JsonArray WriteMap( JsonObject obj )
	{
		JsonArray map = [ "^ " ];
		foreach( (string key, JsonNode? value ) in obj )
		{
			string kebab = Kebab( key );
			map.Add( $"~:{kebab}" );
			map.Add( Write( value, kebab ) );
		}
		return map;
	}

	private static string WriteString( string s, string? parentKey )
	{
		if( UuidRegex().IsMatch( s ) )
			return $"~u{s}";
		if( parentKey is not null && KeywordValueKeys.Contains( parentKey ) )
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
