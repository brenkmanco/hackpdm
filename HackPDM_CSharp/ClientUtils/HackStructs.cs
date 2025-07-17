using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static System.Net.Mime.MediaTypeNames;

namespace HackPDM.ClientUtils
{
    // List Views Column Name and Widths
    public static class NameConfig
    {
        public static readonly ColumnInfo RowID                                 = new("ID",                 75,         ColumnGroup.Row);                                                                    
        public static readonly ColumnInfo RowName                               = new("Name",               300,        ColumnGroup.Row);
        public static readonly ColumnInfo RowType                               = new("Type",               120,        ColumnGroup.Row);
        public static readonly ColumnInfo RowSize                               = new("Size",               new Tuple<int, HorizontalAlignment>(100, HorizontalAlignment.Right), ColumnGroup.Row);
        public static readonly ColumnInfo RowStatus                             = new("Status",             75,         ColumnGroup.Row);
        public static readonly ColumnInfo RowCheckOut                           = new("CheckOut",           120,        ColumnGroup.Row);
        public static readonly ColumnInfo RowCategory                           = new("Category",           110,        ColumnGroup.Row);
        public static readonly ColumnInfo RowLocalDate                          = new("Local Date",         150,        ColumnGroup.Row);
        public static readonly ColumnInfo RowRemoteDate                         = new("Remote Date",        150,        ColumnGroup.Row);
        public static readonly ColumnInfo RowFullName                           = new("FullName",           100,        ColumnGroup.Row);
               
        public static readonly ColumnInfo HistoryVersion                        = new("Version",            50,         ColumnGroup.History);
        public static readonly ColumnInfo HistoryModUser                        = new("ModUser",            140,        ColumnGroup.History);
        public static readonly ColumnInfo HistoryModDate                        = new("ModDate",            140,        ColumnGroup.History);
        public static readonly ColumnInfo HistorySize                           = new("Size",               75,         ColumnGroup.History);
        public static readonly ColumnInfo HistoryRelDate                        = new("RelDate",            75,         ColumnGroup.History);
               
        public static readonly ColumnInfo ParentVersion                         = new("Version",            50,         ColumnGroup.Parent);
        public static readonly ColumnInfo ParentName                            = new("Name",               400,        ColumnGroup.Parent);
        public static readonly ColumnInfo ParentBasePath                        = new("Base Path",          600,        ColumnGroup.Parent);
                
        public static readonly ColumnInfo ChildrenVersion                       = new("Version",            50,         ColumnGroup.Child);
        public static readonly ColumnInfo ChildrenName                          = new("Name",               400,        ColumnGroup.Child);
        public static readonly ColumnInfo ChildrenBasePath                      = new("Base Path",          600,        ColumnGroup.Child);
                
        public static readonly ColumnInfo PropertiesVersion                     = new("Version",            50,         ColumnGroup.Property);
        public static readonly ColumnInfo PropertiesConfiguration               = new("Configuration",      100,        ColumnGroup.Property);
        public static readonly ColumnInfo PropertiesName                        = new("Name",               100,        ColumnGroup.Property);
        public static readonly ColumnInfo PropertiesProperty                    = new("Property",           50,         ColumnGroup.Property);
        public static readonly ColumnInfo PropertiesType                        = new("Type",               75,         ColumnGroup.Property);
        public static readonly ColumnInfo PropertiesValue                       = new("Value",              400,        ColumnGroup.Property);
                
        public static readonly ColumnInfo VersionID                             = new("ID",                 75,         ColumnGroup.Version);
        public static readonly ColumnInfo VersionName                           = new("Name",               300,        ColumnGroup.Version);
        public static readonly ColumnInfo VersionFileSize                       = new("File Size",          100,        ColumnGroup.Version);
        public static readonly ColumnInfo VersionDirectoryID                    = new("Directory ID",       75,         ColumnGroup.Version);
        public static readonly ColumnInfo VersionNodeID                         = new("Node ID",            75,         ColumnGroup.Version);
        public static readonly ColumnInfo VersionEntryID                        = new("Entry ID",           75,         ColumnGroup.Version);
        public static readonly ColumnInfo VersionAttachmentID                   = new("Attachment ID",      75,         ColumnGroup.Version);
        public static readonly ColumnInfo VersionModifyDate                     = new("Modify Date",        120,        ColumnGroup.Version);
        public static readonly ColumnInfo VersionChecksum                       = new("Checksum",           300,        ColumnGroup.Version);
        public static readonly ColumnInfo VersionOdooCompletePath               = new("Odoo Complete path", 300,        ColumnGroup.Version);
                
