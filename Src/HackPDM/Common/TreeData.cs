using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using HackPDM.Core.Pdm;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Windows.UI.Text;

namespace HackPDM.Common;

public enum FolderOriginKind
{
	Synced,
	LocalOnly,
	RemoteOnly,
}

[Microsoft.UI.Xaml.Data.Bindable]
public sealed partial class TreeData( string? name ) : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	public TreeView? ParentTree { get; internal set; }
	public TreeViewNode? Node { get; internal set; }
	public TreeData? Parent => Node?.Depth <= 0 ? null : Node?.Parent?.LinkedData;
	public ImageSource? Icon { get; set; }
	public int? DirectoryId { get; set; }
	public FolderOriginKind Origin { get; internal set; }

	public string? Name
	{
		get => field ??= Depth < 0 ? null : PdmConstants.EmptyPlaceholder;
		set;
	} = name;

	public string? FullPath => Parent is null ? ( Depth < 0 ? null : Name ) : $"{Parent.FullPath}\\{Name}";
	public int Depth => Node?.Depth ?? -1;
	public bool IsRootNode => Depth == 0;
	public int SubfolderCount => Node?.Children.Count ?? 0;
	public string SubfolderCountText => SubfolderCount > 0 ? SubfolderCount.ToString() : string.Empty;
	public Visibility SubfolderCountVisibility => SubfolderCount > 0 ? Visibility.Visible : Visibility.Collapsed;
	public Visibility LocalBadgeVisibility => Origin == FolderOriginKind.LocalOnly ? Visibility.Visible : Visibility.Collapsed;
	public Visibility RemoteBadgeVisibility => Origin == FolderOriginKind.RemoteOnly ? Visibility.Visible : Visibility.Collapsed;
	public FontWeight NameFontWeight => IsRootNode || IsSelectedInTree ? FontWeights.SemiBold : FontWeights.Normal;

	public string OriginLabel => Origin switch
	{
		FolderOriginKind.LocalOnly => "Local only",
		FolderOriginKind.RemoteOnly => "Server only",
		_ => "Synced",
	};

	public string TooltipText =>
		$"{FullPath ?? Name}\n{OriginLabel}{( SubfolderCount > 0 ? $" · {SubfolderCount} subfolder{( SubfolderCount == 1 ? "" : "s" )}" : "" )}";

	public bool IsSelectedInTree
	{
		get;
		internal set
		{
			if( field == value )
				return;
			field = value;
			Notify();
			Notify( nameof( NameFontWeight ) );
		}
	}

	private static readonly bool[] EmptyContinuations = [];

	public bool IsLastSibling { get; private set; } = true;
	public bool[] AncestorContinuations { get; private set; } = EmptyContinuations;
	public int BranchRevision { get; private set; }
	public bool IsLinked => Node is not null;
	public bool HasChildren => Node?.HasChildren ?? false;
	public bool HasSubfolders => SubfolderCount > 0;

	public bool IsExpanded
	{
		get => Node?.IsExpanded ?? false;
		set
		{
			if( Node is null || Node.IsExpanded == value )
				return;
			Node.IsExpanded = value;
			BranchRevision++;
			Notify();
			Notify( nameof( BranchRevision ) );
		}
	}

	public bool? IsRemote => Node is null ? null : DirectoryId is not null and not 0;

	public bool? IsLocal
	{
		get
		{
			if( Node is null )
				return null;
			if( DirectoryId is null or 0 )
				return true;
			return PwaPaths.ToAbsolute( FullPath ) is { } path && Directory.Exists( path );
		}
	}

	public bool? IsLocalOnly => Node is null ? null : IsLocal is true && IsRemote is false;
	public bool? IsRemoteOnly => Node is null ? null : IsRemote is true && IsLocal is false;

	internal void SetBranchState( bool isLastSibling, bool[] ancestorContinuations, bool recursive = false )
	{
		IsLastSibling = isLastSibling;
		AncestorContinuations = ancestorContinuations;
		BranchRevision++;
		RefreshDisplay();
		Notify( nameof( IsLastSibling ) );
		Notify( nameof( AncestorContinuations ) );
		Notify( nameof( BranchRevision ) );

		if( !recursive || Node is null )
			return;

		int count = Node.Children.Count;
		bool[] childContinuations = Depth >= 1 ? [ .. ancestorContinuations, !isLastSibling ] : EmptyContinuations;
		for( int i = 0; i < count; i++ )
			Node.Children[ i ].LinkedData.SetBranchState( i == count - 1, childContinuations, recursive: true );
	}

	internal void NotifyBranchChanged()
	{
		BranchRevision++;
		Notify( nameof( IsExpanded ) );
		Notify( nameof( BranchRevision ) );
	}

	internal void RefreshDisplay()
	{
		Notify( nameof( IsRootNode ) );
		Notify( nameof( NameFontWeight ) );
		Notify( nameof( SubfolderCount ) );
		Notify( nameof( SubfolderCountText ) );
		Notify( nameof( SubfolderCountVisibility ) );
		Notify( nameof( TooltipText ) );
	}

	private void Notify( [CallerMemberName] string? propertyName = null )
		=> PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
}

