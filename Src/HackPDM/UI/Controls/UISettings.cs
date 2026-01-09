using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using HackPDM.UI.Compatibility;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HackPDM.UI.Controls
{
	public class UISettings
	{
		private static IImageProvider GetProvider(Dictionary<string, string>? assetmap = null)
		{
			var os = Environment.OSVersion.Version;
#pragma warning disable CA1416 // Validate platform compatibility
			return os.Major >= 10
				? new AssetsImageProvider(assetmap)
				: throw new NotSupportedException(); // : new ImageListProvider();
#pragma warning restore CA1416 // Validate platform compatibility
		}

		public static IImageProvider? ImageProvider 
		{ 
			get
			{
				field ??= GetProvider(Assets.AssetMap);
				return field;
			}
		}
		public static ImageSource? GetImage(string key)							=> ImageProvider?.GetImage(key);
		public static Task<ImageSource?>? GetImageAsync(string key)				=> ImageProvider?.GetImageAsync(key);
		public static BitmapImage? GetBitmapFromBytes(byte[] imgBytes)
		{
			using var ms = new MemoryStream(imgBytes);
			var img = new BitmapImage();
			img.SetSource(ms.AsRandomAccessStream());
			return img;
		}
		public static IEnumerable<string>? GetAvailableKeys()					=> ImageProvider?.GetAvailableKeys();
		public static void SetImage(string key, byte[] imgBytes)				=> ImageProvider?.SetImage(key, imgBytes);
		public static void SetImage(string key, SoftwareBitmap img)
		{
			if (ImageProvider is AssetsImageProvider aip)
			{
				aip.SetImage(key, img);
			}
		}
		public async static Task<byte[]> GetImageBytes(BitmapImage img)
		{
			var streamref = RandomAccessStreamReference.CreateFromUri(img.UriSource);
			using var stream = await streamref.OpenReadAsync();
			byte[] buffer = new byte[stream.Size];
			await stream.ReadAsync(buffer.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
			return buffer;
		}
	}
	public interface IImageProvider
	{
		ImageSource? GetImage(string key);
		Task<ImageSource?> GetImageAsync(string key);
		void SetImage(string key, byte[] imgBytes);
		IEnumerable<string> GetAvailableKeys();
	}
	// public interface IItemChangeListener<T>
	// {
	// 	void OnItemAdded(object sender,			ItemChangedEventArgs<T> e);
	// 	void OnItemRemoved(object sender,		ItemChangedEventArgs<T> e);
	// 	void OnItemUpdated(object sender,		ItemChangedEventArgs<T> e);
	// 	void OnItemSelected(object sender,		ItemChangedEventArgs<T> e);
	// 	void OnItemClicked(object sender,		ItemChangedEventArgs<T> e);
	// 	void OnItemDoubleClicked(object sender, ItemChangedEventArgs<T> e);
	// 	void OnItemRendering(object sender,		ItemChangedEventArgs<T> e);
	// 	void OnItemFocused(object sender,		ItemChangedEventArgs<T> e);
	// 	void OnItemHovered(object sender,		ItemChangedEventArgs<T> e);
	// }
	
}
