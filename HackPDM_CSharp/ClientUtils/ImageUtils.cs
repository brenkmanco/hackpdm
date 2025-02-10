using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HackPDM.ClientUtils
{
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
	}
}
