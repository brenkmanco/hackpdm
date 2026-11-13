using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core.Graph;
using HackPDM.Infrastructure.Odoo;

namespace HackPDM.Infrastructure.Operations;

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
		Result<OdooRow> reply = await OdooRpc.ExecuteAsync<OdooRow>( "hp.entry", "dependency_graph",
			[ entryIds.ToList(), direction.ToString().ToLowerInvariant() ], OdooRpc.LongTimeout * 3, token );
		return reply.Ok ? Result<DependencyGraph>.Success( Parse( reply.Value! ) ) : Result<DependencyGraph>.Fail( reply.Error );
	}

	/// <summary>Builds a <see cref="DependencyGraph"/> from the server's reply; missing or malformed parts are left out.</summary>
	public static DependencyGraph Parse( IReadOnlyDictionary<string, object?> reply )
	{
		List<GraphNode> nodes = [];
		foreach( var row in reply.GetValueOrDefault( "nodes" ) as IEnumerable<object?> ?? [] )
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
		List<GraphEdge> edges = [];
		foreach( var pair in reply.GetValueOrDefault( "edges" ) as IEnumerable<object?> ?? [] )
			if( pair is IList<object?> { Count: 2 } p && OdooValues.AsInt( p[ 0 ] ) is int parent && OdooValues.AsInt( p[ 1 ] ) is int child )
				edges.Add( new GraphEdge( parent, child ) );
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
