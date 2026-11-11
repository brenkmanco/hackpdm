using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using HackPDM.Mvvm.Views.Common.Helpers;

namespace HackPDM.Mvvm.Views.Common.Controls;

/// <summary>
/// Legacy facade for SolidWorks preview functionality.
/// Delegates to <see cref="SolidWorksPreviewHelper"/>.
/// </summary>
public static class SldWorksUI
{
    public static async Task<SoftwareBitmap?> GetPreview(string fileName, bool deep = false)
        => await SolidWorksPreviewHelper.GetPreviewAsync(fileName, deep);

    public static byte[][] GetRgb(SoftwareBitmap bitmap)
        => SolidWorksPreviewHelper.GetRgb(bitmap);

    public static async Task<SoftwareBitmap> ResizeImageAsync(
        SoftwareBitmap input,
        int outputWidth = 640,
        int outputHeight = 480)
        => await SolidWorksPreviewHelper.ResizeImageAsync(input, outputWidth, outputHeight);
}
