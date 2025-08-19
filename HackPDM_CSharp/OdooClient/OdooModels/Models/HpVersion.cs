using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.ClientUtils;
using HackPDM.Extensions.General;


//using static System.Net.Mime.MediaTypeNames;


using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public class HpVersion : HpBaseModel<HpVersion>
    {
        public string name;
        public string preview_image;
        public int? entry_id;
        public int? node_id;
        public int? dir_id;

        //public string create_stamp; // 
        public DateTime? file_modify_stamp;
        public int? attachment_id;
        public int? file_size;
        public string file_ext;
        public string checksum;
        public string file_contents;
        public string fileContentsBase64 { get; private set; }
        public string winPathway { get; internal set; }
        
        static HpVersion()
        {
            UsualExcludedFields = ["preview_image", "file_contents"];
        }
        public HpVersion() { }
        public HpVersion(
            string name = null,
            string previewImageBase64 = null,
            int? entry_id = null,
            int? node_id = null,
            int? dir_id = null,
            //string create_stamp = null,
            DateTime? file_modify_stamp = null,
            int? attachment_id = null,
            int? file_size = null,
            string file_ext = null,
            string fileContentsBase64 = null,
            string checksum = null)
        {
            this.name = name;
            this.preview_image = previewImageBase64;
            this.entry_id = entry_id;
            this.node_id = node_id;
            this.dir_id = dir_id;
            this.file_size = file_size;
            this.file_ext = file_ext;
            this.attachment_id = attachment_id;

            //if (create_stamp == null) this.create_stamp = OdooDefaults.OdooDateFormat(DateTime.Now);
            //else this.create_stamp = create_stamp;
            if (file_modify_stamp == null)
				this.file_modify_stamp = DateTime.Now;
			else
				this.file_modify_stamp = file_modify_stamp;

            this.fileContentsBase64 = fileContentsBase64;
            this.checksum = checksum;
            
            this.winPathway = null;
        }
        internal override void CompleteConstruction()
        {
            try
            {
                if (this.HashedValues.ContainsKey("dir_id"))
                {
                    winPathway = HpDirectory.ConvertToWindowsPath(
                        (string)
                        ((ArrayList)this.HashedValues["dir_id"])[1], false);
                }
            }
            finally 
            {
                base.CompleteConstruction();
            }
        }
        //public override int Create()
        //{
        //    IrAttachment file = new(this.name, fileContentsBase64:this.fileContentsBase64);
        //    attachment_id = file.Create();
        //    return base.Create();
        //}
        public bool MoveFile(string toPath)
        {
            try
            {
                if (!Directory.Exists(toPath) && !Directory.CreateDirectory(toPath).Exists) return false;

                string fromFilePath = Path.Combine(this.winPathway, this.name);
                string toFilePath = Path.Combine(toPath, this.name);

                FileInfo file = new(fromFilePath);
                if (file.Exists) file.MoveTo(toFilePath);
                else return false;

                this.winPathway = toPath;
            }
            catch
            {
                return false;
            }
            return true;
        }
        public async static Task<int> BatchDownloadFiles(List<HpVersion> processVersions)
        {
            HackFile[] datas = DownloadFilesData(processVersions);
            
            if (datas == null || datas.Length < 1) return 0;
            
            Task<int[]> finish = Task.WhenAll(HackFile.CreateFiles(datas));
            await finish;
            return finish.Result[0];
        }
        public bool DownloadFile() => DownloadFile(Path.Combine(HackDefaults.PWAPathAbsolute, this.winPathway));
        public bool DownloadFile(string toPath)
        {
            if (!Directory.Exists(toPath) && !Directory.CreateDirectory(toPath).Exists) return false;
            HackFile data = DownloadFileData();

            data.BasePath = toPath;
            if (data.FileContents != null && data.FileContents.Length > 0)
                data.CreateFile();

            this.winPathway = toPath;
            return true;
        }
        public string DownloadContents()
        {
            const string fileContents = "file_contents";

            if (this.IsRecord || this.ID != 0)
            {
                // reads the datas field in ir.attachment and returns an ArrayList with one record because of one ID
                // which contains a hashtable with keys: datas and id. datas has a value of string which is the base 64 file contents
                if (file_size != 0)
                {
                    return (string)((Hashtable)OClient.Read(HpModel, [this.ID], [fileContents])[0])[fileContents];
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
            
            ArrayList ids = new(processVersions.Select(v => v.ID).ToArray());
            //string[] fileContentsBase64 = 
            //ArrayList results = OClient.Read(GetHpModel(), ids, [fileContents], 60000);
            HpVersion[] readyVersions = HpVersion.GetRecordsByIDS(ids, includedFields: fileContents, insertFields: ["checkout_user"]);
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
            if (file_contents == null) DownloadContents();

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
                var check_user = versions[i].HashedValues["checkout_user"];
                
                hack.Owner = check_user is int id && OdooDefaults.OdooID == id;
                if (versions[i] != null && versions[i].file_contents is not null and not "")
                {
                    hack.FileContents = Convert.FromBase64String(versions[i].file_contents);
                    hack.Name = versions[i].name;
                    hack.BasePath = versions[i].winPathway;
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

                pathways.Add(HpDirectory.ConvertToWindowsPath($"{dir} / {nam}", false));
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
        internal HpVersionProperty[] GetProperties()
        {
            const string version_prop_field = "version_property_ids";
            if (this.IsRecord || this.ID != 0)
            {
                ArrayList list = OClient.Read(HpModel, [this.ID], [version_prop_field]);
                ArrayList values = (ArrayList)((Hashtable)list[0])[version_prop_field];
                return HpBaseModel<HpVersionProperty>.GetRecordsByIDS(values);
            }
            return null;
        }
        public static List<HpVersionProperty[]> GetAllVersionProperties(ArrayList ids)
        {
            const string version_prop_field = "version_property_ids";

            ArrayList list = OClient.Read(GetHpModel(), ids, [version_prop_field]);

            List<HpVersionProperty[]> versionProperties = [];

            foreach(Hashtable ht in list)
            {
                ArrayList values = (ArrayList)ht[version_prop_field];
                versionProperties.Add(HpBaseModel<HpVersionProperty>.GetRecordsByIDS(values));
            }
            return versionProperties;
        }
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
            HpVersionRelationship[] versionRelationships = GetRelatedRecordByIDS<HpVersionRelationship>( [id], "child_ids", includedFields: ["child_id"] );
            if (versionRelationships is null || versionRelationships.Length == 0) return null;

            ArrayList ids = versionRelationships.Select(vRel => vRel.child_id).ToArrayList();
            HpVersion[] versions = GetRecordsByIDS(ids, includedFields: ["entry_id"]);
            return versions;
        }
        internal static HpVersion PrepareCreation(HackFile hackFile, HpEntry entry, HashedValueStoring hashStoreType = HashedValueStoring.None)
        {
            if (OdooDefaults.RestrictTypes & !OdooDefaults.ExtToType.ContainsKey(hackFile.TypeExt.ToLower()))
                return null;

            string fileBase64 = hackFile.FileContents != null
            ? Convert.ToBase64String(hackFile.FileContents)
            : FileOperations.ConvertToBase64(hackFile.FullPath);

            HpVersion newVersion = new()
            {
                name = $"{entry.ID}.{hackFile.Name}",
                dir_id = entry.dir_id,
                entry_id = entry.ID,
                file_ext = hackFile.TypeExt[1..].ToLower(),
                winPathway = hackFile.FullPath,
            };
            if (fileBase64 is not null and not "")
            {
                newVersion.file_contents = fileBase64;
            }
            return newVersion;
        }
		internal static async Task<HpVersion> CreateNew( HackFile hackFile, HpEntry entry, HashedValueStoring hashStoreType = HashedValueStoring.None )
        {
            HpVersion newVersion = PrepareCreation(hackFile, entry, hashStoreType);
			await newVersion.CreateAsync( false, ["file_ext"] );

            return newVersion.ID == 0 ? null : newVersion;
        }
        internal static async Task<HpVersion[]> CreateAllNew( params (HackFile hackFile, HpEntry entry, HashedValueStoring hashStoreType)[] data)
        {
            ArrayList versions = data.Select(d => PrepareCreation(d.hackFile, d.entry, d.hashStoreType)).ToArrayList();

            ArrayList ids = await MultiCreateAsync<HpVersion>(versions, false);
            return GetRecordsByIDS(ids, excludedFields: UsualExcludedFields);
        }
        public static HpVersion[] GetFromPaths(params string[] fullPaths)
        {
            var paths = Utils.FastSlice(fullPaths, HackDefaults.PWAPathAbsolute.Length+1, "root\\").ToArrayList();

            ArrayList searchParams = new() 
            {
                new ArrayList { "windows_complete_name", "in", paths }
            };
            
            return HpEntry.GetRelatedRecordsBySearch<HpVersion>(searchParams, "latest_version_id", excludedFields: ["preview_image", "file_contents"]);
        }
        public static HpVersion[] GetFromPaths(string[] excludedFields = null, string[] includedFields = null, params string[] fullPaths)
        {
            var paths = Utils.FastSlice(fullPaths, HackDefaults.PWAPathAbsolute.Length + 1, "root\\").ToArrayList();

            ArrayList searchParams = new()
            {
                new ArrayList { "windows_complete_name", "in", paths }
            };

            return HpEntry.GetRelatedRecordsBySearch<HpVersion>(searchParams, "latest_version_id", includedFields: includedFields, excludedFields: excludedFields);
        }
        protected bool ExistsLocally()
        {
            FileInfo fileInfo = new(Path.Combine(this.winPathway, this.name));
            
            if (!fileInfo.Exists) return false;
            
            return true;
        }
		public override string ToString()
		{
			return name;
		}
	}
}
