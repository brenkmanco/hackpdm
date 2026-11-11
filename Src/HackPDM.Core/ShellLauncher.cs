using System;
using System.Diagnostics;
using System.IO;

namespace HackPDM.Core;

/// <summary>Opens files, folders and links with the desktop's default application.</summary>
public static class ShellLauncher
{
	public static bool OpenFile( string path ) => File.Exists( path ) && Launch( path );

	public static bool OpenFolder( string path ) => Directory.Exists( path ) && Launch( path );

	public static bool OpenUrl( string url ) => Launch( url );

	/// <summary>The one place that knows how each OS opens something.</summary>
	private static bool Launch( string target )
	{
		try
		{
			if( OperatingSystem.IsLinux() )
				Process.Start( "xdg-open", target );
			else if( OperatingSystem.IsMacOS() )
				Process.Start( "open", target );
			else
				// Windows (and anything else): the shell picks the application; a folder opens in Explorer
				Process.Start( new ProcessStartInfo( target ) { UseShellExecute = true } );
			return true;
		}
		catch( Exception e )
		{
			Debug.WriteLine( $"could not open {target}: {e.Message}" );
			return false;
		}
	}
}
