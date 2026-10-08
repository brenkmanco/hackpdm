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
        get;
        set
        {
            if (value != field) field = value;
        }
    } = 0;
    public string[] EntryFilterPatterns
    {
        get;
        set;
    }
	
    public IHpNodeModel? MyNode
    {
        get;
        set;
    }

    public IHpDirectoryModel? HpDirectoryRoot
    {
        get;
        set;
    }
   
    public int DownloadBatchSize
    {
        get;
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
        get;
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
        get;
        set;
    }
    public int? MaxBatchSize
    {
        get;
        set;
    }
    // low enough number of records to get before
    public IHpSettingModel []? HpSettings
    {
        get;
        set;
    }
    public string? SwApi
    {
        get;
        set;
    }
    public bool? RestrictProperties
	{
        get;
        set;
	} 
    public bool? RestrictTypes
	{
        get;
        set;
	}
    public IHpEntryNameFilterModel[]? HpEntryNameFilters
    {
        get;
        set;
    }
    public IHpCategoryModel[]? HpCategories
    {
        get;
        set;
    }
    public IHpTypeModel[]? HpTypes
    {
        get;
        set;
    }
    public IHpPropertyModel[]? HpProperties
    {
        get;
        set;
    }
    public IHpNodeModel[]? HpNodes
    {
        get;
        set;
    }
    public IHpUserModel[]? HpUsers
    {
        get;
		set;
	}

    // dictionary mapping some field type to the HpModel
    // like extension to Type or Category
    public Dictionary<string, IHpTypeModel> ExtToType 
    {
        get;
        set;
    }
    public Dictionary<string, IHpCategoryModel> ExtToCat
    {
        get;
        set;
    }
    public Dictionary<string, IHpPropertyModel> ExtToProp
    {
        get;
        set;
    }
    public Dictionary<string, IHpEntryNameFilterModel> ExtToFilter
    {
        get;
        set;
    }
    public Dictionary<int, IHpPropertyModel> IdToProp
    {
        get;
        set;
    }
    public Dictionary<int, IHpUserModel> IdToUser
    {
        get;
        set;
    }
    #endregion
    
    #region Functions
    public async Task InitializeLoginAsync()
	{
		await TryHelper.TryAsync( async () => OdooId = OdooId is 0 ? ( await OdooClient.LoginJson( 7000 ) ?? 0 ) : OdooId );
	}
	public async Task InitializeAsync()
    {
        // OdooId
        if (OdooId is 0)
        {
            await InitializeLoginAsync();
        }
        if (OdooId is 0 )
		{
			throw new Exception( "OdooId is 0 after login attempt" );
		}
		// HpNodes && MyNode
		await TryHelper.TryAsync( async () => HpNodes                 = await HpNode.GetAllRecordsAsync() )
            .ContinueWith( async ( success ) => MyNode      = await success ? HpNodes?.FirstOrDefault( node => node.name.Equals( Environment.MachineName.ToLower() ) )
				?? TryAssignNewHpNode().Result
				?? throw new ArgumentNullException( nameof( HpNode ), @"Unable to register new node" ) : null);
		// HpDirectoryRoot
		await TryHelper.TryAsync( async () => HpDirectoryRoot         = await HpDirectory.GetRecordByIdAsync( 1 ) );
		// HpUsers && IdToUser
		await TryHelper.TryAsync( async () => HpUsers                 = await HpUser.GetAllRecordsAsync() )
            .ContinueWith( async ( success ) => IdToUser    = await success ? IdMapUser( HpUsers ) : [] );
        // HpProperties && ExtToProp && IdToProp
        await TryHelper.TryAsync( async () => HpProperties = await HpProperty.GetAllRecordsAsync() )
             .ContinueWith( async ( success ) =>
             {
                 ExtToProp = await success 
					 ?     HpProperties is IEnumerable<IHpPropertyModel> arr && arr.Any()
						 ?     new( arr.Select( prop => new KeyValuePair<string, IHpPropertyModel>( prop?.name ?? "", prop ) ) )
						 :     []  
					 :     [];
				 IdToProp = HpProperties is IEnumerable<IHpPropertyModel> arr2 && arr2.Any()
			        ? IdMapProperty( HpProperties )
			        : [];
			 } );
		// HpTypes && ExtToType
		await TryHelper.TryAsync( async () => HpTypes                 = await HpType.GetAllRecordsAsync() )
            .ContinueWith( async ( success ) => ExtToType   = await success ? ExtensionMapType( HpTypes ) : [] );
        // HpCategories
		await TryHelper.TryAsync( async () => HpCategories            = await HpCategory.GetAllRecordsAsync() );
		// HpEntryNameFilters && EntryFilterPatterns && ExtToFilter
		await TryHelper.TryAsync( async () => HpEntryNameFilters      = await HpEntryNameFilter.GetAllRecordsAsync() )
			.ContinueWith( async ( success ) =>
            {
                if( !await success )
                    return;

				EntryFilterPatterns = await success ? [ .. HpEntryNameFilters?.SkipNullSelect( eFilter => eFilter.name_regex! ) ?? []] : [];
				ExtToFilter         = HpEntryNameFilters is IEnumerable<IHpEntryNameFilterModel> arr && arr.Any()
			        ? ExtensionMapFilter( HpEntryNameFilters )
			        : [];
			} );
		// HpSettings && RestrictTypes && RestrictProperties && SwApi &&
        // MaxBatchSize && MaxConcurrency && ConcurrencySize && DownloadBatchSize
		await TryHelper.TryAsync( async () => HpSettings              = await HpSetting.GetAllRecordsAsync() )
			.ContinueWith( async ( success ) =>
            {
                if( !await success )
                    return;
                
				RestrictTypes       = ( HpSettings?.First( sett => sett.name == OdooDefaultsConstants.RESTRICT_TYPES_NAME ).bool_value ?? true );
				RestrictProperties  = ( HpSettings?.First( sett => sett.name == OdooDefaultsConstants.RESTRICT_PROP_NAME ).bool_value ?? true );
				SwApi               = ( HpSettings?.First( sett => sett.name == OdooDefaultsConstants.SW_KEY_NAME ).char_value ?? "" );
				MaxBatchSize        = ( HpSettings?.First( sett => sett.name == "max_batch_size" ).int_value ?? 0 );
				MaxConcurrency      = ( HpSettings?.First( sett => sett.name == "max_concurrency" ).int_value ?? 0 );
				ConcurrencySize     = ( Math.Min( MaxConcurrency ?? 2, ConcurrencySize ) );
				DownloadBatchSize   = ( Math.Min( MaxBatchSize ?? 5, DownloadBatchSize ) );
                
			} );
		TryHelper.
				// ExtToCat
				Try( () =>
        {
            ExtToCat = HpCategories is HpCategory[] arr && arr.Length != 0
                && HpTypes is HpType[] arrTypes && arrTypes.Length != 0
                ? ExtensionMapCategory( arr, arrTypes )
                : [];
        } );
	}

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
				HpRecordStaged? versionStaged = await HpVersion.StageVersion(hack, entryStaged) ?? throw new Exception($"{HpVersion.GetHpModel()} was unable to create new version for {entry.name}");

                return (null, versionStaged);
			}
            else
            {
                HpEntry? ent = entry as HpEntry;
                HpVersion version = await HpVersion.CreateVersion(hack, entry, staged?.commit_id?.id ?? 0) ?? throw new Exception( $"{HpVersion.GetHpModel()} was unable to create new version for {entry.name}" );
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

