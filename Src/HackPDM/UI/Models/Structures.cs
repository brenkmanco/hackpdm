using System.Collections.Immutable;
using DataGrid = CommunityToolkit.WinUI.UI.Controls.DataGrid;

namespace HackPDM.UI.Models;


internal record HackLists
{
    internal required DataGrid Entry { get; init; }
    internal required DataGrid History { get; init; }
    internal required DataGrid Parents { get; init; }
    internal required DataGrid Children { get; init; }
    internal required DataGrid Properties { get; init; }
    internal required DataGrid Versions { get; init; }
    internal ImmutableArray<DataGrid> AllLists
        => [ Entry, History, Parents, Children, Properties, Versions ];
    internal ImmutableArray<DataGrid> SubLists
        => [ History, Parents, Children, Properties, Versions ];
}