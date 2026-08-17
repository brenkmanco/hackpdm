using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Core.Helper.Xaml;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HackPDM.UI.Controls;


public static partial class WindowHelper
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, [MarshalAs(UnmanagedType.Bool)] bool bRepaint);
	// Declares managed prototypes for unmanaged functions.
	
	public static void ResizeWindow(Window window, int width, int height)
    {
        IntPtr hwnd = window.IntPtrHandle;
        MoveWindow(hwnd, 100, 100, width, height, true);
    }
    

    public static Task AnimateWindowSize(AppWindow appWindow, Windows.Graphics.SizeInt32 targetSize, double durationMs = 250, CancellationToken token = default)
    {
        return SafeHelper.SafeInvokerAsync(() =>
        {
			var startSize = appWindow.Size;
	        var startTime = DateTime.Now;

	        DispatcherTimer timer = new DispatcherTimer();
	        timer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps

	        timer.Tick += (s, e) =>
	        {
		        var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
		        double progress = Math.Min(elapsed / durationMs, 1.0);

		        // Optional: Add easing (Cubic Out)
		        progress = 1 - Math.Pow(1 - progress, 3);

		        int newWidth = (int)(startSize.Width + (targetSize.Width - startSize.Width) * progress);
		        int newHeight = (int)(startSize.Height + (targetSize.Height - startSize.Height) * progress);

                if (token.IsCancellationRequested)
                {
                    timer.Stop();
                    return;
				}
				appWindow.Resize(new Windows.Graphics.SizeInt32(newWidth, newHeight));

		        if (progress >= 1.0)
		        {
			        timer.Stop();
		        }
	        };

	        timer.Start();
        }, token);
    }
    public static T CreateWindow<T>(string? configName = null, bool activated = true, bool withFrame = true) where T : Window, new()
    {
        T window = new T();
        if (activated) window.Activate();

        if (withFrame)
        {
            Frame rootFrame = new();
            window.Content = rootFrame;
        }
        if (WindowConfig.PresetWindowConfig.TryGetValue(configName ?? "TemplateWindow", out WindowConfig? value))
        {
            SetWindowConfig(window, value);
        }
        return window;
    }
    public static Window CreateWindowPage<T>(bool activated = true, ArrayList? parameters = null) where T : Page, new()
    {
        CreateWindowAndPage<T>(out _, out var window, activated, parameters);
        return window;
    }
    public static Window CreateWindowFromPage<TPage>(TPage page, bool activated = true, ArrayList? parameters = null)
        where TPage : Page
    {
        CreateWindowFromPageInternal(page, out Window window, activated, parameters);
        return window;
    }
    public static void CreateWindowFromPage<TPage, TWindow>(TPage page, out TWindow window, bool activated = true, ArrayList? parameter = null)
        where TPage : Page
        where TWindow : Window, new()
    {
        CreateWindowFromPageInternal(page, out window, activated, parameter);
	}
	public static void CreateWindowAndPage<T>(out T page, out Window window, bool activated = true, ArrayList? parameters = null) 
        where T : Page, new() 
        => CreateWindowAndPageInternal(out page, out window, activated, parameters);
    public static void CreateWindowAndPage<TPage, TWindow>(out TPage page, out TWindow window, bool activated = true, ArrayList? parameters = null) 
        where TPage : Page, new()
        where TWindow : Window, new()
		=> CreateWindowAndPageInternal(out page, out window, activated, parameters);
	private static void CreateWindowFromPageInternal<TPage, TWindow>(TPage page, out TWindow window, bool activated = true, ArrayList? parameters = null)
        where TPage : Page
        where TWindow : Window, new()
	{
        window = new TWindow();
		if (activated) window.Activate();

        Frame rootFrame = new();
		if (parameters is not null)
        {
            parameters.Add( window );
            rootFrame.Navigate(typeof(TPage), parameters);
            page = rootFrame.Content as TPage;
        } else 
        {
            rootFrame.Content = page;
        }
        
		window.Content = rootFrame;
        

        
		if (WindowConfig.PresetWindowConfig.TryGetValue(typeof(TPage).Name, out WindowConfig? value))
		{
			SetWindowConfig(window, value);
		}
		if (page != null)
		{
			InstanceManager.RegisterWindow(page, window);
		}
	}
	private static void CreateWindowAndPageInternal<TPage, TWindow>(out TPage? page, out TWindow window, bool activated = true, ArrayList? parameters = null)
        where TPage : Page, new()
        where TWindow : Window, new()
    {
        page = parameters is null ? new() : null;
        CreateWindowFromPageInternal(page, out window, activated, parameters);
	}
	public static Window CreateWindowPage<T>(WindowConfig winConfig) where T : Page, new()
    {
        Window win = CreateWindowPage<T>();
        SetWindowConfig(win, winConfig);
        return win;
    }
    private static CancellationTokenSource _animationCts = new();
	public static void SetWindowConfig(Window window, WindowConfig windowConfig)
    {
        window.Title = windowConfig.Title;
        window.SetWindowType(windowConfig.WindowKind);
        _animationCts.Cancel();
        _animationCts = new CancellationTokenSource();
		//AnimateWindowSize(window.AppWindow, new Windows.Graphics.SizeInt32(windowConfig.PositionAndSize.z, windowConfig.PositionAndSize.w), 250, _animationCts.Token);
        window.AppWindow.MoveAndResize(windowConfig.PositionAndSize);
		var titleBar = window.AppWindow.TitleBar;

		// Set system caption button backgrounds to transparent
		titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0,0,0,0);
        titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

		// Optional: Match the hover/press states to your custom background color
		titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
		titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
	}
}
public class WindowConfig(string title, Vector4<int> positionAndSize, AppWindowPresenterKind kind = AppWindowPresenterKind.Overlapped)
{
    public static Dictionary<string, WindowConfig> PresetWindowConfig = new ()
	{
		{"ProfileManager", new WindowConfig("Profile Manager", new Vector4<int>(200, 200, 600, 600))},
        {"OdooSettings", new WindowConfig("Odoo Settings", new Vector4<int>(200, 200, 500, 500))},
		{"HackSettings", new WindowConfig("Hack Settings", new Vector4<int>(200, 200, 700, 200))},
		{"ConfigSettings", new WindowConfig("Configuration Settings", new Vector4<int>(200, 200, 900, 600))},
        {"HackFileManager", new WindowConfig("Hack File Manager", new Vector4<int>(100, 100, 1500, 800))},
		{"NotLoggedIn", new WindowConfig("Hack File Manager", new Vector4<int>(0, 0, 1280, 720))},
		{"MessageBox", new WindowConfig("Info", new Vector4<int>(200, 200, 450, 250), AppWindowPresenterKind.CompactOverlay)},
        {"TemplateWindow", new WindowConfig("Template Window", new Vector4<int>(200, 200, 500, 300))},
    };
    public string Title { get; set; } = title;
    public Vector4<int> PositionAndSize { get; set; } = positionAndSize;
    public AppWindowPresenterKind WindowKind { get; set; } = kind;
}