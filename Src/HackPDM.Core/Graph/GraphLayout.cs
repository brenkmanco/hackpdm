using System;
using System.Collections.Generic;
using System.Linq;

namespace HackPDM.Core.Graph;

/// <summary>Spacing and node sizes for <see cref="GraphLayout"/>, in device-independent pixels.</summary>
public sealed record LayoutOptions
{
	public static LayoutOptions Default { get; } = new();

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

/// <summary>
/// A link drawn from a parent's output port to a child's input port, optionally passing through
/// intermediate <see cref="BendPoints"/> that route multi-layer or cycle links around intervening boxes.
/// </summary>
public sealed record EdgeRoute( GraphEdge Edge, double FromX, double FromY, double ToX, double ToY )
{
	/// <summary>Optional intermediate waypoints (empty for direct adjacent-column links).</summary>
	public IReadOnlyList<(double X, double Y)> BendPoints { get; init; } = [];
}

/// <summary>A laid-out graph: boxes, links and the size of the area they cover.</summary>
public sealed record GraphLayoutResult( IReadOnlyList<NodeBox> Nodes, IReadOnlyList<EdgeRoute> Edges, double Width, double Height )
{
	private Dictionary<int, NodeBox>? _byId;

	internal GraphLayoutResult(
		IReadOnlyList<NodeBox> nodes,
		IReadOnlyList<EdgeRoute> edges,
		double width,
		double height,
		Dictionary<int, NodeBox> byId )
		: this( nodes, edges, width, height ) => _byId = byId;

	public static GraphLayoutResult Empty { get; } = new( [], [], 0, 0 );

	/// <summary>The box of file <paramref name="id"/>; null when it is not in the graph.</summary>
	public NodeBox? Find( int id )
	{
		if( Nodes.Count == 0 )
			return null;
		if( _byId is null )
		{
			Dictionary<int, NodeBox> map = new( Nodes.Count );
			for( int i = 0; i < Nodes.Count; i++ )
				map.TryAdd( Nodes[ i ].Node.Id, Nodes[ i ] );
			_byId = map;
		}
		return _byId.GetValueOrDefault( id );
	}

	public bool Equals( GraphLayoutResult? other )
		=> other is not null
			&& ReferenceEquals( Nodes, other.Nodes )
			&& ReferenceEquals( Edges, other.Edges )
			&& Width.Equals( other.Width )
			&& Height.Equals( other.Height );

	public override int GetHashCode() => HashCode.Combine( Nodes, Edges, Width, Height );
}

/// <summary>
/// Lays a dependency graph out in columns using a Sugiyama-style pipeline:
/// <list type="number">
/// <item>Deterministic DFS cycle breaking and longest-path layering (with dependent multi-root relaxation).</item>
/// <item>Weakly connected component separation with a shared global column X grid.</item>
/// <item>Dummy-node insertion for long-span edges so multi-column links route through clean corridors.</item>
/// <item>DFS initial ordering + weighted median/barycenter + adjacent transpose crossing minimization.</item>
/// <item>Variable-height 4-direction Brandes–Köpf vertical alignment with DAG block compaction and hub centering.</item>
/// <item>Waypoint routing for multi-layer forward links and cycle back-edges.</item>
/// </list>
/// </summary>
public static class GraphLayout
{
	private const int MaxDummyNodes = 10_000;
	private const int MaxCrossingSweeps = 24;
	private const int MaxNoImproveSweeps = 4;

	/// <summary>How big a file's box is, from 0 (10 KB or less) to 1 (100 MB or more), on a log scale.</summary>
	public static double SizeFactor( long bytes ) => Math.Clamp( ( Math.Log10( Math.Max( bytes, 1 ) ) - 4 ) / 4, 0, 1 );

	/// <summary>The box size for a file of <paramref name="bytes"/>.</summary>
	public static (double Width, double Height) BoxSize( long bytes, LayoutOptions? options = null )
	{
		options ??= LayoutOptions.Default;
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
		options ??= LayoutOptions.Default;
		Dictionary<int, GraphNode> nodes = graph.Nodes.DistinctBy( n => n.Id ).ToDictionary( n => n.Id );
		if( nodes.Count == 0 )
			return GraphLayoutResult.Empty;

		List<GraphEdge> edges =
		[
			.. graph.Edges
				.Where( e => e.Parent != e.Child && nodes.ContainsKey( e.Parent ) && nodes.ContainsKey( e.Child ) )
				.Distinct()
				.OrderBy( e => e.Parent )
				.ThenBy( e => e.Child ),
		];
		ILookup<int, int> childrenOf = edges.ToLookup( e => e.Parent, e => e.Child );
		ILookup<int, int> parentsOf = edges.ToLookup( e => e.Child, e => e.Parent );
		List<int> roots = [ .. graph.Roots.Where( nodes.ContainsKey ).Distinct() ];
		HashSet<int> rootSet = [ .. roots ];
		Dictionary<int, int> rootOrder = roots.Select( ( id, idx ) => (id, idx) ).ToDictionary( p => p.id, p => p.idx );

		// Stage 1: Cycle-safe longest-path layer assignment
		Dictionary<int, int> layer = AssignLayers( nodes, roots, rootSet, childrenOf, parentsOf, direction );

		// Global column X grid shared across all components so Layer k aligns vertically
		var sizes = nodes.ToDictionary( p => p.Key, p => BoxSize( p.Value.Size, options ) );
		List<int> distinctLayers = [ .. layer.Values.Distinct().Order() ];
		Dictionary<int, int> layerToCol = distinctLayers.Select( ( l, idx ) => (l, idx) ).ToDictionary( p => p.l, p => p.idx );
		int globalColCount = distinctLayers.Count;

		double[] colWidth = new double[ globalColCount ];
		foreach( var (id, l) in layer )
		{
			int c = layerToCol[ l ];
			double w = sizes[ id ].Width;
			if( w > colWidth[ c ] )
				colWidth[ c ] = w;
		}
		double[] colLeft = new double[ globalColCount ];
		double[] colRight = new double[ globalColCount ];
		double curX = options.Margin;
		for( int c = 0; c < globalColCount; c++ )
		{
			colLeft[ c ] = curX;
			colRight[ c ] = curX + colWidth[ c ];
			curX += colWidth[ c ] + options.ColumnGap;
		}
		double baseWidth = curX - options.ColumnGap + options.Margin;

		// Stage 2: Weakly connected components (multi-node components first, singletons grouped cleanly)
		List<List<int>> components = PartitionComponents( nodes, edges, rootOrder );

		// Pre-bucket edges by component in O(V + E) instead of O(K * E)
		Dictionary<int, int> nodeToComp = new( nodes.Count );
		for( int compIndex = 0; compIndex < components.Count; compIndex++ )
		{
			List<int> comp = components[ compIndex ];
			for( int i = 0; i < comp.Count; i++ )
				nodeToComp[ comp[ i ] ] = compIndex;
		}
		List<GraphEdge>?[] compEdgeBuckets = new List<GraphEdge>?[ components.Count ];
		for( int i = 0; i < edges.Count; i++ )
		{
			GraphEdge e = edges[ i ];
			int ci = nodeToComp[ e.Parent ];
			( compEdgeBuckets[ ci ] ??= [] ).Add( e );
		}

		List<NodeBox> allBoxes = new( nodes.Count );
		Dictionary<int, NodeBox> boxesById = new( nodes.Count );
		List<EdgeRoute> allRoutes = new( edges.Count );

		double cursorY = options.Margin;
		double maxX = baseWidth;
		double maxY = options.Margin;
		double componentGap = Math.Max( options.RowGap * 2.0, 48.0 );
		bool prevWasSingleton = false;

		for( int compIndex = 0; compIndex < components.Count; compIndex++ )
		{
			List<int> compNodes = components[ compIndex ];
			bool isSingleton = compNodes.Count == 1;
			if( compIndex > 0 )
				cursorY += ( isSingleton && prevWasSingleton ) ? options.RowGap : componentGap;
			prevWasSingleton = isSingleton;

			if( isSingleton )
			{
				int id = compNodes[ 0 ];
				int c = layerToCol[ layer[ id ] ];
				var (w, h) = sizes[ id ];
				double x = colLeft[ c ] + ( colWidth[ c ] - w ) / 2.0;
				NodeBox box = new( nodes[ id ], layer[ id ], x, cursorY, w, h, rootSet.Contains( id ) );
				allBoxes.Add( box );
				boxesById[ id ] = box;
				cursorY += h;
				if( box.X + box.Width + options.Margin > maxX ) maxX = box.X + box.Width + options.Margin;
				if( box.Y + box.Height + options.Margin > maxY ) maxY = box.Y + box.Height + options.Margin;
				continue;
			}

			List<GraphEdge> compEdges = compEdgeBuckets[ compIndex ] ?? [];

			// Stages 3-6 for this component
			var (compBoxes, compRoutes, compHeight, compMaxX, compMaxY) = LayoutComponent(
				compNodes, compEdges, nodes, sizes, layer, layerToCol, colLeft, colRight, colWidth,
				rootSet, rootOrder, direction, options, cursorY );

			for( int i = 0; i < compBoxes.Count; i++ )
			{
				NodeBox box = compBoxes[ i ];
				allBoxes.Add( box );
				boxesById[ box.Node.Id ] = box;
			}
			allRoutes.AddRange( compRoutes );

			cursorY += compHeight;
			if( compMaxX + options.Margin > maxX ) maxX = compMaxX + options.Margin;
			if( compMaxY + options.Margin > maxY ) maxY = compMaxY + options.Margin;
		}

		double finalHeight = Math.Max( cursorY + options.Margin, maxY );
		return new GraphLayoutResult( allBoxes, allRoutes, maxX, finalHeight, boxesById );
	}

