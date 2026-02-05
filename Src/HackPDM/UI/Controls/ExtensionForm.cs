using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using HackPDM.Abstractions;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Data;
using HackPDM.UI.Forms;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Models;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

using Control = Microsoft.UI.Xaml.Controls.Control;
using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;
using ListViewItem = Microsoft.UI.Xaml.Controls.ListViewItem;
using Panel = Microsoft.UI.Xaml.Controls.Panel;
using TreeData = HackPDM.UI.Types.TreeData;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
//using Microsoft.UI.Xaml.Controls;

//using System.Windows.Controls;

namespace HackPDM.UI.Controls;

public static class ExtensionForm
{
	private static readonly ConditionalWeakTable<DependencyObject, HolderValues> _data = new();
	private static readonly GradientStopCollection _gradientStops;
	private static readonly GradientStop _gradientStop;
	private static readonly GradientStop _gradientStop2;
	private static readonly LinearGradientBrush _brush;

	
	static ExtensionForm()
	{
		_gradientStop = new();
		_gradientStop2 = new();
		_gradientStops = [];

		_gradientStop.Color = Color.FromArgb(255, 139, 224, 249);
		_gradientStop2.Color = Color.FromArgb(255, 209, 242, 252);

		_gradientStops.Add(_gradientStop);
		_gradientStops.Add(_gradientStop2);
            
		_brush = new()
		{
			GradientStops = _gradientStops,
			StartPoint = new Windows.Foundation.Point(0, 0),
			EndPoint = new Windows.Foundation.Point(1, 1)
		};
	}
	
	extension (Window window)
	{
		public bool SetWindowType(AppWindowPresenterKind kind)
		{
			try { window.AppWindow.SetPresenter(kind); return true; } catch { return false; }
		}
		public IntPtr IntPtrHandle { get { return WinRT.Interop.WindowNative.GetWindowHandle( window ); } }
	}
	extension (DataGrid grid)
	{
		public void SetAlternatingRowColors()
		{
			grid.RowBackground = _brush;
			grid.AlternatingRowBackground = UIStorage.BrushWhite;
		}
		public void ItemAdd<T>(T item)
		{
			GridMap.Map[grid].Add(item);
			//if (grid.ItemsSource is ObservableCollection<T> list)
			//{
			//	list.Add(item);
			//	// grid.ItemsSource = list;
			//}
		}
		public void ItemRemove<T>(T item)
		{
			GridMap.Map[grid].Remove(item);
			//if (grid.ItemsSource is ObservableCollection<T> list)
			//{
			//	list.Remove(item);
			//	// grid.ItemsSource = list;
			//}
		}
		//public Collection<object>? Items
		//{
		//	get => grid.ItemsSource as Collection<object>;
		//	set => grid.ItemsSource = value;
		//}
	}
	extension(IEnumerable<HpEntry> entries)
	{
		public bool MessageToRecommit()
		{
			if (entries.Any())
			{
				string lst = string.Join("\n", entries.Where(entry => entry.IsLatest).Take(10).Select(entry => $"{entry.name}"));
				string message = $"{lst}{(entries.Count() > 10 ? $"...\nincluding {entries.Count() - 10} other files\n" : "\n")}";
				if (DialogResult.Yes == MessageBox.ShowAsync($"{message}would you like to recommit the latest versions?", "recommit latest?", MessageBoxButtons.YesNoCancel).Result)
				{
					return true;
				}
			}
			return false;
		}
	}
	extension (ListViewItem item)
	{
		public DataGridData LinkedItem
		{
			get
			{
				if (_data.TryGetValue(item, out var holder)) return holder.ItemData;
				holder = new()
				{
					ItemData = new(),
				};
				_data.Add(item, holder);

				return holder.ItemData;
			}

			set
			{
				if (!_data.TryGetValue(item, out var holder))
				{
					holder = new();
					_data.Add(item, holder);
				}
				holder.ItemData = value;
				item.Content = holder.ItemData;
			}
		}
	}
	extension (TreeViewNode node)
	{
		public TreeData LinkedData
		{
			get 
			{
				if(!_data.TryGetValue(node, out var holder))
				{
					holder = new HolderValues();
					_data.Add(node, holder);
				}
				TreeData? dat = node.Content as TreeData;
				if (holder.TreeNodeData is not null && dat is not null && holder.TreeNodeData != dat)
				{
					holder.TreeNodeData.Name ??= dat.Name;
					holder.TreeNodeData.Tag ??= dat.Tag;
					holder.TreeNodeData.Icon ??= dat.Icon;
				}

				holder.TreeNodeData ??= dat ?? new("");
				holder.TreeNodeData.Node = node;
				node.Content = holder.TreeNodeData;

				return holder.TreeNodeData;
			} 
			set
			{
				if (!_data.TryGetValue(node, out var holder))
				{
					holder = new HolderValues();
					_data.Add(node, holder);
				}
				holder.TreeNodeData = value;
				node.Content = value;
			}
		}
		public T? Content<T>() where T : class
		{   
			ArgumentNullException.ThrowIfNull(node);
			return node.Content as T;
		}
	}
	extension (TreeViewNode? cNode)
	{
		public void UpdateBreadCrumbCollection(ObservableCollection<TreeData>? collection)
		{
			collection?.Clear();
			if (cNode is null) return;

			var currentNodePath = cNode?.GetNodePath()?.ToList();
			if (currentNodePath == null) return;

			foreach (var node in currentNodePath)
			{
				collection!.Add(node.LinkedData);
			}
		}

