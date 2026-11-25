using System.Text.Json.Nodes;

namespace WyrmpdmSync.Penpot;

/// <summary>A Penpot design token as stored in the file.</summary>
internal sealed record PenpotToken( Guid Id, Guid SetId, string SetName, string Name, string Type, string Value );

/// <summary>A page of the file.</summary>
internal sealed record PenpotPage( Guid Id, string Name, JsonObject Objects );

/// <summary>
/// The parts of a Penpot file the sync needs, read from the JSON that <c>get-file</c> returns.
/// The reader looks for things by their shape (a token is an object with id, name, type and value inside a set's
/// "tokens" map) rather than by one fixed path, so small changes in Penpot's storage layout don't break it.
/// </summary>
internal sealed class PenpotFile
{
	public Guid Id { get; }
	public string Name { get; }
	public long Revn { get; }
	public long Vern { get; }
	public IReadOnlyList<string> Features { get; }
	public IReadOnlyList<PenpotPage> Pages { get; }
	public IReadOnlyList<PenpotToken> Tokens { get; }
	public JsonObject Raw { get; }

	private PenpotFile( JsonObject raw )
	{
		Raw = raw;
		Id = Guid.Parse( Str( raw[ "id" ] ) ?? throw new SyncException( "The Penpot file has no id." ) );
		Name = Str( raw[ "name" ] ) ?? "";
		Revn = Long( raw[ "revn" ] );
		Vern = Long( raw[ "vern" ] );
		Features = raw[ "features" ] is JsonArray f ? [ .. f.Select( n => Str( n ) ?? "" ) ] : [];
		JsonObject data = raw[ "data" ] as JsonObject ?? throw new SyncException( "The Penpot file came back without its data." );
		Pages = ReadPages( data );
		Tokens = ReadTokens( data );
	}

	public static PenpotFile Parse( JsonObject raw ) => new( raw );

	/// <summary>The page called <paramref name="name"/>, if any.</summary>
	public PenpotPage? Page( string name ) => Pages.FirstOrDefault( p => p.Name == name );

	/// <summary>The shape with <paramref name="id"/> on any page, and its page.</summary>
	public (PenpotPage Page, JsonObject Shape)? FindShape( Guid id )
	{
		string key = id.ToString();
		foreach( PenpotPage page in Pages )
			if( page.Objects[ key ] is JsonObject shape )
				return (page, shape);
		return null;
	}

	private static List<PenpotPage> ReadPages( JsonObject data )
	{
		List<PenpotPage> pages = [];
		if( ( data[ "pages-index" ] ?? data[ "pagesIndex" ] ) is not JsonObject index )
			return pages;
		// "pages" keeps the user's page order; fall back to the index order
		IEnumerable<string> order = data[ "pages" ] is JsonArray ids
			? ids.Select( n => Str( n ) ?? "" )
			: index.Select( kv => kv.Key );
		foreach( string id in order )
		{
			if( index[ id ] is not JsonObject page || !Guid.TryParse( id, out Guid pageId ) )
				continue;
			pages.Add( new PenpotPage( pageId, Str( page[ "name" ] ) ?? "", page[ "objects" ] as JsonObject ?? [] ) );
		}
		return pages;
	}

	private static List<PenpotToken> ReadTokens( JsonObject data )
	{
		List<PenpotToken> tokens = [];
		JsonNode? lib = data[ "tokens-lib" ] ?? data[ "tokensLib" ];
		if( lib is not null )
			CollectSets( lib, tokens );
		return tokens;
	}

	/// <summary>Finds every token set (an object with an id, a name and a "tokens" map) under <paramref name="node"/>.</summary>
	private static void CollectSets( JsonNode node, List<PenpotToken> into )
	{
		switch( node )
		{
			case JsonObject obj:
				if( obj[ "tokens" ] is JsonObject tokens && Guid.TryParse( Str( obj[ "id" ] ), out Guid setId ) )
				{
					string setName = Str( obj[ "name" ] ) ?? "";
					foreach( (string _, JsonNode? t) in tokens )
						if( t is JsonObject token && ReadToken( token, setId, setName ) is { } read )
							into.Add( read );
					return;
				}
				foreach( (string _, JsonNode? child) in obj )
					if( child is not null )
						CollectSets( child, into );
				break;
			case JsonArray arr:
				foreach( JsonNode? child in arr )
					if( child is not null )
						CollectSets( child, into );
				break;
		}
	}

	private static PenpotToken? ReadToken( JsonObject t, Guid setId, string setName )
		=> Guid.TryParse( Str( t[ "id" ] ), out Guid id ) && Str( t[ "name" ] ) is { } name
			? new PenpotToken( id, setId, setName, name, Str( t[ "type" ] ) ?? "", t[ "value" ] is JsonValue v ? v.ToString() : t[ "value" ]?.ToJsonString() ?? "" )
			: null;

	private static string? Str( JsonNode? node ) => node is JsonValue v && v.TryGetValue( out string? s ) ? s : null;

	private static long Long( JsonNode? node ) => node is JsonValue v && long.TryParse( v.ToString(), out long n ) ? n : 0;
}
