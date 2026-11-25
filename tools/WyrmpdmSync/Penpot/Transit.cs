using System.Text.Json.Nodes;

namespace WyrmpdmSync.Penpot;

/// <summary>
/// Reads Transit JSON (https://github.com/cognitect/transit-format), the encoding Penpot's API uses by default, into
/// plain JSON nodes. The plain JSON encoding leaves out the token library's ids, so the sync reads files this way.
/// </summary>
/// <remarks>
/// Mapping: maps → objects (keyword keys lose their "~:"), keywords → their name, uuids and instants → strings,
/// sets and lists → arrays, ordered maps (Penpot's "ordered-map" tag: a list of [key, value] pairs) → objects in
/// that order, and Penpot records such as token sets and tokens (tagged maps) → the map itself.
/// Other tagged values become { "~tag": name, "value": … }.
/// </remarks>
internal sealed class Transit
{
	private const int CacheSize = 44 * 44;
	private readonly List<string> _cache = [];

	public static JsonNode? Decode( string text ) => new Transit().Read( JsonNode.Parse( text ), asKey: false );

	private JsonNode? Read( JsonNode? node, bool asKey )
	{
		switch( node )
		{
			case JsonValue v when v.TryGetValue( out string? s ):
				return ReadString( s, asKey );
			case JsonValue v:
				return v.DeepClone();
			case JsonArray arr:
				return ReadArray( arr );
			case JsonObject obj:
				// verbose maps ({"~:a": 1}) and single-entry tagged maps ({"~#tag": value})
				if( obj.Count == 1 && obj.First().Key is var only && only.StartsWith( "~#", StringComparison.Ordinal ) )
					return Tagged( Remember( only )[ 2.. ], obj.First().Value );
				JsonObject result = [];
				foreach( (string k, JsonNode? val) in obj )
					result[ KeyText( ReadString( k, asKey: true ) ) ] = Read( val, asKey: false );
				return result;
			default:
				return null;
		}
	}

	private JsonNode? ReadArray( JsonArray arr )
	{
		if( arr.Count > 0 && arr[ 0 ] is JsonValue first && first.TryGetValue( out string? head ) )
		{
			if( head == "^ " )
			{
				JsonObject map = [];
				for( int i = 1; i + 1 < arr.Count; i += 2 )
				{
					string key = KeyText( Read( arr[ i ], asKey: true ) );
					map[ key ] = Read( arr[ i + 1 ], asKey: false );
				}
				return map;
			}
			if( arr.Count == 2 && TagOf( head ) is { } tag )
				return Tagged( tag, arr[ 1 ] );
		}
		JsonArray list = [];
		foreach( JsonNode? item in arr )
			list.Add( Read( item, asKey: false ) );
		return list;
	}

	/// <summary>The tag name if <paramref name="head"/> is a tag ("~#set" or a cache reference to one).</summary>
	private string? TagOf( string head )
	{
		if( head.StartsWith( "~#", StringComparison.Ordinal ) )
			return Remember( head )[ 2.. ];
		if( head.StartsWith( '^' ) && head != "^ " && Lookup( head ) is { } cached && cached.StartsWith( "~#", StringComparison.Ordinal ) )
			return cached[ 2.. ];
		return null;
	}

	private JsonNode? Tagged( string tag, JsonNode? rep )
	{
		JsonNode? value = Read( rep, asKey: false );
		switch( tag )
		{
			case "set" or "list":
				return value;
			case "cmap" or "ordered-map" when value is JsonArray pairs:
				JsonObject ordered = [];
				// cmap is a flat [k1, v1, k2, v2…]; ordered-map is [[k1, v1], [k2, v2]…]
				if( tag == "cmap" )
					for( int i = 0; i + 1 < pairs.Count; i += 2 )
						ordered[ KeyText( pairs[ i ] ) ] = pairs[ i + 1 ]?.DeepClone();
				else
					foreach( JsonNode? pair in pairs )
						if( pair is JsonArray kv && kv.Count == 2 )
							ordered[ KeyText( kv[ 0 ] ) ] = kv[ 1 ]?.DeepClone();
				return ordered;
			case "u" or "uuid":
				return JsonValue.Create( value?.ToString() );
		}
		// Penpot records (token set, token, theme…) are maps under a tag: keep the map, note the type
		if( value is JsonObject record )
		{
			record[ "~tag" ] = tag;
			return record;
		}
		return new JsonObject { [ "~tag" ] = tag, [ "value" ] = value };
	}

	private JsonNode? ReadString( string s, bool asKey, bool fromCache = false )
	{
		// a cache reference stands for a string read earlier; it was cached then, so it must not be cached again
		if( s.StartsWith( '^' ) && s != "^ " && !fromCache )
			return ReadString( Lookup( s ) ?? throw new SyncException( $"Transit cache reference {s} is unknown." ), asKey: false, fromCache: true );
		if( !fromCache )
			Remember( s, asKey );
		if( s.Length < 2 || s[ 0 ] != '~' )
			return JsonValue.Create( s );
		string rest = s[ 2.. ];
		return s[ 1 ] switch
		{
			'~' or '^' or '`' => JsonValue.Create( s[ 1.. ] ),
			':' or '$' => JsonValue.Create( rest ),
			'u' => JsonValue.Create( rest ),
			'm' => long.TryParse( rest, out long ms ) ? JsonValue.Create( DateTimeOffset.FromUnixTimeMilliseconds( ms ).ToString( "O" ) ) : JsonValue.Create( rest ),
			'i' or 'n' => long.TryParse( rest, out long n ) ? JsonValue.Create( n ) : JsonValue.Create( rest ),
			'd' => double.TryParse( rest, System.Globalization.CultureInfo.InvariantCulture, out double d ) ? JsonValue.Create( d ) : JsonValue.Create( rest ),
			'?' => JsonValue.Create( rest == "t" ),
			'_' => null,
			_ => JsonValue.Create( rest ),
		};
	}

	/// <summary>Adds a cacheable string to the read cache (keys and keywords longer than 3 characters), as the writer did.</summary>
	private string Remember( string s, bool asKey = false )
	{
		bool cacheable = s.Length > 3 && ( asKey || s.StartsWith( "~:", StringComparison.Ordinal ) || s.StartsWith( "~$", StringComparison.Ordinal ) || s.StartsWith( "~#", StringComparison.Ordinal ) );
		if( cacheable )
		{
			if( _cache.Count == CacheSize )
				_cache.Clear();
			_cache.Add( s );
		}
		return s;
	}

	private string? Lookup( string code )
	{
		int index = code.Length switch
		{
			2 => code[ 1 ] - 48,
			3 => ( code[ 1 ] - 48 ) * 44 + ( code[ 2 ] - 48 ),
			_ => -1,
		};
		return index >= 0 && index < _cache.Count ? _cache[ index ] : null;
	}

	private static string KeyText( JsonNode? key ) => key switch
	{
		null => "",
		JsonValue v when v.TryGetValue( out string? s ) => s,
		_ => key.ToJsonString(),
	};
}
