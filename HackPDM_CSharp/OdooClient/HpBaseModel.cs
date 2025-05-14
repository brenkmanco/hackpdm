using HackPDM.ClientUtils;
using OdooRpcCs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static Npgsql.PostgresTypes.PostgresCompositeType;
using OClient = OdooRpcCs.OdooClient;

namespace HackPDM
{
    public abstract class HpBaseModel
    {
        internal static string[] UsualExcludedFields { get; set; } = [];

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
        public readonly static Hashtable EmptyHashtable = new Hashtable();
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
        public Hashtable HashedValues { get; internal set; } = EmptyHashtable;
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
                    HashedValues = new Hashtable();
                    HashedValues.Add("dir_id", value);
                }
                IsRecord = true;
            }
            return tempID;
        }
        public virtual async Task<int> CreateAsync() => await CreateAsync(false);
        public virtual async Task<int> CreateAsync(bool withEmpty = false)
        {
            Hashtable ht = ComputeHashtable(withEmpty, isNew:true);
            int tempID = await OClient.CreateAsync(HpModel, ht, 10000);

            if (tempID != 0)
            {
                ID = tempID;
				//HashedValues = ht;
                
                if (HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
                {
                    HashedValues = new Hashtable();
                    HashedValues.Add("dir_id", value);
                }
                IsRecord = true;
            }
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
            if (excludeFields.Count > 0 ) ExcludedFields = excludeFields.ToArray();

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
            T model = HashConverter.ConvertToClass<T>(ht);

            if (ht != null)
            {
                model.ID = recordID;
                //model.HashedValues = ht;
                if (HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
                {
                    HashedValues = new Hashtable();
                    HashedValues.Add("dir_id", value);
                }
                model.IsRecord = true;
                model.CompleteConstruction();
            }

            return model;
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
                    record.HashedValues = new Hashtable();
                    record.HashedValues.Add("dir_id", value);
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

            List<int> ids = new List<int>();
            foreach (Hashtable ht in relatedIds)
            {
                ids.Add((int)ht["id"]);
            }
            return ids.ToArray();
        }
        internal static T[] GetRecordsByIDS(ArrayList recordIDS, ArrayList searchFilters = null, string[] excludedFields = null, string[] includedFields = null, string[] insertFields = null)
        {
            string modelName = HpModelDictionary[typeof(T)];

            List<T> records = [];
            ArrayList fields = GetFields(includedFieldNames: includedFields, excludedFieldNames: excludedFields, insertFieldNames: insertFields);
            ArrayList result;

            if (searchFilters == null)
            {
                result = OClient.Read(modelName, recordIDS, fields, 10000);
            }
            else
            {
                result = OClient.Browse(modelName, [searchFilters, fields], 10000);
            }

            if (result.Count == 0) return null;

            foreach (Hashtable ht in result)
            {
                T record = HashConverter.ConvertToClass<T>(ht);

                // set record settings
                record.ID = (int)ht["id"];
                //record.HashedValues = ht;
                if (record.HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
                {
                    record.HashedValues = new Hashtable();
                    record.HashedValues.Add("dir_id", value);
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
                T record = HashConverter.ConvertToClass<T>(ht);

                // set record settings
                record.ID = (int)ht["id"];
                //record.HashedValues = ht;
                if (record.HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
                {
                    record.HashedValues = new Hashtable();
                    record.HashedValues.Add("dir_id", value);
                }
                record.IsRecord = true;
                record.ExcludedFields = excludedFields;
                record.CompleteConstruction();

                records.Add(record);
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
                T record = HashConverter.ConvertToClass<T>(ht);

                // set record settings
                record.ID = (int)ht["id"];
                //record.HashedValues = ht;
                if (record.HpModel == OdooDefaults.HP_VERSION && ht.TryGetValue("dir_id", out object value)) 
                {
                    record.HashedValues = new Hashtable();
                    record.HashedValues.Add("dir_id", value);
                }
                record.IsRecord = true;
                record.ExcludedFields = excludedFields;
                record.CompleteConstruction();

                records.Add(record);
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
        {
            ArrayList fieldNames = [];
            Type type = typeof(T);
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                bool isExcluded = false, isIncluded = true;
                if (excludedFieldNames != null) isExcluded = excludedFieldNames.Contains(field.Name);
                if (includedFieldNames != null) isIncluded = includedFieldNames.Contains(field.Name);
                if (!isExcluded && isIncluded) fieldNames.Add(field.Name);
            }
            if (insertFieldNames != null )
			{
				foreach ( string field in insertFieldNames )
				{
					if ( !fieldNames.Contains( field ) )
						fieldNames.Add( field );
				}
			}
			return fieldNames;
        }
        
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