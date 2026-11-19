namespace HackPDM.Core.Pdm;

/// <summary>Identifies a folder selected in the directory tree without holding UI types.</summary>
public sealed record DirectoryRef( string Name, int DirectoryId, string TreePath, bool IsRemoteOnly );

/// <summary>One folder in the merged remote + local directory tree.</summary>
public sealed class DirectoryNode( string name, int directoryId = 0 )
{
	private static readonly StringComparison NameComparison = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	public string Name { get; } = name;
	public int DirectoryId { get; } = directoryId;
	public bool ExistsLocally { get; set; }
	public List<DirectoryNode> Children { get; } = [];
	public bool IsRemote => DirectoryId != 0;

	public DirectoryNode? FindChild( string childName )
		=> Children.FirstOrDefault( c => string.Equals( c.Name, childName, NameComparison ) );

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

	public void SortRecursive()
	{
		Children.Sort( ( a, b ) => StringComparer.OrdinalIgnoreCase.Compare( a.Name, b.Name ) );
		foreach( DirectoryNode child in Children )
			child.SortRecursive();
	}

	public int CountRecursive()
	{
		int count = 1;
		foreach( DirectoryNode child in Children )
			count += child.CountRecursive();
		return count;
	}

	public DirectoryNode? Filter( string? query )
	{
		string term = query?.Trim() ?? "";
		return term.Length == 0 ? this : FilterNode( this, term, isRoot: true );
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
			DirectoryNode copy = CloneShallow( node );
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
		DirectoryNode copy = CloneShallow( node );
		foreach( DirectoryNode child in node.Children )
			copy.Children.Add( CloneSubtree( child ) );
		return copy;
	}
}

/// <summary>Merges local-only folders into a server directory tree.</summary>
public static class DirectoryTreeMerger
{
	private static readonly char[] Separators = [ '\\', '/' ];
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
