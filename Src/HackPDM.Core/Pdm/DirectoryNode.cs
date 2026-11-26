using System.ComponentModel;

namespace HackPDM.Core.Pdm;

/// <summary>Identifies a folder selected in the directory tree without holding UI types.</summary>
[Bindable( true )]
public sealed record DirectoryRef( string Name, int DirectoryId, string TreePath, bool IsRemoteOnly );

/// <summary>One folder in the merged remote + local directory tree.</summary>
[Bindable( true )]
public sealed class DirectoryNode( string name, int directoryId = 0 )
{
	private static readonly Comparison<DirectoryNode> ByNameIgnoreCase =
		static ( a, b ) => StringComparer.OrdinalIgnoreCase.Compare( a.Name, b.Name );

	public string Name { get; } = name;
	public int DirectoryId { get; } = directoryId;
	public bool ExistsLocally { get; set; }
	public List<DirectoryNode> Children { get; } = [];
	public bool IsRemote => DirectoryId != 0;

	public DirectoryNode? FindChild( string childName )
	{
		for( int i = 0; i < Children.Count; i++ )
		{
			if( string.Equals( Children[ i ].Name, childName, PwaPaths.PathComparison ) )
				return Children[ i ];
		}
		return null;
	}

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
				InsertSorted( current.Children, child );
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

	private static void InsertSorted( List<DirectoryNode> list, DirectoryNode item )
	{
		int lo = 0, hi = list.Count;
		while( lo < hi )
		{
			int mid = lo + ( ( hi - lo ) >> 1 );
			if( StringComparer.OrdinalIgnoreCase.Compare( list[ mid ].Name, item.Name ) <= 0 )
				lo = mid + 1;
			else
				hi = mid;
		}
		list.Insert( lo, item );
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
		Children.Sort( ByNameIgnoreCase );
		for( int i = 0; i < Children.Count; i++ )
			Children[ i ].SortRecursive();
	}

	public int CountRecursive()
	{
		int count = 1;
		for( int i = 0; i < Children.Count; i++ )
			count += Children[ i ].CountRecursive();
		return count;
	}

	public DirectoryNode? Filter( string? query )
	{
		string term = query?.Trim() ?? "";
		return term.Length == 0 ? this : FilterNode( this, term, isRoot: true );
	}

	private static DirectoryNode? FilterNode( DirectoryNode node, string term, bool isRoot )
	{
		List<DirectoryNode>? matchedChildren = null;
		for( int i = 0; i < node.Children.Count; i++ )
		{
			if( FilterNode( node.Children[ i ], term, isRoot: false ) is { } kept )
				( matchedChildren ??= [] ).Add( kept );
		}

		if( matchedChildren is not null )
		{
			DirectoryNode copy = CloneShallow( node );
			copy.Children.AddRange( matchedChildren );
			return copy;
		}
		if( ( !isRoot || node.Children.Count == 0 ) && node.Name.Contains( term, StringComparison.OrdinalIgnoreCase ) )
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

	public static void AddLocalDirectories( DirectoryNode root, IEnumerable<string> localTreePaths )
	{
		Dictionary<DirectoryNode, Dictionary<string, DirectoryNode>> childMaps = [];

		Dictionary<string, DirectoryNode> ChildrenOf( DirectoryNode parent )
		{
			if( childMaps.TryGetValue( parent, out var map ) )
				return map;
			map = new Dictionary<string, DirectoryNode>( parent.Children.Count, PwaPaths.PathComparer );
			foreach( DirectoryNode existing in parent.Children )
				map.TryAdd( existing.Name, existing );
			childMaps[ parent ] = map;
			return map;
		}

		foreach( string path in localTreePaths )
		{
			DirectoryNode node = root;
			string[] segments = path.Split( Separators, StringSplitOptions.RemoveEmptyEntries );
			for( int i = 1; i < segments.Length; i++ )
			{
				string segment = segments[ i ];
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
