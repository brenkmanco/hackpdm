using System.Text.Json.Nodes;

namespace WyrmpdmSync.Penpot;

/// <summary>Walking and reading Penpot's shape tree (one page's objects map).</summary>
internal static class ShapeTree
{
	/// <summary>
	/// A shape's children in the order they appear: Penpot lists a frame's <c>shapes</c> bottom layer first, and
	/// flex layouts place the top layer first, so the visual order is the list reversed.
	/// </summary>
	public static List<JsonObject> Children( JsonObject objects, JsonObject shape )
	{
		List<JsonObject> children = [];
		if( shape[ "shapes" ] is JsonArray ids )
			for( int i = ids.Count - 1; i >= 0; i-- )
				if( objects[ ids[ i ]!.ToString() ] is JsonObject child )
					children.Add( child );
		return children;
	}

	/// <summary>The top-level frame named <paramref name="name"/> on a page.</summary>
	public static JsonObject? Frame( PenpotPage page, string name )
		=> page.Objects.Select( kv => kv.Value ).OfType<JsonObject>()
			.FirstOrDefault( o => o[ "type" ]?.ToString() == "frame" && o[ "name" ]?.ToString() == name
			                      && o[ "parent-id" ]?.ToString() == Guid.Empty.ToString() );

	/// <summary>
	/// The layer at <paramref name="path"/> below <paramref name="root"/>: names separated by "/", "#n" choosing the
	/// n-th same-named sibling in visual order ("Icon button#2").
	/// </summary>
	public static JsonObject? Resolve( JsonObject objects, JsonObject root, string path )
	{
		JsonObject current = root;
		foreach( string step in path.Split( '/', StringSplitOptions.RemoveEmptyEntries ) )
		{
			string name = step;
			int nth = 1;
			int hash = step.LastIndexOf( '#' );
			if( hash > 0 && int.TryParse( step[ ( hash + 1 ).. ], out int n ) )
			{
				name = step[ ..hash ];
				nth = n;
			}
			JsonObject? next = Children( objects, current ).Where( c => c[ "name" ]?.ToString() == name ).Skip( nth - 1 ).FirstOrDefault();
			if( next is null )
				return null;
			current = next;
		}
		return current;
	}

	/// <summary>The path of <paramref name="shape"/> below <paramref name="root"/>, as <see cref="Resolve"/> reads it.</summary>
	public static string? PathOf( JsonObject objects, JsonObject root, JsonObject shape )
	{
		List<string> steps = [];
		string rootId = root[ "id" ]!.ToString();
		for( JsonObject current = shape; current[ "id" ]?.ToString() != rootId; )
		{
			if( objects[ current[ "parent-id" ]?.ToString() ?? "" ] is not JsonObject parent )
				return null;
			string name = current[ "name" ]?.ToString() ?? "";
			List<JsonObject> same = [ .. Children( objects, parent ).Where( c => c[ "name" ]?.ToString() == name ) ];
			int index = same.FindIndex( c => c[ "id" ]?.ToString() == current[ "id" ]?.ToString() ) + 1;
			steps.Insert( 0, index > 1 ? $"{name}#{index}" : name );
			current = parent;
		}
		return string.Join( "/", steps );
	}

	/// <summary>Every shape below <paramref name="root"/>, depth first, in visual order.</summary>
	public static IEnumerable<JsonObject> Descendants( JsonObject objects, JsonObject root )
	{
		foreach( JsonObject child in Children( objects, root ) )
		{
			yield return child;
			foreach( JsonObject d in Descendants( objects, child ) )
				yield return d;
		}
	}

	/// <summary>The plain text of a text shape (paragraphs joined by newlines); null for other shapes.</summary>
	public static string? Text( JsonObject shape )
	{
		if( shape[ "type" ]?.ToString() != "text" || shape[ "content" ] is not JsonObject content )
			return null;
		List<string> paragraphs = [];
		foreach( JsonNode? set in content[ "children" ] as JsonArray ?? [] )
			foreach( JsonNode? paragraph in set?[ "children" ] as JsonArray ?? [] )
				paragraphs.Add( string.Concat( ( paragraph?[ "children" ] as JsonArray ?? [] ).Select( leaf => leaf?[ "text" ]?.ToString() ) ) );
		return string.Join( "\n", paragraphs );
	}

	/// <summary>
	/// The content of a text shape with its text replaced, keeping the first run's styling; null when the text has
	/// several differently styled runs (those are edited in Penpot).
	/// </summary>
	public static JsonObject? WithText( JsonObject shape, string text )
	{
		if( shape[ "content" ] is not JsonObject content || content.DeepClone() is not JsonObject copy )
			return null;
		JsonArray sets = copy[ "children" ] as JsonArray ?? [];
		JsonObject? firstSet = sets.FirstOrDefault() as JsonObject;
		JsonObject? firstParagraph = ( firstSet?[ "children" ] as JsonArray )?.FirstOrDefault() as JsonObject;
		JsonArray? leaves = firstParagraph?[ "children" ] as JsonArray;
		if( firstSet is null || firstParagraph is null || leaves is null || leaves.Count == 0 )
			return null;
		int runs = sets.Sum( s => ( s?[ "children" ] as JsonArray ?? [] ).Sum( p => ( p?[ "children" ] as JsonArray ?? [] ).Count ) );
		if( runs > 1 )
			return null;
		( (JsonObject)leaves[ 0 ]! )[ "text" ] = text;
		return copy;
	}

	/// <summary>The component a copy was made from (its root's component id), if any.</summary>
	public static Guid? ComponentOf( JsonObject shape )
		=> Guid.TryParse( shape[ "component-id" ]?.ToString(), out Guid id ) ? id : null;
}