	// ================================================================== Stage 1: Layer Assignment

	private static Dictionary<int, int> AssignLayers(
		Dictionary<int, GraphNode> nodes,
		List<int> roots,
		HashSet<int> rootSet,
		ILookup<int, int> childrenOf,
		ILookup<int, int> parentsOf,
		GraphDirection direction )
	{
		Dictionary<int, int> layer = roots.ToDictionary( r => r, _ => 0 );
		HashSet<int> down = [];

		if( direction != GraphDirection.Parents )
		{
			down = Reach( roots, childrenOf );
			HashSet<int> pinnedDownRoots = FindIndependentRoots( roots, down, parentsOf );
			AssignDirectionLayers( down, pinnedDownRoots, roots, childrenOf, layer, step: +1 );
		}

		if( direction != GraphDirection.Children )
		{
			HashSet<int> up = Reach( roots, parentsOf );
			// In Both mode, keep nodes strictly placed on the downstream side out of upstream layering
			HashSet<int> downNonRoots = [ .. down.Where( id => !rootSet.Contains( id ) ) ];
			up.ExceptWith( downNonRoots );
			// Also keep roots that were pushed downstream (layer > 0) out of upstream re-pinning
			HashSet<int> pushedRoots = [ .. roots.Where( r => layer.TryGetValue( r, out int l ) && l > 0 ) ];
			up.ExceptWith( pushedRoots );

			HashSet<int> pinnedUpRoots = FindIndependentRoots(
				[ .. roots.Where( up.Contains ) ], up, childrenOf );
			AssignDirectionLayers( up, pinnedUpRoots, [ .. roots.Where( up.Contains ) ], parentsOf, layer, step: -1 );
		}

		// Anything not linked to a selected file joins layer 0
		foreach( int id in nodes.Keys.Order() )
			layer.TryAdd( id, 0 );

		return layer;
	}

	private static HashSet<int> Reach( List<int> roots, ILookup<int, int> next )
	{
		HashSet<int> seen = [ .. roots ];
		Queue<int> queue = new( roots );
		while( queue.TryDequeue( out int id ) )
			foreach( int n in next[ id ] )
				if( seen.Add( n ) )
					queue.Enqueue( n );
		return seen;
	}

	/// <summary>
	/// Roots that have no incoming edge from another node in <paramref name="subset"/> stay anchored at layer 0;
	/// dependent roots (e.g. when selecting an assembly and its parts via "Select all") are allowed to relax
	/// into downstream/upstream columns. If all roots form a cycle, anchor the earliest root(s).
	/// </summary>
	private static HashSet<int> FindIndependentRoots( List<int> roots, HashSet<int> subset, ILookup<int, int> incoming )
	{
		HashSet<int> independent = [];
		foreach( int r in roots )
		{
			if( !incoming[ r ].Any( p => subset.Contains( p ) && p != r ) )
				independent.Add( r );
		}
		if( independent.Count == 0 && roots.Count > 0 )
			independent.Add( roots[ 0 ] );
		return independent;
	}

