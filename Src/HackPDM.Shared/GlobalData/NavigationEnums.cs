using System;

namespace HackPDM.Shared.GlobalData;

public enum FlowDirection
{
	LeftToRight,
	TopToBottom,
	TopLeftToBottomRight,
	BottomLeftToTopRight,
}

public enum NavigatePageMenu
{
    HackFileManager,
    ProfileManager,
    Configuration,
    JsonEditor,
    Settings,
}

public enum ColumnGroup
{ 
    Row,
    History,
    Parent,
    Child,
    Property,
    Version,
    Search,
    SearchProp,
    FileType,
    FileTypeEntryFilter,
    FileTypeLoc,
    FileTypeLocDat,
}

public enum SortPredefined
{
    String,
    Int,
    Date,
    Unknown,
}
