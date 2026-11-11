using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Models;
using HackPDM.UI.Types;
using Microsoft.UI.Xaml.Controls;
using EntryRow = HackPDM.Domain.Representation.EntryRow;
using TableView = WinUI.TableView.TableView;
using ListView = Microsoft.UI.Xaml.Controls.ListView;
using OClient = HackPDM.Infrastructure.Odoo.OdooClient;
using HackPDM.UI.Compatibility;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HackPDM.UI.Forms.FormTransport
{
	internal class GridHelp
	{
		#region ListView / DataGrid Functions
		public static void InitGridView(TableView grid) => InitListViewInternal(grid);
		private static void InitListViewInternal(TableView grid)
		{
			var collect = grid.ItemsSource as IList;
			collect?.Clear();
		}
		private static void InitListViewInternal(ListView list, ListDetail rows)
			=> InitListView(list, rows);

		public static void ResetListViews(IEnumerable<TableView> grids)
		{
			foreach (var grid in grids)
			{
				InitListViewInternal(grid);
			}
		}
		
		#endregion



		//internal static T EmptyGridTable<T>(ItemsControl grid) where T : new()
		//	=> EmptyListItem<T>(grid);
		//internal static T EmptyListItem<T>(ItemsControl list) where T : new()
		//{
		//	T entry = new();
		//	SafeHelper.SafeInvoker(() => list.ItemsSource ??= new ObservableCollection<T>());
		//	return entry;
		//}
		internal static T EmptyListItem<T>(TableView grid) where T : new()
		{
			T entry = new();
			SafeHelper.SafeInvoker(() =>
			{
				grid.ItemsSource ??= GridMap.Map?.TryGetValue(grid, out IList? collection) is true ? collection as ObservableCollection<T> : [];
			});
			return entry;
		}
		//internal static void InitListViewPercentage(ListView list, ListDetail rows)
		//{
		//	SafeInvokeGen(rows, (row) =>
		//	{
		//		list.ItemsSource = null;
		//		List<ColumnHeader> offsets = [];
		//		int unUsedPercentage = 100;
		//		foreach (ColumnInfo item in row.SortColumnOrder)
		//		{
		//			if (item.Width == 0)
		//			{
		//				offsets.Add(list.Columns.Add(item.Name, item.Name));
		//			}
		//			else
		//			{
		//				int use = (int)(list.Size.Width * (item.Width / 100f));
		//				list.Columns.Add(item.Name, item.Name, use);
		//				unUsedPercentage -= item.Width;
		//			}
		//		}
		//		int totalItems = offsets.Count;
		//		int distNum = unUsedPercentage / totalItems;
		//		foreach (var column in offsets)
		//		{
		//			if (distNum <= unUsedPercentage)
		//			{
		//				column.Width = (int)(list.Size.Width * (distNum / 100f));
		//				unUsedPercentage -= distNum;
		//			}
		//			else
		//			{
		//				column.Width = (int)(list.Size.Width * (unUsedPercentage / 100f)); ;
		//				unUsedPercentage = 0;
		//			}
		//		}
		//		list.ListViewItemSorter = row.SortRowOrder.Sort;
		//	});
		//}
		//internal static T EmptyListItemInternal<T>(ListView list) where T : new()
		//	=> EmptyListItem<T>(list);
		internal static T EmptyListItemInternal<T>(TableView grid) where T : new()
			=> EmptyListItem<T>(grid);

		internal static void InitListView(ItemsControl control, ListDetail? rows)
			=> SafeHelper.SafeInvoker(() =>
			{
				try
				{
					if (control.ItemsSource is IList collect)
					{
						collect?.Clear();
					}
				}
				catch
				{
				}
			});
	}
}