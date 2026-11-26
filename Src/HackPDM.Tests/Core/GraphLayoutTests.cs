using System.Linq;

using HackPDM.Core.Graph;

using Xunit;

namespace HackPDM.Tests.Core;

public class GraphLayoutTests
{
	// top -> sub -> bolt, sub -> plate, top -> bolt (a part used directly and through a sub-assembly)
	private const int Top = 1, Sub = 2, Bolt = 3, Plate = 4, Other = 5;

	private static GraphNode Node( int id, long size = 1000 ) => new( id, $"f{id}.sldprt", "sldprt", size, "", id * 10, false, false, $@"root\f{id}.sldprt" );

	private static DependencyGraph Assembly( params int[] roots ) => new(
		roots,
		[ Node( Top ), Node( Sub ), Node( Bolt ), Node( Plate ), Node( Other ) ],
		[ new( Top, Sub ), new( Top, Bolt ), new( Sub, Bolt ), new( Sub, Plate ), new( Other, Plate ) ],
		false );

	private static int LayerOf( GraphLayoutResult r, int id ) => r.Find( id )!.Layer;

	[Fact]
	public void Children_RootLeft_PartAfterItsSubAssembly()
	{
		var graph = Assembly( Top ) with { Nodes = [ Node( Top ), Node( Sub ), Node( Bolt ), Node( Plate ) ], Edges = [ new( Top, Sub ), new( Top, Bolt ), new( Sub, Bolt ), new( Sub, Plate ) ] };
		var r = GraphLayout.Arrange( graph, GraphDirection.Children );

		Assert.Equal( 0, LayerOf( r, Top ) );
		Assert.Equal( 1, LayerOf( r, Sub ) );
		// used directly and through sub: one column past sub, not next to it
		Assert.Equal( 2, LayerOf( r, Bolt ) );
		Assert.Equal( 2, LayerOf( r, Plate ) );
		Assert.True( r.Find( Top )!.IsRoot );
		Assert.True( r.Nodes.Where( n => n.Node.Id != Top ).All( n => n.X > r.Find( Top )!.X ) );
	}

	[Fact]
	public void Parents_RootRight()
	{
		var graph = Assembly( Plate );
		var r = GraphLayout.Arrange( graph, GraphDirection.Parents );

		Assert.Equal( 0, LayerOf( r, Plate ) );
		Assert.Equal( -1, LayerOf( r, Sub ) );
		Assert.Equal( -1, LayerOf( r, Other ) );
		Assert.Equal( -2, LayerOf( r, Top ) );
		double rootX = r.Find( Plate )!.X;
		Assert.True( r.Nodes.Where( n => n.Node.Id is not (Plate or Bolt) ).All( n => n.X < rootX ) );
		// the children of sub (bolt) are not part of a parents view: it joins the root column, unlinked
		Assert.Equal( 0, LayerOf( r, Bolt ) );
	}

	[Fact]
	public void Both_RootInTheMiddle()
	{
		var graph = Assembly( Sub ) with { Nodes = [ Node( Top ), Node( Sub ), Node( Bolt ), Node( Plate ) ], Edges = [ new( Top, Sub ), new( Sub, Bolt ), new( Sub, Plate ) ] };
		var r = GraphLayout.Arrange( graph, GraphDirection.Both );

		Assert.Equal( -1, LayerOf( r, Top ) );
		Assert.Equal( 0, LayerOf( r, Sub ) );
		Assert.Equal( 1, LayerOf( r, Bolt ) );
		Assert.True( r.Find( Top )!.X < r.Find( Sub )!.X && r.Find( Sub )!.X < r.Find( Bolt )!.X );
	}

	[Fact]
	public void SeveralRoots_ShareTheFirstColumn()
	{
		var r = GraphLayout.Arrange( Assembly( Top, Other ), GraphDirection.Children );
		Assert.Equal( 0, LayerOf( r, Top ) );
		Assert.Equal( 0, LayerOf( r, Other ) );
		Assert.Equal( r.Find( Top )!.X + r.Find( Top )!.Width / 2, r.Find( Other )!.X + r.Find( Other )!.Width / 2, 3 );
	}

	[Fact]
	public void Links_RunFromParentOutputToChildInput()
	{
		var r = GraphLayout.Arrange( Assembly( Top ), GraphDirection.Children );
		foreach( EdgeRoute e in r.Edges )
		{
			Assert.Equal( r.Find( e.Edge.Parent )!.OutPort, (e.FromX, e.FromY) );
			Assert.Equal( r.Find( e.Edge.Child )!.InPort, (e.ToX, e.ToY) );
			Assert.True( e.FromX < e.ToX );
		}
	}

