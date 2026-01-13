using System.Collections;

using HackPDM.Abstractions;

namespace HackPDM.Core.Configuration;

public class CoreSettings(ISettingsProvider provider) : ISettingsProvider
{
    public ISettingsProvider Provider { get; } = provider;
	
    public T? Get<T>(string key, T? defaultValue = default) => Provider.Get(key, defaultValue);
    public void Set<T>(string key, T value) => Provider.Set(key, value);
    public bool Contains(string key) => Provider.Contains(key);
    public void Remove(string key) => Provider.Remove(key);
}
