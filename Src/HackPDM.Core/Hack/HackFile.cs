using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HackPDM.Domain.Hack;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Domain.Representation;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

public class HackFile : HackBaseFile, IHackFileModel
{
    // file settings
    public string? TypeExt 
    {
        get => field ??= Info?.Extension;
        set
        {
            field = value ?? Info?.Extension;
        }
    }
    public DateTime ModifiedDate 
    {
        get;
        set
        {
            if (OverwriteDate != default)
            {
                field = OverwriteDate;
                return;
            }
            if (field == default)
            {
	            field = value == default
		            ? Info?.LastWriteTime ?? default
					: value;
            }
            else
            {
                if (Exists is true)
                {
                    Info?.LastWriteTime = value;
                    field = value == default
	                    ? Info?.LastWriteTime ?? default
	                    : value;
                }
                else
                {
                    field = value;
                }
            }
        }
    }

    public DateTime OverwriteDate { get; set; } = default;
	
    public string? Checksum { get; set; }
    public long? FileSize 
    {
        get => field ??= (Info?.Length 
			?? (FullPath is null 
				? null 
				: (Info = new FileInfo(FullPath ?? "")).Length));
        set => field = value ?? Info?.Length;
    }
        
    // odoo settings
	public int? HpEntryId { get; set; }
	public bool? HasRemoteEntry { get; set; }
    public int? HpVersionId { get; set; }
    public bool? HasRemoteVersion { get; set; }
    public bool? Owner { get; set; }

    public bool? Exists
    {
        get
        {
            field ??= Info?.Exists;
            if (field is not null) return field;
            if (FullPath is not null) field ??= Path.Exists(FullPath);

            return field;
        }
    }


    public HackFile() {}
    public HackFile(HackFile hack)
    {
        AssignToSelf( hack );
    }
    public HackFile(
        string name,
        string? fullPath=null,
        string? typeExt=null, 
        DateTime modifiedDate=default, 
        string? sha1Checksum=null,
        long? fileSize=null,
        int? hpVersionId=null, 
		int? hpEntryId=null,
		bool? hasRemoteEntry=null,
        bool? hasRemoteVersion=null,
        string? basePath=null,
        string? relativePath=null)
    {
        if (fullPath is not null and not "")
        {
            FileInfo file = new(fullPath);
            if (file.Exists)
            {
                this.Info = file;
            }
        }
        // base class
        this.FullPath = fullPath;
        this.Name = name;
        this.DirectoryName = basePath;
        this.RelativePath = relativePath;

        // this class
        this.TypeExt = typeExt;
        this.ModifiedDate = modifiedDate;
        this.Checksum = sha1Checksum;
        this.HpVersionId = hpVersionId;
		this.HpEntryId = hpEntryId;
		this.HasRemoteEntry = hasRemoteEntry;
        this.HasRemoteVersion = hasRemoteVersion;
        this.FileSize = fileSize;
    }
    public HackFile(FileInfo? file)
    {
		if (file is null)
		{
			NullSelf();
			return;
		}
        Info = file;
        Name = file.Name;
        DirectoryName = file?.DirectoryName;
        FullPath = file?.FullName;
        TypeExt = file?.Extension;
        ModifiedDate = file?.LastWriteTime ?? default;
        FileSize = file?.Length;
        Checksum = FileOperations.FileChecksum( file?.FullName, SHA1.Create() );
    }
	public HackFile(EntryRow entry)
	{
		if (entry.LocalFile is not { }) { NullSelf(); return; }
		InitializeFromEntryRow(entry, true);
	}
    public HackFile(string? fullPath) => InitializeHackFromPath( fullPath );
	internal void InitializeFromEntryRow(EntryRow entry, bool initFileInfo=false)
	{
		this.FullPath = entry.ReprType is EntryReprType.Local
			? entry.FullName
			: FileOperations.ConvertToWindowsPath(entry.FullName, true);
		
		if (initFileInfo)
		{
			try
			{
				if (string.IsNullOrEmpty(this.FullPath))
				{
					throw new Exception();
				}

				this.Info = new(this.FullPath);
				if (!this.Info.Exists) this.Info = null;
			}
			catch { }
		}
		this.Name = this.Info?.Name ?? entry.Name;
		this.DirectoryName = this.Info?.DirectoryName ?? Path.GetDirectoryName(FullPath);
		this.FileSize = this.Info?.Length ?? entry.Size;
		this.ModifiedDate = this.Info?.LastWriteTime ?? entry.LocalDate ?? entry.RemoteDate ?? default;
		this.TypeExt = this.Info?.Extension ?? entry.Type;
		this.HpVersionId = entry.LatestId;
		this.HpEntryId = entry.Id;
		this.HasRemoteEntry = entry.Id is not (null or 0);
		this.HasRemoteVersion = entry.LatestId is not (null or 0);
	}
    internal void ApplyModifiedDateToLocal()
    {
        if (OverwriteDate == default) return;
        Info ??= new(FullPath);
        if (Exists is true)
        {
            try
            {
                Info?.LastWriteTime = OverwriteDate;
            }
            catch { }
        }
    }
    internal void ApplyNonOwnerReadOnly()
    {
		if (FullPath is not null) Info ??= new(FullPath);
        if (Owner is true) return;
        if (Exists is true)
        {
            try
            {
                Info?.Attributes |= FileAttributes.ReadOnly;
            }
            catch { }
        }
    }
    public void SetModifiedDate(DateTime date)
    {
	    OverwriteDate = date;
    }
	private void NullSelf()
	{
		Info = null;
		Name = null;
		DirectoryName = null;
		FullPath = null;
		TypeExt = null;
		ModifiedDate = default;
		FileSize = null;
		Checksum = null;
	}
    public void InitializeHackFromPath(string? path) => AssignToSelf(GetFromPath(path));
    private void AssignToSelf(HackFile? hack)
    {
        this.Info = hack?.Info;
        this.FullPath = hack?.FullPath;
        this.Name = hack?.Name;
        this.DirectoryName = hack?.DirectoryName;
        this.RelativePath = hack?.RelativePath;
        this.TypeExt = hack?.TypeExt;
        this.ModifiedDate = hack?.ModifiedDate ?? default;
        this.Checksum = hack?.Checksum;
        this.HpVersionId = hack?.HpVersionId;
        this.HasRemoteVersion = hack?.HasRemoteVersion;
        this.FileContents = hack?.FileContents;
        this.FileSize = hack?.FileSize;
    }
    public static async Task<HackFile> GetFromFileInfo( FileInfo file )
		=> new(file);
	public static async void ApplyFileInfoToHackFileAsync( FileInfo file, HackFile hack)
	{
		hack.Info = file;
		hack.Name = file.Name;
		hack.DirectoryName = file.DirectoryName;
		hack.FullPath = file.FullName;
		hack.TypeExt = file.Extension;
		hack.ModifiedDate = file.LastWriteTime;
		hack.FileSize = file.Length;
		hack.Checksum = await FileOperations.FileChecksumAsync( file.FullName, SHA1.Create() );
	}

