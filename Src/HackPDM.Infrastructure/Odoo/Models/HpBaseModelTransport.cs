using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using HackPDM.Core.General;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

using OClient = HackPDM.Infrastructure.Odoo.OdooClient;

namespace HackPDM.Infrastructure.Odoo.Models;

public abstract partial class HpBaseModelTransport : HpBaseModel
{
	// (MVVM) VIEWMODEL
}
public abstract partial class HpBaseModelTransport
{
	protected static new readonly Dictionary<Type, string> HpModelDictionary = new()
	{
		{typeof(IHpNodeModel),                OdooDefaultsConstants.HP_NODE},
		{typeof(IHpEntryModel),               OdooDefaultsConstants.HP_ENTRY},
		{typeof(IHpEntryNameFilterModel),     OdooDefaultsConstants.HP_ENTRY_NAME_FILTER},
		{typeof(IHpDirectoryModel),           OdooDefaultsConstants.HP_DIRECTORY},
		{typeof(IHpCategoryModel),            OdooDefaultsConstants.HP_CATEGORY},
		{typeof(IHpCategoryPropertyModel),    OdooDefaultsConstants.HP_CATEGORY_PROPERTY},
		{typeof(IHpVersionModel),             OdooDefaultsConstants.HP_VERSION},
		{typeof(IHpVersionPropertyModel),     OdooDefaultsConstants.HP_VERSION_PROPERTY},

		{typeof(IHpPDMCommitModel),     OdooDefaultsConstants.HP_PDM_COMMIT},
		{typeof(IHpRecordStagedModel),     OdooDefaultsConstants.HP_RECORD_STAGED},

		{typeof(IHpVersionRelationshipModel), OdooDefaultsConstants.HP_VERSION_RELATIONSHIP},
		{typeof(IHpReleaseModel),             OdooDefaultsConstants.HP_RELEASE},
		{typeof(IHpReleaseVersionRelModel),   OdooDefaultsConstants.HP_RELEASE_VERSION_REL},
		{typeof(IHpTypeModel),                OdooDefaultsConstants.HP_TYPE},
		{typeof(IHpPropertyModel),            OdooDefaultsConstants.HP_PROPERTY},
		{typeof(IHpSettingModel),             OdooDefaultsConstants.HP_SETTINGS},
		{typeof(IIrAttachment),				  OdooDefaultsConstants.IR_ATTACHMENT},
		{typeof(IHpUserModel),                OdooDefaultsConstants.RES_USERS},

		{typeof(HpNode),                OdooDefaultsConstants.HP_NODE},
		{typeof(HpEntry),               OdooDefaultsConstants.HP_ENTRY},
		{typeof(HpEntryNameFilter),     OdooDefaultsConstants.HP_ENTRY_NAME_FILTER},
		{typeof(HpDirectory),           OdooDefaultsConstants.HP_DIRECTORY},
		{typeof(HpCategory),            OdooDefaultsConstants.HP_CATEGORY},
		{typeof(HpCategoryProperty),    OdooDefaultsConstants.HP_CATEGORY_PROPERTY},
		{typeof(HpVersion),             OdooDefaultsConstants.HP_VERSION},
		{typeof(HpVersionProperty),     OdooDefaultsConstants.HP_VERSION_PROPERTY},
		{typeof(HpVersionRelationship), OdooDefaultsConstants.HP_VERSION_RELATIONSHIP},

		{typeof(HpPDMCommit),			OdooDefaultsConstants.HP_PDM_COMMIT},
		{typeof(HpRecordStaged),		OdooDefaultsConstants.HP_RECORD_STAGED},

		{typeof(HpRelease),             OdooDefaultsConstants.HP_RELEASE},
		{typeof(HpReleaseVersionRel),   OdooDefaultsConstants.HP_RELEASE_VERSION_REL},
		{typeof(HpType),                OdooDefaultsConstants.HP_TYPE},
		{typeof(HpProperty),            OdooDefaultsConstants.HP_PROPERTY},
		{typeof(HpSetting),             OdooDefaultsConstants.HP_SETTINGS},
		{typeof(IrAttachment),          OdooDefaultsConstants.IR_ATTACHMENT},
		{typeof(HpUser),                OdooDefaultsConstants.RES_USERS},
	};
}
public abstract partial class HpBaseModelTransport<T> : HpBaseModelTransport where T : HpBaseModelTransport, new()
{
	public Hashtable					ComputeHashtable(bool includeEmpty, string[]? excludedFieldNames = null, string[]? includedFieldNames = null, string[]? insertFieldNames = null)
	{
		// 1. Get the fields we want to send
		var fields = GetFields(excludedFieldNames, includedFieldNames, insertFieldNames);

		// 2. Convert model to outbound dictionary
		var dict = ToOdoo();

		// 3. Build the Hashtable
		var table = new Hashtable();

		foreach (var field in fields)
		{
			if( dict.TryGetValue( field, out var value ) )
			{
				if( includeEmpty || value is not null )
					table[ field ] = value;
			}
			else if( insertFieldNames is not null && insertFieldNames.Contains( field ) )
			{
				var val = HashedValues.TryGetValue(field, out var v) ? v : null;
				if( includeEmpty || val is not null )
					table[ field ] = val;
			}
		}

		if (id is not null and not 0)
		{
			table[nameof(id)] = id;
		}
		if (commit_id is not null and not {id: 0 })
		{
			table[nameof(commit_id)] = commit_id.id;
		}

		return table;
	}
	public async virtual Task<int>		CreateCommitAsync()
	{
		return (id = await OClient.CreateCommitAsync()) ?? throw new InvalidOperationException();
	}
	public virtual Task<int>			CreateAsync() => CreateAsync( false );
	public virtual Task<HpRecordStaged?> StageRecAsync() => StageRecAsync( false );
	public async virtual Task<int>		CreateAsync( bool withEmpty = false, string[]? excludedFields = null, string[]? insertFIelds = null )
	{
		Hashtable ht = ComputeHashtable(withEmpty, excludedFields);
		id = await OClient.CreateAsync( HpModel, ht, 10000 );

		IsRecord = id != 0;
		return id ?? 0;
	}
	public async virtual Task<HpRecordStaged?> StageRecAsync( bool withEmpty = false, string[]? excludedFields = null, string[]? insertFIelds = null )
	{
		var rec = PreStageRec( withEmpty, excludedFields, insertFIelds ) ;
		_ = await rec?.CreateAsync();
		return rec;
	}
	public async virtual Task<bool>		WriteChangedValuesAsync(params string[] fieldNamesToWrite)
	{
		Hashtable ht = [];
		Type type = GetType();

		foreach (string fieldName in fieldNamesToWrite)
		{
			PropertyInfo field = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
			ht.Add(fieldName, field.GetValue(this));
		}

		return await OClient.UpdateAsync(HpModel, id ?? 0, ht);
	}
	public virtual HpRecordStaged?		PreStageRec( bool withEmpty = false, string[]? excludedFields = null, string[]? insertFIelds = null )
	{
		Hashtable ht = ComputeHashtable(withEmpty, excludedFields);

		if( commit_id is null or { id: 0 } ) return null;
		if( HpModel == OdooDefaultsConstants.HP_RECORD_STAGED ) return this as HpRecordStaged;

		if( HpModel == OdooDefaultsConstants.HP_VERSION && HashedValues.TryGetValue( "__internal__existing", out _ ) )
			ht.Add( "__internal__existing", true );

		HpRecordStaged record = new()
		{
			target_model = HpModel,
			commit_id = commit_id,
			committing_id = (Many2One?)commit_id,
			payload = ht,
			HashedValues = { { "windows_complete_name", this.HashedValues.TryGetValue("windows_complete_name", out var v) ? v : null } },
		};
		return record.PreStageRec( false );
	}
	public static Task<ArrayList>		MultiCreateAsync(ArrayList records, bool withEmpty = false)
	{
		ArrayList hts = records.Select((HpBaseModelTransport<T> v) => v.ComputeHashtable(withEmpty)).ToArrayList();
		var type = typeof(T);
		string hpmodel = HpModelDictionary.TryGetValue(type, out hpmodel) ? hpmodel : null;
		var tempId = OClient.CreateAsync(hpmodel, hts);
		return tempId;
	}
	public static Task<ArrayList>		MultiStageAsync(HpBaseModelTransport<T>[] records, bool withEmpty = false )
	{
		ArrayList hts = records.Select(v => v.PreStageRec(withEmpty)?.ComputeHashtable(false)).ToArrayList();
		var tempId = OClient.CreateAsync(OdooDefaultsConstants.HP_RECORD_STAGED, hts);
		return tempId;
	}
	internal static T[]?				RecordsPopulation(IEnumerable<Hashtable>? hts, string[]? excludedFields = null, Dictionary<string, string>? remapNames = null)
	{
		if (hts is null) return null;

		if (remapNames is not null)
		{
			foreach (Hashtable ht in hts)
			{
				foreach (DictionaryEntry pair in ht)
				{
					if (remapNames.TryGetValue(pair.Key.ToString(), out string newName))
					{
						DictionaryEntry de = new(newName, pair.Value);
						ht[pair.Key.ToString()] = de;
					}
				}
			}
		}
		T[]? records = HashConverter.ConvertToClasses<T>(hts);
		FinalizePopulations(records, excludedFields);
		return records;
	}
	public static async Task<T[]?>		GetAllRecordsAsync(string[]? excludedFields = null, string[]? insertFields = null)
	{
		string modelName = GetHpModel();

		ArrayList fields = GetOdooFields(excludedFieldNames: excludedFields, insertFieldNames: insertFields);

		ArrayList result = await OClient.BrowseAsync(modelName, [new ArrayList(), fields], 10000);


		if (result.Count == 0) return null;

		var records = RecordsPopulation(hts: result?.Select<Hashtable, Hashtable>(static h => h), excludedFields);
		return [.. records ?? []];
	}
	public static async Task<T[]?>		GetRecordsBySmartSearchAsync(ArrayList? searchFilter = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null)
	{
		string modelName = GetHpModel();
		
		ArrayList fields = GetOdooFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);

