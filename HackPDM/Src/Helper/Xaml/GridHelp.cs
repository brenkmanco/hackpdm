using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.WinUI.UI.Controls;

using HackPDM.Data;
using HackPDM.Extensions.Controls;
using HackPDM.Extensions.General;
using HackPDM.Extensions.Odoo;
using HackPDM.Forms.Hack;
using HackPDM.Hack;
using HackPDM.Odoo;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Properties;
using HackPDM.Src.ClientUtils.Types;

using Microsoft.UI.Xaml.Controls;
using OClient = HackPDM.Odoo.OdooClient;

using Windows.Media.Protection.PlayReady;

namespace HackPDM.Src.Helper.Xaml
{
	internal class GridHelp
	{
		private HackFileManager _HFM { get; init; }
		internal GridHelp(HackFileManager hackFM)
		{
			_HFM = hackFM;
		}


		#region ListView / DataGrid Functions
		public static void InitGridView(DataGrid grid) => InitListViewInternal(grid);
		private static void InitListViewInternal(DataGrid grid)
		{
			var collect = grid.ItemsSource as IList;
			collect?.Clear();
		}
		private static void InitListViewInternal(ListView list, ListDetail rows)
			=> InitListView(list, rows);

		public static void ResetListViews(IEnumerable<DataGrid> grids)
		{
			foreach (var grid in grids)
			{
				InitListViewInternal(grid);
			}
		}
		
		#endregion

