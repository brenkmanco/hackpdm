using System;
using System.Collections;
using HackPDM.Shared.GlobalData;

namespace HackPDM.UI.Models;

public class ComparerSort : IComparer
{
    public delegate int CompareFunction(object? x, object? y);
    public CompareFunction? ComparerFunction { get; set; }
    public ColumnGroup Group { get; set; }
    public bool IsAscending { get; set; }
    public bool InvalidsAtBack { get; set; }
    public SortPredefined SortType { get; set; }

    public int Compare(object? x, object? y)
    {
        if (ComparerFunction is null) return 0;
        int result = ComparerFunction(x, y);
        return Math.Max(-1, Math.Min(1, result));
    }
}
