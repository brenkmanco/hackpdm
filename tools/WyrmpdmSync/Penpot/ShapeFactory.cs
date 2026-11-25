using System.Text.Json.Nodes;

namespace WyrmpdmSync.Penpot;

/// <summary>
/// Builds new Penpot layers for `push`: frames for XAML panels, text copied from an existing text layer (so its
/// structure is one Penpot already accepts), and copies of components the way the editor makes them.
/// New layers are placed at their parent's origin; Penpot's layout puts them in order the next time the design is
/// edited (its API stores layout, it doesn't run it).
/// </summary>
internal static class ShapeFactory
{
	/// <summary>A new layer and its descendants, parent first, ready for add-obj changes.</summary>
	public sealed record NewLayer( Guid Id, List<JsonObject> Objects );

	public static JsonObject Matrix() => new() { [ "~tag" ] = "matrix", [ "a" ] = 1, [ "b" ] = 0, [ "c" ] = 0, [ "d" ] = 1, [ "e" ] = 0, [ "f" ] = 0 };

	/// <summary>Sets a layer's position and size, with the bounding rectangle and corner points Penpot keeps alongside.</summary>
	public static void Place( JsonObject shape, double x, double y, double width, double height )
	{
		shape[ "x" ] = x;
		shape[ "y" ] = y;
		shape[ "width" ] = width;
		shape[ "height" ] = height;
		shape[ "selrect" ] = new JsonObject
		{
			[ "~tag" ] = "rect", [ "x" ] = x, [ "y" ] = y, [ "width" ] = width, [ "height" ] = height,
			[ "x1" ] = x, [ "y1" ] = y, [ "x2" ] = x + width, [ "y2" ] = y + height,
		};
		shape[ "points" ] = new JsonArray(
			Point( x, y ), Point( x + width, y ), Point( x + width, y + height ), Point( x, y + height ) );

		static JsonObject Point( double px, double py ) => new() { [ "~tag" ] = "point", [ "x" ] = px, [ "y" ] = py };
	}

	/// <summary>A frame with flex layout (a XAML StackPanel) or without (a Grid or Border).</summary>
	public static NewLayer Frame( string name, JsonObject parent, double width, double height, string? flexDir,
		double gap, (double L, double T, double R, double B) padding, double radius, JsonArray fills, JsonObject appliedTokens )
	{
		Guid id = Guid.NewGuid();
		JsonObject shape = new()
		{
			[ "~tag" ] = "shape", [ "id" ] = id.ToString(), [ "type" ] = "frame", [ "name" ] = name,
			[ "parent-id" ] = parent[ "id" ]!.ToString(), [ "frame-id" ] = parent[ "id" ]!.ToString(),
			[ "rotation" ] = 0, [ "transform" ] = Matrix(), [ "transform-inverse" ] = Matrix(),
			[ "proportion" ] = 1, [ "proportion-lock" ] = false,
			[ "fills" ] = fills, [ "strokes" ] = new JsonArray(), [ "shapes" ] = new JsonArray(),
			[ "r1" ] = radius, [ "r2" ] = radius, [ "r3" ] = radius, [ "r4" ] = radius,
			[ "applied-tokens" ] = appliedTokens,
		};
		Place( shape, Num( parent[ "x" ] ), Num( parent[ "y" ] ), width, height );
		if( flexDir is not null )
		{
			shape[ "layout" ] = "flex";
			shape[ "layout-flex-dir" ] = flexDir;
			shape[ "layout-gap-type" ] = "multiple";
			shape[ "layout-gap" ] = new JsonObject { [ "row-gap" ] = flexDir == "column" ? gap : 0, [ "column-gap" ] = flexDir == "row" ? gap : 0 };
			shape[ "layout-padding-type" ] = "multiple";
			shape[ "layout-padding" ] = new JsonObject { [ "p1" ] = padding.T, [ "p2" ] = padding.R, [ "p3" ] = padding.B, [ "p4" ] = padding.L };
			shape[ "layout-wrap-type" ] = "nowrap";
			shape[ "layout-align-items" ] = "start";
			shape[ "layout-justify-content" ] = "start";
			shape[ "layout-align-content" ] = "stretch";
			shape[ "layout-item-h-sizing" ] = "auto";
			shape[ "layout-item-v-sizing" ] = "auto";
		}
		return new NewLayer( id, [ shape ] );
	}

