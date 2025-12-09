using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using HackPDM.ClientUtils;
using HackPDM.Data;
using HackPDM.Extensions.General;
using HackPDM.Forms.Hack;
using HackPDM.Forms.Settings;
using HackPDM.Odoo;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Properties;
using HackPDM.Src.ClientUtils.Types;

using Microsoft.UI.Xaml.Controls;

using SolidWorks.Interop.swdocumentmgr;

namespace HackPDM.Hack;

public static class HackDefaults
{
    public static string PwaPathAbsolute 
    { 
        get => Settings.Get<string>("PWAPathAbsolute"); 
        set => Settings.Set("PWAPathAbsolute", value);
    }
    public static string PwaPathRelative
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
    public static string MeasureFileSize 
    { 
        get => Settings.Get<string>("MeasureFileSize"); 
        set => Settings.Set("MeasureFileSize", value);
    }
    public static double MeasureByteSize 
    { 
        get => Settings.Get<double>("MeasureByteSize");
        set => Settings.Set("MeasureByteSize", value);
    }
    public static double FileSizeMult
    {
        get => Settings.Get<double>("FileSizeMult");
        set => Settings.Set("FileSizeMult", value);
    }
    public static double? ByteSizeMultiplier
    {
        get
        {
            if (field == null)
            {
                field = 1D /  Math.Pow( MeasureByteSize, FileSizeMult ) ;
            }
            return field;
        }
    } = null;
    public static string CurrentPath { get; set; }

    public static SwDocMgr DocMgr
    {
        get
        {
            return field ??= new(OdooDefaults.SwApi);
        }
        set;
    }
    public static SwHelper SwHelper
    {
        get
        {
            return field ??= new();
        }
        set;
    }

