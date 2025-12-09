// SearchOdoo.xaml.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel; // For ObservableCollection
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MessageBox = System.Windows.Forms.MessageBox;
using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.ClientUtils; // Assuming this is still valid
using HackPDM.Data;
using HackPDM.Extensions.General;
using HackPDM.Forms.Hack;
using HackPDM.Odoo; // Assuming this is still valid
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Src.ClientUtils.Types;
using HackPDM.Src.Extensions.General;
// --- WinUI 3 Namespaces ---
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;
using HackPDM.Extensions.Controls;
using HackPDM.Helper;
using WinRT; // For MessageDialog (basic replacement for MessageBox)

namespace HackPDM.Forms.Settings
{
	/// <summary>
	/// This is the converted WinUI 3 Page.
	/// It no longer inherits from System.Windows.Forms.Form
	/// </summary>
	public sealed partial class SearchOdoo : Page, ISingletonPage<SearchOdoo>
	{
		// NOTE: The SearchWidths and SearchPropWidths dictionaries are no longer needed.
		// All column widths are now defined directly in the XAML.
		
		private HackFileManager hackman;

		// --- WinForms Control Dependencies ---
		// NOTE: These variables are System.Windows.Forms controls.
		// If 'hackman' is also being migrated to WinUI 3, these should be
		// changed to Microsoft.UI.Xaml.Controls.TreeView and .ListView.
		private TreeView OdooDirectoryTree;
		private DataGrid OdooEntryList;

		private Window ParentWindow;
		// --- Data collections for WinUI 3 ListView Binding ---
		private ObservableCollection<SearchRow> SearchResultsList = new();
		private ObservableCollection<SearchPropertiesRow> PropertiesActive = new();
		private ObservableCollection<OperatorsRow> OperatorList = new();
		private ObservableCollection<SearchPropertiesRow> PropertiesSearchable = new();

		public SearchOdoo()
		{
			this.InitializeComponent();

			// Set the ItemsSource for the ListViews. This is the "WinUI 3 way".
			OdooSearchResults.ItemsSource = SearchResultsList;
			OdooSearchPropList.ItemsSource = PropertiesActive;

			SetPropertyDropdown();
			SetPropertyEqualDropdown();
		}

		// NOTE: The "in" parameter modifier is not valid for constructors in this context.
		// You may need to adjust how hackman is passed, or just remove "in".
		public SearchOdoo(HackFileManager hackman) : this()
		{
			SetHackInstance(hackman);
		}

		public void SetHackInstance(HackFileManager hackman)
		{
			this.hackman = hackman;
			this.OdooDirectoryTree = hackman.GetOdooDirectoryTree();
			this.OdooEntryList = hackman.GetOdooEntryList();
		}
		public void StoreWindowInstance(Window window)
		{
			ParentWindow = window;
		}

		private void SetPropertyDropdown()
		{
			foreach (var values in OdooDefaults.IdToProp)
			{
				PropertiesSearchable.Add(
					new SearchPropertiesRow()
					{
						Name = values.Value.name,
						ID = values.Key,
						IsTextOrDate = values.Value.prop_type == "text",
					}
				);
			}
			OdooSearchProperty.ItemsSource = PropertiesSearchable;
		}
		private void SetPropertyEqualDropdown()
		{
			OperatorsRow? defSearchCompare = null;
			OperatorsRow? defSearchProp = null;
			foreach (var op in Enum.GetValues<Operators>())
			{
				OperatorsRow opRow = new() { Operator = op };
				if (op == Operators.ILike) defSearchCompare = opRow;
				else if (op == Operators.Equal) defSearchProp = opRow;
				
				OperatorList.Add(opRow);
			}

			OdooSearchPropEqual.ItemsSource = OperatorList;
			OdooSearchComparer.ItemsSource = OperatorList;

			OdooSearchPropEqual.SelectedItem = defSearchProp;
			OdooSearchComparer.SelectedItem = defSearchCompare;
		}


