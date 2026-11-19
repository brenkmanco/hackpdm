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
	private static readonly bool[] EmptyContinuations = [];
	private static readonly PropertyChangedEventArgs IsSelectedChangedArgs = new( nameof( IsSelectedInTree ) );
	private static readonly PropertyChangedEventArgs NameFontWeightChangedArgs = new( nameof( NameFontWeight ) );
	private static readonly PropertyChangedEventArgs IsExpandedChangedArgs = new( nameof( IsExpanded ) );
	private static readonly PropertyChangedEventArgs BranchRevisionChangedArgs = new( nameof( BranchRevision ) );
	private static readonly PropertyChangedEventArgs IsLastSiblingChangedArgs = new( nameof( IsLastSibling ) );
	private static readonly PropertyChangedEventArgs AncestorContinuationsChangedArgs = new( nameof( AncestorContinuations ) );
	private static readonly PropertyChangedEventArgs IsRootNodeChangedArgs = new( nameof( IsRootNode ) );
	private static readonly PropertyChangedEventArgs SubfolderCountChangedArgs = new( nameof( SubfolderCount ) );
	private static readonly PropertyChangedEventArgs SubfolderCountTextChangedArgs = new( nameof( SubfolderCountText ) );
	private static readonly PropertyChangedEventArgs SubfolderCountVisibilityChangedArgs = new( nameof( SubfolderCountVisibility ) );
	private static readonly PropertyChangedEventArgs TooltipTextChangedArgs = new( nameof( TooltipText ) );

	private string? _cachedFullPath;

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
		set
		{
			field = value;
			_cachedFullPath = null;
		}
	} = name;

	public string? FullPath
	{
		get
		{
			if( _cachedFullPath is not null )
				return _cachedFullPath;
			if( Depth < 0 )
				return null;
			TreeData? parent = Parent;
			return _cachedFullPath = parent is null ? Name : $"{parent.FullPath}\\{Name}";
		}
	}
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
			Notify( IsSelectedChangedArgs );
			Notify( NameFontWeightChangedArgs );
		}
	}

	public int SiblingIndex { get; private set; }
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
			Notify( IsExpandedChangedArgs );
			Notify( BranchRevisionChangedArgs );
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

	internal void SetBranchState( bool isLastSibling, bool[] ancestorContinuations, bool recursive = false, int siblingIndex = 0, string? parentFullPath = null )
	{
		SiblingIndex = siblingIndex;
		IsLastSibling = isLastSibling;
		AncestorContinuations = ancestorContinuations;
		_cachedFullPath = parentFullPath is null ? Name : $"{parentFullPath}\\{Name}";
		BranchRevision++;
		if( PropertyChanged is not null )
		{
			RefreshDisplay();
			Notify( IsLastSiblingChangedArgs );
			Notify( AncestorContinuationsChangedArgs );
			Notify( BranchRevisionChangedArgs );
		}

		if( !recursive || Node is null )
			return;

		int count = Node.Children.Count;
		bool[] childContinuations = Depth >= 1 ? [ .. ancestorContinuations, !isLastSibling ] : EmptyContinuations;
		for( int i = 0; i < count; i++ )
			Node.Children[ i ].LinkedData.SetBranchState( i == count - 1, childContinuations, recursive: true, siblingIndex: i, parentFullPath: _cachedFullPath );
	}

	internal void NotifyBranchChanged()
	{
		BranchRevision++;
		Notify( IsExpandedChangedArgs );
		Notify( BranchRevisionChangedArgs );
	}

	internal void RefreshDisplay()
	{
		if( PropertyChanged is null )
			return;
		Notify( IsRootNodeChangedArgs );
		Notify( NameFontWeightChangedArgs );
		Notify( SubfolderCountChangedArgs );
		Notify( SubfolderCountTextChangedArgs );
		Notify( SubfolderCountVisibilityChangedArgs );
		Notify( TooltipTextChangedArgs );
	}

	private void Notify( PropertyChangedEventArgs args )
		=> PropertyChanged?.Invoke( this, args );
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
				if( node.Content is TreeData existing && ReferenceEquals( existing.Node, node ) )
					return existing;
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
		if( string.IsNullOrEmpty( path ) )
			return null;
		string[] parts = path.Split( '\\', StringSplitOptions.RemoveEmptyEntries );
		if( parts.Length == 0 )
			return null;

		TreeViewNode? node = null;
		for( int i = 0; i < view.RootNodes.Count; i++ )
		{
			if( string.Equals( view.RootNodes[ i ].LinkedData.Name, parts[ 0 ], StringComparison.OrdinalIgnoreCase ) )
			{
				node = view.RootNodes[ i ];
				break;
			}
		}
		if( node is null && view.RootNodes.Count == 1 && string.Equals( parts[ 0 ], PwaPaths.Root, StringComparison.OrdinalIgnoreCase ) )
			node = view.RootNodes[ 0 ];
		for( int p = 1; p < parts.Length && node is not null; p++ )
		{
			string part = parts[ p ];
			TreeViewNode? next = null;
			for( int c = 0; c < node.Children.Count; c++ )
			{
				if( string.Equals( node.Children[ c ].LinkedData.Name, part, StringComparison.OrdinalIgnoreCase ) )
				{
					next = node.Children[ c ];
					break;
				}
			}
			node = next;
		}
		return node?.LinkedData;
	}

	public static void SetAllExpanded( this TreeView view, bool expanded )
	{
		ArgumentNullException.ThrowIfNull( view );
		for( int i = 0; i < view.RootNodes.Count; i++ )
			SetSubtreeExpanded( view.RootNodes[ i ], expanded, keepRootOpen: !expanded );
	}

	private static void SetSubtreeExpanded( TreeViewNode node, bool expanded, bool keepRootOpen )
	{
		bool targetExpanded = ( keepRootOpen && node.Depth == 0 ) || expanded;
		if( node.Children.Count > 0 && node.IsExpanded != targetExpanded )
			node.LinkedData.IsExpanded = targetExpanded;
		for( int i = 0; i < node.Children.Count; i++ )
			SetSubtreeExpanded( node.Children[ i ], expanded, keepRootOpen: false );
	}
}

