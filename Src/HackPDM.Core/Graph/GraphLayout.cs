namespace HackPDM.Core.Graph;

/// <summary>Spacing and node sizes for <see cref="GraphLayout"/>, in device-independent pixels.</summary>
public sealed record LayoutOptions
{
	public double ColumnGap { get; init; } = 120;
	public double RowGap { get; init; } = 28;
	public double Margin { get; init; } = 40;
	/// <summary>The size of the smallest file's box (10 KB and below).</summary>
	public double MinWidth { get; init; } = 170;
	public double MinHeight { get; init; } = 60;
	/// <summary>The size of the largest file's box (100 MB and above).</summary>
	public double MaxWidth { get; init; } = 290;
	public double MaxHeight { get; init; } = 128;
}

/// <summary>Where one file's box is drawn.</summary>
/// <param name="Node">The file.</param>
/// <param name="Layer">Its column: 0 for the selected files, positive for children, negative for parents.</param>
/// <param name="IsRoot">Whether it is one of the selected files.</param>
public sealed record NodeBox( GraphNode Node, int Layer, double X, double Y, double Width, double Height, bool IsRoot )
{
	/// <summary>The input port: the middle of the left side (where links from parents arrive).</summary>
	public (double X, double Y) InPort => (X, Y + Height / 2);
	/// <summary>The output port: the middle of the right side (where links to children leave).</summary>
	public (double X, double Y) OutPort => (X + Width, Y + Height / 2);
}

/// <summary>A link drawn from a parent's output port to a child's input port.</summary>
public sealed record EdgeRoute( GraphEdge Edge, double FromX, double FromY, double ToX, double ToY );

/// <summary>A laid-out graph: boxes, links and the size of the area they cover.</summary>
public sealed record GraphLayoutResult( IReadOnlyList<NodeBox> Nodes, IReadOnlyList<EdgeRoute> Edges, double Width, double Height )
{
	public static GraphLayoutResult Empty { get; } = new( [], [], 0, 0 );

	/// <summary>The box of file <paramref name="id"/>; null when it is not in the graph.</summary>
	public NodeBox? Find( int id ) => Nodes.FirstOrDefault( n => n.Node.Id == id );
}

/// <summary>
/// Lays a dependency graph out in columns, parents always left of their children: with
/// <see cref="GraphDirection.Children"/> the selected files are the leftmost column, with
/// <see cref="GraphDirection.Parents"/> the rightmost, with <see cref="GraphDirection.Both"/> in the middle.
/// A file sits one column past the farthest file that links to it, so a part used both directly and
/// through a sub-assembly lines up after the sub-assembly. Boxes grow with the file's size.
/// </summary>
public static class GraphLayout
{
	/// <summary>How big a file's box is, from 0 (10 KB or less) to 1 (100 MB or more), on a log scale.</summary>
	public static double SizeFactor( long bytes ) => Math.Clamp( ( Math.Log10( Math.Max( bytes, 1 ) ) - 4 ) / 4, 0, 1 );

	/// <summary>The box size for a file of <paramref name="bytes"/>.</summary>
	public static (double Width, double Height) BoxSize( long bytes, LayoutOptions? options = null )
	{
		options ??= new LayoutOptions();
		double t = SizeFactor( bytes );
		return (options.MinWidth + ( options.MaxWidth - options.MinWidth ) * t, options.MinHeight + ( options.MaxHeight - options.MinHeight ) * t);
	}

	/// <summary>Lays <paramref name="graph"/> out for <paramref name="direction"/>.</summary>
	/// <param name="graph">The graph; edges to files not in <see cref="DependencyGraph.Nodes"/> are ignored.</param>
	/// <param name="direction">Which side of the selected files the other files go.</param>
	/// <param name="options">Spacing and sizes; the defaults when null.</param>
	/// <returns>The boxes and links; empty for an empty graph.</returns>
	public static GraphLayoutResult Arrange( DependencyGraph graph, GraphDirection direction, LayoutOptions? options = null )
	{
		options ??= new LayoutOptions();
		Dictionary<int, GraphNode> nodes = graph.Nodes.DistinctBy( n => n.Id ).ToDictionary( n => n.Id );
		if( nodes.Count == 0 )
			return GraphLayoutResult.Empty;

		List<GraphEdge> edges = [ .. graph.Edges.Where( e => e.Parent != e.Child && nodes.ContainsKey( e.Parent ) && nodes.ContainsKey( e.Child ) ).Distinct() ];
		ILookup<int, int> childrenOf = edges.ToLookup( e => e.Parent, e => e.Child );
		ILookup<int, int> parentsOf = edges.ToLookup( e => e.Child, e => e.Parent );
		List<int> roots = [ .. graph.Roots.Where( nodes.ContainsKey ).Distinct() ];
		HashSet<int> rootSet = [ .. roots ];

		Dictionary<int, int> layer = roots.ToDictionary( r => r, _ => 0 );
		List<int> discovery = [ .. roots ];
		HashSet<int> down = [];
		if( direction != GraphDirection.Parents )
		{
			down = Reach( roots, childrenOf, discovery );
			Relax( down, rootSet, childrenOf, layer, +1, nodes.Count );
		}
		if( direction != GraphDirection.Children )
		{
			HashSet<int> up = Reach( roots, parentsOf, discovery );
			up.ExceptWith( down.Except( rootSet ) );
			Relax( up, rootSet, parentsOf, layer, -1, nodes.Count );
		}
		// anything the server sent that is not linked to a selected file joins the selected column
		foreach( int id in nodes.Keys.Except( layer.Keys ).Order() )
		{
			layer[ id ] = 0;
			discovery.Add( id );
		}

		List<List<int>> columns = OrderColumns( layer, discovery, childrenOf, parentsOf );
		return Place( columns, layer, nodes, rootSet, edges, options );
	}

