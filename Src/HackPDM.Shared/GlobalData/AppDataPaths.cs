using System;
using System.IO;

namespace HackPDM.Shared.GlobalData;

/// <summary>
/// Per-user folders for the app's own files, on every platform and without Windows packaging
/// (<c>ApplicationData.Current</c> needs an MSIX package identity on Windows):
/// <list type="bullet">
/// <item><see cref="Config"/>: settings. %APPDATA%\HackPDM on Windows, $XDG_CONFIG_HOME/HackPDM (~/.config/HackPDM) on Linux.</item>
/// <item><see cref="Data"/>: data kept on this machine. %LOCALAPPDATA%\HackPDM on Windows, $XDG_DATA_HOME/HackPDM (~/.local/share/HackPDM) on Linux.</item>
/// </list>
/// <c>HACKPDM_CONFIG_DIR</c> and <c>HACKPDM_DATA_DIR</c> replace them, e.g. so a test run does not touch the user's own settings.
/// </summary>
public static class AppDataPaths
{
	public const string FolderName = "HackPDM";

	public const string ConfigDirVariable = "HACKPDM_CONFIG_DIR";
	public const string DataDirVariable = "HACKPDM_DATA_DIR";

	/// <summary>The folder for settings (not created here).</summary>
	public static string Config => Override( ConfigDirVariable ) ?? Under( Environment.SpecialFolder.ApplicationData, ".config" );

	/// <summary>The folder for data kept on this machine (not created here).</summary>
	public static string Data => Override( DataDirVariable ) ?? Under( Environment.SpecialFolder.LocalApplicationData, Path.Combine( ".local", "share" ) );

	/// <summary>True when the settings folder was replaced through <see cref="ConfigDirVariable"/>.</summary>
	public static bool IsConfigOverridden => Override( ConfigDirVariable ) is not null;

	private static string? Override( string variable )
		=> Environment.GetEnvironmentVariable( variable ) is { Length: > 0 } dir ? Path.GetFullPath( dir ) : null;

	private static string Under( Environment.SpecialFolder folder, string homeFallback )
	{
		string root = Environment.GetFolderPath( folder );
		if( string.IsNullOrEmpty( root ) )
			root = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), homeFallback );
		return Path.Combine( root, FolderName );
	}
}