		// --- Refactored Search Logic ---
		// BackgroundWorker is replaced with a simple async event handler.
		// This removes the need for all 'hackman.SafeInvoke' calls.
		private async void OdooSearch_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
		{
			string fileName = FileNameTextbox.Text;

			// NOTE: CheckBox.Checked is a nullable bool (bool?) in WinUI 3
			bool odooCheckedOutMe = OdooCheckedMe.IsChecked == true;
			bool deletedRemotely = OdooDeletedIsLocal.IsChecked == true;
			bool localOnly = OdooLocalOnly.IsChecked == true;

			// Get items from our ObservableCollection
			var propItems = PropertiesActive;

			if (!int.TryParse(OdooMaxRes.Text, out int maxResults))
			{
				await MessageBox.ShowAsync("Invalid max results value. Please enter a valid number.");
				return;
			}

			ArrayList execParams = [];
			ArrayList searchDomain = [];

			// No SafeInvoke needed - we are on the UI thread.
			string comparer = (OdooSearchComparer.SelectedItem as OperatorsRow)?.OpRepr ?? "=";

			if (fileName.Length > 0)
			{
				searchDomain.Add(new ArrayList { "name", comparer, fileName });
			}

			ArrayList fields = ["id", "name", "directory_complete_name"];

			if (odooCheckedOutMe && !localOnly)
			{
				searchDomain.Add(new ArrayList { "checkout_user", "=", OdooDefaults.OdooId });
			}

			if (deletedRemotely && !localOnly)
			{
				searchDomain.Add(new ArrayList { "deleted", "=", true });
			}

			ArrayList results;
			if (OdooSearchPropList.IsEnabled && propItems?.Count > 0)
			{
				ArrayList[] arrs = await CompilePropertyParams();
				ConcurrentSet<int> candidates = FilterCandidates(arrs);

				searchDomain.Add(new ArrayList { "id", "in", candidates.ToArrayList() });
			}

			execParams =
			[
				searchDomain,
				fields,
			];

			results = await OdooClient.BrowseAsync(HpEntry.GetHpModel(), execParams, 10000);

			// Clear previous results
			SearchResultsList.Clear();

			if (localOnly)
			{
				DisplayLocal(results, fileName, maxResults, false);
			}
			else if (deletedRemotely)
			{
				DisplayLocal(results, fileName, maxResults, true);
			}
			else
			{
				if (results.Count < 1)
				{
					await MessageBox.ShowAsync("No results found.");
					return;
				}
				DisplaySearch(results, maxResults);
			}

			// --- Sorting ---
			// We sort the collection itself, not the ListView control.
			var sortedList = SearchResultsList.OrderBy(item => item.Name, StringComparer.Ordinal).ToList();
			SearchResultsList.Clear();
			foreach (var item in sortedList)
			{
				SearchResultsList.Add(item);
			}

			await MessageBox.ShowAsync("Finished!");
		}
		private void DisplayLocal(ArrayList results, string filename, int limit = 100, bool isNotOnlyLocal = false)
		{
			const string Empty = "-";
			// NOTE: InitListViewPercentage is removed.

			DirectoryInfo directoryInfo = new DirectoryInfo(StorageBox.PwaPathAbsolute);
			FileInfo[] files = [.. directoryInfo.EnumerateFiles($"*{filename}*", SearchOption.AllDirectories)];

			Dictionary<string, List<string>> hts = GetNamePathwaysDict(results);
			int counter = 0;

			foreach (var file in files)
			{
				if (counter >= limit)
					break;

				string odooPath = HpDirectory.WindowsToOdooPath(file.DirectoryName[(StorageBox.PwaPathAbsolute.Length - StorageBox.PwaPathRelative.Length)..]);

				if (isNotOnlyLocal ^ !(hts.TryGetValue(file.Name.ToLower(), out List<string> paths) && paths.Contains(odooPath)))
				{
					counter++;
					// Add a new SearchResultItem to our bound collection
					SearchResultsList.Add(new SearchRow
					{
						Id = null,
						Name = file?.Name,
						Directory = file?.DirectoryName
					});
				}
			}
		}
		private void DisplaySearch(ArrayList list, int limit)
		{
			// NOTE: InitListViewPercentage is removed.
			Hashtable ht;
			int min = Math.Min(list.Count, limit);
			for (int i = 0; i < min; i++)
			{
				ht = (Hashtable)list[i];

				// Add a new SearchResultItem to our bound collection
				SearchResultsList.Add(new SearchRow
				{
					Id = ht["id"] as int?,
					Name = ht["name"].ToString(),
					Directory = ht["directory_complete_name"].ToString()
				});
			}
		}
		private Dictionary<string, List<string>> GetNamePathwaysDict(ArrayList result)
		{
			var dict = new Dictionary<string, List<string>>();
			foreach (Hashtable ht in result)
			{
				string name = ((string)ht["name"]).ToLower();
				string path = (string)ht["directory_complete_name"];
				if (dict.TryGetValue(name, out List<string> list))
				{
					list.Add(path);
				}
				else
				{
					dict.Add(name, [path]);
				}
			}
			return dict;
		}
		private async Task<ArrayList[]> CompilePropertyParams()
		{
			List<Task<ArrayList>> tasks = [];
			if (PropertiesActive?.Count > 0)
			{
				ArrayList fields = ["entry_id"];
				foreach (var item in PropertiesActive)
				{
					// Get the item from the collection

					Task<ArrayList> newTask = Task.Run(async () =>
					{
						ArrayList arr1 = [];

						// Use the data from our SearchPropertyItem
						arr1.Add(new ArrayList { "prop_id", "=", item.ID });
						arr1.Add(new ArrayList{
							"text_value",
							item.Comparer.OperatorConversion(),
							item.Value
						});
						return await OdooClient.BrowseAsync(HpVersionProperty.GetHpModel(), [arr1, fields], 10000);
					});
					// Awaiting here makes the loop sequential. 
					// If you want parallel execution, add the task to 'tasks' *without* awaiting,
					// then 'await Task.WhenAll(tasks)' at the end.
					await newTask;
					tasks.Add(newTask);
				}
			}
			if (tasks.Count > 0)
			{
				return await Task.WhenAll(tasks); // This will re-await, but it's fine.
			}
			return null;
		}
		private ConcurrentSet<int> FilterCandidates(ArrayList[] lists)
		{
			ConcurrentSet<int> candidates = [];

			for (int i = 0; i < lists.Length; i++)
			{
				IEnumerable<int> version_ids = lists[i].SelectNotDefault<Hashtable, int>(hash
					=> hash.TryGetValue<string, ArrayList>("entry_id", out var aList)
						? aList?[0] as int? ?? 0
						: 0);

				if (i == 0)
				{
					candidates = version_ids.ToConcurrentSet();
				}
				else
				{
					candidates.IntersectWith(version_ids);
				}
			}
			return candidates;
		}

