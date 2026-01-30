using System.Collections;
using System.Reflection;
using HackPDM.Core.General;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;
using HackPDM.Shared.OdooAttributes;

using OClient = HackPDM.Infrastructure.Odoo.OdooClient;

namespace HackPDM.Infrastructure.Odoo.Models;

public abstract partial class HpBaseModelTransport : HpBaseModel
{
	// (MVVM) VIEWMODEL
	public virtual Task<int> Create() => Create(false);
	public async virtual Task<int> Create(bool withEmpty = false)
	{
		Hashtable ht = ComputeHashtable(true);
		var tempId = await OClient.CreateAsync(HpModel, ht, 10000);

		if( tempId != 0 )
		{
			Id = tempId;
			//HashedValues = ht;
			if( HpModel == OdooDefaultsConstants.HP_VERSION && ht.TryGetValue<string, object>( "dir_id", out object? value ) )
			{
				HashedValues = new Hashtable
				{
					{ "dir_id", value }
				};
			}
			IsRecord = true;
		}
		
		return tempId;
	}
	public virtual Task<int> CreateAsync() => CreateAsync(false);
	public virtual Task<int> CreateAsync(bool withEmpty = false, string[]? excludedFields = null)
	{
		Hashtable ht = ComputeHashtable(withEmpty, excludedFields, isNew: true);
		var tempId = OClient.CreateAsync(HpModel, ht, 10000);

		IsRecord = true;
		return tempId;
	}
	//private void PopSelf(string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null)
	//{
	//	Type type = GetType();
	//	string modelName = HpModelDictionary[type];

	//	ArrayList fields = GetFields(type, includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
	//	ArrayList result;

	//	result = OClient.Read(modelName, [Id], fields, 90000);

	//	if (result.Count == 0) return;

	//	Hashtable ht = result[0] as Hashtable;

	//	if (ht is not null)
	//	{
	//		this.PopulateSelf(ht, MethodType.PropertyOnly);
	//	}

	//}
	public static Task<ArrayList> MultiCreateAsync<T>(ArrayList records, bool withEmpty = false) where T : HpBaseModelTransport
	{
		ArrayList hts = records.Select((HpBaseModelTransport v) => v.ComputeHashtable(withEmpty, isNew: true)).ToArrayList();
		var type = typeof(T);
		string hpmodel = HpModelDictionary.TryGetValue(type, out hpmodel) ? hpmodel : null;
		var tempId = OClient.CreateAsync(hpmodel, hts);
		return tempId;
	}

	public async virtual Task<bool> WriteChangedValuesAsync(params string[] fieldNamesToWrite)
	{
		Hashtable ht = [];
		Type type = GetType();

		foreach (string fieldName in fieldNamesToWrite)
		{
			PropertyInfo field = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
			ht.Add(fieldName, field.GetValue(this));
			//if ( HashedValues.TryGetValue( fieldName, out object value ) )
			//            {                
			//                object val = field.GetValue( this );
			//                if ( value != val )
			//                {
			//                    ht.Add( fieldName, val );
			//                }
			//            }
		}

		return await OClient.UpdateAsync(HpModel, Id ?? 0, ht);
	}

	protected ArrayList ComputeArrayList(bool includeEmpty, in string[]? excludedFieldNames = null)
	{
		ArrayList al = [];
		Hashtable ht = ComputeHashtable(includeEmpty, in excludedFieldNames);
		foreach ((string, object) item in ht)
		{
			al.Add((item.Item1, "=", item.Item2));
		}
		return al;
	}
	//public static ArrayList GetFields(Type type, string[]? excludedFieldNames = null, string[]? includedFieldNames = null, string[]? insertFieldNames = null)
	//{
	//	ArrayList fieldNames = [];
	//	PropertyInfo[] fields = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p=>Attribute.IsDefined(p, typeof(OdooPropAttribute)))];