		public IEnumerable<TreeViewNode> GetNodePath()
			=> cNode.GetNodePathInternal().Reverse();
		private IEnumerable<TreeViewNode> GetNodePathInternal()
		{
			if (cNode is not null and {Depth: >= 0})
			{
				while (cNode.Depth >= 0)
				{
					yield return cNode;
					cNode = cNode.Parent!;
				}
			}
		}
	}
	extension (ItemsControl control)
	{
		public T? ItemsSource<T>() where T : class
		{
			ArgumentNullException.ThrowIfNull(control);
			return control.ItemsSource as T;
		}

		public void Sort<T>(Comparison<T> comparison) 
		{
			var casted = control.ItemsSource as List<T>;
			casted?.Sort(comparison.Invoke);
			if (casted is not null) control.ItemsSource = casted;
		}
	}
	extension (TreeData node)
	{
		public string? GetTreeNodePath()
		{
			ArgumentNullException.ThrowIfNull(node);
			return node.FullPath;
		}

		public void EnsureVisible(TreeView tree)
		{
			ArgumentNullException.ThrowIfNull(node);
			ArgumentNullException.ThrowIfNull(tree);
			TreeData? current = node;
			TreeViewItem treeItem = new();
			while (current != null)
			{
				current.IsExpanded = true;
				current = current.Parent;
			}
			tree.SelectedNode = node.Node;
			TreeViewItem? item = tree.ContainerFromNode(node.Node) as TreeViewItem;

			item?.StartBringIntoView();
		}
	}
	extension<T> (T page) where T : Page
	{
		public TWin? GetWindow<TWin>() where TWin : Window
		{
			return InstanceManager.GetAWindow<T, TWin>(page);
		}
		public Window? Window
		{
			get => InstanceManager.GetAWindow<T, Window>(page);
		}
	}


	public static IEnumerable<Control> GetAllControls(this Control control)
	{
		ArgumentNullException.ThrowIfNull(control);
		var controls = new List<Control> { control };
		if (control.XamlRoot?.Content is Panel panel)
		{
			foreach (var child in panel.Children)
			{
				if (child is Control childControl)
				{
					controls.AddRange(childControl.GetAllControls());
				}
			}
		}
		return controls;
	}
	private static TreeData? RecurseNode(this TreeData? node, ReadOnlySpan<string> paths)
	{
		foreach (TreeData tNode in node?.Children ?? [])
		{
			if (tNode.Name == paths[0])
			{
				return paths.Length == 1 ? node : node.RecurseNode(paths[1..]);
			}
		}
		return null;
	}
	public static TreeData? FindTreeNode(this TreeView view, string path)
	{
		ArgumentNullException.ThrowIfNull(view, nameof(view));
		Span<string> pathSpan = path.Split("\\").AsSpan();
		List<TreeViewNode>? children = view.RootNodes as List<TreeViewNode>;

		if (children is null) return null;
		foreach (TreeViewNode node in children)
		{
			TreeData treeData = node.LinkedData;
			if (treeData.Name == pathSpan[0])
			{
				return pathSpan.Length == 1 ? treeData : treeData.RecurseNode(pathSpan[1..]);
			}
		}
		return null;        
	}
	
	private class HolderValues
	{
		public bool IsSingleton { get; set; }=false;
		public TreeData? TreeNodeData { get; set;  } = null;
		public DataGridData? ItemData { get; set;  } = null;
	}
}

