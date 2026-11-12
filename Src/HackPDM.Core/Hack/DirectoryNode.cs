using System;
using System.Collections.Generic;
using System.Linq;

namespace HackPDM.Core.Hack;

/// <summary>
/// One folder in the PDM directory tree, independent of any UI control.
/// Remote folders carry their Odoo id; folders that only exist on disk have <see cref="DirectoryId"/> 0.
/// </summary>
public sealed class DirectoryNode( string name, int directoryId = 0 )
{
	public string Name { get; } = name;
	public int DirectoryId { get; } = directoryId;
	/// <summary>True when the folder also exists in the local PWA.</summary>
	public bool ExistsLocally { get; set; }
	public List<DirectoryNode> Children { get; } = [];

	public bool IsRemote => DirectoryId != 0;

	/// <summary>Finds a direct child folder by name.</summary>
	/// <param name="childName">The folder name; compared case-sensitively.</param>
	/// <returns>The first matching child, or <see langword="null"/>.</returns>
	public DirectoryNode? FindChild( string childName )
		=> Children.FirstOrDefault( c => string.Equals( c.Name, childName, StringComparison.Ordinal ) );

	/// <summary>Sorts children by name (case-insensitive), recursively.</summary>
	public void SortRecursive()
	{
		Children.Sort( ( a, b ) => StringComparer.OrdinalIgnoreCase.Compare( a.Name, b.Name ) );
		foreach( var child in Children )
			child.SortRecursive();
	}
}

/// <summary>Merges the folders found on disk into the remote directory tree.</summary>
public static class DirectoryTreeMerger
{
	private static readonly char[] Separators = [ '\\', '/' ];

	/// <summary>
	/// Adds every local folder (given as tree paths like <c>root\a\b</c>) that isn't already in
	/// <paramref name="root"/> as a local-only node, and marks existing remote folders as present locally.
	/// The first path segment is the root itself.
	/// </summary>
	/// <param name="root">The tree's root node; modified in place.</param>
	/// <param name="localTreePaths">The local folders as tree paths, split on both <c>\</c> and <c>/</c>.</param>
	/// <remarks>Children are appended unsorted; call <see cref="DirectoryNode.SortRecursive"/> afterwards if order matters.</remarks>
	public static void AddLocalDirectories( DirectoryNode root, IEnumerable<string> localTreePaths )
	{
		foreach( string path in localTreePaths )
		{
			DirectoryNode node = root;
			foreach( string segment in path.Split( Separators, StringSplitOptions.RemoveEmptyEntries ).Skip( 1 ) )
			{
				DirectoryNode? child = node.FindChild( segment );
				if( child is null )
				{
					child = new DirectoryNode( segment );
					node.Children.Add( child );
				}
				child.ExistsLocally = true;
				node = child;
			}
		}
	}
}
