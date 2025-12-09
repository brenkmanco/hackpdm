using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using HackPDM.Data;
using HackPDM.Extensions.General;
using HackPDM.Forms.Hack;
using HackPDM.Hack;
using HackPDM.Odoo.OdooModels;
using HackPDM.Odoo.OdooModels.Models;
using HackPDM.Src.ClientUtils.Types;

using Microsoft.UI.Xaml.Controls;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

using static System.Runtime.InteropServices.JavaScript.JSType;

using DialogResult = System.Windows.Forms.DialogResult;
using MessageBox = System.Windows.Forms.MessageBox;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace HackPDM.ClientUtils;

public static class Help
{
    /// <summary>
    /// Returns an absolute or relative path for the parent of the passed argument
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetParentDirectory(string path)
    {
        // Check if path is a relative or absolute path:
        if (System.IO.Path.IsPathRooted(path))
        {
            // This is an absolute path:
            try
            {
                System.IO.DirectoryInfo directoryInfo = System.IO.Directory.GetParent(path);
                return (directoryInfo.FullName);
            }
            catch (ArgumentNullException)
            {
                MessageBox.Show("Path is a null reference.  Could not find its parent.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch (ArgumentException)
            {
                MessageBox.Show("Path is an empty string.  Could not find its parent.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                MessageBox.Show("The parent directory for path \"" + path + "\" could not be found.",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
            catch
            {
                MessageBox.Show("Could not find the parent directory for \"" + path + "\".",
                    "Path Error",
                    buttons: MessageBoxButtons.OK,
                    icon: MessageBoxIcon.Error);
                return ("");
            }
        }
        else
        {
            // This is a relative path.  Check if there are any slashes:
            if (path.Contains("\\"))
            {
                return (path.Substring(0, path.LastIndexOf("\\")));
            }
            else
            {
                // This is the last parent directory
                // TODO: Correct code to be more consisent (Some code may expect this method to return "pwa")
                // Return the empty string:
                return ("");
            }
        }
    }
    public static string GetBaseName(string path)
    {
        try
        {
            return (System.IO.Path.GetFileName(path));
        }
        catch
        {
            MessageBox.Show("Error getting Base Name from \"" + path + "\".",
                "Path Error",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
            return ("");
        }
    }
    public static void GetAllFilesInDir(string dirpath, ref List<string> filesfound)
    {
        try
        {
            foreach (string d in Directory.GetDirectories(dirpath))
            {
                foreach (string f in Directory.GetFiles(d))
                {
                    filesfound.Add(f);
                }

                GetAllFilesInDir(d, ref filesfound);
            }
        }
        catch ( System.Exception )
        {
            MessageBox.Show("Error finding local files.",
                "File Discovery Error",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
        }
    }
        

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
    public static (int, TreeViewNode?) LastValidTreeIndex(in string combinedPath, in string[] paths, in Dictionary<string, TreeViewNode> nodeMap)
    {
        ReadOnlySpan<char> strArray = combinedPath.AsSpan();
        int pathLength = combinedPath.Length;

        for (int i = paths.Length - 1; i >= 0; i--)
        {
            if (nodeMap.TryGetValue(strArray.Slice(0, pathLength).ToString(), out TreeViewNode? node))
            {
                return (i, node);
            }
            pathLength -= paths[i].Length + 1;
        }
        return (-1, null);
    }
    private static (int, TreeViewNode?) RecurseNodePath(in TreeViewNode currentNode, string[] nodes, int index)
    {
        if (currentNode == null)
        { 
            return (index-1, null); 
        }
        var entry = currentNode.Content as EntryRow;
        if (index >= nodes.Length || entry?.Name != nodes[index])
        {
            return (index - 1, currentNode);
        }
        if (index == nodes.Length - 1)
        {
            return (index, currentNode);
        }
            
        if (entry.Name == nodes[index]) 
        { 
            foreach (TreeViewNode child in currentNode.Children) 
            {
                var result = RecurseNodePath(child, nodes, index + 1);
                if (result.Item1 != index) 
                { 
                    return result; 
                } 
            } 
        }
        return (index, currentNode);
    }
    public static Dictionary<string, TreeViewNode>? ConvertTreeToDictionary(in TreeView tree)
    {
        if (tree.RootNodes.Count == 0) return null;

        Dictionary<string, TreeViewNode> treeDictionary = [];
        foreach (var node in tree.RootNodes)
            RecurseNodesConvert(node, in treeDictionary);

        return treeDictionary;
    }
    private static void RecurseNodesConvert(in TreeViewNode node, in Dictionary<string, TreeViewNode>? nodeMap)
    {
        var content = node?.Content as TreeData;
            
        nodeMap?.Add(content?.FullPath, node);
            
        foreach (TreeViewNode child in node.Children)
        {
            RecurseNodesConvert(in child, in nodeMap);
        }
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
    public static int Min(params int[] values)
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        int val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            val = val < values[i] ? val : values[i];
        }
        return val;        
    }
    public static int Max(params int[] values)
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        int val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            val = val > values[i] ? val : values[i];
        }
        return val;
    }
    public static int MaxUpTo(int max, params int[] values)
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        int val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            val = val > values[i] ? val : values[i];
            if (val >= max) return max;
        }
        return val > max ? max : val;
    }
    public static int MinDownTo(int min, params int[] values)
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        int val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            val = val < values[i] ? val : values[i];
            if (val <= min) return min;
        }
        return val < min ? min : val;
    }
    public static T MinDownTo<T>(T min, params T[] values) where T : IComparable<T>, IEquatable<T>
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        T val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            T vNext = values[i];
            int compared = val.CompareTo(vNext);
            val = compared < 0 ? val : vNext;

            if (compared <= 0) return min;
        }
        return val.CompareTo(min) < 0 ? min : val;
    }
    public static T MaxUpTo<T>(T max, params T[] values) where T : IComparable<T>, IEquatable<T>
    {
        if (values is null || values.Length < 1) throw new ArgumentException("values is null or empty");
        T val = values[0];

        // skip first value because val is already the first value
        for (int i = 1; i < values.Length; i++)
        {
            T vNext = values[i];
            int compared = val.CompareTo(vNext);
            val = compared > 0 ? val : vNext;

            if (compared >= 0) return max;
        }
        return val.CompareTo(max) > 0 ? max : val;
    }
    public static Dictionary<string, ColumnHeader?> DictExtAdd(params (string key, object value)[] pairs)
        => pairs.ToDictionary(p => p.key, p =>
            p.value switch
            {
                ColumnHeader column
                    => column,
                Tuple<int, HorizontalAlignment> values
                    => new ColumnHeader { Name = p.key, Text = p.key, Width = values.Item1, TextAlign = values.Item2 },
                Tuple<string, int, HorizontalAlignment> values
                    => new ColumnHeader { Name = p.key, Text = values.Item1, Width = values.Item2, TextAlign = values.Item3 },
                Tuple<string, int> values
                    => new ColumnHeader { Name = p.key, Text = values.Item1, Width = values.Item2, TextAlign = HorizontalAlignment.Left },
                int width
                    => new ColumnHeader { Name = p.key, Text = p.key, Width = width, TextAlign = HorizontalAlignment.Left },
                string text
                    => new ColumnHeader { Name = p.key, Text = text, Width = 75, TextAlign = HorizontalAlignment.Left },
                _ => null,
            }
        );
	public static void If(bool condition, Action @true, Action @false)
	{
		if (condition) @true(); else @false();
	}
	public static T If<T>(bool condition, Func<T> @true, Func<T> @false)
	{
		return condition ? @true() : @false();
	}
	public static Lazy<T> LazyIf<T>(bool condition, Func<T> @true, Func<T> @false)
	{
		return new Lazy<T>(() => condition ? @true() : @false());
	}
	public static bool ConvertSWFile<T>(HpVersion version, out T file) where T : new()
	{
		file = new T();
		var swApp = new SldWorksClass();
		FileInfo vInfo = new(Path.Combine(StorageBox.PwaPathAbsolute ?? "", version.WinPathway, version.name));
		if (!vInfo.Exists) return false;

		swDocumentTypes_e extSWType = version.file_ext.ToLower() switch
		{
			"sldprt" => swDocumentTypes_e.swDocPART,
			"sldasm" => swDocumentTypes_e.swDocASSEMBLY,
			"slddrw" => swDocumentTypes_e.swDocDRAWING,
			_ => swDocumentTypes_e.swDocNONE,
		};
		
		var model = swApp.OpenDoc(Path.Combine(StorageBox.TemporaryPath, version.name), (int)extSWType);
		if (model == null) return false;
		return false;
	}
	internal static ResultHackFile ValidateDependency(string path)
		=> new(HackFile.GetFromPath(path, FileOperations.GetRelativePath(path)));
	
	internal static (StatusMessage status, string message) GetStatusMessage(HackResult result, ResultHackFile? parentFile, List<ResultHackFile> list)
		=> result switch
		{
			HackResult.Clean => (StatusMessage.FOUND, $"Found All Dependencies in file {parentFile?.Hack?.FullPath}"),
			HackResult.MissingFile => (StatusMessage.ERROR, "File couldn't be found"),
			HackResult.MissingDepFile => (StatusMessage.ERROR, $"Missing dependency file {list.FirstOrDefault()?.Hack?.FullPath}"),
			HackResult.OutOfPWA => (StatusMessage.ERROR, $"Dependency file is outside of PWA folder: {list.FirstOrDefault()?.Hack?.FullPath}"),
			_ => (StatusMessage.ERROR, $"Other problem with file: {list.FirstOrDefault()?.Hack?.FullPath}"),
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
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        string[] memberNames = [.. fields.Select(x => x.Name), .. properties.Select(x => x.Name)];

        foreach (KeyValuePair<string, object> entry in _kwargs)
        {
            if (memberNames.Contains(entry.Key))
            {
                object memberInfo = type.GetField(entry.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Type mType;
                bool isField = true;

                if (memberInfo == null)
                {
                    memberInfo = type.GetProperty(entry.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
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
    public static T ConvertToClass<T>(in Hashtable ht, MethodType mType = MethodType.FieldOnly) 
        where T : HpBaseModel, new()
    {
        T obj = new();
        AssignToClass(ht, ref obj, mType);
        return obj;
    }
    public static T[] ConvertToClasses<T>(in Hashtable[] hts, MethodType mType = MethodType.FieldOnly) where T : HpBaseModel, new()
    {
        T[] objs = new T[hts.Length].Populate(() => new());
        AssignToClasses(hts, ref objs, mType);
        return objs;
    }
    public static T AssignToClass<T>(in Hashtable ht, T obj, MethodType mType = MethodType.FieldOnly)
        where T : HpBaseModel
    {
        Type type = typeof(T);

        foreach (DictionaryEntry entry in ht)
        {
            if (mType is MethodType.PropertyOnly or MethodType.PropertyAndField)
            {
                PropertyInfo? prop = type?.GetProperty(entry.Key?.ToString() ?? "", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
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
                FieldInfo field = type.GetField(entry.Key.ToString(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
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
    public static T[] AssignToClasses<T>(in Hashtable[] hts, ref T[] objs, MethodType mType = MethodType.FieldOnly)
        where T : HpBaseModel
    {
        if (hts is null) return null;

        Type type = typeof(T);
        string[] firstKeys = [.. hts.First().Keys.Cast<string>()];

        (PropertyInfo, ValueConversion)[]? propInfos
            = mType == MethodType.PropertyOnly
                ? new (PropertyInfo, ValueConversion)[firstKeys.Length]
                : null;
        (FieldInfo, ValueConversion)[]? fieldInfos
            = mType == MethodType.FieldOnly
                ? new (FieldInfo, ValueConversion)[firstKeys.Length]
                : null;

        string[]? pkeys = propInfos is null ? null : firstKeys;
        string[]? fkeys = fieldInfos is null ? null : firstKeys;

        for (int i = 0; i < hts.Length; i++)
        {
            if (hts[i] is null) continue;

            if (mType is MethodType.PropertyOnly or MethodType.PropertyAndField)
            {
                for (int j = 0; j < propInfos.Length; j++)
                {
                    (PropertyInfo, ValueConversion) prop = i == 0
                        ? (type.GetProperty(firstKeys[j], BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), NULL: ValueConversion.Null)
                        : propInfos[j];


                    object value;
                    var cMethod = prop.Item2;
                    var entry = hts[i][j] as DictionaryEntry?;
                    if (entry is null) continue;
                    if (prop.Item1 is not null && prop.Item1.CanWrite)
                    {
                        if (cMethod == ValueConversion.Null && entry?.Value != null)
                        {
                            cMethod = ConvertValueMethod(entry.Value, prop.Item1.PropertyType);
                            prop.Item2 = cMethod;
                            propInfos[j] = prop;
                        }
                        value = ConvertValue(entry?.Value, prop.Item1.PropertyType, cMethod);
                        prop.Item1.SetValue(objs[i], value);
                    }
                    else
                    {
                        objs[i].HashedValues[pkeys[j]] = entry.Value;
                    }
                }

            }
            if (mType is MethodType.FieldOnly or MethodType.PropertyAndField)
            {
                for (int j = 0; j < fieldInfos.Length; j++)
                {
                    (FieldInfo, ValueConversion) field = i == 0
                        ? (type.GetField(firstKeys[j], BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), NULL: ValueConversion.Null)
                        : fieldInfos[j];

                    object value;
                    var cMethod = field.Item2;
                    var entry = hts[i][j] as DictionaryEntry?;
                    if (entry is null) continue;
                    hts[i][fkeys[j]] = cMethod;
                    if (field.Item1 is not null)
                    {
                        if (cMethod == ValueConversion.Null && entry?.Value != null)
                        {
                            cMethod = ConvertValueMethod(entry.Value, field.Item1.FieldType);
                            field.Item2 = cMethod;
                            fieldInfos[j] = field;
                        }
                        value = ConvertValue(entry.Value, field.Item1.FieldType, cMethod);
                        field.Item1.SetValue(objs[i], value);
                    }
                    else
                    {
                        objs[i].HashedValues[fkeys[j]] = entry.Value;
                    }
                }
            }
        }

        return objs;
    }
    public static void PopulateSelf<T>(this T hprecord, in Hashtable ht, MethodType mType = MethodType.FieldOnly) where T : HpBaseModel
        => AssignToClass(ht, hprecord, mType);
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
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
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
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
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
    internal static object ConvertValue(object value, Type targetType)
    {
        if (value == null) return null;

        Type valueOfType = value.GetType();

        if (targetType.IsAssignableFrom( valueOfType ) ) return value;
        if (targetType.IsEnum) return Enum.Parse(targetType, value.ToString());
        if (DateTime.TryParse(value.ToString(), out DateTime dt)) return dt;
            
        if (value is ArrayList list && list.Count > 0) return ConvertValue(list[0], targetType);

        Type underType = Nullable.GetUnderlyingType( targetType );
        bool isEqual = underType == valueOfType;

        return valueOfType == typeof( bool ) && !isEqual ? null : isEqual ? value : Convert.ChangeType(value, targetType);
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
            ValueConversion.Enum => Enum.Parse(targetType, value?.ToString() ?? ""),
            ValueConversion.DateTime => DateTime.TryParse(value?.ToString() ?? "", out DateTime dt) ? dt : null,
            ValueConversion.Nullable => value,
            ValueConversion.OtherConvert => Convert.ChangeType(value, targetType),
            _ => null,
        };
    }
       
}