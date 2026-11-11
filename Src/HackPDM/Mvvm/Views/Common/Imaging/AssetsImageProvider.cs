using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HackPDM.Mvvm.Views.Common.Imaging;

public class AssetsImageProvider
{
	internal static readonly Dictionary<string, string> AssetMap;
	internal static readonly Dictionary<string, StorageFile> FileCache;
	// one decoded image per key, shared by every row that shows it (about 20 distinct icons)
	private static StorageFolder Storage => ApplicationData.Current.LocalFolder;
	private static string ImagesFolderPath => $"{StorageBox.ASSETSFOLDER}/{StorageBox.IMAGEFOLDER}";
    private static string ImagesFolderPathUri => $"{StorageBox.LOCALPREFIX}/{ImagesFolderPath}";
	private static StorageFolder AssetsFolder => field ??= Storage.CreateFolderAsync(StorageBox.ASSETSFOLDER, CreationCollisionOption.OpenIfExists).GetAwaiter().GetResult();
    private static StorageFolder ImagesFolder => field ??= AssetsFolder.CreateFolderAsync(StorageBox.IMAGEFOLDER, CreationCollisionOption.OpenIfExists).GetAwaiter().GetResult();
	public AssetsImageProvider() : this([]) {}
	public AssetsImageProvider(Dictionary<string, string>? assetMap)
	{
		if (assetMap is not { Count: > 0 }) return;
		foreach (var item in assetMap)
		{
			if (!AssetMap.ContainsKey(item.Key)) AssetMap.Add(item.Key, item.Value);
		}
	}
	static AssetsImageProvider()
	{
		AssetMap = new();
		FileCache = new();
	}
	public IEnumerable<string> GetAvailableKeys() => AssetMap.Keys;

}