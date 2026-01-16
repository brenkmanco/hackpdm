using System;
using System.Collections;
using System.Collections.Generic;
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
    
	[OdooProp(OdooFieldType.Many2one, "entry_id")] public Many2One? entry_id { get; set; }
	IMany2One? IHpVersionModel.entry_id { get =>(IMany2One?)entry_id; set => entry_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2one, "node_id")] public Many2One? node_id { get; set; }
	IMany2One? IHpVersionModel.node_id { get =>(IMany2One?)node_id; set => node_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2one, "dir_id")] public Many2One? dir_id { get; set; }
	IMany2One? IHpVersionModel.dir_id { get =>(IMany2One?)dir_id; set => dir_id = (Many2One?)value; }
	[OdooProp(OdooFieldType.Many2one, "attachment_id")] public Many2One? attachment_id { get; set; }
	IMany2One? IHpVersionModel.attachment_id { get =>(IMany2One?)attachment_id; set => attachment_id = (Many2One?)value; }

	[OdooProp(OdooFieldType.DateTime, "file_modify_stamp")] public DateTime? file_modify_stamp { get; set; }

	[OdooProp(OdooFieldType.Integer, "file_size")] public int? file_size { get; set; }

	[OdooProp(OdooFieldType.Binary, "preview_image")] public string? preview_image { get; set; }
	[OdooProp(OdooFieldType.Binary, "file_contents")] public string? file_contents { get; set; }
	public string? FileContentsBase64 { get; private set; }
    public string? WinPathway { get; set; }
    
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
		if (preview_image is null or "" && id != 0) 
		{
			// reads the datas field in ir.attachment and returns an ArrayList with one record because of one ID
			// which contains a hashtable with keys: datas and id. datas has a value of string which is the base 64 file contents
			ArrayList list = await OClient.ReadAsync(HpModel, [this.id], ["preview_image"]);
			preview_image = (list[0] as Hashtable)?["preview_image"] as string;									
		}
		return preview_image is not null and not "";
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
        HackFile[] datas = DownloadFilesData(processVersions);
                
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
    public string DownloadContents()
    {
        const string fileContents = "file_contents";
    
        if (this.IsRecord || this.id != 0)
        {
            // reads the datas field in ir.attachment and returns an ArrayList with one record because of one ID
            // which contains a hashtable with keys: datas and id. datas has a value of string which is the base 64 file contents
            if (file_size != 0)
            {
                return (string)((Hashtable)OClient.Read(HpModel, [this.id], [fileContents])[0])[fileContents];
            }
        }
        return null;
    }
    public static List<HpVersion> DownloadContentsAll(List<HpVersion> versions)
    {
        string[] fileContents = ["file_contents", "dir_id", "name", "file_modify_stamp", "file_size"];
        List<HpVersion> processVersions = [.. versions.TakeAndRemove(version =>
        {
            return version.file_contents is null or ""; 
        })];
                
        ArrayList ids = new(processVersions.Select(v => v.id).ToArray());
        //string[] fileContentsBase64 = 
        //ArrayList results = OClient.Read(GetHpModel(), ids, [fileContents], 60000);
        HpVersion[] readyVersions = HpVersion.GetRecordsByIds(ids, includedFields: fileContents, insertFields: ["checkout_user"]);
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
    
        byte[] fileContents = Convert.FromBase64String(file_contents);
        file.FileContents = fileContents;
    
        return file;
    }
    public static HackFile[] DownloadFilesData(List<HpVersion> versions)
    {
        versions = DownloadContentsAll(versions);
        //string[] fileContentsBase64 = DownloadContentsAll(versions).ToArray();
        //if (versions.Count() != fileContentsBase64.Length) return null;
        if (versions is null) return null;
                
        int vLen = versions.Count();
        var hackFiles = new HackFile[vLen];
    
        for (int i = 0; i < vLen; i++)
        {
            HackFile hack = new(versions[i].name, null);
            var checkUser = versions[i].HashedValues["checkout_user"];
                    
            hack.Owner = checkUser is int id && OdooDefaults.Instance.OdooId == id;
            if (versions[i] != null && versions[i].file_contents is not null and not "")
            {
                hack.FileContents = Convert.FromBase64String(versions[i].file_contents);
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
    public static HpVersionProperty[]? GetProperties(HpVersion version)
    {
        const string versionPropField = "version_property_ids";
        if (version.IsRecord || version.id != 0)
        {
            ArrayList list = OClient.Read(GetHpModel(), [version.id], [versionPropField]);
            ArrayList? values = (list[0] as Hashtable)?[versionPropField] as ArrayList;
            return HpBaseModelTransport<HpVersionProperty>.GetRecordsByIds(values);
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
    public static HpVersion [] GetChildren ( int id )
    {
        HpVersionRelationship[] versionRelationships = GetRelatedRecordByIds<HpVersionRelationship>( [id], "child_ids", includedFields: ["child_id"] );
        if (versionRelationships is null || versionRelationships.Length == 0) return null;
    
        ArrayList ids = versionRelationships.Select(vRel => vRel.child_id).ToArrayList();
        HpVersion[] versions = GetRecordsByIds(ids, includedFields: ["entry_id"]);
        return versions;
    }
    internal static HpVersion? PrepareCreation(HackFile hackFile, IHpEntryModel entry, HashedValueStoring hashStoreType = HashedValueStoring.None)
    {
        if (OdooDefaults.Instance.RestrictTypes is true & !OdooDefaults.Instance.ExtToType.ContainsKey(hackFile.TypeExt.ToLower()))
            return null;
    
        string fileBase64 = hackFile.FileContents != null
            ? Convert.ToBase64String(hackFile.FileContents)
            : FileOperations.ConvertToBase64(hackFile.FullPath);
    
        HpVersion newVersion = new()
        {
            name = $"{entry.id}.{hackFile.Name}",
            dir_id = entry.dir_id as Many2One,
            entry_id = entry.id,
            file_ext = hackFile.TypeExt[1..].ToLower(),
            WinPathway = hackFile.FullPath,
        };
        if (fileBase64 is not null and not "")
        {
            newVersion.file_contents = fileBase64;
        }
        return newVersion;
    }
    public static async Task<HpVersion> CreateNew(HackFile hackFile, IHpEntryModel entry, HashedValueStoring hashStoreType = HashedValueStoring.None )
    {
        HpVersion newVersion = PrepareCreation(hackFile, entry, hashStoreType);
        await newVersion.CreateAsync( false, ["file_ext"] );
    
        return newVersion.id == 0 ? null : newVersion;
    }
    internal static async Task<HpVersion[]> CreateAllNew( params (HackFile hackFile, IHpEntryModel entry, HashedValueStoring hashStoreType)[] data)
    {
        ArrayList versions = data.Select(d => PrepareCreation(d.hackFile, d.entry, d.hashStoreType)).ToArrayList();
    
        ArrayList ids = await MultiCreateAsync<HpVersion>(versions, false);
        return GetRecordsByIds(ids, excludedFields: UsualExcludedFields);
    }
    public static HpVersion[] GetFromPaths(params string[] fullPaths)
    {
        var paths = Help.FastSlice(fullPaths, HackDefaults.Instance.PwaPathAbsolute.Length+1, "root\\").ToArrayList();
    
        ArrayList searchParams = new() 
        {
            new ArrayList { "windows_complete_name", "in", paths }
        };
                
        return HpEntry.GetRelatedRecordsBySearch<HpVersion>(searchParams, "latest_version_id", excludedFields: ["preview_image", "file_contents"]);
    }
    public static HpVersion[] GetFromPaths(string[] excludedFields = null, string[] includedFields = null, params string[] fullPaths)
    {
        var paths = Help.FastSlice(fullPaths, HackDefaults.Instance.PwaPathAbsolute.Length + 1, "root\\").ToArrayList();
    
        ArrayList searchParams = new()
        {
            new ArrayList { "windows_complete_name", "in", paths }
        };
    
        return HpEntry.GetRelatedRecordsBySearch<HpVersion>(searchParams, "latest_version_id", includedFields: includedFields, excludedFields: excludedFields);
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