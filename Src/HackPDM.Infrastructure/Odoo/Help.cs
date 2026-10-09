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
public static class HashConverter
{
	static readonly Type boolType = typeof(bool);
	static readonly Type arrType = typeof(ArrayList);

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
}