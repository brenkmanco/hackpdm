using System;
using System.Linq;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.Views.Common.Types;

public class ListDetail
{
    public ColumnInfo[] SortColumnOrder 
    { 
        get => field;
        set
        {
            if (value is not null and { Length: > 0 })
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
        set => field = value; 
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
        ColumnInfo col = columnSort.First(c => c.Name == activeColumn);
        SortRowOrder = col;
        ColumnGroup = col.Group;
    }

    public void SetRank(int rankFrom = 0, int rankTo = 0) 
        => (SortColumnOrder[rankTo], SortColumnOrder[rankFrom]) = (SortColumnOrder[rankFrom], SortColumnOrder[rankTo]);

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