		#region List Item Selection
		internal async Task<HpVersion[]?> ProcessHistorySelectAsync(DataGrid grid, EntryRow? entry, CancellationToken token, bool listVersions = true)
		{
			HpVersion[]? versions = null;
			if (entry is null) return null;
			if (entry.Id != null)
			{
				versions = await GetVersionsForEntryAsync(entry.Id ?? 0, ["preview_image", "file_contents"], insertedFields: ["create_uid"]);
			}
			if (!listVersions) return versions;

			token.ThrowIfCancellationRequested();
			await SafeHelper.SafeInvokerAsync(() => PopulateHistory(grid, versions ?? []));
			return versions;
		}
		internal async Task<List<HpVersionProperty[]?>?> ProcessPropertiesSelectAsync(DataGrid grid, EntryRow? entry, CancellationToken token, bool listProperties = true)
		{
			if (entry is null) return null;
			if (entry.Id == null) return null;

			HpVersion[]? versions = await GetVersionsForEntryAsync(entry.Id ?? 0, ["preview_image", "file_contents"], insertedFields: ["create_uid"]);
			token.ThrowIfCancellationRequested();
			return await ProcessPropertiesSelectInternalAsync(grid, versions, token, listProperties);
		}
		private async Task<List<HpVersionProperty[]?>> ProcessPropertiesSelectInternalAsync(DataGrid grid, HpVersion[]? versions, CancellationToken token, bool listProperties = true)
		{
			List<HpVersionProperty[]>? versionProperties = null;
			if (versions != null && versions.Length > 0)
			{
				versionProperties = await HpVersion.GetAllVersionPropertiesAsync(versions.ToArrayListIDs());
			}
			if (!listProperties || (versionProperties is null or { Count: < 1 })) return versionProperties;
			token.ThrowIfCancellationRequested();
			await SafeHelper.SafeInvokerAsync(() => PopulateProperties(grid, versionProperties ?? []));
			return versionProperties;
		}
		internal async Task<HpVersion[]?> ProcessParentSelectAsync(DataGrid grid, EntryRow? entry, CancellationToken token, bool listParents = true)
		{
			HpVersion[]? parentVersions = null;
			int? versionId;

			switch (entry)
			{
				case null: return null;

				case { LatestId: not (null or 0) }:
					{
						versionId = entry!.LatestId;
						break;
					}
				case { Id: not (null or 0) }:
					{
						versionId = await HpEntry.GetFieldValueAsync<int>(entry.Id ?? 0, "latest_version_id");
						break;
					}

				default: return null;
			}


			if (versionId != null)
			{
				parentVersions =
					await HpVersionRelationship.GetRelatedRecordsBySearchAsync<HpVersion>([new ArrayList()
					{
						"child_id", "=", versionId
					}], "parent_id",
						excludedFields: ["preview_image", "node_id", "entry_id", "file_modify_stamp", "checksum", "file_contents"],
						insertFields: ["directory_complete_name"]
					);
			}
			if (!listParents || (parentVersions is null or { Length: < 1 })) return parentVersions;
			token.ThrowIfCancellationRequested();
			await SafeHelper.SafeInvokerAsync(() => PopulateParent(grid, parentVersions ?? []));
			return parentVersions;
		}
		internal async Task<HpVersion[]?> ProcessChildSelectAsync(DataGrid grid, EntryRow? entry, CancellationToken token, bool listChildren = true)
		{
			HpVersion[]? childVersions = null;
			if (entry is null) return null;
			if (entry.Id == null) return null;

			int versionId = await HpEntry.GetLatestIDAsync(entry.Id ?? 0);
			if (versionId == 0) return null;

			childVersions =
				await HpVersionRelationship.GetRelatedRecordsBySearchAsync<HpVersion>([new ArrayList()
				{
					"parent_id", "=", versionId
				}], "child_id",
					excludedFields: ["preview_image", "node_id", "entry_id", "file_modify_stamp", "checksum", "file_contents"],
					insertFields: ["directory_complete_name"]
				);

			if (!listChildren || (childVersions is null or { Length: < 1 })) return childVersions;
			token.ThrowIfCancellationRequested();
			await SafeHelper.SafeInvokerAsync(() => PopulateChildren(grid, childVersions ?? []));
			return childVersions;
		}
		internal async Task<HpVersion?> ProcessInfoSelectAsync(DataGrid grid, EntryRow? entry, CancellationToken token, bool listVersionInfo = true)
		{
			HpVersion? versionInfo = null;
			if (entry is null) return null;
			if (entry.Id == null) return null;

			versionInfo = entry?.LatestId is null
				? (await HpEntry.GetRelatedRecordByIdsAsync<HpVersion>(
					[entry?.Id ?? 0],
					"latest_version_id",
					excludedFields: ["preview_image", "file_contents"]))?.First()
				: HpVersion.GetRecordById(entry!.LatestId ?? 0, ["preview_image", "file_contents"]);

			if (!listVersionInfo || versionInfo is null) return versionInfo;
			token.ThrowIfCancellationRequested();
			await SafeHelper.SafeInvokerAsync(() => PopulateVersionInfo(grid, versionInfo));
			return versionInfo;
		}
		
