using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

using HackPDM.Extensions.General;
using HackPDM.Forms.Hack;
using HackPDM.Hack;
using HackPDM.Odoo.OdooModels;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Properties;
using HackPDM.Src.ClientUtils.Types;

using Meziantou.Framework.Win32;

using MessageBox = System.Windows.Forms.MessageBox;

//using static System.Net.Mime.MediaTypeNames;


using OClient = HackPDM.Odoo.OdooClient;

namespace HackPDM.Odoo;

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
    public const string ODOO_VERSION_KEY_NAME = "client_version";
    public const string SW_KEY_NAME = "swdocmgr_key";
    public const string RESTRICT_PROP_NAME = "restrict_properties";
    public const string RESTRICT_TYPES_NAME = "restrict_types";

    public static readonly string[] DependentExt = [".SLDPRT", ".SLDASM", ".SLDDRW"];

    public static readonly string DependentExtRegex = "(?i)(" + string.Join("|", DependentExt.Select(s => $"(\\{s}$)")) + ")";
    public static string[] EntryFilterPatterns = [.. HpEntryNameFilters?.Select(eFilter => eFilter.name_regex) ?? []];
    // lock asynchonous operations
    private static readonly object MLockObject = new();
    public static string? OdooDb 
    {
        get => Settings.Get<string>("OdooDb");

        set
        {
            Settings.Set("OdooDb", value);
        }
    }
    public static string? OdooAddress
    {
        get => Settings.Get<string>("OdooAddress");
        set => Settings.Set("OdooAddress", value);
    }
    public static string? OdooPort
    {
        get => Settings.Get<string>("OdooPort");
        set => Settings.Set("OdooPort", value);
    }
    public static string? OdooUrl 
    {
        get 
        {
            if (field is null or "")
            {
                string? port = Settings.Get<string>("OdooPort");
                port = port is null or "" ? "" : $":{port}";

                field = $"http://{OdooAddress}{port}";
            }
            return field;
        }

        set
        {
            field = value;
        }
    }
    public static string? OdooSwKey
    {
        get => field ??= Settings.Get<string>("SwLicenseKey");

        set
        {
            Settings.Set("SwLicenseKey", value);
            field = value;
        }
    }
    public static decimal OdooAreaFactor
    {
        get => field = Settings.Get<decimal>("AreaFactor");

        set
        {
            Settings.Set("AreaFactor", value);
            field = value;
        }
    }
    public static string? OdooCredentialTarget 
    {
        // Settings.Get<string?>("OdooCredentialTarget", StorageBox.DEFAULT_ODOO_CREDENTIALS)
        get => field ??= StorageBox.DEFAULT_ODOO_CREDENTIALS;

        set
        {
            Settings.Set("OdooCredentialTarget", value);
            field = value;
        }
    }
    public static string? OdooUser
    {
        get 
        {
			if (field is not null) return field;
			var cm = CredentialManager.ReadCredential(StorageBox.DEFAULT_ODOO_CREDENTIALS, CredentialType.Generic);
			field = cm?.UserName;
            return field;
        }

        set
        {
			if (!string.IsNullOrEmpty(OdooPass)) CredentialManager.WriteCredential(StorageBox.DEFAULT_ODOO_CREDENTIALS, value ?? "", OdooPass, CredentialPersistence.LocalMachine);
            field = value;
        }
    }
    public static string? OdooPass
    {
        get
        {
			if (field is not null) return field;
			// read from windows credential manager
			var cm = CredentialManager.ReadCredential(StorageBox.DEFAULT_ODOO_CREDENTIALS, CredentialType.Generic);
            field = cm?.Password;
            return field;
        }

        set
        {
			if (!string.IsNullOrEmpty(OdooUser)) CredentialManager.WriteCredential(StorageBox.DEFAULT_ODOO_CREDENTIALS, OdooUser, value ?? "", CredentialPersistence.LocalMachine);
            field = value;
        }
    }
	private static bool _failedLogin = false;
	public static int? OdooId
    {
        get
        {
            try
            {
				if (!_failedLogin && field is null or 0)
				{
					field = OClient.Login(7000);
					if (field is null or 0)
					{
						_failedLogin = true;
					}
					return field;
				}
				return field;
            }
            catch
            {
                field = 0;
            }
            return field;
        }

		internal set
		{
			if (value is not (null or 0))
			{
				if (value != field)
				{
					_failedLogin = false;
					field = value;
				}
			}
		}
    }
    public static HpNode? MyNode
    {
        get
        {
            field ??= HpNodes?.FirstOrDefault(node => node.name.Equals(Environment.MachineName.ToLower()))
	            ?? TryAssignNewHpNode().Result ?? throw new ArgumentNullException(nameof(HpNode), @"Unable to register new node");
            return field;
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
            field ??= HpSettings?.First(setting => setting.name == "max_concurrency").int_value;
            return field;
        }
    }
    public static int? MaxBatchSize
    {
        get
        {
            field ??= HpSettings?.First(setting => setting.name == "max_batch_size").int_value;
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
    public static string SwApi = HpSettings?.First(sett => sett.name == SW_KEY_NAME).char_value ?? "";
    public static bool RestrictProperties = HpSettings?.First(sett => sett.name == RESTRICT_PROP_NAME).bool_value ?? true;
    public static bool RestrictTypes = HpSettings?.First(sett => sett.name == RESTRICT_TYPES_NAME).bool_value ?? true;
	
    public static HpEntryNameFilter[] HpEntryNameFilters
    {
        get
        {
            field ??= HpEntryNameFilter.GetAllRecords();
            return field;
        } 
        set => field = value;
    }
    public static HpCategory[] HpCategories
    {
        get
        {
            field ??= HpCategory.GetAllRecords();
            return field;
        }
        set => field = value;
    }
    public static HpType[] HpTypes
    {
        get
        {
            field ??= HpType.GetAllRecords();
            return field;
        }
        set => field = value;
    }
    public static HpProperty[] HpProperties
    {
        get
        {
            field ??= HpProperty.GetAllRecords();
            return field;
        }
        set => field = value;
    }
    public static HpNode[] HpNodes
    {
        get
        {
            field ??= HpNode.GetAllRecords();
            return field;
        }
        set => field = value;
    }
    public static HpUser[] HpUsers
    {
        get
        {
            field ??= HpUser.GetAllRecords();
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
            field ??= ExtensionMapType( HpTypes );
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
            field ??= ExtensionMapCategory( HpCategories, [ .. ExtToType.Values ] );
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
    public static Dictionary<int, HpProperty> IdToProp
    {
        get
        {
            field ??= IdMapProperty(HpProperties);
            return field;
        }
        set => field = value;
    }
    public static Dictionary<int, HpUser> IdToUser
    {
        get
        {
            field ??= IdMapUser( HpUsers );
            return field;
        }
        set => field = value;
    }
    public static Dictionary<string, HpEntryNameFilter> ExtToFilter
    {
        get
        {
            field ??= ExtensionMapFilter( HpEntryNameFilters );
            return field;
        }
        set => field = value;
    }
	private static async Task<HpNode?> TryAssignNewHpNode()
	{
		HpNode? node = null;
		HpNode createdNode = new() { name = Environment.MachineName.ToLower(), };
		if (HpNodes.Any(n => n.name.Equals(createdNode.name)))
			return node;

		return await HpNode.GetRecordByIdAsync(await createdNode.CreateAsync());
	}
	private static Dictionary<string, HpEntryNameFilter> ExtensionMapFilter( HpEntryNameFilter [] hpEntryNameFilters )
    {
        Dictionary<string, HpEntryNameFilter> dict = [];

        foreach ( HpEntryNameFilter filter in hpEntryNameFilters )
        {
            dict.Add( $"{filter.name_proto}", filter );
        }
        return dict;
    }

    private static Dictionary<int, HpUser> IdMapUser( in HpUser[] hpUsers )
    {
        Dictionary<int, HpUser> dict = [];

        foreach ( HpUser user in hpUsers )
        {
            dict.Add( user.Id, user );
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
                if ( category.Id == type.cat_id )
                {
                    dict.Add( $".{type.file_ext.ToLower()}", category );
                    break;
                }
            }
        }
        return dict;
    }
    public static Dictionary<int, HpProperty> IdMapProperty(in HpProperty[] props )
    {
        Dictionary<int, HpProperty> dict = [];

        foreach ( HpProperty prop in props )
        {
            dict.Add( prop.Id, prop );
        }
        return dict;
    }
    public async static Task<(EntryReturnType, HpVersion?)> ConvertHackFile(HackFile hackFile)
    {
        Hashtable ht = [];
            
        ArrayList paths = hackFile.RelativePath.Split<ArrayList>("\\", StringSplitOptions.RemoveEmptyEntries);

		EntryReturnType entryReturn = EntryReturnType.Failed;
		try
        {
			// create directories that don't exist in odoo
            HpDirectory[] directories = await HpDirectory.CreateNew(paths);
            HpDirectory lastDirectory = directories.Last() ?? throw new Exception($"{HpDirectory.GetHpModel()} didn't create any records");
            // create an HpEntry that doesn't exist in odoo
            (entryReturn, HpEntry? entry) = await HpEntry.GetFallbackCreateEntryAsync(hackFile, lastDirectory.Id);

			switch (entryReturn)
			{
				case EntryReturnType.Created:
				{
					HackFileManager.Dialog?.AddStatusLine(StatusMessage.SUCCESS, $"Created new entry for {hackFile.Name}"); 
					break;
				}
				case EntryReturnType.GotExisting:
				{
					HackFileManager.Dialog?.AddStatusLine(StatusMessage.FOUND, $"Found existing entry for {hackFile.Name}"); 
					break;
				}
				case EntryReturnType.Failed:
				{
					HackFileManager.Dialog?.AddStatusLine(StatusMessage.ERROR, $"Failed to create entry for {hackFile.Name}"); 
					throw new Exception($"{hackFile.Name} was unable to create or get record");
				}
				case EntryReturnType.InvalidType:
				{
					if (RestrictTypes)
					{
						HackFileManager.Dialog?.AddStatusLine(StatusMessage.ERROR, $"Found invalid type for {hackFile.Name}, file extension {hackFile.TypeExt}");
						throw new Exception($"found invalid type for {hackFile.Name}, file extension {hackFile.TypeExt}");
					}
					else
						HackFileManager.Dialog?.AddStatusLine(StatusMessage.WARNING, $"Found invalid type for file extension {hackFile.TypeExt}, but continuing due to unrestricted types");
					break;
				}
			}

			// create an HpVersion that doesn't exist in odoo
			HpVersion version = await CreateNewVersion(hackFile, entry);
			if (version.Id is 0) entryReturn = EntryReturnType.Failed;
			return (entryReturn, version);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
        }
        return (entryReturn, null);
    }
    public async static Task<HpVersion> CreateNewVersion( HackFile hack, HpEntry entry )
    {
        try { 
            // create an HpVersion that doesn't exist in odoo
            HpVersion version = await HpVersion.CreateNew(hack, entry) ?? throw new Exception( $"{HpVersion.GetHpModel()} was unable to create new version for {entry.name}" );
            entry.latest_version_id = version.Id;
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
    public string Name;
    public string Type;
    public long Size;
    public string Checkout;
    public string Fullname;
    public DateTime LatestDate;
    public bool? Deleted;
    public string LatestChecksum;
    public string Category;
}