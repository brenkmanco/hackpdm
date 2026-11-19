using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using HackPDM.Core.Configuration;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HackPDM.Common;

public sealed record WindowPreset(
	string Title,
	int Width,
	int Height,
	AppWindowPresenterKind Kind = AppWindowPresenterKind.Overlapped );

public static partial class WindowHelper
{
	private static readonly ConditionalWeakTable<Page, Window> PageWindows = new();
	private static readonly WindowSettingsData Defaults = new();
	private static readonly string? AppIconPath = ResolveAppIconPath();

	private static string? ResolveAppIconPath()
	{
		string iconPath = Path.Combine( AppContext.BaseDirectory, "Assets", "hackpdm-icon.ico" );
		return File.Exists( iconPath ) ? iconPath : null;
	}

	public static WindowPreset Preset( string name )
	{
		WindowConfigData cfg = UIStorage.GlobalData.Win.PresetWindowConfig.GetValueOrDefault( name )
			?? Defaults.PresetWindowConfig.GetValueOrDefault( name )
			?? new WindowConfigData();
		return new WindowPreset(
			cfg.Title,
			cfg.PositionAndSize.z > 0 ? cfg.PositionAndSize.z : 500,
			cfg.PositionAndSize.w > 0 ? cfg.PositionAndSize.w : 350,
			ToPresenterKind( cfg.WindowKind ) );
	}

	private static AppWindowPresenterKind ToPresenterKind( WindowKind kind ) => kind switch
	{
		WindowKind.CompactOverlay => AppWindowPresenterKind.CompactOverlay,
		WindowKind.FullScreen => AppWindowPresenterKind.FullScreen,
		WindowKind.Default => AppWindowPresenterKind.Default,
		_ => AppWindowPresenterKind.Overlapped,
	};

	public static Window? WindowOf( Page page )
		=> PageWindows.TryGetValue( page, out Window? window ) ? window : null;

	public static void UnregisterPageWindow( Page page ) => PageWindows.Remove( page );

	/// <summary>Applies the bundled HackPDM icon to the window titlebar and taskbar when supported.</summary>
	public static void ApplyAppIcon( Window window )
	{
		if( AppIconPath is null )
			return;
		try
		{
			window.AppWindow?.SetIcon( AppIconPath );
		}
		catch
		{
		}
	}

	public static void ResizeWindow( Window window, int width, int height )
	{
		if( window.AppWindow is { } appWindow )
		{
			appWindow.Resize( new Windows.Graphics.SizeInt32 { Width = width, Height = height } );
			X11WindowSize.TryResize( window, width, height );
		}
	}

	public static T CreateWindow<T>( string? presetName = null, bool activated = true, bool withFrame = true ) where T : Window, new()
	{
		T window = new();
		ApplyAppIcon( window );
		if( activated )
			window.Activate();
		if( withFrame )
			window.Content = new Frame();
		ApplyPreset( window, Preset( presetName ?? "TemplateWindow" ) );
		AppComposition.Theme.Attach( window );
		return window;
	}

	public static void CreateWindowAndPage<T>( out T page, out Window window, bool activated = true ) where T : Page, new()
	{
		page = new T();
		window = CreateWindowForExistingPage( page, activated );
	}

	public static Window CreateWindowForExistingPage( Page page, bool activated = true )
	{
		Window window = new();
		ApplyAppIcon( window );
		if( activated )
			window.Activate();
		window.Content = new Frame { Content = page };
		ApplyPreset( window, Preset( page.GetType().Name ) );
		AppComposition.Theme.Attach( window );
		PageWindows.AddOrUpdate( page, window );
		return window;
	}

	public static void ApplyPreset( Window window, WindowPreset preset )
	{
		window.Title = preset.Title;
		window.SetWindowType( preset.Kind );
		if( window.AppWindow is not { } appWindow )
			return;

		var current = appWindow.Position;
		var (targetX, targetY, targetW, targetH) = FitToWorkArea( appWindow, current, preset.Width, preset.Height );
		if( targetX != current.X || targetY != current.Y )
			appWindow.Move( new Windows.Graphics.PointInt32 { X = targetX, Y = targetY } );
		appWindow.Resize( new Windows.Graphics.SizeInt32 { Width = targetW, Height = targetH } );
		X11WindowSize.TryResize( window, targetW, targetH );
	}