	/// <summary>
	/// Deterministic DFS cycle breaking followed by Kahn topological longest-path layering in O(V + E).
	/// </summary>
	private static void AssignDirectionLayers(
		HashSet<int> subset,
		HashSet<int> pinnedRoots,
		List<int> rootOrder,
		ILookup<int, int> forward,
		Dictionary<int, int> layer,
		int step )
	{
		if( subset.Count == 0 )
			return;

		// Build deterministic DFS start order: pinned roots first, then remaining roots, then remaining subset
		List<int> starts = [];
		HashSet<int> added = [];
		foreach( int r in rootOrder )
			if( pinnedRoots.Contains( r ) && added.Add( r ) )
				starts.Add( r );
		foreach( int r in rootOrder )
			if( subset.Contains( r ) && added.Add( r ) )
				starts.Add( r );
		foreach( int id in subset.Order() )
			if( added.Add( id ) )
				starts.Add( id );

		// Iterative DFS to mark back-edges (breaks all cycles cleanly without stack overflow on deep chains)
		Dictionary<int, int> state = new( subset.Count ); // 0 = unvisited, 1 = on stack, 2 = done
		HashSet<(int From, int To)> backEdges = [];

		foreach( int start in starts )
		{
			if( state.GetValueOrDefault( start ) != 0 )
				continue;

			Stack<(int Node, List<int> Neighbors, int Index)> stack = new();
			state[ start ] = 1;
			stack.Push( (start, [ .. forward[ start ].Where( subset.Contains ).Order() ], 0) );

			while( stack.Count > 0 )
			{
				var (u, neighbors, idx) = stack.Pop();
				if( idx < neighbors.Count )
				{
					stack.Push( (u, neighbors, idx + 1) );
					int v = neighbors[ idx ];
					int vState = state.GetValueOrDefault( v );
					if( vState == 1 )
					{
						backEdges.Add( (u, v) );
					}
					else if( vState == 0 )
					{
						state[ v ] = 1;
						stack.Push( (v, [ .. forward[ v ].Where( subset.Contains ).Order() ], 0) );
					}
				}
				else
				{
					state[ u ] = 2;
				}
			}
		}

		// Build DAG adjacency and in-degrees (excluding back-edges and edges targeting pinned roots)
		Dictionary<int, List<int>> dag = new( subset.Count );
		Dictionary<int, int> inDeg = new( subset.Count );
		foreach( int u in subset )
		{
			dag[ u ] = [];
			inDeg[ u ] = 0;
		}
		foreach( int u in subset )
		{
			foreach( int v in forward[ u ] )
			{
				if( !subset.Contains( v ) || pinnedRoots.Contains( v ) || backEdges.Contains( (u, v) ) )
					continue;
				dag[ u ].Add( v );
				inDeg[ v ]++;
			}
		}

		// Initialize anchored roots to 0 and run Kahn topological longest-path
		Queue<int> queue = new();
		foreach( int u in starts )
		{
			if( inDeg[ u ] == 0 )
			{
				layer[ u ] = 0;
				queue.Enqueue( u );
			}
		}

		while( queue.TryDequeue( out int u ) )
		{
			int at = layer.GetValueOrDefault( u, 0 );
			foreach( int v in dag[ u ] )
			{
				int want = at + step;
				if( !layer.TryGetValue( v, out int current ) || ( step > 0 ? want > current : want < current ) )
					layer[ v ] = want;
				if( --inDeg[ v ] == 0 )
					queue.Enqueue( v );
			}
		}
	}

	// ================================================================== Stage 2: Components

	private static List<List<int>> PartitionComponents(
		Dictionary<int, GraphNode> nodes,
		List<GraphEdge> edges,
		Dictionary<int, int> rootOrder )
	{
		Dictionary<int, List<int>> undirected = new( nodes.Count );
		foreach( int id in nodes.Keys )
			undirected[ id ] = [];
		foreach( GraphEdge e in edges )
		{
			undirected[ e.Parent ].Add( e.Child );
			undirected[ e.Child ].Add( e.Parent );
		}

		HashSet<int> visited = new( nodes.Count );
		List<List<int>> components = [];
		foreach( int start in nodes.Keys.OrderBy( id => rootOrder.GetValueOrDefault( id, int.MaxValue ) ).ThenBy( id => id ) )
		{
			if( !visited.Add( start ) )
				continue;
			List<int> comp = [];
			Queue<int> q = new();
			q.Enqueue( start );
			while( q.TryDequeue( out int u ) )
			{
				comp.Add( u );
				foreach( int v in undirected[ u ] )
					if( visited.Add( v ) )
						q.Enqueue( v );
			}
			components.Add( comp );
		}

		// Multi-node components first (by earliest root, then size desc, then min id), then singletons
		return
		[
			.. components
				.OrderBy( c => c.Count == 1 ? 1 : 0 )
				.ThenBy( c => c.Min( id => rootOrder.GetValueOrDefault( id, int.MaxValue ) ) )
				.ThenByDescending( c => c.Count )
				.ThenBy( c => c.Min() ),
		];
	}

	// ================================================================== Stages 3-6: Per-Component Layout

	private sealed record EdgeChain( GraphEdge Edge, bool ReversedForOrdering, List<int> DummyIds, bool SameColumn );

