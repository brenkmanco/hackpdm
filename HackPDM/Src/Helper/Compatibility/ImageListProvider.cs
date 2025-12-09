using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

using HackPDM.Src.ClientUtils.Types;

using Microsoft.UI.Xaml.Media;

namespace HackPDM.Helper.Compatibility;

public class ImageListProvider : IImageProvider
{
	private readonly Dictionary<string, Bitmap?> _images;
	private readonly ImageList _imageList;
	public ImageListProvider()
	{
		_images = [];
		_imageList = new();
	}

	public Bitmap? GetBitmap(string key) => (Bitmap?)(_imageList.Images.ContainsKey(key) ? new Bitmap(_imageList.Images[key]) : null);

	public ImageSource? GetImage(string key) => throw new NotSupportedException("ImageSource Isn't supported");

	public IEnumerable<string> GetAvailableKeys() => _images.Keys;


	public async void SetImage(string key, byte[] imgBytes)
	{
		MemoryStream ms = new();
		await ms.WriteAsync(imgBytes);
		var img = Image.FromStream(ms);
		_imageList.Images.Add(key, img);
	}

	public async Task<ImageSource?> GetImageAsync(string key)
	{
		throw new NotImplementedException();
	}
}