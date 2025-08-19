using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Extensions.General;


//using static System.Net.Mime.MediaTypeNames;


using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public class HpDirectory : HpBaseModel<HpDirectory>
    {
        internal readonly string[] usualExcludedFields = [];
        public string name;
        public string parent_path;
        public int? parent_id;
        public int? default_cat;
        public bool? deleted;
        public bool? sandboxed;

        public HpDirectory() { }
        public HpDirectory(
            string name, 
            string parent_path = null, 
            int? parent_id = 0, 
            int? default_cat = 0, 
            bool? deleted = false, 
            bool? sandboxed = false) : this()
        {
            this.name = name;
            this.parent_path = parent_path;
            this.parent_id = parent_id;
            this.default_cat = default_cat;
            this.deleted = deleted;
            this.sandboxed = sandboxed;
        }
		public static (int, int) LastAvailableDirectory( ArrayList paths )
		{
			Hashtable last = OClient.Command<Hashtable>(GetHpModel(), "last_available_directory", [paths]);

            return ( (int)last [ "index" ], (int)last [ "dir_id" ]);
		}

		public async static Task<bool> CreateNew( HpDirectory[] directories )
        {
            for (int i = 0; i < directories.Count(); i++ )
			{
				if ( directories [ i ].ID == 0 )
				{
					await directories [ i ].CreateAsync( false );
					if ( directories [ i ].ID == 0 )
						return false;
				}
			}
            return true;
		}
		public async static Task<HpDirectory[]> CreateNew( ArrayList paths )
        {
            Hashtable last = await OClient.CommandAsync<Hashtable>(GetHpModel(), "last_available_directory", [paths]);

            // this means that all directories in paths were found 
            int nextIndex = (int)last["index"] + 1;
            int lastDirID = (int)last["dir_id"];

			if (nextIndex >= paths.Count)
                return [GetRecordByID( lastDirID )];

			HpDirectory[] directories = new HpDirectory[paths.Count - nextIndex];
            int lastParentID = lastDirID;
            for (int i = nextIndex; i < paths.Count; i++)
            {
                HpDirectory newDirectory = new()
                {
                    name = (string)paths[i],
                    parent_id = lastParentID,
                    sandboxed = false,
                    deleted = false,
                    default_cat = 1,
                };
                await newDirectory.CreateAsync(false);

                if (newDirectory.ID == 0) throw new Exception("HpDirectory not created");
                    
                directories[nextIndex] = newDirectory;
                // for next iteration
                lastParentID = newDirectory.ID;
            }
            return directories;
        }
        public int GetId()
        {
            string linuxPath = parent_path.Replace(@"\", @" / ").Replace(@"\\", @" / ");
            return OClient.Command<int>(this.HpModel, "get_dir_id_for_parentpath", new ArrayList(new string[] { linuxPath }));
        }
        public Hashtable GetSubdirectories(bool withEntries = true)
        {
            if (this.IsRecord)
            {
                return OClient.Command<Hashtable>(HpModel, "get_children_directories_by_id", new ArrayList(new ArrayList { this.ID, withEntries }));
            }
            return null;
        }
        public static Dictionary<string, object> GetSubdirectories(int ID)
        {
            if (ID != 0)
            {
                return OClient.Command<Dictionary<string, object>>(GetHpModel(), "get_children_directories_by_id", new ArrayList(new ArrayList { ID, false }));
            }
            return null;
        }
        
        public Hashtable GetSubdirectories(string pathway)
        {
            string linuxPath = pathway.Replace(@"\", @" / ").Replace(@"\\", @" / ");
            return OClient.Command<Hashtable>(HpModel, "get_children_directories", new ArrayList(new string[] { linuxPath }));
        }
        public Hashtable GetEntries()
        {
            if (this.IsRecord || this.ID != 0)
            {
                return GetEntries(this.ID);
            }
            return null;
        }
        public static Hashtable GetEntries(int directoryID, bool showInActive = false)
            => OClient.Command<Hashtable>(
                GetHpModel(), 
                "get_entries", 
                new ArrayList(new ArrayList { new ArrayList {directoryID, showInActive} })
            );
            
        
        public ArrayList GetDirectoryEntryIDs(bool withSubEntries = false, bool withDeleted = true)
            => GetDirectoryEntryIDs( this.ID, withSubEntries, withDeleted );
		public static ArrayList GetDirectoryEntryIDs( int directoryID, bool withSubEntries = false, bool withDeleted = false )
		{
			return  directoryID != 0 
				?  OClient.Command<ArrayList>( GetHpModel(), "get_all_entry_ids", [ directoryID, withDeleted, withSubEntries ], 10000 ) 
				:   null;
		}
		public static string ConvertToWindowsPath(string pathway, bool withAbsolutePath)
        {
            string[] pathwaySegmented = pathway.Split([" / "], StringSplitOptions.RemoveEmptyEntries);
            if (pathwaySegmented[0] == "root" || pathwaySegmented[0] == HackDefaults.PWAPathRelative)
            {
                pathwaySegmented = [.. pathwaySegmented.Skip(1)];
            }
            string relativePath = string.Join(@"\", pathwaySegmented);

            return withAbsolutePath ? Path.Combine(HackDefaults.PWAPathAbsolute, relativePath) : relativePath;
        }
        public static string WindowsToOdooPath(string pathway, bool fromFullPath = false)
        {
            if (fromFullPath)
            {
                pathway = pathway[(HackDefaults.PWAPathAbsolute.Length - HackDefaults.PWAPathRelative.Length)..];
            }
            string[] pathwaySegmented = pathway.Split('\\');
            if (pathwaySegmented[0] == HackDefaults.PWAPathRelative)
            {
                pathwaySegmented[0] = "root";
            }
            if (pathwaySegmented[0] != "root")
            {
                pathwaySegmented = [.. pathwaySegmented.Prepend("root")];
            }
            string relativePath = string.Join(@" / ", pathwaySegmented);
            return relativePath;
        }
		public override string ToString()
		{
			return name;
		}
	}
}
