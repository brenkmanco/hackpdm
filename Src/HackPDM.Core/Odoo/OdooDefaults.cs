using System.Collections.Frozen;
using System.Text.RegularExpressions;

using HackPDM.Core.Configuration;
using HackPDM.Core.Credentials;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;

namespace HackPDM.Core.Odoo;

/// <summary>Connection settings backed by local JSON settings and the OS credential store.</summary>
public sealed class OdooConnectionSettings( ISettingsProvider? settings )
{
	private ISettingsProvider? _settings = settings;
	private string? _user;
	private string? _pass;
	private string? _address;
	private string? _port;
	private string? _db;
	private bool? _useHttps;
	private string? _url;
	private string? _swKey;
	private decimal? _areaFactor;
	private string? _credentialTarget;

	public ISettingsProvider? Settings
	{
		get => _settings;
		set
		{
			if( ReferenceEquals( _settings, value ) )
				return;
			_settings = value;
			_user = null;
			_pass = null;
			_address = null;
			_port = null;
			_db = null;
			_useHttps = null;
			_url = null;
			_swKey = null;
			_areaFactor = null;
			_credentialTarget = null;
		}
	}

	public string? OdooUser
	{
		get
		{
			if( !string.IsNullOrEmpty( _user ) )
				return _user;
			var cm = CrossPlatformCredentialManager.ReadCredential( OdooCredentialTarget ?? PdmConstants.DefaultOdooCredentials );
			return _user = cm?.UserName;
		}
		set => _user = value;
	}

	public string? OdooPass
	{
		get
		{
			if( _pass is not null )
				return _pass;
			var cm = CrossPlatformCredentialManager.ReadCredential( OdooCredentialTarget ?? PdmConstants.DefaultOdooCredentials );
			return _pass = cm?.Password;
		}
		set => _pass = value;
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
		get => _address ??= _settings?.Get<string>( nameof( OdooAddress ) );
		set
		{
			_address = value;
			_url = null;
			_settings?.Set( nameof( OdooAddress ), value );
		}
	}

	public string? OdooPort
	{
		get => _port ??= _settings?.Get<string>( nameof( OdooPort ) );
		set
		{
			_port = value;
			_url = null;
			_settings?.Set( nameof( OdooPort ), value );
		}
	}

	public string? OdooDb
	{
		get => _db ??= _settings?.Get<string>( nameof( OdooDb ) );
		set => _settings?.Set( nameof( OdooDb ), _db = value );
	}

	public bool OdooUseHttps
	{
		get => _useHttps ??= _settings?.Get( nameof( OdooUseHttps ), true ) ?? true;
		set
		{
			_useHttps = value;
			_url = null;
			_settings?.Set( nameof( OdooUseHttps ), value );
		}
	}

	public string? OdooUrl
	{
		get => _url ??= OdooEndpoint.BuildUrl( OdooAddress, OdooPort, OdooUseHttps );
		set => _url = value;
	}

	public string? OdooSwKey
	{
		get => _swKey ??= _settings?.Get<string>( "SwLicenseKey" );
		set => _settings?.Set( "SwLicenseKey", _swKey = value );
	}

	public decimal? OdooAreaFactor
	{
		get => _areaFactor ??= _settings?.Get<decimal?>( "AreaFactor" );
		set => _settings?.Set( "AreaFactor", _areaFactor = value );
	}

	public string? OdooCredentialTarget
	{
		get => _credentialTarget ??= _settings?.Get( nameof( OdooCredentialTarget ), PdmConstants.DefaultOdooCredentials );
		set
		{
			if( !string.Equals( _credentialTarget, value, StringComparison.Ordinal ) )
			{
				_user = null;
				_pass = null;
			}
			_credentialTarget = value;
			_settings?.Set( nameof( OdooCredentialTarget ), value );
		}
	}
}

/// <summary>Immutable reference data read from Odoo after login.</summary>
public sealed record OdooLookupCache
{
	public const int DefaultBatchSize = 50;
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
	public Regex? EntryFilterRegex { get; init; }
	public Dictionary<string, HpType> ExtToType { get; init; } = [];
	public Dictionary<string, HpCategory> ExtToCat { get; init; } = [];
	public Dictionary<string, HpProperty> ExtToProp { get; init; } = [];
	public Dictionary<string, HpEntryNameFilter> ExtToFilter { get; init; } = [];
	public Dictionary<int, HpProperty> IdToProp { get; init; } = [];
	public Dictionary<int, HpUser> IdToUser { get; init; } = [];
	public FrozenDictionary<string, HpCategory> NameToCategory { get; init; } = FrozenDictionary<string, HpCategory>.Empty;

	public static Task<OdooLookupCache> LoadAsync() => LoadAsync( progress: null );

	public static async Task<OdooLookupCache> LoadAsync( IProgress<string>? progress )
	{
		string machine = Environment.MachineName.ToLowerInvariant();
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

		myNode = nodes?.FirstOrDefault( node => string.Equals( node.name, machine, StringComparison.OrdinalIgnoreCase ) );
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
		Dictionary<string, HpCategory> nameToCategory = MapBy( categories, c => c.name );

		Dictionary<string, HpType> extToType = new( StringComparer.OrdinalIgnoreCase );
		Dictionary<string, HpCategory> extToCat = new( StringComparer.OrdinalIgnoreCase );
		if( types is not null )
		{
			foreach( HpType t in types )
			{
				if( Extension( t.file_ext ) is not { } ext )
					continue;
				extToType.TryAdd( ext, t );
				if( t.cat_id?.id is int catId && categoryById.TryGetValue( catId, out HpCategory? cat ) )
					extToCat.TryAdd( ext, cat );
			}
		}

		string[] patterns = [ .. filters?.Select( f => f.name_regex ).OfType<string>() ?? [] ];
		Regex? compiledFilter = BuildFilterRegex( patterns );

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
			EntryFilterPatterns = patterns,
			EntryFilterRegex = compiledFilter,
			ExtToFilter = MapBy( filters, f => $"{f.name_proto}", StringComparer.OrdinalIgnoreCase ),
			IdToUser = MapBy( users, u => u.id ?? 0 ),
			ExtToProp = MapBy( properties, p => p.name ?? "" ),
			IdToProp = MapBy( properties, p => p.id ?? 0 ),
			ExtToType = extToType,
			ExtToCat = extToCat,
			NameToCategory = nameToCategory.ToFrozenDictionary( StringComparer.Ordinal ),
		};
	}

	internal static Regex? BuildFilterRegex( string[]? patterns )
	{
		if( patterns is not { Length: > 0 } )
			return null;
		List<string>? valid = null;
		for( int i = 0; i < patterns.Length; i++ )
		{
			string pattern = patterns[ i ];
			if( string.IsNullOrEmpty( pattern ) )
				continue;
			try
			{
				_ = new Regex( pattern, RegexOptions.IgnoreCase );
				( valid ??= new( patterns.Length ) ).Add( $"(?:{pattern})" );
			}
			catch( ArgumentException ex )
			{
				HackLogger.Warn( "OdooLookup", $"Ignoring invalid entry filter regex '{pattern}': {ex.Message}" );
			}
		}
		return valid is null
			? null
			: new Regex( string.Join( "|", valid ), RegexOptions.IgnoreCase | RegexOptions.Compiled );
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
	public Regex? EntryFilterRegex => Lookups.EntryFilterRegex;
	public FrozenDictionary<string, HpCategory> NameToCategory => Lookups.NameToCategory;
	public int DownloadBatchSize => Lookups.DownloadBatchSize;
	public int ConcurrencySize => Lookups.ConcurrencySize;
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
