using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using HackPDM.Hack;
using HackPDM.Properties;
using HackPDM.Src;
using HackPDM.Forms.Hack;

using Microsoft.UI.Xaml;

using static System.Net.Mime.MediaTypeNames;

using HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;

namespace HackPDM.Src.ClientUtils.Types;

public class NotifyIcon
{
    public ToolTipIcon BalloonTipIcon;
    public string? BalloonTipText;
    public string? BalloonTipTitle;
    public string? Text;
    public System.Drawing.Icon? Icon;
    public void ShowBalloonTip(int timeout) { }
}
public enum ToolTipIcon
{
    None,
    Info,
    Warning,
    Error
}
public class Notifier
{
    private static CancellationTokenSource _fileSystemCancel = new();
    public static ConcurrentQueue<FileCheck> QueueFileCheck = new();
    public static DirectoryInfo? Directory;
    public static FileSystemWatcher? FileWatcher { get; set; }
    public static NotifyIcon Notify { get; set; } = new();
    public static bool IsRunning { get; private set; } = false;
    public static bool IsInvalidDirectory  { get; private set; } = true;
    static Notifier()
    {
        try
        {
            FileWatcher = null;
            FileWatcher = new()
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.CreationTime | NotifyFilters.Attributes,
                Path = StorageBox.PwaPathAbsolute ?? "",
                EnableRaisingEvents = true,
            };
            FileWatcher.Created += (s, e) => QueueFileCheck.Enqueue(new FileCheck(e));
            FileWatcher.Deleted += (s, e) => QueueFileCheck.Enqueue(new FileCheck(e));
            FileWatcher.Changed += (s, e) => QueueFileCheck.Enqueue(new FileCheck(e));
            FileWatcher.Renamed += (s, e) => QueueFileCheck.Enqueue(new FileCheck(e));
            FileWatcher.EnableRaisingEvents = true;
            Directory = new(StorageBox.PwaPathAbsolute ?? "");
            IsInvalidDirectory = Directory?.Exists ?? false;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
    public static void CancelCheckLoop()
    {
        if (IsRunning)
        {
            _fileSystemCancel.Cancel();
        }
    }
    public async static void FileCheckLoop()
    {
        if (IsRunning) return;
        Debug.WriteLine("file check loop was started");

        CancellationToken cToken = _fileSystemCancel.Token;
        IsRunning = true;
        try
        {
            while (!cToken.IsCancellationRequested)
            {
                // Notify is not null &&
                if (QueueFileCheck.Count == 1)
                {
                    if (QueueFileCheck.TryDequeue(out FileCheck fileCheck)) fileCheck.Notify();
                }
                // Notify is not null &&
                else if (QueueFileCheck.Count > 1)
                {
                    //string commonPath = FileCheck.FindCommonPath(QueueFileCheck);
                    FileCheck.Notify("Files Changed", $"{QueueFileCheck.Count} files were changed");
                }

                QueueFileCheck = new(); // clear the queue
                await Task.Delay(2000, cToken);
            }
        }
        catch
        {
            Debug.WriteLine("file check loop was cancelled");
        }
        _fileSystemCancel.Dispose();
        _fileSystemCancel = new();
        IsRunning = false;
    }
}
// List Views Column Name and Widths
public static class NameConfig
{
    public static readonly ColumnInfo RowId                                 = new("ID",                 75,         ColumnGroup.Row,        0, new(SortPredefined.Int,      true, true));                                                                    
    public static readonly ColumnInfo RowName                               = new("Name",               300,        ColumnGroup.Row,        1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo RowType                               = new("Type",               120,        ColumnGroup.Row,        2, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo RowSize                               = new("Size",               new Tuple<int, HorizontalAlignment>(100, HorizontalAlignment.Right), ColumnGroup.Row, 3, new(SortPredefined.Int, true, true));
    public static readonly ColumnInfo RowStatus                             = new("Status",             75,         ColumnGroup.Row,        4, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo RowCheckOut                           = new("CheckOut",           120,        ColumnGroup.Row,        5, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo RowCategory                           = new("Category",           110,        ColumnGroup.Row,        6, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo RowLocalDate                          = new("Local Date",         150,        ColumnGroup.Row,        7, new(SortPredefined.Date,     true, true));
    public static readonly ColumnInfo RowRemoteDate                         = new("Remote Date",        150,        ColumnGroup.Row,        8, new(SortPredefined.Date,     true, true));
    public static readonly ColumnInfo RowFullName                           = new("FullName",           100,        ColumnGroup.Row,        9, new(SortPredefined.String,   true, true));
               
    public static readonly ColumnInfo HistoryVersion                        = new("Version",            50,         ColumnGroup.History,    0, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo HistoryModUser                        = new("ModUser",            140,        ColumnGroup.History,    1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo HistoryModDate                        = new("ModDate",            140,        ColumnGroup.History,    2, new(SortPredefined.Date,     true, true));
    public static readonly ColumnInfo HistorySize                           = new("Size",               75,         ColumnGroup.History,    3, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo HistoryRelDate                        = new("RelDate",            75,         ColumnGroup.History,    4, new(SortPredefined.Date,     true, true));
               
    public static readonly ColumnInfo ParentVersion                         = new("Version",            50,         ColumnGroup.Parent,     0, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo ParentName                            = new("Name",               400,        ColumnGroup.Parent,     1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo ParentBasePath                        = new("Base Path",          600,        ColumnGroup.Parent,     2, new(SortPredefined.String,   true, true));

    public static readonly ColumnInfo ChildrenVersion                       = new("Version",            50,         ColumnGroup.Child,      0, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo ChildrenName                          = new("Name",               400,        ColumnGroup.Child,      1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo ChildrenBasePath                      = new("Base Path",          600,        ColumnGroup.Child,      2, new(SortPredefined.Date,     true, true));
                
    public static readonly ColumnInfo PropertiesVersion                     = new("Version",            50,         ColumnGroup.Property,   0, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo PropertiesConfiguration               = new("Configuration",      100,        ColumnGroup.Property,   1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo PropertiesName                        = new("Name",               100,        ColumnGroup.Property,   2, new(SortPredefined.Date,     true, true));
    public static readonly ColumnInfo PropertiesProperty                    = new("Property",           50,         ColumnGroup.Property,   3, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo PropertiesType                        = new("Type",               75,         ColumnGroup.Property,   4, new(SortPredefined.Date,     true, true));
    public static readonly ColumnInfo PropertiesValue                       = new("Value",              400,        ColumnGroup.Property,   5, new(SortPredefined.String,   true, true));
                
    public static readonly ColumnInfo VersionId                             = new("ID",                 75,         ColumnGroup.Version,    0, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo VersionName                           = new("Name",               300,        ColumnGroup.Version,    1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo VersionFileSize                       = new("File Size",          100,        ColumnGroup.Version,    2, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo VersionDirectoryId                    = new("Directory ID",       75,         ColumnGroup.Version,    3, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo VersionNodeId                         = new("Node ID",            75,         ColumnGroup.Version,    4, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo VersionEntryId                        = new("Entry ID",           75,         ColumnGroup.Version,    5, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo VersionAttachmentId                   = new("Attachment ID",      75,         ColumnGroup.Version,    6, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo VersionModifyDate                     = new("Modify Date",        120,        ColumnGroup.Version,    7, new(SortPredefined.Date,     true, true));
    public static readonly ColumnInfo VersionChecksum                       = new("Checksum",           300,        ColumnGroup.Version,    8, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo VersionOdooCompletePath               = new("Odoo Complete path", 300,        ColumnGroup.Version,    9, new(SortPredefined.String,   true, true));

    public static readonly ColumnInfo SearchId                              = new("ID",                 10,         ColumnGroup.Search,     0, new(SortPredefined.Int,      true, true));
    public static readonly ColumnInfo SearchName                            = new("Name",               25,         ColumnGroup.Search,     1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo SearchDirectory                       = new("Directory",          0,          ColumnGroup.Search,     2, new(SortPredefined.String,   true, true));

    public static readonly ColumnInfo SearchPropName                        = new("Name",               30,         ColumnGroup.SearchProp, 0, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo SearchPropEqual                       = new("Comparer",           15,         ColumnGroup.SearchProp, 1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo SearchPropValue                       = new("Value",              0,          ColumnGroup.SearchProp, 2, new(SortPredefined.String,   true, true));

    public static readonly ColumnInfo FileTypeExtension                     = new("Extension",          15,         ColumnGroup.FileType,   0, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo FileTypeCategory                      = new("Category",           10,         ColumnGroup.FileType,   1, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo FileTypeRegEx                         = new("RegEx",              18,         ColumnGroup.FileType,   2, new(SortPredefined.String,   true, true));
    public static readonly ColumnInfo FileTypeDescription                   = new("Description",        0,          ColumnGroup.FileType,   3, new(SortPredefined.String,   true, true));

    public static readonly ColumnInfo FileTypeEntryFilterId                 = new("ID",                 75,         ColumnGroup.FileTypeEntryFilter, 0, new(SortPredefined.Int,     true, true));
    public static readonly ColumnInfo FileTypeEntryFilterProto              = new("Proto",              100,        ColumnGroup.FileTypeEntryFilter, 1, new(SortPredefined.String,  true, true));
    public static readonly ColumnInfo FileTypeEntryFilterRegEx              = new("RegEx",              100,        ColumnGroup.FileTypeEntryFilter, 2, new(SortPredefined.String,  true, true));
    public static readonly ColumnInfo FileTypeEntryFilterDescription        = new("Description",        500,        ColumnGroup.FileTypeEntryFilter, 3, new(SortPredefined.String,  true, true));

    public static readonly ColumnInfo FileTypeLocExt                        = new("Extension",          15,         ColumnGroup.FileTypeLoc, 0, new(SortPredefined.String,  true, true));
    public static readonly ColumnInfo FileTypeLocStatus                     = new("Status",             21,         ColumnGroup.FileTypeLoc, 1, new(SortPredefined.String,  true, true));
    public static readonly ColumnInfo FileTypeLocExample                    = new("Example",            0,          ColumnGroup.FileTypeLoc, 2, new(SortPredefined.String,  true, true));
                
    public static readonly ColumnInfo FileTypeLocDatExt                     = new("Extension",          75,         ColumnGroup.FileTypeLocDat, 0, new(SortPredefined.String,  true, true));
    public static readonly ColumnInfo FileTypeLocDatReg                     = new("RegEx",              100,        ColumnGroup.FileTypeLocDat, 1, new(SortPredefined.String,  true, true));
    public static readonly ColumnInfo FileTypeLocDatCat                     = new("Category",           100,        ColumnGroup.FileTypeLocDat, 2, new(SortPredefined.String,  true, true));
    public static readonly ColumnInfo FileTypeLocDatDes                     = new("Description",        300,        ColumnGroup.FileTypeLocDat, 3, new(SortPredefined.String,  true, true));
    public static readonly ColumnInfo FileTypeLocDatIco                     = new("Icon",               100,        ColumnGroup.FileTypeLocDat, 4, new(SortPredefined.Unknown, true, true));
    public static readonly ColumnInfo FileTypeLocDatIcoCancel               = new("Remove Icon",        75,         ColumnGroup.FileTypeLocDat, 5, new(SortPredefined.Unknown, true, true));

}
public static class ColumnMap
{
    public static readonly ListDetail RowWidths                  = new(
    [
        NameConfig.RowId,
        NameConfig.RowName,
        NameConfig.RowType,
        NameConfig.RowSize,
        NameConfig.RowStatus,
        NameConfig.RowCheckOut,
        NameConfig.RowCategory,
        NameConfig.RowLocalDate,
        NameConfig.RowRemoteDate,
        NameConfig.RowFullName
    ], "ID");
    public static readonly ListDetail HistoryRows                = new(
    [
        NameConfig.HistoryVersion,
        NameConfig.HistoryModUser,
        NameConfig.HistoryModDate,
        NameConfig.HistorySize,
        NameConfig.HistoryRelDate
    ], "Version");
    public static readonly ListDetail ParentRows                 = new(
    [
        NameConfig.ParentVersion,
        NameConfig.ParentName,
        NameConfig.ParentBasePath
    ], "Version");
    public static readonly ListDetail ChildrenRows               = new(
    [
        NameConfig.ChildrenVersion,
        NameConfig.ChildrenName,
        NameConfig.ChildrenBasePath
    ], "Version");
    public static readonly ListDetail PropertiesRows             = new(
    [
        NameConfig.PropertiesVersion,
        NameConfig.PropertiesConfiguration,
        NameConfig.PropertiesName,
        NameConfig.PropertiesProperty,
        NameConfig.PropertiesType,
        NameConfig.PropertiesValue
    ], "Version");
    public static readonly ListDetail VersionInfoRows            = new(
    [
        NameConfig.VersionId,
        NameConfig.VersionName,
        NameConfig.VersionFileSize,
        NameConfig.VersionDirectoryId,
        NameConfig.VersionNodeId,
        NameConfig.VersionEntryId,
        NameConfig.VersionAttachmentId,
        NameConfig.VersionModifyDate,
        NameConfig.VersionChecksum,
        NameConfig.VersionOdooCompletePath
    ], "ID");
    public static readonly ListDetail SearchRows                 = new(
    [
        NameConfig.SearchId,
        NameConfig.SearchName,
        NameConfig.SearchDirectory
    ], "ID");
    public static readonly ListDetail SearchPropRows             = new(
    [
        NameConfig.SearchPropName,
        NameConfig.SearchPropEqual,
        NameConfig.SearchPropValue
    ], "Name");
    public static readonly ListDetail FileTypeRows               = new(
    [
        NameConfig.FileTypeExtension,
        NameConfig.FileTypeCategory,
        NameConfig.FileTypeRegEx,
        NameConfig.FileTypeDescription
    ], "Extension");
    public static readonly ListDetail FileTypeEntryFilterRows    = new(
    [
        NameConfig.FileTypeEntryFilterId,
        NameConfig.FileTypeEntryFilterProto,
        NameConfig.FileTypeEntryFilterRegEx,
        NameConfig.FileTypeEntryFilterDescription
    ], "ID");
    public static readonly ListDetail FileTypeLocRows            = new(
    [
        NameConfig.FileTypeLocExt,
        NameConfig.FileTypeLocStatus,
        NameConfig.FileTypeLocExample
    ], "Extension");
    public static readonly ListDetail FileTypeLocDatRows         = new(
    [
        NameConfig.FileTypeLocDatExt,
        NameConfig.FileTypeLocDatReg,
        NameConfig.FileTypeLocDatCat,
        NameConfig.FileTypeLocDatDes,
        NameConfig.FileTypeLocDatIco,
        NameConfig.FileTypeLocDatIcoCancel
    ], "Extension");

    static ColumnMap()
    {
        RowWidths.ColumnGroup = ColumnGroup.Row;
        HistoryRows.ColumnGroup = ColumnGroup.History;
        ParentRows.ColumnGroup = ColumnGroup.Parent;
        ChildrenRows.ColumnGroup = ColumnGroup.Child;
        PropertiesRows.ColumnGroup = ColumnGroup.Property;
        VersionInfoRows.ColumnGroup = ColumnGroup.Version;
        SearchRows.ColumnGroup = ColumnGroup.Search;
        SearchPropRows.ColumnGroup = ColumnGroup.SearchProp;
        FileTypeRows.ColumnGroup = ColumnGroup.FileType;
        FileTypeLocRows.ColumnGroup = ColumnGroup.FileTypeLoc;
        FileTypeLocDatRows.ColumnGroup = ColumnGroup.FileTypeLocDat;
    }
}
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
    public static ListDetail GetListDetail(ColumnGroup group) => group switch
    {
        ColumnGroup.Row                 => ColumnMap.RowWidths,
        ColumnGroup.History             => ColumnMap.HistoryRows,
        ColumnGroup.Parent              => ColumnMap.ParentRows,
        ColumnGroup.Child               => ColumnMap.ChildrenRows,
        ColumnGroup.Property            => ColumnMap.PropertiesRows,
        ColumnGroup.Version             => ColumnMap.VersionInfoRows,
        ColumnGroup.Search              => ColumnMap.SearchRows,
        ColumnGroup.SearchProp          => ColumnMap.SearchPropRows,
        ColumnGroup.FileType            => ColumnMap.FileTypeRows,
        ColumnGroup.FileTypeEntryFilter => ColumnMap.FileTypeEntryFilterRows,
        ColumnGroup.FileTypeLoc         => ColumnMap.FileTypeLocRows,
        ColumnGroup.FileTypeLocDat      => ColumnMap.FileTypeLocDatRows,
        _                               => ColumnMap.RowWidths,
    };
}
public class ColumnInfo<T> { }
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
    public void Notify()
        => Notify($"File {Enum.GetName(typeof(WatcherChangeTypes), ChangeType)}", $"File: {Name}");
    public static void Notify(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        Notifier.Notify.BalloonTipTitle = title;
        Notifier.Notify.BalloonTipText = text;
        Notifier.Notify.Text = text[0..Math.Min(text.Length, 62)];
        Notifier.Notify.BalloonTipIcon = icon;
        Notifier.Notify.Icon = Resources.hackpdm_icon;
            
        Notifier.Notify.ShowBalloonTip(2000); // Show for 3 seconds
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

    public ComparerSort() => Init(DefaultComparer, false, SortPredefined.Int, true);
    public ComparerSort(CompareFunction func, bool isAscending = false, SortPredefined sortType = SortPredefined.Int, bool invalidsAtBack = true, ListDetail listDetail = null)
        => Init(func, isAscending, sortType, invalidsAtBack);
    public ComparerSort(bool isAscending = false, bool invalidsAtBack = true)
        => Init(DefaultComparer, isAscending, SortPredefined.Int, invalidsAtBack);
    public ComparerSort(SortPredefined sortType, bool isAscending = false, bool invalidsAtBack = true) 
        => Init(DefaultComparer, isAscending, sortType, invalidsAtBack);
    private void Init(CompareFunction func, bool isAscending = false, SortPredefined sortType = SortPredefined.Int, bool invalidsAtBack = true, ListDetail listDetail = null) 
    {
        this.ComparerFunction = func;
        this.IsAscending = isAscending;
        this.SortType = sortType;
        this.InvalidsAtBack = invalidsAtBack;
        if (listDetail is null) this._listDetail = ColumnMap.RowWidths;
    }
    public int Compare(object x, object y)
    {
        int result = ComparerFunction(x, y);
        return Math.Max(-1, Math.Min(1, result));
    }
    private int DefaultComparer(object x, object y)
    {
        _listDetail = ListDetail.GetListDetail(Group);
        string xItem = (x as ListViewItem).SubItems[_listDetail.SortRowOrder.Name].Text;
        string yItem = (y as ListViewItem).SubItems[_listDetail.SortRowOrder.Name].Text;

        switch (SortType)
        {
            case SortPredefined.String:
            {
                return 
                    xItem is not null ? 
                        yItem is not null ? 
                            xItem == StorageBox.EMPTY_PLACEHOLDER ? 
                                yItem == StorageBox.EMPTY_PLACEHOLDER ?
                                    0
                                    : AscOrDesc()
                                : SwitchAsc(string.Compare(xItem.ToUpper(), yItem.ToUpper())) 
                            : AscOrDesc()
                        : yItem is not null? 
                            AscOrDesc() //
                            : 0;
            }
            case SortPredefined.Int:
            {
                if (xItem is not null && int.TryParse(xItem, out int xint))
                {
                    if (int.TryParse(yItem, out int yint))
                    {
                        return
                            xint > yint ? 
                                SwitchAsc(1)
                                : xint < yint ?
                                    SwitchAsc(-1)
                                    : 0;
                    }
                    else
                    {
                        return AscOrDesc();
                    }
                }
                else
                {
                    if (int.TryParse(yItem, out int _))
                    {
                        return AscOrDesc();//
                    }
                    return 0;
                }
            }
            case SortPredefined.Date:
            {
                return 
                    DateTime.TryParse(xItem, out DateTime xresult) ?
                        DateTime.TryParse(yItem, out DateTime yresult) ? 
                            SwitchAsc(DateTime.Compare(xresult, yresult)) 
                            : AscOrDesc()
                        : DateTime.TryParse(yItem, out DateTime _) ?
                            AscOrDesc()//
                            : 0;
            }
            case SortPredefined.Unknown: return 0;
            default:
            {
                return SwitchAsc(xItem.CompareTo(yItem));
            }
        }
    }
    private int AscOrDesc(bool reverse = false)
        => IsAscending ^ InvalidsAtBack ^ reverse ? 1 : -1;
    private int SwitchAsc(int i = 0, bool reverse = false)
    {
        if (i == 0) return 0;
        // isAscending true : -1 
        // reverse false : -1
            
        bool comp = IsAscending ^ reverse;

        return comp ? i : i * -1;
    }
}