	private static (List<NodeBox> Boxes, List<EdgeRoute> Routes, double Height, double MaxX, double MaxY) LayoutComponent(
		List<int> compNodes,
		List<GraphEdge> compEdges,
		Dictionary<int, GraphNode> allNodes,
		Dictionary<int, (double Width, double Height)> sizes,
		Dictionary<int, int> layer,
		Dictionary<int, int> globalLayerToCol,
		double[] globalColLeft,
		double[] globalColRight,
		double[] globalColWidth,
		HashSet<int> rootSet,
		Dictionary<int, int> rootOrder,
		GraphDirection direction,
		LayoutOptions o,
		double offsetY )
	{
		// Local dense column index range spanned by this component
		int minGlobalCol = compNodes.Min( id => globalLayerToCol[ layer[ id ] ] );
		int maxGlobalCol = compNodes.Max( id => globalLayerToCol[ layer[ id ] ] );
		int numCols = maxGlobalCol - minGlobalCol + 1;

		// Vertex indexing: 0..realCount-1 are real nodes; realCount..totalVerts-1 are dummy nodes
		List<int> realIds = [ .. compNodes ];
		Dictionary<int, int> nodeIdToVert = new( realIds.Count );
		List<int> vertCol = new( realIds.Count );
		List<double> vertHeight = new( realIds.Count );
		List<bool> isDummy = new( realIds.Count );

		for( int i = 0; i < realIds.Count; i++ )
		{
			int id = realIds[ i ];
			nodeIdToVert[ id ] = i;
			vertCol.Add( globalLayerToCol[ layer[ id ] ] - minGlobalCol );
			vertHeight.Add( sizes[ id ].Height );
			isDummy.Add( false );
		}

		// Stage 3: Insert dummy nodes for multi-column edges
		List<List<int>> rightAdj = new( realIds.Count );
		List<List<int>> leftAdj = new( realIds.Count );
		for( int i = 0; i < realIds.Count; i++ )
		{
			rightAdj.Add( [] );
			leftAdj.Add( [] );
		}

		List<EdgeChain> chains = new( compEdges.Count );
		int dummyBudget = MaxDummyNodes;

		int AddDummy( int col )
		{
			int v = vertCol.Count;
			vertCol.Add( col );
			vertHeight.Add( 0.0 );
			isDummy.Add( true );
			rightAdj.Add( [] );
			leftAdj.Add( [] );
			return v;
		}

		void AddUnitEdge( int leftV, int rightV )
		{
			rightAdj[ leftV ].Add( rightV );
			leftAdj[ rightV ].Add( leftV );
		}

		foreach( GraphEdge e in compEdges )
		{
			int u = nodeIdToVert[ e.Parent ];
			int v = nodeIdToVert[ e.Child ];
			int cu = vertCol[ u ];
			int cv = vertCol[ v ];

			if( cu == cv )
			{
				chains.Add( new EdgeChain( e, false, [], SameColumn: true ) );
				continue;
			}

			bool reversed = cu > cv;
			int startV = reversed ? v : u;
			int endV = reversed ? u : v;
			int startC = vertCol[ startV ];
			int endC = vertCol[ endV ];
			int span = endC - startC;

			List<int> dummies = [];
			if( span == 1 )
			{
				AddUnitEdge( startV, endV );
			}
			else if( span - 1 <= dummyBudget )
			{
				dummyBudget -= span - 1;
				int prev = startV;
				for( int c = startC + 1; c < endC; c++ )
				{
					int d = AddDummy( c );
					dummies.Add( d );
					AddUnitEdge( prev, d );
					prev = d;
				}
				AddUnitEdge( prev, endV );
			}
			chains.Add( new EdgeChain( e, reversed, dummies, SameColumn: false ) );
		}

		int vertCount = vertCol.Count;

		// Stage 4: Crossing minimization (DFS initial order + median/barycenter + transpose)
		List<int>[] columns = BuildInitialColumnOrder(
			vertCount, numCols, realIds, vertCol, isDummy, rightAdj, leftAdj, allNodes, rootOrder, direction, minGlobalCol, globalLayerToCol );

		MinimizeCrossings( columns, rightAdj, leftAdj, direction );

		// Stage 5: Variable-height 4-direction Brandes–Köpf + safe hub centering
		double[] centerY = AssignYCoordinates( columns, vertCount, vertHeight, isDummy, rightAdj, leftAdj, direction, o );

		// Shift component so its minimum top edge sits at offsetY
		double minTop = double.PositiveInfinity;
		double maxBottom = double.NegativeInfinity;
		for( int v = 0; v < vertCount; v++ )
		{
			double half = vertHeight[ v ] / 2.0;
			double top = centerY[ v ] - half;
			double bottom = centerY[ v ] + half;
			if( top < minTop ) minTop = top;
			if( bottom > maxBottom ) maxBottom = bottom;
		}
		if( double.IsInfinity( minTop ) )
			minTop = 0;

		double shiftY = offsetY - minTop;
		for( int v = 0; v < vertCount; v++ )
			centerY[ v ] += shiftY;

		double compHeight = Math.Max( 0, maxBottom - minTop );

		// Build real NodeBoxes
		List<NodeBox> boxes = new( realIds.Count );
		Dictionary<int, NodeBox> byId = new( realIds.Count );
		double maxX = 0;
		double maxY = offsetY + compHeight;

		for( int v = 0; v < realIds.Count; v++ )
		{
			int id = realIds[ v ];
			int gc = minGlobalCol + vertCol[ v ];
			var (w, h) = sizes[ id ];
			double x = globalColLeft[ gc ] + ( globalColWidth[ gc ] - w ) / 2.0;
			double y = centerY[ v ] - h / 2.0;
			NodeBox box = new( allNodes[ id ], layer[ id ], x, y, w, h, rootSet.Contains( id ) );
			boxes.Add( box );
			byId[ id ] = box;
			if( x + w > maxX ) maxX = x + w;
			if( y + h > maxY ) maxY = y + h;
		}

		// Stage 6: Build EdgeRoutes with BendPoints for multi-layer forward links and cycle back-edges
		List<EdgeRoute> routes = new( chains.Count );
		double backStub = Math.Min( 28.0, o.ColumnGap * 0.3 );

		double clearPad = Math.Max( 16.0, o.RowGap * 0.65 );
		List<(double X, double Y)> WrapAroundBends( NodeBox a, NodeBox b, double exitX, double enterX, double fy, double ty )
		{
			double clearY = Math.Min( a.Y, b.Y ) - clearPad;
			if( clearY < o.Margin * 0.5 )
				clearY = Math.Max( a.Y + a.Height, b.Y + b.Height ) + clearPad;
			return [ ( exitX, fy ), ( exitX, clearY ), ( enterX, clearY ), ( enterX, ty ) ];
		}

		foreach( EdgeChain chain in chains )
		{
			NodeBox parentBox = byId[ chain.Edge.Parent ];
			NodeBox childBox = byId[ chain.Edge.Child ];
			var (fx, fy) = parentBox.OutPort;
			var (tx, ty) = childBox.InPort;

			List<(double X, double Y)>? bends = null;
			if( chain.SameColumn )
			{
				bends = WrapAroundBends( parentBox, childBox, Math.Max( fx, tx ) + backStub, Math.Min( fx, tx ) - backStub, fy, ty );
			}
			else if( !chain.ReversedForOrdering )
			{
				if( chain.DummyIds.Count > 0 )
				{
					bends = new( chain.DummyIds.Count * 2 );
					foreach( int d in chain.DummyIds )
					{
						int gc = minGlobalCol + vertCol[ d ];
						bends.Add( ( globalColLeft[ gc ], centerY[ d ] ) );
						bends.Add( ( globalColRight[ gc ], centerY[ d ] ) );
					}
				}
			}
			else if( chain.DummyIds.Count > 0 )
			{
				bends = new( chain.DummyIds.Count * 2 + 2 ) { ( fx + backStub, centerY[ chain.DummyIds[ ^1 ] ] ) };
				for( int i = chain.DummyIds.Count - 1; i >= 0; i-- )
				{
					int d = chain.DummyIds[ i ];
					int gc = minGlobalCol + vertCol[ d ];
					bends.Add( ( globalColRight[ gc ], centerY[ d ] ) );
					bends.Add( ( globalColLeft[ gc ], centerY[ d ] ) );
				}
				bends.Add( ( tx - backStub, centerY[ chain.DummyIds[ 0 ] ] ) );
			}
			else
			{
				bends = WrapAroundBends( parentBox, childBox, fx + backStub, tx - backStub, fy, ty );
			}

			if( bends is not null )
				UpdateBounds( bends, ref maxX, ref maxY );
			routes.Add( bends is null
				? new EdgeRoute( chain.Edge, fx, fy, tx, ty )
				: new EdgeRoute( chain.Edge, fx, fy, tx, ty ) { BendPoints = bends } );
		}

		return (boxes, routes, compHeight, maxX, maxY);
	}

	private static void UpdateBounds( List<(double X, double Y)> bends, ref double maxX, ref double maxY )
	{
		foreach( var (x, y) in bends )
		{
			if( x > maxX ) maxX = x;
			if( y > maxY ) maxY = y;
		}
	}

	// ================================================================== Stage 4: Crossing Minimization

