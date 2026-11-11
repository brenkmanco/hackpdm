using HackPDM.Core.Hack;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Converters;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>Turns a <see cref="DirectoryNode"/> tree into TreeView nodes with folder icons.</summary>
internal static class DirectoryTreeBuilder
{
	/// <summary>Replaces the tree's nodes with <paramref name="root"/>.</summary>
	public static void Build( TreeView tree, DirectoryNode root )
	{
		tree.RootNodes.Clear();
		tree.RootNodes.Add( CreateNode( root ) );
	}

	private static TreeViewNode CreateNode( DirectoryNode directory )
	{
		// give the node its content before it joins the tree: TreeView indexes nodes by
		// content, and a null content throws on Uno (ArgumentNullException 'key')
		TreeViewNode node = new();
		var data = node.LinkedData;
		data.Name = directory.Name;
		data.DirectoryId = directory.DirectoryId;
		data.Icon = EntryIcons.Asset( !directory.IsRemote ? "fo_loc"
			: directory.ExistsLocally ? "def_fo"
			: "fo_serv" );

		foreach( DirectoryNode child in directory.Children )
			node.Children.Add( CreateNode( child ) );
		return node;
	}
}
