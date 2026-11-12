using System.Collections;

using HackPDM.Abstractions;

namespace HackPDM.Core.Configuration;

/// <summary>An <see cref="HackPDM.Abstractions.ISettingsProvider"/> that forwards every call to another provider.</summary>
/// <param name="provider">The provider that actually stores the settings.</param>
public class CoreSettings(ISettingsProvider provider) : ISettingsProvider
{
    /// <summary>The provider every call is forwarded to.</summary>
    public ISettingsProvider Provider { get; } = provider;
	
    /// <inheritdoc/>
    public T? Get<T>(string key, T? defaultValue = default) => Provider.Get(key, defaultValue);
    /// <inheritdoc/>
    public void Set<T>(string key, T value) => Provider.Set(key, value);
    /// <inheritdoc/>
    public bool Contains(string key) => Provider.Contains(key);
    /// <inheritdoc/>
    public void Remove(string key) => Provider.Remove(key);
}
