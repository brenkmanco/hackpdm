using System.Text.Json.Nodes;

namespace WyrmpdmSync.Penpot;

/// <summary>A Penpot design token as stored in the file.</summary>
internal sealed record PenpotToken( Guid Id, Guid SetId, string SetName, string Name, string Type, string Value );

/// <summary>A token set (a group of tokens that can be switched on and off).</summary>
internal sealed record PenpotTokenSet( Guid Id, string Name );

/// <summary>A color in the file's library (the Assets panel).</summary>
internal sealed record PenpotLibraryColor( Guid Id, string Name, string Path, string Color, double Opacity );

/// <summary>A component (one variant of a variant set), with where its main shape lives.</summary>
internal sealed record PenpotComponent( Guid Id, string Name, Guid MainInstanceId, Guid MainInstancePage, IReadOnlyDictionary<string, string> Variant );

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
	public IReadOnlyList<PenpotTokenSet> TokenSets { get; }
	public IReadOnlyList<Guid> ActiveSetIds { get; }
	public IReadOnlyList<Guid> ActiveThemeIds { get; }
	public IReadOnlyList<PenpotLibraryColor> LibraryColors { get; }
	public IReadOnlyList<PenpotComponent> Components { get; }
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
		List<PenpotTokenSet> sets = [];
		Tokens = ReadTokens( data, sets );
		TokenSets = sets;
		JsonNode? tokensStatus = data[ "tokens-status" ] ?? data[ "tokensStatus" ];
		ActiveSetIds = ReadGuids( tokensStatus?[ "active-set-ids" ] ?? tokensStatus?[ "activeSetIds" ] );
		ActiveThemeIds = ReadGuids( tokensStatus?[ "active-theme-ids" ] ?? tokensStatus?[ "activeThemeIds" ] );
		Components = data[ "components" ] is JsonObject comps
			? [ .. comps.Select( kv => kv.Value ).OfType<JsonObject>().Select( ReadComponent ).OfType<PenpotComponent>() ]
			: [];
		LibraryColors = data[ "colors" ] is JsonObject colors
			? [ .. colors.Select( kv => kv.Value ).OfType<JsonObject>().Select( ReadLibraryColor ).OfType<PenpotLibraryColor>() ]
			: [];
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

	private static List<PenpotToken> ReadTokens( JsonObject data, List<PenpotTokenSet> sets )
	{
		List<PenpotToken> tokens = [];
		JsonNode? lib = data[ "tokens-lib" ] ?? data[ "tokensLib" ];
		if( lib is not null )
			CollectSets( lib, tokens, sets );
		return tokens;
	}

	/// <summary>The main shape of a component, or null when its page or shape is gone.</summary>
	public JsonObject? MainShape( PenpotComponent component )
		=> Pages.FirstOrDefault( p => p.Id == component.MainInstancePage )?.Objects[ component.MainInstanceId.ToString() ] as JsonObject;

	private static PenpotComponent? ReadComponent( JsonObject c )
	{
		if( !Guid.TryParse( Str( c[ "id" ] ), out Guid id ) || !Guid.TryParse( Str( c[ "main-instance-id" ] ), out Guid main )
		    || !Guid.TryParse( Str( c[ "main-instance-page" ] ), out Guid page ) || c[ "deleted" ] is JsonValue d && d.ToString() == "true" )
			return null;
		Dictionary<string, string> variant = new( StringComparer.Ordinal );
		if( c[ "variant-properties" ] is JsonArray props )
			foreach( JsonNode? prop in props )
				if( Str( prop?[ "name" ] ) is { } name )
					variant[ name ] = Str( prop?[ "value" ] ) ?? "";
		return new PenpotComponent( id, Str( c[ "name" ] ) ?? "", main, page, variant );
	}

	private static PenpotLibraryColor? ReadLibraryColor( JsonObject c )
		=> Guid.TryParse( Str( c[ "id" ] ), out Guid id ) && Str( c[ "color" ] ) is { } color
			? new PenpotLibraryColor( id, Str( c[ "name" ] ) ?? "", Str( c[ "path" ] ) ?? "", color,
				c[ "opacity" ] is JsonValue o && double.TryParse( o.ToString(), System.Globalization.CultureInfo.InvariantCulture, out double op ) ? op : 1 )
			: null;

	/// <summary>Finds every token set (an object with an id, a name and a "tokens" map) under <paramref name="node"/>.</summary>
	private static void CollectSets( JsonNode node, List<PenpotToken> into, List<PenpotTokenSet> sets )
	{
		switch( node )
		{
			case JsonObject obj:
				if( obj[ "tokens" ] is JsonObject tokens && Guid.TryParse( Str( obj[ "id" ] ), out Guid setId ) )
				{
					string setName = Str( obj[ "name" ] ) ?? "";
					sets.Add( new PenpotTokenSet( setId, setName ) );
					foreach( (string _, JsonNode? t) in tokens )
						if( t is JsonObject token && ReadToken( token, setId, setName ) is { } read )
							into.Add( read );
					return;
				}
				foreach( (string _, JsonNode? child) in obj )
					if( child is not null )
						CollectSets( child, into, sets );
				break;
			case JsonArray arr:
				foreach( JsonNode? child in arr )
					if( child is not null )
						CollectSets( child, into, sets );
				break;
		}
	}

	private static PenpotToken? ReadToken( JsonObject t, Guid setId, string setName )
		=> Guid.TryParse( Str( t[ "id" ] ), out Guid id ) && Str( t[ "name" ] ) is { } name
			? new PenpotToken( id, setId, setName, name, Str( t[ "type" ] ) ?? "", t[ "value" ] is JsonValue v ? v.ToString() : t[ "value" ]?.ToJsonString() ?? "" )
			: null;

	private static List<Guid> ReadGuids( JsonNode? node ) => node is JsonArray arr
		? [ .. arr.Select( n => Guid.TryParse( Str( n ), out Guid g ) ? g : Guid.Empty ).Where( g => g != Guid.Empty ) ]
		: [];

	private static string? Str( JsonNode? node ) => node is JsonValue v && v.TryGetValue( out string? s ) ? s : null;

	private static long Long( JsonNode? node ) => node is JsonValue v && long.TryParse( v.ToString(), out long n ) ? n : 0;
}
