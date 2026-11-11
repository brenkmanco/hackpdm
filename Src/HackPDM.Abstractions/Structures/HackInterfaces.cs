using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;


//
using System.IO;
//

namespace HackPDM.Abstractions;


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

