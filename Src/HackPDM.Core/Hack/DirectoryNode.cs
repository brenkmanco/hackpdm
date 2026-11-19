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

	private static readonly StringComparison NameComparison = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	/// <summary>Finds a direct child folder by name.</summary>
	/// <param name="childName">The folder name (case-insensitive on Windows).</param>
	/// <returns>The first matching child, or <see langword="null"/>.</returns>
	public DirectoryNode? FindChild( string childName )
		=> Children.FirstOrDefault( c => string.Equals( c.Name, childName, NameComparison ) );

	/// <summary>Walks down from this root using folder segments below root; returns this node when empty.</summary>
	public DirectoryNode? FindBySegments( ReadOnlySpan<string> segmentsBelowRoot )
	{
		DirectoryNode current = this;
		foreach( string segment in segmentsBelowRoot )
		{
			if( current.FindChild( segment ) is not { } next )
				return null;
			current = next;
		}
		return current;
	}

	/// <summary>
	/// Ensures every folder along <paramref name="segmentsBelowRoot"/> exists in the tree and is marked
	/// <see cref="ExistsLocally"/>. Returns <see langword="true"/> when any node was added or changed state.
	/// </summary>
	public bool EnsureLocalPath( ReadOnlySpan<string> segmentsBelowRoot )
	{
		bool changed = false;
		DirectoryNode current = this;
		if( !current.ExistsLocally )
		{
			current.ExistsLocally = true;
			changed = true;
		}
		foreach( string segment in segmentsBelowRoot )
		{
			DirectoryNode? child = current.FindChild( segment );
			if( child is null )
			{
				child = new DirectoryNode( segment ) { ExistsLocally = true };
				current.Children.Add( child );
				current.Children.Sort( ( a, b ) => StringComparer.OrdinalIgnoreCase.Compare( a.Name, b.Name ) );
				changed = true;
			}
			else if( !child.ExistsLocally )
			{
				child.ExistsLocally = true;
				changed = true;
			}
			current = child;
		}
		return changed;
	}

	/// <summary>
	/// Updates the tree after the local folder at <paramref name="segmentsBelowRoot"/> was deleted or moved away:
	/// clears <see cref="ExistsLocally"/> across its subtree and removes local-only nodes.
	/// Returns <see langword="true"/> when the tree changed.
	/// </summary>
	public bool MarkLocalDeleted( ReadOnlySpan<string> segmentsBelowRoot )
	{
		if( segmentsBelowRoot.IsEmpty )
			return false;
		DirectoryNode? parent = FindBySegments( segmentsBelowRoot[ ..^1 ] );
		DirectoryNode? target = parent?.FindChild( segmentsBelowRoot[ ^1 ] );
		if( parent is null || target is null )
			return false;

		if( !target.IsRemote )
		{
			parent.Children.Remove( target );
			return true;
		}

		return ClearLocalRecursive( target );
	}

	private static bool ClearLocalRecursive( DirectoryNode node )
	{
		bool changed = false;
		if( node.ExistsLocally )
		{
			node.ExistsLocally = false;
			changed = true;
		}
		for( int i = node.Children.Count - 1; i >= 0; i-- )
		{
			DirectoryNode child = node.Children[ i ];
			if( !child.IsRemote )
			{
				node.Children.RemoveAt( i );
				changed = true;
			}
			else
			{
				changed |= ClearLocalRecursive( child );
			}
		}
		return changed;
	}

	/// <summary>Sorts children by name (case-insensitive), recursively.</summary>
	public void SortRecursive()
	{
		Children.Sort( ( a, b ) => StringComparer.OrdinalIgnoreCase.Compare( a.Name, b.Name ) );
		foreach( var child in Children )
			child.SortRecursive();
	}

	/// <summary>Total number of folders in this subtree, including this node.</summary>
	public int CountRecursive()
	{
		int count = 1;
		foreach( var child in Children )
			count += child.CountRecursive();
		return count;
	}

	/// <summary>
	/// Returns a filtered copy of this subtree keeping folders matching <paramref name="query"/>
	/// (case-insensitive) and their ancestor chain, or <see langword="null"/> if nothing matches.
	/// </summary>
	public DirectoryNode? Filter( string? query )
	{
		string term = query?.Trim() ?? "";
		if( term.Length == 0 )
			return this;
		return FilterNode( this, term, isRoot: true );
	}

	private static DirectoryNode? FilterNode( DirectoryNode node, string term, bool isRoot )
	{
		List<DirectoryNode> matchedChildren = [];
		foreach( DirectoryNode child in node.Children )
		{
			if( FilterNode( child, term, isRoot: false ) is { } kept )
				matchedChildren.Add( kept );
		}

		bool selfMatches = node.Name.Contains( term, StringComparison.OrdinalIgnoreCase );
		if( matchedChildren.Count > 0 )
		{
			var copy = CloneShallow( node );
			copy.Children.AddRange( matchedChildren );
			return copy;
		}
		if( selfMatches && ( !isRoot || node.Children.Count == 0 ) )
			return CloneSubtree( node );
		return null;
	}

	private static DirectoryNode CloneShallow( DirectoryNode node )
		=> new( node.Name, node.DirectoryId ) { ExistsLocally = node.ExistsLocally };

	private static DirectoryNode CloneSubtree( DirectoryNode node )
	{
		var copy = CloneShallow( node );
		foreach( DirectoryNode child in node.Children )
			copy.Children.Add( CloneSubtree( child ) );
		return copy;
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
	private static readonly StringComparer SegmentComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	public static void AddLocalDirectories( DirectoryNode root, IEnumerable<string> localTreePaths )
	{
		Dictionary<DirectoryNode, Dictionary<string, DirectoryNode>> childMaps = [];

		Dictionary<string, DirectoryNode> ChildrenOf( DirectoryNode parent )
		{
			if( childMaps.TryGetValue( parent, out var map ) )
				return map;
			map = new Dictionary<string, DirectoryNode>( parent.Children.Count, SegmentComparer );
			foreach( DirectoryNode existing in parent.Children )
				map.TryAdd( existing.Name, existing );
			childMaps[ parent ] = map;
			return map;
		}

		foreach( string path in localTreePaths )
		{
			DirectoryNode node = root;
			foreach( string segment in path.Split( Separators, StringSplitOptions.RemoveEmptyEntries ).Skip( 1 ) )
			{
				var map = ChildrenOf( node );
				if( !map.TryGetValue( segment, out DirectoryNode? child ) )
				{
					child = new DirectoryNode( segment );
					node.Children.Add( child );
					map[ segment ] = child;
				}
				child.ExistsLocally = true;
				node = child;
			}
		}
	}
}
