using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Infrastructure.Odoo;

/// <summary>
/// The server's reference data, read once after login: nodes, users, types, categories, properties,
/// entry name filters, settings, and the maps built from them. Immutable: a reload makes a new one.
/// </summary>
public sealed record OdooLookupCache
{
	public const int DefaultBatchSize = 5;
	public const int DefaultConcurrency = 2;

	/// <summary>Nothing loaded yet: empty maps, no restrictions decided.</summary>
	public static OdooLookupCache Empty { get; } = new();

	public IHpNodeModel[]? HpNodes { get; init; }
	public IHpNodeModel? MyNode { get; init; }
	public IHpDirectoryModel? HpDirectoryRoot { get; init; }
	public IHpUserModel[]? HpUsers { get; init; }
	public IHpPropertyModel[]? HpProperties { get; init; }
	public IHpTypeModel[]? HpTypes { get; init; }
	public IHpCategoryModel[]? HpCategories { get; init; }
	public IHpEntryNameFilterModel[]? HpEntryNameFilters { get; init; }
	public IHpSettingModel[]? HpSettings { get; init; }

	public bool? RestrictTypes { get; init; }
	public bool? RestrictProperties { get; init; }
	public string? SwApi { get; init; }
	public int? MaxBatchSize { get; init; }
	public int? MaxConcurrency { get; init; }

	/// <summary>Files per download batch: the max_batch_size setting, or the default when it is unset or 0.</summary>
	public int DownloadBatchSize => MaxBatchSize is > 0 and int size ? size : DefaultBatchSize;
	/// <summary>Parallel downloads: the max_concurrency setting, or the default when it is unset or 0.</summary>
	public int ConcurrencySize => MaxConcurrency is > 0 and int count ? count : DefaultConcurrency;

	public string[] EntryFilterPatterns { get; init; } = [];
	public Dictionary<string, IHpTypeModel> ExtToType { get; init; } = [];
	public Dictionary<string, IHpCategoryModel> ExtToCat { get; init; } = [];
	public Dictionary<string, IHpPropertyModel> ExtToProp { get; init; } = [];
	public Dictionary<string, IHpEntryNameFilterModel> ExtToFilter { get; init; } = [];
	public Dictionary<int, IHpPropertyModel> IdToProp { get; init; } = [];
	public Dictionary<int, IHpUserModel> IdToUser { get; init; } = [];

	/// <summary>
	/// Reads all reference data from the server (must be logged in) and builds the lookup maps.
	/// A lookup that fails stays empty instead of failing the rest; this machine's node is registered when the server doesn't know it yet.
	/// </summary>
	/// <returns>A new cache. Never throws for server errors: failed reads leave their arrays null and their maps empty.</returns>
	/// <remarks>
	/// Reads hp.node, hp.directory id 1 (the root), hp.user, hp.property, hp.type, hp.category, hp.entry.name.filter and
	/// hp.setting one after another. Side effect: creates an hp.node named after the lower-cased machine name when none
	/// matches. <see cref="RestrictTypes"/>, <see cref="RestrictProperties"/> and <see cref="SwApi"/> stay null when the
	/// settings could not be read, and default to true / true / "" when a setting is missing. Extension keys are
	/// lower-cased with a leading dot (".sldprt"); when two records share a key, the first wins.
	/// </remarks>
	public static async Task<OdooLookupCache> LoadAsync()
	{
		string machine = Environment.MachineName.ToLower();
		HpNode[]? nodes = null;
		IHpNodeModel? myNode = null;
		await TryHelper.TryAsync( async () =>
		{
			nodes = await HpNode.GetAllRecordsAsync();
			myNode = nodes?.FirstOrDefault( node => node.name?.Equals( machine ) is true )
				?? await RegisterNodeAsync( machine )
				?? throw new ArgumentNullException( nameof( HpNode ), "Unable to register new node" );
		} );

		HpDirectory? root = null;
		HpUser[]? users = null;
		HpProperty[]? properties = null;
		HpType[]? types = null;
		HpCategory[]? categories = null;
		HpEntryNameFilter[]? filters = null;
		HpSetting[]? settings = null;
		await TryHelper.TryAsync( async () => root = await HpDirectory.GetRecordByIdAsync( 1 ) );
		await TryHelper.TryAsync( async () => users = await HpUser.GetAllRecordsAsync() );
		await TryHelper.TryAsync( async () => properties = await HpProperty.GetAllRecordsAsync() );
		await TryHelper.TryAsync( async () => types = await HpType.GetAllRecordsAsync() );
		await TryHelper.TryAsync( async () => categories = await HpCategory.GetAllRecordsAsync() );
		await TryHelper.TryAsync( async () => filters = await HpEntryNameFilter.GetAllRecordsAsync() );
		// read (even with no rows): the restrictions are decided; unread they stay undecided (null)
		bool settingsRead = await TryHelper.TryAsync( async () => settings = await HpSetting.GetAllRecordsAsync() );

		// a setting missing on the server keeps its default instead of failing the rest
		IHpSettingModel? Setting( string name ) => settings?.FirstOrDefault( setting => setting.name == name );
		Dictionary<int, IHpCategoryModel> categoryById = MapBy<IHpCategoryModel, int>( categories, category => category.id ?? 0 );

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

			RestrictTypes = !settingsRead ? null : Setting( OdooDefaultsConstants.RESTRICT_TYPES_NAME )?.bool_value ?? true,
			RestrictProperties = !settingsRead ? null : Setting( OdooDefaultsConstants.RESTRICT_PROP_NAME )?.bool_value ?? true,
			SwApi = !settingsRead ? null : Setting( OdooDefaultsConstants.SW_KEY_NAME )?.char_value ?? "",
			MaxBatchSize = Setting( "max_batch_size" )?.int_value,
			MaxConcurrency = Setting( "max_concurrency" )?.int_value,

			EntryFilterPatterns = [ .. filters?.Select( filter => filter.name_regex ).OfType<string>() ?? [] ],
			ExtToFilter = MapBy<IHpEntryNameFilterModel, string>( filters, filter => $"{filter.name_proto}" ),
			IdToUser = MapBy<IHpUserModel, int>( users, user => user.id ?? 0 ),
			ExtToProp = MapBy<IHpPropertyModel, string>( properties, property => property.name ?? "" ),
			IdToProp = MapBy<IHpPropertyModel, int>( properties, property => property.id ?? 0 ),
			ExtToType = MapBy<IHpTypeModel, string>( types, type => Extension( type.file_ext ) ),
			// each type's extension → its category
			ExtToCat = MapBy<IHpTypeModel, string>( types?.Where( type => type.cat_id?.id is int id && categoryById.ContainsKey( id ) ), type => Extension( type.file_ext ) )
				.ToDictionary( pair => pair.Key, pair => categoryById[ pair.Value.cat_id!.id!.Value ] ),
		};
	}

	private static async Task<HpNode?> RegisterNodeAsync( string machine )
	{
		HpNode created = new() { name = machine };
		return await HpNode.GetRecordByIdAsync( await created.CreateAsync() );
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
}
