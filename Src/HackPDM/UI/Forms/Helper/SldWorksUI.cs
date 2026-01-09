using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using Microsoft.Graphics.Canvas;
using SolidWorks.Interop.swdocumentmgr;

namespace HackPDM.UI.Forms.Helper;

public static class SldWorksUI
{
    public static SoftwareBitmap GetPreview(string fileName, bool deep = false)
    {
        // external references for assembly files (GetAllExternalReferences4)
        // external references for part files (GetExternalFeatureReferences)
        SwDMDocument11 swDoc = default(SwDMDocument11);

        // get doc type
        SwDmDocumentType swDocType = SwDocMgr.GetTypeFromString(fileName);
        if (swDocType == SwDmDocumentType.swDmDocumentUnknown)
        {
            return null;
        }

        // get the document
        SwDmDocumentOpenError nRetVal = 0;
        swDoc = (SwDMDocument11)SwDocMgr.GetApplication().GetDocument(fileName, swDocType, true, out nRetVal);
        if (SwDmDocumentOpenError.swDmDocumentOpenErrorNone != nRetVal)
        {
            DialogResult dr = MessageBox.Show("Failed to open solidworks file: " + fileName,
                "Loading SW File",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
            return null;
        }

        SwDmPreviewError ePrevError = SwDmPreviewError.swDmPreviewErrorNone;
        try
        {
            byte[] bPreview = (byte[])swDoc.GetPreviewBitmapBytes(out ePrevError);
            var softBitmap =
                SoftwareBitmap.CreateCopyFromBuffer(bPreview.AsBuffer(), BitmapPixelFormat.Unknown, 640, 480);

            // crop and pad the image to 640x480
            return softBitmap;
        }
        catch
        {
            //DialogResult dr = MessageBox.Show("Failed to get solidworks preview image: " + FileName + ": " + ePrevError.ToString(),
            //    "Loading SW Preview",
            //    MessageBoxButtons.OK,
            //    MessageBoxIcon.Exclamation,
            //    MessageBoxDefaultButton.Button1);
            return null;
        }

    }
    // methods for processing preview images

    public static byte[][] GetRgb(SoftwareBitmap bitmap)
    {
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

                // Get pointer to pixel buffer
                ((IMemoryBufferByteAccess)reference).GetBuffer(out data, out capacity);

                int index = 0;

                // BGRA or RGBA depending on alpha mode
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
                        else // RGBA
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

        return new[] { r, g, b };
    }

    // Required COM interface for buffer access
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
    public static async Task<SoftwareBitmap> ResizeImageAsync(
        SoftwareBitmap input,
        int outputWidth = 640,
        int outputHeight = 480)
    {
        // Convert SoftwareBitmap → CanvasBitmap
        var device = CanvasDevice.GetSharedDevice();
        using var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, input);

        uint sourceWidth = canvasBitmap.SizeInPixels.Width;
        uint sourceHeight = canvasBitmap.SizeInPixels.Height;

        // Compute aspect‑ratio preserving scale
        float percentW = (float)outputWidth / sourceWidth;
        float percentH = (float)outputHeight / sourceHeight;
        float scale = Math.Min(percentW, percentH);

        int destWidth = (int)(sourceWidth * scale);
        int destHeight = (int)(sourceHeight * scale);

        int leftOffset = (outputWidth - destWidth) / 2;
        int topOffset = (outputHeight - destHeight) / 2;

        // Create output render target
        using var renderTarget =
            new CanvasRenderTarget(device, outputWidth, outputHeight, 96);

        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Windows.UI.Color.FromArgb(255, 255, 255, 255)); // white background

            var destRect = new Windows.Foundation.Rect(
                leftOffset,
                topOffset,
                destWidth,
                destHeight);

            ds.DrawImage(canvasBitmap, destRect);
        }

        // Convert CanvasRenderTarget → SoftwareBitmap
        return await SoftwareBitmap.CreateCopyFromSurfaceAsync(renderTarget);
    }

}