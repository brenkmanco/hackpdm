using HackPDM.Src.ClientUtils.Types;

namespace HackPDM.Helper.Compatibility;

public class LegacySettingsProvider : ISettingsProvider
{
    public T Get<T>(string key, T defaultValue = default)
    {
        var value = Properties.Settings.Default[key];
        return value is T typed ? typed : defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        Properties.Settings.Default[key] = value;
        Properties.Settings.Default.Save();
    }
}