        public static readonly ColumnInfo SearchID                              = new("ID",                 10,         ColumnGroup.Search);
        public static readonly ColumnInfo SearchName                            = new("Name",               25,         ColumnGroup.Search);
        public static readonly ColumnInfo SearchDirectory                       = new("Directory",          0,          ColumnGroup.Search);
                
        public static readonly ColumnInfo SearchPropName                        = new("Name",               30,         ColumnGroup.SearchProp);
        public static readonly ColumnInfo SearchPropEqual                       = new("Comparer",           15,         ColumnGroup.SearchProp);
        public static readonly ColumnInfo SearchPropValue                       = new("Value",              0,          ColumnGroup.SearchProp);
                
        public static readonly ColumnInfo FileTypeExtension                     = new("Extension",          15,         ColumnGroup.FileType);
        public static readonly ColumnInfo FileTypeCategory                      = new("Category",           10,         ColumnGroup.FileType);
        public static readonly ColumnInfo FileTypeRegEx                         = new("RegEx",              18,         ColumnGroup.FileType);
        public static readonly ColumnInfo FileTypeDescription                   = new("Description",        0,          ColumnGroup.FileType);
                
        public static readonly ColumnInfo FileTypeEntryFilterID                 = new("ID",                 75,         ColumnGroup.FileTypeEntryFilter);
        public static readonly ColumnInfo FileTypeEntryFilterProto              = new("Proto",              100,        ColumnGroup.FileTypeEntryFilter);
        public static readonly ColumnInfo FileTypeEntryFilterRegEx              = new("RegEx",              100,        ColumnGroup.FileTypeEntryFilter);
        public static readonly ColumnInfo FileTypeEntryFilterDescription        = new("Description",        500,        ColumnGroup.FileTypeEntryFilter);
                
        public static readonly ColumnInfo FileTypeLocExt                        = new("Extension",          15,         ColumnGroup.FileTypeLoc);
        public static readonly ColumnInfo FileTypeLocStatus                     = new("Status",             21,         ColumnGroup.FileTypeLoc);
        public static readonly ColumnInfo FileTypeLocExample                    = new("Example",            0,          ColumnGroup.FileTypeLoc);
                
