using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.Mvvm.Views.Common.Helpers;

/// <summary>
/// Extensions and utilities for inspecting and traversing the WinUI Visual Tree.
/// </summary>
public static class VisualTreeExtensions
{
    /// <summary>Finds the first <see cref="ScrollViewer"/> below an element, searching depth-first.</summary>
    /// <param name="parent">The element whose descendants are searched (not the element itself).</param>
    /// <returns>The first scroll viewer found; null when there is none or the element's template is not applied yet.</returns>
    public static ScrollViewer? GetScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv)
                return sv;

            var result = GetScrollViewer(child);
            if (result != null)
                return result;
        }
        return null;
    }
}