	private static (int X, int Y, int Width, int Height) FitToWorkArea(
		AppWindow appWindow,
		Windows.Graphics.PointInt32 current,
		int width,
		int height )
	{
		int targetX = current.X;
		int targetY = current.Y;
		int targetW = Math.Max( 200, width );
		int targetH = Math.Max( 150, height );
		try
		{
#if WINDOWS
			if( DisplayArea.GetFromWindowId( appWindow.Id, DisplayAreaFallback.Nearest ) is { WorkArea: { Width: > 0, Height: > 0 } work } )
			{
				targetW = Math.Min( targetW, work.Width );
				targetH = Math.Min( targetH, work.Height );
				targetX = Math.Clamp( targetX, work.X, work.X + work.Width - targetW );
				targetY = Math.Clamp( targetY, work.Y, work.Y + work.Height - targetH );
			}
#else
			_ = appWindow;
			if( X11WindowSize.TryGetScreenSize( out int screenW, out int screenH ) )
			{
				targetW = Math.Min( targetW, screenW );
				targetH = Math.Min( targetH, screenH );
				targetX = Math.Clamp( targetX, 0, Math.Max( 0, screenW - targetW ) );
				targetY = Math.Clamp( targetY, 0, Math.Max( 0, screenH - targetH ) );
			}
#endif
		}
		catch
		{
		}
		return ( targetX, targetY, targetW, targetH );
	}
}

public static class WindowExtensions
{
	extension( Window window )
	{
		public bool SetWindowType( AppWindowPresenterKind kind )
		{
			try
			{
				window.AppWindow.SetPresenter( kind );
				return true;
			}
			catch
			{
				return false;
			}
		}

#if WINDOWS
		public IntPtr IntPtrHandle => WinRT.Interop.WindowNative.GetWindowHandle( window );
#else
		public IntPtr IntPtrHandle => IntPtr.Zero;
#endif
	}

	extension<T>( T page ) where T : Page
	{
		public Window? Window => WindowHelper.WindowOf( page );
	}

	public static void SetGrid( this FrameworkElement element, int row, int column, int rowspan = 1, int columnspan = 1 )
	{
		Grid.SetRow( element, row );
		Grid.SetColumn( element, column );
		Grid.SetRowSpan( element, rowspan );
		Grid.SetColumnSpan( element, columnspan );
	}

	public static ScrollViewer? GetScrollViewer( DependencyObject parent )
	{
		for( int i = 0; i < VisualTreeHelper.GetChildrenCount( parent ); i++ )
		{
			DependencyObject child = VisualTreeHelper.GetChild( parent, i );
			if( child is ScrollViewer sv )
				return sv;
			if( GetScrollViewer( child ) is { } found )
				return found;
		}
		return null;
	}
}

/// <summary>Resizes X11 client windows directly on Linux where window managers ignore post-map frame resizes.</summary>
internal static partial class X11WindowSize
{
#if HAS_UNO
	private static readonly object Gate = new();
	private static IntPtr _display;
	private static bool _unavailable;
#endif

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
				if( !EnsureDisplayLocked() )
					return false;
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
		_ = ( window, width, height );
		return false;
#endif
	}

	public static bool TryGetScreenSize( out int width, out int height )
	{
		width = 0;
		height = 0;
#if HAS_UNO
		if( !OperatingSystem.IsLinux() )
			return false;
		lock( Gate )
		{
			try
			{
				if( !EnsureDisplayLocked() )
					return false;
				int screen = XDefaultScreen( _display );
				width = XDisplayWidth( _display, screen );
				height = XDisplayHeight( _display, screen );
				return width > 0 && height > 0;
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

#if HAS_UNO
	private static bool EnsureDisplayLocked()
	{
		if( _unavailable )
			return false;
		if( _display != IntPtr.Zero )
			return true;
		_display = XOpenDisplay( null );
		if( _display == IntPtr.Zero )
		{
			_unavailable = true;
			return false;
		}
		return true;
	}

	[LibraryImport( "libX11.so.6", StringMarshalling = StringMarshalling.Utf8 )]
	private static partial IntPtr XOpenDisplay( string? name );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XResizeWindow( IntPtr display, IntPtr window, uint width, uint height );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XFlush( IntPtr display );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XDefaultScreen( IntPtr display );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XDisplayWidth( IntPtr display, int screenNumber );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XDisplayHeight( IntPtr display, int screenNumber );
#endif
}