	[Fact]
	public void Boxes_GrowWithFileSize_AndDoNotOverlap()
	{
		Assert.Equal( 0, GraphLayout.SizeFactor( 0 ) );
		Assert.Equal( 0, GraphLayout.SizeFactor( 10_000 ) );
		Assert.Equal( 0.5, GraphLayout.SizeFactor( 1_000_000 ), 3 );
		Assert.Equal( 1, GraphLayout.SizeFactor( 100_000_000 ) );
		Assert.Equal( 1, GraphLayout.SizeFactor( long.MaxValue ) );

		var graph = new DependencyGraph( [ 1 ], [ Node( 1 ), Node( 2, 50_000_000 ), Node( 3, 5 ), Node( 4, 200_000 ) ],
			[ new( 1, 2 ), new( 1, 3 ), new( 1, 4 ) ], false );
		var r = GraphLayout.Arrange( graph, GraphDirection.Children );
		Assert.True( r.Find( 2 )!.Width > r.Find( 4 )!.Width && r.Find( 4 )!.Width > r.Find( 3 )!.Width );
		Assert.True( r.Find( 2 )!.Height > r.Find( 3 )!.Height );
		foreach( var a in r.Nodes )
			foreach( var b in r.Nodes.Where( b => b != a ) )
				Assert.False( a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height,
					$"{a.Node.Id} overlaps {b.Node.Id}" );
		Assert.True( r.Nodes.All( n => n.X + n.Width <= r.Width && n.Y + n.Height <= r.Height ) );
	}

	[Fact]
	public void Cycles_AndStrayEdges_DoNotHang()
	{
		var graph = new DependencyGraph( [ 1 ], [ Node( 1 ), Node( 2 ), Node( 3 ) ],
			[ new( 1, 2 ), new( 2, 3 ), new( 3, 2 ), new( 2, 2 ), new( 3, 99 ) ], false );
		var r = GraphLayout.Arrange( graph, GraphDirection.Children );
		Assert.Equal( 3, r.Nodes.Count );
		Assert.Equal( 3, r.Edges.Count );
	}

	[Fact]
	public void Empty_GraphHasNothing()
	{
		var r = GraphLayout.Arrange( DependencyGraph.Empty, GraphDirection.Both );
		Assert.Empty( r.Nodes );
		Assert.Equal( 0, r.Width );
	}

	[Fact]
	public void SingleFile_WithoutDependencies()
	{
		var r = GraphLayout.Arrange( new DependencyGraph( [ 7 ], [ Node( 7 ) ], [], false ), GraphDirection.Children );
		Assert.Single( r.Nodes );
		Assert.True( r.Nodes[ 0 ].IsRoot );
	}

	[Fact]
	public void DependentMultiRoots_RelaxIntoProperDagLayers()
	{
		// Simulates "Select all" on a folder containing an assembly, sub-assembly, and parts
		var graph = new DependencyGraph(
			[ Top, Sub, Bolt, Plate ],
			[ Node( Top ), Node( Sub ), Node( Bolt ), Node( Plate ) ],
			[ new( Top, Sub ), new( Top, Bolt ), new( Sub, Bolt ), new( Sub, Plate ) ],
			false );
		var r = GraphLayout.Arrange( graph, GraphDirection.Children );

		Assert.Equal( 0, LayerOf( r, Top ) );
		Assert.Equal( 1, LayerOf( r, Sub ) );
		Assert.Equal( 2, LayerOf( r, Bolt ) );
		Assert.Equal( 2, LayerOf( r, Plate ) );
		Assert.All( r.Nodes, b => Assert.True( b.IsRoot ) );
		Assert.All( r.Edges, e => Assert.True( e.FromX < e.ToX ) );
	}

	[Fact]
	public void Cycle_StaysAtShallowDepthInsteadOfPumpingToNodeCount()
	{
		var nodes = Enumerable.Range( 1, 20 ).Select( id => Node( id ) ).ToList();
		var edges = new[] { new GraphEdge( 1, 2 ), new GraphEdge( 2, 3 ), new GraphEdge( 3, 2 ) }
			.Concat( Enumerable.Range( 4, 17 ).Select( id => new GraphEdge( 1, id ) ) )
			.ToList();
		var r = GraphLayout.Arrange( new DependencyGraph( [ 1 ], nodes, edges, false ), GraphDirection.Children );

		Assert.Equal( 0, LayerOf( r, 1 ) );
		Assert.Equal( 1, LayerOf( r, 2 ) );
		Assert.Equal( 2, LayerOf( r, 3 ) );
	}

