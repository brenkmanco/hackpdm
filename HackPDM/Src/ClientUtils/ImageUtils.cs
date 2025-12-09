using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;

namespace HackPDM.ClientUtils;

internal static class ImageUtils
{
	public static Image ImageOverlay( Image imgExt, Image imgOverlay )
	{
		const int width = 32;
		const int height = 32;

		Bitmap bitmap = new(width, height);
		Graphics canvas = Graphics.FromImage(bitmap);

		if ( imgExt.Width != width || imgExt.Height != height )
		{
			imgExt = ResizeImage( imgExt, width, height );
		}
		if ( imgOverlay.Width != width || imgOverlay.Height != height )
		{
			imgOverlay = ResizeImage( imgOverlay, width, height );
		}
		canvas.DrawImage( imgExt, new Point( 0, 0 ) );
		canvas.DrawImage( imgOverlay, new Point( 0, 0 ) );
		canvas.Save();

		return Image.FromHbitmap( bitmap.GetHbitmap() );
	}
	public static Bitmap ResizeImage( Image img, int width, int height )
	{
		var destRect = new Rectangle(0, 0, width, height);
		var destImage = new Bitmap(width, height);

		destImage.SetResolution( img.HorizontalResolution, img.VerticalResolution );

		using ( var graphics = Graphics.FromImage( destImage ) )
		{
			graphics.CompositingMode = CompositingMode.SourceCopy;
			graphics.CompositingQuality = CompositingQuality.HighQuality;
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			using ( var wrapMode = new ImageAttributes() )
			{
				wrapMode.SetWrapMode( WrapMode.TileFlipXY );
				graphics.DrawImage( img, destRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, wrapMode );
			}
		}

		return destImage;
	}

	// extension
	public static bool ImageFormater( this Image image, string imageName, ImageFormat format )
	{
		try
		{
			image.Save(imageName, format);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public async static Task<SoftwareBitmap?> OverlayBitmapImagesAsync(BitmapImage baseImage, BitmapImage overlayImage, int width = 32, int height = 32)
	{
		var device = CanvasDevice.GetSharedDevice();
		var renderTarget = new CanvasRenderTarget(device, width, height, 96);

		using (var ds = renderTarget.CreateDrawingSession())
		{
			var baseBitmap = await LoadCanvasBitmapAsync(device, baseImage);
			var overlayBitmap = await LoadCanvasBitmapAsync(device, overlayImage);

			if (baseBitmap is null || overlayBitmap is null) return null;
			ds.DrawImage(baseBitmap);
			ds.DrawImage(overlayBitmap);
		}

		return await SoftwareBitmap.CreateCopyFromSurfaceAsync(renderTarget);
	}

	private async static Task<CanvasBitmap?> LoadCanvasBitmapAsync(CanvasDevice device, BitmapImage bitmapImage)
	{
		try
		{
			var streamRef = RandomAccessStreamReference.CreateFromUri(bitmapImage.UriSource);
			using var stream = await streamRef.OpenReadAsync();
			return await CanvasBitmap.LoadAsync(device, stream);
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