    public static bool GetFiles(string relativePath, out IEnumerable<string> files)
    {
        CurrentPath = Path.Combine(PwaPathAbsolute, relativePath);
        try
        {         
            // EnumerateFiles goes off a relative path from your project
            files = Directory.EnumerateFiles(CurrentPath, "*", SearchOption.AllDirectories);
                
            return true;
        }
        catch (DirectoryNotFoundException e)
        {
            Console.WriteLine(e.Message);
            files = null;
            return false;
        }
    }
    public static void CreateDirectories(DirectoryDict directory)
    {
        RecurseTravel(directory, PwaPathAbsolute);
    }
    public static void CreateDirectories(DirectoryDict[] directories)
    {
        foreach (DirectoryDict hdr in directories)
        {
            RecurseTravel(hdr, PwaPathAbsolute + "\\" + hdr.Name);
        }
    }
    public static string DefaultPath(string? pathway, bool withAbsolute = false)
    {
        if (pathway is null || pathway == "") return withAbsolute ? PwaPathAbsolute : "root";
        string[] paths = pathway.Split('\\');
        paths = [.. paths.Skip(1)];

        string relativePath = string.Join(@"\", paths);

        if (withAbsolute) return Path.Combine(PwaPathAbsolute, relativePath);
            
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

public class HackFile : HackBaseFile
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
            if (_overwriteDate != default)
            {
                field = _overwriteDate;
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
    private DateTime _overwriteDate = default;
    public string? Checksum { get; private set; }
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
		get => field ??= Info?.Exists ?? Path.Exists(FullPath);
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
        this.BasePath = basePath;
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
        BasePath = file?.DirectoryName;
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
			: HpDirectory.ConvertToWindowsPath(entry.FullName, true);
		
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
		this.BasePath = this.Info?.DirectoryName ?? Path.GetDirectoryName(FullPath);
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
        if (_overwriteDate == default) return;
        Info ??= new(FullPath);
        if (Exists is true)
        {
            try
            {
                Info?.LastWriteTime = _overwriteDate;
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
        _overwriteDate = date;
    }
	private void NullSelf()
	{
		Info = null;
		Name = null;
		BasePath = null;
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
        this.BasePath = hack?.BasePath;
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
		hack.BasePath = file.DirectoryName;
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
        hack?.RelativePath = Path.Combine("root", hack?.BasePath?[Math.Min(HackDefaults.PwaPathAbsolute.Length+1, hack.BasePath.Length)..] ?? "");
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

        hack?.RelativePath = Path.Combine("root", hack?.BasePath?[Math.Min(HackDefaults.PwaPathAbsolute.Length+1, hack.BasePath.Length)..] ?? "");
        return hack;
    }
    private static HackFile? GetHackFromPathUninitialized(string? path, string? directory=null)
	{
		HackFile hack = new()
		{
			Name = Path.GetFileName(path),
			FullPath = Path.GetFullPath(path ?? ""),
			BasePath = directory ?? Path.GetDirectoryName(path),
			TypeExt = Path.GetExtension(path),
			ModifiedDate = File.GetLastWriteTime(path ?? ""),
		};
		return hack;
	}
    public static List<HackFile> FolderPathToHackWithDependencies(string pathway, SearchOption options = SearchOption.AllDirectories)
    {
        // get all files in folder path to commit.
        string[] files = [.. Directory.EnumerateFiles(pathway, "*", options)];
        return FilePathsToHackWithDependencies(files);
    }
    
	// ////////////////////////////////////////
	// ////////////////////////////////////////
	public static List<HackFile> GetHackFolderWithDependencies(string folderPath, bool listOutputDialog)
	{
		Regex rxSearch = new Regex(OdooDefaults.DependentExtRegex, RegexOptions.IgnoreCase);
		var filesInDir = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories);
		var matchedFiles = filesInDir.SegmentWhere(file => rxSearch.IsMatch(file ?? ""));
		var allHackDependencyFiles = matchedFiles.Item1.SelectMany(m => GetHackFileWithDependencies(m, true));
		List<HackFile> allcombinedFiles = [.. allHackDependencyFiles, .. matchedFiles.Item2.Select(s=>new HackFile(s)).SkipWhile(hf => hf.Exists is null or false)];
		return allcombinedFiles;
	}
	public static List<HackFile> GetHackFileWithDependencies(HackFile file, bool listOutputDialog)
	{
		List<HackResultTree.ResultNode> hackResults = [];
		ResultHackFile rHack = new(file);
		var hackResultCode = TryFilePathToHackWithDependencies(out var list, rHack);
		HackResultTree hackTree = new(rHack);
		HackResultTree.ResultNode hackTreeNode = hackTree.Root;
		var queue = new Queue<HackResultTree.ResultNode>(list.Select(r => new HackResultTree.ResultNode(r)));
		
		while (queue.TryDequeue(out HackResultTree.ResultNode? result))
		{
			if (listOutputDialog)
			{
				(StatusMessage status, string message) = Help.GetStatusMessage(hackResultCode, rHack, list);
				HackFileManager.Dialog?.AddStatusLine(status, message);
			}

			if (hackResultCode is not HackResult.Clean) continue;
			
			hackResults.Add(result);
			foreach (ResultHackFile resultHack in list)
			{
				queue.Enqueue(new(resultHack));
			}
			hackResultCode = TryFilePathToHackWithDependencies(out list, result.Value);
		}

		return [.. hackResults.SkipSelect(r => r.Value.Hack is null, node => node.Value.Hack!)];
	}
	public static List<HackFile> GetHackFileWithDependencies(EntryRow? entry, bool listOutputDialog)
		=> entry is null ? [] : GetHackFileWithDependencies(new HackFile(entry), true);
	public static List<HackFile> GetHackFileWithDependencies(string? filePath, bool listOutputDialog)
		=> filePath is null ? [] : GetHackFileWithDependencies(new HackFile(new FileInfo(filePath)), listOutputDialog);

	public static bool TryFilePathsToHackWithDependencies(out (List<ResultHackFile> depAllInPWAorBrokenList, ResultHackFile parentFile)[] hackFiles, params string[] filePaths)
	{
		hackFiles = new (List<ResultHackFile>, ResultHackFile)[filePaths.Length];
		bool hasErrors = false;
		for (int i = 0; i < filePaths.Length; i++)
		{
			// once true, always true
			hasErrors |= TryFilePathToHackWithDependencies(out var hd, out var pf, filePaths[i]) is not HackResult.Clean;
			hackFiles[i] = (hd, pf);
		}
		return hasErrors;
	}
	public static HackResult TryFilePathToHackWithDependencies(out List<ResultHackFile> depAllInPWAorBrokenList, ResultHackFile parentFile)
	{
		List<string> newFiles = [];
		depAllInPWAorBrokenList = [];
		HackResult hackResultCode = HackResult.Clean;

		// find all dependencies
		if (parentFile is not { Result: HackResult.Clean } || !(parentFile.Hack?.TypeExt is { } ext && OdooDefaults.DependentExt.Contains($"{ext}"))) 
		{
			hackResultCode = parentFile.Result;
			return hackResultCode;
		}
		
		var dependencies = HackDefaults.DocMgr.GetDependencies(parentFile.Hack.FullPath!);
		var dependencies2test = HackDefaults.DocMgr.GetDependencies(parentFile.Hack.FullPath!, true);
		if (dependencies is not {Count: > 0}) return hackResultCode;
		
		foreach (var path in dependencies.Select(deps => deps[1]))
		{
			if (!FileOperations.InPWAFolder(path))
			{
				ResultHackFile resHack = new(GetFromPath(path, FileOperations.GetRelativePath(path)), HackTestDepth.FileExistsTest);
				depAllInPWAorBrokenList = [
					resHack
				];
				hackResultCode = resHack.Result;
				return hackResultCode;
			}
			newFiles.Add(path);
		}
		
		foreach (var result in newFiles.Select(Help.ValidateDependency))
		{
			if (result.Result is not HackResult.Clean) 
			{
				depAllInPWAorBrokenList = [result];
				return hackResultCode;
			}
			depAllInPWAorBrokenList.Add(result);
		}
		return hackResultCode;
	}
	public static HackResult TryFilePathToHackWithDependencies(out List<ResultHackFile> depAllInPWAorBrokenList, out ResultHackFile parentFile, string filePath)
	{
		var fInfo = new HackFile(new FileInfo(filePath));
		parentFile = new(fInfo);
		return TryFilePathToHackWithDependencies(out depAllInPWAorBrokenList, parentFile);
	}

	// ///////////////////////////////////////////////////////
	// ///////////////////////////////////////////////////////

    public static List<HackFile> FilePathsToHackWithDependencies(params string[] filePaths)
    {
        HashSet<string> newFiles = [.. filePaths];
        List<HackFile> hackFiles = [];
        // find all dependencies
        foreach (string file in filePaths)
        {
            var fInfo = new FileInfo(file);
            if (OdooDefaults.DependentExt.Contains(fInfo.Extension))
            {
                var dependencies = HackDefaults.DocMgr.GetDependencies(file);
                if (dependencies is not null && dependencies.Count > 0)
                {
                    foreach (string[] deps in dependencies)
                    {
                        string path = deps[1];
                        var splitPath = path.Split([$"\\{HackDefaults.PwaPathRelative}\\"], StringSplitOptions.RemoveEmptyEntries);
                        if (splitPath.Length == 2)
                        {
                            newFiles.Add(Path.Combine([HackDefaults.PwaPathAbsolute, splitPath[1]]));
                        }
                    }
                }
            }
        }

        foreach (string item in newFiles)
        {
            HackFile hack = GetFromPath(item, FileOperations.GetRelativePath(item));
            if (hack != null)
                hackFiles.Add(hack);
        }
        return hackFiles;
    }
    public static HackFile GetFromVersion(HpVersion version)
    {
        if (version.WinPathway == null) return null;
        HackFile hack = GetFromPath(Path.Combine(HackDefaults.PwaPathAbsolute, version.WinPathway, version.name), Path.Combine(HackDefaults.PwaPathRelative, version.WinPathway));
        if (hack != null && hack.Checksum == version.checksum)
        {
            hack.HasRemoteVersion = true;
            hack.HpVersionId = version.Id;
        }
        return hack;
    }
    public static bool GetLocalVersion(in HpVersion version, out HackFile hackFile)
    {
        hackFile = GetFromVersion(version);
        if ( hackFile == null ) return false;

        return IsLocalVersion(version, hackFile);
    }
    public static bool HasLocalVersion(in HackFile hackFile, out HpVersion version)
    {
        version = null;
        if (hackFile == null) return false;
        if (hackFile.HasRemoteVersion != null && (bool)hackFile.HasRemoteVersion && hackFile.HpVersionId != null)
        {
            version = HpVersion.GetRecordById((int)hackFile.HpVersionId, HpVersion.UsualExcludedFields);
            return true;
        }


        return false;
    }
    public static bool IsLocalVersion(in HpVersion version, in HackFile hackFile)
    {
        //if (HasLocalVersion(hackFile) && hackFile?.HpVersionID == version.ID) return true;
        if (hackFile.Checksum == version.checksum) return true;
        return false;
    }
    public static bool GetLocalVersion(in HpVersion[] versions, out HackFile hackFile)
    {
        hackFile = null;
        foreach(HpVersion version in versions)
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
    public static bool GetVersionFromLocal(HackFile hackFile, out HpVersion version)
    {
        string filePath = HpDirectory.WindowsToOdooPath(hackFile.RelativePath);
        ArrayList arrList =
        [
            new ArrayList()
            {
                new ArrayList() { "name", "=", hackFile.Name },
                new ArrayList() { "directory_complete_name", "=", filePath },
            }
        ];
        version = HpVersion.GetRecordsBySearch(arrList, ["file_contents", "preview_image"])?[0];

        return version != null;
    }

    public static async Task<int> CreateFiles(params HackFile[] hackFiles)
    {
        List<Task<bool>> tasks = [];
        int success = 0;

        foreach (HackFile file in hackFiles)
        {
            if (file.FileContents != null && file.FileContents.Length > 0)
                tasks.Add(FileOperations.WriteAllBytesAsync(file));
            if (file.FileSize is long size)
            {
                StatusData.StaticData.DownloadBytes += size;
                StatusData.SessionDownloadBytes += size;
            }
        }
        Task<bool[]> waitTask = Task.WhenAll(tasks);
        await waitTask;

        foreach (bool val in waitTask.Result) success += val ? 1 : 0;
        return success;
    }
    public bool CreateFile()
    {
        return FileOperations.WriteAllBytes(this);
    }
    public override bool Equals( object obj )
    {
        string filePath = "";
        HackFile hack = obj as HackFile;
        HpVersion version = hack == null ? obj as HpVersion : null;

        if ( hack is not null || version is not null )
        {
            if ( this.FullPath is not null and not "" )
            {
                filePath = this.FullPath;
            }
            if ( filePath is ""
                 && this.BasePath is not null and not ""
                 && this.Name is not null and not "" )
            {
                filePath = Path.Combine( this.BasePath, this.Name );
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
			
        if ( version is not null )
        {
            if ( this.HpVersionId is not null and not 0 )
            {
                if ( this.HpVersionId == version.Id )
                    return true;
            }
            if ( this.Checksum is not null and not "" )
            {
                if ( this.Checksum == version.checksum )
                    return true;
            }
            if ( version.checksum is not null and not "" )
            {
                if ( this.FullPath is not null and not "" )
                {
                    filePath = this.FullPath;
                }
                if ( filePath is ""
                     && this.BasePath is not null and not ""
                     && this.Name is not null and not "" )
                {
                    filePath = Path.Combine( this.BasePath, this.Name );
                }
                if ( filePath is not "" )
                {
                    string checksum = FileOperations.FileChecksum( this.FullPath, SHA1.Create() );
                    if ( checksum == version.checksum )
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

	public static implicit operator HackFile( FileInfo file)
	{
		return new HackFile( file );
	}
	internal static SwDmDocumentType GetSwDmDocumentTypeFromExtension(string file_ext)
		=> file_ext switch
		{
			"sldprt" => SwDmDocumentType.swDmDocumentPart,
			"sldasm" => SwDmDocumentType.swDmDocumentAssembly,
			"slddrw" => SwDmDocumentType.swDmDocumentDrawing,
			_ => SwDmDocumentType.swDmDocumentUnknown,
		};
}
public struct DirectoryDict : IConvert<DirectoryDict>
{
    public DirectoryDict[] Directories;
    public string Name;
    public HpEntryReturn[] Entries;
    public int Id;

    public DirectoryDict ConvertFromHt(Hashtable ht) => ht;
    public static implicit operator DirectoryDict(Hashtable ht)
    {
        DirectoryDict[] directories = 
            HackDefaults.ArrayListToModelsArray<DirectoryDict>((ArrayList)ht["directories"]);
        HpEntryReturn[] entries = 
            HackDefaults.ArrayListToModelsArray<HpEntryReturn>((ArrayList)ht["entries"]);

        return new DirectoryDict
        {
            Directories = [.. directories],
            Entries = [.. entries],
            Id = (int)ht["id"],
            Name = (string)ht["name"],
        };
    }
}
public struct HpEntryReturn : IConvert<HpEntryReturn>
{
    public string Name;
    public int Id;

    public HpEntryReturn ConvertFromHt(Hashtable ht) => ht;

    public static implicit operator HpEntryReturn(Hashtable ht)
    {
        return new HpEntryReturn
        {
            Id = (int)ht["id"],
            Name = (string)ht["name"],
        };
    }
}
public struct HackFileResults
{
	public List<HackFile> CleanFiles { get; private set; } = new();
	public List<HackFile> BrokenFiles { get; private set; } = new();

	public HackFileResults(List<HackFile> cleanFiles, List<HackFile> brokenFiles)
	{

	}
}
public class HackResultTree(ResultHackFile value)
{
	public class ResultNode : IList<ResultNode>
	{
		public ResultNode? Parent { get; set; }
		public ResultHackFile Value { get; set; }
		public List<ResultNode> Children { get; set; }
		public ResultNode(ResultHackFile result)
		{
			Value = result;
		}

		public int Count { get; }
		public bool IsReadOnly { get; }
		public ResultNode this[int index]
		{
			get => Children[index];
			set => Children[index] = value;
		}
		public IEnumerator<ResultNode> GetEnumerator() => Children.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public void Add(ResultNode item) => Children.Add(item);
		public void Clear() => Children.Clear();
		public bool Contains(ResultNode item) => Children.Contains(item);
		public void CopyTo(ResultNode[] array, int arrayIndex) => Children.CopyTo(array, arrayIndex);
		public bool Remove(ResultNode item) => Children.Remove(item);

		public int IndexOf(ResultNode item) => Children.IndexOf(item);
		public void Insert(int index, ResultNode item) =>  Children.Insert(index, item);
		public void RemoveAt(int index) => Children.RemoveAt(index);
	}
	public ResultNode Root { get; set; } = new(value);
}
public class ResultHackFile
{
	public bool IsBroken { get; private set; }
	public bool IsTested { get; private set; }
	public HackTestDepth TestDepth { get; private set; }
	public HackResult Result { get; private set; } = HackResult.NotTested;
	public HackFile? Hack { get; }

	public ResultHackFile(HackFile? hack, HackTestDepth test = HackTestDepth.InPWATest)
	{
		this.Hack = hack;
		DoTest(test);
	}

	private void Init(HackFile? hack, HackTestDepth test)
	{
		DoTest();
		this.Result = hack is null or { Exists: false } 
			? HackResult.MissingFile 
			: FileOperations.InPWAFolder(hack?.FullPath) 
				? HackResult.Clean 
				: HackResult.OutOfPWA;
		this.IsBroken = this.Result is HackResult.Clean or HackResult.NotTested;
	}
	public void DoTest() => DoTest(TestDepth);
	public void DoTest(HackTestDepth test)
	{
		if (test != TestDepth) TestDepth = test;
		
		this.Result = test switch
		{
			HackTestDepth.InPWATest			=> TestPWA(),
			HackTestDepth.FileExistsTest	=> TestFileExists(),
			HackTestDepth.FileIsCorruptTest	=> TestFileIsCorrupt(),
			_								=> HackResult.NotTested,
		};
		
		this.IsBroken = this.Result is HackResult.Clean or HackResult.NotTested;
		IsTested = true;
	}

	private HackResult TestPWA()
	{
		if (string.IsNullOrEmpty(Hack?.FullPath)) return HackResult.MissingFile;
		return Hack.FullPath is not null && FileOperations.InPWAFolder(Hack.FullPath)
			? HackResult.Clean
			: HackResult.OutOfPWA;
	}
	private HackResult TestFileExists()
	{
		var resultPWA = TestPWA();
		if (resultPWA is not HackResult.Clean) return resultPWA;
		if (Hack!.Exists is true) return HackResult.Clean;
		if (Hack!.Exists is null)
		{
			Hack.Info ??= new FileInfo(Hack.FullPath ?? "");
		}
		return Hack!.Exists is true
			? HackResult.Clean
			: HackResult.MissingFile;
	}
	private HackResult TestFileIsCorrupt() => TestFileExists();
}

[Flags]
public enum HackTestDepth
{
	NoTest,
	InPWATest,
	FileExistsTest,
	FileIsCorruptTest,
}
public enum HackResult
{
	NotTested,
	Clean,
	OutOfPWA,
	PropogatedFailure,
	CorruptDepFile,
	MissingFile,
	MissingDepFile,
}