        public static readonly ColumnInfo FileTypeLocDatExt                     = new("Extension",          75,         ColumnGroup.FileTypeLoc);
        public static readonly ColumnInfo FileTypeLocDatReg                     = new("RegEx",              100,        ColumnGroup.FileTypeLoc);
        public static readonly ColumnInfo FileTypeLocDatCat                     = new("Category",           100,        ColumnGroup.FileTypeLoc);
        public static readonly ColumnInfo FileTypeLocDatDes                     = new("Description",        300,        ColumnGroup.FileTypeLoc);
        public static readonly ColumnInfo FileTypeLocDatIco                     = new("Icon",               100,        ColumnGroup.FileTypeLoc);
        public static readonly ColumnInfo FileTypeLocDatIcoCancel               = new("Remove Icon",        75,         ColumnGroup.FileTypeLoc);

    }
    public static class ColumnMap
    {
        public static readonly ColumnInfo[] RowWidths =
        [
            NameConfig.RowID,
            NameConfig.RowName,
            NameConfig.RowType,
            NameConfig.RowSize,
            NameConfig.RowStatus,
            NameConfig.RowCheckOut,
            NameConfig.RowCategory,
            NameConfig.RowLocalDate,
            NameConfig.RowRemoteDate,
            NameConfig.RowFullName
        ];
        public static readonly ColumnInfo[] HistoryRows =
        [
            NameConfig.HistoryVersion,
            NameConfig.HistoryModUser,
            NameConfig.HistoryModDate,
            NameConfig.HistorySize,
            NameConfig.HistoryRelDate
        ];
        public static readonly ColumnInfo[] ParentRows =
        [
            NameConfig.ParentVersion,
            NameConfig.ParentName,
            NameConfig.ParentBasePath
        ];
        public static readonly ColumnInfo[] ChildrenRows =
        [
            NameConfig.ChildrenVersion,
            NameConfig.ChildrenName,
            NameConfig.ChildrenBasePath
        ];
        public static readonly ColumnInfo[] PropertiesRows =
        [
            NameConfig.PropertiesVersion,
            NameConfig.PropertiesConfiguration,
            NameConfig.PropertiesName,
            NameConfig.PropertiesProperty,
            NameConfig.PropertiesType,
            NameConfig.PropertiesValue
        ];
        public static readonly ColumnInfo[] VersionInfoRows =
        [
            NameConfig.VersionID,
            NameConfig.VersionName,
            NameConfig.VersionFileSize,
            NameConfig.VersionDirectoryID,
            NameConfig.VersionNodeID,
            NameConfig.VersionEntryID,
            NameConfig.VersionAttachmentID,
            NameConfig.VersionModifyDate,
            NameConfig.VersionChecksum,
            NameConfig.VersionOdooCompletePath
        ];
        public static readonly ColumnInfo[] SearchRows =
        [
            NameConfig.SearchID,
            NameConfig.SearchName,
            NameConfig.SearchDirectory
        ];
        public static readonly ColumnInfo[] SearchPropRows =
        [
            NameConfig.SearchPropName,
            NameConfig.SearchPropEqual,
            NameConfig.SearchPropValue
        ];
        public static readonly ColumnInfo[] FileTypeRows =
        [
            NameConfig.FileTypeExtension,
            NameConfig.FileTypeCategory,
            NameConfig.FileTypeRegEx,
            NameConfig.FileTypeDescription
        ];
        public static readonly ColumnInfo[] FileTypeEntryFilterRows =
        [
            NameConfig.FileTypeEntryFilterID,
            NameConfig.FileTypeEntryFilterProto,
            NameConfig.FileTypeEntryFilterRegEx,
            NameConfig.FileTypeEntryFilterDescription
        ];
        public static readonly ColumnInfo[] FileTypeLocRows =
        [
            NameConfig.FileTypeLocExt,
            NameConfig.FileTypeLocStatus,
            NameConfig.FileTypeLocExample
        ];
        public static readonly ColumnInfo[] FileTypeLocDatRows =
        [
            NameConfig.FileTypeLocDatExt,
            NameConfig.FileTypeLocDatReg,
            NameConfig.FileTypeLocDatCat,
            NameConfig.FileTypeLocDatDes,
            NameConfig.FileTypeLocDatIco,
            NameConfig.FileTypeLocDatIcoCancel
        ];
    }
    public readonly struct ColumnInfo
    {
        public const int DefaultWidth = 75;
        public readonly string Name;
        public readonly int Width;
        public readonly ColumnGroup Group;
        public readonly ColumnHeader Header;

        public ColumnInfo(string Name, object value, ColumnGroup group = ColumnGroup.Row)
        {
            switch (value)
            {
                case ColumnHeader column:
                    this.Name = column.Name;
                    this.Width = column.Width;
                    this.Header = column;
                    break;

                case Tuple<int, HorizontalAlignment> values:
                    this.Name = Name;
                    this.Width = values.Item1;
                    this.Header = new ColumnHeader
                    {
                        Name = Name,
                        Text = Name,
                        Width = values.Item1,
                        TextAlign = values.Item2
                    };
                    break;

                case Tuple<string, int, HorizontalAlignment> values:
                    this.Name = values.Item1;
                    this.Width = values.Item2;
                    this.Header = new ColumnHeader
                    {
                        Name = Name,
                        Text = values.Item1,
                        Width = values.Item2,
                        TextAlign = values.Item3
                    };
                    break;

                case Tuple<string, int> values:
                    this.Name = Name;
                    this.Width = values.Item2;
                    this.Header = new ColumnHeader
                    {
                        Name = Name,
                        Text = values.Item1,
                        Width = values.Item2,
                        TextAlign = HorizontalAlignment.Left
                    };
                    break;

                case int width:
                    this.Name = Name;
                    this.Width = width;
                    this.Header = new ColumnHeader
                    {
                        Name = Name,
                        Text = Name,
                        Width = width,
                        TextAlign = HorizontalAlignment.Left
                    };
                    break;

                case string text:
                    this.Name = Name;
                    this.Width = DefaultWidth;
                    this.Header = new ColumnHeader
                    {
                        Name = Name,
                        Text = text,
                        Width = DefaultWidth,
                        TextAlign = HorizontalAlignment.Left
                    };
                    break;

                default:
                    this.Name = Name;
                    this.Width = DefaultWidth;
                    this.Header = new ColumnHeader
                    {
                        Name = Name,
                        Text = Name,
                        Width = DefaultWidth,
                        TextAlign = HorizontalAlignment.Left
                    };
                    break;
            }

        }
    }


