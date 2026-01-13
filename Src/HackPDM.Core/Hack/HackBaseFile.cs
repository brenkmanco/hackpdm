using System.Collections;
using HackPDM.Abstractions;
using HackPDM.Core.General;
using HackPDM.Domain.Hack;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

public abstract class HackBaseFile : IHackBaseFileModel
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

    public Hashtable ComputeHashtable(bool includeEmpty = true, in string[] excludedFieldNames = null)
    {
        var ht = ReflectionHelp.ConvertToHashtable(this, MethodType.PropertyOnly, includeEmpty, excludedFieldNames);
            
        return ht;
    }
}