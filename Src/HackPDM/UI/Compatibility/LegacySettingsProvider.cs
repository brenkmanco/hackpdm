using HackPDM.Abstractions;

namespace HackPDM.UI.Compatibility;

public class LegacySettingsProvider : ISettingsProvider
{
    public T? Get<T>(string key, T? defaultValue = default) => throw new System.NotImplementedException();
    public void Set<T>(string key, T value) => throw new System.NotImplementedException();
    public bool Contains(string key) => throw new System.NotImplementedException();
    public void Remove(string key) => throw new System.NotImplementedException();
}