using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.UI.Helpers;

/// <summary>
/// Extensions and utilities for inspecting and traversing the WinUI Visual Tree.
/// </summary>
public static class VisualTreeExtensions
{
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