		internal static async Task UpdateListAsync<T>(DataGrid list, T item)
		{
			await Task.Yield();
			SafeHelper.SafeInvoker(() => list.ItemAdd(item));
		}
		internal HpVersion[]? GetVersionsForEntry(int entryId, string[]? excludedFields = null, string[]? insertedFields = null)
			=> GetVersionsForEntryAsync(entryId, excludedFields, insertedFields).GetAwaiter().GetResult();
		internal async Task<HpVersion[]?> GetVersionsForEntryAsync(int entryId, string[]? excludedFields = null, string[]? insertedFields = null)
		{
			HpVersion[] versions = [];
			ArrayList ids = [entryId];
			ArrayList al = await OClient.ReadAsync(HpEntry.GetHpModel(), ids, ["version_ids"], 10000);
			if (al != null && al.Count > 0)
			{
				if (al[0] is not Hashtable ht) return null;
				if (ht?["version_ids"] is not ArrayList result) return null;
				excludedFields ??= ["preview_image", "file_contents"];
				versions = await HpVersion.GetRecordsByIdsAsync(result, excludedFields: excludedFields, insertFields: insertedFields);
			}
			return versions;
		}
		internal async Task<HpRelease[]?> GetReleaseForEntryAsync(int entryId, string[]? excludedFields = null, string[]? insertedFields = null)
		{
			HpRelease[] releases = [];
			ArrayList ids = [entryId];
			ArrayList al = await OClient.ReadAsync(HpEntry.GetHpModel(), ids, ["release_ids"], 10000);
			if (al != null && al.Count > 0)
			{
				if (al[0] is not Hashtable ht) return null;
				if (ht?["release_ids"] is not ArrayList result) return null;
				// excludedFields ??= ["preview_image", "file_contents"];
				releases = await HpRelease.GetRecordsByIdsAsync(result, excludedFields: excludedFields, insertFields: insertedFields);
			}
			return releases;
		}
		private void PopulateProperties(DataGrid grid, in List<HpVersionProperty[]> allProperties)
		{
			//"Version", 50
			//"Configuration", 100
			//"Property", 140
			//"Value", 400
			//"Type", 140
			object lockObject = new();
			lock (lockObject)
			{

				if (allProperties == null) return;
				SafeHelper.SafeInvokeGen(allProperties, (allp) =>
				{
					InitListViewInternal(grid);
					foreach (HpVersionProperty[] versionProperties in allp)
					{
						if (versionProperties == null || versionProperties.Length == 0) continue;

						foreach (HpVersionProperty versionProp in versionProperties)
						{
							if (versionProp == null || versionProp.Id == 0) continue;

							var item = EmptyListItemInternal<PropertiesRow>(grid);

							item.Version = versionProp.version_id;
							item.Configuration = versionProp.sw_config_name;
							item.Name = versionProp.prop_name;
							item.Property = versionProp.prop_id;

							item.Type = versionProp.GetValueType();
							item.ValueData = item.Type switch
							{
								HpVersionProperty.PropertyType.Yesno => versionProp.yesno_value,
								HpVersionProperty.PropertyType.Number => versionProp.number_value,
								HpVersionProperty.PropertyType.Text => versionProp.text_value,
								HpVersionProperty.PropertyType.Date => versionProp.date_value,
								_ => null,
							};

							grid.ItemAdd(item);
						}
					}
				});
			}
		}
		private void PopulateChildren(DataGrid grid, in HpVersion[] versions)
		{
			// "Version", 50
			// "Name", 600
			object lockObject = new();
			lock (lockObject)
			{

				if (versions == null) return;
				SafeHelper.SafeInvokeGen(versions, (v) =>
				{
					InitListViewInternal(grid);
					foreach (HpVersion version in v)
					{
						var item = EmptyListItemInternal<ChildrenRow>(grid);
						item.Version = version.Id;
						item.Name = version.name;
						item.BasePath = Path.Combine(/*HackDefaults.PWAPathAbsolute,*/ version.WinPathway);
						grid.ItemAdd(item);
					}
				});
			}
		}
		private void PopulateParent(DataGrid grid, in HpVersion[] versions)
		{
			// "Version", 50
			// "Name", 600
			object lockObject = new();
			lock (lockObject)
			{

				if (versions == null) return;
				SafeHelper.SafeInvokeGen(versions, (v) =>
				{
					InitListViewInternal(grid);
					foreach (HpVersion version in v)
					{
						var item = EmptyListItemInternal<ParentRow>(grid);
						item.Version = version.Id;
						item.Name = version.name;
						item.BasePath = version.WinPathway;

						grid.ItemAdd(item);
					}
				});
			}
		}
		private void PopulateHistory(DataGrid grid, in HpVersion[] versions)
		{
			// "Version", 50
			// "ModUser", 140
			// "ModDate", 140
			// "Size", 75
			// "RelDate", 75
			object lockObject = new();
			lock (lockObject)
			{

				if (versions == null) return;
				SafeHelper.SafeInvokeGen(versions, (v) =>
				{
					InitListViewInternal(grid);
					foreach (HpVersion version in v)
					{
						var item = EmptyListItemInternal<HistoryRow>(grid);

						item.Version = version.Id;
						int? moduser = null;
						if (version.HashedValues.TryGetValue("create_uid", out ArrayList? obj))
						{
							if (obj is not null)
							{
								moduser = obj[0] as int?;
							}
						}
						if (moduser is not null) item.ModUser = OdooDefaults.IdToUser.TryGetValue(moduser ?? 0, out var user) ? user : null;
						item.ModDate = version.file_modify_stamp;
						item.Size = version.file_size;
						item.RelDate = null;

						grid.ItemAdd(item);
					}
				});
			}
		}
		private void PopulateVersionInfo(DataGrid grid, HpVersion version)
		{
			// int CheckoutColumnIndex = OdooEntryList.Columns["Checkout"].Index;
			// item.SubItems [ CheckoutColumnIndex ].Text == ""
			object lockObject = new();
			lock (lockObject)
			{


				if (version == null) return;
				SafeHelper.SafeInvokeGen(version, (v) =>
				{
					InitListViewInternal(grid);
					var item = EmptyListItemInternal<VersionRow>(grid);

					item.Id = version.Id;
					item.Name = version.name;
					item.Checksum = version.checksum;
					item.FileSize = version.file_size;
					item.DirectoryId = version.dir_id;
					item.NodeId = version.node_id;
					item.EntryId = version.entry_id;
					item.AttachmentId = version.attachment_id;
					item.ModifyDate = version.file_modify_stamp;
					string? path = version.HashedValues != null && version.HashedValues.TryGetValue("dir_id", out ArrayList? list)
						? (list?[1]?.ToString())
						: null;
					item.OdooCompletePath = path;

					grid.ItemAdd(item);
				});
			}
		}
		internal async Task PreviewImage(HpVersion? version)
		{
			if (version is null) return;
			if (!await version.GetPreviewImage()) return;

			byte[] previewImageBytes = Convert.FromBase64String(version.preview_image!);
			MemoryStream ms = new(previewImageBytes)
			{
				Position = 0
			};
			
			// OdooEntryImage.Source = Assets.GetBitmapFromBytes(previewImageBytes);
		}
		internal async Task PreviewImage(int? hpVersionId)
		{
			const string previewImage = "preview_image";
			if (hpVersionId is null || hpVersionId == 0) return;

			HpVersion? version = (await HpVersion.GetRecordsByIdsAsync([hpVersionId], includedFields: [previewImage])).FirstOrDefault();
			PreviewImage(version);
		}
		#endregion


