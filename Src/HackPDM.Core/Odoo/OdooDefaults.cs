using HackPDM.Core.Configuration;
using HackPDM.Core.Credentials;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Odoo;

/// <summary>Connection settings backed by local JSON settings and the OS credential store.</summary>
public sealed class OdooConnectionSettings( ISettingsProvider? settings )
{
	private bool? _useHttps;

	public ISettingsProvider? Settings { get; set; } = settings;

	public string? OdooUser
	{
		get
		{
			if( !string.IsNullOrEmpty( field ) )
				return field;
			var cm = CrossPlatformCredentialManager.ReadCredential( OdooCredentialTarget ?? PdmConstants.DefaultOdooCredentials );
			return field = cm?.UserName;
		}
		set;
	}

	public string? OdooPass
	{
		get
		{
			if( field is not null )
				return field;
			var cm = CrossPlatformCredentialManager.ReadCredential( OdooCredentialTarget ?? PdmConstants.DefaultOdooCredentials );
			return field = cm?.Password;
		}
		set;
	}

	public bool SaveCredentials()
		=> SaveCredentials( OdooUser, OdooPass, OdooCredentialTarget );

	public static bool SaveCredentials( string? username, string? password, string? credentialName = null )
	{
		if( string.IsNullOrEmpty( password ) || string.IsNullOrEmpty( username ) )
			return false;
		CrossPlatformCredentialManager.WriteCredential(
			credentialName ?? PdmConstants.DefaultOdooCredentials,
			username,
			password );
		return true;
	}

	public string? OdooAddress
	{
		get => field ??= Settings?.Get<string>( "OdooAddress" );
		set => Settings?.Set( "OdooAddress", field = value );
	}

	public string? OdooPort
	{
		get => field ??= Settings?.Get<string>( "OdooPort" );
		set => Settings?.Set( "OdooPort", field = value );
	}

	public string? OdooDb
	{
		get => field ??= Settings?.Get<string>( "OdooDb" );
		set => Settings?.Set( "OdooDb", field = value );
	}

	public bool OdooUseHttps
	{
		get => _useHttps ??= Settings?.Get( "OdooUseHttps", true ) ?? true;
		set => Settings?.Set( "OdooUseHttps", ( _useHttps = value ).Value );
	}

	public string? OdooUrl
	{
		get => field ??= OdooEndpoint.BuildUrl( OdooAddress, Settings?.Get<string>( "OdooPort" ), OdooUseHttps );
		set;
	}

	public string? OdooSwKey
	{
		get => field ??= Settings?.Get<string>( "SwLicenseKey" );
		set => Settings?.Set( "SwLicenseKey", field = value );
	}

	public decimal? OdooAreaFactor
	{
		get => field ??= Settings?.Get<decimal>( "AreaFactor" );
		set => Settings?.Set( "AreaFactor", field = value );
	}

	public string? OdooCredentialTarget
	{
		get => field ??= Settings?.Get( "OdooCredentialTarget", PdmConstants.DefaultOdooCredentials );
		set => Settings?.Set( "OdooCredentialTarget", field = value );
	}
}

/// <summary>Immutable reference data read from Odoo after login.</summary>
public sealed record OdooLookupCache
{
	public const int DefaultBatchSize = 5;
	public const int DefaultConcurrency = 2;

	public static OdooLookupCache Empty { get; } = new();

	public HpNode[]? HpNodes { get; init; }
	public HpNode? MyNode { get; init; }
	public HpDirectory? HpDirectoryRoot { get; init; }
	public HpUser[]? HpUsers { get; init; }
	public HpProperty[]? HpProperties { get; init; }
	public HpType[]? HpTypes { get; init; }
	public HpCategory[]? HpCategories { get; init; }
	public HpEntryNameFilter[]? HpEntryNameFilters { get; init; }
	public HpSetting[]? HpSettings { get; init; }

	public bool? RestrictTypes { get; init; }
	public bool? RestrictProperties { get; init; }
	public string? SwApi { get; init; }
	public int? MaxBatchSize { get; init; }
	public int? MaxConcurrency { get; init; }

	public int DownloadBatchSize => MaxBatchSize is > 0 and int size ? size : DefaultBatchSize;
	public int ConcurrencySize => MaxConcurrency is > 0 and int count ? count : DefaultConcurrency;

	public string[] EntryFilterPatterns { get; init; } = [];
	public Dictionary<string, HpType> ExtToType { get; init; } = [];
	public Dictionary<string, HpCategory> ExtToCat { get; init; } = [];
	public Dictionary<string, HpProperty> ExtToProp { get; init; } = [];
	public Dictionary<string, HpEntryNameFilter> ExtToFilter { get; init; } = [];
	public Dictionary<int, HpProperty> IdToProp { get; init; } = [];
	public Dictionary<int, HpUser> IdToUser { get; init; } = [];

