using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels.Models;



//
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HackPDM.Core;
using HackPDM.Domain.OdooModels;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Shared.GlobalData;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swdocumentmgr;
using EntryRow = HackPDM.Domain.Representation.EntryRow;
using TreeData = HackPDM.Domain.Representation.TreeData;
using OClient = HackPDM.Infrastructure.Odoo.OdooClient;
using Attribute = System.Attribute;
using HackPDM.Shared.OdooAttributes;

//

namespace HackPDM.Infrastructure.Odoo;

public static class Help
{
    // [0, 1, 2, 3, 4]
	// [0, 1], [2, 3], [4]
	public static List<List<T>>? BatchList<T>(T[]? list, int batchSize)
    {
        if (list is null) return null;
        List<List<T>> batchList = [];
        int listSize = list.Length;
        Span<T> spanList = list.AsSpan();

        for (int i = 0; i < listSize; i += batchSize)
        {
            List<T> innerList = [];


            if (listSize < batchSize + i)
                innerList.AddRange(spanList.Slice(i, (listSize - i)).ToArray());
            else
            {
                innerList.AddRange(spanList.Slice(i, batchSize).ToArray());
            }
            batchList.Add(innerList);
        }
        return batchList;
    }
	public static T[][]? BatchArray<T>(T[]? array, int batchSize)
	{
		if (array is null) return null;
		if (array.Length == 0) return null;

		(var numOfBatches, var remainder) = Math.DivRem(array.Length, batchSize);

		if (remainder > 0) numOfBatches++;
		T[][] batchArray = new T[numOfBatches][];

		for (int i = 0; i < numOfBatches; i++)
		{
			T[] values = i == numOfBatches-1 
				? array[(i * batchSize)..((i * batchSize) + remainder)] 
				: array[(i * batchSize) .. ((i+1) * batchSize)];
			batchArray[i] = values;
		}
		return batchArray;
	}
	public static T[][]? BatchArray<T>(IEnumerable<T>? array, int batchSize)
		=> array is null ? null : BatchArray<T>([.. array], batchSize);
    public static List<List<T>>? BatchList<T>(IEnumerable<T>? list, int batchSize)
        => list is null ? null : BatchList<T>([.. list], batchSize);
        
    // give the ArrayList class an extension method that selects
    public static IEnumerable<string> FastSlice(IEnumerable<string> source, int startIndex, string prependText = null, string appendText = null)
    {
        foreach (string str in source)
        {
            StringBuilder sb = new();

            // add prepended text
            if (prependText != null) sb.Append(prependText);
            // slice
            sb.Append(str.AsSpan()[startIndex..].ToString());
            // add appended text
            if (appendText != null) sb.Append(appendText);

            yield return sb.ToString();
        }
    }
    public static ArrayList GetResults(in ArrayList source, string hashKeyName, bool singleValue=false)
    {
        ArrayList results = [];
            
        foreach (Hashtable ht in source)
        {
            if (ht.ContainsKey(hashKeyName))
            {
                //if (ht[hashKeyName] is ArrayList al)
                if (singleValue)
                    results.Add(((ArrayList)ht[hashKeyName])[0]);
                else
                    results.AddRange((ArrayList)ht[hashKeyName]);

            }
        }
        return results;
    }
    
    public static Hashtable OdooIdBecomesKey(ArrayList arr)
    {
        Hashtable newHt = [];
        foreach (Hashtable ht in arr)
        {
            Hashtable entryDict = [];

            foreach (DictionaryEntry de in ht)
            {
                if ((string)de.Key != "id") entryDict.Add(de.Key, de.Value);
            }
            newHt.Add(ht["id"], entryDict);
        }
        return newHt;
    }
    
	public static bool ConvertSWFile<T>(HpVersion versionModel, out T file) where T : new()
	{
		file = new T();
		var swApp = new SldWorksClass();
		FileInfo vInfo = new(Path.Combine(HackDefaults.Instance.PwaPathAbsolute ?? "", versionModel.WinPathway, versionModel.name));
		if (!vInfo.Exists) return false;

		swDocumentTypes_e extSWType = versionModel.file_ext.ToLower() switch
		{
			"sldprt" => swDocumentTypes_e.swDocPART,
			"sldasm" => swDocumentTypes_e.swDocASSEMBLY,
			"slddrw" => swDocumentTypes_e.swDocDRAWING,
			_ => swDocumentTypes_e.swDocNONE,
		};
		
		var model = swApp.OpenDoc(Path.Combine(StorageBox.TemporaryPath, versionModel.name), (int)extSWType);
		if (model == null) return false;
		return false;
	}

