using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Abstractions;
using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

using Meziantou.Framework.Win32;

using Newtonsoft.Json.Linq;

using OClient = HackPDM.Infrastructure.Odoo.OdooClient;

//using static System.Net.Mime.MediaTypeNames;

namespace HackPDM.Infrastructure.Odoo;

public class OdooDefaults : IOdooDefaults
{
    public static IOdooDefaults? Instance { get; set; } = new OdooDefaults();
    public ISettingsProvider Settings { get; set; }

    private OdooDefaults() {}
    public OdooDefaults(ISettingsProvider settingsProvider)
    {
        if (Instance is not null)
        {
            Instance.Settings = settingsProvider;
            return;
        }
        Settings = settingsProvider;
        Instance = this;
    }

	#region Declarations
	public string? OdooUser
    {
        get 
        {
			if (!string.IsNullOrEmpty(field)) return field;
			var cm = CredentialManager.ReadCredential(OdooCredentialTarget ?? StorageBox.DEFAULT_ODOO_CREDENTIALS, CredentialType.Generic);
			field = cm?.UserName;
            return field;
        }
        set 
        {
            field = value;
        }
    }
    public string? OdooPass
    {
        get
        {
			if (field is not null) return field;
			// read from windows credential manager
			var cm = CredentialManager.ReadCredential(OdooCredentialTarget ?? StorageBox.DEFAULT_ODOO_CREDENTIALS, CredentialType.Generic);
            field = cm?.Password;
            return field;
        }
        set
        {
            field = value;
        }
    }
    public bool SaveCredentials() => TrySetWindowsOdooUserPass( OdooUser, OdooPass, OdooCredentialTarget );
	public static bool TrySetWindowsOdooUserPass(string? username, string? password, string? credentialName = null)
    {
        if( string.IsNullOrEmpty( password ) || string.IsNullOrEmpty( username ))
            return false;

		CredentialManager.WriteCredential( credentialName ?? StorageBox.DEFAULT_ODOO_CREDENTIALS, username, password, CredentialPersistence.LocalMachine );
        return true;
	}
    public string? OdooAddress
    {
        get => field ??= Settings.Get<string>("OdooAddress");
        set => Settings.Set("OdooAddress", field = value);
    }
    public string? OdooPort
    {
        get => field ??= Settings.Get<string>("OdooPort");
        set => Settings.Set("OdooPort", field = value);
        
    }
    public string? OdooDb 
    {
        get => field ??= Settings.Get<string>("OdooDb");
        set => Settings.Set("OdooDb", field = value);
    }
    public string? OdooUrl 
    {
        get 
        {
            if (field is not null) return field;
            string? port = Settings.Get<string>("OdooPort");
            port = port is null or "" ? "" : $":{port}";

            field = $"http://{OdooAddress}{port}";
            
            return field;
        }
        set
        {
            field = value;
        }
    }
    public string? OdooSwKey
    {
        get => field ??= Settings.Get<string>("SwLicenseKey");
        set => Settings.Set("SwLicenseKey", field = value);
    }
    public decimal? OdooAreaFactor
    {
        get => field ??= Settings.Get<decimal>("AreaFactor");
        set => Settings.Set("AreaFactor", field = value);
    }
    public string? OdooCredentialTarget 
    {
        // Settings.Get<string?>("OdooCredentialTarget", StorageBox.DEFAULT_ODOO_CREDENTIALS)
        get => field ??= Settings.Get( "OdooCredentialTarget", StorageBox.DEFAULT_ODOO_CREDENTIALS);
        set => Settings.Set("OdooCredentialTarget", field = value);
        
    }
    public int OdooId
    {
        get
        {
            try
            {
                if (field is not 0) return field;
                field = OClient.Login(7000) ?? 0;
                return field;
            }
            catch 
            {
            }
            return 0;
        }
        set
        {
            if (value != field) field = value;
        }
    } = 0;
    public string[] EntryFilterPatterns
    {
        get => [.. HpEntryNameFilters?.SkipNullSelect(eFilter => eFilter.name_regex) ?? []];
    }
    // lock asynchronous operations
    private readonly object MLockObject = new();
	
    public IHpNodeModel? MyNode
    {
        get => field ??= HpNodes?.FirstOrDefault(node => node.name.Equals(Environment.MachineName.ToLower()))
	            ?? TryAssignNewHpNode().Result 
                ?? throw new ArgumentNullException(nameof(HpNode), @"Unable to register new node");
    }