	[Fact]
	public void LongEdge_RoutesAroundIntermediateBoxes()
	{
		var graph = new DependencyGraph(
			[ Top ],
			[ Node( Top ), Node( Sub ), Node( Bolt ), Node( Plate ) ],
			[ new( Top, Sub ), new( Top, Bolt ), new( Sub, Bolt ), new( Sub, Plate ) ],
			false );
		var r = GraphLayout.Arrange( graph, GraphDirection.Children );

		EdgeRoute longRoute = r.Edges.Single( e => e.Edge.Parent == Top && e.Edge.Child == Bolt );
		NodeBox subBox = r.Find( Sub )!;
		Assert.NotEmpty( longRoute.BendPoints );
		Assert.All( longRoute.BendPoints, bp =>
			Assert.True( bp.Y < subBox.Y || bp.Y > subBox.Y + subBox.Height,
				$"Long edge bend point Y={bp.Y} intersects Sub box [{subBox.Y}, {subBox.Y + subBox.Height}]" ) );
	}

	[Fact]
	public void AsymmetricSubtrees_AlignParentsWithOwnChildrenAndSeparateComponents()
	{
		// Component 1: root(1) -> bigSub(2) (8 parts 10..17) and smallSub(3) (1 part 20)
		// Component 2: root(100) -> child(101)
		var nodes = new[] { Node( 1 ), Node( 2 ), Node( 3 ), Node( 20 ), Node( 100 ), Node( 101 ) }
			.Concat( Enumerable.Range( 10, 8 ).Select( id => Node( id ) ) )
			.ToList();
		var edges = new[] { new GraphEdge( 1, 2 ), new GraphEdge( 1, 3 ), new GraphEdge( 3, 20 ), new GraphEdge( 100, 101 ) }
			.Concat( Enumerable.Range( 10, 8 ).Select( id => new GraphEdge( 2, id ) ) )
			.ToList();
		var r = GraphLayout.Arrange( new DependencyGraph( [ 1, 100 ], nodes, edges, false ), GraphDirection.Children );

		// 1->1 chain in component 2 stays horizontally aligned
		NodeBox c2Root = r.Find( 100 )!;
		NodeBox c2Child = r.Find( 101 )!;
		Assert.Equal( c2Root.Y + c2Root.Height / 2, c2Child.Y + c2Child.Height / 2, 3 );

		// SmallSub aligns with its single child (20) rather than jamming against BigSub's top
		NodeBox smallSub = r.Find( 3 )!;
		NodeBox smallPart = r.Find( 20 )!;
		Assert.Equal( smallSub.Y + smallSub.Height / 2, smallPart.Y + smallPart.Height / 2, 3 );

		// Components do not interleave vertically, while their layer-0 roots share X center
		double comp1MaxY = r.Nodes.Where( n => n.Node.Id < 100 ).Max( n => n.Y + n.Height );
		double comp2MinY = r.Nodes.Where( n => n.Node.Id >= 100 ).Min( n => n.Y );
		Assert.True( comp1MaxY < comp2MinY, $"Components interleaved: comp1MaxY={comp1MaxY}, comp2MinY={comp2MinY}" );
		Assert.Equal( r.Find( 1 )!.X + r.Find( 1 )!.Width / 2, c2Root.X + c2Root.Width / 2, 3 );
	}

	[Fact]
	public void Deterministic_AndNoOverlapsOnLargerDag()
	{
		// 120-node multi-layer CAD-like DAG with shared parts and varied sizes
		var nodes = Enumerable.Range( 1, 120 ).Select( id => Node( id, ( id % 7 + 1 ) * 500_000L ) ).ToList();
		var edges = new System.Collections.Generic.List<GraphEdge>();
		for( int i = 2; i <= 15; i++ ) edges.Add( new( 1, i ) );
		for( int i = 16; i <= 60; i++ )
		{
			edges.Add( new( 2 + ( i % 14 ), i ) );
			if( i % 5 == 0 ) edges.Add( new( 1, i ) ); // skip-layer link
		}
		for( int i = 61; i <= 120; i++ )
		{
			edges.Add( new( 16 + ( i % 45 ), i ) );
			if( i % 6 == 0 ) edges.Add( new( 2 + ( i % 14 ), i ) ); // skip-layer link
		}

		var graph = new DependencyGraph( [ 1 ], nodes, edges, false );
		var r1 = GraphLayout.Arrange( graph, GraphDirection.Children );
		var r2 = GraphLayout.Arrange( graph, GraphDirection.Children );

		Assert.Equal( r1.Nodes.Count, r2.Nodes.Count );
		for( int i = 0; i < r1.Nodes.Count; i++ )
			Assert.Equal( r1.Nodes[ i ], r2.Nodes[ i ] );

		for( int i = 0; i < r1.Nodes.Count; i++ )
		{
			var a = r1.Nodes[ i ];
			Assert.True( a.X >= 0 && a.Y >= 0 && a.X + a.Width <= r1.Width && a.Y + a.Height <= r1.Height );
			for( int j = i + 1; j < r1.Nodes.Count; j++ )
			{
				var b = r1.Nodes[ j ];
				Assert.False( a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height,
					$"{a.Node.Id} overlaps {b.Node.Id}" );
			}
		}
	}

