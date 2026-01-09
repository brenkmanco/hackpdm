using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HackPDM.Domain.Representation;
using HackPDM.UI.Forms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.UI.Controls;


public static partial class WindowHelper
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, [MarshalAs(UnmanagedType.Bool)] bool bRepaint);

    public static void ResizeWindow(Window window, int width, int height)
    {
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        MoveWindow(hwnd, 100, 100, width, height, true);
    }

    public static Window CreateWindowPage(Type pageType, bool activated = true)
    {
        var window = new Window();
        var rootFrame = new Frame();
        if (activated) window.Activate();
        window.Content = rootFrame;
		string name = pageType.Name;
		if (WindowConfig.PresetWindowConfig.TryGetValue(name, out WindowConfig? value))
		{
			SetWindowConfig(window, value);
		}
		rootFrame.Navigate(pageType);
        return window;
    }
    public static Window CreateWindowPage<T>(bool activated = true) where T : Page
    {
        CreateWindowAndPage<T>(out _, out var window, activated);
        return window;
    }
	public static void CreateWindowAndPage<T>(out T page, out Window window, bool activated = true) 
        where T : Page 
        => CreateWindowAndPageInternal(out page, out window, activated);
    public static void CreateWindowAndPage<TPage, TWindow>(out TPage page, out TWindow window, bool activated = true) 
        where TPage : Page
        where TWindow : Window, new()
		=> CreateWindowAndPageInternal(out page, out window, activated);
	private static void CreateWindowAndPageInternal<TPage, TWindow>(out TPage page, out TWindow window, bool activated = true)
        where TPage : Page
        where TWindow : Window, new()
    {
        window = new TWindow();
        Frame rootFrame = new();
        if (activated) window.Activate();
        window.Content = rootFrame;
        string name = typeof(TPage).Name;
        if (WindowConfig.PresetWindowConfig.TryGetValue(name, out WindowConfig? value))
        {
            SetWindowConfig(window, value);
        }
        rootFrame.Navigate(typeof(TPage));
        page = rootFrame.Content as TPage;
        if (page != null)
        {
            InstanceManager.RegisterWindow(page, window);
        }
	}
	public static Window CreateWindowPage<T>(WindowConfig winConfig) where T : Page
    {
        Window win = CreateWindowPage<T>();
        SetWindowConfig(win, winConfig);
        return win;
    }
    private static void SetWindowConfig(Window window, WindowConfig windowConfig)
    {
        window.AppWindow.MoveAndResize(windowConfig.PositionAndSize);
        window.Title = windowConfig.Title;
    }
}
public class WindowConfig(string title, Vector4<int> positionAndSize)
{
    public static Dictionary<string, WindowConfig> PresetWindowConfig = new ()
	{
		{"ProfileManager", new WindowConfig("Profile Manager", new Vector4<int>(200, 200, 500, 200))},
        {"OdooSettings", new WindowConfig("Odoo Settings", new Vector4<int>(200, 200, 500, 500))},
        {"HackSettings", new WindowConfig("Hack Settings", new Vector4<int>(200, 200, 500, 200))},
        {"HackFileManager", new WindowConfig("Hack File Manager", new Vector4<int>(0, 0, 1280, 720))},
    };
    public string Title { get; set; } = title;
    public Vector4<int> PositionAndSize { get; set; } = positionAndSize;
}