    public IHpDirectoryModel? HpDirectoryRoot
    {
        get => field ??= HpDirectory.GetRecordById(1);
        set => field = value;
    }
   
    public int DownloadBatchSize
    {
        get
        {
            if (field == 0)
            {
                field = MaxBatchSize ?? 5;
            }
            return field;
        }
        set
        {
            if (field == 0)
            {
                field = MaxBatchSize ?? 5;
            }
            field = Math.Min(MaxBatchSize ?? 5, field);
        }
    }
    public int ConcurrencySize
    {
        get
        {
            if (field == 0)
            {
                field = MaxConcurrency ?? 2;
            }
            return field;
        }
        internal set
        {
            if (field == 0)
            {
                field = MaxConcurrency ?? 2;
            }
            field = Math.Min(MaxConcurrency ?? 2, field);
        }
    }
    public int? MaxConcurrency
    {
        get
        {
            field ??= HpSettings?.First(setting => setting.name == "max_concurrency").int_value;
            return field;
        }
    }
    public int? MaxBatchSize
    {
        get
        {
            field ??= HpSettings?.First(setting => setting.name == "max_batch_size").int_value;
            return field;
        }
    }
    // low enough number of records to get before
    public IHpSettingModel []? HpSettings
    {
        get => field ??= HpSetting.GetAllRecords();
        set => field = value;
    }
    public string? SwApi
    {
        get => !string.IsNullOrEmpty(field) ? field : HpSettings?.First(sett => sett.name == OdooDefaultsConstants.SW_KEY_NAME).char_value;
        set => field = value;
    }
    public bool? RestrictProperties
	{
		get => field ??= HpSettings?.First(sett => sett.name == OdooDefaultsConstants.RESTRICT_PROP_NAME).bool_value ?? true;
		set => field = value;
	} 
    public bool? RestrictTypes
	{
		get => field ??= HpSettings?.First(sett => sett.name == OdooDefaultsConstants.RESTRICT_TYPES_NAME).bool_value ?? true;
		set => field = value;
	}
    public IHpEntryNameFilterModel[]? HpEntryNameFilters
    {
        get => field ??= HpEntryNameFilter.GetAllRecords();
        set => field = value;
    }
    public IHpCategoryModel[]? HpCategories
    {
        get => field ??= HpCategory.GetAllRecords();
        set => field = value;
    }
    public IHpTypeModel[]? HpTypes
    {
        get => field ??= HpType.GetAllRecords();
        set => field = value;
    }
    public IHpPropertyModel[]? HpProperties
    {
        get => field ??= HpProperty.GetAllRecords();
        set => field = value;
    }
    public IHpNodeModel[]? HpNodes
    {
        get => field ??= HpNode.GetAllRecords();
        set => field = value;
    }
    public IHpUserModel[]? HpUsers
    {
        get => field ??= HpUser.GetAllRecords();
        set => field = value;
    }

    // dictionary mapping some field type to the HpModel
    // like extension to Type or Category
    public Dictionary<string, IHpTypeModel> ExtToType 
    { 
        get => field ??= HpTypes is IEnumerable<IHpTypeModel> arr && arr.Any() 
            ? ExtensionMapType( HpTypes )
            : [];
        set => field = value;
    }
    public Dictionary<string, IHpCategoryModel> ExtToCat
    {
        get => field ??= 
            HpCategories is HpCategory[] arr && arr.Length != 0
				&& HpTypes is HpType[] arrTypes && arrTypes.Length != 0
				? ExtensionMapCategory( arr, arrTypes )
                : [];
        set =>field = value;
    }
    public Dictionary<string, IHpPropertyModel> ExtToProp
    {
        get => field ??= HpProperties is IEnumerable<IHpPropertyModel> arr && arr.Any() 
            ? new(arr.Select(prop => new KeyValuePair<string, IHpPropertyModel>(prop?.name ?? "", prop)))
            : [];
        set => field = value;
    }
    public Dictionary<string, IHpEntryNameFilterModel> ExtToFilter
    {
        get => field ??= HpEntryNameFilters is IEnumerable<IHpEntryNameFilterModel> arr && arr.Any() 
            ? ExtensionMapFilter( HpEntryNameFilters )
            : [];
        set => field = value;
    }
    public Dictionary<int, IHpPropertyModel> IdToProp
    {
        get => field ??= HpProperties is IEnumerable<IHpPropertyModel> arr && arr.Any()
            ? IdMapProperty(HpProperties)
            : [];
        set => field = value;
    }
    public Dictionary<int, IHpUserModel> IdToUser
    {
        get => field ??= HpUsers is IEnumerable<IHpUserModel> arr && arr.Any()
            ? IdMapUser(HpUsers)
            : [];
        set => field = value;
    }
    #endregion
    
