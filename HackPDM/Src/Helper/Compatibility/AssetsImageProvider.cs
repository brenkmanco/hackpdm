using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using HackPDM.Forms.Hack;
using HackPDM.Src.ClientUtils.Types;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Graphics.Imaging;
using Windows.Storage;

using HackPDM.Extensions.General;

namespace HackPDM.Helper.Compatibility;

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
		if (assetMap is not null and { Count: > 0 })
		{
			foreach (var item in assetMap)
			{
				if (!AssetMap.ContainsKey(item.Key)) AssetMap.Add(item.Key, item.Value);
			}
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
        if (!AssetMap.TryGetValue(key, out var uriString)) return null;
		Uri uri = new (uriString);
		return uriString.EndsWith(".svg") ? new SvgImageSource(uri) : new BitmapImage(uri);

		//if (uri.Scheme == "ms-appx")
		//{
		//}
		//if (!FileCache.TryGetValue(key, out var file))
		//{
		//	file = await StorageFile.GetFileFromPathAsync(uri.AbsolutePath);
		//	FileCache.TryAdd(key, file);
		//}
		//try
		//{
		//	var stream = await file.OpenAsync(FileAccessMode.Read);
		//	var bitmap = new BitmapImage();
		//	await bitmap.SetSourceAsync(stream);
		//	return bitmap;
		//}
		//catch { Debug.WriteLine("Unable to load image"); }
		//return null;
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
	public Bitmap? GetBitmap(string key) => throw new NotSupportedException("WinUI doesn't use System.Drawing");
	public void SetImage(string key, Bitmap bitmap) => throw new NotSupportedException("WinUI doesn't use System.Drawing");

}