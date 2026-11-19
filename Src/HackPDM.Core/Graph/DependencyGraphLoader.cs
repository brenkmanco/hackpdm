using HackPDM.Core.Odoo;

namespace HackPDM.Core.Graph;

/// <summary>Loads dependency graphs from the server (<c>hp.entry.dependency_graph</c>).</summary>
public static class DependencyGraphLoader
{
	/// <summary>The files the selected files use, are used by, or both.</summary>
	/// <param name="entryIds">The selected files (hp.entry ids).</param>
	/// <param name="direction">Which dependencies to follow.</param>
	/// <param name="token">Cancels the call.</param>
	/// <returns>The graph (empty for no files); or the server's error.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
	public static async Task<Result<DependencyGraph>> LoadAsync( IReadOnlyList<int> entryIds, GraphDirection direction, CancellationToken token = default )
	{
		if( entryIds.Count == 0 )
			return Result<DependencyGraph>.Success( DependencyGraph.Empty );
		string dir = direction switch
		{
			GraphDirection.Children => "children",
			GraphDirection.Parents => "parents",
			_ => "both",
		};
		Result<OdooRow> reply = await OdooRpc.ExecuteAsync<OdooRow>( "hp.entry", "dependency_graph",
			[ entryIds, dir ], OdooRpc.LongTimeout * 3, token );
		return reply.Ok ? Result<DependencyGraph>.Success( Parse( reply.Value! ) ) : Result<DependencyGraph>.Fail( reply.Error );
	}

	/// <summary>Builds a <see cref="DependencyGraph"/> from the server's reply; missing or malformed parts are left out.</summary>
	public static DependencyGraph Parse( IReadOnlyDictionary<string, object?> reply )
	{
		IEnumerable<object?>? rawNodes = reply.GetValueOrDefault( "nodes" ) as IEnumerable<object?>;
		List<GraphNode> nodes = rawNodes is ICollection<object?> nodeCol ? new( nodeCol.Count ) : [];
		if( rawNodes is not null )
		{
			foreach( var row in rawNodes )
			{
				if( row is not IReadOnlyDictionary<string, object?> n || n.Int( "id" ) is not int id )
					continue;
				nodes.Add( new GraphNode( id, n.Text( "name" ) ?? "", n.Text( "ext" ) ?? "", Long( n.GetValueOrDefault( "size" ) ),
					n.Text( "category" ) ?? "", n.Int( "version_id" ) ?? 0, n.Bool( "deleted" ), n.Bool( "checked_out" ), n.Text( "path" ) ?? "" )
				{
					CheckoutUser = n.Text( "checkout_user" ) ?? "",
					TypeName = n.Text( "type" ) ?? "",
					Modified = n.Text( "modified" ) ?? "",
					Versions = n.Int( "versions" ) ?? 0,
				} );
			}
		}

		IEnumerable<object?>? rawEdges = reply.GetValueOrDefault( "edges" ) as IEnumerable<object?>;
		List<GraphEdge> edges = rawEdges is ICollection<object?> edgeCol ? new( edgeCol.Count ) : [];
		if( rawEdges is not null )
		{
			foreach( var pair in rawEdges )
				if( pair is IList<object?> { Count: 2 } p && OdooValues.AsInt( p[ 0 ] ) is int parent && OdooValues.AsInt( p[ 1 ] ) is int child )
					edges.Add( new GraphEdge( parent, child ) );
		}
		return new DependencyGraph( OdooValues.Ids( reply.GetValueOrDefault( "roots" ) ), nodes, edges, reply.GetValueOrDefault( "truncated" ) is true );
	}

	private static long Long( object? value ) => value switch
	{
		int i => i,
		long l => l,
		double d => (long)d,
		_ => 0,
	};
}
