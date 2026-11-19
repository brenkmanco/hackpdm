namespace HackPDM.Core.Pdm;

public enum FileStatus
{
	Lo, // Local Only
	Ro, // Remote Only
	Ok, // Latest
	Nv, // Newer Remote Version
	Lm, // Local Modification
	Dt, // Deleted
	Ds, // Destroyed
	If, // Ignore Filter
	Ft, // Filter Type
	Cm, // Checked Out To Me
	Co, // Checked Out To Other
	NoVersions,
	Unknown,
}

public enum EntryReturnType
{
	Created,
	GotExisting,
	Failed,
	InvalidType,
	Staged,
}

public enum EntryReprType
{
	Remote,
	Local,
	Both,
}

public enum ChecksumType
{
	Sha1,
	Md5,
	Sha256,
	Sha512,
}

[Flags]
public enum HackResult : ushort
{
	NotTested = 0,
	Clean = 1,
	OutOfPWA = 2,
	PropogatedFailure = 4,
	CorruptDepFile = 8,
	MissingFile = 16,
	MissingDepFile = 32,
}

public enum PropertyType
{
	Text,
	Number,
	Yesno,
	Date,
	None,
}

public enum StatusMessage
{
	PROCESSING,
	SKIP,
	FOUND,
	INFO,
	SUCCESS,
	WARNING,
	ERROR,
	OTHER,
}

public enum NavigatePageMenu
{
	HackFileManager,
	ProfileManager,
	Configuration,
	Settings,
	DependencyGraph,
}

public enum DialogResult
{
	None = 0,
	OK = 1,
	Cancel = 2,
	Abort = 3,
	Retry = 4,
	Ignore = 5,
	Yes = 6,
	No = 7,
	TryAgain = 10,
	Continue = 11,
}

public enum MessageBoxButtons : uint
{
	OK = 0x00000000,
	OKCancel = 0x00000001,
	AbortRetryIgnore = 0x00000002,
	YesNoCancel = 0x00000003,
	YesNo = 0x00000004,
	RetryCancel = 0x00000005,
	CancelTryContinue = 0x00000006,
}

public enum MessageBoxIcon : uint
{
	None = 0,
	Error = 0x00000010,
	Question = 0x00000020,
	Warning = 0x00000030,
	Information = 0x00000040,
}

public static class ProfileStatusCategory
{
	public const string Login = "Login";
	public const string Connection = "Connection";
	public const string Configuration = "Configuration";
	public const string Settings = "Settings";
}

public static class PdmConstants
{
	public const string AppName = "WyrmPDM";
	public const string DefaultOdooCredentials = "HackPDM-OdooUser";
	public const string EmptyPlaceholder = "-";

	public const string HistoryTab = "HistoryTab";
	public const string ParentTab = "ParentTab";
	public const string ChildTab = "ChildTab";
	public const string PropertiesTab = "PropertiesTab";
	public const string InfoTab = "InfoTab";

	public const string ClientVersionKey = "client_version";
	public const string ClientLatestVersionKey = "client_latest_version";
	public const string ClientDownloadUrlKey = "client_download_url";
	public const string ClientDownloadSha256Key = "client_download_sha256";
	public const string SwKeyName = "swdocmgr_key";
	public const string RestrictPropKey = "restrict_properties";
	public const string RestrictTypesKey = "restrict_types";

	public static string TemporaryPath
	{
		get => field ??= Path.Combine( Path.GetTempPath(), AppName );
		set;
	}
}

/// <summary>Human-readable binary byte size formatting.</summary>
public static class ByteSize
{
	private static readonly string[] Units = [ "B", "KB", "MB", "GB", "TB" ];

	public static string Format( long bytes, string separator = " " )
	{
		if( bytes is < 1024 or > 1125899906842624 )
			return $"{bytes}{separator}B";
		int unit = 0;
		double value = bytes;
		while( value >= 1024 && unit < Units.Length - 1 )
		{
			value /= 1024;
			unit++;
		}
		return $"{value:.##}{separator}{Units[ unit ]}";
	}
}