	[Fact]
	public void BackEdge_InMixedWidthColumn_ClearsWidestBoxAndRespectsSmallMargin()
	{
		// Node 1 (tiny) shares column 0 with Node 4 (huge), and has a same-column cycle with Node 4
		var graph = new DependencyGraph(
			[ 1, 4 ],
			[ Node( 1, 100 ), Node( 2, 100 ), Node( 4, 100_000_000 ) ],
			[ new( 1, 2 ), new( 1, 4 ), new( 4, 1 ) ],
			false );
		var opts = new LayoutOptions { Margin = 5, ColumnGap = 120 };
		var r = GraphLayout.Arrange( graph, GraphDirection.Children, opts );

		NodeBox tiny = r.Find( 1 )!;
		NodeBox huge = r.Find( 4 )!;
		double colLeft = Math.Min( tiny.X, huge.X );
		double colRight = Math.Max( tiny.X + tiny.Width, huge.X + huge.Width );

		EdgeRoute back = r.Edges.Single( e => e.Edge.Parent == 4 && e.Edge.Child == 1 );
		Assert.NotEmpty( back.BendPoints );
		Assert.All( back.BendPoints, bp =>
		{
			Assert.True( bp.X >= 0 && bp.Y >= 0, $"Negative bend point ({bp.X}, {bp.Y})" );
			Assert.True( bp.X > colRight || bp.X < colLeft, $"Bend point X={bp.X} cuts into column [{colLeft}, {colRight}]" );
		} );
	}

	[Fact]
	public void ExhaustedDummyBudget_StillEmitsCorridorBendsForForwardLongEdge()
	{
		var graph = new DependencyGraph(
			[ Top ],
			[ Node( Top ), Node( Sub ), Node( Bolt ), Node( Plate ) ],
			[ new( Top, Sub ), new( Top, Bolt ), new( Sub, Bolt ), new( Sub, Plate ) ],
			false );
		var r = GraphLayout.Arrange( graph, GraphDirection.Children, LayoutOptions.Default, maxDummyNodes: 0 );

		EdgeRoute longRoute = r.Edges.Single( e => e.Edge.Parent == Top && e.Edge.Child == Bolt );
		NodeBox subBox = r.Find( Sub )!;
		Assert.NotEmpty( longRoute.BendPoints );
		Assert.All( longRoute.BendPoints, bp =>
			Assert.True( bp.Y < subBox.Y || bp.Y > subBox.Y + subBox.Height,
				$"Corridor bend Y={bp.Y} intersects Sub box [{subBox.Y}, {subBox.Y + subBox.Height}]" ) );
	}

	[Fact]
	public void Both_AsymmetricChain_CentersLayerZeroRootBetweenParentAndChildren()
	{
		// Asymmetric in Both mode: 1 parent column (-1: node 10), root at layer 0 (node 1), 3 child columns (1..3: nodes 2,3,4)
		// plus extra fanout children on node 1 so mid would previously land on layer 1 instead of layer 0.
		var graph = new DependencyGraph(
			[ 1 ],
			[ Node( 10 ), Node( 1 ), Node( 2 ), Node( 3 ), Node( 4 ), Node( 5 ), Node( 6 ) ],
			[ new( 10, 1 ), new( 1, 2 ), new( 2, 3 ), new( 3, 4 ), new( 1, 5 ), new( 1, 6 ) ],
			false );
		var r = GraphLayout.Arrange( graph, GraphDirection.Both );

		Assert.Equal( -1, LayerOf( r, 10 ) );
		Assert.Equal( 0, LayerOf( r, 1 ) );
		Assert.Equal( 3, LayerOf( r, 4 ) );
		// Verify zero overlaps and positive coordinates
		foreach( var a in r.Nodes )
		{
			Assert.True( a.X >= 0 && a.Y >= 0 );
			foreach( var b in r.Nodes.Where( b => b != a ) )
				Assert.False( a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height );
		}
	}
}
