using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using HackPDM.Core;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Windows.UI.Text;
using WinUI.TableView;


using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.Mvvm.Views.Common.Types;

/// <summary>Where a folder in the directory tree exists.</summary>
public enum FolderOriginKind
{
	/// <summary>Exists both locally and on the Odoo server.</summary>
	Synced,
	/// <summary>Exists only in the local PWA folder.</summary>
	LocalOnly,
	/// <summary>Exists only on the Odoo server.</summary>
	RemoteOnly,
}

/// <summary>The data behind one node of the directory tree view: name, server directory id, icon and the linked <see cref="TreeViewNode"/>.</summary>
/// <param name="name">The folder name; null shows a placeholder once the node is linked.</param>
public partial class TreeData(string? name) : HackPDM.Domain.Representation.TreeData, IEnumerable<TreeData>, INotifyPropertyChanged
{
	private static readonly bool[] EmptyContinuations = [];

	public event PropertyChangedEventHandler? PropertyChanged;

	public TreeView? ParentTree { get; internal set; }
	public TreeViewNode? Node { get; internal set; }
	public TreeViewItem? VisualContainer => ParentTree?.ContainerFromNode(Node) as TreeViewItem;
	public TreeData? Parent => Node?.Depth <= 0 ? null : Node?.Parent?.LinkedData;
	public ImageSource? Icon { get; set; }
	public IEnumerable<TreeData>? Children => Node?.Children.Select(n => n.LinkedData);
	public override string? Name
	{
		get
		{
			return field ??= Depth is < 0 ? null : StorageBox.EMPTY_PLACEHOLDER;
		}
		set;
	} = name;

	public override string? FullPath => Parent is null ? Depth < 0 ? null : Name : $"{Parent?.FullPath}\\{Name}";
	public override object? Tag { get; set; }
	public override int? DirectoryId { get; set; }

	/// <summary>Whether the folder is synced, local-only, or server-only.</summary>
	public FolderOriginKind Origin { get; internal set; }

	/// <summary>True for a top-level root folder in the tree.</summary>
	public bool IsRootNode => Depth == 0;

	/// <summary>True when this folder is currently selected in the active tree.</summary>
	public bool IsSelectedInTree
	{
		get;
		internal set
		{
			if (field == value) return;
			field = value;
			Notify();
			Notify(nameof(NameFontWeight));
		}
	}

	public int SubfolderCount => Node?.Children.Count ?? 0;
	public bool HasSubfolders => SubfolderCount > 0;
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
		$"{FullPath ?? Name}\n{OriginLabel}{(SubfolderCount > 0 ? $" · {SubfolderCount} subfolder{(SubfolderCount == 1 ? "" : "s")}" : "")}";

	/// <summary>True when this node is the last child of its parent.</summary>
	public bool IsLastSibling { get; private set; } = true;

	/// <summary>
	/// Vertical guide continuation flags for ancestor columns <c>0 .. Depth - 2</c>;
	/// <see langword="true"/> at index <c>i</c> means ancestor at depth <c>i + 1</c> has siblings below it.
	/// </summary>
	public bool[] AncestorContinuations { get; private set; } = EmptyContinuations;

	/// <summary>Bumps whenever branch geometry (expansion, siblings, ancestors) changes so guide strips redraw.</summary>
	public int BranchRevision { get; private set; }

	public override bool IsLinked => Node is not null;
	public override bool HasChildren => Node?.HasChildren ?? false;
	public override int Depth => Node?.Depth ?? -1;
	public override bool IsExpanded
	{
		get => Node?.IsExpanded ?? false;
		set
		{
			if (Node is null) return;
			bool changed = Node.IsExpanded != value;
			Node.IsExpanded = value;
			if (changed)
			{
				BranchRevision++;
				Notify(nameof(IsExpanded));
				Notify(nameof(BranchRevision));
			}
		}
	}

	internal void SetBranchState(bool isLastSibling, bool[] ancestorContinuations, bool recursive = false)
	{
		IsLastSibling = isLastSibling;
		AncestorContinuations = ancestorContinuations;
		BranchRevision++;
		Notify(nameof(IsRootNode));
		Notify(nameof(NameFontWeight));
		Notify(nameof(SubfolderCount));
		Notify(nameof(SubfolderCountText));
		Notify(nameof(SubfolderCountVisibility));
		Notify(nameof(TooltipText));
		Notify(nameof(IsLastSibling));
		Notify(nameof(AncestorContinuations));
		Notify(nameof(BranchRevision));

		if (!recursive || Node is null)
			return;

		int count = Node.Children.Count;
		bool[] childContinuations = Depth >= 1
			? [.. ancestorContinuations, !isLastSibling]
			: EmptyContinuations;
		for (int i = 0; i < count; i++)
		{
			Node.Children[i].LinkedData.SetBranchState(i == count - 1, childContinuations, recursive: true);
		}
	}

	internal void NotifyBranchChanged()
	{
		BranchRevision++;
		Notify(nameof(IsExpanded));
		Notify(nameof(BranchRevision));
	}

	private void Notify([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	public bool? IsLocalOnly
	{
		get => Node is null
			? null
			: IsLocal is true && IsRemote is false;
	}
	/// <summary>Whether the folder exists in the local vault.</summary>
	/// <value>Null when not linked to a node; true for a folder with no server id; otherwise checks the disk on every read.</value>
	public bool? IsLocal
	{
		get
		{
			if (Node is null) return null;
			if (DirectoryId is null or 0) return true;
			if (PwaPaths.ToAbsolute( FullPath ) is not string path) return false;

			DirectoryInfo folder = new(path);
			return folder.Exists;
		}
	}
	public bool? IsRemoteOnly
	{
		get => Node is null
			? null
			: IsRemote is true && IsLocal is false;
	}
	public bool? IsRemote
	{
		get =>
			Node is null
				? null
				: DirectoryId is not null and not 0;

	}

	/// <summary>Sorts this node's children, and recursively theirs, by name ignoring case.</summary>
	/// <remarks>Rebuilds the linked node's child collection; does nothing when the data is not linked to a node.</remarks>
	public override void SortTree()
	{
		var root = Node;
		if (root is null || root.Children.Count == 0) return;

		// sort children by TreeData.Name
		ObservableCollection<TreeViewNode> sortedChildren =
		[
			.. root.Children
				.OrderBy(n => (n.LinkedData?.Name ?? string.Empty), StringComparer.OrdinalIgnoreCase)
		];
		root.Children.Clear();
		foreach (var child in sortedChildren)
		{
			root.Children.Add(child);
			child.LinkedData.SortTree();
		}
		SetBranchState(IsLastSibling, AncestorContinuations, recursive: true);
	}
	public IEnumerator<TreeData> GetEnumerator() => Children?.GetEnumerator() ?? Enumerable.Empty<TreeData>().GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => (Children as IEnumerable)?.GetEnumerator() ?? Enumerable.Empty<TreeData>().GetEnumerator();
}