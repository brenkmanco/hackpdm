using System.Threading.Tasks;

using HackPDM.Core.Hack;
using HackPDM.Services;
using HackPDM.UI.Controls;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.UI.Forms.Hack;

/// <summary>Turns a <see cref="DirectoryNode"/> tree into TreeView nodes with folder icons.</summary>
internal sealed class DirectoryTreeBuilder( IEntryIconProvider icons )
{
	private ImageSource? _remoteOnly, _synced, _localOnly;

	/// <summary>Replaces the tree's nodes with <paramref name="root"/>.</summary>
	public async Task BuildAsync( TreeView tree, DirectoryNode root )
	{
		_remoteOnly ??= await icons.GetAssetAsync( "fo_serv" );
		_synced ??= await icons.GetAssetAsync( "def_fo" );
		_localOnly ??= await icons.GetAssetAsync( "fo_loc" );

		tree.RootNodes.Clear();
		tree.RootNodes.Add( CreateNode( root ) );
	}

	private TreeViewNode CreateNode( DirectoryNode directory )
	{
		// give the node its content before it joins the tree: TreeView indexes nodes by
		// content, and a null content throws on Uno (ArgumentNullException 'key')
		TreeViewNode node = new();
		var data = node.LinkedData;
		data.Name = directory.Name;
		data.DirectoryId = directory.DirectoryId;
		data.Icon = !directory.IsRemote ? _localOnly
			: directory.ExistsLocally ? _synced
			: _remoteOnly;

		foreach( DirectoryNode child in directory.Children )
			node.Children.Add( CreateNode( child ) );
		return node;
	}
}