    public static ResultHackFile ValidateDependency(string path)
		=> new(HackFile.GetFromPath(path, FileOperations.GetRelativePath(path)));

    public static (StatusMessage status, string message) GetStatusMessage(HackResult result, ResultHackFile? parentFile, List<ResultHackFile> list)
		=> result switch
		{
			HackResult.Clean => (StatusMessage.FOUND, $"Found All Dependencies in file {parentFile?.Hack?.FullPath}"),
			HackResult.MissingFile => (StatusMessage.ERROR, "File couldn't be found"),
			HackResult.MissingDepFile => (StatusMessage.ERROR, $"Missing dependency file {list.FirstOrDefault()?.Hack?.FullPath}"),
			HackResult.OutOfPWA => (StatusMessage.ERROR, $"Dependency file is outside of PWA folder: {list.FirstOrDefault()?.Hack?.FullPath}"),
			_ => (StatusMessage.ERROR, $"Other problem with file: {list.FirstOrDefault()?.Hack?.FullPath}"),
		};
    public static SwDmDocumentType GetSwDmDocumentTypeFromExtension(string file_ext)
        => file_ext.ToLower() switch
        {
            "sldprt" => SwDmDocumentType.swDmDocumentPart,
            "sldasm" => SwDmDocumentType.swDmDocumentAssembly,
            "slddrw" => SwDmDocumentType.swDmDocumentDrawing,
            _ => SwDmDocumentType.swDmDocumentUnknown,
        };
}

public class Kwargs<T>(T obj)
{
    T _obj = obj;
    Dictionary<string, object> _kwargs;

    public Kwargs(T obj, Dictionary<string, object> kwargs) : this(obj)
    {
        this._kwargs = kwargs;
    }

    public T ApplyKwargsToObject()
    {
        Type type = _obj.GetType();
        FieldInfo[] fields = [.. type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooFieldAttribute)))];
		PropertyInfo[] properties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooPropAttribute)))];

        string[] memberNames = [.. fields.Select(x => x.Name), .. properties.Select(x => x.Name)];

        foreach (KeyValuePair<string, object> entry in _kwargs)
        {
            if (memberNames.Contains(entry.Key))
            {
                object memberInfo = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooFieldAttribute))).ToArray();
                Type mType;
                bool isField = true;

                if (memberInfo == null)
                {
                    memberInfo = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooPropAttribute))).ToArray();
                    mType = ((PropertyInfo)memberInfo).PropertyType;
                    isField = false;
                }
                else mType = ((FieldInfo)memberInfo).FieldType;

                if (entry.Value == null || mType.IsAssignableFrom(entry.Value.GetType()))
                {
                    if (isField) ((FieldInfo)memberInfo).SetValue(_obj, entry.Value);
                    else ((PropertyInfo)memberInfo).SetValue(_obj, entry.Value);
                }
                else if (mType.IsEnum)
                {
                    if (isField) ((FieldInfo)memberInfo).SetValue(_obj, Enum.Parse(mType, entry.Value.ToString()));
                    else ((PropertyInfo)memberInfo).SetValue(_obj, Enum.Parse(mType, entry.Value.ToString()));
                }
                else
                {
                    try
                    {
                        if (isField) ((FieldInfo)memberInfo).SetValue(_obj, Convert.ChangeType(entry.Value, mType));
                        else ((PropertyInfo)memberInfo).SetValue(_obj, Convert.ChangeType(entry.Value, mType));
                    }
                    catch { }
                }
            }
        }
        return _obj;
    }

}
public static class HashConverter
{
	static readonly Type boolType = typeof(bool);
	static readonly Type arrType = typeof(ArrayList);

