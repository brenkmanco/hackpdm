using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swdocumentmgr;

using DateTime = System.DateTime;

//using static System.Net.Mime.MediaTypeNames;


using OClient = HackPDM.Infrastructure.Odoo.OdooClient;
// Resharper disable InconsistentNaming

namespace HackPDM.Infrastructure.Odoo.Models;

[OdooModel(OdooDefaultsConstants.HP_VERSION_NAME, OdooDefaultsConstants.HP_VERSION)]
public partial class HpVersion : HpBaseModelTransport<HpVersion>, IHpVersionModel
{
	[OdooProp(OdooFieldType.Char, "name")] public string? name { get; set; }
	[OdooProp(OdooFieldType.Char, "file_ext")] public string? file_ext { get; set; }
	[OdooProp(OdooFieldType.Char, "checksum")] public string? checksum { get; set; }
    [OdooProp(OdooFieldType.Char, "windows_complete_name")] public string? windows_complete_name { get; set; }
    
	[OdooProp(OdooFieldType.Many2One, "entry_id")] public Many2One? entry_id { get; set; }
	IMany2One? IHpVersionModel.entry_id { get =>(IMany2One?)entry_id; set => entry_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "node_id")] public Many2One? node_id { get; set; }
	IMany2One? IHpVersionModel.node_id { get =>(IMany2One?)node_id; set => node_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "dir_id")] public Many2One? dir_id { get; set; }
	IMany2One? IHpVersionModel.dir_id { get =>(IMany2One?)dir_id; set => dir_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "checkout_user")] public Many2One? checkout_user { get; set; }
	IMany2One? IHpVersionModel.checkout_user { get => (IMany2One?)checkout_user; set => checkout_user = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2One, "attachment_id")] public Many2One? attachment_id { get; set; }
	IMany2One? IHpVersionModel.attachment_id { get =>(IMany2One?)attachment_id; set => attachment_id = (Many2One?)value; }

	[OdooProp(OdooFieldType.Many2Many, "parent_ids")] public Many2Many? parent_ids { get; set; }
	[OdooProp(OdooFieldType.Many2Many, "child_ids")] public Many2Many? child_ids { get; set; }
	IMany2Many? IHpVersionModel.parent_ids { get => (IMany2Many?)parent_ids; set => parent_ids = (Many2Many?)value; }
	IMany2Many? IHpVersionModel.child_ids { get => (IMany2Many?)child_ids; set => child_ids = (Many2Many?)value; }


	[OdooProp(OdooFieldType.DateTime, "file_modify_stamp")] public DateTime? file_modify_stamp { get; set; }

	[OdooProp(OdooFieldType.Integer, "file_size")] public int? file_size { get; set; }

	[OdooProp(OdooFieldType.Binary, "preview_image")] public byte[]? preview_image { get; set; }
	[OdooProp(OdooFieldType.Binary, "file_contents")] public byte[]? file_contents { get; set; }
	public string? FileContentsBase64 { get; private set; }
    public string? WinPathway { get => field ??= windows_complete_name?[OdooDefaultsConstants.ODOO_PATH_PREFIX_LENGTH..]; set; }
    
    public SwDmDocumentType FileTypeExt
	{
		get => file_ext is null or "" ? SwDmDocumentType.swDmDocumentUnknown : Help.GetSwDmDocumentTypeFromExtension(file_ext);
	}
        
    static HpVersion()
    {
        UsualExcludedFields = ["preview_image", "file_contents"];
    }
    
    public override void CompleteConstruction()
    {
        try
        {
            if (this.HashedValues.ContainsKey("dir_id"))
            {
                WinPathway = FileOperations.ConvertToWindowsPath(
                    (HashedValues["dir_id"] as ArrayList)?[1] as string, false);
            }
			if (WinPathway is null && this.HashedValues.ContainsKey("directory_complete_name"))
			{
				WinPathway = FileOperations.ConvertToWindowsPath(
					(HashedValues["directory_complete_name"] as string ?? ""), false);
			}
		}
        finally 
        {
            base.CompleteConstruction();
        }
    }

    public async Task<bool> GetPreviewImage()
	{
		if (preview_image is { Length: > 0 } && id != 0) 
		{
			// reads the datas field in ir.attachment and returns an ArrayList with one record because of one ID
			// which contains a hashtable with keys: datas and id. datas has a value of string which is the base 64 file contents
			ArrayList list = await OClient.ReadAsync(HpModel, [this.id], ["preview_image"]);
			preview_image = FileOperations.ConvertFromBase64((list[0] as Hashtable)?["preview_image"] as string);									
		}
		return preview_image is { Length: > 0 };
	}
}
public partial class HpVersion : HpBaseModelTransport<HpVersion>
{
	public bool MoveFile(string toPath)
        {
            try
            {
                if (!Directory.Exists(toPath) && !Directory.CreateDirectory(toPath).Exists) return false;
    
                string fromFilePath = Path.Combine(this.WinPathway, this.name);
                string toFilePath = Path.Combine(toPath, this.name);
    
                FileInfo file = new(fromFilePath);
                if (file.Exists) file.MoveTo(toFilePath);
                else return false;
    
                this.WinPathway = toPath;
            }
            catch
            {
                return false;
            }
            return true;
        }
    public async static Task<(int, long)?> BatchDownloadFiles(List<HpVersion> processVersions)
    {
        HackFile[] datas = await DownloadFilesData(processVersions);
                
        if (datas == null || datas.Length < 1) return null;
                
        var finish = Task.WhenAll(HackFile.CreateFiles(datas));
        await finish;
        return finish.Result[0];
    }
    public bool DownloadFile() => DownloadFile(Path.Combine(HackDefaults.Instance.PwaPathAbsolute, this.WinPathway));
    public bool DownloadFile(string toPath)
    {
        if (!Directory.Exists(toPath) && !Directory.CreateDirectory(toPath).Exists) return false;
        HackFile data = DownloadFileData();
    
        data.DirectoryName = toPath;
        if (data.FileContents != null && data.FileContents.Length > 0)
            data.CreateFile();
    
        this.WinPathway = toPath;
        return true;
    }
    public byte[]? DownloadContents()
    {
        const string fileContents = "file_contents";
    
        if (this.IsRecord || this.id != 0)
        {
            // reads the datas field in ir.attachment and returns an ArrayList with one record because of one ID
            // which contains a hashtable with keys: datas and id. datas has a value of string which is the base 64 file contents
            if (file_size != 0)
            {
                return ((OClient.Read(HpModel, [this.id], [fileContents])?[0] as Hashtable)?[fileContents]) as byte[];
            }
        }
        return null;
    }
    public static async Task<List<HpVersion>> DownloadContentsAll(List<HpVersion> versions)
    {
        string[] fileContents = [
            nameof(file_contents), 
            nameof(dir_id), 
            nameof(name), 
            nameof(file_modify_stamp), 
            nameof(file_size), 
            nameof(checkout_user), 
            nameof(windows_complete_name)];

        List<HpVersion> processVersions = [.. versions.TakeAndRemove(version 
            => version.file_contents is not { Length: 0 })];
                
        ArrayList ids = new(processVersions.Select(v => v.id).ToArray());
        //string[] fileContentsBase64 = 
        //ArrayList results = OClient.Read(GetHpModel(), ids, [fileContents], 60000);
        HpVersion[]? readyVersions = await GetRecordsByIdsAsync(ids, includedFields: fileContents);
        if (readyVersions is not null && readyVersions.Length > 0)
            versions.AddRange(readyVersions);
    
        //IEnumerable<string> fileContentsBase64 = results.Select<object, string>(obj => {
        //    Hashtable ht = ((Hashtable)obj);
        //    object val = ht[fileContents];
        //    return (val is string str) ? str : null;
        //});
        ////Utils.MapValues(typeof(HpVersion).GetProperty("fileContentsBase64"), versions, fileContentsBase64);
        //return fileContentsBase64;
        return versions;
    }
    public HackFile DownloadFileData()
    {
        if (file_contents == null) file_contents = DownloadContents();
    
        HackFile file = new(name, null);
        if (file_contents == null) return file;
    
        file.FileContents = file_contents;
    
        return file;
    }
    public static async Task<HackFile[]> DownloadFilesData(List<HpVersion> versions)
    {
        versions = await DownloadContentsAll(versions);
        //string[] fileContentsBase64 = DownloadContentsAll(versions).ToArray();
        //if (versions.Count() != fileContentsBase64.Length) return null;
        if (versions is null) return null;
                
        int vLen = versions.Count;
        var hackFiles = new HackFile[vLen];
    
        for (int i = 0; i < vLen; i++)
        {
            HackFile hack = new(versions[i].name, null);
            var checkUser = versions[i].checkout_user;
                    
            hack.Owner = (checkUser?.id is not null)
                ? OdooDefaults.Instance?.OdooId == checkUser?.id
				: null;

            if (versions[i] != null && versions[i].file_contents is { Length: > 0 })
            {
                hack.FileContents = versions[i].file_contents;
                hack.Name = versions[i].name;
                hack.DirectoryName = versions[i].WinPathway;
                hack.SetModifiedDate(versions[i]?.file_modify_stamp ?? default);
                hack.FileSize = versions[i].file_size;
                // winpathway is probably the shortened version
            }
            else
            {
                hack.FileContents = null;
            }
    
            hackFiles[i] = hack;
        }
        return hackFiles;
    }
    public static string[] GetDirectoryPath(ArrayList ids)
    {
        const string directory = "dir_id";
        const string name = "name";
    
        ArrayList list = OClient.Read(GetHpModel(), ids, [directory, name]);
    
        List<string> pathways = [];
        pathways.Capacity = ids.Count;
                
        foreach (Hashtable ht in list)
        {
            // Documents\\dev\\hackpdm\\HackPDM_CSharp\\pwa\\
            string nam = (string)ht[name];
            string dir = (string)((ArrayList)ht[directory])[1];
    
            pathways.Add(FileOperations.ConvertToWindowsPath($"{dir} / {nam}", false));
        }
        return [.. pathways];
    }
    internal static HpVersion MostRecent(HpVersion[] versions)
    {
        HpVersion version = Default();
        if (versions.Count() < 1) return version;
    
        DateTime? mostRecent = DateTime.MinValue;
        foreach ( HpVersion v in versions)
        {
            if (mostRecent < v?.file_modify_stamp)
            {
                mostRecent = v?.file_modify_stamp;
                version = v;
            }
        }
        return version;
    }
    public static async Task<HpVersionProperty[]?> GetProperties(HpVersion version)
    {
        const string versionPropField = "version_property_ids";
        if (version.IsRecord || version.id != 0)
        {
            ArrayList list = await OClient.ReadAsync(GetHpModel(), [version.id], [versionPropField]);
            ArrayList? values = (list[0] as Hashtable)?[versionPropField] as ArrayList;
            return await HpBaseModelTransport<HpVersionProperty>.GetRecordsByIdsAsync(values);
        }
        return null;
    }
    public static async Task<List<HpVersionProperty[]>> GetAllVersionPropertiesAsync(ArrayList ids)
    {
    	const string versionPropField = "version_property_ids";
    	ArrayList list = await OClient.ReadAsync(GetHpModel(), ids, [versionPropField]);
    	List<HpVersionProperty[]> versionProperties = [];
    	foreach (Hashtable ht in list)
    	{
    		ArrayList values = (ArrayList)ht[versionPropField];
    		versionProperties.Add(await HpBaseModelTransport<HpVersionProperty>.GetRecordsByIdsAsync(values));
    	}
    	return versionProperties;
    }
    public static List<HpVersionProperty[]> GetAllVersionProperties(ArrayList ids) 
    	=> GetAllVersionPropertiesAsync(ids).GetAwaiter().GetResult();
    public static bool HasChecksum(string checksum, params HpVersion[] versions)
    {
        foreach (HpVersion version in versions)
        {
            if (version.checksum == checksum) return true;
        }
        return false;
    }
    //public static int []? GetChildren( int id ) => GetRelatedIdsById( [ id ], "child_ids" );
    public static async Task<HpVersion []?> GetChildren ( int id )
    {
        HpVersionRelationship[]? versionRelationships = await GetRelatedRecordByIdsAsync<HpVersionRelationship>( [id], "child_ids", includedFields: ["child_id"] );
        if (versionRelationships is not {Length: > 0 } ) return null;
    
        ArrayList ids = versionRelationships.Select(vRel => vRel.child_id).ToArrayList();
        HpVersion[]? versions = await GetRecordsByIdsAsync(ids, includedFields: ["entry_id"]);
        return versions;
    }
    internal static HpVersion? PrepareCreation(HackFile hackFile, IHpEntryModel entry, int commit_id, HashedValueStoring hashStoreType = HashedValueStoring.None)
    {
        if (OdooDefaults.Instance.RestrictTypes is true & !OdooDefaults.Instance.ExtToType.ContainsKey(hackFile.TypeExt.ToLower()))
            return null;

		HpVersion newVersion = new()
		{
			name = $"{entry.id}.{hackFile.Name}",
			dir_id = entry.dir_id as Many2One,
			entry_id = entry.id,
			file_ext = hackFile.TypeExt[1..].ToLower(),
			WinPathway = entry.windows_complete_name,
			file_contents = hackFile.FileContents,
            commit_id = (Many2One?)commit_id,
		};
		return PrepareCreation(hackFile, newVersion);
	}
	internal static HpVersion? PrepareCreation(HackFile hackFile, IHpRecordStagedModel staged, HashedValueStoring hashStoreType = HashedValueStoring.None)
	{
		if (OdooDefaults.Instance.RestrictTypes is true & !OdooDefaults.Instance.ExtToType.ContainsKey(hackFile.TypeExt.ToLower()))
			return null;

		HpVersion newVersion = new()
		{
			name = $"{hackFile.Name}",
			dir_id = staged.payload?[nameof(dir_id)] as int?,
			entry_id = staged.id,
			file_ext = hackFile.TypeExt[1..].ToLower(),
			WinPathway = staged.payload?["windows_complete_name"] as string,
			file_contents = hackFile.FileContents,
            commit_id = staged.commit_id,
		};
		
		return PrepareCreation(hackFile, newVersion);
	}
	private static HpVersion? PrepareCreation(in HackFile hackFile, in HpVersion newVersion)
    {
		if (newVersion.file_contents is null)
		{
			try
			{
				newVersion.file_contents ??= File.ReadAllBytes(hackFile?.FullPath ?? "");
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}
		return newVersion;
	}
    public static async Task<HpVersion?> CreateVersion(HackFile hackFile, IHpEntryModel entry, int commit_id, HashedValueStoring hashStoreType = HashedValueStoring.None )
    {
        HpVersion? newVersion = PrepareCreation(hackFile, entry, commit_id, hashStoreType);
        await newVersion?.CreateAsync( false, ["file_ext"] );
    
        return newVersion.id is not null and not 0 ? newVersion : null;
	}
	public static async Task<HpRecordStaged?> StageVersion(HackFile hackFile, IHpRecordStagedModel staged, HashedValueStoring hashStoreType = HashedValueStoring.None)
	{
		HpVersion? newVersion = PrepareCreation(hackFile, staged, hashStoreType);
        HpRecordStaged? versionStage = await newVersion?.StageRecAsync();

		return versionStage?.id is not null and not 0 ? versionStage : null;
	}
	public static async Task<HpRecordStaged?> StageVersion(HackFile hackFile, IHpEntryModel entry, int commit, HashedValueStoring hashStoreType = HashedValueStoring.None)
	{
		HpVersion? newVersion = PrepareCreation(hackFile, entry, commit, hashStoreType);
		HpRecordStaged? versionStage = await newVersion?.StageRecAsync();

		return versionStage?.id is not null and not 0 ? versionStage : null;
	}
	internal static async Task<HpVersion[]?> CreateAllNew( params (HackFile hackFile, IHpEntryModel entry, int commit_id, HashedValueStoring hashStoreType)[] data)
    {
        ArrayList versions = data.Select(d => PrepareCreation(d.hackFile, d.entry, d.commit_id, d.hashStoreType)).ToArrayList();
        ArrayList ids = await MultiCreateAsync(versions, false);
    
        return await GetRecordsByIdsAsync(ids, excludedFields: UsualExcludedFields);
    }
    public static HpVersion[] GetFromPaths(params string[] fullPaths)
    {
        var paths = Help.FastSlice(fullPaths, HackDefaults.Instance.PwaPathAbsolute.Length+1, "root\\").ToArrayList();
    
        ArrayList searchParams = new() 
        {
            new ArrayList { "windows_complete_name", "in", paths }
        };
                
        return HpEntry.GetRelatedRecordsBySearch<HpVersion>(searchParams, nameof(HpEntry.latest_version_id), excludedFields: ["preview_image", "file_contents"]);
    }
    public static HpVersion[] GetFromPaths(string[] excludedFields = null, string[] includedFields = null, params string[] fullPaths)
    {
        var paths = Help.FastSlice(fullPaths, HackDefaults.Instance.PwaPathAbsolute.Length + 1, "root\\").ToArrayList();
    
        ArrayList searchParams =
		[
			new ArrayList { "windows_complete_name", "in", paths }
        ];
    
        return HpEntry.GetRelatedRecordsBySearch<HpVersion>(searchParams, nameof(HpEntry.latest_version_id), includedFields: includedFields, excludedFields: excludedFields);
    }
	public bool ExistsLocally
	{
		get
		{
			FileInfo fileInfo = new(Path.Combine(WinPathway, name));
			return fileInfo.Exists;
		}
	}

	public override string ToString()
    {
        return name;
    }
}