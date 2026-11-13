namespace HackPDM.Core.Graph;

/// <summary>Which dependencies a graph shows, from the selected files.</summary>
public enum GraphDirection
{
	/// <summary>The files the selected files use; the selected files are on the left.</summary>
	Children,
	/// <summary>The files that use the selected files; the selected files are on the right.</summary>
	Parents,
	/// <summary>Both; the selected files are in the middle.</summary>
	Both,
}

/// <summary>One file in a dependency graph.</summary>
/// <param name="Id">The hp.entry id.</param>
/// <param name="Name">The file name, with its extension.</param>
/// <param name="Ext">The extension, lower case, without the dot.</param>
/// <param name="Size">The latest version's size in bytes.</param>
/// <param name="Category">The file type's category name; empty when none.</param>
/// <param name="VersionId">The latest hp.version id; 0 when none.</param>
/// <param name="Deleted">Whether the file is logically deleted.</param>
/// <param name="CheckedOut">Whether someone has it checked out.</param>
/// <param name="Path">The server path ("root\a\b.sldprt").</param>
public sealed record GraphNode( int Id, string Name, string Ext, long Size, string Category, int VersionId, bool Deleted, bool CheckedOut, string Path )
{
	/// <summary>Who has it checked out; empty when nobody.</summary>
	public string CheckoutUser { get; init; } = "";
	/// <summary>The file type's description ("SolidWorks part"); empty when unknown.</summary>
	public string TypeName { get; init; } = "";
	/// <summary>When the latest version's file was last changed, as the server writes it (UTC, "yyyy-MM-dd HH:mm:ss"); empty when unknown.</summary>
	public string Modified { get; init; } = "";
	/// <summary>How many versions the file has.</summary>
	public int Versions { get; init; }
}

/// <summary>A dependency: <paramref name="Parent"/>'s latest version uses <paramref name="Child"/>.</summary>
public readonly record struct GraphEdge( int Parent, int Child );

/// <summary>The files around the selected ones (<see cref="Roots"/>) and how they depend on each other.</summary>
/// <param name="Roots">The selected files' ids, in selection order.</param>
/// <param name="Nodes">Every file in the graph, the roots included.</param>
/// <param name="Edges">Parent-to-child dependencies between those files.</param>
/// <param name="Truncated">Whether the server stopped early (too deep or too many files).</param>
public sealed record DependencyGraph( IReadOnlyList<int> Roots, IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges, bool Truncated )
{
	/// <summary>A graph with nothing in it.</summary>
	public static DependencyGraph Empty { get; } = new( [], [], [], false );
}