	private static List<int>[] BuildInitialColumnOrder(
		int vertCount,
		int numCols,
		List<int> realIds,
		List<int> vertCol,
		List<bool> isDummy,
		List<List<int>> rightAdj,
		List<List<int>> leftAdj,
		Dictionary<int, GraphNode> allNodes,
		Dictionary<int, int> rootOrder,
		GraphDirection direction,
		int minGlobalCol,
		Dictionary<int, int> globalLayerToCol )
	{
		string LabelOf( int v ) => isDummy[ v ] ? "" : allNodes[ realIds[ v ] ].Name;
		int SortKeyId( int v ) => isDummy[ v ] ? int.MaxValue - v : realIds[ v ];

		int CompareVerts( int a, int b )
		{
			bool da = isDummy[ a ];
			bool db = isDummy[ b ];
			if( da != db )
				return da ? 1 : -1;
			if( !da )
			{
				int ra = rootOrder.GetValueOrDefault( realIds[ a ], int.MaxValue );
				int rb = rootOrder.GetValueOrDefault( realIds[ b ], int.MaxValue );
				if( ra != rb )
					return ra.CompareTo( rb );
				int nameCmp = StringComparer.OrdinalIgnoreCase.Compare( LabelOf( a ), LabelOf( b ) );
				if( nameCmp != 0 )
					return nameCmp;
			}
			return SortKeyId( a ).CompareTo( SortKeyId( b ) );
		}

		// DFS pre-order starting from roots (gives 0 crossings immediately on trees and readable sibling order)
		List<int> startOrder = new( vertCount );
		for( int v = 0; v < vertCount; v++ )
			startOrder.Add( v );
		int zeroGlobal = 0;
		bool hasZeroLayer = direction == GraphDirection.Both && globalLayerToCol.TryGetValue( 0, out zeroGlobal );
		int zeroLocal = hasZeroLayer ? zeroGlobal - minGlobalCol : 0;
		startOrder.Sort( ( a, b ) =>
		{
			int colA = direction == GraphDirection.Parents ? -vertCol[ a ] : vertCol[ a ];
			int colB = direction == GraphDirection.Parents ? -vertCol[ b ] : vertCol[ b ];
			if( hasZeroLayer )
			{
				colA = Math.Abs( vertCol[ a ] - zeroLocal );
				colB = Math.Abs( vertCol[ b ] - zeroLocal );
			}
			int cmp = colA.CompareTo( colB );
			return cmp != 0 ? cmp : CompareVerts( a, b );
		} );

		bool[] visited = new bool[ vertCount ];
		List<int>[] columns = new List<int>[ numCols ];
		for( int c = 0; c < numCols; c++ )
			columns[ c ] = [];

		Stack<int> stack = new();
		List<int> next = [];
		for( int s = 0; s < startOrder.Count; s++ )
		{
			int start = startOrder[ s ];
			if( visited[ start ] )
				continue;
			stack.Push( start );
			while( stack.Count > 0 )
			{
				int u = stack.Pop();
				if( visited[ u ] )
					continue;
				visited[ u ] = true;
				columns[ vertCol[ u ] ].Add( u );

				next.Clear();
				if( direction == GraphDirection.Parents )
				{
					next.AddRange( leftAdj[ u ] );
					next.AddRange( rightAdj[ u ] );
				}
				else
				{
					next.AddRange( rightAdj[ u ] );
					next.AddRange( leftAdj[ u ] );
				}
				next.Sort( CompareVerts );
				for( int i = next.Count - 1; i >= 0; i-- )
				{
					if( !visited[ next[ i ] ] )
						stack.Push( next[ i ] );
				}
			}
		}

		return columns;
	}

	private static void MinimizeCrossings(
		List<int>[] columns,
		List<List<int>> rightAdj,
		List<List<int>> leftAdj,
		GraphDirection direction )
	{
		if( columns.Length <= 1 )
			return;

		int vertCount = rightAdj.Count;
		int[] pos = new int[ vertCount ];
		SyncPositions( columns, pos );

		int maxColLen = 0;
		for( int c = 0; c < columns.Length; c++ )
		{
			if( columns[ c ].Count > maxColLen )
				maxColLen = columns[ c ].Count;
		}
		int[] bitScratch = new int[ maxColLen + 1 ];
		List<int> targetsScratch = [];

		long bestCrossings = CountAllCrossings( columns, rightAdj, pos, bitScratch, targetsScratch );
		if( bestCrossings == 0 )
			return;

		List<int>[] bestOrder = CloneColumns( columns );
		bool startRightward = direction != GraphDirection.Parents;
		int staleSweeps = 0;

		List<(int Vert, double Median, double Mean, int OrigIdx)> movableScratch = new( maxColLen );
		List<int> slotsScratch = new( maxColLen );
		List<int> neighborScratch = [];

		for( int sweep = 0; sweep < MaxCrossingSweeps; sweep++ )
		{
			bool rightward = ( sweep % 2 == 0 ) == startRightward;
			if( rightward )
			{
				for( int c = 1; c < columns.Length; c++ )
					ReorderColumnByMedian( columns[ c ], leftAdj, pos, movableScratch, slotsScratch, neighborScratch );
			}
			else
			{
				for( int c = columns.Length - 2; c >= 0; c-- )
					ReorderColumnByMedian( columns[ c ], rightAdj, pos, movableScratch, slotsScratch, neighborScratch );
			}

			TransposePass( columns, rightAdj, leftAdj, pos );

			long crossings = CountAllCrossings( columns, rightAdj, pos, bitScratch, targetsScratch );
			if( crossings < bestCrossings )
			{
				bestCrossings = crossings;
				CopyColumnsInto( columns, bestOrder );
				staleSweeps = 0;
				if( bestCrossings == 0 )
					break;
			}
			else if( ++staleSweeps >= MaxNoImproveSweeps )
			{
				break;
			}
		}

		for( int c = 0; c < columns.Length; c++ )
			columns[ c ] = bestOrder[ c ];
	}

	private static void ReorderColumnByMedian(
		List<int> column,
		List<List<int>> refAdj,
		int[] pos,
		List<(int Vert, double Median, double Mean, int OrigIdx)> movable,
		List<int> movableSlots,
		List<int> neighborPositions )
	{
		int n = column.Count;
		if( n <= 1 )
			return;

		// Only reorder nodes that actually have neighbors in the reference column; unlinked nodes stay pinned in place
		movable.Clear();
		movableSlots.Clear();

		for( int i = 0; i < n; i++ )
		{
			int v = column[ i ];
			List<int> adj = refAdj[ v ];
			if( adj.Count == 0 )
				continue;

			neighborPositions.Clear();
			double sum = 0;
			for( int j = 0; j < adj.Count; j++ )
			{
				int p = pos[ adj[ j ] ];
				neighborPositions.Add( p );
				sum += p;
			}
			neighborPositions.Sort();

			double median = WeightedMedian( neighborPositions );
			double mean = sum / neighborPositions.Count;
			movable.Add( (v, median, mean, i) );
			movableSlots.Add( i );
		}

		if( movable.Count <= 1 )
			return;

		movable.Sort( static ( a, b ) =>
		{
			int cmp = a.Median.CompareTo( b.Median );
			if( cmp != 0 ) return cmp;
			cmp = a.Mean.CompareTo( b.Mean );
			if( cmp != 0 ) return cmp;
			return a.OrigIdx.CompareTo( b.OrigIdx );
		} );

		for( int k = 0; k < movable.Count; k++ )
		{
			int slot = movableSlots[ k ];
			int vert = movable[ k ].Vert;
			column[ slot ] = vert;
			pos[ vert ] = slot;
		}
	}

	/// <summary>Gansner et al. (1993) weighted median over sorted neighbor positions.</summary>
	private static double WeightedMedian( List<int> sorted )
	{
		int k = sorted.Count;
		int m = k / 2;
		if( k == 1 ) return sorted[ 0 ];
		if( k % 2 == 1 ) return sorted[ m ];
		if( k == 2 ) return ( sorted[ 0 ] + sorted[ 1 ] ) / 2.0;

		double left = sorted[ m - 1 ] - sorted[ 0 ];
		double right = sorted[ k - 1 ] - sorted[ m ];
		double denom = left + right;
		return denom <= 0
			? ( sorted[ m - 1 ] + sorted[ m ] ) / 2.0
			: ( sorted[ m - 1 ] * right + sorted[ m ] * left ) / denom;
	}

