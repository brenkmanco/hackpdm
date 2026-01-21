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
public interface IHackDefaultBase
{
	public ISettingsProvider? SettingsProvider { get; set; }
	public string PwaPathAbsolute 
	{
		get;set;
	}
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
	public double? ByteSizeMultiplier
	{
		get;
	}
	public string? CurrentPath { get; set; }
}

