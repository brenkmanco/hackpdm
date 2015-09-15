using System;
using System.Collections.Generic;
using System.Text;

using System.Runtime.InteropServices;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using OpenMcdf;

namespace HackPDM
{
	class IconFromFile
	{

		private static string imageFilter = ".jpg,.jpeg,.png,.gif";
		private static string solidworksFilter = ".sldprt,.sldasm,.slddrw";
		private static  System.Drawing.Size size = new System.Drawing.Size(256, 256);

		private const uint SHGFI_ICON = 0x100;
		private const uint SHGFI_LARGEICON = 0x0;
		private const uint SHGFI_SMALLICON = 0x1;
		private const uint SHGFI_DISPLAYNAME = 0x00000200;
		private const uint SHGFI_TYPENAME = 0x400;


		[DllImport("user32")]
		public static extern int DestroyIcon(IntPtr hIcon);

		private static Icon GetSmallFileIcon(FileInfo file)
		{
			if (file.Exists)
			{
				SHFILEINFO shFileInfo = new SHFILEINFO();
				SHGetFileInfo(file.FullName, 0, ref shFileInfo, (uint)Marshal.SizeOf(shFileInfo), SHGFI_ICON | SHGFI_SMALLICON);
				Icon ico = Icon.FromHandle(shFileInfo.hIcon);
				//DestroyIcon(shFileInfo.hIcon);
				return ico;
			}
			else return SystemIcons.WinLogo;

		}

		public Icon GetSmallFileIcon(string fileName)
		{
			if (File.Exists(fileName))
			{
				return GetSmallFileIcon(new FileInfo(fileName));
			}
			else
			{
				string strFileExt = Path.GetExtension(fileName).ToLower();
				string strTempName = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + "." + strFileExt;
				FileInfo fileInfo = new FileInfo(fileName);
				fileInfo.Attributes = FileAttributes.Temporary;

				using (new FileStream(fileName, FileMode.CreateNew))
				{
					return GetSmallFileIcon(fileInfo);
				}
			}
		}

		private static Icon GetLargeFileIcon(FileInfo file)
		{
			if (file.Exists)
			{
				SHFILEINFO shFileInfo = new SHFILEINFO();
				SHGetFileInfo(file.FullName, 0, ref shFileInfo, (uint)Marshal.SizeOf(shFileInfo), SHGFI_ICON | SHGFI_LARGEICON);
				Icon ico = Icon.FromHandle(shFileInfo.hIcon);
				//DestroyIcon(shFileInfo.hIcon);
				return ico;
			}
			else return SystemIcons.WinLogo;
		}

		public Icon GetLargeFileIcon(string fileName)
		{
			if (File.Exists(fileName))
			{
				return GetLargeFileIcon(new FileInfo(fileName));
			}
			else
			{
				string strFileExt = Path.GetExtension(fileName).ToLower();
				string strTempName = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + "." + strFileExt;

				using (new FileStream(strTempName, FileMode.CreateNew))
				{
                    FileInfo fileInfo = new FileInfo(strTempName);
					fileInfo.Attributes = FileAttributes.Temporary;
					return GetLargeFileIcon(fileInfo);
				}
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SHFILEINFO
		{
			public SHFILEINFO(bool b)
			{
				hIcon = IntPtr.Zero;
				iIcon = IntPtr.Zero;
				dwAttributes = 0;
				szDisplayName = "";
				szTypeName = "";
			}

			public IntPtr hIcon;
			public IntPtr iIcon;
			public uint dwAttributes;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szDisplayName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		};


		[DllImport("shell32.dll")]
		public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

		public Image GetThumbnail(string FileName)
		{
			
			if (isImage(FileName))
			{
				// get thumbnail from image file
				Bitmap origBitmap = GetImageThumbnail(FileName);
				return (Image)origBitmap;
			}
			else if (isSolidWorks(FileName))
			{
				// get preview image from solidworks file
				Bitmap origBitmap = GetCompoundPreview(FileName, "PreviewPNG");
				return (Image)origBitmap;
			}
			else
			{
				// try to get preview image from other compound document
				Bitmap origBitmap = GetCompoundPreview(FileName, "Preview");
				if (origBitmap != null)
				{
					return (Image)origBitmap;
				}

				Icon ico = GetLargeFileIcon(FileName);
				if (ico == null) return null;
				return resizeImage(ico.ToBitmap());
			}

		}

		private Bitmap GetImageThumbnail(string FileName)
		{

			try
			{
				using (Bitmap origBitmap = new Bitmap(FileName))
				{
					return resizeImage(origBitmap);
				}
			}
			catch {
				return null;
			}

		}

		private Bitmap GetCompoundPreview(string FileName, string StreamName)
		{

			try
			{
				byte[] bImage;
                Stream fStream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				CompoundFile cf = new CompoundFile(fStream);
				CFStream st;
				st = cf.RootStorage.GetStream(StreamName);
				bImage = st.GetData();
				MemoryStream ms = new MemoryStream(bImage);
				Image returnImage = Image.FromStream(ms);
				ms.Dispose();
				cf.Close();
				return (Bitmap)returnImage;
			}
            catch (IOException ex)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return null;
            }

		}

		private static Bitmap resizeImage(Bitmap sourceImage)
		{
			int sourceWidth = sourceImage.Width;
			int sourceHeight = sourceImage.Height;

			float nPercent = 0;
			float nPercentW = 0;
			float nPercentH = 0;

			nPercentW = ((float)size.Width / (float)sourceWidth);
			nPercentH = ((float)size.Height / (float)sourceHeight);

			if (nPercentH < nPercentW)
				nPercent = nPercentH;
			else
				nPercent = nPercentW;

			int destWidth = (int)(sourceWidth * nPercent);
			int destHeight = (int)(sourceHeight * nPercent);
			var destRect = new Rectangle(0, 0, destWidth, destWidth);

			int leftOffset = (int)((size.Width - destWidth) / 2);
			int topOffset = (int)((size.Height - destHeight) / 2);


			Bitmap destImage = new Bitmap(size.Width, size.Height);
			destImage.SetResolution(sourceImage.HorizontalResolution, sourceImage.VerticalResolution);

			using (var graphics = Graphics.FromImage((Image)destImage))
			{
				graphics.CompositingMode = CompositingMode.SourceCopy;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

				using (var wrapMode = new System.Drawing.Imaging.ImageAttributes())
				{
					wrapMode.SetWrapMode(WrapMode.TileFlipXY);
					graphics.DrawImage(sourceImage, destRect, leftOffset, topOffset, sourceImage.Width, sourceImage.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}

			return destImage;

		}

		private static bool isImage(string fileName)
		{
			string ext = Path.GetExtension(fileName).ToLower();
			if (ext == "")
				return false;
			return (imageFilter.IndexOf(ext) != -1 && File.Exists(fileName));
		}

		private static bool isSolidWorks(string fileName)
		{
			string ext = Path.GetExtension(fileName).ToLower();
			if (ext == "")
				return false;
			return (solidworksFilter.IndexOf(ext) != -1 && File.Exists(fileName));
		}


	}
}
