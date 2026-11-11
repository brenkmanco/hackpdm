global using HackPDM.UI.Helpers;

using System;
using System.Collections.Generic;
using HackPDM.Shared.GlobalData;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

using FlowDirection = HackPDM.Shared.GlobalData.FlowDirection;

namespace HackPDM.UI.Forms.Helper;

/// <summary>
/// Backward-compatibility facade for form helpers.
/// Methods are delegated to <see cref="ColorDrawingHelper"/> and <see cref="VisualTreeExtensions"/>.
/// </summary>
public static class FormHelper
{
    public static LinearGradientBrush EZGradient(FlowDirection direction, params Color[] colorGradient)
        => ColorDrawingHelper.EZGradient(direction, colorGradient);

    public static LinearGradientBrush EZGradient(double angle, params Color[] colorGradient)
        => ColorDrawingHelper.EZGradient(angle, colorGradient);

    public static IEnumerable<GradientStop> EvenSpacedGradient(params Color[] colorGradient)
        => ColorDrawingHelper.EvenSpacedGradient(colorGradient);

    public static ScrollViewer? GetScrollViewer(DependencyObject parent)
        => VisualTreeExtensions.GetScrollViewer(parent);
}
