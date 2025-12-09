using System.Runtime.Versioning;
using Windows.Storage;
using HackPDM.Src.ClientUtils.Types;
using System.Diagnostics;

namespace HackPDM.Helper.Compatibility;

[SupportedOSPlatform("windows10.0.17763.0")]
public class ModernSettingsProvider : ISettingsProvider
{
    private static ApplicationDataContainer Settings => ApplicationData.Current.LocalSettings;

    public T? Get<T>(string key, T? defaultValue = default)
    {
        return Settings.Values.TryGetValue(key, out var value) && value is T typed
            ? typed
            : defaultValue;
    }

    public void Set<T>(string key, T? value)
    {
        try
        {
            Settings.Values[key] = value is null ? default : value;
        }
        catch
        {
            Debug.WriteLine("Can't write to data container");
        }
	}
}