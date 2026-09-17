using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

using HackPDM.Core;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using static HackPDM.Core.General.UnsafeExtensions;

using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.UI.Types;

public partial class DynamicGroupCollection<T> : ObservableCollection<GroupInfoList<T>>
{
	public CollectionViewSource ViewSource { get; private set; }
	public ObservableCollection<T> Master { get; } = [];
	private Func<T, object>? _selector;
	public bool IsGrouped => this.Count != 0 && _selector != null;
	public bool NoGrouping { get; set; } = true;
}
public partial class DynamicGroupCollection<T> : ObservableCollection<GroupInfoList<T>>, IItemsName
{
	public string ItemsPropName { get => "Values"; }

	public DynamicGroupCollection() : this( Enumerable.Empty<GroupInfoList<T>>() ) {}
	public DynamicGroupCollection( List<GroupInfoList<T>> collection ) : this( ( IEnumerable < GroupInfoList < T >> )collection ) { }
	public DynamicGroupCollection( IEnumerable<GroupInfoList<T>> collection, Func<T, object>? selector = null ) : base( collection ) 
	{
		_selector = selector;
		ViewSource = new()
		{
			IsSourceGrouped = true,
			ItemsPath = new Microsoft.UI.Xaml.PropertyPath( ItemsPropName ),
			Source = this,
		};
	}

	public void SetViewSource(CollectionViewSource viewSource)
	{
		ViewSource = viewSource;
		viewSource.Source = this;
		viewSource.ItemsPath = new PropertyPath(ItemsPropName);
	}
	public void Regroup(Func<T, object>? keySelector)
	{
		_selector = keySelector;

		// clear current groups
		this.Clear();

		if( keySelector == null || !Master.Any() )
			return;

		// group generic list based on selector
		var newGroups = Master.GroupBy(keySelector).Select(g => new GroupInfoList<T>(g.Key, g));
		foreach (var group in newGroups)
		{
			this.Add( group );
		}
	}
	public void Regroup() => Regroup( _selector );
	// Helper method to add an item to the MasterList and instantly re-apply the current grouping
	public void AddItemAndRegroup( T item )
	{
		Master.Add( item );
		if( NoGrouping is false && _selector != null )
		{
			Regroup( _selector );
		}
	}
	public void AddItem( T item )
	{
		// 1. Always keep the master list in sync
		Master.Add( item );

		// 2. If we aren't currently grouped, do nothing else
		if( NoGrouping || _selector == null )
			return;

		// 3. Figure out which group this item belongs to
		object key = _selector(item);

		// 4. Look for an existing group with this key
		// We use object.Equals to safely handle null keys
		var targetGroup = this.FirstOrDefault(g => object.Equals(g.Key, key));

		if( targetGroup != null )
		{
			// The group exists! Just add the item to it. 
			// The UI will automatically animate the new row in.
			targetGroup.Add( item );
		}
		else
		{
			// The group doesn't exist yet. Create a new group header and add it.
			var newGroup = new GroupInfoList<T>(key, [item]);
			this.Add( newGroup );
		}
	}
	public void RemoveItem( T item )
	{
		// 1. Remove from master list
		if( !Master.Remove( item ) )
			return; // Item wasn't in the list

		// 2. If we aren't grouped, we are done
		if( _selector == null )
			return;

		// 3. Find the group it belongs to
		object key = _selector(item);
		var targetGroup = this.FirstOrDefault(g => object.Equals(g.Key, key));

		if( targetGroup != null )
		{
			// Remove the item from the UI group
			targetGroup.Remove( item );

			// Optional cleanup: If the group is now empty, remove the whole group header!
			if( targetGroup.Count == 0 )
			{
				this.Remove( targetGroup );
			}
		}
	}
	public void ClearAll()
	{
		Master.Clear();
		base.ClearItems();
	}
}
public interface IItemsName
{
	public string ItemsPropName { get; }
}
public class GroupInfoList<T> : ObservableCollection<T>, IItemsName
{
	public object Key { get; set; }
	public string ItemsPropName { get => nameof(Values); }
	public IList<T> Values { get => Items; }
	public void AddItem( T item ) => this.Add( item );
	
	public void RemoveItem( T item ) => this.Remove( item );
	public GroupInfoList( object key, IEnumerable<T> items ) : base(items)
	{
		this.Key = key;
	}
}
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