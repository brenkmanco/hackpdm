using System.Collections;
using System.IO;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Domain.Hack;

public interface IHackBaseFileModel
{
    public string? Name
    {
        get;
        set;
    }
    public string? DirectoryName
    {
        get;
        set;
    }
    public string? FullPath
    {
        get;
        set;
    }
    public string? RelativePath
    {
        get;
        set;
    }
    public byte[]? FileContents { get; set; }
    public FileInfo? Info { get; set; }

    public Hashtable ComputeHashtable(bool includeEmpty = true, in string[] excludedFieldNames = null);
    
	protected static T? DefaultType<T>() where T : new() => default;
}