	public static Task<OdooLookupCache> LoadAsync() => LoadAsync( progress: null );

	public static async Task<OdooLookupCache> LoadAsync( IProgress<string>? progress )
	{
		string machine = Environment.MachineName.ToLower();
		HpNode[]? nodes = null;
		HpNode? myNode = null;
		HpDirectory? root = null;
		HpUser[]? users = null;
		HpProperty[]? properties = null;
		HpType[]? types = null;
		HpCategory[]? categories = null;
		HpEntryNameFilter[]? filters = null;
		HpSetting[]? settings = null;
		bool nodesRead = false;
		bool settingsRead = false;

		progress?.Report( "Loading workspace metadata (nodes, users, types, settings)…" );
		await Task.WhenAll(
			TryAsync( async () => { nodes = await HpNode.GetAllRecordsAsync(); nodesRead = true; } ),
			TryAsync( async () => root = await HpDirectory.GetRecordByIdAsync( 1 ) ),
			TryAsync( async () => users = await HpUser.GetAllRecordsAsync() ),
			TryAsync( async () => properties = await HpProperty.GetAllRecordsAsync() ),
			TryAsync( async () => types = await HpType.GetAllRecordsAsync() ),
			TryAsync( async () => categories = await HpCategory.GetAllRecordsAsync() ),
			TryAsync( async () => filters = await HpEntryNameFilter.GetAllRecordsAsync() ),
			TryAsync( async () => { settings = await HpSetting.GetAllRecordsAsync(); settingsRead = true; } ) );

		myNode = nodes?.FirstOrDefault( node => node.name?.Equals( machine ) is true );
		if( myNode is null && nodesRead )
		{
			progress?.Report( $"Registering workstation node '{machine}'…" );
			await TryAsync( async () =>
			{
				HpNode created = new() { name = machine };
				myNode = await HpNode.GetRecordByIdAsync( await created.CreateAsync() );
			} );
		}

		HpSetting? Setting( string name ) => settings?.FirstOrDefault( s => s.name == name );
		Dictionary<int, HpCategory> categoryById = MapBy( categories, c => c.id ?? 0 );

		return new OdooLookupCache
		{
			HpNodes = nodes,
			MyNode = myNode,
			HpDirectoryRoot = root,
			HpUsers = users,
			HpProperties = properties,
			HpTypes = types,
			HpCategories = categories,
			HpEntryNameFilters = filters,
			HpSettings = settings,
			RestrictTypes = !settingsRead ? null : Setting( PdmConstants.RestrictTypesKey )?.bool_value ?? true,
			RestrictProperties = !settingsRead ? null : Setting( PdmConstants.RestrictPropKey )?.bool_value ?? true,
			SwApi = !settingsRead ? null : Setting( PdmConstants.SwKeyName )?.char_value ?? "",
			MaxBatchSize = Setting( "max_batch_size" )?.int_value,
			MaxConcurrency = Setting( "max_concurrency" )?.int_value,
			EntryFilterPatterns = [ .. filters?.Select( f => f.name_regex ).OfType<string>() ?? [] ],
			ExtToFilter = MapBy( filters, f => $"{f.name_proto}" ),
			IdToUser = MapBy( users, u => u.id ?? 0 ),
			ExtToProp = MapBy( properties, p => p.name ?? "" ),
			IdToProp = MapBy( properties, p => p.id ?? 0 ),
			ExtToType = MapBy( types, t => Extension( t.file_ext ), StringComparer.OrdinalIgnoreCase ),
			ExtToCat = MapBy( types?.Where( t => t.cat_id?.id is int id && categoryById.ContainsKey( id ) ), t => Extension( t.file_ext ), StringComparer.OrdinalIgnoreCase )
				.ToDictionary( pair => pair.Key, pair => categoryById[ pair.Value.cat_id!.id!.Value ], StringComparer.OrdinalIgnoreCase ),
		};
	}

	private static async Task TryAsync( Func<Task> action )
	{
		try
		{
			await action();
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "OdooLookup", $"Reference lookup failed: {ex.Message}", ex );
		}
	}

	private static Dictionary<TKey, TValue> MapBy<TValue, TKey>(
		IEnumerable<TValue>? records,
		Func<TValue, TKey?> key,
		IEqualityComparer<TKey>? comparer = null )
		where TKey : notnull
	{
		Dictionary<TKey, TValue> map = new( comparer );
		foreach( TValue record in records ?? [] )
		{
			if( key( record ) is { } k )
				map.TryAdd( k, record );
		}
		return map;
	}

	private static string? Extension( string? fileExt )
	{
		if( string.IsNullOrWhiteSpace( fileExt ) )
			return null;
		string trimmed = fileExt.Trim().TrimStart( '.' );
		return trimmed.Length == 0 ? null : $".{trimmed.ToLowerInvariant()}";
	}
}