	/// <summary>A copy of <paramref name="template"/> (a text layer) holding <paramref name="content"/>, under <paramref name="parent"/>.</summary>
	public static NewLayer Text( JsonObject template, JsonObject parent, string name, JsonObject content, JsonObject appliedTokens )
	{
		Guid id = Guid.NewGuid();
		JsonObject shape = (JsonObject)template.DeepClone();
		foreach( string key in new[] { "touched", "shape-ref", "component-id", "component-file", "component-root", "main-instance", "position-data" } )
			shape.Remove( key );
		shape[ "~tag" ] = "shape";
		shape[ "id" ] = id.ToString();
		shape[ "name" ] = name;
		shape[ "parent-id" ] = parent[ "id" ]!.ToString();
		shape[ "frame-id" ] = parent[ "id" ]!.ToString();
		shape[ "content" ] = content;
		shape[ "applied-tokens" ] = appliedTokens;
		shape[ "grow-type" ] = "auto-width";
		Place( shape, Num( parent[ "x" ] ), Num( parent[ "y" ] ), Num( template[ "width" ] ), Num( template[ "height" ] ) );
		return new NewLayer( id, [ shape ] );
	}

	/// <summary>
	/// A copy of a component: its main layer tree with new ids, each layer pointing back at the one it copies
	/// (<c>shape-ref</c>), moved under <paramref name="parent"/>.
	/// </summary>
	public static NewLayer Copy( JsonObject objects, JsonObject main, Guid fileId, JsonObject parent )
	{
		// parent first, children in Penpot's list order, so appending each one rebuilds the same lists
		List<JsonObject> tree = [];
		void Walk( JsonObject shape )
		{
			tree.Add( shape );
			foreach( JsonNode? child in shape[ "shapes" ] as JsonArray ?? [] )
				if( objects[ child!.ToString() ] is JsonObject c )
					Walk( c );
		}
		Walk( main );
		Dictionary<string, string> ids = tree.ToDictionary( s => s[ "id" ]!.ToString(), _ => Guid.NewGuid().ToString() );
		double dx = Num( parent[ "x" ] ) - Num( main[ "x" ] ), dy = Num( parent[ "y" ] ) - Num( main[ "y" ] );
		List<JsonObject> copies = [];
		foreach( JsonObject original in tree )
		{
			JsonObject copy = (JsonObject)original.DeepClone();
			string originalId = original[ "id" ]!.ToString();
			foreach( string key in new[] { "touched", "main-instance", "variant-id", "variant-name", "position-data", "is-variant-container" } )
				copy.Remove( key );
			copy[ "~tag" ] = "shape";
			copy[ "id" ] = ids[ originalId ];
			copy[ "shape-ref" ] = originalId;
			bool root = original == main;
			copy[ "parent-id" ] = root ? parent[ "id" ]!.ToString() : ids.GetValueOrDefault( original[ "parent-id" ]!.ToString(), parent[ "id" ]!.ToString() );
			copy[ "frame-id" ] = root ? parent[ "id" ]!.ToString() : ids.GetValueOrDefault( original[ "frame-id" ]!.ToString(), copy[ "parent-id" ]!.ToString() );
			if( copy[ "shapes" ] is JsonArray children )
				copy[ "shapes" ] = new JsonArray( [ .. children.Select( c => (JsonNode)JsonValue.Create( ids.GetValueOrDefault( c!.ToString(), c!.ToString() ) )! ) ] );
			if( root )
			{
				copy[ "component-root" ] = true;
				copy[ "component-file" ] = fileId.ToString();
			}
			Place( copy, Num( original[ "x" ] ) + dx, Num( original[ "y" ] ) + dy, Num( original[ "width" ] ), Num( original[ "height" ] ) );
			copies.Add( copy );
		}
		return new NewLayer( Guid.Parse( copies[ 0 ][ "id" ]!.ToString() ), copies );
	}

	/// <summary>The add-obj changes for a new layer tree. The root goes in at <paramref name="index"/> of its parent's layer list.</summary>
	public static IEnumerable<JsonObject> AddChanges( NewLayer layer, Guid pageId, int index )
	{
		bool first = true;
		foreach( JsonObject shape in layer.Objects )
		{
			JsonObject obj = (JsonObject)shape.DeepClone();
			// children arrive through their own add-obj; Penpot fills each parent's list as they come
			obj[ "shapes" ] = new JsonArray();
			JsonObject change = new()
			{
				[ "type" ] = "add-obj", [ "id" ] = shape[ "id" ]!.ToString(), [ "pageId" ] = pageId.ToString(),
				[ "parentId" ] = shape[ "parent-id" ]!.ToString(), [ "frameId" ] = shape[ "frame-id" ]!.ToString(),
				[ "obj" ] = obj, [ "ignoreTouched" ] = true,
			};
			// the root takes its place among its new siblings; the rest append, in list order
			if( first )
				change[ "index" ] = index;
			first = false;
			yield return change;
		}
	}

	public static double Num( JsonNode? node ) => node is JsonValue v && double.TryParse( v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d ) ? d : 0;
}
