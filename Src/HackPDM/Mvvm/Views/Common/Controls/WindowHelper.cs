using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Settings;
using HackPDM.Shell;
using HackPDM.Mvvm.Models.Settings;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HackPDM.Mvvm.Views.Common.Controls;

public static partial class WindowHelper
{
#if WINDOWS
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, [MarshalAs(UnmanagedType.Bool)] bool bRepaint);
#endif
    // Declares managed prototypes for unmanaged functions.

    public static void ResizeWindow(Window window, int width, int height)
    {
        if (window.AppWindow != null)
        {
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });
            return;
        }
#if WINDOWS
        IntPtr hwnd = window.IntPtrHandle;
        if (hwnd != IntPtr.Zero)
        {
            MoveWindow(hwnd, 100, 100, width, height, true);
        }
#endif
    }


    public static T CreateWindow<T>(string? configName = null, bool activated = true, bool withFrame = true) where T : Window, new()
    {
        T window = new();
        if (activated) window.Activate();

        if (withFrame)
        {
            Frame rootFrame = new();
            window.Content = rootFrame;
            HackPDM.Shell.AppComposition.Theme.Attach(window);
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
            parameters.Add(window);
            rootFrame.Navigate(typeof(TPage), parameters);
            page = rootFrame.Content as TPage;
        }
        else 
        {
            rootFrame.Content = page;
        }
        
        window.Content = rootFrame;
        HackPDM.Shell.AppComposition.Theme.Attach(window);

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


    private static CancellationTokenSource _animationCts = new();

    public static void SetWindowConfig(Window window, WindowConfig windowConfig)
    {
        window.Title = windowConfig.Title;
        window.SetWindowType(windowConfig.WindowKind);
        _animationCts.Cancel();
        _animationCts = new CancellationTokenSource();
        window.AppWindow?.MoveAndResize(new Windows.Graphics.RectInt32 { X = windowConfig.PositionAndSize.x, Y = windowConfig.PositionAndSize.y, Width = windowConfig.PositionAndSize.z, Height = windowConfig.PositionAndSize.w });

        var titleBar = window.AppWindow?.TitleBar;

        if (titleBar == null) return;

        // Set system caption button backgrounds to transparent
        titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

        // Optional: Match the hover/press states to your custom background color
        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
        titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
    }
}

public class WindowConfig
{
    [System.Text.Json.Serialization.JsonIgnore]
    public static Dictionary<string, WindowConfig> PresetWindowConfig = UIStorage.GlobalData.Win.PresetWindowConfig;
    public string Title { get; set; } = "Window";
    public HackPDM.Domain.Representation.Vector4<int> PositionAndSize { get; set; } = (0, 0, 500, 350);
    public AppWindowPresenterKind WindowKind { get; set; } = AppWindowPresenterKind.Overlapped;
}