	//	foreach (PropertyInfo field in fields)
	//	{
	//		bool isExcluded = false, isIncluded = true;
	//		if (excludedFieldNames != null) isExcluded = excludedFieldNames.Contains(field.Name);
	//		if (includedFieldNames != null) isIncluded = includedFieldNames.Contains(field.Name);
	//		if (!isExcluded && isIncluded) fieldNames.Add(field.Name);
	//	}
	//	if (insertFieldNames != null)
	//	{
	//		foreach (string field in insertFieldNames)
	//		{
	//			if (!fieldNames.Contains(field))
	//				fieldNames.Add(field);
	//		}
	//	}
	//	return fieldNames;
	//}
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
		{typeof(IHpVersionRelationshipModel), OdooDefaultsConstants.HP_VERSION_RELATIONSHIP},
		{typeof(IHpReleaseModel),             OdooDefaultsConstants.HP_RELEASE},
		{typeof(IHpReleaseVersionRelModel),   OdooDefaultsConstants.HP_RELEASE_VERSION_REL},
		{typeof(IHpTypeModel),                OdooDefaultsConstants.HP_TYPE},
		{typeof(IHpPropertyModel),            OdooDefaultsConstants.HP_PROPERTY},
		{typeof(IHpSettingModel),             OdooDefaultsConstants.HP_SETTINGS},
		{typeof(IIrAttachment),          OdooDefaultsConstants.IR_ATTACHMENT},
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
	public async virtual Task<T?>				GetRecord()
	{
		ArrayList list = ComputeArrayList(false);
		int? recordId = (await OClient.SearchAsync(HpModel, list))?[0] as int?;
		return await GetRecord(recordId);
	}
	public async virtual Task<T?>				GetRecord(int? recordId)
	{
		if (recordId is null or 0) return null;
		Hashtable? ht = (await OClient.ReadAsync(HpModel, [recordId], GetOdooFields()))?.FirstOrDefault<Hashtable>();

		return  ht is null  ?     null   :  RecordPopulation(ht);
	}
	//public virtual ArrayList GetAllFields()
	//{
	//    Type type = GetType();
	//    MethodInfo method = typeof(HpBaseModelTransport<T>).GetMethod("GetFields");
	//    MethodInfo genericMethod = method.MakeGenericMethod(type);
	//    return (ArrayList)genericMethod.Invoke(this, parameters: [null, null]);
	//}
	public virtual T				GetThisRecordsField<T2>(string fieldName) => GetThisRecordsField<T>(fieldName, null);
	public virtual T2				GetThisRecordsField<T2>(string fieldName, in string[] excludedFieldNames = null)
	{
		ArrayList list = ComputeArrayList(false, in excludedFieldNames);
		T2 fieldValue = (T2)OClient.Browse(HpModel, list)[0];
		return fieldValue;
	}

	// static methods
	// if includedFieldNames is null then automatically add it if it isn't excluded
	// if excludedFieldNames is null then don't exclude unless includedFieldNames is not null and doesn't contain field name