    internal static HackFile? GetFromPath(EntryRow? entry, bool initWithFileInfo=true)
    {
        if (entry is null) return null;
        HackFile? hack;
        if (initWithFileInfo)
        {
            hack = new(entry);
            if (hack.Info is null || !hack.Info.Exists) return hack;   
        }
        else
        {
            hack = GetHackFromPathUninitialized(entry.FullName, null);
        }
        hack?.RelativePath = Path.Combine("root", hack?.DirectoryName?[Math.Min(HackDefaults.Instance.PwaPathAbsolute.Length+1, hack.DirectoryName.Length)..] ?? "");
        return hack;
    }
	public static HackFile? GetFromPath(string? path, string? directory = null, bool initWithFileInfo=true)
    {
		if (path is null) return null;
        HackFile? hack;
		if (initWithFileInfo)
		{
			FileInfo file = new(path);
			hack = new(file);
			if (!file.Exists) return hack;
		}
		else
		{
			hack = GetHackFromPathUninitialized(path, directory);
		}

        hack?.RelativePath = Path.Combine("root", hack?.DirectoryName?[Math.Min(HackDefaults.Instance.PwaPathAbsolute.Length+1, hack.DirectoryName.Length)..] ?? "");
        return hack;
    }
    private static HackFile? GetHackFromPathUninitialized(string? path, string? directory=null)
	{
		HackFile hack = new()
		{
			Name = Path.GetFileName(path),
			FullPath = Path.GetFullPath(path ?? ""),
			DirectoryName = directory ?? Path.GetDirectoryName(path),
			TypeExt = Path.GetExtension(path),
			ModifiedDate = File.GetLastWriteTime(path ?? ""),
		};
		return hack;
	}
    
    
	// ////////////////////////////////////////
	// ////////////////////////////////////////
	
	
	