		searchFilter ??= [];

		var result = await OClient.SmartSearchAsync(modelName, searchFilter, [], fields, 10000);
		if (result.Count == 0) return null;
		var records = RecordsPopulation(hts: result?.Select<Hashtable, Hashtable>(static h => h), excludedFields);
		return [.. records ?? []];
	}
	public static async Task<T[]?>		GetRecordsBySearchAsync(ArrayList? searchFilter = null, string[]? includedFields = null, string[]? excludedFields = null, string[]? insertFields = null)
	{
		string modelName = GetHpModel();

		ArrayList fields = GetOdooFields(excludedFieldNames: excludedFields, includedFieldNames: includedFields, insertFieldNames: insertFields);
		ArrayList result;

		if (searchFilter == null)
		{
			searchFilter = [];
		}

		result = await OClient.BrowseAsync(modelName, [searchFilter, fields], 10000);


		if (result.Count == 0) return null;

		var records = RecordsPopulation(hts: result?.Select<Hashtable, Hashtable>(static h => h), excludedFields);
		return [.. records ?? []];
	}
	public static async Task<T?>		GetRecordByIdAsync(int recordId, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null)
	{
		if (recordId == 0) return default;
		T[]? records = await GetRecordsByIdsAsync([recordId], excludedFields: excludedFields, includedFields: includedFields, insertFields: insertFields);
		return records != null && records!.Length > 0 ? records![0] : default;
	}
	public static async Task<T[]?>		GetRecordsByIdsAsync(ArrayList? recordIds, ArrayList? searchFilters = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null)
	{
		string modelName = GetHpModel();

		ArrayList fields = GetOdooFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		if (searchFilters == null)
		{
			result = await OClient.ReadAsync(modelName, recordIds, fields, 90000);
		}
		else
		{
			if (recordIds is not null and { Count: > 0 }) searchFilters.Add(new ArrayList { "id", "in", recordIds });
			result = await OClient.BrowseAsync(modelName, [searchFilters, new ArrayList { "dir_id" }], 90000);
			// result = await OClient.SmartSearchAsync(modelName, searchFilters, [], fields, 90000);
		}

		return result.Count == 0 
			? null 
			: RecordsPopulation(result.Select<Hashtable, Hashtable>(h => h), excludedFields);
	}
}
public abstract partial class HpBaseModelTransport<T> 
{
	public override string? HpModel 
	{ 
		get => field ??= GetHpModel();
		set => field = SetHpModel(value);
	}
	// getter setter
	public static string? SetHpModel(string? value)
	{
		if (value is null) return null;
		HpModelDictionaryGen[typeof(T).Name] = value;
		return value;
	}
	
