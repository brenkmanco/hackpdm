using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HackPDM.ClientUtils;
using HackPDM.Extensions.General;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Src.ClientUtils.Types;

using SolidWorks.Interop.swdocumentmgr;

using OClient = HackPDM.Odoo.OdooClient;

namespace HackPDM.Odoo.OdooModels;

public abstract partial class HpBaseModel
{
	// (MVVM) VIEW
	internal static string[] UsualExcludedFields { get; set; } = [];
    internal static string[] UsualIncludedFields { get; set; } = [];
    
    protected static readonly Dictionary<Type, string> HpModelDictionary = new()
    {
        {typeof(HpNode), OdooDefaults.HP_NODE},
        {typeof(HpEntry), OdooDefaults.HP_ENTRY},
        {typeof(HpEntryNameFilter), OdooDefaults.HP_ENTRY_NAME_FILTER},
        {typeof(HpDirectory), OdooDefaults.HP_DIRECTORY},
        {typeof(HpCategory), OdooDefaults.HP_CATEGORY},
        {typeof(HpCategoryProperty), OdooDefaults.HP_CATEGORY_PROPERTY},
        {typeof(HpVersion), OdooDefaults.HP_VERSION},
        {typeof(HpVersionProperty), OdooDefaults.HP_VERSION_PROPERTY},
        {typeof(HpVersionRelationship), OdooDefaults.HP_VERSION_RELATIONSHIP},
        {typeof(HpRelease), OdooDefaults.HP_RELEASE},
        {typeof(HpReleaseVersionRel), OdooDefaults.HP_RELEASE_VERSION_REL},
        {typeof(HpType), OdooDefaults.HP_TYPE},
        {typeof(HpProperty), OdooDefaults.HP_PROPERTY},
        {typeof(HpSetting), OdooDefaults.HP_SETTINGS},
        {typeof(IrAttachment), OdooDefaults.IR_ATTACHMENT},
        {typeof(HpUser), OdooDefaults.RES_USERS},
    };
    public int Id { get; internal set; }
    // ID of the record in the database
    public virtual string HpModel
    {
        get
        {
            var type = GetType();
            return HpModelDictionary.TryGetValue(type, out string value) ? value : null;
        }
        internal set
        {
            var type = GetType();
            HpModelDictionary[type] = value;
        }
    }
    //public readonly Hashtable EmptyHashtable = new Hashtable();
    // public bool IsModifiedRecord
    // {
    //     get
    //     {
    //         if (!IsRecord) return false;
    //         bool wasModified = true;
    //         if (wasModified) IsRecord = false;
    //         return wasModified;
    //     }
    // }
    public bool IsRecord { get; internal set; }
    public Hashtable HashedValues { get; internal set; } = [];
    public string[] ExcludedFields { get; internal set; }
    public string[] InsertFields { get; internal set; }
}
public abstract partial class HpBaseModel
{
	// (MVVM) VIEWMODEL
    public virtual int Create() => Create(false);
    public virtual int Create(bool withEmpty = false)
    {
        Hashtable ht = ComputeHashtable(true);
        int tempId = OClient.Create(HpModel, ht, 10000);

        if (tempId != 0)
        {
            Id = tempId;
            //HashedValues = ht;
            if (HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
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
    public virtual async Task<int> CreateAsync() => await CreateAsync(false);
    public virtual async Task<int> CreateAsync(bool withEmpty = false, string[] excludedFields = null)
    {
        Hashtable ht = ComputeHashtable(withEmpty, excludedFields, isNew:true);
        int tempId = await OClient.CreateAsync(HpModel, ht, 10000);

        if (tempId != 0)
        {
            Id = tempId;
				
            if (HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
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
    private void PopSelf(string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null)
    {
        Type type = GetType();
        string modelName = HpModelDictionary[type];

        ArrayList fields = GetFields(type, includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
        ArrayList result;

        result = OClient.Read(modelName, [Id], fields, 90000);
            
        if (result.Count == 0) return;

        Hashtable ht = result[0] as Hashtable;
            
        if (ht is not null)
        {
            this.PopulateSelf(ht, MethodType.FieldOnly);
                
        }
            
    }
    public static async Task<ArrayList> MultiCreateAsync<T>(ArrayList records, bool withEmpty = false) where T : HpBaseModel
    {
        ArrayList hts = records.Select((HpBaseModel v) => v.ComputeHashtable(withEmpty, isNew: true)).ToArrayList();
        var type = typeof(T);
        string hpmodel = HpModelDictionary.TryGetValue(type, out hpmodel) ? hpmodel : null;
        ArrayList tempId = await OClient.CreateAsync(hpmodel, hts);
        return tempId;
    }

    protected Hashtable ComputeHashtable(bool includeEmpty = true, in string[] excludedFieldNames = null, bool isNew = false)
    {
        Hashtable ht = [];
        Type type = GetType();
        List<string> excludeFields = [];
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (FieldInfo field in fields)
        {
            if (excludedFieldNames != null && excludedFieldNames.Contains(field.Name))
            {
                excludeFields.Add(field.Name);
                continue;
            }
            if (!includeEmpty)
            {
                Type fType = field.FieldType;
                bool valueType = fType.IsValueType;

                object fVal = field.GetValue(this);
                if (valueType && Activator.CreateInstance(fType) == fVal) continue;
                else if (!valueType && fVal == null) continue;
            }

            string fieldName = field.Name;
            object fieldValue = field.GetValue(this);

            if (isNew && fieldValue is DateTime dt)
            {
                string date = OdooDefaults.ConvertToOdooFormat(dt);
                fieldValue = date;
            }
            ht.Add(fieldName, fieldValue);
        }
        if (excludeFields.Count > 0 ) ExcludedFields = [.. excludeFields];

        if (!isNew)
            ht.Add("id", Id);

        return ht;
    }
    public virtual bool WriteAll()
    {
        Type type = GetType();

        WriteInternal(type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        return true;
    }
    public virtual bool Write(params string[] fieldNamesToWrite)
    {
        //List<string> fields = [];
        //foreach (string fieldName in fieldNamesToWrite)
        //{
        //    FieldInfo field = GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        //    fields.Add(fieldName);
        //    //if (HashedValues.ContainsKey(fieldName) && HashedValues[fieldName] != field.GetValue(this))
        //    //{
        //    //    fields.Add(fieldName);
        //    //}
        //}

        Hashtable ht = [];
        foreach (string field in fieldNamesToWrite)
        {
            FieldInfo fieldInfo = GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            ht.Add(field, fieldInfo.GetValue(this));
        }
        return WriteInternal(ht);
    }
    public async virtual Task<bool> WriteChangedValuesAsync(params string[] fieldNamesToWrite)
    {
        Hashtable ht = [];
        Type type = GetType();

        foreach ( string fieldName in fieldNamesToWrite )
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            ht.Add(fieldName, field.GetValue( this ));
            //if ( HashedValues.TryGetValue( fieldName, out object value ) )
            //            {                
            //                object val = field.GetValue( this );
            //                if ( value != val )
            //                {
            //                    ht.Add( fieldName, val );
            //                }
            //            }
        }

        return await OClient.UpdateAsync( HpModel, Id, ht );
    }
        

    /// <summary>
    /// To compute any remaining fields that are based off of other field initializations
    /// </summary>
    internal virtual void CompleteConstruction() { }
    //private bool VerifyModified()
    //{
    //    if (HashedValues == null || ExcludedFields == null) return false;
    //    Type type = GetType();
    //    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    //    foreach (FieldInfo field in fields)
    //    {
    //        string fieldName = field.Name;
    //        object fieldValue = field.GetValue(this);

    //        if (ExcludedFields.Contains(fieldName)) continue;
    //        if (HashedValues.ContainsKey(fieldName) && HashedValues[fieldName] != null && HashedValues[fieldName] != fieldValue)
    //        {
    //            return true;
    //        }
    //    }

    //    return false;
    //}
    private bool WriteInternal(Hashtable ht)
    {
        bool wasWritten = OClient.Update(HpModel, Id, ht);
        if (wasWritten)
        {
            //Refresh();
            Console.WriteLine("record was modified");
        }
        else
        {
            Console.WriteLine("record wasn't modified");
        }
        return wasWritten;
    }
    protected bool WriteInternal(params FieldInfo[] fields)
    {
        Hashtable ht = [];
        foreach ( FieldInfo field in fields )
        {
            ht.Add( field.Name, field.GetValue( this ) );
        }
        return WriteInternal( ht );
    }


    protected ArrayList ComputeArrayList(bool includeEmpty, in string[] excludedFieldNames = null)
    {
        ArrayList al = [];
        Hashtable ht = ComputeHashtable(includeEmpty, in excludedFieldNames);
        foreach ((string, object) item in ht)
        {
            al.Add((item.Item1, "=", item.Item2));
        }

        return al;
    }
    public static ArrayList GetFields(Type type, string[] excludedFieldNames = null, string[] includedFieldNames = null, string[] insertFieldNames = null)
    {
        ArrayList fieldNames = [];
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (FieldInfo field in fields)
        {
            bool isExcluded = false, isIncluded = true;
            if (excludedFieldNames != null) isExcluded = excludedFieldNames.Contains(field.Name);
            if (includedFieldNames != null) isIncluded = includedFieldNames.Contains(field.Name);
            if (!isExcluded && isIncluded) fieldNames.Add(field.Name);
        }
        if (insertFieldNames != null)
        {
            foreach (string field in insertFieldNames)
            {
                if (!fieldNames.Contains(field))
                    fieldNames.Add(field);
            }
        }
        return fieldNames;
    }
}
public partial class HpBaseModel<T> : HpBaseModel where T : HpBaseModel, new()
{
    public virtual T GetRecord()
    {
        ArrayList list = ComputeArrayList(false);
        int recordId = (int)OClient.Search(HpModel, list)[0];
        return GetRecord(recordId);
    }
    public virtual T GetRecord(int recordId)
    {
        Hashtable ht = OClient.Read(HpModel, [recordId], GetFields())[0] as Hashtable;
        return RecordPopulation(ht);
    }
    //public virtual ArrayList GetAllFields()
    //{
    //    Type type = GetType();
    //    MethodInfo method = typeof(HpBaseModel<T>).GetMethod("GetFields");
    //    MethodInfo genericMethod = method.MakeGenericMethod(type);
    //    return (ArrayList)genericMethod.Invoke(this, parameters: [null, null]);
    //}
    public virtual T GetThisRecordsField<T2>(string fieldName) => GetThisRecordsField<T>(fieldName, null);
    public virtual T2 GetThisRecordsField<T2>(string fieldName, in string[] excludedFieldNames = null)
    {
        ArrayList list = ComputeArrayList(false, in excludedFieldNames);
        T2 fieldValue = (T2)OClient.Browse(HpModel, list)[0];
        return fieldValue;
    }

    // static methods
    // if includedFieldNames is null then automatically add it if it isn't excluded
    // if excludedFieldNames is null then don't exclude unless includedFieldNames is not null and doesn't contain field name
    
    // HTTP response blocking methods
    internal static T[] GetRecordsByIds(ArrayList recordIds, ArrayList searchFilters = null, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null)
    {
        string modelName = HpModelDictionary[typeof(T)];

        List<T> records = [];
        ArrayList fields = GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
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

        if (result.Count == 0) return null;

        //records = RecordsPopulation([.. result.Select<Hashtable, Hashtable>(h=>h)], excludedFields);
        foreach (Hashtable ht in result)
        {
            records.Add(RecordPopulation(ht, excludedFields));
        }
        //return records;
        
        return [.. records];
    }
    internal static T GetRecordById(int recordId, string[] excludedFields = null)
    {
        T[] records = GetRecordsByIds([recordId], excludedFields: excludedFields);
        return records != null && records.Length > 0 ? records[0] : default;
    }
    internal static TOther[] GetRelatedRecordByIds<TOther>(ArrayList recordIds, string relatedFieldName, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null) where TOther : HpBaseModel<TOther>, new()
    {
        string modelName = HpModelDictionary[typeof(T)];

        List<TOther> records = [];
        ArrayList fields = HpBaseModel<TOther>.GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
        ArrayList result;

        result = OClient.RelatedBrowse(modelName, [recordIds, relatedFieldName, fields], 60000);
            

        if (result.Count == 0) return null;

        foreach (Hashtable ht in result)
        {
            TOther record = HashConverter.ConvertToClass<TOther>(ht);

            // set record settings
            record.Id = (int)ht["id"];
            //record.HashedValues = ht;
            if (record.HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out ArrayList? value)) 
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
    internal static int[]? GetRelatedIdsById(ArrayList recordIds, string relatedFieldName)
    {
        string modelName = HpModelDictionary[typeof(T)];

        ArrayList relatedIds;
        relatedIds = OClient.RelatedBrowse(modelName, [recordIds, relatedFieldName, new ArrayList {"id"}], 60000);

        if (relatedIds.Count == 0) return null;

        List<int> ids = [];
        foreach (Hashtable ht in relatedIds)
        {
            ids.Add((int)ht["id"]);
        }
        return [.. ids];
    }
    internal static TOther[] GetRelatedRecordsBySearch<TOther>(ArrayList searchFilter, string relatedFieldName, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null) where TOther : HpBaseModel<TOther>, new()
    {
        string modelName = HpModelDictionary[typeof(T)];

        List<TOther> records = [];
        ArrayList fields = HpBaseModel<TOther>.GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
        ArrayList result;

        result = OClient.RelatedSearch(modelName, [searchFilter, relatedFieldName, fields], 60000);


        if (result.Count == 0) return null;

        foreach (Hashtable ht in result)
        {
            TOther record = HashConverter.ConvertToClass<TOther>(ht);

            // set record settings
            record.Id = (int)ht["id"];
            //record.HashedValues = ht;
            if (record.HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value))
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
    internal static T[] GetRecordsBySearch(ArrayList searchFilter = null, string[] excludedFields = null, string[] insertFields = null)
    {
        string modelName = HpModelDictionary[typeof(T)];

        List<T> records = [];
        ArrayList fields = GetFields(excludedFieldNames: excludedFields, insertFieldNames: insertFields);
        ArrayList result;

        if (searchFilter == null)
        {
            searchFilter = [];
        }

        result = OClient.Browse(modelName, [searchFilter, fields], 10000);
            

        if (result.Count == 0) return null;

        foreach (Hashtable ht in result)
        {
            records.Add(RecordPopulation(ht, excludedFields));
        }
        return [.. records];
    }
    internal static T[] GetAllRecords(string[] excludedFields = null, string[] insertFields = null)
    {
        string modelName = HpModelDictionary[typeof(T)];

        List<T> records = [];
        ArrayList fields = GetFields(excludedFieldNames: excludedFields, insertFieldNames: insertFields);
            
        ArrayList result = OClient.Browse(modelName, [new ArrayList(), fields], 10000);
            

        if (result.Count == 0) return null;

        foreach (Hashtable ht in result)
        {
            records.Add(RecordPopulation(ht, excludedFields));
        }
        return [.. records];
    }
    public static object? GetFieldValue(int id, string fieldName)
    {
        if (id == 0) return null;

        ArrayList result = OClient.Read(GetHpModel(), [id], [fieldName], 10000);
        Hashtable? ht = result[0] as Hashtable;

		return ht?[fieldName] is ArrayList list ? list[0] : null;
	}
    public void Refresh()
    {
        Hashtable ht = (Hashtable)OClient.Read(HpModel, [Id], GetFields())?[0];

        if (ht != null)
        {
            HashConverter.AssignToClass(ht, this);

            // set record settings
            // HashedValues = ht;
            if (HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
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
    public static Tval? GetFieldValue<Tval>(int id, string fieldName) where Tval : class
        => GetFieldValueAsync<Tval>(id, fieldName).GetAwaiter().GetResult();
	public static Tval? GetFieldValue<Tval>(int id, string fieldName, Tval? defaultVal = null) where Tval : struct
        => GetFieldValueAsync<Tval>(id, fieldName, defaultVal).GetAwaiter().GetResult();


    internal static T? RecordPopulation(Hashtable ht, string[] excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None, Dictionary<string, string> remapNames = null)
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
            
        FinalizePopulation(ref record, ht, excludedFields, hashStoreType);
        return record;
    }
    internal static T[] RecordsPopulation(Hashtable[] hts, string[] excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None, Dictionary<string, string> remapNames = null)
    {
        if (hts is null) return null;

        if (remapNames is not null)
        {
            foreach(Hashtable ht in hts)
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
        T[] records = HashConverter.ConvertToClasses<T>(hts);

        FinalizePopulations(records, hts, excludedFields, hashStoreType);
        return records;
    }

    public static void FinalizePopulation(ref T record, Hashtable ht, string[] excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None)
    {
        // set record settings
        record.Id = (int)ht["id"];

        record.IsRecord = (bool)true;
        record.ExcludedFields = excludedFields;

        //record.HashedValues = [];
        switch (hashStoreType)
        {
            case HashedValueStoring.None: break;

            case HashedValueStoring.ExistingFields:
            case HashedValueStoring.NonExistingFields:
            {
                record.HashedValues = ScalpFields(ht, hashStoreType);
                break;
            }

            case HashedValueStoring.All:
            {
                record.HashedValues = ht;
                break;
            }
        }
        if ((record.HpModel is OdooDefaults.HP_VERSION or OdooDefaults.HP_ENTRY)
            && ht.TryGetValue("dir_id", out object value))
        {
            record.HashedValues.Add("dir_id", value);
        }


        record.CompleteConstruction();
    }
    public static void FinalizePopulations(T[] records, Hashtable[] hts, string[] excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None)
    {
        if (records.Length != hts.Length) return;
        for (int i = 0; i < records.Length; i++)
        {
            FinalizePopulation(ref records[i], hts[i], excludedFields, hashStoreType);
        }
    }
    private static Hashtable ScalpFields(Hashtable ht, HashedValueStoring hashStoreType)
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
        IEnumerable<string> fieldInfo = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(fi => fi.Name);
        // if All then take all                                                                     true            = true
        // if ExistingFields then IsExisting is true so if it does contain the key then             true    ^ !true = true
        // if NotExistingFields then IsExisting is false so if it does not contain the key then     false   ^ !true = false
        Hashtable newHt = ht.TakeWhere(de => hashStoreType == HashedValueStoring.All || (isExisting ^ !fieldInfo.Contains(de.Key)));
        return newHt;
    }
    internal static void SortById(T[] arr)
    {
        Array.Sort(arr, CompareIds);
    }
    internal static void SortReverseById(T[] arr)
    {
        SortById(arr);
        arr.Reverse();
    }
    private static int CompareIds(T a, T b)
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
                return a.Id.CompareTo(b.Id);
            }
        }
    }

    
    

	private static ArrayList SearchParams(ArrayList values, string fieldName)
    {
        ArrayList arr = [];
        foreach (object value in values)
        {
            arr.Add((fieldName, "=", value));
        }
        return arr;
    }
    public static ArrayList SearchParams(Hashtable ht)
    {
        ArrayList arr = [];
        foreach (DictionaryEntry de in ht)
        {
            arr.Add(new ArrayList() { de.Key, "=", de.Value });
        }
        return arr;
    }

    internal static T Default()
    {
        if ( typeof( T ).IsValueType )
        {
            return default;
        }
        return new T();
    }
        
    public static ArrayList GetAllFields() => GetFields();
    public static ArrayList GetFields(string[]? excludedFieldNames = null, string[]? includedFieldNames = null, string[]? insertFieldNames = null)
        => GetFields(typeof(T), excludedFieldNames, includedFieldNames, insertFieldNames);

        
        
    // getter setter
    internal static void SetHpModel(string value)
        => HpModelDictionary[typeof(T)] = value;
    internal static string? GetHpModel()
        => HpModelDictionary.TryGetValue(typeof(T), out string? value) ? value : null;
    public override string ToString()
    {
        return Id.ToString();
    }
}
public partial class HpBaseModel<T> : HpBaseModel where T : HpBaseModel, new()
{	
    // async methods
	internal async static Task<T[]?> GetRecordsByIdsAsync(ArrayList recordIds, ArrayList? searchFilters = null, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null)
	{
		string modelName = HpModelDictionary[typeof(T)];

		List<T> records = [];
		ArrayList fields = GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		if (searchFilters == null)
		{
			result = await OClient.ReadAsync(modelName, recordIds, fields, 90000);
		}
		else
		{
			if (recordIds is not null and { Count: > 0 }) searchFilters.Add(new ArrayList { "id", "in", recordIds });
			result = await OClient.BrowseAsync(modelName, [searchFilters, fields], 90000);
		}

		if (result.Count == 0) return null;

		//records = RecordsPopulation([.. result.Select<Hashtable, Hashtable>(h=>h)], excludedFields);
		foreach (Hashtable ht in result)
		{
			records.Add(RecordPopulation(ht, excludedFields));
		}
		//return records;
		return [.. records];
	}
	public static async Task<Tval?> GetFieldValueAsync<Tval>(int id, string fieldName) where Tval : class
	{
		if (id == 0) return default;
		ArrayList result = await OClient.ReadAsync(GetHpModel(), [id], [fieldName], 10000);

		return (result[0] as Hashtable)?[fieldName]
			is ArrayList list
				? (list[0] as Tval)
				: null;
	}
	public static async Task<Tval?> GetFieldValueAsync<Tval>(int id, string fieldName, Tval? defaultVal = null) where Tval : struct
	{
		if (id == 0) return defaultVal;
		ArrayList result = await OClient.ReadAsync(GetHpModel(), [id], [fieldName], 10000);

		return (result[0] as Hashtable)?[fieldName]
			is ArrayList list
				? (list[0] is Tval val)
					? val : defaultVal
				: defaultVal;
	}

	public static async Task<T?> GetRecordByIdAsync(int recordId, string[] excludedFields = null)
	{
		if (recordId == 0) return null;
		T[] records = await GetRecordsByIdsAsync([recordId], excludedFields: excludedFields);
		return records != null && records!.Length > 0 ? records![0] : null;
	}
	public static async Task<TOther[]?> GetRelatedRecordByIdsAsync<TOther>(ArrayList recordIds, string relatedFieldName, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null) where TOther : HpBaseModel<TOther>, new()
	{
		string modelName = HpModelDictionary[typeof(T)];

		List<TOther> records = [];
		ArrayList fields = HpBaseModel<TOther>.GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		result = await OClient.RelatedBrowseAsync(modelName, [recordIds, relatedFieldName, fields], 60000);

		if (result.Count == 0) return null;

		foreach (Hashtable ht in result)
		{
			TOther record = HashConverter.ConvertToClass<TOther>(ht);

			// set record settings
			record.Id = (int)ht["id"];
			//record.HashedValues = ht;
			if (record.HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value))
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
	public static async Task<int[]?> GetRelatedIdsByIdAsync(ArrayList recordIds, string relatedFieldName)
	{
		string modelName = HpModelDictionary[typeof(T)];

		ArrayList relatedIds;
		relatedIds = await OClient.RelatedBrowseAsync(modelName, [recordIds, relatedFieldName, new ArrayList { "id" }], 60000);

		if (relatedIds.Count == 0) return null;

		List<int> ids = [];
		foreach (Hashtable ht in relatedIds)
		{
            if (ht["id"] is int id) ids.Add(id);
		}
		return [.. ids];
	}
	public static async Task<TOther[]?> GetRelatedRecordsBySearchAsync<TOther>(ArrayList searchFilter, string relatedFieldName, string[]? excludedFields = null, string[]? includedFields = null, string[]? insertFields = null) where TOther : HpBaseModel<TOther>, new()
	{
		string modelName = HpModelDictionary[typeof(T)];

		List<TOther> records = [];
		ArrayList fields = HpBaseModel<TOther>.GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		result = await OClient.RelatedSearchAsync(modelName, [searchFilter, relatedFieldName, fields], 60000);

		if (result.Count == 0) return null;

		foreach (Hashtable ht in result)
		{
			TOther record = HashConverter.ConvertToClass<TOther>(ht);

			// set record settings
			record.Id = (int)ht["id"];
			//record.HashedValues = ht;
			if (record.HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out int value))
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
	public static async Task<T[]?> GetRecordsBySearchAsync(ArrayList? searchFilter = null, string[]? excludedFields = null, string[]? insertFields = null)
	{
		string modelName = HpModelDictionary[typeof(T)];

		List<T> records = [];
		ArrayList fields = GetFields(excludedFieldNames: excludedFields, insertFieldNames: insertFields);
		ArrayList result;

		if (searchFilter == null)
		{
			searchFilter = [];
		}

		result = await OClient.BrowseAsync(modelName, [searchFilter, fields], 10000);


		if (result.Count == 0) return null;

		foreach (Hashtable ht in result)
		{
			records.Add(RecordPopulation(ht, excludedFields));
		}
		return [.. records];
	}
	public static async Task<T[]?> GetAllRecordsAsync(string[]? excludedFields = null, string[]? insertFields = null)
	{
		string modelName = HpModelDictionary[typeof(T)];

		List<T> records = [];
		ArrayList fields = GetFields(excludedFieldNames: excludedFields, insertFieldNames: insertFields);

		ArrayList result = await OClient.BrowseAsync(modelName, [new ArrayList(), fields], 10000);


		if (result.Count == 0) return null;

		foreach (Hashtable ht in result)
		{
			records.Add(RecordPopulation(ht, excludedFields));
		}
		return [.. records];
	}
	public static async Task<object?> GetFieldValueAsync(int id, string fieldName)
	{
		if (id == 0) return null;

		ArrayList result = await OClient.ReadAsync(GetHpModel(), [id], [fieldName], 10000);
		Hashtable? ht = result[0] as Hashtable;

		return ht?[fieldName] is ArrayList list ? list[0] : null;
	}
	public async Task RefreshAsync()
	{
		if ((await OClient.ReadAsync(HpModel, [Id], GetFields()))?[0] is Hashtable ht)
		{
			HashConverter.AssignToClass(ht, this);

			// set record settings
			// HashedValues = ht;
			if (HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out int value))
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
public partial class HpBaseModel<T> : HpBaseModel where T : HpBaseModel, new()
{
	public static T Empty => field ??= new();
	public override string HpModel 
	{ 
		get => GetHpModel(); 
		internal set => HpModelDictionary[typeof(T)] = value; 
	}
	public HpBaseModel() { }
}