    //
    public static T? ConvertToClass<T>(in Hashtable ht) where T : HpBaseModelTransport, new()
    {
		T record = new();
        return AssignToClass(ht, ref record);
	}
	public static T[]? ConvertToClasses<T>(in IEnumerable<Hashtable>? hts) where T : HpBaseModelTransport, new()
	{
		(Hashtable, T)[] records = [.. hts?.PopulateZip(obj => new T()) ?? []];
        var values = records.AsSpan();
        return AssignToClasses(ref values).SelectSecond();
	}
	
    public static ref T AssignToClass<T>(in Hashtable ht, ref T record) where T : HpBaseModelTransport
    {
		var map = OdooAssignments<T>.Map;
		
		if (ht.TryGetValue("id", out int? id) && id is not null)
			record.id = id ?? 0;

		foreach (DictionaryEntry entry in ht)
		{
			if (map.TryGetValue(entry.Key as string ?? "", out var assign))
				assign(record, entry.Value);
			else
				record.HashedValues.Add( entry.Key, entry.Value );
		}
		return ref record;
	}
    public static void AssignToClass<T>(in Hashtable ht, T record) where T : HpBaseModelTransport
    {
		var map = OdooAssignments<T>.Map;

		if (ht.TryGetValue("id", out int? id) && id is not null)
			record.id = id ?? 0;

		foreach (DictionaryEntry entry in ht)
		{
			if (map.TryGetValue(entry.Key as string ?? "", out var assign))
				assign(record, entry.Value);
			else
				record.HashedValues.Add( entry.Key, entry.Value );
		}
	}
	public static ref Span<(Hashtable, T)> AssignToClasses<T>(ref Span<(Hashtable, T)> values) where T : HpBaseModelTransport
    {
        if (values.Length == 0) return ref values;
		var map = OdooAssignments<T>.Map;
        
		for (int i = 0; i < values.Length; i++)
        {
            ref (Hashtable ht, T record) record = ref values[i];

			if (record.ht.TryGetValue("id", out int? id) && id is not null)
				record.record.id = id ?? 0;
            
		    foreach (DictionaryEntry entry in record.ht)
		    {
                if( map.TryGetValue( entry.Key as string ?? "", out var assign ) )
                    assign( record.record, entry.Value );
                else
                    record.record.HashedValues.Add( entry.Key, entry.Value );
		    }
		}
        return ref values;
	}
	//
	//
	public static T ConvertToClassFallback<T>(in Hashtable ht, MethodType mType = MethodType.PropertyOnly) 
        where T : HpBaseModelTransport, new()
    {
        T obj = new();
        AssignToClassFallback(ht, ref obj, mType);
        return obj;
    }
    public static T[]? ConvertToClassesFallback<T>(in IEnumerable<Hashtable>? hts, MethodType mType = MethodType.PropertyOnly) where T : HpBaseModelTransport, new()
    {
        //T[] objs = new T[hts.TryGetNonEnumeratedCount(out int len) ? len : hts.Count()].PopulateZip(() => new());
        IEnumerable<(Hashtable, T)>? objs = hts?.PopulateZip(obj => new T());
        return AssignToClassesFallback(ref objs, mType);
    }
    public static T AssignToClassFallback<T>(in Hashtable ht, T obj, MethodType mType = MethodType.PropertyOnly)
        where T : HpBaseModel
    {
        Type type = typeof(T);

        PropertyInfo[]? properties = mType is MethodType.PropertyAndField or MethodType.PropertyOnly ? [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooPropAttribute)))] : null;
        FieldInfo[]? fields = mType is MethodType.PropertyAndField or MethodType.FieldOnly ? [.. type?.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f=>Attribute.IsDefined(f, typeof(OdooFieldAttribute)))] : null;
		
        foreach (DictionaryEntry entry in ht)
        {
            if (mType is MethodType.PropertyOnly or MethodType.PropertyAndField)
            {
                PropertyInfo? prop = properties?.FirstOrDefault(p => p.Name == entry.Key.ToString());
				// type?.GetProperty(entry.Key?.ToString() ?? "", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				if (prop != null && prop.CanWrite)
                {
                    object value = ConvertValue(entry.Value, prop.PropertyType);
                    prop.SetValue(obj, value);
                }
                else
                {
                    obj.HashedValues[entry.Key.ToString()] = entry.Value;
                }
            }
            if (mType is MethodType.FieldOnly or MethodType.PropertyAndField)
            {
                FieldInfo? field = fields?.FirstOrDefault(f => f.Name == entry.Key.ToString());
				// type.GetField(entry.Key.ToString(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				if (field != null)
                {
                    object value = ConvertValue(entry.Value, field.FieldType);
                    field.SetValue(obj, value);
                }
                else
                {
                    obj.HashedValues[entry.Key.ToString()] = entry.Value;
                }
            }
        }
        return obj;
    }
    public static T[]? AssignToClassesFallback<T>(ref IEnumerable<(Hashtable, T)>? hts, MethodType mType = MethodType.PropertyOnly)
        where T : HpBaseModelTransport
    {
        if (hts is null || hts.FirstOrDefault().Item1 is not Hashtable hashFirst) return null;

        Type type = typeof(T);
        string[] firstKeys = [.. hashFirst.Keys.Cast<string>()];

        List<PropertyInfo>? properties = null;
        //mType is MethodType.PropertyAndField or MethodType.PropertyOnly
        //    ? type?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        //    : null;
        List<FieldInfo>? fields = null;
        //= mType is MethodType.PropertyAndField or MethodType.FieldOnly 
        //    ? type?.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        //    : null;
        bool IsProp = mType is MethodType.PropertyOnly or MethodType.PropertyAndField;
        bool IsField = mType is MethodType.FieldOnly or MethodType.PropertyAndField;
        
		//(PropertyInfo?, ValueConversion?)[]? propInfos
  //          = IsProp
		//		? new (PropertyInfo?, ValueConversion?)[firstKeys.Length]
  //              : null;
  //      (FieldInfo?, ValueConversion?)[]? fieldInfos
  //          = IsField
		//		? new (FieldInfo?, ValueConversion?)[firstKeys.Length]
  //              : null;

        bool IsFlagged;

		(IEnumerable<ReflectionInfo.PropInfoEntry> propInfos, 
            IEnumerable<ReflectionInfo.FieldInfoEntry> fieldInfos) = firstKeys.SegmentSelectDiffWhere(
                (key, index) =>
        {
			IsFlagged = false;
            
            ReflectionInfo.PropInfoEntry? propReturn = null;
            ReflectionInfo.FieldInfoEntry? fieldReturn = null;

			if (mType is MethodType.PropertyOnly or MethodType.PropertyAndField)
			{
                var prop = type?.GetProperty(key!, BindingFlags.Public | BindingFlags.Instance);
                IsFlagged = prop is null;
                propReturn = new (!IsFlagged
                    ? (prop, key, null)
                    : (null, key, ValueConversion.DoNothing));
			}
			if (mType is MethodType.FieldOnly or MethodType.PropertyAndField)
			{
                if (!IsFlagged && mType is MethodType.PropertyAndField)
                {
                    fieldReturn = new (null, key, ValueConversion.Skip);
                }
                else
                {
				    var field = type?.GetField(key!, BindingFlags.Public | BindingFlags.Instance);
                
                    fieldReturn = new(field is not null 
                        ? (field, key, null) 
                        : (null, key, ValueConversion.DoNothing));
                }
			}
            return (!(IsField && fieldReturn.Conversion is not ValueConversion.Skip), propReturn, fieldReturn);
		});

        
        foreach ((Hashtable hashtable, T obj) in hts)
        {
            if (hashtable is null) continue;

   //          if (hashtable.TryGetValue("id", out int? id) && id is not null)
   //          {
   //              obj.id = id ?? 0;
			// }
			
			if (mType is MethodType.PropertyOnly or MethodType.PropertyAndField)
            {
                foreach (var propEntry in propInfos)
                {
					if (!hashtable.TryGetValue(propEntry.Name, out object? val) || val is null) continue;

                    if (propEntry.Conversion is ValueConversion.Skip) continue;

					if (propEntry.PropInfo?.Name is "dir_id" && (obj.HpModel is OdooDefaultsConstants.HP_VERSION or OdooDefaultsConstants.HP_ENTRY))
					{
						obj.HashedValues.Add("dir_id", val);
					}

					if (propEntry.Conversion is ValueConversion.DoNothing || propEntry.PropInfo is null || !propEntry.PropInfo.CanWrite)
					{
						obj.HashedValues[propEntry.Name!] = val;
                        propEntry.Conversion = ValueConversion.DoNothing;
						continue;
					}

                    if (propEntry.Conversion is ValueConversion.Null && val is not null)
                    {
                        propEntry.Conversion = ConvertValueMethod(val, propEntry.PropInfo.PropertyType);
					}
                    propEntry.PropInfo?.SetValue(obj, ConvertValue(val, propEntry.PropInfo.PropertyType, propEntry.Conversion ?? ValueConversion.Null));
					obj.CompleteConstruction();
					obj.IsRecord = true;
				}
            }
            if (mType is MethodType.FieldOnly or MethodType.PropertyAndField)
            {
                foreach(var fieldEntry in fieldInfos)
                {
					if (!hashtable.TryGetValue(fieldEntry.Name, out object? val) || val is null) continue;

                    if (fieldEntry.Conversion is ValueConversion.Skip) continue;

					if (fieldEntry.Name is "dir_id" && (obj.HpModel is OdooDefaultsConstants.HP_VERSION or OdooDefaultsConstants.HP_ENTRY))
					{
						obj.HashedValues.Add("dir_id", val);
					}

					if (fieldEntry.Conversion is ValueConversion.DoNothing || fieldEntry.FieldInfo is null)
                    {
						obj.HashedValues[fieldEntry.Name!] = val;
                        fieldEntry.Conversion = ValueConversion.DoNothing;
						continue;
					}

					if (fieldEntry.Conversion is null or ValueConversion.Null && val is not null)
					{
						fieldEntry.Conversion = ConvertValueMethod(val, fieldEntry.FieldInfo.FieldType);
					}

					fieldEntry.FieldInfo?.SetValue(obj, ConvertValue(val, fieldEntry.FieldInfo.FieldType, fieldEntry.Conversion ?? ValueConversion.Null));
                    obj.CompleteConstruction();
                    obj.IsRecord = true;
				}
			}

			
		}

		return [.. hts.Select(i => i.Item2)];
    }
    //

    public static void PopulateSelf<T>(this T hprecord, in Hashtable ht, MethodType mType = MethodType.PropertyOnly) where T : HpBaseModel
        => AssignToClassFallback(ht, hprecord, mType);
    public static void AssignToClassFallback<T>( in Hashtable ht, ref T obj, MethodType mType = MethodType.PropertyOnly )
        where T : HpBaseModel, new()
        => AssignToClassFallback( ht, obj, mType );
    public static Hashtable ConvertToHashtable<T>(T obj, MethodType mType = MethodType.PropertyAndField, bool includeEmpty = true, in string[] excludedFieldNames = null)
    {
        Hashtable ht = [];

        switch (mType)
        {
            case MethodType.PropertyOnly:
            {
                GetProperties(obj, ref ht);
                break;
            }
            case MethodType.FieldOnly:
            {
                GetFields(obj, ref ht);
                break;
            }
            case MethodType.PropertyAndField:
            {
                GetProperties(obj, ref ht);
                GetFields(obj, ref ht);
                break;
            }
        }
        return ht;
    }
    private static void GetProperties<T>(T obj, ref Hashtable ht, bool includeEmpty = true, in string[] excludedFieldNames = null)
    {
        Type type = typeof(T);
        PropertyInfo[] properties = (PropertyInfo[])type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => System.Attribute.IsDefined(p, typeof(OdooPropAttribute)));
        foreach (PropertyInfo prop in properties)
        {
            if (!prop.CanRead) continue;
            if (!includeEmpty)
            {
                Type pType = prop.PropertyType;
                bool valueType = pType.IsValueType;
                if (valueType && Activator.CreateInstance(pType) == prop.GetValue(obj)) continue;
                else if (!valueType && prop.GetValue(obj) == null) continue;
            }

            string propertyName = prop.Name;
            object propertyValue = prop.GetValue(obj);
            ht.Add(propertyName, propertyValue);
        }
    }
    private static void GetFields<T>(T obj, ref Hashtable ht, bool includeEmpty = true, in string[] excludedFieldNames = null)
    {
        Type type = typeof(T);
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (FieldInfo field in fields)
        {
            if (!includeEmpty)
            {
                Type fType = field.FieldType;
                bool valueType = fType.IsValueType;
                if (valueType && Activator.CreateInstance(fType) == field.GetValue(obj)) continue;
                else if (!valueType && field.GetValue(obj) == null) continue;
            }

            string fieldName = field.Name;
            object fieldValue = field.GetValue(obj);
            ht.Add(fieldName, fieldValue);
        }
    }

    // first case: value is nullable but target type isn't
    // second case: target type is nullable but value isn't
    // 
    internal static object? ConvertValue(object value, Type targetType)
    {
		if (value == null) return null;
		
        // if the value is an ArrayList, get the first value and convert that
		if (value is ArrayList list && list is [int id, string _]) return ConvertValue(id, targetType);

		// get the type for value from odoo
		Type typeOfValue = value.GetType();

		// get the underlying type if nullable from target type
		Type? underType = Nullable.GetUnderlyingType( targetType );
		// If there is an underlying type, use that for checking, otherwise use the target type
		Type checkType = underType ?? targetType;

        // check if both types are the same
		bool isEqual = checkType == typeOfValue;

		// handles the bool special case
		// --
		// value can be false because it is a bool or because it is null
		// if value is true then it is definitely a bool
		if (value is bool boolOrNull)
		{
			return boolOrNull == false ?
                isEqual
				// if same type, return false
				    ? false
				    // if not same type, but nullable, return default of checkType
				    : checkType.IsValueType
				        ? Activator.CreateInstance(checkType)
				        : null
				// value is true, so definitely a bool
				: true;
            
		}

		// check direct assignable
		if (checkType.IsAssignableFrom( typeOfValue ) ) return value;

		// check enum conversion
		if (checkType.IsEnum) return Enum.Parse(checkType, value.ToString()!);

		// check DateTime conversion
		if (DateTime.TryParse(value.ToString(), out DateTime dt)) return dt;
            
        // general conversion
        return isEqual ? value : Convert.ChangeType(value, checkType);
    }
    internal static ValueConversion ConvertValueMethod(object value, Type targetType)
    {
        if (value == null) return ValueConversion.Null;

        Type valueOfType = value.GetType();

        if (targetType.IsAssignableFrom(valueOfType)) return ValueConversion.Assignable;
        if (targetType.IsEnum) return ValueConversion.Enum;
        if (DateTime.TryParse(value.ToString(), out _)) return ValueConversion.DateTime;
        if (value is ArrayList list && list.Count > 0) return ConvertValueMethod(list[0], targetType);

        Type underType = Nullable.GetUnderlyingType(targetType);
        bool isEqual = underType == valueOfType;

        return valueOfType == typeof(bool) && !isEqual ? ValueConversion.Null : isEqual ? ValueConversion.Nullable : ValueConversion.OtherConvert;
    }
    internal static object? ConvertValue(object? value, Type targetType, ValueConversion conversion)
    {
        return conversion switch
        {
            ValueConversion.Null => null,
            ValueConversion.Assignable => value,
            ValueConversion.Nullable => value,
            ValueConversion.Enum => Enum.Parse(targetType, value?.ToString() ?? ""),
            ValueConversion.DateTime => DateTime.TryParse(value?.ToString() ?? "", out DateTime dt) ? dt : null,
            ValueConversion.OtherConvert => Convert.ChangeType(value, targetType),
            _ => null,
        };
    }
    
    public static ArrayList FilesNotInOdoo(string[] filePaths)
    {
        // key: checksum, value: filepath
        Dictionary<string, string> checkFiles = new(filePaths.Length);
        foreach (string filePath in filePaths)
        {
            checkFiles.Add(FileOperations.FileChecksum(filePath, SHA1.Create()), filePath);
        }

        ArrayList domain = ["checksum", "in", checkFiles.Keys.ToArray()];
        ArrayList fields = ["checksum"];
        ArrayList result = OClient.Browse(HpVersion.GetHpModel(), [domain, fields], 10000);

        // Hashtable of all results
        // might have array or value
        ArrayList values = Help.GetResults(result, "checksum", true);
        return values;
    }
}