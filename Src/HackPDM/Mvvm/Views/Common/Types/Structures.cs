using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

using HackPDM.Abstractions;
using HackPDM.Domain.Representation;
using HackPDM.UI.Types;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Mvvm.Views.Common.Types;

using TableView = WinUI.TableView.TableView;

namespace HackPDM.Mvvm.Views.Common.Types;


public record HackLists
{
    public required TableView Entry { get; init; }
    public required TableView History { get; init; }
    public required TableView Parents { get; init; }
    public required TableView Children { get; init; }
    public required TableView Properties { get; init; }
    public required TableView Versions { get; init; }
    public ImmutableArray<TableView> AllLists
        => [ Entry, History, Parents, Children, Properties, Versions ];
    public ImmutableArray<TableView> SubLists
        => [ History, Parents, Children, Properties, Versions ];
}
public record HackItemsSource
{
    public required ImmutableArray<ObservableCollection<HackPDM.Mvvm.Models.Hack.EntryRow>> Entry { get; init; } = [];
    public required ImmutableArray<ObservableCollection<HistoryRow>> History { get; init; } = [];
    public required ImmutableArray<ObservableCollection<ParentRow>> Parents { get; init; } = [];
    public required ImmutableArray<ObservableCollection<ChildrenRow>> Children { get; init; } = [];
    public required ImmutableArray<ObservableCollection<PropertiesRow>> Properties { get; init; } = [];
    public required ImmutableArray<ObservableCollection<VersionRow>> Versions { get; init; } = [];
}

public static class GridMap
{
    public static BiMap<TableView, IList> Map { get; set; }
}