		#region Form Events
		private void OdooCancel_Click(object sender, RoutedEventArgs e)
		{
			// 'this.Close()' does not exist on a Page.
			// You need to control navigation from the parent Frame or Window.
			// For example, if this Page is in a Frame:
			if (this.Frame.CanGoBack)
			{
				this.Frame.GoBack();
			}

			// Or, if it's the root of a secondary window, you might need
			// to get the AppWindow and destroy it.
		}
		private void OdooReset_Click(object sender, RoutedEventArgs e)
		{
			FileNameTextbox.Text = "";
			OdooSearchProperty.SelectedItem = null;
			OdooSearchPropValue.Text = "";
			OdooCheckedMe.IsChecked = false;
			OdooDeletedIsLocal.IsChecked = false;
			OdooLocalOnly.IsChecked = false;

			OdooMaxRes.Text = "100";

			// Clear the bound collections
			SearchResultsList.Clear();
			PropertiesActive.Clear();

			OdooSearchComparer.SelectedItem = null;
		}

		private async void OdooSearchResults_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
		{
			if (OdooSearchResults.SelectedItems.Count > 0)
			{
				FindSearchSelection((sender as DataGrid)?.SelectedItem as SearchRow);
				// ParentWindow?.Close();
			}
		}
		private async void FindSearchSelection(SearchRow? item)
		{
			if (item == null) return;

			// first select the treeview node
			// then select the listview item
			string directory = item.Directory!;
			string fileName = item.Name!;

			string[] paths = directory.Split([" / "], StringSplitOptions.None);

			IEnumerable<TreeViewNode>? nodes = null;
			TreeViewNode? node = null;
			try
			{
				for (int i = 0; i < paths.Length; i++)
				{
					if (i == 0) nodes = OdooDirectoryTree.RootNodes;
					else nodes = node?.Children;

					bool wasFound = false;
					if (nodes == null) throw new ArgumentException();
					foreach (var n in nodes!)
					{
						if (n.LinkedData.Name == paths[i])
						{
							wasFound = true;
							node = n;
							break;
						}
					}
					if (!wasFound) throw new ArgumentException();
				}
				OdooDirectoryTree.Collapse(node);
				foreach (var item1 in node?.Parent.Children ?? [])
				{
					OdooDirectoryTree.Collapse(item1);
				}
				hackman.LastSelectedNode = node;
				//hackman.lastSelectedNode.Expand();
				hackman.LastSelectedNode?.LinkedData.EnsureVisible(OdooDirectoryTree);
				OdooDirectoryTree.SelectedNode = hackman.LastSelectedNode;

				while (!hackman.IsListLoaded)
				{
					await Task.Delay(100);
				}
				EntryRow? entryItem = null;
				// string index = NameConfig.SearchName.Name;
				foreach (var entry in OdooEntryList.ItemsSource as ObservableCollection<EntryRow> ?? [])
				{
					if (entry.Name == fileName)
					{
						entryItem = entry;
						break;
					}
				}
				if (entryItem == null) throw new ArgumentException();

				OdooEntryList.SelectedItem = entryItem;
				OdooEntryList.Focus(FocusState.Programmatic);
				OdooEntryList.ScrollIntoView(entryItem, null);

			}
			catch
			{
				return;
			}
		}

