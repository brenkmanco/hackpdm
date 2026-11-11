using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Common.Helpers;

/// <summary>
/// Layout extensions for FrameworkElement.
/// </summary>
public static class FrameworkElementExtensions
{
    public static void SetGrid(this FrameworkElement element, int row, int column, int rowspan = 1, int columnspan = 1)
    {
        element.SetValue(Grid.RowProperty, row);
        element.SetValue(Grid.ColumnProperty, column);
        element.SetValue(Grid.RowSpanProperty, rowspan);
        element.SetValue(Grid.ColumnSpanProperty, columnspan);
    }
}
