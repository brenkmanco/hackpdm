using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;


//
using System.IO;
//

namespace HackPDM.Abstractions;


/// <summary>A persistent key/value store for the client's local settings.</summary>
public interface ISettingsProvider
{
	/// <summary>Reads a setting.</summary>
	/// <typeparam name="T">The type to read the stored value as.</typeparam>
	/// <param name="key">The setting name.</param>
	/// <param name="defaultValue">Returned when the setting is missing or can't be read as <typeparamref name="T"/>.</param>
	/// <returns>The stored value, or <paramref name="defaultValue"/>.</returns>
	T? Get<T>(string key, T? defaultValue = default);
	/// <summary>Stores a setting, replacing any previous value.</summary>
	/// <typeparam name="T">The value's type.</typeparam>
	/// <param name="key">The setting name.</param>
	/// <param name="value">The value; implementations may treat <see langword="null"/> as a removal.</param>
	void Set<T>(string key, T value);
	/// <summary>Tells whether a setting is stored.</summary>
	/// <param name="key">The setting name.</param>
	/// <returns><see langword="true"/> when the key exists.</returns>
	bool Contains(string key);
	/// <summary>Deletes a setting; does nothing when it isn't stored.</summary>
	/// <param name="key">The setting name.</param>
	void Remove(string key);
}
/// <summary>The client's local (non-server) settings.</summary>
public interface IHackDefaultBase
{
	/// <summary>The store the settings are read from and written to; <see langword="null"/> until one is connected.</summary>
	public ISettingsProvider? SettingsProvider { get; set; }
	/// <summary>The working folder (the vault's local root) as an absolute path.</summary>
	public string PwaPathAbsolute 
	{
		get;set;
	}
	/// <summary>The working folder's own name (its last path segment), accepted in place of "root" in server paths.</summary>
	public string PwaPathRelative
	{
		get;set;
	}
	public string MeasureFileSize 
	{
		get;set;
	}
	public double MeasureByteSize 
	{
		get;set;
	}
	public double FileSizeMult
	{
		get;set;
	}
	/// <summary>The factor that converts a size in bytes to the display unit.</summary>
	public double? ByteSizeMultiplier
	{
		get;
	}
	public string? CurrentPath { get; set; }
}