/// <summary>Process-wide Odoo session: connection settings, logged-in user id, and reference lookup cache.</summary>
public sealed class OdooDefaults
{
	public static OdooDefaults Instance { get; set; } = new();

	public OdooConnectionSettings Connection { get; }
	public OdooLookupCache Lookups { get; set; } = OdooLookupCache.Empty;

	public OdooDefaults() => Connection = new OdooConnectionSettings( null );

	public OdooDefaults( ISettingsProvider settingsProvider )
	{
		Connection = new OdooConnectionSettings( settingsProvider );
		if( Instance is not null )
		{
			Instance.Settings = settingsProvider;
			return;
		}
		Instance = this;
	}

	public ISettingsProvider? Settings { get => Connection.Settings; set => Connection.Settings = value; }
	public string? OdooUser { get => Connection.OdooUser; set => Connection.OdooUser = value; }
	public string? OdooPass { get => Connection.OdooPass; set => Connection.OdooPass = value; }
	public string? OdooAddress { get => Connection.OdooAddress; set => Connection.OdooAddress = value; }
	public string? OdooPort { get => Connection.OdooPort; set => Connection.OdooPort = value; }
	public string? OdooDb { get => Connection.OdooDb; set => Connection.OdooDb = value; }
	public string? OdooUrl { get => Connection.OdooUrl; set => Connection.OdooUrl = value; }
	public bool OdooUseHttps { get => Connection.OdooUseHttps; set => Connection.OdooUseHttps = value; }
	public string? OdooSwKey { get => Connection.OdooSwKey; set => Connection.OdooSwKey = value; }
	public decimal? OdooAreaFactor { get => Connection.OdooAreaFactor; set => Connection.OdooAreaFactor = value; }
	public string? OdooCredentialTarget { get => Connection.OdooCredentialTarget; set => Connection.OdooCredentialTarget = value; }

	public bool SaveCredentials() => Connection.SaveCredentials();

	public int OdooId { get; set; }
	public string? LoginError { get; private set; }

	public async Task InitializeLoginAsync()
	{
		if( OdooId != 0 )
			return;
		Result<int> login = await OdooRpc.LoginAsync( 7000 );
		LoginError = login.Error;
		OdooId = login.ValueOr( 0 );
	}

	public Task InitializeAsync() => InitializeAsync( progress: null );

	public async Task InitializeAsync( IProgress<string>? progress )
	{
		progress?.Report( "Signing in to Odoo…" );
		await InitializeLoginAsync();
		if( OdooId == 0 )
			throw new Exception( $"login failed: {LoginError ?? "no user id"}" );
		Lookups = await OdooLookupCache.LoadAsync( progress );
	}

	public HpNode? MyNode => Lookups.MyNode;
	public HpDirectory? HpDirectoryRoot => Lookups.HpDirectoryRoot;
	public string[] EntryFilterPatterns => Lookups.EntryFilterPatterns;
	public int DownloadBatchSize => Lookups.DownloadBatchSize;
	public int ConcurrencySize => Lookups.ConcurrencySize;
	public int? MaxConcurrency => Lookups.MaxConcurrency;
	public int? MaxBatchSize => Lookups.MaxBatchSize;
	public HpSetting[]? HpSettings => Lookups.HpSettings;
	public string? SwApi => Lookups.SwApi;
	public bool? RestrictProperties => Lookups.RestrictProperties;
	public bool? RestrictTypes => Lookups.RestrictTypes;
	public HpEntryNameFilter[]? HpEntryNameFilters => Lookups.HpEntryNameFilters;
	public HpCategory[]? HpCategories => Lookups.HpCategories;
	public HpType[]? HpTypes => Lookups.HpTypes;
	public HpProperty[]? HpProperties => Lookups.HpProperties;
	public HpNode[]? HpNodes => Lookups.HpNodes;
	public HpUser[]? HpUsers => Lookups.HpUsers;
	public Dictionary<string, HpType> ExtToType => Lookups.ExtToType;
	public Dictionary<string, HpCategory> ExtToCat => Lookups.ExtToCat;
	public Dictionary<string, HpProperty> ExtToProp => Lookups.ExtToProp;
	public Dictionary<string, HpEntryNameFilter> ExtToFilter => Lookups.ExtToFilter;
	public Dictionary<int, HpProperty> IdToProp => Lookups.IdToProp;
	public Dictionary<int, HpUser> IdToUser => Lookups.IdToUser;
}
