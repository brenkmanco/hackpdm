using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.UI.Xaml.Controls;

using TreeData = HackPDM.Mvvm.Views.Common.Types.TreeData;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.Mvvm.Views.Common.Controls;

/// <summary>The <see cref="TreeData"/> behind each tree node, and walking the tree by path.</summary>
public static class TreeViewExtensions
{
	// the node's data, kept beside the node (weak: does not keep the node alive)
	private static readonly ConditionalWeakTable<TreeViewNode, TreeData> LinkedNodeData = new();

	extension( TreeViewNode node )
	{
		/// <summary>The <see cref="TreeData"/> kept for this node.</summary>
		/// <value>On read, created from the node's content when missing (merging any missing name, tag or icon from it),
		/// linked back to the node and set as its content. Setting it replaces the data and the content.</value>
		public TreeData LinkedData
		{
			get
			{
				TreeData? content = node.Content as TreeData;
				LinkedNodeData.TryGetValue( node, out TreeData? linked );
				if( linked is not null && content is not null && linked != content )
				{
					linked.Name ??= content.Name;
					linked.Tag ??= content.Tag;
					linked.Icon ??= content.Icon;
				}
				linked ??= content ?? new( "" );
				LinkedNodeData.AddOrUpdate( node, linked );
				linked.Node = node;
				node.Content = linked;
				return linked;
			}
			set
			{
				LinkedNodeData.AddOrUpdate( node, value );
				node.Content = value;
			}
		}
	}

	extension( TreeViewNode? node )
	{
		/// <summary>Fills <paramref name="collection"/> with the path from the root to this node.</summary>
		/// <param name="collection">The breadcrumb list to clear and refill; null does nothing.</param>
		public void UpdateBreadCrumbCollection( ObservableCollection<TreeData>? collection )
		{
			collection?.Clear();
			if( node is null || collection is null )
				return;
			foreach( TreeViewNode step in node.GetNodePath() )
				collection.Add( step.LinkedData );
		}

		/// <summary>The nodes from the root down to this one.</summary>
		/// <returns>The path, root first; empty for a null node.</returns>
		public IEnumerable<TreeViewNode> GetNodePath()
		{
			List<TreeViewNode> path = [];
			for( TreeViewNode? current = node; current is { Depth: >= 0 }; current = current.Parent )
				path.Add( current );
			path.Reverse();
			return path;
		}
	}

	extension( TreeData node )
	{
		/// <summary>Expands every ancestor, selects the node and scrolls to it.</summary>
		/// <param name="tree">The tree view showing the node.</param>
		/// <exception cref="ArgumentNullException">The node or <paramref name="tree"/> is null.</exception>
		public void EnsureVisible( TreeView tree )
		{
			ArgumentNullException.ThrowIfNull( node );
			ArgumentNullException.ThrowIfNull( tree );
			for( TreeData? current = node; current is not null; current = current.Parent )
				current.IsExpanded = true;
			tree.SelectedNode = node.Node;
			( tree.ContainerFromNode( node.Node ) as TreeViewItem )?.StartBringIntoView();
		}
	}

	/// <summary>Finds a node by its tree path.</summary>
	/// <param name="view">The tree view to search.</param>
	/// <param name="path">The tree path ("root\a\b", as <see cref="TreeData.FullPath"/> gives it); names are compared case-sensitively.</param>
	/// <returns>The node's data; null when the path is empty or a folder on the way is missing.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="view"/> is null.</exception>
	public static TreeData? FindTreeNode( this TreeView view, string path )
	{
		ArgumentNullException.ThrowIfNull( view );
		string[] parts = path.Split( '\\', StringSplitOptions.RemoveEmptyEntries );
		if( parts.Length == 0 )
			return null;
		// (this used to return the parent of the folder it found, and needed RootNodes to be a
		// List<TreeViewNode>: a rebuilt tree reselected the wrong folder, or none)
		TreeViewNode? node = view.RootNodes.FirstOrDefault( root => root.LinkedData.Name == parts[ 0 ] );
		foreach( string part in parts.AsSpan( 1 ) )
			node = node?.Children.FirstOrDefault( child => child.LinkedData.Name == part );
		return node?.LinkedData;
	}
}