	public abstract Dictionary<string, object?> ToOdoo();
	public static string[] GetFields( 
		string[]? excluded = null, 
		string[]? included = null, 
		string[]? insert = null 
		) 
		=> OdooFieldSelectionCache<T>.GetFields( excluded, included, insert ); 
	public static string? GetHpModel()
	{ 
		
		return HpModelDictionaryGen.GetValueOrDefault(typeof(T).Name);
	}
	
	public static void				FinalizePopulation(ref T record, string[]? excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None)
	{
		// set record settings

		// this is included in HashConverter.ConvertToClass
		//record.id = (int)ht["id"];

		record.IsRecord = true;
		record.ExcludedFields = excludedFields;
		
		record.CompleteConstruction();
	}
	public static void				FinalizePopulations(T[]? records, string[]? excludedFields = null)
	{
		if (records is not {Length: > 0 } ) return;
		
		for (int i = 0; i < records.Length; i++)
		{
			FinalizePopulation(ref records[i], excludedFields);
		}
	}
	public static ArrayList			GetAllFields() => GetOdooFields();
	public static ArrayList			GetOdooFields(string[]? excludedFieldNames = null, string[]? includedFieldNames = null, string[]? insertFieldNames = null)
		=> [.. GetFields(excludedFieldNames, includedFieldNames, insertFieldNames)];
	public override string			ToString() => id.ToString();

