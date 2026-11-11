using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Dialogs;
#if WINDOWS
using Microsoft.Graphics.Canvas;
#endif
#if WINDOWS
using SolidWorks.Interop.swdocumentmgr;
#endif

namespace HackPDM.Mvvm.Views.Common.Helpers;

/// <summary>
/// Helper for extracting and processing SolidWorks preview bitmaps,
/// using COM interop with SWDocumentMgr and Win2D image resizing.
/// </summary>
public static class SolidWorksPreviewHelper
{
    public static async Task<SoftwareBitmap?> GetPreviewAsync(string fileName, bool deep = false)
    {
#if WINDOWS
        SwDmDocumentType swDocType = SwDocMgr.GetTypeFromString(fileName);
        if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
        {
            return null;
        }

        SwDmDocumentOpenError nRetVal = 0;
        var swDoc = (SwDMDocument11)SwDocMgr.GetApplication().GetDocument(fileName, swDocType, true, out nRetVal);
        if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
        {
            await MessageBox.ShowAsync(
                "Failed to open solidworks file: " + fileName,
                "Loading SW File",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
            return null;
        }

        try
        {
            byte[] bPreview = (byte[])swDoc.GetPreviewBitmapBytes(out SwDmPreviewError ePrevError);
            var softBitmap = SoftwareBitmap.CreateCopyFromBuffer(bPreview.AsBuffer(), BitmapPixelFormat.Unknown, 640, 480);
            return softBitmap;
        }
        catch
        {
            return null;
        }
#else
        return null;
#endif
    }

    public static byte[][] GetRgb(SoftwareBitmap bitmap)
    {
#if WINDOWS
        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 &&
            bitmap.BitmapPixelFormat != BitmapPixelFormat.Rgba8)
        {
            throw new ArgumentException("Bitmap must be Bgra8 or Rgba8.");
        }

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int numPixels = width * height;

        byte[] r = new byte[numPixels];
        byte[] g = new byte[numPixels];
        byte[] b = new byte[numPixels];

        using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read))
        using (var reference = buffer.CreateReference())
        {
            unsafe
            {
                byte* data;
                uint capacity;

                ((IMemoryBufferByteAccess)reference).GetBuffer(out data, out capacity);

                int index = 0;
                bool isBGRA = bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8;

                for (int y = 0; y < height; y++)
                {
                    byte* row = data + y * buffer.GetPlaneDescription(0).Stride;

                    for (int x = 0; x < width; x++)
                    {
                        byte* pixel = row + x * 4;

                        if (isBGRA)
                        {
                            b[index] = pixel[0];
                            g[index] = pixel[1];
                            r[index] = pixel[2];
                        }
                        else
                        {
                            r[index] = pixel[0];
                            g[index] = pixel[1];
                            b[index] = pixel[2];
                        }

                        index++;
                    }
                }
            }
        }

        return [r, g, b];
#else
        return [[], [], []];
#endif
    }

#if WINDOWS
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
#endif

#if WINDOWS
    public static async Task<SoftwareBitmap> ResizeImageAsync(
        SoftwareBitmap input,
        int outputWidth = 640,
        int outputHeight = 480)
    {
        var device = CanvasDevice.GetSharedDevice();
        using var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, input);

        uint sourceWidth = canvasBitmap.SizeInPixels.Width;
        uint sourceHeight = canvasBitmap.SizeInPixels.Height;

        float percentW = (float)outputWidth / sourceWidth;
        float percentH = (float)outputHeight / sourceHeight;
        float scale = Math.Min(percentW, percentH);

        int destWidth = (int)(sourceWidth * scale);
        int destHeight = (int)(sourceHeight * scale);

        int leftOffset = (outputWidth - destWidth) / 2;
        int topOffset = (outputHeight - destHeight) / 2;

        using var renderTarget = new CanvasRenderTarget(device, outputWidth, outputHeight, 96);

        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Windows.UI.Color.FromArgb(255, 255, 255, 255));

            var destRect = new Windows.Foundation.Rect(
                leftOffset,
                topOffset,
                destWidth,
                destHeight);

            ds.DrawImage(canvasBitmap, destRect);
        }

        return await SoftwareBitmap.CreateCopyFromSurfaceAsync(renderTarget);
    }
#else
    public static Task<SoftwareBitmap> ResizeImageAsync(
        SoftwareBitmap input,
        int outputWidth = 640,
        int outputHeight = 480) => Task.FromResult(input);
#endif
}
