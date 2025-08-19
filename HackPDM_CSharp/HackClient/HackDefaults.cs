using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using HackPDM.Forms.Settings;
using HackPDM.Properties;

using Microsoft.VisualStudio.TextManager.Interop;

namespace HackPDM
{
    public static class HackDefaults
    {
        public static string PWAPathAbsolute 
        { 
            get => Properties.UserSettings.Default.PWAPathAbsolute; 
            set
            {
                Properties.UserSettings.Default.PWAPathAbsolute = value;
                Properties.UserSettings.Default.Save();
            }
        }
        public static string PWAPathRelative
        {
            get
            {
                field ??= Path.GetFileName(PWAPathAbsolute);
                return field;
            }
            set
            {
                field = value;
            }
        }
        public static string MeasureFileSize 
        { 
            get => Properties.AppSettings.Default.MeasureFileSize; 
            set
            {
                Properties.AppSettings.Default.MeasureFileSize = value;
                Properties.AppSettings.Default.Save();
            }
        }
        public static double MeasureByteSize 
        { 
            get => Properties.AppSettings.Default.MeasureByteSize; 
            set
            {
                Properties.AppSettings.Default.MeasureByteSize = value;
                Properties.AppSettings.Default.Save();
            }
        }
        public static double FileSizeMult
        {
            get => Properties.AppSettings.Default.FileSizeMult;
            set 
            {
                Properties.AppSettings.Default.FileSizeMult = value;
                Properties.AppSettings.Default.Save();
            }
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

        public static SWDocMgr docMgr
        {
            get
            {
                return field ??= new(OdooDefaults.SWApi);
            }
            set;
        }
        public static SWHelper swHelper
        {
            get
            {
                return field ??= new();
            }
            set;
        }

        public static bool GetFiles(string relativePath, out IEnumerable<string> files)
        {
            CurrentPath = Path.Combine(PWAPathAbsolute, relativePath);
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
            RecurseTravel(directory, PWAPathAbsolute);
        }
        public static void CreateDirectories(DirectoryDict[] directories)
        {
            foreach (DirectoryDict hdr in directories)
            {
                RecurseTravel(hdr, PWAPathAbsolute + "\\" + hdr.name);
            }
        }
        public static string DefaultPath(string pathway, bool withAbsolute = false)
        {
            string[] paths = pathway.Split('\\');
            paths = [.. paths.Skip(1)];

            string relativePath = string.Join(@"\", paths);

            if (withAbsolute) return Path.Combine(PWAPathAbsolute, relativePath);
            
            return relativePath;
        }
        public static T[] ArrayListToModelsArray<T>(ArrayList al) where T : IConvert<T>, new()
        {
            List<T> models = [];
            foreach (Hashtable ht in al)
            {
                T model = new();
                models.Add(model.ConvertFromHT(ht));
            }
            return [.. models];
        }
        private static void RecurseTravel(DirectoryDict directory, string directoryFullPath)
        {
            string pathway = directoryFullPath + "\\" + directory.name;
            Directory.CreateDirectory(pathway);

            // recurse traverse children
            foreach (DirectoryDict hdr in directory.directories)
            {
                RecurseTravel(hdr, pathway);
            }
        }
    }

    public class HackFile : HackBaseFile
    {
        // file settings
        public string TypeExt 
        {
            get => Info?.Extension ?? field;
            set
            {
                field = Info?.Extension ?? value;
            }
        }
        public DateTime ModifiedDate 
        {
            get => field;
            set
            {
                if (OverwriteDate != default)
                {
                    field = OverwriteDate;
                    return;
                }
                if (field == default)
                {
                    field = Info?.LastWriteTime ?? value;
                }
                else
                {
                    if (Exists)
                    {
                        Info?.LastWriteTime = value;
                        field = Info?.LastWriteTime ?? value;
                    }
                    else
                    {
                        field = value;
                    }
                }
            }
        }
        internal void ApplyModifiedDateToLocal()
        {
            if (OverwriteDate == default) return;
            Info ??= new(FullPath);
            if (Exists)
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
            Info ??= new(FullPath);
            if (Owner) return;
            if (Exists)
            {
                try
                {
                    Info.Attributes |= FileAttributes.ReadOnly;
                }
                catch { }
            }
        }
        public void SetModifiedDate(DateTime date)
        {
            OverwriteDate = date;
        }
        private DateTime OverwriteDate = default;
        public string Checksum { get; set; }
        public long? FileSize 
        {
            get => Info?.Length ?? field;
            set
            {
                field = Info?.Length ?? value;
            }
        }
        
        // odoo settings
        public int? HpVersionID { get; set; }
        public bool? HasRemoteVersion { get; set; }
        public bool Owner { get; set; } = false;
        public bool Exists => Info?.Exists ?? false;

        public HackFile() {}
        public HackFile(HackFile hack)
		{
			AssignToSelf( hack );
		}
		public HackFile(
            string name,
            string fullPath=null,
            string typeExt=null, 
            DateTime modifiedDate=default, 
            string SHA1Checksum=null,
            long? fileSize=null,
            int? hpVersionID=null, 
            bool? hasRemoteVersion=null,
            string basePath=null,
            string relativePath=null)
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
            this.Checksum = SHA1Checksum;
            this.HpVersionID = hpVersionID;
            this.HasRemoteVersion = hasRemoteVersion;
            this.FileSize = fileSize;
        }
        public HackFile(FileInfo file)
        {
            Info = file;
			Name = file.Name;
			BasePath = file.DirectoryName;
			FullPath = file.FullName;
			TypeExt = file.Extension;
			ModifiedDate = file.LastWriteTime;
            FileSize = file.Length;
			Checksum = FileOperations.FileChecksum( file.FullName, SHA1.Create() );
		}
        public HackFile(string fullPath) => InitializeHackFromPath( fullPath );
		public void InitializeHackFromPath(string path) => AssignToSelf(GetFromPath(path));
        private void AssignToSelf(HackFile hack)
        {
            this.Info = hack?.Info;
			this.FullPath = hack.FullPath;
            this.Name = hack.Name;
			this.BasePath = hack.BasePath;
			this.RelativePath = hack.RelativePath;
			this.TypeExt = hack.TypeExt;
			this.ModifiedDate = hack.ModifiedDate;
			this.Checksum = hack.Checksum;
			this.HpVersionID = hack.HpVersionID;
			this.HasRemoteVersion = hack.HasRemoteVersion;
			this.FileContents = hack.FileContents;
            this.FileSize = hack.FileSize;
		}
        public static async Task<HackFile> GetFromFileInfo( FileInfo file )
		{
            HackFile hack = new()
            {
                Info = file,
			    Name = file.Name,
			    BasePath = file.DirectoryName,
			    FullPath = file.FullName,
			    TypeExt = file.Extension,
			    ModifiedDate = file.LastWriteTime,
                FileSize = file.Length,
			    Checksum = await FileOperations.FileChecksumAsync( file.FullName, SHA1.Create() ),
            };
			return hack;
		}
		public static HackFile GetFromPath(string path, string directory = null)
        {
            FileInfo file = new(path);
            if (!file.Exists) return null;

            HackFile hack = new(file);

            hack.RelativePath = Path.Combine("root", hack.BasePath[Math.Min(HackDefaults.PWAPathAbsolute.Length+1, hack.BasePath.Length)..]);
            return hack;
        }
        public static List<HackFile> FolderPathToHackWithDependencies(string pathway, SearchOption options = SearchOption.AllDirectories)
        {
            // get all files in folder path to commit.
            string[] files = [.. Directory.EnumerateFiles(pathway, "*", options)];
            return FilePathsToHackWithDependencies(files);
        }
        public static List<HackFile> FilePathsToHackWithDependencies(params string[] filePaths)
        {
            HashSet<string> newFiles = [.. filePaths];
            List<HackFile> hackFiles = [];
            // find all dependencies
            foreach (string file in filePaths)
            {
                var fInfo = new FileInfo(file);
                if (OdooDefaults.dependentExt.Contains(fInfo.Extension))
                {
                    var dependencies = HackDefaults.docMgr.GetDependencies(file);
                    if (dependencies is not null && dependencies.Count > 0)
                    {
                        foreach (string[] deps in dependencies)
                        {
                            string path = deps[1];
                            var splitPath = path.Split([$"\\{HackDefaults.PWAPathRelative}\\"], StringSplitOptions.RemoveEmptyEntries);
                            if (splitPath.Length == 2)
                            {
                                newFiles.Add(Path.Combine([HackDefaults.PWAPathAbsolute, splitPath[1]]));
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
            if (version.winPathway == null) return null;
            HackFile hack = GetFromPath(Path.Combine(HackDefaults.PWAPathAbsolute, version.winPathway, version.name), Path.Combine(HackDefaults.PWAPathRelative, version.winPathway));
            if (hack != null && hack.Checksum == version.checksum)
            {
                hack.HasRemoteVersion = true;
                hack.HpVersionID = version.ID;
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
            if (hackFile.HasRemoteVersion != null && (bool)hackFile.HasRemoteVersion && hackFile.HpVersionID != null)
            {
                version = HpVersion.GetRecordByID((int)hackFile.HpVersionID, HpVersion.UsualExcludedFields);
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
				if (this.HpVersionID is not null and not 0 )
                {
                    if (this.HpVersionID == hack.HpVersionID ) return true;
                    
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
				if ( this.HpVersionID is not null and not 0 )
				{
					if ( this.HpVersionID == version.ID )
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
			HashCode hash = new HashCode();
			hash.Add( this.Name );
			hash.Add( this.FullPath );
			hash.Add( this.BasePath );
			hash.Add( this.RelativePath );
			hash.Add( this.TypeExt );
			hash.Add( this.ModifiedDate );
			hash.Add( this.Checksum );
			hash.Add( this.HpVersionID );
			hash.Add( this.HasRemoteVersion );
			hash.Add( this.FileContents );
			return hash.ToHashCode();
		}
    }
    public struct DirectoryDict : IConvert<DirectoryDict>
    {
        public DirectoryDict[] directories;
        public string name;
        public HpEntryReturn[] entries;
        public int id;

        public DirectoryDict ConvertFromHT(Hashtable ht) => ht;
        public static implicit operator DirectoryDict(Hashtable ht)
        {
            DirectoryDict[] directories = 
                HackDefaults.ArrayListToModelsArray<DirectoryDict>((ArrayList)ht["directories"]);
            HpEntryReturn[] entries = 
                HackDefaults.ArrayListToModelsArray<HpEntryReturn>((ArrayList)ht["entries"]);

            return new DirectoryDict
            {
                directories = [.. directories],
                entries = [.. entries],
                id = (int)ht["id"],
                name = (string)ht["name"],
            };
        }
    }
    public struct HpEntryReturn : IConvert<HpEntryReturn>
    {
        public string name;
        public int id;

        public HpEntryReturn ConvertFromHT(Hashtable ht) => ht;

        public static implicit operator HpEntryReturn(Hashtable ht)
        {
            return new HpEntryReturn
            {
                id = (int)ht["id"],
                name = (string)ht["name"],
            };
        }
    }
    
}
