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
		public void UpdateBreadCrumbCollection( ObservableCollection<TreeData>? collection )
		{
			collection?.Clear();
			if( node is null || collection is null )
				return;
			foreach( TreeViewNode step in node.GetNodePath() )
				collection.Add( step.LinkedData );
		}

		/// <summary>The nodes from the root down to this one.</summary>
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

	/// <summary>The node at <paramref name="path"/> ("root\a\b", as <see cref="TreeData.FullPath"/> gives it); null when a folder on the way is missing.</summary>
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
