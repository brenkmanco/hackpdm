using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

using CommunityToolkit.Mvvm.ComponentModel;

using HackPDM.Core.Pdm;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

using Windows.UI.Text;

namespace HackPDM.Common;

public enum FolderOriginKind
{
	Synced,
	LocalOnly,
	RemoteOnly,
}

[Microsoft.UI.Xaml.Data.Bindable]
public sealed partial class TreeData : ObservableObject
{
	private static readonly bool[] EmptyContinuations = [];

	private string? _cachedFullPath;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( NameFontWeight ) )]
	public partial bool IsSelectedInTree { get; internal set; }

	[ObservableProperty]
	public partial bool IsLastSibling { get; private set; }

	[ObservableProperty]
	public partial bool[] AncestorContinuations { get; private set; }

	[ObservableProperty]
	public partial int BranchRevision { get; private set; }

	public TreeData( string? name )
	{
		Name = name;
		IsLastSibling = true;
		AncestorContinuations = EmptyContinuations;
	}

	public TreeView? ParentTree { get; internal set; }
	public TreeViewNode? Node { get; internal set; }
	public TreeData? Parent => Node?.Depth <= 0 ? null : Node?.Parent?.LinkedData;
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
	}

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

	public int SiblingIndex { get; private set; }
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
			OnPropertyChanged( nameof( IsExpanded ) );
		}
	}

	internal void SetBranchState(
		bool isLastSibling,
		bool[] ancestorContinuations,
		bool recursive = false,
		int siblingIndex = 0,
		string? parentFullPath = null,
		int depth = -1,
		bool notifyDisplay = true )
	{
		int currentDepth = depth >= 0 ? depth : Depth;
		SiblingIndex = siblingIndex;
		IsLastSibling = isLastSibling;
		AncestorContinuations = ancestorContinuations;
		_cachedFullPath = parentFullPath is null ? Name : $"{parentFullPath}\\{Name}";
		BranchRevision++;
		if( notifyDisplay )
			RefreshDisplay();

		if( !recursive || Node is null )
			return;

		int count = Node.Children.Count;
		if( count == 0 )
			return;

		bool[] childContinuations = currentDepth >= 1 ? [ .. ancestorContinuations, !isLastSibling ] : EmptyContinuations;
		for( int i = 0; i < count; i++ )
		{
			Node.Children[ i ].LinkedData.SetBranchState(
				i == count - 1,
				childContinuations,
				recursive: true,
				siblingIndex: i,
				parentFullPath: _cachedFullPath,
				depth: currentDepth + 1,
				notifyDisplay: notifyDisplay );
		}
	}

	internal void NotifyBranchChanged()
	{
		BranchRevision++;
		OnPropertyChanged( nameof( IsExpanded ) );
	}

	internal void RefreshDisplay()
	{
		OnPropertyChanged( nameof( IsRootNode ) );
		OnPropertyChanged( nameof( NameFontWeight ) );
		OnPropertyChanged( nameof( SubfolderCount ) );
		OnPropertyChanged( nameof( SubfolderCountText ) );
		OnPropertyChanged( nameof( SubfolderCountVisibility ) );
		OnPropertyChanged( nameof( TooltipText ) );
	}
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
					linked.Name ??= content.Name;
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
			if( collection is null )
				return;
			if( node is null )
			{
				if( collection is RangeObservableCollection<TreeData> range )
					range.ClearIfNonEmpty();
				else
					collection.Clear();
				return;
			}
			List<TreeViewNode> steps = node.GetNodePath();
			if( collection is RangeObservableCollection<TreeData> rangeCol )
			{
				List<TreeData> items = new( steps.Count );
				for( int i = 0; i < steps.Count; i++ )
					items.Add( steps[ i ].LinkedData );
				rangeCol.ReplaceAll( items );
				return;
			}
			collection.Clear();
			for( int i = 0; i < steps.Count; i++ )
				collection.Add( steps[ i ].LinkedData );
		}

		public List<TreeViewNode> GetNodePath()
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
		tree.RootNodes.Clear();
		TreeViewNode rootNode = CreateNode( tree, root, parentPath: null, expandedPaths, expandAll );
		rootNode.LinkedData.SetBranchState( isLastSibling: true, [], recursive: true, depth: 0, notifyDisplay: false );
		tree.RootNodes.Add( rootNode );
	}

	private static TreeViewNode CreateNode(
		TreeView tree,
		DirectoryNode directory,
		string? parentPath,
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

		if( directory.Children.Count > 0 )
		{
			for( int i = 0; i < directory.Children.Count; i++ )
				node.Children.Add( CreateNode( tree, directory.Children[ i ], fullPath, expandedPaths, expandAll ) );
			if( expandAll || ( expandedPaths is not null && expandedPaths.Contains( fullPath ) ) )
				node.IsExpanded = true;
		}
		return node;
	}
}