	// HTTP response blocking methods
	public static T[]?				GetRecordsByIds(ArrayList? recordIds, ArrayList? searchFilters = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null)
	{
		string modelName = GetHpModel();

		ArrayList fields = GetOdooFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		if (searchFilters == null)
		{
			result = OClient.Read(modelName, recordIds, fields, 90000);
		}
		else
		{
			if (recordIds is not null and { Count: > 0 }) searchFilters.Add(new ArrayList { "id", "in", recordIds });
			result = OClient.Browse(modelName, [searchFilters, fields], 90000);
		}

		if (result?.Count == 0) return null;

		//records = RecordsPopulation([.. result.Select<Hashtable, Hashtable>(h=>h)], excludedFields);
		var records = RecordsPopulation(hts: result?.Select<Hashtable, Hashtable>(static h => h), excludedFields);
		
		return [.. records ?? []];
	}
	public static T?				GetRecordById(int recordId, string[] excludedFields = null)
	{
		T[] records = GetRecordsByIds([recordId], excludedFields: excludedFields);
		return records != null && records.Length > 0 ? records[0] : default;
	}
	public static T[]				GetRecordsBySearch(ArrayList searchFilter = null, string[] excludedFields = null, string[] insertFields = null)
	{
		string modelName = GetHpModel();

		ArrayList fields = GetOdooFields(excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		if (searchFilter == null)
		{
			searchFilter = [];
		}

		result = OClient.Browse(modelName, [searchFilter, fields], 10000);


		if (result.Count == 0) return null;

		var records = RecordsPopulation(hts: result?.Select<Hashtable, Hashtable>(static h => h), excludedFields);
		return [.. records];
	}
	public static T[]?				GetAllRecords(string[] excludedFields = null, string[] insertFields = null)
	{
		string modelName = GetHpModel();

		ArrayList fields = GetOdooFields(excludedFieldNames: excludedFields, insertFieldNames: insertFields);

		ArrayList result = OClient.Browse(modelName, [new ArrayList(), fields], 10000);


		if (result?.Count == 0) return null;

		var records = RecordsPopulation(hts: result?.Select<Hashtable, Hashtable>(static h => h), excludedFields);
		return [.. records ?? []];
	}

	internal static T?				RecordPopulation(Hashtable ht, string[]? excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None, Dictionary<string, string>? remapNames = null)
	{
		if (ht is null) return null;

		if (remapNames is not null)
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
		
		T record = HashConverter.ConvertToClass<T>(ht);

		FinalizePopulation(ref record, excludedFields);
		return record;
	}
	internal static T[]?			RecordsPopulation(IEnumerable<Hashtable>? hts, string[]? excludedFields = null, Dictionary<string, string>? remapNames = null)
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
	
	private static Hashtable		ScalpFields(Hashtable ht, HashedValueStoring hashStoreType)
	{
		if (hashStoreType is HashedValueStoring.None) return null;
		bool isExisting = true;
		switch (hashStoreType)
		{
			case HashedValueStoring.ExistingFields:
				{
					isExisting = true;
					break;
				}
			case HashedValueStoring.NonExistingFields:
				{
					isExisting = false;
					break;
				}
		}
		Type type = typeof(T);
		IEnumerable<string> fieldInfo = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(fi => fi.Name);
		// if All then take all                                                                     true            = true
		// if ExistingFields then IsExisting is true so if it does contain the key then             true    ^ !true = true
		// if NotExistingFields then IsExisting is false so if it does not contain the key then     false   ^ !true = false
		Hashtable newHt = ht.TakeWhere(de => hashStoreType == HashedValueStoring.All || (isExisting ^ !fieldInfo.Contains(de.Key)));
		return newHt;
	}
	
	public static T					Default()
	{
		if (typeof(T).IsValueType)
		{
			return default;
		}
		return new T();
	}


	// async
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
	public static async Task<T?>		GetRecordByIdAsync(int recordId, string[] excludedFields = null)
	{
		if (recordId == 0) return default;
		T[] records = await GetRecordsByIdsAsync([recordId], excludedFields: excludedFields);
		return records != null && records!.Length > 0 ? records![0] : default;
	}
	public async static Task<T[]?>	GetRecordsByIdsAsync(ArrayList? recordIds, ArrayList? searchFilters = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null)
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
	public static T Empty => field ??= new();
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

	public static TOther[]			GetRelatedRecordByIds<TOther>(ArrayList recordIds, string relatedFieldName, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null) 
		where TOther : HpBaseModelTransport<TOther>, new()
	{
		string modelName = GetHpModel();

		List<TOther> records = [];
		ArrayList fields = HpBaseModelTransport<TOther>.GetOdooFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		result = OClient.RelatedBrowse(modelName, [recordIds, relatedFieldName, fields], 60000);

		if (result.Count == 0) return null;

		foreach (Hashtable ht in result)
		{
			TOther record = HashConverter.ConvertToClass<TOther>(ht);

			// set record settings
			record.Id = (int)ht["id"];
			//record.HashedValues = ht;
			if (record.HpModel == OdooDefaultsConstants.HP_VERSION && ht.TryGetValue("dir_id", out ArrayList? value))
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

	internal static int[]?			GetRelatedIdsById(ArrayList recordIds, string relatedFieldName)
	{
		string modelName = GetHpModel();

		ArrayList relatedIds;
		relatedIds = OClient.RelatedBrowse(modelName, [recordIds, relatedFieldName, new ArrayList { "id" }], 60000);

		if (relatedIds.Count == 0) return null;

		List<int> ids = [];
		foreach (Hashtable ht in relatedIds)
		{
			ids.Add((int)ht["id"]);
		}
		return [.. ids];
	}

	internal static TOther[]		GetRelatedRecordsBySearch<TOther>(ArrayList searchFilter, string relatedFieldName, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null) 
		where TOther : HpBaseModelTransport<TOther>, new()
	{
		string modelName = GetHpModel();

		List<TOther> records = [];
		ArrayList fields = HpBaseModelTransport<TOther>.GetOdooFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		result = OClient.RelatedSearch(modelName, [searchFilter, relatedFieldName, fields], 60000);


		if (result.Count == 0) return null;

		foreach (Hashtable ht in result)
		{
			TOther record = HashConverter.ConvertToClass<TOther>(ht);

			// set record settings
			record.Id = (int)ht["id"];
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

	public object?					GetFieldValue(int id, string fieldName)
	{
		if (id == 0) return null;

		ArrayList result = OClient.Read(HpModel, [id], [fieldName], 10000);
		Hashtable? ht = result[0] as Hashtable;

		return ht?[fieldName] is ArrayList list ? list[0] : null;
	}
	public void						Refresh()
	{
		Hashtable ht = (Hashtable)OClient.Read(HpModel, [Id], GetOdooFields())?[0];

		if (ht != null)
		{
			HashConverter.AssignToClass(ht, this);

			// set record settings
			// HashedValues = ht;
			if (HpModel == OdooDefaultsConstants.HP_VERSION && ht.TryGetValue("dir_id", out object value))
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
	public static Tval?				GetFieldValue<Tval>(int id, string fieldName) 
		where Tval : class
		=> GetFieldValueAsync<Tval>(id, fieldName).GetAwaiter().GetResult();
	public static Tval?				GetFieldValue<Tval>(int id, string fieldName, Tval? defaultVal = null) 
		where Tval : struct
		=> GetFieldValueAsync<Tval>(id, fieldName, defaultVal).GetAwaiter().GetResult();
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
	internal static void			SortById(T[] arr)
	{
		Array.Sort(arr, CompareIds);
	}
	internal static void			SortReverseById(T[] arr)
	{
		SortById(arr);
		arr.Reverse();
	}
	private static int				CompareIds(T a, T b)
	{
		if (a is null)
		{
			if (b is null) return 0;
			else return -1;
		}
		else
		{
			if (a is null) return 0;
			else
			{
				return a.Id?.CompareTo(b.Id) ?? 0;
			}
		}
	}
	private static ArrayList		SearchParams(ArrayList values, string fieldName)
	{
		ArrayList arr = [];
		foreach (object value in values)
		{
			arr.Add((fieldName, "=", value));
		}
		return arr;
	}
	public static ArrayList			SearchParams(Hashtable ht)
	{
		ArrayList arr = [];
		foreach (DictionaryEntry de in ht)
		{
			arr.Add(new ArrayList() { de.Key, "=", de.Value });
		}
		return arr;
	}
	public static ArrayList			GetAllFields() => GetOdooFields();
	public static ArrayList			GetOdooFields(string[]? excludedFieldNames = null, string[]? includedFieldNames = null, string[]? insertFieldNames = null)
		=> [.. GetFields(excludedFieldNames, includedFieldNames, insertFieldNames)];
	public override string			ToString() => Id.ToString();

	// async methods
	public static async Task<Tval?>		GetFieldValueAsync<Tval>(int id, string fieldName) 
		where Tval : class
	{
		if (id == 0) return default;
		ArrayList result = await OClient.ReadAsync(GetHpModel(), [id], [fieldName], 10000);

		return (result[0] as Hashtable)?[fieldName]
			is ArrayList list
				? (list[0] as Tval)
				: null;
	}
	public static async Task<Tval?>		GetFieldValueAsync<Tval>(int id, string fieldName, Tval? defaultVal = null) 
		where Tval : struct
	{
		if (id == 0) return defaultVal;
		ArrayList result = await OClient.ReadAsync(GetHpModel(), [id], [fieldName], 10000);

		return (result[0] as Hashtable)?[fieldName]
			is ArrayList list
				? (list[0] is Tval val)
					? val : defaultVal
				: defaultVal;
	}
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
			record.Id = (int)ht["id"];
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
			record.Id = (int)ht["id"];
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
	public static async Task<int[]?>	GetRelatedIdsByIdAsync(ArrayList recordIds, string relatedFieldName)
	{
		string modelName = GetHpModel();

		var relatedIds = await OClient.RelatedBrowseAsync(modelName, [recordIds, relatedFieldName, new ArrayList { "id" }], 60000);

		if (relatedIds.Count == 0) return null;

		List<int> ids = [];
		foreach (Hashtable ht in relatedIds)
		{
			if (ht["id"] is int id) ids.Add(id);
		}
		return [.. ids];
	}
	
	public static async Task<object?>	GetFieldValueAsync(int id, string fieldName)
	{
		if (id == 0) return null;

		ArrayList result = await OClient.ReadAsync(GetHpModel(), [id], [fieldName], 10000);
		Hashtable? ht = result[0] as Hashtable;

		return ht?[fieldName] is ArrayList list ? list[0] : null;
	}
	public async Task					RefreshAsync()
	{
		if ((await OClient.ReadAsync(HpModel, [Id], GetOdooFields()))?[0] is Hashtable ht)
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