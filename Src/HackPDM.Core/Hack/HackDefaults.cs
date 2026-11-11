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
	/// <summary>
	/// The working folder's name. It was worked out once and kept, so after the working folder
	/// changed it still named the old one (and relative paths came out wrong, or threw); it now
	/// follows <see cref="PwaPathAbsolute"/>, unless set explicitly for the current folder.
	/// </summary>
	public string PwaPathRelative
	{
		get
		{
			string absolute = PwaPathAbsolute;
			if (field is null || _relativeFor != absolute)
			{
				field = Path.GetFileName(Path.TrimEndingDirectorySeparator(absolute));
				_relativeFor = absolute;
			}
			return field;
		}
		set
		{
			field = value;
			_relativeFor = PwaPathAbsolute;
		}
	}
	private string? _relativeFor;
	public string MeasureFileSize
	{
		get => SettingsProvider?.Get<string>("MeasureFileSize") ?? "";
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

	public static IHackDefaults Instance { get; set; } = new HackDefaults();

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
}