	/// <summary>Local adjacent-swap refinement checking crossings against both adjacent columns simultaneously.</summary>
	private static void TransposePass( List<int>[] columns, List<List<int>> rightAdj, List<List<int>> leftAdj, int[] pos )
	{
		bool improved = true;
		int maxRounds = 4;
		while( improved && maxRounds-- > 0 )
		{
			improved = false;
			for( int c = 0; c < columns.Length; c++ )
			{
				List<int> col = columns[ c ];
				for( int i = 0; i + 1 < col.Count; i++ )
				{
					int u = col[ i ];
					int v = col[ i + 1 ];
					( long curL, long swapL ) = PairCrossingsBoth( leftAdj[ u ], leftAdj[ v ], pos );
					( long curR, long swapR ) = PairCrossingsBoth( rightAdj[ u ], rightAdj[ v ], pos );
					if( swapL + swapR < curL + curR )
					{
						col[ i ] = v;
						col[ i + 1 ] = u;
						pos[ v ] = i;
						pos[ u ] = i + 1;
						improved = true;
					}
				}
			}
		}
	}

	private static (long Current, long Swapped) PairCrossingsBoth( List<int> upperAdj, List<int> lowerAdj, int[] pos )
	{
		if( upperAdj.Count == 0 || lowerAdj.Count == 0 )
			return ( 0, 0 );
		long cur = 0;
		long swapped = 0;
		for( int i = 0; i < upperAdj.Count; i++ )
		{
			int pa = pos[ upperAdj[ i ] ];
			for( int j = 0; j < lowerAdj.Count; j++ )
			{
				int pb = pos[ lowerAdj[ j ] ];
				if( pa > pb )
					cur++;
				else if( pa < pb )
					swapped++;
			}
		}
		return ( cur, swapped );
	}

	private static void SyncPositions( List<int>[] columns, int[] pos )
	{
		for( int c = 0; c < columns.Length; c++ )
		{
			List<int> col = columns[ c ];
			for( int i = 0; i < col.Count; i++ )
				pos[ col[ i ] ] = i;
		}
	}

	private static List<int>[] CloneColumns( List<int>[] columns )
	{
		List<int>[] copy = new List<int>[ columns.Length ];
		for( int c = 0; c < columns.Length; c++ )
			copy[ c ] = [ .. columns[ c ] ];
		return copy;
	}

	private static void CopyColumnsInto( List<int>[] source, List<int>[] target )
	{
		for( int c = 0; c < source.Length; c++ )
		{
			target[ c ].Clear();
			target[ c ].AddRange( source[ c ] );
		}
	}

	/// <summary>Exact bilayer crossing count across all column pairs via Fenwick tree (BIT) in O(E log V).</summary>
	private static long CountAllCrossings( List<int>[] columns, List<List<int>> rightAdj, int[] pos, int[] bit, List<int> targets )
	{
		long total = 0;
		for( int c = 0; c + 1 < columns.Length; c++ )
			total += CountBilayerCrossings( columns[ c ], columns[ c + 1 ].Count, rightAdj, pos, bit, targets );
		return total;
	}

	private static long CountBilayerCrossings( List<int> leftCol, int rightCount, List<List<int>> rightAdj, int[] pos, int[] bit, List<int> targets )
	{
		if( leftCol.Count <= 1 || rightCount <= 1 )
			return 0;

		Array.Clear( bit, 0, rightCount + 1 );
		long crossings = 0;
		int inserted = 0;

		for( int i = 0; i < leftCol.Count; i++ )
		{
			List<int> adj = rightAdj[ leftCol[ i ] ];
			if( adj.Count == 0 )
				continue;
			targets.Clear();
			for( int j = 0; j < adj.Count; j++ )
				targets.Add( pos[ adj[ j ] ] );
			targets.Sort();

			// Query in ascending target order so edges sharing left endpoint u don't count as crossing each other
			for( int j = 0; j < targets.Count; j++ )
			{
				int leq = 0;
				for( int idx = targets[ j ] + 1; idx > 0; idx -= idx & -idx )
					leq += bit[ idx ];
				crossings += inserted - leq;
			}
			for( int j = 0; j < targets.Count; j++ )
			{
				for( int idx = targets[ j ] + 1; idx <= rightCount; idx += idx & -idx )
					bit[ idx ]++;
				inserted++;
			}
		}

		return crossings;
	}

	// ================================================================== Stage 5: Vertical (Y) Coordinates

	private static double[] AssignYCoordinates(
		List<int>[] columns,
		int vertCount,
		List<double> vertHeight,
		List<bool> isDummy,
		List<List<int>> rightAdj,
		List<List<int>> leftAdj,
		GraphDirection direction,
		LayoutOptions o )
	{
		int[] pos = new int[ vertCount ];
		SyncPositions( columns, pos );

		// Sort adjacency lists by neighbor column position ascending (required for BK median selection)
		for( int v = 0; v < vertCount; v++ )
		{
			if( leftAdj[ v ].Count > 1 ) leftAdj[ v ].Sort( ( a, b ) => pos[ a ].CompareTo( pos[ b ] ) );
			if( rightAdj[ v ].Count > 1 ) rightAdj[ v ].Sort( ( a, b ) => pos[ a ].CompareTo( pos[ b ] ) );
		}

		double Gap( int u, int v ) => (isDummy[ u ], isDummy[ v ]) switch
		{
			(false, false ) => o.RowGap,
			(true, true ) => Math.Max( 12.0, o.RowGap * 0.5 ),
			_ => Math.Max( 16.0, o.RowGap * 0.65 ),
		};

		double Sep( int u, int v ) => vertHeight[ u ] / 2.0 + Gap( u, v ) + vertHeight[ v ] / 2.0;

		// Mark Type-1 conflicts (non-inner segment crossing a dummy-to-dummy inner segment)
		HashSet<(int Left, int Right)> type1Conflicts = MarkType1Conflicts( columns, isDummy, rightAdj, pos );

		// Compute the 4 Brandes–Köpf directional layouts
		double[][] layouts =
		[
			ComputeBkLayout( columns, vertCount, vertHeight, leftAdj, rightAdj, pos, type1Conflicts, Sep, leftToRight: true, topToBottom: true ),
			ComputeBkLayout( columns, vertCount, vertHeight, leftAdj, rightAdj, pos, type1Conflicts, Sep, leftToRight: false, topToBottom: true ),
			ComputeBkLayout( columns, vertCount, vertHeight, leftAdj, rightAdj, pos, type1Conflicts, Sep, leftToRight: true, topToBottom: false ),
			ComputeBkLayout( columns, vertCount, vertHeight, leftAdj, rightAdj, pos, type1Conflicts, Sep, leftToRight: false, topToBottom: false ),
		];

		// Normalize each layout to start at top = 0 and find the tightest (minimum-height) one
		double[] heights = new double[ 4 ];
		int smallestIdx = 0;
		for( int k = 0; k < 4; k++ )
		{
			double[] yk = layouts[ k ];
			double minT = double.PositiveInfinity;
			double maxB = double.NegativeInfinity;
			for( int v = 0; v < vertCount; v++ )
			{
				double half = vertHeight[ v ] / 2.0;
				if( yk[ v ] - half < minT ) minT = yk[ v ] - half;
				if( yk[ v ] + half > maxB ) maxB = yk[ v ] + half;
			}
			for( int v = 0; v < vertCount; v++ )
				yk[ v ] -= minT;
			heights[ k ] = maxB - minT;
			if( heights[ k ] < heights[ smallestIdx ] )
				smallestIdx = k;
		}

		// Align UL/UR (bottom-up layouts) to the bottom of the tightest layout
		double targetHeight = heights[ smallestIdx ];
		for( int k = 2; k < 4; k++ )
		{
			double shift = targetHeight - heights[ k ];
			double[] yk = layouts[ k ];
			for( int v = 0; v < vertCount; v++ )
				yk[ v ] += shift;
		}

		// Combine by average median: provably preserves y[i+1] - y[i] >= Sep(i, i+1) for variable heights
		double[] y = new double[ vertCount ];
		double[] y0 = layouts[ 0 ], y1 = layouts[ 1 ], y2 = layouts[ 2 ], y3 = layouts[ 3 ];
		for( int v = 0; v < vertCount; v++ )
		{
			// 4-element sorting network (5 comparisons) for zero-allocation median
			double a = y0[ v ], b = y1[ v ], c = y2[ v ], d = y3[ v ];
			if( a > b ) ( a, b ) = ( b, a );
			if( c > d ) ( c, d ) = ( d, c );
			if( a > c ) ( a, c ) = ( c, a );
			if( b > d ) ( b, d ) = ( d, b );
			y[ v ] = ( b + c ) / 2.0;
		}

		// 1-pass safe hub centering (fixes median bias on asymmetric 3+ child/parent fan-outs while preserving all separations)
		CenterHubsSafely( columns, y, isDummy, leftAdj, rightAdj, direction, Sep );

		return y;
	}

