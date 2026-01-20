using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

using HackPDM.Abstractions;
using HackPDM.Domain.Representation;
using HackPDM.UI.Types;

using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;

namespace HackPDM.UI.Models;


public record HackLists
{
    public required DataGrid Entry { get; init; }
    public required DataGrid History { get; init; }
    public required DataGrid Parents { get; init; }
    public required DataGrid Children { get; init; }
    public required DataGrid Properties { get; init; }
    public required DataGrid Versions { get; init; }
    public ImmutableArray<DataGrid> AllLists
        => [ Entry, History, Parents, Children, Properties, Versions ];
    public ImmutableArray<DataGrid> SubLists
        => [ History, Parents, Children, Properties, Versions ];
}
public record HackItemsSource
{
    public required ImmutableArray<ObservableCollection<Types.EntryRow>> Entry { get; init; } = [];
    public required ImmutableArray<ObservableCollection<HistoryRow>> History { get; init; } = [];
    public required ImmutableArray<ObservableCollection<ParentRow>> Parents { get; init; } = [];
    public required ImmutableArray<ObservableCollection<ChildrenRow>> Children { get; init; } = [];
    public required ImmutableArray<ObservableCollection<PropertiesRow>> Properties { get; init; } = [];
    public required ImmutableArray<ObservableCollection<VersionRow>> Versions { get; init; } = [];
}

public static class GridMap
{
    public static BiMap<DataGrid, IList> Map { get; set; }
}