	/// <summary>Everything reachable from <paramref name="roots"/> through <paramref name="next"/>, the roots included; new ids are appended to <paramref name="discovery"/>.</summary>
	private static HashSet<int> Reach( List<int> roots, ILookup<int, int> next, List<int> discovery )
	{
		HashSet<int> seen = [ .. roots ];
		Queue<int> queue = new( roots );
		while( queue.TryDequeue( out int id ) )
			foreach( int n in next[ id ] )
				if( seen.Add( n ) )
				{
					queue.Enqueue( n );
					if( !discovery.Contains( n ) )
						discovery.Add( n );
				}
		return seen;
	}

	/// <summary>Longest-path layering: each file goes one step (<paramref name="step"/>) past the farthest file linking to it. Cycles stop growing after <paramref name="limit"/> passes.</summary>
	private static void Relax( HashSet<int> set, HashSet<int> roots, ILookup<int, int> next, Dictionary<int, int> layer, int step, int limit )
	{
		for( int pass = 0; pass < limit; pass++ )
		{
			bool changed = false;
			foreach( int id in set )
			{
				if( !layer.TryGetValue( id, out int at ) )
					continue;
				foreach( int n in next[ id ] )
				{
					if( !set.Contains( n ) || roots.Contains( n ) )
						continue;
					int want = at + step;
					if( Math.Abs( want ) > limit )
						continue;
					if( !layer.TryGetValue( n, out int current ) || ( step > 0 ? want > current : want < current ) )
					{
						layer[ n ] = want;
						changed = true;
					}
				}
			}
			if( !changed )
				break;
		}
	}

	/// <summary>The files of each column, top to bottom, ordered to cross fewer links (barycenter sweeps).</summary>
	private static List<List<int>> OrderColumns( Dictionary<int, int> layer, List<int> discovery, ILookup<int, int> childrenOf, ILookup<int, int> parentsOf )
	{
		Dictionary<int, int> found = discovery.Select( ( id, i ) => (id, i) ).ToDictionary( p => p.id, p => p.i );
		List<List<int>> columns = [ .. layer.GroupBy( p => p.Value ).OrderBy( g => g.Key )
			.Select( g => g.Select( p => p.Key ).OrderBy( id => found.GetValueOrDefault( id, int.MaxValue ) ).ToList() ) ];

		for( int sweep = 0; sweep < 4; sweep++ )
		{
			bool rightward = sweep % 2 == 0;
			IEnumerable<int> order = rightward ? Enumerable.Range( 1, columns.Count - 1 ) : Enumerable.Range( 0, columns.Count - 1 ).Reverse();
			foreach( int c in order )
			{
				List<int> reference = columns[ rightward ? c - 1 : c + 1 ];
				Dictionary<int, int> at = reference.Select( ( id, i ) => (id, i) ).ToDictionary( p => p.id, p => p.i );
				List<int> column = columns[ c ];
				Dictionary<int, double> weight = [];
				for( int i = 0; i < column.Count; i++ )
				{
					int id = column[ i ];
					// children view: compare with the column on the left through parents; on the right through children
					List<int> linked = [ .. ( rightward ? parentsOf[ id ] : childrenOf[ id ] ).Where( at.ContainsKey ) ];
					weight[ id ] = linked.Count > 0 ? linked.Average( n => at[ n ] ) : i;
				}
				columns[ c ] = [ .. column.Select( ( id, i ) => (id, i) ).OrderBy( p => weight[ p.id ] ).ThenBy( p => p.i ).Select( p => p.id ) ];
			}
		}
		return columns;
	}

	private static GraphLayoutResult Place( List<List<int>> columns, Dictionary<int, int> layer, Dictionary<int, GraphNode> nodes,
		HashSet<int> roots, List<GraphEdge> edges, LayoutOptions o )
	{
		var sizes = nodes.ToDictionary( p => p.Key, p => BoxSize( p.Value.Size, o ) );
		double[] columnWidth = [ .. columns.Select( c => c.Max( id => sizes[ id ].Width ) ) ];
		double[] columnHeight = [ .. columns.Select( c => c.Sum( id => sizes[ id ].Height ) + o.RowGap * ( c.Count - 1 ) ) ];
		double tallest = columnHeight.Max();

		List<NodeBox> boxes = [];
		double x = o.Margin;
		for( int c = 0; c < columns.Count; c++ )
		{
			double y = o.Margin + ( tallest - columnHeight[ c ] ) / 2;
			foreach( int id in columns[ c ] )
			{
				var (w, h) = sizes[ id ];
				boxes.Add( new NodeBox( nodes[ id ], layer[ id ], x + ( columnWidth[ c ] - w ) / 2, y, w, h, roots.Contains( id ) ) );
				y += h + o.RowGap;
			}
			x += columnWidth[ c ] + o.ColumnGap;
		}

		Dictionary<int, NodeBox> byId = boxes.ToDictionary( b => b.Node.Id );
		List<EdgeRoute> routes = [ .. edges.Select( e =>
		{
			var (fx, fy) = byId[ e.Parent ].OutPort;
			var (tx, ty) = byId[ e.Child ].InPort;
			return new EdgeRoute( e, fx, fy, tx, ty );
		} ) ];
		return new GraphLayoutResult( boxes, routes, x - o.ColumnGap + o.Margin, tallest + 2 * o.Margin );
	}
}
