using System;
using HackPDM.Shared.GlobalData;
using Microsoft.UI.Xaml;

namespace HackPDM.UI.Models;

public class ColumnInfo
{
    public const int DEFAULT_WIDTH = 75;
    public readonly ColumnGroup Group;
    public string Name;
    public int Width;
    public HorizontalAlignment Align;
    public ComparerSort? Sort;

    // order rank amongst the other columns
    public int Rank;

    public ColumnInfo(string name, object value, ColumnGroup group = ColumnGroup.Row, int rank = -1, ComparerSort? sort = null)
    {
        Rank = rank;
        Sort = sort;
        if (sort is not null)
        {
            sort.Group = group;
        }

        switch (value)
        {
            case Tuple<int, HorizontalAlignment> values:
                Name = name;
                Width = values.Item1;
                Align = values.Item2;
                break;

            case Tuple<string, int, HorizontalAlignment> values:
                Name = values.Item1;
                Width = values.Item2;
                Align = values.Item3;
                break;

            case Tuple<string, int> values:
                Name = name;
                Width = values.Item2;
                break;

            case int width:
                Name = name;
                Width = width;
                break;

            case string:
            default:
                Name = name;
                Width = DEFAULT_WIDTH;
                break;
        }
    }
}

public class ColumnHeader
{
    public string Name = "";
    public string Text = "";
    public int Width;
    public HorizontalAlignment TextAlign;

    public ColumnHeader(string name, string text, int width = 75, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        Name = name;
        Width = width;
        TextAlign = align;
        Text = name;
    }

    public ColumnHeader() 
    { 
    }
}