public static class TreeViewExtensions
{
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
					linked.Icon ??= content.Icon;
				}
				linked ??= content ?? new( "" );
				LinkedNodeData.AddOrUpdate( node, linked );
				linked.Node = node;
				node.Content = linked;
				return linked;
			}
		}
	}

	extension( TreeViewNode? node )
	{
		public void UpdateBreadCrumbCollection( ObservableCollection<TreeData>? collection )
		{
			collection?.Clear();
			if( node is null || collection is null )
				return;
			foreach( TreeViewNode step in node.GetNodePath() )
				collection.Add( step.LinkedData );
		}

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

	public static TreeData? FindTreeNode( this TreeView view, string path )
	{
		ArgumentNullException.ThrowIfNull( view );
		string[] parts = path.Split( '\\', StringSplitOptions.RemoveEmptyEntries );
		if( parts.Length == 0 )
			return null;

		TreeViewNode? node = view.RootNodes.FirstOrDefault( root => string.Equals( root.LinkedData.Name, parts[ 0 ], StringComparison.OrdinalIgnoreCase ) );
		if( node is null && view.RootNodes.Count == 1 && string.Equals( parts[ 0 ], PwaPaths.Root, StringComparison.OrdinalIgnoreCase ) )
			node = view.RootNodes[ 0 ];
		foreach( string part in parts.AsSpan( 1 ) )
			node = node?.Children.FirstOrDefault( child => string.Equals( child.LinkedData.Name, part, StringComparison.OrdinalIgnoreCase ) );
		return node?.LinkedData;
	}

	public static void SetAllExpanded( this TreeView view, bool expanded )
	{
		ArgumentNullException.ThrowIfNull( view );
		foreach( TreeViewNode root in view.RootNodes )
			SetSubtreeExpanded( root, expanded, keepRootOpen: !expanded );
	}

	private static void SetSubtreeExpanded( TreeViewNode node, bool expanded, bool keepRootOpen )
	{
		bool targetExpanded = ( keepRootOpen && node.Depth == 0 ) || expanded;
		if( node.Children.Count > 0 && node.IsExpanded != targetExpanded )
			node.LinkedData.IsExpanded = targetExpanded;
		foreach( TreeViewNode child in node.Children )
			SetSubtreeExpanded( child, expanded, keepRootOpen: false );
	}
}

internal static class DirectoryTreeBuilder
{
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

	private static TreeViewNode CreateNode(
		TreeView tree,
		DirectoryNode directory,
		ImageSource? localIcon,
		ImageSource? bothIcon,
		ImageSource? serverIcon )
	{
		TreeViewNode node = new();
		TreeData data = node.LinkedData;
		data.ParentTree = tree;
		data.Name = directory.Name;
		data.DirectoryId = directory.DirectoryId;
		data.Origin = !directory.IsRemote
			? FolderOriginKind.LocalOnly
			: directory.ExistsLocally
				? FolderOriginKind.Synced
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