	// async methods
	public static async Task<TOther[]?> GetRelatedRecordsBySearchAsync<TOther>(ArrayList searchFilter, string relatedFieldName, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null) 
		where TOther : HpBaseModelTransport<TOther>, new()
	{
		string modelName = GetHpModel();

		List<TOther> records = [];
		ArrayList fields = HpBaseModelTransport<TOther>.GetOdooFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);

		var result = await OClient.RelatedSearchAsync(modelName, [searchFilter, relatedFieldName, fields], 60000);

		if (result.Count == 0) return null;

		foreach (Hashtable ht in result)
		{
			TOther record = HashConverter.ConvertToClass<TOther>(ht);

			// set record settings
			record.id = (int)ht["id"];
			//record.HashedValues = ht;
			if (record.HpModel == OdooDefaultsConstants.HP_VERSION && ht.TryGetValue("dir_id", out int value))
			{
				record.HashedValues = new Hashtable
				{
					{ "dir_id", value }
				};
			}
			record.IsRecord = true;
			record.ExcludedFields = excludedFields;
			record.CompleteConstruction();

			records.Add(record);
		}
		return [.. records];
	}
	public static async Task<TOther[]?> GetRelatedRecordByIdsAsync<TOther>(ArrayList recordIds, string relatedFieldName, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null) 
		where TOther : HpBaseModelTransport<TOther>, new()
	{
		string modelName = GetHpModel();

		List<TOther> records = [];
		ArrayList fields = HpBaseModelTransport<TOther>.GetOdooFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result = await OClient.RelatedBrowseAsync(modelName, [recordIds, relatedFieldName, fields], 60000);

		if (result.Count == 0) return null;

		foreach (Hashtable ht in result)
		{
			TOther record = HashConverter.ConvertToClass<TOther>(ht);

			// set record settings
			record.id = (int)ht["id"];
			//record.HashedValues = ht;
			if (record.HpModel == OdooDefaultsConstants.HP_VERSION && ht.TryGetValue("dir_id", out object value))
			{
				record.HashedValues = new Hashtable
				{
					{ "dir_id", value }
				};
			}
			record.IsRecord = true;
			record.ExcludedFields = excludedFields;
			record.CompleteConstruction();

			records.Add(record);
		}
		return [.. records];
	}
	public async Task					RefreshAsync()
	{
		if ((await OClient.ReadAsync(HpModel, [id], GetOdooFields()))?[0] is Hashtable ht)
		{
			HashConverter.AssignToClass(ht, this);

			// set record settings
			// HashedValues = ht;
			if (HpModel == OdooDefaultsConstants.HP_VERSION && ht.TryGetValue("dir_id", out int value))
			{
				HashedValues = new Hashtable
				{
					{ "dir_id", value }
				};
			}
			IsRecord = true;
			CompleteConstruction();
		}
	}
}