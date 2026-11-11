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

public class AssetsImageProvider : IImageProvider
{
	internal static readonly Dictionary<string, string> AssetMap;
	internal static readonly Dictionary<string, StorageFile> FileCache;
	// one decoded image per key, shared by every row that shows it (about 20 distinct icons)
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> ImageCache = new();
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
	public ImageSource? GetImage(string key)
	{
		return GetImageAsync(key).GetAwaiter().GetResult();
		// return AssetMap.TryGetValue(key, out var uri) ? new BitmapImage(new Uri(uri)) : (ImageSource?)null;
	}
	public async Task<ImageSource?> GetImageAsync(string key)
	{
		if (ImageCache.TryGetValue(key, out var cached)) return cached;
		// unknown keys never need the UI thread
		if (!AssetMap.TryGetValue(key, out var uriString)) return null;

		// image sources must be created on the UI thread
		var image = await SafeHelper.SafeInvoker(ImageSource? () =>
		{
			Uri uri = new(uriString);
			return uriString.EndsWith(".svg") 
				? new SvgImageSource(uri) 
				: new BitmapImage(uri);
		});
		return image is null ? null : ImageCache.GetOrAdd(key, image);
	}
	public async void SetImage(string key, byte[] imageBytes)
	{
		try
		{
			StorageFile imgFile = await ImagesFolder.CreateFileAsync($"{key}.png", CreationCollisionOption.ReplaceExisting);
			using var fileStream = await imgFile.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowReadersAndWriters);
			await fileStream.WriteAsync(imageBytes.AsBuffer());
			AssetMap.TryAdd(key, $"{ImagesFolderPathUri}/{key}.png");
			ImageCache.TryRemove(key, out _);
		}
		catch
		{
			Debug.WriteLine("Can't create image");
		}
	}
	public static async void SetImage(string key, SoftwareBitmap softwareBitmap)
	{
		try
		{
			
			StorageFile imgFile = await ImagesFolder.CreateFileAsync($"{key}.png", CreationCollisionOption.ReplaceExisting);
			using var fileStream = await imgFile.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowReadersAndWriters);
			BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
			encoder.SetSoftwareBitmap(softwareBitmap);
			await encoder.FlushAsync();
			AssetMap.TryAdd(key, $"{ImagesFolderPathUri}/{key}.png");
			ImageCache.TryRemove(key, out _);
		}
		catch
		{
            Debug.WriteLine("Can't create image");
        }
	}
	public IEnumerable<string> GetAvailableKeys() => AssetMap.Keys;

}