		internal static T EmptyGridTable<T>(ItemsControl grid) where T : new()
			=> EmptyListItem<T>(grid);
		internal static T EmptyListItem<T>(ItemsControl list) where T : new()
		{
			T entry = new();
			SafeHelper.SafeInvoker(() => list.ItemsSource ??= new ObservableCollection<T>());
			return entry;
		}
		internal static T EmptyListItem<T>(DataGrid grid) where T : new()
		{
			T entry = new();
			SafeHelper.SafeInvoker(() => grid.ItemsSource ??= new ObservableCollection<T>());
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
		internal static T EmptyListItemInternal<T>(ListView list) where T : new()
			=> EmptyListItem<T>(list);
		internal static T EmptyListItemInternal<T>(DataGrid grid) where T : new()
			=> EmptyListItem<T>(grid);

		internal static async Task<Dictionary<string, Task<HackFile>>> GetFileMap(Hashtable entries)
		{
			// need to check local files
			List<Task<HackFile>> hackTasks = new(entries.Count);
			Dictionary<string, Task<HackFile>> hackFileMap = new(entries.Count);

			foreach (DictionaryEntry pair in entries)
			{
				string? filepath = (pair.Value as Hashtable)?["fullname"] as string;

				if (filepath is null) continue;
				Task<HackFile> hackTask = HackBaseFile
					.GetHackFileAsync<HackFile>(HpDirectory
						.ConvertToWindowsPath(filepath, true));

				hackFileMap.Add(filepath, hackTask);
				hackTasks.Add(hackTask);
			}
			await Task.WhenAll(hackTasks);
			//Task.WaitAll(hackTasks.ToArray());
			return hackFileMap;
		}
		internal static void InitGridView(ItemsControl control) => InitListView(control, null);
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