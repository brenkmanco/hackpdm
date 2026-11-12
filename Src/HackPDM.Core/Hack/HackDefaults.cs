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


/// <summary>
/// The client's local settings (working folder, file size display), read from and written to an
/// <see cref="HackPDM.Abstractions.ISettingsProvider"/>.
/// </summary>
public class HackDefaults : IHackDefaults
{
	public ISettingsProvider? SettingsProvider { get; set; }
	/// <summary>The working folder (the vault's local root) as an absolute path.</summary>
	/// <value>The <c>PWAPathAbsolute</c> setting; "" when unset or there is no provider. Setting it saves immediately.</value>
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
	/// <summary>The factor that converts a size in bytes to the display unit.</summary>
	/// <value>
	/// 1 / <see cref="MeasureByteSize"/> ^ <see cref="FileSizeMult"/>, computed on first read and then cached, so later
	/// changes to those settings are not reflected.
	/// </value>
	public double? ByteSizeMultiplier
	{
		get
		{
			field ??= 1D / Math.Pow(MeasureByteSize, FileSizeMult);
			return field;
		}
	} = null;
	public string? CurrentPath { get; set; } = null;

	/// <summary>The process-wide settings used by <see cref="HackPDM.Core.PwaPaths"/> and the file classes.</summary>
	/// <value>Starts as an instance with no settings provider, so it is never <see langword="null"/>.</value>
	public static IHackDefaults Instance { get; set; } = new HackDefaults();

	/// <summary>Creates settings with no provider; every value reads as its default until one is set.</summary>
	public HackDefaults() { }
	/// <summary>Connects the global settings to a settings provider.</summary>
	/// <param name="settingsProvider">The store the settings are read from and written to.</param>
	/// <remarks>
	/// Because <see cref="Instance"/> is never <see langword="null"/>, this only gives <paramref name="settingsProvider"/>
	/// to the existing <see cref="Instance"/>; the new object itself keeps no provider and does not become the instance.
	/// </remarks>
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
