using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Common.Helpers;

/// <summary>
/// Layout extensions for FrameworkElement.
/// </summary>
public static class FrameworkElementExtensions
{
    /// <summary>Places an element in a <see cref="Grid"/> cell.</summary>
    /// <param name="element">The element to place.</param>
    /// <param name="row">The zero-based row.</param>
    /// <param name="column">The zero-based column.</param>
    /// <param name="rowspan">How many rows it spans.</param>
    /// <param name="columnspan">How many columns it spans.</param>
    public static void SetGrid(this FrameworkElement element, int row, int column, int rowspan = 1, int columnspan = 1)
    {
        element.SetValue(Grid.RowProperty, row);
        element.SetValue(Grid.ColumnProperty, column);
        element.SetValue(Grid.RowSpanProperty, rowspan);
        element.SetValue(Grid.ColumnSpanProperty, columnspan);
    }
}