		// WinUI 3 uses Checked/Unchecked events, or you can use one handler.
		private void OdooLocalOnly_CheckedChanged(object sender, RoutedEventArgs e) => ControlEnabler();
		private void OdooCheckedMe_CheckedChanged(object sender, RoutedEventArgs e) => ControlEnabler();
		private void OdooDeletedIsLocal_CheckedChanged(object sender, RoutedEventArgs e) => ControlEnabler();

		private async void OdooPropAdd_Click(object sender, RoutedEventArgs e)
		{
			if (OdooSearchProperty.SelectedItem == null || OdooSearchPropEqual.SelectedItem == null)
			{
				await MessageBox.ShowAsync("Add Property Name and Comparator");
				return;
			}

			// NOTE: InitListViewPercentage is removed.

			if (OdooSearchProperty.SelectedItem is not SearchPropertiesRow listItem) return;

			// Add a new SearchPropertyItem to our bound collection
			PropertiesActive.Add(listItem.Clone());
		}
		private void OdooPropertyReset_Click(object sender, RoutedEventArgs e)
		{
			PropertiesActive.Clear();
		}
		private void OdooPropDelete_Click(object sender, RoutedEventArgs e)
		{
			if (OdooSearchPropList.SelectedItems?.Count > 0)
			{
				// We must copy the items to a list before removing,
				// as you can't modify a collection while iterating it.

				var selectedItems = OdooSearchPropList.SelectedItems.As<IList>();
				for (int i = selectedItems.Count; i >= 0; i--)
				{
					var item = selectedItems[i] as SearchPropertiesRow;
					if (item is not null) PropertiesActive.Remove(item);
				}
			}
		}
		#endregion