internal static class DirectoryTreeBuilder
{
	public static void Build( TreeView tree, DirectoryNode root, IReadOnlySet<string>? expandedPaths = null, bool expandAll = false )
	{
		ImageSource? localIcon = EntryIcons.Asset( "fo_loc" );
		ImageSource? bothIcon = EntryIcons.Asset( "def_fo" );
		ImageSource? serverIcon = EntryIcons.Asset( "fo_serv" );

		tree.RootNodes.Clear();
		TreeViewNode rootNode = CreateNode( tree, root, parentPath: null, localIcon, bothIcon, serverIcon, expandedPaths, expandAll );
		tree.RootNodes.Add( rootNode );
		rootNode.LinkedData.SetBranchState( isLastSibling: true, [], recursive: true );
	}

	private static TreeViewNode CreateNode(
		TreeView tree,
		DirectoryNode directory,
		string? parentPath,
		ImageSource? localIcon,
		ImageSource? bothIcon,
		ImageSource? serverIcon,
		IReadOnlySet<string>? expandedPaths,
		bool expandAll )
	{
		string fullPath = parentPath is null ? directory.Name : $"{parentPath}\\{directory.Name}";
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

		if( directory.Children.Count > 0 )
		{
			for( int i = 0; i < directory.Children.Count; i++ )
				node.Children.Add( CreateNode( tree, directory.Children[ i ], fullPath, localIcon, bothIcon, serverIcon, expandedPaths, expandAll ) );
			if( expandAll || ( expandedPaths is not null && expandedPaths.Contains( fullPath ) ) )
				node.IsExpanded = true;
		}
		return node;
	}
}