    //readonly Dictionary<string, ColumnHeader> RowWidths = DictExtAdd
    //(
    //    (NameConfig.RowID.Name,         75),
    //    (NameConfig.RowName.Name,       300),
    //    (NameConfig.RowType.Name,       120),
    //    (NameConfig.RowSize.Name,       new Tuple<int, HorizontalAlignment>(100, HorizontalAlignment.Right)),
    //    (NameConfig.RowStatus.Name,     75),
    //    (NameConfig.RowCheckOut.Name,   120),
    //    (NameConfig.RowCategory.Name,   110),
    //    (NameConfig.RowLocalDate.Name,  150),
    //    (NameConfig.RowRemoteDate.Name, 150),
    //    (NameConfig.RowFullName.Name,   100)
    //);
    //readonly Dictionary<string, ColumnHeader> HistoryRows = DictExtAdd
    //(
    //    (NameConfig.HistoryVersion.Name,    50),
    //    (NameConfig.HistoryModUser.Name,    140),
    //    (NameConfig.HistoryModDate.Name,    140),
    //    (NameConfig.HistorySize.Name,       75),
    //    (NameConfig.HistoryRelDate.Name,    75)
    //);
    //readonly Dictionary<string, ColumnHeader> ParentRows = DictExtAdd
    //(
    //    (NameConfig.ParentVersion.Name,     50),
    //    (NameConfig.ParentName.Name,        400),
    //    (NameConfig.ParentBasePath.Name,    600)
    //);
    //readonly Dictionary<string, ColumnHeader> ChildrenRows = DictExtAdd
    //(
    //    (NameConfig.ChildrenVersion.Name,   50),
    //    (NameConfig.ChildrenName.Name,      400),
    //    (NameConfig.ChildrenBasePath.Name,  600)
    //);
    //readonly Dictionary<string, ColumnHeader> PropertiesRows = DictExtAdd
    //(
    //    (NameConfig.PropertiesVersion.Name,         50),
    //    (NameConfig.PropertiesConfiguration.Name,   100),
    //    (NameConfig.PropertiesName.Name,            100),
    //    (NameConfig.PropertiesProperty.Name,        50),
    //    (NameConfig.PropertiesType.Name,            75),
    //    (NameConfig.PropertiesValue.Name,           400)
    //);
    //readonly Dictionary<string, ColumnHeader> VersionInfoRows = DictExtAdd
    //(
    //    (NameConfig.VersionID.Name,                 75),
    //    (NameConfig.VersionName.Name,               300),
    //    (NameConfig.VersionFileSize.Name,           100),
    //    (NameConfig.VersionDirectoryID.Name,        75),
    //    (NameConfig.VersionNodeID.Name,             75),
    //    (NameConfig.VersionEntryID.Name,            75),
    //    (NameConfig.VersionAttachmentID.Name,       75),
    //    (NameConfig.VersionModifyDate.Name,         120),
    //    (NameConfig.VersionChecksum.Name,           300),
    //    (NameConfig.VersionOdooCompletePath.Name,   300)
    //);


}
