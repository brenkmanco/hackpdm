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
	            // FileInfo reports 1601-01-01 for a missing file, so only a file on disk has a date
	            field = value == default
		            ? Info is { Exists: true } info ? info.LastWriteTime.ToUniversalTime() : default
					: value;
            }
            else
            {
                if (Exists is true)
                {
                    Info?.LastWriteTime = value;
                    field = value == default
	                    ? Info?.LastWriteTime.ToUniversalTime() ?? default
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
        // null while the file isn't on disk (FileInfo.Length throws for a missing file)
        get => field ??= (Info is { Exists: true } info
			? info.Length
			: FullPath is null || (Info = new FileInfo(FullPath)) is not { Exists: true } found
				? null
				: found.Length);
        set => field = value ?? (Info is { Exists: true } info ? info.Length : null);
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
    public HackFile(FileInfo? file) : this(file, computeChecksum: true) {}
    /// <param name="computeChecksum">
    /// false skips reading and hashing the whole file; <see cref="GetChecksum"/> computes it on demand.
    /// </param>
    public HackFile(FileInfo? file, bool computeChecksum)
    {
		if (file is null)
		{
			NullSelf();
			return;
		}
        Info = file;
        Name = file.Name;
        DirectoryName = file.DirectoryName;
        FullPath = file.FullName;
        TypeExt = file.Extension;
        // a file that isn't on disk yet (about to be downloaded, say) has no date, size or checksum
        if (!file.Exists) return;
        ModifiedDate = file.LastWriteTime.ToUniversalTime();
        FileSize = file.Length;
        if (computeChecksum)
            Checksum = Checksums.OfFile( file.FullName );
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
			: PwaPaths.ToAbsolute( entry.FullName );
		
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
		this.ModifiedDate = this.Info?.LastWriteTime.ToUniversalTime() ?? entry.LocalDate ?? entry.RemoteDate ?? default;
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
    public string? GetChecksum() => Checksum ??= Checksums.OfFile( FullPath ) ;
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
        hack?.RelativePath = Path.Combine([PwaPaths.Root, .. PwaPaths.LocalSegments(hack?.DirectoryName) ?? []]);
        return hack;
    }
	public static HackFile? GetFromPath(string? path, string? directory = null, bool initWithFileInfo=true, bool computeChecksum=true)
    {
		if (path is null) return null;
        HackFile? hack;
		if (initWithFileInfo)
		{
			FileInfo file = new(path);
			hack = new(file, computeChecksum);
			if (!file.Exists) return hack;
		}
		else
		{
			hack = GetHackFromPathUninitialized(path, directory);
		}

        hack?.RelativePath = Path.Combine([PwaPaths.Root, .. PwaPaths.LocalSegments(hack?.DirectoryName) ?? []]);
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
			ModifiedDate = File.GetLastWriteTime(path ?? "").ToUniversalTime(),
		};
		return hack;
	}
    
    
	// ////////////////////////////////////////
	// ////////////////////////////////////////
	
	
	

	// ///////////////////////////////////////////////////////
	// ///////////////////////////////////////////////////////

    
    
    

    public static async Task<(int, long)?> CreateFiles(params HackFile[] hackFiles)
    {
        List<Task<bool>> tasks = [];
        int success = 0;
        long sizeAgg = 0;

        foreach (HackFile file in hackFiles)
        {
            if (file.FileContents is { Length: > 0 })
                tasks.Add(LocalFileWriter.WriteAsync(file));
            
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
    public long? CreateFile() => LocalFileWriter.Write(this);

    // a HackFile stands for a file on disk: two are equal when they point at the same path
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private string? IdentityPath
    {
        get
        {
            string? path = FullPath is { Length: > 0 } full
                ? full
                : DirectoryName is { Length: > 0 } dir && Name is { Length: > 0 } name ? Path.Combine(dir, name) : null;
            if (path is null) return null;
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }
    }

    /// <summary>
    /// Same file path (version ids and checksums aren't identity: identical copies at two paths are
    /// two files). Consistent with <see cref="GetHashCode"/>, so hash sets of files de-duplicate.
    /// </summary>
    public override bool Equals( object? obj )
    {
        if ( ReferenceEquals( this, obj ) ) return true;
        if ( obj is not HackFile other ) return false;
        string? mine = IdentityPath, theirs = other.IdentityPath;
        return mine is not null && theirs is not null && PathComparer.Equals( mine, theirs );
    }
    public override int GetHashCode() => IdentityPath is { } path ? PathComparer.GetHashCode( path ) : 0;
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
