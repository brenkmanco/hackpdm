using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Core.Hack;

using HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using HackPDM.Abstractions;
using HackPDM.Shared.GlobalData;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.UI.Types;


public class ListDetail
{
    public ColumnInfo[] SortColumnOrder 
    { 
        get => field;
        set
        {
            if (value is not null and {Length: > 0})
            {
                for (int i = 0; i < value.Length; i++)
                {
                    value[i].Rank = i;
                }
            }
            field = value;
        } 
    }
    public ColumnInfo SortRowOrder
    {
        get => field;
        set
        {
            field = value;
        }
    }
    public ColumnGroup ColumnGroup { get; set; }
    public ListDetail(ColumnInfo[] columnSort, int indexOfSort = 0)
    {
        SortColumnOrder = columnSort;
        SortRowOrder = columnSort[indexOfSort];
        ColumnGroup = SortRowOrder.Group;
    }
    public ListDetail(ColumnInfo[] columnSort, string activeColumn = "ID")
    {
        SortColumnOrder = columnSort;
        ColumnInfo col = columnSort.First(col => col.Name == activeColumn);
        SortRowOrder = col;
        ColumnGroup = col.Group;
    }
    public void SetRank(int rankFrom = 0, int rankTo = 0) => (SortColumnOrder[rankTo], SortColumnOrder[rankFrom]) = (SortColumnOrder[rankFrom], SortColumnOrder[rankTo]);
    public void SetActiveColumn(int index = 0)
    {
        SortRowOrder = SortColumnOrder[index];
    }
    public void SetActiveColumn(string name = "ID")
    {
        SortRowOrder = SortColumnOrder.First(col => col.Name == name);
    }
    public void SetActiveColumn(ColumnInfo column)
    {
        if (SortColumnOrder.Contains(column))
        {
            SortRowOrder = column;
        }
    }
}
public class ColumnInfo
{
    public const int DEFAULT_WIDTH = 75;
    public readonly ColumnGroup Group;
    public string Name;
    public int Width;
    public HorizontalAlignment Align;
    public ComparerSort Sort;

    // order rank amongst the other columns
    public int Rank;

    public ColumnInfo(string name, object value, ColumnGroup group = ColumnGroup.Row, int rank = -1, ComparerSort sort = null)
    {
        Rank = rank;
        Sort = sort;
        sort?.Group = group;

        switch (value)
        {
            case Tuple<int, HorizontalAlignment> values:
                this.Name = name;
                this.Width = values.Item1;
                this.Align = values.Item2;
                break;

            case Tuple<string, int, HorizontalAlignment> values:
                this.Name = values.Item1;
                this.Width = values.Item2;
                this.Align = values.Item3;
                break;

            case Tuple<string, int> values:
                this.Name = name;
                this.Width = values.Item2;
                break;

            case int width:
                this.Name = name;
                this.Width = width;
                break;

            case string text:
            default:
                this.Name = name;
                this.Width = DEFAULT_WIDTH;
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
public struct FileCheck
{
    public readonly WatcherChangeTypes ChangeType { get; }
    public readonly string Name { get; }
    public readonly string CurrentPath { get; }
    public readonly string OldPath { get; }
    public HackFile Hack
    {
        get
        {
            if (CurrentPath is not null && ChangeType != (WatcherChangeTypes.Deleted | WatcherChangeTypes.All))
            {
                field ??= new HackFile(CurrentPath);
            }
            return field;
        }
    }
    public FileCheck(string name, string path, string oldPath = null, WatcherChangeTypes type = WatcherChangeTypes.All)
    {
        Name = name;
        ChangeType = type;
        CurrentPath = path;
        OldPath = oldPath;
    }
    public FileCheck(EventArgs e)
    {
        switch (e)
        {
            case RenamedEventArgs renamedEvent:
            {
                Name = renamedEvent.Name;
                ChangeType = WatcherChangeTypes.Renamed;
                CurrentPath = renamedEvent.FullPath;
                OldPath = renamedEvent.OldFullPath;
                break;
            }
            case FileSystemEventArgs fileEvent:
            {
                Name = fileEvent.Name;
                ChangeType = fileEvent.ChangeType;
                CurrentPath = fileEvent.FullPath;
                OldPath = null;
                break;
            }
            default:
            {
                Name = string.Empty;
                ChangeType = WatcherChangeTypes.All;
                CurrentPath = string.Empty;
                OldPath = string.Empty;
                Hack = null;
                break;
            }
        }
    }
        
    public override bool Equals(object obj)
    {

        if (obj is FileCheck other)
        {
            return Name == other.Name && CurrentPath == other.CurrentPath;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, CurrentPath);
    }

    public static bool operator ==(FileCheck left, FileCheck right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FileCheck left, FileCheck right)
    {
        return !(left == right);
    }

    public static string FindCommonPath(IEnumerable<FileCheck> fileChecks)
    {
        if (fileChecks == null || !fileChecks.Any())
            return string.Empty;

        var paths = fileChecks.Select(fc => fc.CurrentPath).Where(p => !string.IsNullOrEmpty(p)).ToList();
            
        if (!paths.Any())
            return string.Empty;

        // Find the common path
        var commonPath = paths[0];
        foreach (var path in paths.Skip(1))
        {
            while (!path.StartsWith(commonPath, StringComparison.OrdinalIgnoreCase))
            {
                commonPath = Path.GetDirectoryName(commonPath);
                if (commonPath == null)
                    return string.Empty;
            }
        }
        return commonPath;
    }
}
public class ComparerSort : IComparer
{
    public delegate int CompareFunction(object x, object y);
    public CompareFunction ComparerFunction;
    private ListDetail _listDetail;
    public ColumnGroup Group;
    public bool IsAscending;
    public bool InvalidsAtBack;
    public SortPredefined SortType;

    // public ComparerSort() => Init(DefaultComparer, false, SortPredefined.Int, true);
    // public ComparerSort(CompareFunction func, bool isAscending = false, SortPredefined sortType = SortPredefined.Int, bool invalidsAtBack = true, ListDetail listDetail = null)
    //     => Init(func, isAscending, sortType, invalidsAtBack);
    // public ComparerSort(bool isAscending = false, bool invalidsAtBack = true)
    //     => Init(DefaultComparer, isAscending, SortPredefined.Int, invalidsAtBack);
    // public ComparerSort(SortPredefined sortType, bool isAscending = false, bool invalidsAtBack = true) 
    //     => Init(DefaultComparer, isAscending, sortType, invalidsAtBack);
    // private void Init(CompareFunction func, bool isAscending = false, SortPredefined sortType = SortPredefined.Int, bool invalidsAtBack = true, ListDetail listDetail = null) 
    // {
    //     this.ComparerFunction = func;
    //     this.IsAscending = isAscending;
    //     this.SortType = sortType;
    //     this.InvalidsAtBack = invalidsAtBack;
    //     if (listDetail is null) this._listDetail = ColumnMap.RowWidths;
    // }
    public int Compare(object x, object y)
    {
        int result = ComparerFunction(x, y);
        return Math.Max(-1, Math.Min(1, result));
    }
}

