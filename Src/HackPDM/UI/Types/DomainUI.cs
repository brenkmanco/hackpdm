using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HackPDM.Core;

using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.UI.Types;

public class EntryRow : HackPDM.Domain.Representation.EntryRow
{
	public ImageSource? Icon { get; set; }
	public ImageSource? StatusIcon { get; set; }
	public override FileInfo? LocalFile
	{
		get
		{
			if (field is not null) return field;

			string? path = ReprType switch
			{
				EntryReprType.Both or EntryReprType.Remote => FileOperations.ConvertToWindowsPath(FullName, true),
				EntryReprType.Local => FullName,
				_ => null,
			};
			
			FileInfo? file = string.IsNullOrEmpty(path) ? null : new(path);
			field = file?.Exists is true ? file : null;
			return field;
		}
		set;
	}
	public override bool? IsLocal
	{
		get
		{
			field = Id switch
			{
				null => null,
				0 => true,
				_ => LocalFile?.Exists ?? false,
			};
			
			return field;
		}
	}
	public override bool? IsRemote
	{
		get
		{
			field = Id switch
			{
				null => null,
				not 0 => true,
				_ => false,
			};
			return field;
		}
	}
	// need IsLocal to be hit so that LocalFile is evaluated
	public override bool? IsOnlyLocal => IsRemote is false & IsLocal is true;
	public override bool? IsOnlyRemote => IsRemote is true & IsLocal is false;
}

public partial class TreeData(string? name) : HackPDM.Domain.Representation.TreeData, IEnumerable<TreeData>
{
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

	public override bool IsLinked => Node is not null;
	public override bool HasChildren => Node?.HasChildren ?? false;
	public override int Depth => Node?.Depth ?? -1;
	public override bool IsExpanded
	{
		get => Node?.IsExpanded ?? false;
		set => Node?.IsExpanded = value;
	}
	public bool? IsLocalOnly
	{
		get => Node is null
			? null
			: IsLocal is true && IsRemote is false;
	}
	public bool? IsLocal
	{
		get
		{
			if (Node is null) return null;
			if (DirectoryId is null or 0) return true;
			if (FileOperations.NodePathToWindowsPath(FullPath, true) is not string path) return false;

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
	}
	public IEnumerator<TreeData> GetEnumerator() => Children?.GetEnumerator() ?? Enumerable.Empty<TreeData>().GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => (Children as IEnumerable)?.GetEnumerator() ?? Enumerable.Empty<TreeData>().GetEnumerator();
}