		private void ControlEnabler()
		{
			// NOTE: Properties changed from 'Checked' to 'IsChecked == true'
			// and 'Enabled' to 'IsEnabled'

			if (OdooLocalOnly.IsChecked == true)
			{
				OdooCheckedMe.IsChecked = false;
				OdooCheckedMe.IsEnabled = false;

				OdooDeletedIsLocal.IsChecked = false;
				OdooDeletedIsLocal.IsEnabled = false;
				OdooSearchPropList.IsEnabled = false;

				OdooSearchComparer.SelectedItem = OdooSearchComparer.Items[0];
				OdooSearchComparer.IsEnabled = false;

				OdooSearchProperty.IsEnabled = false;
				OdooSearchPropEqual.IsEnabled = false;
				OdooSearchPropValue.IsEnabled = false;
				OdooPropAdd.IsEnabled = false;
				OdooPropDelete.IsEnabled = false;
				OdooPropertyReset.IsEnabled = false;
			}
			else
			{
				OdooCheckedMe.IsEnabled = true;
				OdooDeletedIsLocal.IsEnabled = true;
				OdooSearchPropList.IsEnabled = true;
				OdooSearchComparer.IsEnabled = true;
				OdooSearchProperty.IsEnabled = true;
				OdooSearchPropEqual.IsEnabled = true;
				OdooSearchPropValue.IsEnabled = true;
				OdooPropAdd.IsEnabled = true;
				OdooPropDelete.IsEnabled = true;
				OdooPropertyReset.IsEnabled = true;
			}

			if (OdooCheckedMe.IsChecked == true || OdooDeletedIsLocal.IsChecked == true)
			{
				OdooLocalOnly.IsChecked = false;
				OdooLocalOnly.IsEnabled = false;
			}
			else
			{
				OdooLocalOnly.IsEnabled = true;
			}
		}

		private async void CheckOutMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (OdooLocalOnly.IsChecked == true)
			{
				await MessageBox.ShowAsync("Can't checkout local only entries");
				return;
			}
			await CheckOutItems(OdooSearchResults.SelectedItems.Cast<SearchRow>());
		}
		private async void unCheckoutToolStripMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (OdooLocalOnly.IsChecked == true)
			{
				await MessageBox.ShowAsync("Can't uncheckout local only entries");
				return;
			}
			await CheckOutItems(OdooSearchResults.SelectedItems.Cast<SearchRow>(), false);
		}
		private async Task CheckOutItems(IEnumerable<SearchRow>? items, bool willCheckout = true)
		{
			if (items is null || (items.TryGetNonEnumeratedCount(out int count) ? count : items?.Count()) < 1) return;
			ArrayList ids = [];

			// 'items' is now a collection of SearchResultItem
			foreach (var item in items)
			{
				ids.Add(item.Id);
			}
			// get latest status
			WindowHelper.CreateWindowAndPage<StatusDialog>(out var Dialog, out _);
			HackFileManager.Dialog = Dialog;
			await hackman.GetLatestInternal(ids);
			//
			await hackman.CheckoutInternal(ids);
		}

		private async void openToolStripMenuItem_Click(object sender, RoutedEventArgs e)
		{
			// 'items' is now a collection of SearchResultItem
			var selectedItems = OdooSearchResults.SelectedItems.Cast<SearchRow>();

			if (OdooLocalOnly.IsChecked == true)
			{
				foreach (var item in selectedItems ?? [])
				{
					string path = item.Directory; // Assuming this is correct from original
					OpenLocalFile(path);
				}
			}
			else
			{
				foreach (var item in selectedItems ?? [])
				{
					await DownloadRemoteFile(item.Id ?? 0);
				}
			}

		}

		private void checkoutOpenToolStripMenuItem_Click(object sender, RoutedEventArgs e)
		{
			CheckOutMenuItem_Click(sender, e);
			openToolStripMenuItem_Click(sender, e);
		}

		private void OpenLocalFile(string path)
		{
			FileOperations.OpenFile(path);
		}
		private async Task DownloadRemoteFile(int entryID)
		{
			const string latest_version = "latest_version_id";
			HpVersion? version = (await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>([entryID], latest_version, excludedFields: ["preview_image"]))?.FirstOrDefault();

			if (version == null)
				return;

			// download version data and place into temporary folder
			version.DownloadFile();
			FileOperations.OpenFile(Path.Combine(version.WinPathway, version.name));
		}
		private async Task PreviewRemoteFile(int entryID)
		{
			const string latest_version = "latest_version_id";
			HpVersion? version = (await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>([entryID], latest_version, excludedFields: ["preview_image"]))?.FirstOrDefault();

			if (version == null)
				return;

			// download version data and place into temporary folder
			version.DownloadFile(StorageBox.TemporaryPath);
			FileOperations.OpenFile(Path.Combine(version.WinPathway, version.name));
		}
	}
}