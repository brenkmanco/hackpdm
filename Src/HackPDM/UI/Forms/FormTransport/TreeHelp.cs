using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.FormTransport;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Compatibility;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Hack;
using HackPDM.UI.Forms.Helper;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage.Streams;

using static System.Runtime.InteropServices.JavaScript.JSType;
using static HackPDM.UI.Controls.UISettings;

using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;
using EntryRow = HackPDM.UI.Types.EntryRow;
using Image = Microsoft.UI.Xaml.Controls.Image;
using String = System.String;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.UI.Forms.FormTransport
{
	public class TreeHelp
	{
		private HackFileManager _HFM { get; set; }
		private TreeView _tree { get; set; }
		private DataGrid _grid { get; set; }
		private static AssetsImageProvider? AssetsProvider { get => field ??= ImageProvider as AssetsImageProvider; }
		
		public TreeHelp() { }

		public void InjectHFM(HackFileManager hackFM)
		{
			_HFM = hackFM;
			_tree = hackFM.GetOdooDirectoryTree();
			_grid = hackFM.GetOdooEntryList();
		}
		#region TreeView functions
		// tree view directories
		public async Task CreateTreeViewBackground(TreeView tree)
		{
			await LoadOdooDirectoryTree(tree);
		}

		private async Task LoadOdooDirectoryTree(TreeView tree)
		{
			_HFM.IsTreeLoaded = false;
			var (img, ring) = _HFM.GetVisualizer();
			LoadingVisualized(img, ring);

			try
			{
				await SafeHelper.SafeInvokerAsync (async () =>
				{
					await CreateTreeHash(tree, OdooDefaults.Instance.HpDirectoryRoot as HpDirectory);

					await CreateLocalTree(tree);

					if (_HFM.LastSelectedNode != null)
					{
						_HFM.LastSelectedNode = tree.FindTreeNode(_HFM.LastSelectedNodePath)?.Node;
					}

					var tData = tree.RootNodes;
					foreach (var n in tData)
					{
						n.LinkedData.SortTree();
					}

					_HFM.LastSelectedNode?.LinkedData.EnsureVisible(tree);
										
				});
				_HFM.IsTreeLoaded = true;
			}
			catch (Exception exception)
			{
				Debug.WriteLine(exception.Message);
			}
			ResetImagePreview(img, ring);
		}
		internal void ResetImagePreview(Image img, ProgressRing ring)
		{
			_HFM.DispatcherQueue.TryEnqueue(() =>
			{
				ring.Width = 0;
				ring.Height = 0;
				ring.IsActive = false;
				
				var sp = img.FindParent<StackPanel>();
				img.Width = sp?.Width ?? 0;
				img.Height = sp?.Height ?? 0;
			});
		}
		internal void LoadingVisualized(Image img, ProgressRing ring)
		{
			_HFM.DispatcherQueue.TryEnqueue(async () =>
			{
				img.Width		= 0;
				img.Height		= 0;
				var sp = img.FindParent<StackPanel>();
				ring.Width		= sp?.Width ?? 0;
				ring.Height		= sp?.Height ?? 0;
				ring.IsActive	= true;
			});
		}
		// to infrastructure
		internal async Task<(Hashtable? entries, Dictionary<string, Task<HackFile>>? hackmap)> GetHackAndEntry(int? directoryId)
		{
			if (directoryId is null) return (null, null);
			Hashtable entries = await Task.Run(() => HpDirectory.GetEntries(directoryId, _HFM.IsActive));
			Dictionary<string, Task<HackFile>> hackFileMap = await GridHelp.GetFileMap(entries);
			return (entries, hackFileMap);
		}
		internal async Task TreeSelectItem(TreeView tree, TreeViewNode node, DataGrid grid, CancellationToken token = default)
		{
			_HFM.IsListLoaded = false;
			await AsyncHelper.WaitUntil(() => _HFM.IsTreeLoaded, 100, -1, token);
			node.LinkedData.EnsureVisible(tree);
			GridHelp.InitGridView(grid);

			try
			{
				if (node?.Content is TreeData tData)
				{
					if (tData?.DirectoryId is null or 0)
					{
						// add file entries to folder
						AddLocalEntries(grid, node);
						return;
					}

					token.ThrowIfCancellationRequested();
					(Hashtable? entries, Dictionary<string, Task<HackFile>>? hackmap) = await GetHackAndEntry(tData.DirectoryId);
					token.ThrowIfCancellationRequested();

					AddRemoteEntries(grid, entries, hackmap);
					ListView items = new();

					AddLocalEntries(grid, _HFM.LastSelectedNode, hackmap);

					_HFM.DispatcherQueue.TryEnqueue(() =>
					{
						_HFM.OEntries.Sort((x, y) => String.CompareOrdinal(x.Name, y.Name));
						_HFM.IsListLoaded = true;
						//_grid.InvalidateArrange();
						_grid.UpdateLayout();
					});
				}
			}
			catch { }
		}
		internal async Task<bool> TreeItemsChangedPolling(int timeout = -1, CancellationToken token = default)
		{
			while (!token.IsCancellationRequested && _HFM.EntryPollingMs > 0)
			{
				bool isLoaded = await AsyncHelper.WaitUntil(() => _HFM.IsTreeLoaded && _HFM.IsListLoaded, 1000, -1, token);

				if (!isLoaded || _HFM.LastSelectedNode is null) continue;

				await Task.Delay(_HFM.EntryPollingMs, token);
			}
			return false;
		}
		internal static async Task CreateTreeHash(TreeView tree, HpDirectory directoryModel)
		{
			await AddDirectoriesToTree(tree, directoryModel.GetSubdirectories(false));
		}
		internal static async Task CreateLocalTree(TreeView treeView)
		{
			Dictionary<string, TreeViewNode>? vNodes = new();//FormHelper.ConvertTreeToDictionary(treeView);
			IEnumerable<string> pathways = Directory.EnumerateDirectories(HackDefaults.Instance.PwaPathAbsolute, "*", SearchOption.AllDirectories);
			pathways = Help.FastSlice(pathways, HackDefaults.Instance.PwaPathAbsolute.Length, prependText: "root");

			foreach (string pathway in pathways)
			{
				string[] paths = pathway.Split('\\');
				await AddLocalDirectoriesTest(treeView, paths, vNodes);
				//(int, TreeViewNode?) validIndexNode = FormHelper.LastValidTreeIndex(in pathway, in paths, treeDict);
				//// the last valid index does not go to the end meaning it didn't find the
				//// remaining paths
				//if (validIndexNode.Item1 != paths.Length - 1)
				//{
				//	AddLocalDirectories(treeView, validIndexNode.Item2, paths[(validIndexNode.Item1 + 1) .. ], treeDict);
				//}
			}
			Debug.WriteLine("stop");
		}
		internal async static Task AddLocalDirectoriesTest(TreeView tree, string[] paths, Dictionary<string, TreeViewNode> vNodes)
		{
			TreeViewNode? node = null;
			IList<TreeViewNode> nodeList;
			bool IsFound;
			StringBuilder pathUpTo = new();
			for (int i = 0; i < paths.Length; i++)
			{
				IsFound = false;
				string pathway = paths[i];

				if (i == 0)
				{
					nodeList = tree.RootNodes;
					pathUpTo.Append(pathway);
				}
				else 
				{
					nodeList = node?.Children ?? [];
					pathUpTo.Append(@$"\{pathway}");
				}

				if (vNodes.TryGetValue(pathUpTo.ToString(), out TreeViewNode? foundNode))
				{
					IsFound = true;
					node = foundNode;
				}
				foreach (TreeViewNode evalNode in nodeList)
				{
					var data = evalNode?.Content<TreeData>();
					if (data?.Name == pathway) 
					{ 
						IsFound = true; 
						node = evalNode;
						break; 
					}
				}

				if (!IsFound)
				{
					if ((node ??= tree.RootNodes?[0]) == null)
					{
						node = new();
						tree.RootNodes?.Add(node);
					}
					await AddLocalDirectory(node, pathway, vNodes);
				}
			}
		}
		internal async static Task AddLocalDirectory(TreeViewNode? parent, string namePath, Dictionary<string, TreeViewNode> vNodes)
		{
			await SafeHelper.SafeInvokerAsync(async () =>
			{
				TreeViewNode tNode = new();
				var newNodeData = tNode.LinkedData;
				newNodeData.Name = namePath;
				newNodeData.Icon = await (AssetsProvider?.GetImageAsync("fo_loc"));
				newNodeData.DirectoryId = 0;

				parent?.Children.Add(tNode);
				if (!string.IsNullOrEmpty(newNodeData.FullPath)) vNodes.TryAdd(newNodeData.FullPath, tNode);
			});
		}
		internal async static void AddLocalDirectories(TreeView tree, TreeViewNode node, string[] pathway, Dictionary<string, TreeViewNode> treeDict)
		{
			await SafeHelper.SafeInvokerAsync(async () =>
			{
				for (int i = 0; i < pathway.Length; i++)
				{
					var parentData = node?.Content as TreeData;
					TreeViewNode tNode = new();
					var newNode = tNode.LinkedData;
					newNode.Name = pathway[i];
					newNode.Icon = await (AssetsProvider?.GetImageAsync("fo_loc"));
					newNode.DirectoryId = 0;

					node?.Children.Add(tNode);
					treeDict.TryAdd(newNode?.FullPath ?? "", tNode);
					//treeDict.Add(parentData?.FullPath ?? "", node);
				}
			});
		}
		internal static async Task AddDirectoriesToTree(TreeView tree, Hashtable entries) 
			=> await SafeHelper.SafeInvokerAsync(
				async () =>
				{
					tree.RootNodes.Clear();
					await AddNodesMinimalMemoryAsync(tree, entries);
				});

		internal static async Task AddNodesMinimalMemoryAsync(TreeView tree, Hashtable rootNode)
		{
			var remoteImage = await AssetsProvider?.GetImageAsync("fo_serv");
			var defaultImage = await AssetsProvider?.GetImageAsync("def_fo");

			var queue = new Queue<(TreeViewNode? parent, Hashtable node)>();
			queue.Enqueue((null, rootNode));

			while (queue.Count > 0)
			{
				var (parent, node) = queue.Dequeue();

				var myNode = new TreeViewNode();
				if (parent is null)
					tree.RootNodes.Add(myNode);
				else
					parent.Children.Add(myNode);

				var dat = myNode.LinkedData;
				dat.Name = node["name"] as string;
				dat.DirectoryId = node["id"] as int?;

				string? fullPath = dat.FullPath ?? dat.Name;
				string path = HackDefaults.DefaultPath(fullPath ?? "root", true);

				if (!Directory.Exists(path))
					dat.Icon = remoteImage;
				else
					dat.Icon = defaultImage;

					myNode.LinkedData = dat;

				// Push children to stack (LIFO order — reverse if you want original order preserved)
				if (node["directories"] is Hashtable { Count: > 0 } directory)
				{
					foreach (DictionaryEntry pair in directory)
					{
						if (pair.Value is Hashtable childDirectory)
							queue.Enqueue((myNode, childDirectory));
					}
				}
			}
		}
		public static void RefreshTree(TreeView tree)
			=> SafeHelper.SafeInvoker(tree.UpdateLayout);
		public async Task RestartTree(TreeView tree)
			=> await CreateTreeViewBackground(tree);
		public void RestartEntries(TreeView tree, DataGrid grid)
		{
			if (_HFM.LastSelectedNode is null) return;
			
			SafeHelper.SafeInvoker(async () =>
			{
				await TreeSelectItem(tree, _HFM.LastSelectedNode!, grid);
				await AsyncHelper.WaitUntil(() => _HFM.IsListLoaded);
				if (grid.SelectedItems is not null and { Count: > 0 } items)
				{
					var entry = items[0] as EntryRow;
					if (entry is not null) grid.ScrollIntoView(entry, grid.Columns.First());
				}
				grid.UpdateLayout();
			});
		}
		#endregion
		#region Tree Item Selection
		private static void FindUpdatedEntries(DataGrid grid, TreeViewNode node, Hashtable entries, Dictionary<string, Task<HackFile>> hackFileMap)
		{
			// things to check for:
			// 1. if the entry is in the entries hashtable
			// 2. if the entry is in the hackFileMap
			// 3. if the entry is not in the entries hashtable but is in the hackFileMap
			// 4. if the entry is not in the hackFileMap but is in the entries hashtable
			// 5. if the entry is not in either the entries hashtable or the hackFileMap
			// 6. if the entry is in both the entries hashtable and the hackFileMap but has been modified locally
			// 7. if the entry is in both the entries hashtable and the hackFileMap but has been modified remotely

			HackFile[]? files = GetHackNonEntries(node, hackFileMap, out int lRC);
			ObservableCollection<EntryRow>? items = grid.ItemsSource as ObservableCollection<EntryRow>;
			if (items is null) return;

			foreach (EntryRow item in items)
			{
				// this means that the item is not in the entries hashtable
				if (item.Id is null or 0)
				{
				}
				else
				{
					//Hashtable entry = entries.TakeWhere(e => e.Value is Hashtable ht && (ht["id"] as int?) == item.ID);
				}
			}

		}
		private static HackFile[]? GetHackNonEntries(TreeViewNode? node, Dictionary<string, Task<HackFile>>? hackFileMap, out int localAndRemoteCount)
		{
			localAndRemoteCount = 0;
			if (node is null) return null;
			string path = HackDefaults.DefaultPath((node.Content as TreeData)?.FullPath, true);
			if (!Directory.Exists(path)) return null;

			HackFile[]? files;

			bool hasEntries = hackFileMap != null;
			if (hasEntries) files = FileOperations.FilesInDirectory(path, hackFileMap, out localAndRemoteCount); //, out Dictionary<string, Hashtable> conflictPaths);
			else files = FileOperations.FilesInDirectory(path);
			return files;
		}
		internal async void AddRemoteEntries(DataGrid grid, Hashtable entries, Dictionary<string, Task<HackFile>> hackFileMap)
		{
#if DEBUG
			_HFM.TimerStopwatch = Stopwatch.StartNew();
#endif
			_HFM.GetEntriesLabel().Text = $"Entries: {(hackFileMap.Count == 0 ? "None" : hackFileMap.Count)}";
			foreach (DictionaryEntry pair in entries)
			{
				await AddRemoteEntry(grid, pair, hackFileMap);
			}
#if DEBUG
			_HFM.TimerStopwatch.Stop();
			Console.WriteLine($"remote entries time: {_HFM.TimerStopwatch.Elapsed}");
#endif
		}

		private static async Task AddRemoteEntry(DataGrid grid, DictionaryEntry pair, Dictionary<string, Task<HackFile>> hackFileMap)
		{
			if (pair.Value is not Hashtable table) return;

			//ListViewItem item = EmptyListItemInternal(OdooEntryList);

			EntryRow item = new()
			{
				Id = table["id"] as int?,
				//item.SubItems.Add(((int)table["id"]).ToString());
				Name = pair.Key.ToString(),
				Type = table["type"] is string ttypeString ? ttypeString : null,
				//double size = (double)( Convert.ToDouble(table["size"]) * HackDefaults.ByteSizeMultiplier );
				Size = Convert.ToInt64(table["size"]),
				Checkout = table["checkout"] is int c and not 0 ? OdooDefaults.Instance.IdToUser.GetValueOrDefault(c) : null, 
			};
			

			// check if latest checksum
			//string status = "";
			string? fullName = table["fullname"] as string;
			HackFile? hack = null;
			if (!string.IsNullOrWhiteSpace(fullName)) hack = hackFileMap[fullName]?.Result;
			item.ReprType = hack?.Exists is true
				? EntryReprType.Both
				: EntryReprType.Remote;
			item.LatestReleaseId = table["release"] as int?;

			//string latest = EmptyPlaceholder;
			item.LatestId = table["latest"] as int?;
			string datePlace = table["latest_date"] is not string latest ? null : latest;

			item.RemoteDate = DateTime.TryParse(datePlace, out DateTime remoteDate) && remoteDate != default ? remoteDate : null;
			// 2006-12-15 01:43:49.623

			item.LocalDate = hack?.ModifiedDate.Year is null or 1 ? null : hack?.ModifiedDate;

			// remote only // local only // new remote version
			// checked out to me  // checked out to other // ignore filter
			// no remote file type // local modification // deleted
			// destroyed
			
			FileStatus status = table["deleted"] is bool deleted && !deleted
				? item.Checkout?.Id is not null or 0
					? item.Checkout?.Id == OdooDefaults.Instance.OdooId ? FileStatus.Cm : FileStatus.Co
					: table["latest_checksum"] switch
					{
						bool => FileStatus.Lo,
						string latestChecksum => hack?.Checksum switch
						{
							null => FileStatus.Ro,
							_ when hack.Checksum == latestChecksum => FileStatus.Ok,
							_ => remoteDate > hack.ModifiedDate ? FileStatus.Nv : FileStatus.Lm
						},
						_ => FileStatus.Lo
					}
				: FileStatus.Dt;
			item.Status = status;

			ImageSource? image = await AssetsProvider?.GetImageAsync(item.Type ?? "lo")
					?? (await GetRemoteImage(item.Type))
					?? await AssetsProvider?.GetImageAsync("def_fi");

			ImageSource? statImg = await AssetsProvider?.GetImageAsync(Enum.GetName(status) ?? "lo");

			item.Icon = image;
			item.StatusIcon = statImg;

			if (table["category"] is string category) item.Category = OdooDefaults.Instance.HpCategories?.FirstOrDefault(c => c.name?.Equals(category) is true);

			item.FullName = fullName;
			await GridHelp.UpdateListAsync(grid, item);
			
		}

		internal static async Task<ImageSource?> GetRemoteImage(string? name)
		{
			BitmapImage? imgExt = null;
			if (string.IsNullOrEmpty(name)) return imgExt;
			
			if (OdooDefaults.Instance.ExtToType.TryGetValue($".{name}", out var hpType))
			{
				// get remote image
				imgExt = new();
				using var stream = new InMemoryRandomAccessStream();
				using var writer = new DataWriter(stream);
				try
				{
					if (hpType.icon is null) return null;
					byte[] imgBytes = FileOperations.ConvertFromBase64(hpType.icon);
					writer.WriteBytes(imgBytes);
					await writer.StoreAsync();
					await writer.FlushAsync();
					writer.DetachStream();

					stream.Seek(0);
					await imgExt.SetSourceAsync(stream);
				}
				catch
				{
					return null;
				}
			}

			return imgExt;
		}
		
		internal async void AddLocalEntries(DataGrid grid, TreeViewNode? node, Dictionary<string, Task<HackFile>>? hackFileMap = null)
		{
#if DEBUG
			_HFM.TimerStopwatch = Stopwatch.StartNew();
#endif
			HackFile[]? files = GetHackNonEntries(node, hackFileMap, out int lRC);
			int totalFiles = hackFileMap?.Count ?? 0;
			int localOnly = files?.Length ?? 0;
			int remoteOnly = totalFiles - lRC;

			_HFM.GetEntriesRemoteLabel().Text = $"Remote Only: {(remoteOnly == 0 ? "None" : remoteOnly)}";
			switch (files)
			{
				case null:
				{
					_HFM.GetEntriesLocalLabel().Text = "Local Only: None";
					if (totalFiles == 0)
					{
						_HFM.GetEntriesLabel().Text = $"Entries: {(remoteOnly == 0 ? "None" : remoteOnly)}";
					}
					return;
				}
				case { Length: < 1 }: 
				{
					goto case null;
				}
				default:
				{
					_HFM.GetEntriesLabel().Text = $"Entries: {totalFiles+localOnly}";
					_HFM.GetEntriesLocalLabel().Text = $"Local Only: {files.Length}";
					break;
				}
			}

			foreach (HackFile file in files)
			{
				await AddLocalEntry(grid, node, file);
			}

#if DEBUG
			_HFM.TimerStopwatch.Stop();
			Console.WriteLine($"local entries time: {_HFM.TimerStopwatch.Elapsed}");
#endif
		}
		private async Task AddLocalEntry(DataGrid grid, TreeViewNode node, HackFile file)
		{
			if (file is null) return;
			string type = file.TypeExt.ToLower();
			string status = "lo";



			if ((OdooDefaults.Instance.RestrictTypes is true || _HFM.IsFiltered) & !OdooDefaults.Instance.ExtToType.TryGetValue(type, out var hpType))
			{
				status = "ft";
			}
			if (OdooDefaults.Instance.RestrictTypes is true & OdooDefaults.Instance.ExtToFilter.TryGetValue(type, out var filterType))
			{
				status = "if";
			}

			type = type[1..];
			// get or add image key
			ImageSource? image = await AssetsProvider?.GetImageAsync(type)
					?? (await GetRemoteImage(type))
					?? await AssetsProvider?.GetImageAsync("def_fi");

			ImageSource? statImg = await AssetsProvider?.GetImageAsync(status);
			IHpCategoryModel? nameCategory = OdooDefaults.Instance.ExtToCat.GetValueOrDefault($".{type}");

			EntryRow item = new()
			{
				Id = null,
				Name = file.Name,
				Type = type,
				Size = file.FileSize,
				LocalDate = file.ModifiedDate,
				RemoteDate = null,
				Status = Enum.Parse<FileStatus>(status, true),
				Icon = image,
				StatusIcon = statImg,
				Checkout = null,
				Category = nameCategory,
				FullName = file.FullPath,
				ReprType = EntryReprType.Local,
			};

			await GridHelp.UpdateListAsync(grid, item);
		}

		#endregion
	}
}
