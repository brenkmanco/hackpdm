using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using HackPDM.ClientUtils;
using HackPDM.Src.ClientUtils.Types;

namespace HackPDM.Hack;

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
    public string? FullPath 
    {
        get
        {
            field ??= Info?.FullName;
			return field is not null
				? (field)
				: DirectoryName is null || Name is null 
                    ? (field) 
                    : (field = Path.Combine(HackDefaults.PwaPathAbsolute, DirectoryName, Name));
		}
		set => field = value;
    }
    public string? RelativePath { get; set; }
    internal byte[]? FileContents { get; set; }
    internal FileInfo? Info { get; set; }

    public Hashtable ComputeHashtable(bool includeEmpty = true, in string[] excludedFieldNames = null)
    {
        Hashtable ht;
        ht = HashConverter.ConvertToHashtable(this, MethodType.PropertyOnly, includeEmpty, excludedFieldNames);
            
        return ht;
    }
    public async static Task<HackFile?> GetHackFileAsync<T>(string? fullFilePath) where T : HackFile, new()
    {
		if (string.IsNullOrEmpty(fullFilePath)) return null;
        HackFile hackFile = DefaultType<HackFile>();

        try
        {
            // if the directory doesn't exist then return its default type
            FileInfo fileInfo = new(fullFilePath);
            if (!fileInfo.Exists) return hackFile;
                
            hackFile = await FileInfoToHackFile(fileInfo);
        }
        catch (Exception ex) 
        {
            Console.WriteLine(ex);
        }
        return hackFile;
    }
    private static async Task<HackFile> FileInfoToHackFile(FileInfo fileInfo) => await HackFile.GetFromFileInfo(fileInfo);
	private static T? DefaultType<T>() where T : new() => typeof(T).IsValueType ? default : new T();
}