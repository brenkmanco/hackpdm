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

    public static Window CreateWindowPage<T>(bool activated = true) where T : Page, new()
    {
        CreateWindowAndPage<T>(out _, out var window, activated);
        return window;
    }
    public static Window CreateWindowFromPage<TPage>(TPage page, bool activated = true)
        where TPage : Page
    {
        CreateWindowFromPageInternal(page, out Window window, activated);
        return window;
    }
    public static void CreateWindowFromPage<TPage, TWindow>(TPage page, out TWindow window, bool activated = true)
        where TPage : Page
        where TWindow : Window, new()
    {
        CreateWindowFromPageInternal(page, out window, activated);
	}
	public static void CreateWindowAndPage<T>(out T page, out Window window, bool activated = true) 
        where T : Page, new() 
        => CreateWindowAndPageInternal(out page, out window, activated);
    public static void CreateWindowAndPage<TPage, TWindow>(out TPage page, out TWindow window, bool activated = true) 
        where TPage : Page, new()
        where TWindow : Window, new()
		=> CreateWindowAndPageInternal(out page, out window, activated);
	private static void CreateWindowFromPageInternal<TPage, TWindow>(TPage page, out TWindow window, bool activated = true)
        where TPage : Page
        where TWindow : Window, new()
	{
		Frame rootFrame = new()
		{
			Content = page
		};

		window = new TWindow();
		if (activated) window.Activate();
        
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
	private static void CreateWindowAndPageInternal<TPage, TWindow>(out TPage page, out TWindow window, bool activated = true)
        where TPage : Page, new()
        where TWindow : Window, new()
    {
        page = new();
        CreateWindowFromPageInternal(page, out window, activated);
	}
	public static Window CreateWindowPage<T>(WindowConfig winConfig) where T : Page, new()
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