    #region Functions

    public async Task<HpNode?> TryAssignNewHpNode()
	{
		HpNode? node = null;
		HpNode createdNode = new() { name = Environment.MachineName.ToLower(), };
		return HpNodes?.Any(n => n.name.Equals(createdNode.name)) is true
			? node
			: await HpNode.GetRecordByIdAsync(await createdNode.CreateAsync());
	}
	private static Dictionary<string, IHpEntryNameFilterModel> ExtensionMapFilter( IHpEntryNameFilterModel []? hpEntryNameFilters )
    {
        if (hpEntryNameFilters is null) return [];

        Dictionary<string, IHpEntryNameFilterModel> dict = [];
        foreach ( IHpEntryNameFilterModel filter in hpEntryNameFilters )
        {
            dict.Add( $"{filter.name_proto}", filter );
        }
        return dict;
    }
    private static Dictionary<int, IHpUserModel> IdMapUser( in IHpUserModel[]? hpUsers )
    {
        if (hpUsers is null) return [];
        Dictionary<int, IHpUserModel> dict = [];

        foreach ( IHpUserModel user in hpUsers )
        {
            dict.Add( user.id ?? 0, user );
        }
        return dict;
    }










    // TODO: fix type.file_ext being null.
    // field is not actually null but the field
    // AssignClasses funciton is not reading properly

    public static Dictionary<string, IHpTypeModel> ExtensionMapType( in IHpTypeModel []? types )
    {
        if (types is null) return [];
        Dictionary<string, IHpTypeModel> dict = [];
        foreach ( HpType type in types )
        {
            dict.Add( $".{type.file_ext?.ToLower()}", type );
        }
        return dict;
    }
















    public static Dictionary<string, IHpCategoryModel> ExtensionMapCategory( in HpCategory []? categories, in HpType []? types )
    {
        if (categories is null || types is null) return [];

        Dictionary<string, IHpCategoryModel> dict = [];
        foreach ( HpType type in types )
        {
            foreach ( HpCategory category in categories )
            {
                if (category.id != type.cat_id?.id || type.file_ext is null) continue;
                dict.Add( $".{type.file_ext.ToLower()}", category );
                break;
            }
        }
        return dict;
    }
    public static Dictionary<int, IHpPropertyModel> IdMapProperty(in IHpPropertyModel[]? props )
    {
		if (props is null) return [];
		Dictionary<int, IHpPropertyModel> dict = [];

        foreach ( HpProperty prop in props )
        {
            dict.Add( prop.id ?? 0, prop );
        }
        return dict;
    }
    
    public async static Task<(HpVersion?, HpRecordStaged?)> CreateNewVersion( HackFile hack, IHpEntryModel? entry, IHpRecordStagedModel? staged )
    {
        try 
        { 
            // create an HpVersion that doesn't exist in odoo
            if (entry is null || entry.id == 0)
            {
                HpRecordStaged? entryStaged = staged as HpRecordStaged;
				HpRecordStaged? versionStaged = await HpVersion.CreateNew(hack, entryStaged) ?? throw new Exception($"{HpVersion.GetHpModel()} was unable to create new version for {entry.name}");
                return (null, versionStaged);
			}
            else
            {
                HpEntry? ent = entry as HpEntry;
                HpVersion version = await HpVersion.CreateNew(hack, entry) ?? throw new Exception( $"{HpVersion.GetHpModel()} was unable to create new version for {entry.name}" );
			    ent?.latest_version_id = version.id;
                return (version, null); ;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"{e.Message}\n{e.StackTrace}");
        }
        return (null, null);
    }
    #endregion
}

//
// All the fields in the classes below correspond to a field name in the odoo module
// so reflections can map it's values from the hashtable to the class fields like newtonsoft json
// converter converts to classes with properties that align with values from the json fields.
// changing field names will break the program unless they are mapped to the names of fields in odoo models.
//

