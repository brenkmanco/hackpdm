using HackPDM.Core.Hack;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Converters;
using HackPDM.Mvvm.Views.Common.Types;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using TreeData = HackPDM.Mvvm.Views.Common.Types.TreeData;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>Turns a <see cref="DirectoryNode"/> tree into TreeView nodes with folder icons and branch metadata.</summary>
internal static class DirectoryTreeBuilder
{
	/// <summary>Replaces the tree's nodes with <paramref name="root"/>.</summary>
	/// <param name="tree">The tree view to fill; its current nodes are removed.</param>
	/// <param name="root">The directory tree to show; each folder gets a local-only, server-only or both icon.</param>
	public static void Build( TreeView tree, DirectoryNode root )
	{
		ImageSource? localIcon = EntryIcons.Asset( "fo_loc" );
		ImageSource? bothIcon = EntryIcons.Asset( "def_fo" );
		ImageSource? serverIcon = EntryIcons.Asset( "fo_serv" );

		tree.RootNodes.Clear();
		TreeViewNode rootNode = CreateNode( tree, root, localIcon, bothIcon, serverIcon );
		tree.RootNodes.Add( rootNode );
		rootNode.LinkedData.SetBranchState( isLastSibling: true, [], recursive: true );
	}

	private static TreeViewNode CreateNode( TreeView tree, DirectoryNode directory, ImageSource? localIcon, ImageSource? bothIcon, ImageSource? serverIcon )
	{
		// give the node its content before it joins the tree: TreeView indexes nodes by
		// content, and a null content throws on Uno (ArgumentNullException 'key')
		TreeViewNode node = new();
		TreeData data = node.LinkedData;
		data.ParentTree = tree;
		data.Name = directory.Name;
		data.DirectoryId = directory.DirectoryId;
		data.Origin = !directory.IsRemote ? FolderOriginKind.LocalOnly
			: directory.ExistsLocally ? FolderOriginKind.Synced
			: FolderOriginKind.RemoteOnly;
		data.Icon = data.Origin switch
		{
			FolderOriginKind.LocalOnly => localIcon,
			FolderOriginKind.RemoteOnly => serverIcon,
			_ => bothIcon,
		};

		foreach( DirectoryNode child in directory.Children )
			node.Children.Add( CreateNode( tree, child, localIcon, bothIcon, serverIcon ) );
		return node;
	}
}
