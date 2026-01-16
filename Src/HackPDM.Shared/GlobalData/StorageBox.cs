using System.IO;

namespace HackPDM.Shared.GlobalData;

public static class StorageBox
{
    #region Application Settings
    public const string APP_NAME = "WyrmPDM";
    public const string APP_VERSION = "1.0.0";
    public const string APP_DEVELOPER = "Justin";
    // static one-time initialize
    public static string? TemporaryPath
    {
        get => field ??= Path.Combine(Path.GetTempPath(), APP_NAME);
        set;
    }
    #endregion
    #region Profile Manager
    public const int PROFILE_MANAGER_WIDTH = 600;
    public const int PROFILE_MANAGER_HEIGHT = 415;
    #endregion
    #region Message Box
    public const int MESSAGE_BOX_WIDTH = 400;
    public const int MESSAGE_BOX_HEIGHT = 200;
    public const string MESSAGE_BOX_TITLE = "Info";
    public const string MESSAGE_BOX_OK = "OK";
    public const string MESSAGE_BOX_CANCEL = "Cancel";
    public const string MESSAGE_BOX_YES = "Yes";
    public const string MESSAGE_BOX_NO = "No";
    public const string MESSAGE_BOX_CONTENT = "";
    #endregion
    #region Status Dialog
    public const int STATUS_BOX_WIDTH = 1280;
    public const int STATUS_BOX_HEIGHT = 720;
    #endregion
    #region HackFileManager
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
    #region OdooDefaults
    public const string DEFAULT_ODOO_CREDENTIALS = "HackPDM-OdooUser";
    #endregion
    #region Assets and Storage Paths
        public const string ASSETSPREFIX    = "ms-appx:///";
        public const string LOCALPREFIX     = "ms-appdata:///local";
        public const string ASSETSFOLDER    = "Assets";
        public const string IMAGEFOLDER     = "Images";

        public const string EXTENSIONFOLDER = "ExtensionIcons";
        public const string ICONFOLDER		= "Icons";
        public const string FOLDERICONS     = "FolderIcons";
        public const string STATUSFOLDER    = "StatusIcons";
    #endregion
}

