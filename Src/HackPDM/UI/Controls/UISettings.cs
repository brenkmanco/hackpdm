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
		private static AssetsImageProvider GetProvider( Dictionary<string, string>? assetmap = null )
		{
			var os = Environment.OSVersion.Version;

			return os.Major >= 10
				? new AssetsImageProvider(assetmap)
				: throw new NotSupportedException();
		}

		public static AssetsImageProvider? ImageProvider 
		{ 
			get
			{
				field ??= GetProvider(Assets.AssetMap);
				return field;
			}
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
