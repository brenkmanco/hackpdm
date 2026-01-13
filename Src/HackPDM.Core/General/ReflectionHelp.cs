using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using HackPDM.Core.Hack;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.OdooModels.Models;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.General;

public class ReflectionHelp
{
    public static T ConvertToClass<T>(in Hashtable ht, MethodType mType = MethodType.FieldOnly) 
        where T : HpBaseModel, new()
    {
        T obj = new();
        AssignToClass(ht, ref obj, mType);
        return obj;
    }
    public static T[]? ConvertToClasses<T>(in IEnumerable<Hashtable>? hts, MethodType mType = MethodType.FieldOnly) where T : HpBaseModel, new()
    {
        //T[] objs = new T[hts.TryGetNonEnumeratedCount(out int len) ? len : hts.Count()].PopulateZip(() => new());
        IEnumerable<(Hashtable, T)>? objs = hts?.PopulateZip(obj => new T());
        return AssignToClasses(ref objs, mType);
    }
    public static T AssignToClass<T>(in Hashtable ht, T obj, MethodType mType = MethodType.FieldOnly)
        where T : HpBaseModel
    {
        Type type = typeof(T);

        PropertyInfo[]? properties = mType is MethodType.PropertyAndField or MethodType.PropertyOnly ? [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooPropAttribute)))] : null;
        FieldInfo[]? fields = mType is MethodType.PropertyAndField or MethodType.FieldOnly ? [.. type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooFieldAttribute)))] : null;
		
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
    public static T[]? AssignToClasses<T>(ref IEnumerable<(Hashtable, T)>? hts, MethodType mType = MethodType.FieldOnly)
        where T : HpBaseModel
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
    
    public static void AssignToClass<T>( in Hashtable ht, ref T obj, MethodType mType = MethodType.FieldOnly )
        where T : HpBaseModel, new()
        => AssignToClass( ht, obj, mType );
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
        PropertyInfo[] properties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooPropAttribute)))];
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
		PropertyInfo[] fields = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => Attribute.IsDefined(p, typeof(OdooPropAttribute)))];
		foreach (PropertyInfo field in fields)
        {
            if (!includeEmpty)
            {
                Type fType = field.PropertyType;
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

        Type valueOfType = value.GetType();
        Type? underType = Nullable.GetUnderlyingType( targetType );
        bool isEqual = underType == valueOfType;
        
        if (value is ArrayList list && list.Count > 0) return ConvertValue(list[0], targetType);

        if (targetType.IsAssignableFrom( valueOfType ) ) return value;
        if (targetType.IsEnum) return Enum.Parse(targetType, value.ToString());
        if (DateTime.TryParse(value.ToString(), out DateTime dt)) return dt;
            
        if (!isEqual && underType != typeof(bool)) return null;

        return isEqual ? value : Convert.ChangeType(value, targetType);
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
}