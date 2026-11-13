using System.Collections.Generic;

using HackPDM.Core.Graph;
using HackPDM.Infrastructure.Operations;

using Xunit;

namespace HackPDM.Tests.Operations;

public class DependencyGraphLoaderTests
{
	[Fact]
	public void Parse_ReadsNodesEdgesAndRoots()
	{
		var reply = new Dictionary<string, object?>
		{
			[ "roots" ] = new List<object?> { 1L },
			[ "nodes" ] = new List<object?>
			{
				new Dictionary<string, object?> { [ "id" ] = 1L, [ "name" ] = "top.sldasm", [ "ext" ] = "sldasm", [ "size" ] = 5_000_000_000L,
					[ "category" ] = false, [ "version_id" ] = 10L, [ "deleted" ] = false, [ "checked_out" ] = true, [ "path" ] = @"root\top.sldasm",
					[ "checkout_user" ] = "Ann", [ "type" ] = "SolidWorks assembly", [ "modified" ] = "2026-11-01 08:30:00", [ "versions" ] = 3L },
				new Dictionary<string, object?> { [ "id" ] = 2L, [ "name" ] = "bolt.sldprt", [ "size" ] = 12.0 },
				new Dictionary<string, object?> { [ "name" ] = "no id" },
				"not a row",
			},
			[ "edges" ] = new List<object?> { new List<object?> { 1L, 2L }, new List<object?> { 1L }, "bad" },
			[ "truncated" ] = true,
		};

		DependencyGraph graph = DependencyGraphLoader.Parse( reply );

		Assert.Equal( [ 1 ], graph.Roots );
		Assert.Equal( 2, graph.Nodes.Count );
		GraphNode top = graph.Nodes[ 0 ];
		Assert.Equal( ("top.sldasm", "sldasm", 5_000_000_000L, "", 10, true), (top.Name, top.Ext, top.Size, top.Category, top.VersionId, top.CheckedOut) );
		Assert.Equal( ("Ann", "SolidWorks assembly", "2026-11-01 08:30:00", 3), (top.CheckoutUser, top.TypeName, top.Modified, top.Versions) );
		Assert.Equal( 12, graph.Nodes[ 1 ].Size );
		Assert.Equal( ("", 0), (graph.Nodes[ 1 ].CheckoutUser, graph.Nodes[ 1 ].Versions) );
		Assert.Equal( "", graph.Nodes[ 1 ].Ext );
		Assert.Equal( [ new GraphEdge( 1, 2 ) ], graph.Edges );
		Assert.True( graph.Truncated );
	}

	[Fact]
	public void Parse_EmptyReply()
	{
		DependencyGraph graph = DependencyGraphLoader.Parse( new Dictionary<string, object?>() );
		Assert.Empty( graph.Nodes );
		Assert.Empty( graph.Edges );
		Assert.False( graph.Truncated );
	}

	[Fact]
	public async System.Threading.Tasks.Task Load_NoFiles_IsEmptyWithoutAsking()
	{
		var result = await DependencyGraphLoader.LoadAsync( [], GraphDirection.Both );
		Assert.True( result.Ok );
		Assert.Same( DependencyGraph.Empty, result.Value );
	}
}
