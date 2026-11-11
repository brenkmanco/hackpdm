using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Models.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Shell;

public static class InstanceManager
{
    // Weak mapping for Page -> Window (does not retain Page in memory)
    private static readonly ConditionalWeakTable<Page, Window> _pageToWindow = new();

    // Type-based ViewModel / State store
    private static readonly ConcurrentDictionary<Type, object> _stateStore = new();

    #region Window & Configuration Helpers


    public static WindowConfig? GetConfig(string configName)
        => WindowConfig.PresetWindowConfig.GetValueOrDefault(configName);

    public static TWin? GetAWindow<TPage, TWin>(TPage page) where TPage : Page where TWin : Window
    {
        if (page is null) return null;
        return _pageToWindow.TryGetValue(page, out var window) ? window as TWin : null;
    }

    public static void RegisterWindow<TPage, TWin>(TPage page, TWin win) where TPage : Page where TWin : Window
    {
        if (page is null || win is null) return;
        _pageToWindow.AddOrUpdate(page, win);
    }

    #endregion

    #region ViewModel & State Storage

    /// <summary>
    /// Gets or creates a cached ViewModel or Model instance.
    /// Checks the local state store, falls back to DI (HackApp.Services), or creates via factory / new().
    /// </summary>
    public static T GetOrCreate<T>(Func<T>? factory = null) where T : class
    {
        return (T)_stateStore.GetOrAdd(typeof(T), _ =>
        {
            if (HackApp.Services?.GetService<T>() is { } diService)
                return diService;

			return  factory != null
				?     factory()
				:     Activator.CreateInstance<T>()
				?? throw new InvalidOperationException($"Unable to create instance of {typeof(T).FullName}");
		} );
    }

    public static T Get<T>() where T : class, new()
        => GetOrCreate<T>(() => new T());





    // Convenience accessors for application ViewModel environment
    public static VMEnv Env => Get<VMEnv>();
    public static HFM_VM HFM => Env.HFM ??= AppComposition.CreateHackFileManager();
    public static PM_VM PM => Env.PM ??= Get<PM_VM>();
    public static SearchOdoo_VM SearchOdoo => Env.SearchOdoo ??= AppComposition.CreateSearchOdoo();
	public static (StatusDialog_VM Dialog, StatusLogSession Session) Logger => Env.Logger;
	public static StatusDialog_VM Dialog => Logger.Dialog;
	public static StatusLogSession Session => Logger.Session;
	
    public static OdooSettings_VM OdooSettings => Env.OdooSettings ??= Get<OdooSettings_VM>();
    public static HackSettings_VM HackSettings => Env.HackSettings ??= Get<HackSettings_VM>();
    public static AppSettings_VM AppSettings => Env.AppSettings ??= Get<AppSettings_VM>();
    public static Home_VM Home => Env.Home ??= Get<Home_VM>();
    public static NotLoggedIn_VM NotLoggedIn => Env.NotLoggedIn ??= Get<NotLoggedIn_VM>();


    #endregion

    #region Legacy Page Helpers (Obsolete - to prevent breaks during migration)


    #endregion
}
