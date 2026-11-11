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
using HackPDM.Mvvm.Views.Common.Settings;
using HackPDM.Shell;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Dialogs;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Views.Common.Types;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

using Control = Microsoft.UI.Xaml.Controls.Control;
using TableView = WinUI.TableView.TableView;
using ListViewItem = Microsoft.UI.Xaml.Controls.ListViewItem;
using Panel = Microsoft.UI.Xaml.Controls.Panel;
using TreeData = HackPDM.Mvvm.Views.Common.Types.TreeData;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
//using Microsoft.UI.Xaml.Controls;

//using System.Windows.Controls;

namespace HackPDM.Mvvm.Views.Common.Controls;

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
#if WINDOWS
		public IntPtr IntPtrHandle { get { return WinRT.Interop.WindowNative.GetWindowHandle( window ); } }
#else
		public IntPtr IntPtrHandle => IntPtr.Zero;
#endif
	}
	extension (TableView grid)
	{
		//public Collection<object>? Items
		//{
		//	get => grid.ItemsSource as Collection<object>;
		//	set => grid.ItemsSource = value;
		//}
	}
	extension(IEnumerable<HpEntry> entries)
	{
	}
	extension (ListViewItem item)
	{
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

	}
	extension (TreeData node)
	{

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
		public Window? Window
		{
			get => InstanceManager.GetAWindow<T, Window>(page);
		}
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
		return default;
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
		return default;        
	}
	

	private class HolderValues
	{
		public bool IsSingleton { get; set; }=false;
		public TreeData? TreeNodeData { get; set;  } = null;
		public DataGridData? ItemData { get; set;  } = null;
	}
}

