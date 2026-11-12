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

/// <summary>Creating windows (optionally hosting a page), sizing them from the saved window presets, and finding a page's window.</summary>
public static partial class WindowHelper
{
#if WINDOWS
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, [MarshalAs(UnmanagedType.Bool)] bool bRepaint);
#endif
    // Declares managed prototypes for unmanaged functions.

    /// <summary>Resizes a window, keeping its position.</summary>
    /// <param name="window">The window to resize.</param>
    /// <param name="width">The new width in pixels.</param>
    /// <param name="height">The new height in pixels.</param>
    /// <remarks>Uses <c>AppWindow.Resize</c>; only on Windows without an AppWindow does it fall back to moving the window to (100, 100).</remarks>
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


    /// <summary>Creates a window of type <typeparamref name="T"/>, optionally with an empty frame and the app theme, sized by a saved preset.</summary>
    /// <typeparam name="T">The window type to create.</typeparam>
    /// <param name="configName">The window preset to apply; "TemplateWindow" when null. Nothing is applied when no preset has that name.</param>
    /// <param name="activated">Whether to show the window right away.</param>
    /// <param name="withFrame">Whether to set a new <see cref="Frame"/> as the content and apply the theme to it.</param>
    /// <returns>The new window.</returns>
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

    /// <summary>Finds the window a page was opened in by this helper.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The window <see cref="CreateWindowAndPage{T}"/> opened for <paramref name="page"/>; null for any other page.</returns>
    public static Window? WindowOf(Page page)
        => PageWindows.TryGetValue(page, out Window? window) ? window : null;

    /// <summary>Opens a new window showing a new <typeparamref name="T"/> page.</summary>
    /// <typeparam name="T">The page type to show.</typeparam>
    /// <param name="activated">Whether to show the window right away.</param>
    /// <returns>The new window.</returns>
    public static Window CreateWindowPage<T>(bool activated = true) where T : Page, new()
    {
        CreateWindowAndPage<T>(out _, out Window window, activated);
        return window;
    }

    /// <summary>A new window showing a new <typeparamref name="T"/>, sized by the preset named after the page type.</summary>
    /// <typeparam name="T">The page type to show; its type name selects the window preset.</typeparam>
    /// <param name="page">The created page.</param>
    /// <param name="window">The created window, with the app theme applied.</param>
    /// <param name="activated">Whether to show the window right away.</param>
    /// <remarks>The page is remembered weakly so <see cref="WindowOf"/> can find its window.</remarks>
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

    /// <summary>Applies a window preset: title, presenter, position and size, and transparent caption button backgrounds.</summary>
    /// <param name="window">The window to configure.</param>
    /// <param name="windowConfig">The preset to apply.</param>
    /// <remarks>On Linux/X11 the client window is also resized directly, since window managers ignore frame resizes once the window is shown.</remarks>
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

#if WINDOWS
        // caption button colours exist on Windows only; Uno's desktop title bar has no such settings
        var titleBar = window.AppWindow?.TitleBar;

        if (titleBar == null) return;

        // Set system caption button backgrounds to transparent
        titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

        // Optional: Match the hover/press states to your custom background color
        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
        titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
#endif
    }
}

/// <summary>A saved window preset: title, position and size, and presenter kind.</summary>
public class WindowConfig
{
    [System.Text.Json.Serialization.JsonIgnore]
    public static Dictionary<string, WindowConfig> PresetWindowConfig = UIStorage.GlobalData.Win.PresetWindowConfig;

    private static readonly Dictionary<string, WindowConfig> Defaults = new WindowSettings().PresetWindowConfig;

    /// <summary>The saved preset; the built-in one when the settings file predates it; a plain window otherwise.</summary>
    /// <param name="name">The preset name, usually a page type name.</param>
    /// <returns>The preset to apply; never null.</returns>
    public static WindowConfig Preset(string name)
        => PresetWindowConfig.GetValueOrDefault(name) ?? Defaults.GetValueOrDefault(name) ?? new();
    public string Title { get; set; } = "Window";
    public HackPDM.Domain.Representation.Vector4<int> PositionAndSize { get; set; } = (0, 0, 500, 350);
    public AppWindowPresenterKind WindowKind { get; set; } = AppWindowPresenterKind.Overlapped;
}
