using System;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Common.Controls;

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
		/// <summary>The window <see cref="WindowHelper"/> opened for this page; null for a page hosted anywhere else.</summary>
		public Window? Window => WindowHelper.WindowOf( page );
	}
}
