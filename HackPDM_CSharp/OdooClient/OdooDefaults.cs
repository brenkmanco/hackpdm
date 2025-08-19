using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

using CredentialManagement;

using HackPDM.Extensions.General;


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
        // odoo name identifiers
        public const string OdooVersionKeyName = "client_version";
        public const string SWKeyName = "swdocmgr_key";
        public const string RestrictPropName = "restrict_properties";
        public const string RestrictTypesName = "restrict_types";

        public static readonly string[] dependentExt = [".SLDPRT", ".SLDASM", ".SLDDRW"];
        public static string[] EntryFilterPatterns = [.. HpEntryNameFilters.Select(eFilter => eFilter.name_regex)];
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
        public static int? OdooID
        {
            get
            {
                try
                {
                    if (field is null or 0)
                    {
                        field = OClient.Login(9000);
                    }
                }
                catch
                {
                    field = 0;
                }
                return field;
            }

            set => field = value;
        }
        public static HpNode MyNode
        {
            get
            {
                field ??= HpNodes.First(node => node.name == System.Environment.MachineName.ToLower());
                return field;
            }
            set
            {
                field = value;
            }
        }
        public static int DownloadBatchSize
        {
            get
            {
                if (field == 0)
                {
                    field = OdooDefaults.MaxBatchSize ?? 5;
                }
                return field;
            }
            set
            {
                if (field == 0)
                {
                    field = OdooDefaults.MaxBatchSize ?? 5;
                }
                field = Math.Min(OdooDefaults.MaxBatchSize ?? 5, field);
            }
        }
        public static int ConcurrencySize
        {
            get
            {
                if (field == 0)
                {
                    field = OdooDefaults.MaxConcurrency ?? 2;
                }
                return field;
            }
            set
            {
                if (field == 0)
                {
                    field = OdooDefaults.MaxConcurrency ?? 2;
                }
                field = Math.Min(OdooDefaults.MaxConcurrency ?? 2, field);
            }
        }
        public static int? MaxConcurrency
        {
            get
            {
                field ??= HpSettings.First(setting => setting.name == "max_concurrency").int_value;
                return field;
            }
        }
        public static int? MaxBatchSize
        {
            get
            {
                field ??= HpSettings.First(setting => setting.name == "max_batch_size").int_value;
                return field;
            }
        }
        // low enough number of records to get before
        public static HpSetting [] HpSettings
        {
            get
            {
                field ??= HpSetting.GetAllRecords();
                return field;
            }
            set => field = value;
        }
        public static string SWApi = HpSettings.First(sett => sett.name == SWKeyName).char_value;
        public static bool RestrictProperties = HpSettings.First(sett => sett.name == RestrictPropName).bool_value;
        public static bool RestrictTypes = HpSettings.First(sett => sett.name == RestrictTypesName).bool_value;
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
        public static Dictionary<string, HpProperty> ExtToProp
        {
            get 
            {
                if (field is null)
                {
                    field = [];

                    foreach (HpProperty prop in HpProperties)
                    {
                        field.Add(prop.name, prop);
                    }
                }
                return field;
            }
            set => field = value;
        }
		public static Dictionary<int, HpProperty> IDToProp
        {
            get
            {
                field ??= IDMapProperty(HpProperties);
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
    public class HpEntryMini : HpBaseModel<HpEntry>
    {
        public string name;
        public string type;
        public long size;
        public string checkout;
        public string fullname;
        public DateTime latest_date;
        public bool? deleted;
        public string latest_checksum;
        public string category;
    }
}
