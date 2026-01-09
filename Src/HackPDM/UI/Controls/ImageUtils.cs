using System;
using System.Diagnostics;

using System.Threading.Tasks;

using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;


namespace HackPDM.UI.Controls;

internal static class ImageUtils
{
	private static readonly CanvasDevice _device = CanvasDevice.GetSharedDevice();

	public static CanvasRenderTarget ResizeCanvasBitmap(
		CanvasBitmap source,
		int width=32,
		int height=32)
	{
		var target = new CanvasRenderTarget(_device, width, height, 96);

		using (var ds = target.CreateDrawingSession())
		{
			ds.DrawImage(source, new Rect(0, 0, width, height));
		}

		return target;
	}

	public async static Task<SoftwareBitmap?> OverlayBitmapImagesAsync(
		BitmapImage baseImage,
		BitmapImage overlayImage,
		int width = 32,
		int height = 32)
	{
		var device = _device;
		var renderTarget = new CanvasRenderTarget(device, width, height, 96);

		using (var ds = renderTarget.CreateDrawingSession())
		{
			var baseBitmap = await LoadCanvasBitmapAsync(baseImage);
			var overlayBitmap = await LoadCanvasBitmapAsync(overlayImage);

			if (baseBitmap is null || overlayBitmap is null)
				return null;

			// Resize both images to match old behavior
			ds.DrawImage(baseBitmap, new Rect(0, 0, width, height));
			ds.DrawImage(overlayBitmap, new Rect(0, 0, width, height));
		}

		return await SoftwareBitmap.CreateCopyFromSurfaceAsync(renderTarget);
	}

	public static async Task<bool> SaveSoftwareBitmapAsync(
	SoftwareBitmap bitmap,
	StorageFile file,
	Guid encoderId)
	{
		try
		{
			using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
			var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);

			encoder.SetSoftwareBitmap(bitmap);
			await encoder.FlushAsync();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async static Task<CanvasBitmap?> LoadCanvasBitmapAsync(BitmapImage bitmapImage)
	{
		try
		{
			var streamRef = RandomAccessStreamReference.CreateFromUri(bitmapImage.UriSource);
			using var stream = await streamRef.OpenReadAsync();
			return await CanvasBitmap.LoadAsync(_device, stream);
		}
		catch (Exception ex)
		{
			Debug.Write("Failed to load CanvasBitmap from BitmapImage.", ex.Message);
			return null;
		}
	}
	public static BitmapImage Load(string name) =>
		new(new Uri($"ms-appx:///Assets/{name}.png"));
       
}
