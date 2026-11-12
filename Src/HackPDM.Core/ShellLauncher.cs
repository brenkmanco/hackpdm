using System;
using System.Diagnostics;
using System.IO;

namespace HackPDM.Core;

/// <summary>Opens files, folders and links with the desktop's default application.</summary>
public static class ShellLauncher
{
	/// <summary>Opens a file with the desktop's default application for its type.</summary>
	/// <param name="path">The file's path.</param>
	/// <returns><see langword="true"/> when the file exists and the opener process started; launch errors are caught and logged.</returns>
	/// <remarks>Uses <c>xdg-open</c> on Linux, <c>open</c> on macOS and the shell elsewhere. Does not wait for the application.</remarks>
	public static bool OpenFile( string path ) => File.Exists( path ) && Launch( path );

	/// <summary>Opens a folder in the desktop's file manager.</summary>
	/// <param name="path">The folder's path.</param>
	/// <returns><see langword="true"/> when the folder exists and the opener process started.</returns>
	public static bool OpenFolder( string path ) => Directory.Exists( path ) && Launch( path );

	/// <summary>Opens a URL (or any target) with the desktop's default handler.</summary>
	/// <param name="url">The URL to open; it is not validated.</param>
	/// <returns><see langword="true"/> when the opener process started.</returns>
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
