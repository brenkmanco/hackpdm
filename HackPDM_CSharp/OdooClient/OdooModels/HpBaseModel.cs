using HackPDM.ClientUtils;
using HackPDM.Extensions.General;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public abstract class HpBaseModel
    {
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
        public int ID { get; internal set; }
        // ID of the record in the database
        public string HpModel
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
        public bool IsModifiedRecord
        {
            get
            {
                if (!IsRecord) return false;
                bool wasModified = true;
                if (wasModified) IsRecord = false;
                return wasModified;
            }
        }
        public bool IsRecord { get; internal set; }
        public Hashtable HashedValues { get; internal set; } = [];
        public string[] ExcludedFields { get; internal set; }
        public string[] InsertFields { get; internal set; }
        public virtual int Create() => Create(false);
        public virtual int Create(bool withEmpty = false)
        {
			Hashtable ht = ComputeHashtable(true);
            int tempID = OClient.Create(HpModel, ht, 10000);

            if (tempID != 0)
            {
                ID = tempID;
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
            return tempID;
        }
        public virtual async Task<int> CreateAsync() => await CreateAsync(false);
        public virtual async Task<int> CreateAsync(bool withEmpty = false, string[] excludedFields = null)
        {
            Hashtable ht = ComputeHashtable(withEmpty, excludedFields, isNew:true);
            int tempID = await OClient.CreateAsync(HpModel, ht, 10000);

            if (tempID != 0)
            {
                ID = tempID;
				
                if (HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
                {
                    HashedValues = new Hashtable
                    {
                        { "dir_id", value }
                    };
                }
                IsRecord = true;
            }
            return tempID;
        }
        private void PopSelf(string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null)
        {
            Type type = GetType();
            string modelName = HpModelDictionary[type];

            ArrayList fields = GetFields(type, includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
            ArrayList result;

            result = OClient.Read(modelName, [ID], fields, 90000);
            
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
            ArrayList tempID = await OClient.CreateAsync(hpmodel, hts);
            return tempID;
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
                ht.Add("id", ID);

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

			return await OClient.UpdateAsync( HpModel, ID, ht );
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
            bool wasWritten = OClient.Update(HpModel, ID, ht);
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
    public abstract class HpBaseModel<T> : HpBaseModel where T : HpBaseModel, new()
    {
        public virtual T GetRecord()
        {
            ArrayList list = ComputeArrayList(false);
            int recordID = (int)OClient.Search(HpModel, list)[0];
            return GetRecord(recordID);
        }
        public virtual T GetRecord(int recordID)
        {
            Hashtable ht = (Hashtable)OClient.Read(HpModel, [recordID], GetFields())[0];
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


        internal static T GetRecordByID(int recordID, string[] excludedFields = null)
        {
            T[] records = GetRecordsByIDS([recordID], excludedFields: excludedFields);
            return records != null && records.Length > 0 ? records[0] : default;
        }
        internal static Tother[] GetRelatedRecordByIDS<Tother>(ArrayList recordIDS, string relatedFieldName, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null) where Tother : HpBaseModel<Tother>, new()
        {
            string modelName = HpModelDictionary[typeof(T)];

            List<Tother> records = [];
            ArrayList fields = HpBaseModel<Tother>.GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
            ArrayList result;

            result = OClient.RelatedBrowse(modelName, [recordIDS, relatedFieldName, fields], 60000);
            

            if (result.Count == 0) return null;

            foreach (Hashtable ht in result)
            {
                Tother record = HashConverter.ConvertToClass<Tother>(ht);

                // set record settings
                record.ID = (int)ht["id"];
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
        internal static int[]? GetRelatedIdsById(ArrayList recordIDS, string relatedFieldName)
        {
            string modelName = HpModelDictionary[typeof(T)];

            ArrayList relatedIds;
            relatedIds = OClient.RelatedBrowse(modelName, [recordIDS, relatedFieldName, new ArrayList {"id"}], 60000);

            if (relatedIds.Count == 0) return null;

            List<int> ids = [];
            foreach (Hashtable ht in relatedIds)
            {
                ids.Add((int)ht["id"]);
            }
            return [.. ids];
        }
        internal static T[] GetRecordsByIDS(ArrayList recordIDS, ArrayList searchFilters = null, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null)
        {
            string modelName = HpModelDictionary[typeof(T)];

            List<T> records = [];
            ArrayList fields = GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
            ArrayList result;

            if (searchFilters == null)
            {
                result = OClient.Read(modelName, recordIDS, fields, 90000);
            }
            else
            {
                if (recordIDS is not null and { Count: > 0 }) searchFilters.Add(new ArrayList { "id", "in", recordIDS });
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
        internal async static Task<T[]> GetRecordsByIDSAsync(ArrayList recordIDS, ArrayList searchFilters = null, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null)
        {
            string modelName = HpModelDictionary[typeof(T)];

            List<T> records = [];
            ArrayList fields = GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
            ArrayList result;

            if (searchFilters == null)
            {
                result = await OClient.ReadAsync(modelName, recordIDS, fields, 90000);
            }
            else
            {
                if (recordIDS is not null and { Count: > 0 }) searchFilters.Add(new ArrayList { "id", "in", recordIDS });
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

        internal static T RecordPopulation(Hashtable ht, string[] excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None, Dictionary<string, string> RemapNames = null)
        {
            if (ht is null) return null;

            if (RemapNames is not null)
            {
                foreach (DictionaryEntry pair in ht)
                {
                    if (RemapNames.TryGetValue(pair.Key.ToString(), out string newName))
                    {
                        DictionaryEntry de = new(newName, pair.Value);
                        ht[pair.Key.ToString()] = de;
                    }
                }
            }
            T record = HashConverter.ConvertToClass<T>(ht);
            
            FinalizePopulation(record, ht, excludedFields, hashStoreType);
            return record;
        }
        internal static T[] RecordsPopulation(Hashtable[] hts, string[] excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None, Dictionary<string, string> RemapNames = null)
        {
            if (hts is null) return null;

            if (RemapNames is not null)
            {
                foreach(Hashtable ht in hts)
                {
                    foreach (DictionaryEntry pair in ht)
                    {
                        if (RemapNames.TryGetValue(pair.Key.ToString(), out string newName))
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

        public static void FinalizePopulation(T record, Hashtable ht, string[] excludedFields = null, HashedValueStoring hashStoreType = HashedValueStoring.None)
        {
            // set record settings
            record.ID = (int)ht["id"];

            record.IsRecord = true;
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
            if (record.HpModel == OdooDefaults.HP_VERSION
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
                FinalizePopulation(records[i], hts[i], excludedFields, hashStoreType);
            }
        }
        private static Hashtable ScalpFields(Hashtable ht, HashedValueStoring hashStoreType)
        {
            if (hashStoreType is HashedValueStoring.None) return null;
            bool IsExisting = true;
            switch (hashStoreType)
            {
                case HashedValueStoring.ExistingFields:
                {
                    IsExisting = true;
                    break;
                }
                case HashedValueStoring.NonExistingFields:
                {
                    IsExisting = false;
                    break;
                }
            }
            Type type = typeof(T);
            IEnumerable<string> fieldInfo = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(fi => fi.Name);
            // if All then take all                                                                     true            = true
            // if ExistingFields then IsExisting is true so if it does contain the key then             true    ^ !true = true
            // if NotExistingFields then IsExisting is false so if it does not contain the key then     false   ^ !true = false
            Hashtable newHT = ht.TakeWhere(de => hashStoreType == HashedValueStoring.All || (IsExisting ^ !fieldInfo.Contains(de.Key)));
            return newHT;
        }
        internal static Tother[] GetRelatedRecordsBySearch<Tother>(ArrayList searchFilter, string relatedFieldName, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null) where Tother : HpBaseModel<Tother>, new()
        {
            string modelName = HpModelDictionary[typeof(T)];

            List<Tother> records = [];
            ArrayList fields = HpBaseModel<Tother>.GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
            ArrayList result;

            result = OClient.RelatedSearch(modelName, [searchFilter, relatedFieldName, fields], 60000);


            if (result.Count == 0) return null;

            foreach (Hashtable ht in result)
            {
                Tother record = HashConverter.ConvertToClass<Tother>(ht);

                // set record settings
                record.ID = (int)ht["id"];
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
                    return a.ID.CompareTo(b.ID);
                }
            }
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

        public static object GetFieldValue(int ID, string fieldName)
        {
            if (ID == 0) return null;

            ArrayList result = OClient.Read(GetHpModel(), [ID], [fieldName], 10000);
            Hashtable ht = (Hashtable)result[0];

            if (ht[fieldName] is ArrayList list) return list[0];
            else return null;
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
        public static ArrayList GetFields(string[] excludedFieldNames = null, string[] includedFieldNames = null, string[] insertFieldNames = null)
            => GetFields(typeof(T), excludedFieldNames, includedFieldNames, insertFieldNames);

        
        
        public void Refresh()
        {
            Hashtable ht = (Hashtable)OClient.Read(HpModel, [ID], GetFields())?[0];

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
        // getter setter
        internal static void SetHpModel(string value)
            => HpModelDictionary[typeof(T)] = value;
        internal static string GetHpModel()
            => HpModelDictionary.TryGetValue(typeof(T), out string value) ? value : null;
        public override string ToString()
        {
            return ID.ToString();
        }
    }
}