	// ///////////////////////////////////////////////////////
	// ///////////////////////////////////////////////////////

    
    public static HackFile GetFromVersion(IHpVersionModel versionModel)
    {
        if (versionModel.windows_complete_name == null) return null;
        HackFile hack = GetFromPath(Path.Combine(HackDefaults.Instance.PwaPathAbsolute, versionModel.windows_complete_name[HpBaseModel.ROOT_OFFSET..]), Path.Combine(HackDefaults.Instance.PwaPathRelative, versionModel.windows_complete_name[HpBaseModel.ROOT_OFFSET..(versionModel.windows_complete_name.Length - versionModel.name.Length)]));
        if (hack != null && hack.Checksum == versionModel.checksum)
        {
            hack.HasRemoteVersion = true;
            hack.HpVersionId = versionModel.id;
        }
        return hack;
    }
    public static bool GetLocalVersion(in IHpVersionModel versionModel, out HackFile hackFile)
    {
        hackFile = GetFromVersion(versionModel);
        if ( hackFile == null ) return false;

        return IsLocalVersion(versionModel, hackFile);
    }
    
    public static bool IsLocalVersion(in IHpVersionModel versionModel, in HackFile hackFile)
    {
        //if (HasLocalVersion(hackFile) && hackFile?.HpVersionID == version.ID) return true;
        if (hackFile.Checksum == versionModel.checksum) return true;
        return false;
    }
    public static bool GetLocalVersion(in IHpVersionModel[] versions, out HackFile hackFile)
    {
        hackFile = null;
        foreach(IHpVersionModel version in versions)
        {
            if (hackFile != null)
            {
                if (IsLocalVersion(version, hackFile)) return true;
            }
            else
            {
                if (GetLocalVersion(version, out hackFile)) return true;
            }
        }
        return false;
    }
    

    public static async Task<(int, long)?> CreateFiles(params HackFile[] hackFiles)
    {
        List<Task<bool>> tasks = [];
        int success = 0;
        long sizeAgg = 0;

        foreach (HackFile file in hackFiles)
        {
            if (file.FileContents is { Length: > 0 })
                tasks.Add(FileOperations.WriteAllBytesAsync(file));
            
            if (file.FileSize is long size)
            {
	            sizeAgg += size;
            }
        }
        Task<bool[]> waitTask = Task.WhenAll(tasks);
        await waitTask;

        foreach (bool val in waitTask.Result) success += val ? 1 : 0;
        return (success, sizeAgg);
    }
    public long? CreateFile() => FileOperations.WriteAllBytes(this);

    public override bool Equals( object obj )
    {
        string filePath = "";
        HackFile hack = obj as HackFile;
        IHpVersionModel versionModel = hack == null ? obj as IHpVersionModel : null;

        if ( hack is not null || versionModel is not null )
        {
            if ( this.FullPath is not null and not "" )
            {
                filePath = this.FullPath;
            }
            if ( filePath is ""
                 && this.DirectoryName is not null and not ""
                 && this.Name is not null and not "" )
            {
                filePath = Path.Combine( this.DirectoryName, this.Name );
            }
        }

        if ( hack is not null )
        {
            if (this.HpVersionId is not null and not 0 )
            {
                if (this.HpVersionId == hack.HpVersionId ) return true;
                    
            }
            if (this.Checksum is not null and not "" )
            {
                if ( this.Checksum == hack.Checksum ) return true;
					
            }
            if (hack.Checksum is not null and not "")
            {

                if ( filePath is not "" )
                {
                    string checksum = FileOperations.FileChecksum( this.FullPath, SHA1.Create() );
                    if ( checksum == hack.Checksum ) return true;
                }
            }
        }
			
        if ( versionModel is not null )
        {
            if ( this.Checksum is not null and not "" )
            {
                if ( this.Checksum == versionModel.checksum )
                    return true;
            }
            if ( versionModel.checksum is not null and not "" )
            {
                if ( this.FullPath is not null and not "" )
                {
                    filePath = this.FullPath;
                }
                if ( filePath is ""
                     && this.DirectoryName is not null and not ""
                     && this.Name is not null and not "" )
                {
                    filePath = Path.Combine( this.DirectoryName, this.Name );
                }
                if ( filePath is not "" )
                {
                    string checksum = FileOperations.FileChecksum( this.FullPath, SHA1.Create() );
                    if ( checksum == versionModel.checksum )
                        return true;
                }
            }
        }

        return false;
    }
    public override int GetHashCode()
    {
		HashCode hash = new();
        hash.Add( this.Name );
        hash.Add( this.FullPath );
        hash.Add( this.Checksum );
        return hash.ToHashCode();
    }
    public async static Task<HackFile?> GetHackFileAsync(string? fullFilePath)
    {
	    if (string.IsNullOrEmpty(fullFilePath)) return null;
	    HackFile hackFile = default;

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

    public static implicit operator HackFile( FileInfo file)
	{
		return new HackFile( file );
	}
}
