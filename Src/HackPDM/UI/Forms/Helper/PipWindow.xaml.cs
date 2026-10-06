using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Helper;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class PipWindow : Window
{
	// The explicit callback event for MainWindow to listen to
	public event EventHandler ReturnRequested;

	public PipWindow()
	{
		this.InitializeComponent();
		ExtendsContentIntoTitleBar = true;
		ConfigureCompactOverlay();
	}

	private void ConfigureCompactOverlay()
	{
		IntPtr hWnd = WindowNative.GetWindowHandle(this);
		var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
		var appWindow = AppWindow.GetFromWindowId(windowId);
		appWindow.SetPresenter( AppWindowPresenterKind.CompactOverlay );
	}

	private void OnExitPipClicked( object sender, RoutedEventArgs e )
	{
		// Fire the event to notify MainWindow
		ReturnRequested?.Invoke( this, EventArgs.Empty );
	}

	public void HostComponent( UIElement element )
	{
		PipContentRoot.Children.Clear();
		PipContentRoot.Children.Add( element );
	}

	public UIElement ReleaseComponent()
	{
		if( PipContentRoot.Children.Count > 0 )
		{
			var element = PipContentRoot.Children[0];
			PipContentRoot.Children.Clear();
			return element;
		}
		return null;
	}
}