using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;


//
using System.IO;
//

namespace HackPDM.Abstractions;

public interface IConvert<T>
{
	T ConvertFromHt(Hashtable ht);
}
public interface ICloneable<T>
{
	T Clone();
}
public interface IRowData<T> : ICloneable<T>
{
	
}
public interface ITreeItem
{
	public object? Tag { get; set; }
	public IEnumerable<ITreeItem>? Children { get; set; }
}
public interface IListItem<T>
{
	public T? Value { get; }
	public bool IsSelected { get; set; }
	//public ListViewItem Item { get; set; }
}

public interface ISettingsProvider
{
	T? Get<T>(string key, T? defaultValue = default);
	void Set<T>(string key, T value);
	bool Contains(string key);
	void Remove(string key);
}
public abstract class HackDefaultBase
{
	public static ISettingsProvider? SettingsProvider { get; set; }
	public static string PwaPathAbsolute 
	{ 
		get => SettingsProvider?.Get<string>("PWAPathAbsolute") ?? ""; 
		set => SettingsProvider?.Set("PWAPathAbsolute", value);
	}
	public static string PwaPathRelative
	{
		get
		{
			field ??= Path.GetFileName(PwaPathAbsolute);
			return field;
		}
		set
		{
			field = value;
		}
	}
	public static string MeasureFileSize 
	{ 
		get => SettingsProvider?.Get<string>("MeasureFileSize"); 
		set => SettingsProvider?.Set("MeasureFileSize", value);
	}
	public static double MeasureByteSize 
	{ 
		get => SettingsProvider?.Get<double>("MeasureByteSize") ?? 0;
		set => SettingsProvider?.Set("MeasureByteSize", value);
	}
	public static double FileSizeMult
	{
		get => SettingsProvider?.Get<double>("FileSizeMult") ?? 0;
		set => SettingsProvider?.Set("FileSizeMult", value);
	}
	public static double? ByteSizeMultiplier
	{
		get
		{
			field ??= 1D / Math.Pow(MeasureByteSize, FileSizeMult);
			return field;
		}
	} = null;
	public static string CurrentPath { get; set; }
}