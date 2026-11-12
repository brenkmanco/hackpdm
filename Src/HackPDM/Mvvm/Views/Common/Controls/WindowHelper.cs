using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Settings;
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

    // which window each page created here is shown in (weak: does not keep the page alive)
    private static readonly ConditionalWeakTable<Page, Window> PageWindows = new();

    /// <summary>The window <see cref="CreateWindowAndPage{T}"/> opened for <paramref name="page"/>; null for any other page.</summary>
    public static Window? WindowOf(Page page)
        => PageWindows.TryGetValue(page, out Window? window) ? window : null;

    public static Window CreateWindowPage<T>(bool activated = true) where T : Page, new()
    {
        CreateWindowAndPage<T>(out _, out Window window, activated);
        return window;
    }

    /// <summary>A new window showing a new <typeparamref name="T"/>, sized by the preset named after the page type.</summary>
    public static void CreateWindowAndPage<T>(out T page, out Window window, bool activated = true) where T : Page, new()
    {
        page = new T();
        window = new Window();
        if (activated) window.Activate();

        window.Content = new Frame { Content = page };
        HackPDM.Shell.AppComposition.Theme.Attach(window);

        if (WindowConfig.PresetWindowConfig.TryGetValue(typeof(T).Name, out WindowConfig? value))
        {
            SetWindowConfig(window, value);
        }
        PageWindows.AddOrUpdate(page, window);
    }

    private static CancellationTokenSource _animationCts = new();

    public static void SetWindowConfig(Window window, WindowConfig windowConfig)
    {
        window.Title = windowConfig.Title;
        window.SetWindowType(windowConfig.WindowKind);
        _animationCts.Cancel();
        _animationCts = new CancellationTokenSource();
        var (x, y, width, height) = (windowConfig.PositionAndSize.x, windowConfig.PositionAndSize.y, windowConfig.PositionAndSize.z, windowConfig.PositionAndSize.w);
        if (window.AppWindow is { } appWindow)
        {
            // separate Move + Resize, not MoveAndResize: Uno's desktop (Skia) heads leave MoveAndResize
            // unimplemented (it does nothing), while Move and Resize work on every platform
            appWindow.Move(new Windows.Graphics.PointInt32 { X = x, Y = y });
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });
            // on X11 that resize goes to the window manager's frame, which it ignores once the window is shown
            X11WindowSize.TryResize(window, width, height);
        }

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

    private static readonly Dictionary<string, WindowConfig> Defaults = new WindowSettings().PresetWindowConfig;

    /// <summary>The saved preset; the built-in one when the settings file predates it; a plain window otherwise.</summary>
    public static WindowConfig Preset(string name)
        => PresetWindowConfig.GetValueOrDefault(name) ?? Defaults.GetValueOrDefault(name) ?? new();
    public string Title { get; set; } = "Window";
    public HackPDM.Domain.Representation.Vector4<int> PositionAndSize { get; set; } = (0, 0, 500, 350);
    public AppWindowPresenterKind WindowKind { get; set; } = AppWindowPresenterKind.Overlapped;
}
