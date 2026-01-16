using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using HackPDM.Abstractions;
using HackPDM.Core.Configuration;
using HackPDM.Domain.Hack;

namespace HackPDM.Core.Hack;


public class HackDefaults : IHackDefaults
{
	public ISettingsProvider? SettingsProvider { get; set; }
	public string PwaPathAbsolute
	{
		get => SettingsProvider?.Get<string>("PWAPathAbsolute") ?? "";
		set => SettingsProvider?.Set("PWAPathAbsolute", value);
	}
	public string PwaPathRelative
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
	public string MeasureFileSize
	{
		get => SettingsProvider?.Get<string>("MeasureFileSize");
		set => SettingsProvider?.Set("MeasureFileSize", value);
	}
	public double MeasureByteSize
	{
		get => SettingsProvider?.Get<double>("MeasureByteSize") ?? 0;
		set => SettingsProvider?.Set("MeasureByteSize", value);
	}
	public double FileSizeMult
	{
		get => SettingsProvider?.Get<double>("FileSizeMult") ?? 0;
		set => SettingsProvider?.Set("FileSizeMult", value);
	}
	public double? ByteSizeMultiplier
	{
		get
		{
			field ??= 1D / Math.Pow(MeasureByteSize, FileSizeMult);
			return field;
		}
	} = null;
	public string? CurrentPath { get; set; } = null;

	public static IHackDefaults? Instance { get; set; } = new HackDefaults();

	public HackDefaults() { }
	public HackDefaults(ISettingsProvider settingsProvider)
	{
		if (Instance is null)
		{
			SettingsProvider = settingsProvider;
			Instance = this;
			return;
		}
		Instance.SettingsProvider = settingsProvider;
	}
	public static bool GetFiles(string relativePath, out IEnumerable<string> files)
	{
		Instance.CurrentPath = Path.Combine(Instance.PwaPathAbsolute, relativePath);
		try
		{
			// EnumerateFiles goes off a relative path from your project
			files = Directory.EnumerateFiles(Instance.CurrentPath, "*", SearchOption.AllDirectories);

			return true;
		}
		catch (DirectoryNotFoundException e)
		{
			Console.WriteLine(e.Message);
			files = null;
			return false;
		}
	}
	public static string DefaultPath(string? pathway, bool withAbsolute = false)
	{
		if (pathway is null || pathway == "") return withAbsolute ? Instance.PwaPathAbsolute : "root";
		string[] paths = pathway.Split('\\');
		paths = [.. paths.Skip(1)];

		string relativePath = string.Join(@"\", paths);

		if (withAbsolute) return Path.Combine(Instance.PwaPathAbsolute, relativePath);

		return relativePath;
	}
	public static T[] ArrayListToModelsArray<T>(ArrayList al) where T : IConvert<T>, new()
	{
		List<T> models = [];
		foreach (Hashtable ht in al)
		{
			T model = new();
			models.Add(model.ConvertFromHt(ht));
		}
		return [.. models];
	}
	private static void RecurseTravel(DirectoryDict directory, string directoryFullPath)
	{
		string pathway = directoryFullPath + "\\" + directory.Name;
		Directory.CreateDirectory(pathway);

		// recurse traverse children
		foreach (DirectoryDict hdr in directory.Directories)
		{
			RecurseTravel(hdr, pathway);
		}
	}
}
