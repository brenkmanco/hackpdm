using System.Diagnostics;
using System.Runtime.Versioning;
using Windows.Storage;
using HackPDM.Abstractions;
using System.Collections.Generic;

namespace HackPDM.UI.Compatibility;

[SupportedOSPlatform("windows10.0.17763.0")]
public class ModernSettingsProvider : ISettingsProvider
{
    private static ApplicationDataContainer Settings => ApplicationData.Current.LocalSettings;

    public T? Get<T>(string key, T? defaultValue = default) => 
        Settings.Values.TryGetValue(key, out var value) && value is T typed
            ? typed
            : defaultValue;
    public void Set<T>(string key, T value)
    {
        try 
        {
            if (value is null) return;
            Settings.Values[key] = value; 
        } 
        catch { Debug.WriteLine("Can't write to data container"); }
    }
    public bool Contains(string key) => Settings.Values.ContainsKey(key);
    public void Remove(string key) => Settings.Values.Remove(key);
}