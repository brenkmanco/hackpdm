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
    public static IOdooDefaults Instance { get; set; } = new OdooDefaults();
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
			var cm = CrossPlatformCredentialManager.ReadCredential(OdooCredentialTarget ?? StorageBox.DEFAULT_ODOO_CREDENTIALS);
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
			var cm = CrossPlatformCredentialManager.ReadCredential(OdooCredentialTarget ?? StorageBox.DEFAULT_ODOO_CREDENTIALS);
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

		CrossPlatformCredentialManager.WriteCredential( credentialName ?? StorageBox.DEFAULT_ODOO_CREDENTIALS, username, password );
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
		// Each lookup is fetched, then its maps are built, before the next starts: initialization is
		// complete when this returns. A failed lookup leaves its maps empty instead of failing login.
		string machine = Environment.MachineName.ToLower();
		await TryHelper.TryAsync( async () =>
		{
			HpNodes = await HpNode.GetAllRecordsAsync();
			MyNode = HpNodes?.FirstOrDefault( node => node.name?.Equals( machine ) is true )
				?? await TryAssignNewHpNode()
				?? throw new ArgumentNullException( nameof( HpNode ), @"Unable to register new node" );
		} );
		await TryHelper.TryAsync( async () => HpDirectoryRoot = await HpDirectory.GetRecordByIdAsync( 1 ) );

		IdToUser = await TryHelper.TryAsync( async () => HpUsers = await HpUser.GetAllRecordsAsync() )
			? MapBy<IHpUserModel, int>( HpUsers, user => user.id ?? 0 )
			: [];

		if( await TryHelper.TryAsync( async () => HpProperties = await HpProperty.GetAllRecordsAsync() )
			&& HpProperties is IEnumerable<IHpPropertyModel> properties && properties.Any() )
		{
			ExtToProp = MapBy( properties, prop => prop?.name ?? "" );
			IdToProp = MapBy<IHpPropertyModel, int>( properties, prop => prop.id ?? 0 );
		}
		else
		{
			ExtToProp = [];
			IdToProp = [];
		}

		ExtToType = await TryHelper.TryAsync( async () => HpTypes = await HpType.GetAllRecordsAsync() )
			? MapBy<IHpTypeModel, string>( HpTypes, type => Extension( type.file_ext ) )
			: [];

		await TryHelper.TryAsync( async () => HpCategories = await HpCategory.GetAllRecordsAsync() );

		if( await TryHelper.TryAsync( async () => HpEntryNameFilters = await HpEntryNameFilter.GetAllRecordsAsync() ) )
		{
			EntryFilterPatterns = [ .. HpEntryNameFilters?.SkipNullSelect( eFilter => eFilter.name_regex! ) ?? [] ];
			ExtToFilter = MapBy<IHpEntryNameFilterModel, string>( HpEntryNameFilters, filter => $"{filter.name_proto}" );
		}

		if( await TryHelper.TryAsync( async () => HpSettings = await HpSetting.GetAllRecordsAsync() ) )
		{
			// a setting missing on the server keeps its default instead of failing the rest
			IHpSettingModel? Setting( string name ) => HpSettings?.FirstOrDefault( sett => sett.name == name );
			RestrictTypes       = Setting( OdooDefaultsConstants.RESTRICT_TYPES_NAME )?.bool_value ?? true;
			RestrictProperties  = Setting( OdooDefaultsConstants.RESTRICT_PROP_NAME )?.bool_value ?? true;
			SwApi               = Setting( OdooDefaultsConstants.SW_KEY_NAME )?.char_value ?? "";
			MaxBatchSize        = Setting( "max_batch_size" )?.int_value ?? 0;
			MaxConcurrency      = Setting( "max_concurrency" )?.int_value ?? 0;
			ConcurrencySize     = Math.Min( MaxConcurrency ?? 2, ConcurrencySize );
			DownloadBatchSize   = Math.Min( MaxBatchSize ?? 5, DownloadBatchSize );
		}

		// ExtToCat (needs the categories and types above)
		TryHelper.Try( () =>
		{
			// each type's extension → its category
			var categoryById = MapBy<IHpCategoryModel, int>( HpCategories, category => category.id ?? 0 );
			ExtToCat = MapBy( HpTypes?.Where( type => type.cat_id?.id is int id && categoryById.ContainsKey( id ) ), type => Extension( type.file_ext ) )
				.ToDictionary( pair => pair.Key, pair => categoryById[ pair.Value.cat_id!.id!.Value ] );
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
	/// <summary>A lookup map; the first record wins when two share a key (Dictionary.Add used to throw, emptying the whole map).</summary>
	private static Dictionary<TKey, TValue> MapBy<TValue, TKey>( IEnumerable<TValue>? records, Func<TValue, TKey?> key ) where TKey : notnull
	{
		Dictionary<TKey, TValue> map = [];
		foreach( TValue record in records ?? [] )
			if( key( record ) is { } k )
				map.TryAdd( k, record );
		return map;
	}

	private static string? Extension( string? fileExt ) => fileExt is null ? null : $".{fileExt.ToLower()}";

    #endregion
}

//
// All the fields in the classes below correspond to a field name in the odoo module
// so reflections can map it's values from the hashtable to the class fields like newtonsoft json
// converter converts to classes with properties that align with values from the json fields.
// changing field names will break the program unless they are mapped to the names of fields in odoo models.
//

