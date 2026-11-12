using System.Collections;
using System.IO;
using HackPDM.Abstractions;
using HackPDM.Core.General;
using HackPDM.Domain.Hack;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

/// <summary>The path-related parts of a vault file, filled lazily from <see cref="Info"/> when not set explicitly.</summary>
public abstract class HackBaseFile
{
    public string? Name 
    { 
        get => field ??= Info?.Name; 
        set => field = value ?? Info?.Name;
    }
    public string? DirectoryName 
    { 
        get => field ??= Info?.DirectoryName; 
        set => field = value ?? Info?.DirectoryName;
    }
    
    /// <summary>The file's absolute local path.</summary>
    /// <value>
    /// When not set, <see cref="Info"/>'s full name; failing that, the working folder combined with
    /// <see cref="DirectoryName"/> and <see cref="Name"/>. The computed value is cached.
    /// </value>
    public string? FullPath 
    {
        get
        {
            field ??= Info?.FullName;
			return field ?? (DirectoryName is null || Name is null 
                ? (field) 
                : (field = Path.Combine(HackDefaults.Instance.PwaPathAbsolute, DirectoryName, Name)));
		}
		set => field = value;
    }
    /// <summary>The file's path relative to the working folder.</summary>
    /// <value>
    /// When not set, computed from <see cref="FullPath"/> with <see cref="System.IO.Path.GetRelativePath(string, string)"/>
    /// and cached. <see cref="HackFile.GetFromPath"/> sets it to the folder only (for example <c>root/a/b</c>).
    /// </value>
    public string? RelativePath 
    {
        get
        {
            field ??= FullPath is null
                ? null
                : Path.GetRelativePath(HackDefaults.Instance.PwaPathAbsolute, FullPath);
            return field;
		} 
        set; 
    }

    public byte[]? FileContents { get; set; }
    public FileInfo? Info { get; set; }
}