using HackPDM.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
            get => Properties.UserSettings.Default.PWAPathRelative;
            set
            {
                Properties.UserSettings.Default.PWAPathRelative = value;
                Properties.UserSettings.Default.Save();
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
            get;
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
            paths = paths.Skip(1).ToArray();

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
            return models.ToArray();
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
        public string TypeExt { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string SHA1Checksum { get; set; }
        public long? FileSize { get; set; }
        
        // odoo settings
        public int? HpVersionID { get; set; }
        public bool? HasRemoteVersion { get; set; }

        internal byte[] fileContents 
        { 
            get;
            set; 
        } = null;

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
            // base class
            this.Name = name;
            this.FullPath = fullPath;
            this.BasePath = basePath;
            this.RelativePath = relativePath;

            // this class
            this.TypeExt = typeExt;
            this.ModifiedDate = modifiedDate;
            this.SHA1Checksum = SHA1Checksum;
            this.HpVersionID = hpVersionID;
            this.HasRemoteVersion = hasRemoteVersion;
            this.FileSize = fileSize;
        }
        public HackFile(FileInfo file)
        {
			Name = file.Name;
			BasePath = file.DirectoryName;
			FullPath = file.FullName;
			TypeExt = file.Extension;
			ModifiedDate = file.LastWriteTime;
            FileSize = file.Length;
			SHA1Checksum = FileOperations.FileChecksum( file.FullName, SHA1.Create() );
		}
        public HackFile(string fullPath) => InitializeHackFromPath( fullPath );
		public void InitializeHackFromPath(string path) => AssignToSelf(GetFromPath(path));
        private void AssignToSelf(HackFile hack)
        {
			this.Name = hack.Name;
			this.FullPath = hack.FullPath;
			this.BasePath = hack.BasePath;
			this.RelativePath = hack.RelativePath;
			this.TypeExt = hack.TypeExt;
			this.ModifiedDate = hack.ModifiedDate;
			this.SHA1Checksum = hack.SHA1Checksum;
			this.HpVersionID = hack.HpVersionID;
			this.HasRemoteVersion = hack.HasRemoteVersion;
			this.fileContents = hack.fileContents;
            this.FileSize = hack.FileSize;
		}
        public static async Task<HackFile> GetFromFileInfo( FileInfo file )
		{
            HackFile hack = new()
            {
			    Name = file.Name,
			    BasePath = file.DirectoryName,
			    FullPath = file.FullName,
			    TypeExt = file.Extension,
			    ModifiedDate = file.LastWriteTime,
                FileSize = file.Length,
			    SHA1Checksum = await FileOperations.FileChecksumAsync( file.FullName, SHA1.Create() ),
            };
			return hack;
		}
		public static HackFile GetFromPath(string path)
        {
            FileInfo file = new(path);
            if (!file.Exists) return null;

            return new HackFile(file);
        }
        
        public static HackFile GetFromPath(string path, string directory)
        {
            HackFile hack = GetFromPath(path);
            if (hack == null) return null;

            hack.RelativePath = directory;
            return hack;
        }
        public static HackFile GetFromVersion(HpVersion version)
        {
            if (version.winPathway == null) return null;
            HackFile hack = GetFromPath(Path.Combine(HackDefaults.PWAPathAbsolute, version.winPathway, version.name), Path.Combine(HackDefaults.PWAPathRelative, version.winPathway));
            if (hack != null && hack.SHA1Checksum == version.checksum)
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
        public static bool HasLocalVersion(in HackFile hackFile)
        {
            if (hackFile == null) return false;
            if (hackFile.HasRemoteVersion != null && (bool)hackFile.HasRemoteVersion && hackFile.HpVersionID != null)
                return true;
            
            return false;
        }
        public static bool IsLocalVersion(in HpVersion version, in HackFile hackFile)
        {
            //if (HasLocalVersion(hackFile) && hackFile?.HpVersionID == version.ID) return true;
            if (hackFile.SHA1Checksum == version.checksum) return true;
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
                    //new ArrayList() { "checksum", "=", hackFile.SHA1Checksum },
                    //new ArrayList() { "directory_complete_name", "=", filePath },
                }
            ];
            version = HpVersion.GetRecordsBySearch(arrList)?[0];

            return version != null;
        }
        public static Dictionary<HackFile, HpVersion> GetVersionFromLocals(HackFile[] hackFiles)
        {
            Dictionary<HackFile, HpVersion> hackMap = [];
            foreach (HackFile hf in hackFiles)
            {
                if (GetVersionFromLocal(hf, out HpVersion version))
                {
                    hackMap.Add(hf, version);
                }
            }
            return hackMap;
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
                if (this.SHA1Checksum is not null and not "" )
                {
					if ( this.SHA1Checksum == hack.SHA1Checksum ) return true;
					
				}
                if (hack.SHA1Checksum is not null and not "")
                {

				    if ( filePath is not "" )
				    {
					    string checksum = FileOperations.FileChecksum( this.FullPath, SHA1.Create() );
					    if ( checksum == hack.SHA1Checksum ) return true;
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
				if ( this.SHA1Checksum is not null and not "" )
				{
					if ( this.SHA1Checksum == version.checksum )
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
			hash.Add( this.SHA1Checksum );
			hash.Add( this.HpVersionID );
			hash.Add( this.HasRemoteVersion );
			hash.Add( this.fileContents );
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
                directories = directories.ToArray(),
                entries = entries.ToArray(),
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
