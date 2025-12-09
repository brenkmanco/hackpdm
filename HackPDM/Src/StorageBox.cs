using System.Collections.Generic;
using System.Windows.Controls;

using HackPDM.Forms.Settings;
using HackPDM.Helper;
using HackPDM.Properties;
using HackPDM.Src.ClientUtils.Types;

using Microsoft.UI.Xaml.Media;

using HackPDM.Src.Data.Numeric;
using Color = Windows.UI.Color;
using HackPDM.Forms.Odoo;
using HackPDM.Forms.Hack;
using System.IO;


namespace HackPDM;

public static class StorageBox
{
	#region Application Settings
	public const string APP_NAME = "WyrmPDM";
	public const string APP_VERSION = "1.0.0";
	public const string APP_DEVELOPER = "Justin";
	public static string? PwaPathAbsolute
	{
		get => field ??= Settings.Get<string>("PWAPathAbsolute");
		set => Settings.Set("PWAPathAbsolute", field = value);
	}
	public static string? PwaPathRelative
	{
		get => field ??= new System.IO.DirectoryInfo(PwaPathAbsolute ?? "").Name;
		set => field = value;
	}
	public static string? TemporaryPath
	{
		get => field ??= Path.Combine(Path.GetTempPath(), APP_NAME);
		set => field = value;
	}
	public static Dictionary<string, WindowConfig> PresetWindowConfig = new ()
	{
		{nameof(ProfileManager), new WindowConfig("Profile Manager", new int4(200, 200, 500, 200))},
        {nameof(OdooSettings), new WindowConfig("Odoo Settings", new int4(200, 200, 500, 500))},
        {nameof(HackSettings), new WindowConfig("Hack Settings", new int4(200, 200, 500, 200))},
        {nameof(HackFileManager), new WindowConfig("Hack File Manager", new int4(0, 0, 1280, 720))},
    };
	#endregion
	#region Profile Manager
	public const int PROFILE_MANAGER_WIDTH = 600;
	public const int PROFILE_MANAGER_HEIGHT = 415;
	public static Theme? MyTheme
	{
		get => Settings.Get<Theme?>("Theme");
		set => Settings.Set("Theme", value);
	}
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
	#region Color Settings
	public static readonly Color White              = Color.FromArgb(255, 255, 255, 255);
	public static readonly Color Black              = Color.FromArgb(255, 0, 0, 0);
	public static readonly Color LightGray         = Color.FromArgb(255, 211, 211, 211);
	public static readonly Color Gray               = Color.FromArgb(255, 128, 128, 128);
	public static readonly Color MustardYellow     = Color.FromArgb(255, 150, 150, 0);
	public static readonly Color DarkGray          = Color.FromArgb(255, 64, 64, 64);
	public static readonly Color DarkRed           = Color.FromArgb(255, 139, 0, 0);
	public static readonly Color DarkBlue		    = Color.FromArgb(255, 0, 0, 139);
	public static readonly Color DarkOliveGreen   = Color.FromArgb(255, 85, 107, 47);

	public static readonly SolidColorBrush BrushWhite              = new(White);
	public static readonly SolidColorBrush BrushBlack              = new(Black);
	public static readonly SolidColorBrush BrushLightGray         = new(LightGray);
	public static readonly SolidColorBrush BrushGray               = new(Gray);
	public static readonly SolidColorBrush BrushMustardYellow     = new(MustardYellow);
	public static readonly SolidColorBrush BrushDarkGray          = new(DarkGray);
	public static readonly SolidColorBrush BrushDarkOliveGreen   = new(DarkOliveGreen);
	public static readonly SolidColorBrush BrushDarkBlue          = new(DarkBlue);
	public static readonly SolidColorBrush BrushDarkRed           = new(DarkRed);
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