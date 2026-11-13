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
}
