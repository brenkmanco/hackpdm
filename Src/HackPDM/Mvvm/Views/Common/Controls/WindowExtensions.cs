using System;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Common.Controls;

/// <summary>Window helpers: presenter kind, native handle, and the window a page was opened in.</summary>
public static class WindowExtensions
{
	extension( Window window )
	{
		/// <summary>Switches the window's presenter (overlapped, full screen, compact overlay...).</summary>
		/// <param name="kind">The presenter to use.</param>
		/// <returns><see langword="true"/> when the platform accepted it; any exception is swallowed and gives <see langword="false"/>.</returns>
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

		/// <summary>The native window handle (HWND) on Windows; <see cref="IntPtr.Zero"/> on other platforms.</summary>
#if WINDOWS
		public IntPtr IntPtrHandle => WinRT.Interop.WindowNative.GetWindowHandle( window );
#else
		public IntPtr IntPtrHandle => IntPtr.Zero;
#endif
	}

	extension<T>( T page ) where T : Page
	{
		/// <summary>The window <see cref="WindowHelper"/> opened for this page; null for a page hosted anywhere else.</summary>
		public Window? Window => WindowHelper.WindowOf( page );
	}
}
