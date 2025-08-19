using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using HackPDM.ClientUtils;

namespace HackPDM.Extensions.General
{
    public static class ExtensionMethods
    {
        public static T[] Populate<T>(this T[] values, Func<T> func)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = func();
            }
            return values;
        }
        public static IEnumerable<Tout> Select<Tin, Tout>(this ArrayList list, Func<Tin, Tout> selector)
        {
            foreach (Tin obj in list.OfType<Tin>())
            {
                yield return selector(obj);
            }
        }
        public static IEnumerable<Tout> Select<Tin, Tout>(this Hashtable ht, Func<Tin, Tout> selector)
        {
            foreach (Tin obj in ht.Keys.OfType<Tin>())
            {
                yield return selector(obj);
            }
        }
        public static IEnumerable<Tout> SelectKeysWhere<Tin, Tout>(this Hashtable ht, Func<Tin, Tout> selector, Predicate<Tout> predicate)
        {
            foreach (Tin obj in ht.Keys.OfType<Tin>())
            {
                Tout result = selector(obj);
                bool isPredicate = predicate(result);
                if (isPredicate) yield return selector(obj);
            }
        }
        public static IEnumerable<Tout> SelectKeysWhere<Tin, Tout>(this Hashtable ht, Func<Tin, Tout> selector, Func<Tin, Tout, bool> predicate)
        {
            foreach (Tin obj in ht.Keys.OfType<Tin>())
            {
                Tout result = selector(obj);
                bool isPredicate = predicate(obj, result);
                if (isPredicate) yield return selector(obj);
            }
        }
        public static IEnumerable<Tout> SelectMany<Tin, Tout>(this Hashtable source, Func<Tin, IEnumerable<Tout>> selector)
        {
            foreach (Tin item in source.Keys.OfType<Tin>())
            {
                foreach (var result in selector(item))
                {
                    yield return result;
                }
            }
        }
        public static IEnumerable<Tout> SelectMany<Tin, Tout>(this ArrayList source, Func<Tin, IEnumerable<Tout>> selector)
        {
            foreach (Tin item in source.OfType<Tin>())
            {
                foreach (var result in selector(item))
                {
                    yield return result;
                }
            }
        }
        public static IEnumerable<Tout> SkipSelect<Tin, Tout>(this IEnumerable<Tin> source, Predicate<Tin> predicate, Func<Tin, Tout> selector)
        {
            foreach (Tin obj in source.OfType<Tin>())
            {
                if (!predicate(obj))
                {
                    yield return selector(obj);
                }
            }
        }
        public static IEnumerable<Tout> SkipList<Tout>(this IEnumerable<Tout> source, IEnumerable<Tout> match)
        {
            foreach (Tout obj in source)
            {
                if (!match.Contains(obj))
                {
                    yield return obj;
                }
            }
        }
        public static Hashtable TakeWhere(this Hashtable ht, Predicate<DictionaryEntry> predicate)
        {
            Hashtable newHT = [];
            foreach (DictionaryEntry de in ht)
            {
                bool isPredicate = predicate(de);
                if (isPredicate)
                {
                    newHT.Add(de.Key, de.Value);
                }
            }
            foreach (DictionaryEntry de in newHT)
            {
                ht.Remove(de.Key);
            }
            return newHT;
        }
        public static List<Tout> TakeAndRemove<Tout>(this List<Tout> source, Func<Tout, bool> predicate)
        {
            var takenElements = source.Where(predicate).ToList();

            // Remove the elements that match the predicate
            foreach (var element in takenElements)
            {
                source.Remove(element);
            }

            return takenElements;
        }
        public static bool TryGetValue(this Hashtable ht, object key, out object value)
        {
            value = ht[key];
            if (value != null) return true;
            return false;
        }
        public static bool ContainsAny<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> values)
        {
            foreach (TSource value in values)
            {
                foreach (TSource item in source)
                {
                    if (item.Equals(value)) return true;
                }
            }
            return false;
        }
        public static bool SelectContains<Tin, Tout>(this IEnumerable<Tin> source, Tout value, Func<Tin, Tout> selector)
        {
            foreach (Tin obj in source)
            {
                Tout sourceValue = selector(obj);
                if (sourceValue.Equals(value))
                    return true;
            }
            return false;
        }
        public static bool SelectContainsAny<Tin, Tout>(this IEnumerable<Tin> source, IEnumerable<Tout> values, Func<Tin, Tout> selector)
        {
            foreach (Tin obj in source)
            {
                Tout value = selector(obj);
                if (values.Contains(value)) return true;
            }
            return false;
        }
        public static IEnumerable<object> Flatten(this IEnumerable source)
        {
            foreach (object obj in source)
            {
                if (obj is IEnumerable ie)
                {
                    foreach (var nestedItem in ie.Flatten())
                    {
                        yield return nestedItem;
                    }
                }
                else
                {
                    yield return obj;
                }
            }
        }
        public static Tarray Split<Tarray>(this string str, string delimiter = " ", StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
            where Tarray : IList, new()
        {
            string[] strSplit = str.Split([.. delimiter]);
            Tarray tarray = new();
            foreach (string s in strSplit)
            {
                tarray.Add(s);
            }
            return tarray;
        }
        public static bool GetFileEndType(this string str, out string extension)
        {
            extension = null;
            for (int i = str.Length - 1; i >= 0; i--)
            {
                if (str[i] == '.')
                {
                    extension = str.Substring(i + 1).ToLower();
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Determines whether <paramref name="str"/> contains only empty space.
        /// Tests against ' ' \n \t \f \r
        /// </summary>
        /// <param name="str">The string.</param>
        /// <returns>
        ///   <c>true</c> if <paramref name="str"/> is empty space; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsEmptySpace(this string str)
        {
            foreach (char c in str)
            {
                if (
                    !(c == ' '
                    || c == '\n'
                    || c == '\t'
                    || c == '\f'
                    || c == '\r'))
                {
                    return false;
                }
            }
            return true;
        }
        public static bool MoveFile(this FileInfo file, string toPath)
        {
            try
            {
                if (!Directory.Exists(toPath) && !Directory.CreateDirectory(toPath).Exists) return false;

                string toFilePath = Path.Combine(toPath, file.Name);

                if (file.Exists)
                {
                    var newFile = file.CopyTo(toFilePath, true);
                    file.Delete();
                    file = newFile;
                }
                else return false;
            }
            catch
            {
                return false;
            }
            return true;
        }
        public static FileInfo CopyFile(this FileInfo file, string toPath)
        {
            try
            {
                if (!Directory.Exists(toPath) && !Directory.CreateDirectory(toPath).Exists) return null;

                string toFilePath = Path.Combine(toPath, file.Name);

                if (file.Exists)
                {
                    return file.CopyTo(toFilePath, true);
                }
                else return null;
            }
            catch { }
            return null;
        }
        public static HashSet<T> AddAll<T>(this HashSet<T> hashset, IEnumerable<T> values)
        {
            foreach (T value in values)
            {
                hashset.Add(value);
            }
            return hashset;
        }
    }
    public static class ExtensionConvertMethods
    {
        public static T[] ToArray<T>(this ArrayList list) => [.. list.Cast<T>()];
        public static HashSet<T> ToHashSet<T>(this ArrayList list) => [.. list.Cast<T>()];
        public static ConcurrentBag<T> ToConcurrentBag<T>(this IEnumerable<T> items)
        {
            try { return [.. items]; }
            catch { return null; }
        }
        public static ConcurrentBag<T> ToConcurrentBag<T>(this IEnumerable items)
        {
            return items.Cast<object>().ToConcurrentBag<T>();
        }
        public static ConcurrentSet<T> ToConcurrentSet<T>(this IEnumerable list)
            => list.Cast<T>().ToConcurrentSet();
        public static ConcurrentSet<T> ToConcurrentSet<T>(this IEnumerable<T> list)
        {
            ConcurrentSet<T> set = [.. list];
            return set;
        }
        public static ArrayList ToArrayList<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return [.. source];
        }
        public static ArrayList ToArrayList(this IEnumerable source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return [.. source];
        }
        public static byte[] ToBytes(this Image image) => FileOperations.ImageToByteArray(image);
        public static string ToBase64String(this Image image) => Convert.ToBase64String(image.ToBytes());
    }
    public static class FileInfoExtensions
    {
        extension(FileInfo file)
        {
            //public string Checksum
            //{
            //    get
            //    {

            //        if (file.Exists && file.Length > 0)
            //        {
            //            using SHA1 sha = SHA1.Create();
            //            using FileStream stream = file.OpenRead();
            //            byte[] bytearray = sha.ComputeHash(stream);

            //            return string.Join("", bytearray.Select(i => i.ToString("X2")));
            //        }

            //        return file.Checksum;
            //    }
            //}
        }
    }
}