	private static HashSet<(int Left, int Right)> MarkType1Conflicts(
		List<int>[] columns,
		List<bool> isDummy,
		List<List<int>> rightAdj,
		int[] pos )
	{
		HashSet<(int Left, int Right)> conflicts = [];
		for( int c = 1; c + 1 < columns.Length - 1; c++ )
		{
			List<int> col = columns[ c ];
			int n = col.Count;
			if( n == 0 )
				continue;

			bool hasInner = false;
			int[] minRvAt = new int[ n ];
			int[] maxRvAt = new int[ n ];
			Array.Fill( minRvAt, int.MaxValue );
			Array.Fill( maxRvAt, -1 );

			for( int i = 0; i < n; i++ )
			{
				int u = col[ i ];
				if( !isDummy[ u ] )
					continue;
				List<int> adj = rightAdj[ u ];
				for( int j = 0; j < adj.Count; j++ )
				{
					int v = adj[ j ];
					if( !isDummy[ v ] )
						continue;
					int rv = pos[ v ];
					if( rv < minRvAt[ i ] ) minRvAt[ i ] = rv;
					if( rv > maxRvAt[ i ] ) maxRvAt[ i ] = rv;
					hasInner = true;
				}
			}
			if( !hasInner )
				continue;

			// Prefix max of inner rv for lu < pa; suffix min of inner rv for lu > pa -> O(1) conflict test per edge
			int[] prefixMaxRv = new int[ n ];
			int runningMax = -1;
			for( int i = 0; i < n; i++ )
			{
				prefixMaxRv[ i ] = runningMax;
				if( maxRvAt[ i ] > runningMax )
					runningMax = maxRvAt[ i ];
			}

			int[] suffixMinRv = new int[ n ];
			int runningMin = int.MaxValue;
			for( int i = n - 1; i >= 0; i-- )
			{
				suffixMinRv[ i ] = runningMin;
				if( minRvAt[ i ] < runningMin )
					runningMin = minRvAt[ i ];
			}

			for( int i = 0; i < n; i++ )
			{
				int a = col[ i ];
				List<int> adj = rightAdj[ a ];
				bool aDummy = isDummy[ a ];
				int pMax = prefixMaxRv[ i ];
				int sMin = suffixMinRv[ i ];
				for( int j = 0; j < adj.Count; j++ )
				{
					int b = adj[ j ];
					if( aDummy && isDummy[ b ] )
						continue;
					int pb = pos[ b ];
					if( pb > sMin || pb < pMax )
						conflicts.Add( ( a, b ) );
				}
			}
		}
		return conflicts;
	}

