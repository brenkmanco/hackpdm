using System.Collections.Generic;
using HackPDM.Shared.GlobalData;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace HackPDM.Mvvm.Views.Common.Helpers;

/// <summary>
/// Helper for generating gradients, gradient stops, and color modifications.
/// </summary>
public static class ColorDrawingHelper
{
    public static LinearGradientBrush EZGradient(FlowDirection direction, params Color[] colorGradient)
    {
        return EZGradient(direction switch
        {
            FlowDirection.TopToBottom => 90,
            FlowDirection.TopLeftToBottomRight => 135,
            FlowDirection.BottomLeftToTopRight => 225,
            _ => 0,
        }, colorGradient);
    }

    public static Color ModifyColor(this Color color, byte? A = null, byte? R = null, byte? G = null, byte? B = null)
    {
        if (A is { } a) color.A = a;
        if (R is { } r) color.R = r;
        if (G is { } g) color.G = g;
        if (B is { } b) color.B = b;
        return color;
    }

    public static LinearGradientBrush EZGradient(double angle, params Color[] colorGradient)
    {
        var gradStopCollection = new GradientStopCollection();

        for (int i = 0; i < colorGradient.Length; i++)
        {
            var gradStop1 = new GradientStop
            {
                Color = colorGradient[i],
                Offset = ((double)i / (colorGradient.Length - 1))
            };
            gradStopCollection.Add(gradStop1);
        }
        return new LinearGradientBrush(gradStopCollection, angle);
    }

    public static IEnumerable<GradientStop> EvenSpacedGradient(params Color[] colorGradient)
    {
        for (int i = 0; i < colorGradient.Length; i++)
        {
            yield return new GradientStop
            {
                Color = colorGradient[i],
                Offset = (double)i / (colorGradient.Length - 1)
            };
        }
    }
}
