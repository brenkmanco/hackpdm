using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using HackPDM.Core.Helper.Xaml;

namespace HackPDM.UI.Compatibility;

public class AssetsImageProvider : IImageProvider
{
	internal static readonly Dictionary<string, string> AssetMap;
	internal static readonly Dictionary<string, StorageFile> FileCache;
	private static StorageFolder Storage => ApplicationData.Current.LocalFolder;
	private static string ImagesFolderPath => $"{StorageBox.ASSETSFOLDER}/{StorageBox.IMAGEFOLDER}";
    private static string ImagesFolderPathUri => $"{StorageBox.LOCALPREFIX}/{ImagesFolderPath}";
	private static StorageFolder AssetsFolder => field ??= Storage.CreateFolderAsync(StorageBox.ASSETSFOLDER, CreationCollisionOption.OpenIfExists).Get();
    private static StorageFolder ImagesFolder => field ??= AssetsFolder.CreateFolderAsync(StorageBox.IMAGEFOLDER, CreationCollisionOption.OpenIfExists).Get();
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
	public ImageSource? GetImage(string key)
	{
		return GetImageAsync(key).GetAwaiter().GetResult();
		// return AssetMap.TryGetValue(key, out var uri) ? new BitmapImage(new Uri(uri)) : (ImageSource?)null;
	}
	public async Task<ImageSource?> GetImageAsync(string key)
	{
		return await SafeHelper.SafeInvoker(ImageSource? () =>
		{
			if (!AssetMap.TryGetValue(key, out var uriString)) return null;
			Uri uri = new(uriString);
			return uriString.EndsWith(".svg") 
				? new SvgImageSource(uri) 
				: new BitmapImage(uri);
		});
	}
	public async void SetImage(string key, byte[] imageBytes)
	{
		try
		{
			StorageFile imgFile = await ImagesFolder.CreateFileAsync($"{key}.png", CreationCollisionOption.ReplaceExisting);
			using var fileStream = await imgFile.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowReadersAndWriters);
			await fileStream.WriteAsync(imageBytes.AsBuffer());
			AssetMap.TryAdd(key, $"{ImagesFolderPathUri}/{key}.png");
		}
		catch
		{
			Debug.WriteLine("Can't create image");
		}
	}
	public async void SetImage(string key, SoftwareBitmap softwareBitmap)
	{
		try
		{
			
			StorageFile imgFile = await ImagesFolder.CreateFileAsync($"{key}.png", CreationCollisionOption.ReplaceExisting);
			using var fileStream = await imgFile.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowReadersAndWriters);
			BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
			encoder.SetSoftwareBitmap(softwareBitmap);
			await encoder.FlushAsync();
			AssetMap.TryAdd(key, $"{ImagesFolderPathUri}/{key}.png");
		}
		catch
		{
            Debug.WriteLine("Can't create image");
        }
	}
	public IEnumerable<string> GetAvailableKeys() => AssetMap.Keys;

}