	private static double[] ComputeBkLayout(
		List<int>[] columns,
		int vertCount,
		List<double> vertHeight,
		List<List<int>> leftAdj,
		List<List<int>> rightAdj,
		int[] pos,
		HashSet<(int Left, int Right)> type1Conflicts,
		Func<int, int, double> sep,
		bool leftToRight,
		bool topToBottom )
	{
		int[] root = new int[ vertCount ];
		int[] align = new int[ vertCount ];
		for( int v = 0; v < vertCount; v++ )
		{
			root[ v ] = v;
			align[ v ] = v;
		}

		// Step A: Vertical alignment (forming horizontal chains / blocks)
		int colStart = leftToRight ? 1 : columns.Length - 2;
		int colEnd = leftToRight ? columns.Length : -1;
		int colStep = leftToRight ? 1 : -1;

		for( int c = colStart; c != colEnd; c += colStep )
		{
			List<int> col = columns[ c ];
			int r = topToBottom ? -1 : int.MaxValue;
			int iStart = topToBottom ? 0 : col.Count - 1;
			int iEnd = topToBottom ? col.Count : -1;
			int iStep = topToBottom ? 1 : -1;

			for( int i = iStart; i != iEnd; i += iStep )
			{
				int v = col[ i ];
				List<int> neighbors = leftToRight ? leftAdj[ v ] : rightAdj[ v ];
				int d = neighbors.Count;
				if( d == 0 )
					continue;

				int m1 = ( d - 1 ) / 2;
				int m2 = d / 2;
				int first = topToBottom ? m1 : m2;
				int second = topToBottom ? m2 : m1;

				for( int attempt = 0; attempt < 2; attempt++ )
				{
					int m = attempt == 0 ? first : second;
					if( attempt == 1 && first == second )
						break;
					int u = neighbors[ m ];
					var conflictEdge = leftToRight ? (u, v) : (v, u);
					if( align[ v ] == v && !type1Conflicts.Contains( conflictEdge ) )
					{
						bool orderOk = topToBottom ? r < pos[ u ] : r > pos[ u ];
						if( orderOk )
						{
							align[ u ] = v;
							root[ v ] = root[ u ];
							align[ v ] = root[ v ];
							r = pos[ u ];
							break;
						}
					}
				}
			}
		}

		// Step B: 2-Pass DAG block compaction using dense arrays indexed by block root (0..vertCount-1)
		bool[] isBlock = new bool[ vertCount ];
		int blockCount = 0;
		for( int v = 0; v < vertCount; v++ )
		{
			int rv = root[ v ];
			if( !isBlock[ rv ] )
			{
				isBlock[ rv ] = true;
				blockCount++;
			}
		}

		List<int> blocks = new( blockCount );
		for( int v = 0; v < vertCount; v++ )
		{
			if( isBlock[ v ] )
				blocks.Add( v );
		}

		List<(int To, double Weight)>?[] blockEdges = new List<(int To, double Weight)>?[ vertCount ];
		int[] inDeg = new int[ vertCount ];

		for( int c = 0; c < columns.Length; c++ )
		{
			List<int> col = columns[ c ];
			for( int i = 0; i + 1 < col.Count; i++ )
			{
				int a = col[ i ];
				int b = col[ i + 1 ];
				int ba = root[ a ];
				int bb = root[ b ];
				if( ba == bb )
					continue;
				double s = sep( a, b );
				int from = topToBottom ? ba : bb;
				int to = topToBottom ? bb : ba;

				List<(int To, double Weight)>? outs = blockEdges[ from ];
				if( outs is null )
				{
					blockEdges[ from ] = [ ( to, s ) ];
					inDeg[ to ]++;
				}
				else
				{
					bool found = false;
					for( int eIdx = 0; eIdx < outs.Count; eIdx++ )
					{
						if( outs[ eIdx ].To == to )
						{
							if( s > outs[ eIdx ].Weight )
								outs[ eIdx ] = ( to, s );
							found = true;
							break;
						}
					}
					if( !found )
					{
						outs.Add( ( to, s ) );
						inDeg[ to ]++;
					}
				}
			}
		}

		// Solve longest-path on the block DAG in the requested vertical direction, then tighten hanging roots/sinks
		List<int> topo = TopoSortDense( blocks, blockEdges, inDeg, vertCount );
		double[] blockY = new double[ vertCount ];

		for( int i = 0; i < topo.Count; i++ )
		{
			int a = topo[ i ];
			List<(int To, double Weight)>? outs = blockEdges[ a ];
			if( outs is null )
				continue;
			double ya = blockY[ a ];
			for( int j = 0; j < outs.Count; j++ )
			{
				var (b, w) = outs[ j ];
				if( ya + w > blockY[ b ] )
					blockY[ b ] = ya + w;
			}
		}
		for( int i = topo.Count - 1; i >= 0; i-- )
		{
			int a = topo[ i ];
			List<(int To, double Weight)>? outs = blockEdges[ a ];
			if( outs is null || outs.Count == 0 )
				continue;
			double limit = double.PositiveInfinity;
			for( int j = 0; j < outs.Count; j++ )
			{
				var (b, w) = outs[ j ];
				if( blockY[ b ] - w < limit )
					limit = blockY[ b ] - w;
			}
			if( limit > blockY[ a ] )
				blockY[ a ] = limit;
		}
		if( !topToBottom )
		{
			for( int i = 0; i < blocks.Count; i++ )
				blockY[ blocks[ i ] ] = -blockY[ blocks[ i ] ];
		}

		double[] y = new double[ vertCount ];
		for( int v = 0; v < vertCount; v++ )
			y[ v ] = blockY[ root[ v ] ];
		return y;
	}

	private static List<int> TopoSortDense(
		List<int> sortedBlocks,
		List<(int To, double Weight)>?[] adj,
		int[] inDeg,
		int vertCount )
	{
		Queue<int> q = new();
		for( int i = 0; i < sortedBlocks.Count; i++ )
		{
			int u = sortedBlocks[ i ];
			if( inDeg[ u ] == 0 )
				q.Enqueue( u );
		}

		List<int> order = new( sortedBlocks.Count );
		bool[] placed = new bool[ vertCount ];
		while( q.TryDequeue( out int u ) )
		{
			order.Add( u );
			placed[ u ] = true;
			List<(int To, double Weight)>? outs = adj[ u ];
			if( outs is null )
				continue;
			for( int j = 0; j < outs.Count; j++ )
			{
				int v = outs[ j ].To;
				if( --inDeg[ v ] == 0 )
					q.Enqueue( v );
			}
		}

		// Safety fallback if any cycle ever appeared
		if( order.Count < sortedBlocks.Count )
		{
			for( int i = 0; i < sortedBlocks.Count; i++ )
			{
				int u = sortedBlocks[ i ];
				if( !placed[ u ] )
					order.Add( u );
			}
		}
		return order;
	}

	/// <summary>
	/// Sweeps inward toward the roots and centers multi-branch real hubs between their extremal neighbors,
	/// strictly clamped inside neighbor separation bounds so zero overlaps are guaranteed.
	/// </summary>
	private static void CenterHubsSafely(
		List<int>[] columns,
		double[] y,
		List<bool> isDummy,
		List<List<int>> leftAdj,
		List<List<int>> rightAdj,
		GraphDirection direction,
		Func<int, int, double> sep )
	{
		if( columns.Length <= 1 )
			return;

		void CenterColumn( int c, bool useRight, bool useLeft )
		{
			List<int> col = columns[ c ];
			for( int i = 0; i < col.Count; i++ )
			{
				int v = col[ i ];
				if( isDummy[ v ] )
					continue;

				double? rightMid = useRight && rightAdj[ v ].Count > 0 ? ExtremalMidpoint( rightAdj[ v ], y ) : null;
				double? leftMid = useLeft && leftAdj[ v ].Count > 0 ? ExtremalMidpoint( leftAdj[ v ], y ) : null;

				double target;
				if( rightMid is { } rm && leftMid is { } lm )
					target = ( rm + lm ) / 2.0;
				else if( rightMid is { } rOnly )
					target = rOnly;
				else if( leftMid is { } lOnly )
					target = lOnly;
				else
					continue;

				double lo = i > 0 ? y[ col[ i - 1 ] ] + sep( col[ i - 1 ], v ) : double.NegativeInfinity;
				double hi = i + 1 < col.Count ? y[ col[ i + 1 ] ] - sep( v, col[ i + 1 ] ) : double.PositiveInfinity;
				if( lo <= hi )
					y[ v ] = Math.Clamp( target, lo, hi );
			}
		}

		if( direction == GraphDirection.Parents )
		{
			for( int c = 1; c < columns.Length; c++ )
				CenterColumn( c, useRight: false, useLeft: true );
		}
		else if( direction == GraphDirection.Children )
		{
			for( int c = columns.Length - 2; c >= 0; c-- )
				CenterColumn( c, useRight: true, useLeft: false );
		}
		else
		{
			// Both: sweep inward from right and left toward middle
			int mid = columns.Length / 2;
			for( int c = columns.Length - 2; c >= mid; c-- )
				CenterColumn( c, useRight: true, useLeft: c == mid );
			for( int c = 1; c <= mid; c++ )
				CenterColumn( c, useRight: c == mid, useLeft: true );
		}
	}

	private static double ExtremalMidpoint( List<int> neighbors, double[] y )
	{
		double minY = double.PositiveInfinity;
		double maxY = double.NegativeInfinity;
		foreach( int n in neighbors )
		{
			if( y[ n ] < minY ) minY = y[ n ];
			if( y[ n ] > maxY ) maxY = y[ n ];
		}
		return ( minY + maxY ) / 2.0;
	}
}
