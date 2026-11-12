using System;
using System.Runtime.InteropServices;

using Microsoft.UI.Xaml;

namespace HackPDM.Mvvm.Views.Common.Controls;

/// <summary>
/// Resizes a window on Linux/X11 the way window managers accept.
/// <para>
/// Uno's <c>AppWindow.Resize</c> resizes the window manager's frame once the window has one, and
/// reparenting window managers (Cinnamon/Muffin, GNOME/Mutter, ...) ignore that: only the first resize,
/// before the window was framed, takes effect. Resizing the app's own (client) window is a request the
/// window manager honours, and it resizes the frame to match.
/// </para>
/// </summary>
internal static partial class X11WindowSize
{
	private static readonly object Gate = new();
	private static IntPtr _display;
	private static bool _unavailable;

	/// <summary>Resizes <paramref name="window"/>'s X11 client window; false when not on X11 (the caller resizes another way).</summary>
	public static bool TryResize( Window window, int width, int height )
	{
#if HAS_UNO
		if( !OperatingSystem.IsLinux() || width <= 0 || height <= 0 )
			return false;
		if( Uno.UI.Xaml.WindowHelper.GetNativeWindow( window ) is not Uno.UI.NativeElementHosting.X11NativeWindow { WindowId: var id } || id == IntPtr.Zero )
			return false;
		lock( Gate )
		{
			try
			{
				if( _unavailable )
					return false;
				if( _display == IntPtr.Zero )
				{
					// a connection of our own: requests on it reach the same server and the window manager
					_display = XOpenDisplay( null );
					if( _display == IntPtr.Zero )
					{
						_unavailable = true;
						return false;
					}
				}
				_ = XResizeWindow( _display, id, (uint)width, (uint)height );
				_ = XFlush( _display );
				return true;
			}
			catch( Exception e ) when( e is DllNotFoundException or EntryPointNotFoundException )
			{
				_unavailable = true;
				return false;
			}
		}
#else
		return false;
#endif
	}

	[LibraryImport( "libX11.so.6", StringMarshalling = StringMarshalling.Utf8 )]
	private static partial IntPtr XOpenDisplay( string? name );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XResizeWindow( IntPtr display, IntPtr window, uint width, uint height );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XFlush( IntPtr display );
}
