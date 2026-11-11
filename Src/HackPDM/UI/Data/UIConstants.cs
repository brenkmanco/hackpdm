namespace HackPDM.UI.Data;

/// <summary>
/// UI-specific constants for window dimensions, dialog properties, tab names, and asset paths.
/// Decoupled from core shared data storage to maintain clean presentation-layer separation.
/// </summary>
public static class UIConstants
{
    #region Profile Manager Window
    public const int PROFILE_MANAGER_WIDTH = 600;
    public const int PROFILE_MANAGER_HEIGHT = 415;
    #endregion

    #region Message Box Window
    public const int MESSAGE_BOX_WIDTH = 400;
    public const int MESSAGE_BOX_HEIGHT = 200;
    public const string MESSAGE_BOX_TITLE = "Info";
    public const string MESSAGE_BOX_OK = "OK";
    public const string MESSAGE_BOX_CANCEL = "Cancel";
    public const string MESSAGE_BOX_YES = "Yes";
    public const string MESSAGE_BOX_NO = "No";
    public const string MESSAGE_BOX_CONTENT = "";
    #endregion

    #region Status Dialog Window
    public const int STATUS_BOX_WIDTH = 1280;
    public const int STATUS_BOX_HEIGHT = 720;
    #endregion

    #region HackFileManager Window & Tabs
    public const int HACK_FILE_MANAGER_WIDTH = 1280;
    public const int HACK_FILE_MANAGER_HEIGHT = 720;
    public const string HACK_FILE_MANAGER_TITLE = "Hack File Manager - HackPDM";
    public const string EMPTY_PLACEHOLDER = "-";
    public const string HISTORY_TAB = "HistoryTab";
    public const string PARENT_TAB = "ParentTab";
    public const string CHILD_TAB = "ChildTab";
    public const string PROPERTIES_TAB = "PropertiesTab";
    public const string INFO_TAB = "InfoTab";
    #endregion

    #region Asset & Pack URI Paths
    public const string ASSETS_PREFIX    = "ms-appx:///";
    public const string LOCAL_PREFIX     = "ms-appdata:///local";
    public const string ASSETS_FOLDER    = "Assets";
    public const string IMAGE_FOLDER     = "Images";
    public const string EXTENSION_FOLDER = "ExtensionIcons";
    public const string ICON_FOLDER      = "Icons";
    public const string FOLDER_ICONS     = "FolderIcons";
    public const string STATUS_FOLDER    = "StatusIcons";
    #endregion
}
