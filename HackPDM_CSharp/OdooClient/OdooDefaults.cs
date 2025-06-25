using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using CredentialManagement;

using HackPDM.ClientUtils;

//using static System.Net.Mime.MediaTypeNames;


using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public static class OdooDefaults
    {
        #region Declarations
        // made models
        public const string HP_NODE = "hp.node";
        public const string HP_ENTRY = "hp.entry";
        public const string HP_ENTRY_NAME_FILTER = "hp.entry.name.filter";
        public const string HP_DIRECTORY = "hp.directory";
        public const string HP_CATEGORY = "hp.category";
        public const string HP_CATEGORY_PROPERTY = "hp.category.property";
        public const string HP_VERSION = "hp.version";
        public const string HP_VERSION_PROPERTY = "hp.version.property";
        public const string HP_VERSION_RELATIONSHIP = "hp.version.relationship";
        public const string HP_RELEASE = "hp.release";
        public const string HP_RELEASE_VERSION_REL = "hp.release.version.rel";
        public const string HP_SETTINGS = "hp.settings";
        public const string HP_PROPERTY = "hp.property";
        public const string HP_TYPE = "hp.type";
        // adopted models
        public const string RES_USERS = "res.users";
        public const string IR_ATTACHMENT = "ir.attachment";
        public const string IR_MODEL = "ir.model";

        public const string OdooVersionKeyName = "client_version";
        public const string SWKeyName = "swdocmgr_key";
        public static readonly string[] dependentExt = [".SLDPRT", ".SLDASM", ".SLDDRW"];
        // lock asynchonous operations
        private static readonly object m_lockObject = new();
        public static string OdooDb 
        {
            get => Properties.UserSettings.Default.OdooDb;

            set
            {
                Properties.UserSettings.Default.OdooDb = value;
                Properties.UserSettings.Default.Save();
			}
        }
        public static string OdooAddress
        {
            get => Properties.UserSettings.Default.OdooAddress;
			set
			{
				Properties.UserSettings.Default.OdooAddress = value;
				Properties.UserSettings.Default.Save();
			}
		}
		public static string OdooPort
		{
			get => Properties.UserSettings.Default.OdooPort;
			set
			{
				Properties.UserSettings.Default.OdooPort = value;
				Properties.UserSettings.Default.Save();
			}
		}
		public static string OdooUrl 
        {
            get 
            {
                if (field is null or "")
                {
                    string port = Properties.UserSettings.Default.OdooPort;
                    if (port is null or "") port = "";
                    else port = $":{port}";

                    field = $"http://{OdooAddress}{port}";
                }
                return field;
            }

            set
            {
                field = value;
			}
        }
        public static string OdooSwKey
        {
            get => Properties.AppSettings.Default.SwLicenseKey;

            set
            {
                Properties.AppSettings.Default.SwLicenseKey = value;
                Properties.AppSettings.Default.Save();
			}
        }
        public static decimal OdooAreaFactor
        {
            get => Properties.AppSettings.Default.AreaFactor;

            set
            {
                Properties.AppSettings.Default.AreaFactor = value;
                Properties.AppSettings.Default.Save();
			}
        }
        public static string OdooCredentialTarget 
        { 
            get => Properties.AppSettings.Default.OdooCredentialTarget;

            set
            {
                Properties.AppSettings.Default.OdooCredentialTarget = value;
                Properties.AppSettings.Default.Save();
			}
        }
        public static string OdooUser
        {
            get 
            {
                if (field == null)
                {
                    var cm = new Credential { Target = OdooCredentialTarget };
                    if (cm.Load())
                        field = cm.Username;
                }
                return field;
            }

            set
            {
                Credential cred = new()
                {
                    Target = Properties.AppSettings.Default.OdooCredentialTarget,
                    Username = value,
                    Password = OdooPass,
                    PersistanceType = PersistanceType.LocalComputer
                };

                Credential cm = new() { Target = Properties.AppSettings.Default.OdooCredentialTarget };

                if (cm.Load())
                    cm.Delete();

                cred.Save();
            }
        }
        public static string OdooPass
        {
            get
            {
                if (field == null)
                {
                    var cm = new Credential { Target = OdooCredentialTarget };
                    if (cm.Load()) 
                        field = cm.Password;
                }
                return field;
            }

            set
            {
                Credential cred = new()
                {
                    Target = Properties.AppSettings.Default.OdooCredentialTarget,
                    Username = OdooUser,
                    Password = value,
                    PersistanceType = PersistanceType.LocalComputer
                };

                Credential cm = new() { Target = Properties.AppSettings.Default.OdooCredentialTarget };

                if (cm.Load())
                    cm.Delete();

                cred.Save();
            }
        }
        public static int OdooID
        {
            get
            {
                if (field == 0)
                {
                    field = OClient.Login(9000);
                }
                return field;
            }

            set => field = value;
        }
        // low enough number of records to get before
        public static HpSetting [] HpSettings
        {
            get
            {
                if ( field == null )
                    field = HpSetting.GetAllRecords();
                return field;
            }
            set => field = value;
        }
        public static string SWApi = HpSettings.First(sett => sett.name == SWKeyName).char_value;

        public static HpEntryNameFilter[] HpEntryNameFilters
        {
            get
            {
                if (field == null) 
                    field = HpEntryNameFilter.GetAllRecords();
                return field;
            } 
            set => field = value;
        }
        public static HpCategory[] HpCategories
        {
            get
            {
                if (field == null) 
                    field = HpCategory.GetAllRecords();
                return field;
            }
            set => field = value;
        }
        public static HpType[] HpTypes
		{
			get
			{
				if ( field == null )
					field = HpType.GetAllRecords();
				return field;
			}
			set => field = value;
		}
        public static HpProperty[] HpProperties
		{
			get
			{
				if ( field == null )
					field = HpProperty.GetAllRecords();
				return field;
			}
			set => field = value;
		}
		public static HpNode[] HpNodes
		{
			get
			{
				if ( field == null )
					field = HpNode.GetAllRecords();
				return field;
			}
			set => field = value;
		}
        public static HpUser[] HpUsers
        {
            get
			{
				if ( field == null )
					field = HpUser.GetAllRecords();
				return field;
			}
			set => field = value;
        }

        // dictionary mapping some field type to the HpModel
        // like extension to Type or Category
        public static Dictionary<string, HpType> ExtToType 
        { 
            get
            {
                if (field == null)
                {
                    field = ExtensionMapType( HpTypes );
                }
                return field;
            }
            set
            {
                field = value;
            }
        }
        public static Dictionary<string, HpCategory> ExtToCat
        {
			get
			{
				if ( field == null )
				{
					field = ExtensionMapCategory( HpCategories, [ .. ExtToType.Values ] );
				}
				return field;
			}
			set
			{
				field = value;
			}
		}
		public static Dictionary<int, HpProperty> IDToProp
        {
            get
            {
                if ( field == null )
                {
                    field = IDMapProperty(HpProperties);
                }
                return field;
            }
            set => field = value;
        }
        public static Dictionary<int, HpUser> IDToUser
		{
			get
			{
				if ( field == null )
				{
					field = IDMapUser( HpUsers );
				}
				return field;
			}
			set => field = value;
		}
        public static Dictionary<string, HpEntryNameFilter> ExtToFilter
		{
			get
			{
				if ( field == null )
				{
					field = ExtensionMapFilter( HpEntryNameFilters );
				}
				return field;
			}
			set => field = value;
		}

		private static Dictionary<string, HpEntryNameFilter> ExtensionMapFilter( HpEntryNameFilter [] hpEntryNameFilters )
        {
            Dictionary<string, HpEntryNameFilter> dict = [];

            foreach ( HpEntryNameFilter filter in hpEntryNameFilters )
			{
				dict.Add( $".{filter.name_proto}", filter );
			}
            return dict;
		}

		private static Dictionary<int, HpUser> IDMapUser( in HpUser[] hpUsers )
        {
            Dictionary<int, HpUser> dict = [];

			foreach ( HpUser user in hpUsers )
			{
				dict.Add( user.ID, user );
			}
			return dict;
        }
		#endregion

		public static string OdooDateFormat(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }
		public static Dictionary<string, HpType> ExtensionMapType( in HpType [] types )
		{
			Dictionary<string, HpType> dict = [];

			foreach ( HpType type in types )
			{
				dict.Add( $".{type.file_ext.ToLower()}", type );
			}
			return dict;
		}
		public static Dictionary<string, HpCategory> ExtensionMapCategory( in HpCategory [] categories, in HpType [] types )
		{
			Dictionary<string, HpCategory> dict = [];
			foreach ( HpType type in types )
			{
				foreach ( HpCategory category in categories )
				{
					if ( category.ID == type.cat_id )
					{
						dict.Add( $".{type.file_ext.ToLower()}", category );
						break;
					}
				}
			}
			return dict;
		}
        public static Dictionary<int, HpProperty> IDMapProperty(in HpProperty[] props )
        {
            Dictionary<int, HpProperty> dict = [];

			foreach ( HpProperty prop in props )
			{
				dict.Add( prop.ID, prop );
			}
			return dict;
        }
		public async static Task<HpVersion> ConvertHackFile(HackFile hackFile)
        {
            Hashtable ht = [];
            
            ArrayList paths = hackFile.RelativePath.Split<ArrayList>("\\", StringSplitOptions.RemoveEmptyEntries);

            try
            {
                // create directories that don't exist in odoo
                HpDirectory[] directories = await HpDirectory.CreateNew(paths);
                HpDirectory lastDirectory = directories.Last() ?? throw new Exception($"{HpDirectory.GetHpModel()} didn't create any records");
                // create an HpEntry that doesn't exist in odoo
                HpEntry entry = await HpEntry.CreateNew(hackFile, lastDirectory.ID) ?? throw new Exception($"{HpEntry.GetHpModel()} was unable to create record");
                // create an HpVersion that doesn't exist in odoo
                HpVersion version = await CreateNewVersion(hackFile, entry) ?? throw new Exception($"{HpVersion.GetHpModel()} was unable to create record");
                return version;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
            }
            return null;
        }

		public async static Task<HpVersion> CreateNewVersion( HackFile hack, HpEntry entry )
        {
            try { 
                // create an HpVersion that doesn't exist in odoo
                HpVersion version = await HpVersion.CreateNew(hack, entry) ?? throw new Exception( $"{HpVersion.GetHpModel()} was unable to create new version for {entry.name}" );
                entry.latest_version_id = version.ID;
                return version;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
            }
            return null;
        }
        public static string ConvertToOdooFormat(DateTime dt)
        {
            return dt.ToString( "yyyy-MM-dd HH:mm:ss" );
		}
	}

    //
    // All the fields in the classes below correspond to a field name in the odoo module
    // so reflections can map it's values from the hashtable to the class fields like newtonsoft json
    // converter converts to classes with properties that align with values from the json fields.
    // changing field names will break the program unless they are mapped to the names of fields in odoo models.
    //

    public class HpRecord : HpBaseModel<HpRecord>
    {
        public bool IsCreated { get; set; }
        public string Name { get; set; }
        public HpRecord()
        {

        }

        public static implicit operator HpRecord(bool v)
        {
            HpRecord record = new() { IsCreated = v };
            return record;
        }
    }
    public class HpNode : HpBaseModel<HpNode>
    {
        public string name;
        public string parent_path;
        public int parent_id;
        public int default_cat;
        public bool active;
        public bool sandboxed;
        public HpNode() { }
        public HpNode(
            string name,
            string parent_path = null,
            int parent_id = 0,
            int default_cat = 0,
            bool active = true,
            bool sandboxed = false)
        {
            this.name = name;
            this.parent_path = parent_path;
            this.parent_id = parent_id;
            this.default_cat = default_cat;
            this.active = active;
            this.sandboxed = sandboxed;
        }
		public override string ToString() 
        {
            return name;
        }
	}
    public class HpEntry : HpBaseModel<HpEntry>
    {
        public string name;
        public string checkout_date;
        public bool deleted;
        public int latest_version_id;
        public int dir_id;
        public int type_id;
        public int cat_id;
        public int checkout_user;
        public int checkout_node;

        public HpEntry() {  }
        public HpEntry(
            string name,
            string checkout_date = null,
            bool active = true,
            int latest_version_id = 0,
            int dir_id = 0,
            int type_id = 0,
            int cat_id = 0,
            int checkout_user = 0,
            int checkout_node = 0)
        {
            this.name = name;
            this.deleted = !active;
            this.latest_version_id = latest_version_id;
            this.dir_id = dir_id;
            this.type_id = type_id;
            this.cat_id = cat_id;
            this.checkout_node = checkout_node;

            if (checkout_user == 0) this.checkout_user = OdooDefaults.OdooID;
            else this.checkout_user = checkout_user;
            if (checkout_date == null) this.checkout_date = OdooDefaults.OdooDateFormat(DateTime.Now);
            else this.checkout_date = checkout_date;
        }
        public ArrayList GetLatestIDs()
        {
            return GetLatestIDs([this.ID]);
        }
        public static ArrayList GetLatestIDs(ArrayList ids)
        {
            const string latest = "latest_version_id";
           
            ArrayList list = OClient.Read(GetHpModel(), ids, [latest], 10000);

            return list;
        }
        public bool CanCheckOut()
        {
            if (checkout_user != 0) return false;
            if (deleted) return false;

            return true;
        }
        public bool CanUnCheckOut()
        {
            if (checkout_user == OdooDefaults.OdooID) return true;
            return false;
        }
        public async Task CheckOut()
        {
            checkout_user = OdooDefaults.OdooID;
			checkout_date = OdooDefaults.OdooDateFormat( DateTime.Now );
            checkout_node = 0;

            await WriteChangedValuesAsync("checkout_user", "checkout_date", "checkout_node");
		}
        public async Task UnCheckOut()
        {
			checkout_user = 0;
			checkout_date = "";
			checkout_node = 0;

			await WriteChangedValuesAsync( "checkout_user", "checkout_date", "checkout_node" );
		}
        internal static async Task<HpEntry> CreateNew( HackFile hackFile, int dir_id )
        {
			if ( !OdooDefaults.ExtToType.TryGetValue( hackFile.TypeExt, out HpType type ) )
				return null;

			HpEntry newEntry = new()
			{
				name = hackFile.Name,
				deleted = false,
				dir_id = dir_id,
				cat_id = type.cat_id,
				type_id = type.ID,
			};
            await newEntry.CreateAsync( false );
            
            if ( newEntry.ID == 0 ) return null;
            return newEntry;
		}

		internal async Task LogicalDelete() 
        {
            deleted = true;
            await WriteChangedValuesAsync( "deleted" );
		}
		internal async Task LogicalUnDelete() 
        {
            deleted = false;
            await WriteChangedValuesAsync( "deleted" );
        }
		public override string ToString()
		{
			return name;
		}

	}
    public class HpEntryNameFilter : HpBaseModel<HpEntryNameFilter>
    {
        public string name_proto;
        public string name_regex;
        public string description;

        public HpEntryNameFilter() { }
        public HpEntryNameFilter(
            string name_proto = null,
            string name_regex = null,
            string description = null)
        {
            this.name_proto = name_proto;
            this.name_regex = name_regex;
            this.description = description;
        }
    }
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
		public static ArrayList GetDirectoryEntryIDs( int directoryID, bool withSubEntries = false, bool withDeleted = true )
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
                pathwaySegmented = pathwaySegmented.Skip(1).ToArray();
            }
            string relativePath = string.Join(@"\", pathwaySegmented);

            if (withAbsolutePath) 
                return Path.Combine(HackDefaults.PWAPathAbsolute, relativePath);
            return relativePath;
        }
        public static string WindowsToOdooPath(string pathway, bool fromFullPath = false)
        {
            if (fromFullPath)
            {
                pathway = pathway.Substring(HackDefaults.PWAPathAbsolute.Length - HackDefaults.PWAPathRelative.Length);
            }
            string[] pathwaySegmented = pathway.Split('\\');
            if (pathwaySegmented[0] == HackDefaults.PWAPathRelative)
            {
                pathwaySegmented[0] = "root";
            }
            if (pathwaySegmented[0] != "root")
            {
                pathwaySegmented = pathwaySegmented.Prepend("root").ToArray();
            }
            string relativePath = string.Join(@" / ", pathwaySegmented);
            return relativePath;
        }
		public override string ToString()
		{
			return name;
		}
	}
    public class HpCategory : HpBaseModel<HpCategory>
    {
        internal readonly string[] usualExcludedFields = [];
        public string name;
        public string cat_description;
        public bool track_version;
        public bool track_depends;

        public HpCategory() { }
        public HpCategory(
            string name,
            string cat_description = "CAD files are versioned and have dependencies",
            bool track_version = true,
            bool track_depends = true)
        {
            this.name = name;
            this.cat_description = cat_description;
            this.track_version = track_version;
            this.track_depends = track_depends;
        }
		public override string ToString()
		{
			return name;
		}
	}
    public class HpCategoryProperty : HpBaseModel<HpCategoryProperty>
    {
        public int cat_id;
        public int prop_id;

        public HpCategoryProperty() { }
        public HpCategoryProperty(
            int cat_id = 0,
            int prop_id = 0)
        {
            this.cat_id = cat_id;
            this.prop_id = prop_id;
        }
    }
	public class HpSetting : HpBaseModel<HpSetting>
	{
        public string name;
        public string description;
        public string type;
        public bool bool_value;
        public int int_value;
        public string char_value;
        public float float_value;
        public DateTime date_value;

		public HpSetting()
		{
		}
        public HpSetting(
            string name,
            string description,
            string type,
            bool bool_value=default,
            int int_value=default,
            string char_value=null,
            float float_value=default,
            DateTime date_value=default)
		{
			this.name = name;
            this.description = description;
            this.type = type;
            this.bool_value = bool_value;
            this.int_value = int_value;
            this.char_value = char_value;
            this.float_value = float_value;
            this.date_value = date_value;
		}
	}
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
            FileData[] datas = DownloadFilesData(processVersions);
            
            if (datas == null || datas.Length < 1) return 0;
            
            //// filter FileData[]
            //FileData[] revisedData = datas.TakeWhile((data) =>
            //{
            //    if (data.FileContents != null && data.FileContents.Length > 0) 
            //        return true;
            //    return false;
            //}).ToArray();
            Task<int[]> finish = Task.WhenAll(FileData.CreateFiles(datas));
            await finish;
            return finish.Result[0];
        }
        public bool DownloadFile() => DownloadFile(Path.Combine(HackDefaults.PWAPathAbsolute, this.winPathway));
        public bool DownloadFile(string toPath)
        {
            if (!Directory.Exists(toPath) && !Directory.CreateDirectory(toPath).Exists) return false;
            FileData data = DownloadFileData();

            data.FilePath = toPath;
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
                    this.file_contents = (string)((Hashtable)OClient.Read(HpModel, [this.ID], [fileContents])[0])[fileContents];
                    return this.file_contents;
                }
            }
            return null;
        }
        public static List<HpVersion> DownloadContentsAll(List<HpVersion> versions)
        {
            string[] fileContents = ["file_contents", "dir_id", "name"];
            List<HpVersion> processVersions = versions.TakeAndRemove(version =>
            {
                if (version.file_contents is not null and not "") return false;
                return true;
            }).ToList();
            
            ArrayList ids = new(processVersions.Select(v => 
            {
                return v.ID;
            }).ToArray());
            //string[] fileContentsBase64 = 
            //ArrayList results = OClient.Read(GetHpModel(), ids, [fileContents], 60000);
            HpVersion[] readyVersions = HpVersion.GetRecordsByIDS(ids, includedFields: fileContents);
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
        public FileData DownloadFileData()
        {
            if (file_contents == null) DownloadContents();

            FileData file = new(name, type:null);
            if (file_contents == null) return file;

            byte[] fileContents = Convert.FromBase64String(file_contents);
            file.FileContents = fileContents;

            return file;
        }
        public static FileData[] DownloadFilesData(List<HpVersion> versions)
        {
            versions = DownloadContentsAll(versions);
            //string[] fileContentsBase64 = DownloadContentsAll(versions).ToArray();
            //if (versions.Count() != fileContentsBase64.Length) return null;
            int vLen = versions.Count();
            FileData[] fileData = new FileData[vLen];

            for (int i = 0; i < vLen; i++)
            {
                FileData file = new(versions[i].name, type: null);

                if (versions[i] != null && versions[i].file_contents is not null and not "")
                {
                    byte[] fileContents = Convert.FromBase64String(versions[i].file_contents);
                    file.Name = versions[i].name;
                    file.FileContents = fileContents;
                    file.FilePath = versions[i].winPathway;
                }
                else
                {
                    file.FileContents = null;
                }

                fileData[i] = file;
            }
            return fileData;
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
            if (!OdooDefaults.ExtToType.ContainsKey(hackFile.TypeExt))
                return null;

            string fileBase64 = hackFile.fileContents != null
            ? Convert.ToBase64String(hackFile.fileContents)
            : FileOperations.ConvertToBase64(hackFile.FullPath);

            HpVersion newVersion = new()
            {
                name = $"{entry.ID}.{hackFile.Name}",
                dir_id = entry.dir_id,
                entry_id = entry.ID,
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
			await newVersion.CreateAsync( false );

            if (newVersion.ID == 0) return null;


            return newVersion;
        }
        internal static async Task<HpVersion[]> CreateAllNew( params (HackFile hackFile, HpEntry entry, HashedValueStoring hashStoreType)[] data)
        {
            ArrayList versions = data.Select(d => PrepareCreation(d.hackFile, d.entry, d.hashStoreType)).ToArrayList();

            List<int> ids = await MultiCreateAsync<HpVersion>(versions, false);
            return null;
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
    public class IrAttachment : HpBaseModel<IrAttachment>
    {
        public string name;
        public int res_id;
        public int file_size;
        public string res_model;
        public string checksum;
        public string mimetype;
        public string type;

        private string fileContentsBase64;
        public IrAttachment() { }
        public IrAttachment(
            string name,
            int res_id = 0,
            int file_size = 0,
            string res_model = null,
            string checksum = null,
            string mimetype = null,
            string type = "binary",
            string fileContentsBase64 = null)
        {
            this.name = name;
            this.res_id = res_id;
            this.file_size = file_size;
            this.res_model = res_model;
            this.checksum = checksum;
            this.mimetype = mimetype;
            this.type = type;
            this.fileContentsBase64 = fileContentsBase64;
        }

		public string DownloadContents()
        {
            const string datas = "datas";
            if (this.IsRecord || this.ID != 0)
            {
                // reads the datas field in ir.attachment and returns an ArrayList with one record because of one ID
                // which contains a hashtable with keys: datas and id. datas has a value of string which is the base 64 file contents
                this.fileContentsBase64 = (string)((Hashtable)OClient.Read(HpModel, [this.ID], [datas])[0])[datas];
                return this.fileContentsBase64;
            }
            return null;
        }
        public FileData DownloadFileData()
        {
            DownloadContents();
            FileData file = new(name, type);
            if (fileContentsBase64 == null) return file;
            
            byte[] fileContents = Convert.FromBase64String(fileContentsBase64);
            file.FileContents = fileContents;

            return file;
        }
        public string GetFileContentsB64() => fileContentsBase64;
    }
    public struct FileData(
        string name,
        string type,
        string filePath = null,
        int fileSize = 0,
        //string checksum = null, 
        byte[] fileContents = null)
    {
        // file information
        public string Name { get; set; } = name;
        public string Type { get; set; } = type;
        public int FileSize { get; set; } = fileSize;

        // ir.attachment records store by SHA1 checksum
        private string m_Checksum = null;
        public string Checksum 
        { 
            get
            {
                if (m_Checksum != null) return m_Checksum;
                if (FileContents != null && FileContents.Length > 0)
                {
                    using (var sha = SHA1.Create())
                    {
                        byte[] byteArr = sha.ComputeHash(FileContents);

                        m_Checksum = string.Join("", byteArr.Select(i => i.ToString("X2")));
                        return m_Checksum;
                    }
                }
                return null;
            }
            
        }

        // directory info
        public string FilePath { get; set; } = filePath;

        // file contents
        public byte[] FileContents { get; set; } = fileContents;

        public bool CreateFile()
        {
            return FileOperations.WriteAllBytes(this);
        }
        public async static Task<int> CreateFiles(FileData[] filedata)
        {
            List<Task<bool>> tasks = [];
            int success = 0;

            foreach (FileData file in filedata)
            {
                if (file.FileContents != null && file.FileContents.Length > 0)
                    tasks.Add(FileOperations.WriteAllBytesAsync(file));
            }
            Task<bool[]> waitTask = Task.WhenAll(tasks);
            await waitTask;
            foreach (bool val in waitTask.Result) success += val ? 1 : 0;
            return success;
        }
        
    }
    public class HpVersionProperty : HpBaseModel<HpVersionProperty>
    {
        public string sw_config_name;
        public string text_value;
        public float number_value;
        public bool yesno_value;
        public string date_value;
        public int version_id;
        public int prop_id;
        public string prop_name;

        public HpVersionProperty() { }
        public HpVersionProperty(
            string sw_config_name = null,
            string text_value = null,
            float number_value = default,
            bool yesno_value = default,
            string date_value = null,
            int version_id = 0,
            int prop_id = 0)
        {
            this.sw_config_name = sw_config_name;
            this.text_value = text_value;
            this.number_value = number_value;
            this.yesno_value = yesno_value;
            this.date_value = date_value;
            this.version_id = version_id;
            this.prop_id = prop_id;
        }
        public PropertyType GetValueType()
        {
            if (text_value != null && text_value != "" && text_value != "False") return PropertyType.Text;
            if (date_value != null && date_value != "" && date_value != "False") return PropertyType.Date;
            if (number_value != default) return PropertyType.Number;
            if (yesno_value != default) return PropertyType.YesNo;
            return PropertyType.None;
        }
        public bool IsText( out string text )
        {
            PropertyType pType = GetValueType();
            text = null;
            if (pType == PropertyType.Text)
            {
                text = text_value;
                return true;
            }
            return false;
        }
        public bool IsNumber( out float number )
        {
            PropertyType pType = GetValueType();
            number = default;
            if (pType == PropertyType.Number)
            {
                number = number_value;
                return true;
            }
            return false;
        }
        public bool IsYesNo(out bool yesNo)
        {
            PropertyType pType = GetValueType();
            yesNo = default;
            if (pType == PropertyType.YesNo)
            {
                yesNo = yesno_value;
                return true;
            }
            return false;
        }
        public bool IsDate(out string date)
        {
            PropertyType pType = GetValueType();
            date = null;
            if (pType == PropertyType.Date)
            {
                date = text_value;
                return true;
            }
            return false;
        }
        public bool IsNone()
        {
            bool isValue = true;
            
            isValue = IsText(out _);
            if (isValue) return false;
            
            isValue = IsNumber(out _);
            if (isValue) return false;

            isValue = IsDate(out _);
            if (isValue) return false;
            
            isValue = IsYesNo(out _);
            if (isValue) return false;

            return true;
        }
        public enum PropertyType
        {
            Text,
            Number,
            YesNo,
            Date,
            None,
        }
    }
    public class HpVersionRelationship : HpBaseModel<HpVersionRelationship>
    {
        public int parent_id;
        public int child_id;

        public HpVersionRelationship() { } 
        public HpVersionRelationship(
            int parent_id = 0,
            int child_id = 0)
        {
            this.parent_id = parent_id;
            this.child_id = child_id;
        }
        public static bool Create(params HpVersion[] versions)
        {
            ArrayList ids = versions.Select(v => v.ID).ToArrayList();
            ArrayList versionFields = OClient.Read(HpVersion.GetHpModel(), ids, ["id", "file_ext"]);
            // HpVersionRelationship
            foreach (HpVersion version in versions)
            {
                List<string[]> dependencies = HackDefaults.docMgr.GetDependencies(version.winPathway);

            }
            return false;
        }
    }
    public class HpRelease : HpBaseModel<HpRelease>
    {
        public string release_note;
        public int release_user_id;

        public HpRelease() { }
        public HpRelease(
            string release_note,
            int release_user_id = 0)
        {
            this.release_note = release_note;
            this.release_user_id = release_user_id;
        }
    }
    public class HpReleaseVersionRel : HpBaseModel<HpReleaseVersionRel>
    {
        public int release_id;
        public int release_version;
        public int release_user;

        public HpReleaseVersionRel() { }
        public HpReleaseVersionRel(
            int release_id = 0,
            int release_version = 0,
            int release_user = 0)
        {
            this.release_id = release_id;
            this.release_version = release_version;
            this.release_user = release_user;
        }
    }
    public class HpType : HpBaseModel<HpType>
    {
        public string description;
        public string file_ext;
        public string icon;
        public string type_regex;
        public int cat_id;
        public Image image_save {get;set;}

        public HpType()
        { 
        }
        public HpType(
            string description = null,
            string file_ext = null,
            string iconBase64 = null,
            string type_regex = null,
            int cat_id = 0)
        {
            this.description = description;
            this.file_ext = file_ext; 
            this.icon = iconBase64;
            this.type_regex = type_regex;
            this.cat_id = cat_id;
            this.image_save = null;
        }
		public HpType(
			string description = null,
			string file_ext = null,
			Image icon = null,
			string type_regex = null,
			int cat_id = 0,
            bool saveImageType = false)
		{
			this.description = description;
			this.file_ext = file_ext;
			this.type_regex = type_regex;
			this.cat_id = cat_id;

            if (saveImageType) this.image_save = icon;
            else this.image_save = null;

			this.icon = icon?.ToBase64String();
		}
	}
    public class HpProperty : HpBaseModel<HpProperty>
    {
        public string name;
        public string prop_type;
        public bool active;

        public HpProperty() { }
        public HpProperty(
            string name,
            string prop_type = null,
            bool active = default)
        {
            this.name = name;
            this.prop_type = prop_type;
            this.active = active;
        }
		public override string ToString()
		{
			return name;
		}
	}
    public class HpUser : HpBaseModel<HpUser>
    {
        public string name;
		public string login;
        public string email;
        public string tz_offset;
        public string signature;
        public string totp_secret;
        public string odoobot_state;
        public string notification_type;
        public DateTime? login_date;
        
        public int? partner_id;
        public int? company_id;
        public int? groups_id;
        public int? action_id;

        public int? log_ids;
        public int? company_ids;
        
        public int? groups_count;
        public int? companies_count;
        public int? accesses_count;
        public int? rules_count;
        
        public bool? share;
        public bool? active;
        public bool? active_partner;

        public HpUser() {}

		public HpUser( string name,
				string login = null,
				string email = null,
				string tz_offset = null,
				string signature = null,
				string totp_secret = null,
				string odoobot_state = null,
				string notification_type = null,
				DateTime? login_date = null,
				int? partner_id = null,
				int? company_id = null,
				int? groups_id = null,
				int? action_id = null,
				int? log_ids = null,
				int? company_ids = null,
				int? groups_count = null,
				int? companies_count = null,
				int? accesses_count = null,
				int? rules_count = null,
				bool? share = null,
				bool? active = null,
				bool? active_partner = null)
		{
			this.name= name;
			this.login= login;
			this.email= email;
			this.tz_offset= tz_offset;
			this.signature= signature;
			this.totp_secret= totp_secret;
			this.odoobot_state= odoobot_state;
			this.notification_type= notification_type;
			this.login_date= login_date;
			this.partner_id= partner_id;
			this.company_id= company_id;
			this.groups_id= groups_id;
			this.action_id= action_id;
			this.log_ids= log_ids;
			this.company_ids= company_ids;
			this.groups_count= groups_count;
			this.companies_count= companies_count;
			this.accesses_count= accesses_count;
			this.rules_count= rules_count;
			this.share= share;
			this.active= active;
			this.active_partner= active_partner;
		}
		public override string ToString()
		{
			return name;
		}
	}
}
