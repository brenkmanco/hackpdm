using System;
using System.IO;
using System.Windows.Forms;
using System.Globalization;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Text;
using HackPDM.ClientUtils;
using System.ComponentModel;

namespace HackPDM
{
    public static class Utils
    {
        public static HashAlgorithm SHAAlg { get; private set; } = SHA1.Create();
        public static HashAlgorithm MD5Alg { get; private set; } = MD5.Create();
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
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    return ("");
                }
                catch (ArgumentException)
                {
                    MessageBox.Show("Path is an empty string.  Could not find its parent.",
                            "Path Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    return ("");
                }
                catch (System.IO.DirectoryNotFoundException)
                {
                    MessageBox.Show("The parent directory for path \"" + path + "\" could not be found.",
                            "Path Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    return ("");
                }
                catch
                {
                    MessageBox.Show("Could not find the parent directory for \"" + path + "\".",
                            "Path Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
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
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                return ("");
            }
        }
        public static string GetAbsolutePath(string strLocalFileRoot, string stringPath)
        {
            //Get Full path
 //           if (stringPath.Substring(0, 3) == "pwa")
            if (!System.IO.Path.IsPathRooted(stringPath))
            {
                return (strLocalFileRoot + stringPath.Substring(3));
            }
            else
            {
                // NOTE 1:  This will occur if:
                //  a) stringPath starts with a drive identifier (e.g., "C:/"), or
                //  b) stringPath starts with a root folder slash (e.g., "\\").
                //
                // NOTE 2:  System.IO.Path.IsPathRooted() does not check if stringPath is an actual path.
                return(stringPath);
            }
        }
        public static string GetRelativePath(string strLocalFileRoot, string stringPath)
        {
            // terminate paths to prevent matching with paths that begin the same as strLocalFileRoot
            string strRootTest = strLocalFileRoot.ToUpper() + "\\";
            string strPathTest = (stringPath.EndsWith("\\") ? stringPath + "\\" : stringPath + "\\");

            // only return a value if this path is in strLocalFileRoot
            if (strPathTest.Length < strRootTest.Length) return "";
            if (!strPathTest.ToUpper().StartsWith(strRootTest))
            {
                return "";
            }

            // if we get here, this path is in the pwa
            // get relative tree path
            string stringParse = "";
            // replace actual root path with pwa
            if (stringPath.IndexOf(strLocalFileRoot, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                stringParse = "pwa" + stringPath.Substring(strLocalFileRoot.Length);
            }
            return stringParse;
        }
        public static string GetShortName(string FullName)
        {
            //    return FullName.Substring(FullName.LastIndexOf("\\") + 1);
            return Utils.GetBaseName(FullName);
        }
        //protected string GetFileExt(string strFileName) {
        //    //Get Name of folder
        //    string[] strSplit = strFileName.Split('.');
        //    int _maxIndex = strSplit.Length-1;
        //    return strSplit[_maxIndex];
        //}
        public static string FormatDate(DateTime dtDate)
        {

            // if file not in local current day light saving time, then add an hour?
            //if (TimeZone.CurrentTimeZone.IsDaylightSavingTime(dtDate) == false)
            //{
            //    dtDate = dtDate.AddHours(1);
            //}

            // get date and time in short format and return it
            string stringDate = "";
            //stringDate = dtDate.ToShortDateString().ToString() + " " + dtDate.ToShortTimeString().ToString();
            stringDate = dtDate.ToString("yyyy-MM-dd HH:mm:ss");
            return stringDate;

        }
        public static string FormatSize(Int64 lSize)
        {
            //Format number to KB
            string stringSize = "";
            NumberFormatInfo myNfi = new();

            Int64 lKBSize = 0;

            if (lSize < 1024)
            {
                if (lSize == 0)
                {
                    //zero byte
                    stringSize = "0";
                }
                else
                {
                    //less than 1K but not zero byte
                    stringSize = "1";
                }
            }
            else
            {
                //convert to KB
                lKBSize = lSize / 1024;
                //format number with default format
                stringSize = lKBSize.ToString("n", myNfi);
                //remove decimal
                stringSize = stringSize.Replace(".00", "");
            }

            return stringSize + " KB";

        }
        public static string StringMD5(string FileName)
        {
            if (!File.Exists(FileName)) return null;
            // get local file checksum
            using (var md5 = MD5.Create())
            {
                //using (var stream = File.OpenRead(FileName))
                using (var stream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                }
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
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
            }
        }
        

        // [0, 1, 2, 3, 4]
        // [0, 1], [2, 3], [4]
        public static List<List<T>> BatchList<T>(T[] list, int batchSize)
        {
            
            List<List<T>> batchList = [];
            int listSize = list.Count();
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
        public static void MapValues<T, T2>(PropertyInfo property, List<T> source, IEnumerable<T2> mapValues)
        {
            int sourceCount = source.Count(), valuesCount = mapValues.Count();
            List<T2> values = new(mapValues);

            if (sourceCount != valuesCount) return;

            for (int i = 0; i < sourceCount; i++)
            {
                property.SetValue(source[i], values[i]);
            }
        }
        // give the ArrayList class an extension method that selects
        public static IEnumerable<string> FastSlice(IEnumerable<string> source, int startIndex, string prependText = null, string appendText = null)
        {
            foreach (string str in source)
            {
                StringBuilder sb = new();

                // add prepended text
                if (prependText != null) sb.Append(prependText);
                // slice
                sb.Append(str.AsSpan().Slice(startIndex).ToString());
                // add appended text
                if (appendText != null) sb.Append(appendText);

                yield return sb.ToString();
            }
        }
        public static string[] FastSlice(string[] source, int startIndex, string prependText = null, string appendText = null)
        {
            string[] replace = new string[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                StringBuilder sb = new();

                // add prepended text
                if (prependText != null) sb.Append(prependText);
                // slice
                sb.Append(source[i].AsSpan().Slice(startIndex).ToString());
                // add appended text
                if (appendText != null) sb.Append(appendText);

                replace[i] = sb.ToString();
            }

            return replace;
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
        public static IEnumerable<T> GetResults<T>(ArrayList source, string hashKeyName, bool singleValue = false)
        {
            foreach (Hashtable ht in source)
            {
                if (ht.ContainsKey(hashKeyName))
                {
                    //if (ht[hashKeyName] is ArrayList al)
                    if (singleValue)
                        yield return (T)(((ArrayList)ht[hashKeyName])[0]);
                    else
                    {
                        ArrayList result = ((ArrayList)ht[hashKeyName]);
                        foreach (T item in result)
                        {
                            yield return item;
                        }
                    }
                        
                }
            }
        }
        public static (int, TreeNode) LastValidTreeIndex(in string[] paths, in TreeView treeView)
        {
            return RecurseNodePath(treeView.Nodes[0], paths, 0);
        }
        public static (int, TreeNode) LastValidTreeIndex(in string combinedPath, in string[] paths, in Dictionary<string, TreeNode> nodeMap)
        {
            ReadOnlySpan<char> strArray = combinedPath.AsSpan();
            int pathLength = combinedPath.Length;

            for (int i = paths.Length - 1; i >= 0; i--)
            {
                if (nodeMap.TryGetValue(strArray.Slice(0, pathLength).ToString(), out TreeNode node))
                {
                    return (i, node);
                }
                pathLength -= paths[i].Length + 1;
            }
            return (-1, null);
        }


		private static (int, TreeNode) RecurseNodePath(in TreeNode currentNode, string[] nodes, int index)
        {
            if (currentNode == null)
            { 
                return (index-1, null); 
            }
            if (index >= nodes.Length || currentNode.Text != nodes[index])
            {
                return (index - 1, currentNode);
            }
            if (index == nodes.Length - 1)
            {
                return (index, currentNode);
            }
            
            if (currentNode.Text == nodes[index]) 
            { 
                foreach (TreeNode child in currentNode.Nodes) 
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
        public static Dictionary<string, TreeNode> ConvertTreeToDictionary(in TreeView tree)
        {
            if (tree.Nodes.Count == 0) return null;

            Dictionary<string, TreeNode> treeDictionary = [];
            RecurseNodesConvert(tree.Nodes[0], in treeDictionary);

            return treeDictionary;
        }
        private static void RecurseNodesConvert(in TreeNode node, in Dictionary<string, TreeNode> nodeMap)
        {
            nodeMap.Add(node.FullPath, node);
            
            foreach (TreeNode child in node.Nodes)
            {
                RecurseNodesConvert(in child, in nodeMap);
            }
        }
        
    }

    public class Kwargs<T>(T obj)
    {
        T obj = obj;
        Dictionary<string, object> kwargs;

        public Kwargs(T obj, Dictionary<string, object> kwargs) : this(obj)
        {
            this.kwargs = kwargs;
        }

        public T ApplyKwargsToObject()
        {
            Type type = obj.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            string[] memberNames = fields.Select(x => x.Name).Concat(properties.Select(x => x.Name)).ToArray();

            foreach (KeyValuePair<string, object> entry in kwargs)
            {
                if (memberNames.Contains(entry.Key))
                {
                    object memberInfo = type.GetField(entry.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    Type mType;
                    bool IsField = true;

                    if (memberInfo == null)
                    {
                        memberInfo = type.GetProperty(entry.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        mType = ((PropertyInfo)memberInfo).PropertyType;
                        IsField = false;
                    }
                    else mType = ((FieldInfo)memberInfo).FieldType;

                    if (entry.Value == null || mType.IsAssignableFrom(entry.Value.GetType()))
                    {
                        if (IsField) ((FieldInfo)memberInfo).SetValue(obj, entry.Value);
                        else ((PropertyInfo)memberInfo).SetValue(obj, entry.Value);
                    }
                    else if (mType.IsEnum)
                    {
                        if (IsField) ((FieldInfo)memberInfo).SetValue(obj, Enum.Parse(mType, entry.Value.ToString()));
                        else ((PropertyInfo)memberInfo).SetValue(obj, Enum.Parse(mType, entry.Value.ToString()));
                    }
                    else
                    {
                        try
                        {
                            if (IsField) ((FieldInfo)memberInfo).SetValue(obj, Convert.ChangeType(entry.Value, mType));
                            else ((PropertyInfo)memberInfo).SetValue(obj, Convert.ChangeType(entry.Value, mType));
                        }
                        catch { }
                    }
                }
            }
            return obj;
        }

    }
    public class HashConverter
    {
        public static T ConvertToClass<T>(in Hashtable ht, MethodType mType = MethodType.FieldOnly) 
            where T : HpBaseModel, new()
        {
            T obj = new();
            AssignToClass(ht, ref obj, mType);
            return obj;
        }
        
        public static T AssignToClass<T>(in Hashtable ht, T obj, MethodType mType = MethodType.FieldOnly)
            where T : HpBaseModel
        {
            Type type = typeof(T);

            foreach (DictionaryEntry entry in ht)
            {
                if (mType == MethodType.PropertyOnly || mType == MethodType.PropertyAndField)
                {
                    PropertyInfo prop = type.GetProperty(entry.Key.ToString(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
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
                if (mType == MethodType.FieldOnly || mType == MethodType.PropertyAndField)
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

			if ( valueOfType == typeof( bool ) && !isEqual ) return null;
            if (isEqual) return value;
            
            return Convert.ChangeType(value, targetType);
        }
        private static bool IsDerivedFromGenericBase(Type type, Type genericBaseType)
        {
            while (type != null && type != typeof(object))
            {
                var currentType = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (currentType == genericBaseType)
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }
    }
}