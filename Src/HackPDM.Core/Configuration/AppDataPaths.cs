namespace HackPDM.Core.Configuration;

/// <summary>Per-user config and data folders across Windows, Linux, and macOS.</summary>
public static class AppDataPaths
{
	public const string FolderName = "HackPDM";
	public const string ConfigDirVariable = "HACKPDM_CONFIG_DIR";
	public const string DataDirVariable = "HACKPDM_DATA_DIR";

	public static string Config
		=> Override( ConfigDirVariable ) ?? Under( Environment.SpecialFolder.ApplicationData, ".config" );

	public static string Data
		=> Override( DataDirVariable ) ?? Under( Environment.SpecialFolder.LocalApplicationData, Path.Combine( ".local", "share" ) );

	public static bool IsConfigOverridden => Override( ConfigDirVariable ) is not null;

	private static string? Override( string variable )
		=> Environment.GetEnvironmentVariable( variable ) is { Length: > 0 } dir
			? Path.GetFullPath( dir )
			: null;

	private static string Under( Environment.SpecialFolder folder, string homeFallback )
	{
		string root = Environment.GetFolderPath( folder );
		if( string.IsNullOrEmpty( root ) )
			root = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), homeFallback );
		return Path.Combine( root, FolderName );
	}
}
