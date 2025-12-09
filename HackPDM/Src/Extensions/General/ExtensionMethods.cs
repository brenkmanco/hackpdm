using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using HackPDM.ClientUtils;
using HackPDM.Src.ClientUtils.Types;

using Windows.ApplicationModel.Activation;

namespace HackPDM.Extensions.General;

public static class ExtensionMethods
{
	public static int Compare(this int? a, int? b) => Nullable.Compare(a, b);
	public static void RemoveFromIndex<T>(this IList<T> list, int index, bool isInclusive = false)
	{
		if (list == null || index < 0 || index >= list.Count)
			return;

		int start = isInclusive ? index : index + 1;
		if (start >= list.Count)
			return;

		for (int i = list.Count - 1; i >= start; i--)
		{
			list.RemoveAt(i);
		}
	}

	public static T GetAssign<T>(this T obj, Func<T> func) where T : class
    {
        obj ??= func();
        return obj;
    }
    public static T[] Populate<T>(this T[] values, Func<T> func)
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = func();
        }
        return values;
    }
    public static IEnumerable<TOut> Select<TIn, TOut>(this ArrayList list, Func<TIn, TOut> selector)
    {
        foreach (TIn obj in list.OfType<TIn>())
        {
            yield return selector(obj);
        }
    }
    public static IEnumerable<TOut> Select<TIn, TOut>(this Hashtable ht, Func<TIn, TOut> selector)
    {
        foreach (TIn obj in ht.Keys.OfType<TIn>())
        {
            yield return selector(obj);
        }
    }
    public static IEnumerable<TOut> SelectKeysWhere<TIn, TOut>(this Hashtable ht, Func<TIn, TOut> selector, Predicate<TOut> predicate)
    {
        foreach (TIn obj in ht.Keys.OfType<TIn>())
        {
            TOut result = selector(obj);
            bool isPredicate = predicate(result);
            if (isPredicate) yield return selector(obj);
        }
    }
    public static IEnumerable<TOut> SelectKeysWhere<TIn, TOut>(this Hashtable ht, Func<TIn, TOut> selector, Func<TIn, TOut, bool> predicate)
    {
        foreach (TIn obj in ht.Keys.OfType<TIn>())
        {
            TOut result = selector(obj);
            bool isPredicate = predicate(obj, result);
            if (isPredicate) yield return selector(obj);
        }
    }
    public static IEnumerable<TOut> SelectMany<TIn, TOut>(this Hashtable source, Func<TIn, IEnumerable<TOut>> selector)
    {
        foreach (TIn item in source.Keys.OfType<TIn>())
        {
            foreach (var result in selector(item))
            {
                yield return result;
            }
        }
    }
    public static IEnumerable<TOut> SelectMany<TIn, TOut>(this ArrayList source, Func<TIn, IEnumerable<TOut>> selector)
    {
        foreach (TIn item in source.OfType<TIn>())
        {
            foreach (var result in selector(item))
            {
                yield return result;
            }
        }
    }
    public static IEnumerable<TOut> SkipSelect<TIn, TOut>(this IEnumerable<TIn> source, Predicate<TIn> predicate, Func<TIn, TOut> selector)
    {
        foreach (TIn obj in source)
        {
            if (!predicate(obj))
            {
                yield return selector(obj);
            }
        }
    }
	public static IEnumerable<Tout> SkipSelect<TIn, Tout>(this ArrayList source, Predicate<TIn> predicate, Func<TIn, Tout> selector)
	{
		foreach (TIn obj in source.OfType<TIn>())
		{
			if (!predicate(obj))
			{
				yield return selector(obj);
			}
		}
	}
	public static IEnumerable<TOut> SelectNotDefault<TIn, TOut>(this ArrayList list, Func<TIn, TOut?> selector) where TOut : IEquatable<TOut>
	{
		foreach (TIn obj in list.OfType<TIn>())
		{
			var item = selector(obj);
			if (item is { } clean && !clean.Equals(default)) yield return clean;
		}
	}
	public static (List<TOut>, List<TOut>) SegmentWhere<TOut>(this IEnumerable<TOut?> array, Predicate<TOut?> predicate)
	{
		(List<TOut>, List<TOut>) items = new(new(), new());
		foreach (TOut? item in array)
		{
			if (predicate(item)) items.Item1.Add(item);
			else items.Item2.Add(item);
		}
		return items;
	}
	public static IEnumerable<TOut> SkipList<TOut>(this IEnumerable<TOut> source, IEnumerable<TOut> match)
    {
        foreach (TOut obj in source)
        {
            if (!match.Contains(obj))
            {
                yield return obj;
            }
        }
    }
    public static Hashtable TakeWhere(this Hashtable ht, Predicate<DictionaryEntry> predicate)
    {
        Hashtable newHt = [];
        foreach (DictionaryEntry de in ht)
        {
            bool isPredicate = predicate(de);
            if (isPredicate)
            {
                newHt.Add(de.Key, de.Value);
            }
        }
        foreach (DictionaryEntry de in newHt)
        {
            ht.Remove(de.Key);
        }
        return newHt;
    }
    public static List<TOut> TakeAndRemove<TOut>(this List<TOut> source, Func<TOut, bool> predicate)
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
    public static bool TryGetValue<T>(this Hashtable ht, object key, out T? value) 
    {
        if (ht[key] is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
	}
    public static bool TryGetValue<TKey, TVal>(this Hashtable ht, TKey key, out TVal? value) where TKey : notnull
    {
        if (ht[key] is TVal t)
        {
            value = t;
            return true;
        }
        value = default;
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
    public static bool SelectContains<TIn, TOut>(this IEnumerable<TIn> source, TOut value, Func<TIn, TOut> selector)
    {
        foreach (TIn obj in source)
        {
            TOut sourceValue = selector(obj);
            if (sourceValue.Equals(value))
                return true;
        }
        return false;
    }
    public static bool SelectContainsAny<TIn, TOut>(this IEnumerable<TIn> source, IEnumerable<TOut> values, Func<TIn, TOut> selector)
    {
        foreach (TIn obj in source)
        {
            TOut value = selector(obj);
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
    public static string[] SplitByPath(this string str)
    {
        return str.Split(["\\", "/"], StringSplitOptions.RemoveEmptyEntries);
	}
    
    //
    //
    //
    //
    // TODO: finish this
    private static void TakeWhile<T>(this Span<T> span, Predicate<T> predicate)
    {
	    for (int i = 0; i < span.Length; i++)
	    {
		    ref var item = ref span[i];
		    if (predicate(item))
		    {
			    // list[i] = item;
		    }
	    }
    }
	public static bool StartsWith(this Span<char> str, Span<string> list)
	{
		
		BitArray mask = new(list.Length);
		
		for (int index = 0; index < str.Length; index++)
		{
			var current = str[index];
			for (int i = 0; i < list.Length; i++)
			{
				if (mask.Get(i)) continue;
				ref var listStr = ref list[i];
				var listCurrent = listStr[index];

				if (current == listCurrent)
				{
					if (i == list.Length - 1) return true;
				}
				else mask.Set(i, true);
			}
		}
		return false;
	}
	//
	//
	//
	//
	//
	//

	public static ObservableCollection<string> SplitByPathObserve(this string str)
		=> new (str.Split(["\\", "/"], StringSplitOptions.RemoveEmptyEntries));
    public static TArray Split<TArray>(this string str, string[]? delimiters = null, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
        where TArray : IList, new()
    {
        if (delimiters == null || delimiters.Length == 0) delimiters = [" "];
        string[] strSplit = str.Split(delimiters, options);
        TArray tarray = new();
        foreach (string s in strSplit)
        {
            tarray.Add(s);
        }
        return tarray;
    }
    public static TArray Split<TArray>(this string str, string delimiter = " ", StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
        where TArray : IList, new()
    {
        string[] strSplit = str.Split([.. delimiter]);
        TArray tarray = new();
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
	private static void SortInternal<T>(this ObservableCollection<T> oc, Comparison<T> comparer, bool reverse = false)
	{
		// Step 1: Create a sorted snapshot
		var sorted = oc.ToList();
		sorted.Sort((a, b) =>
		{
			int result = comparer(a, b);
			return reverse ? -result : result;
		});

		// Step 2: Reorder the original collection to match the sorted snapshot
		for (int i = 0; i < sorted.Count; i++)
		{
			var item = sorted[i];
			int currentIndex = oc.IndexOf(item);
			if (currentIndex != i)
			{
				oc.Move(currentIndex, i);
			}
		}

		Debug.WriteLine("Finished Sorting");
	}

	extension<T>(ObservableCollection<T> oc)
    {
        public void Sort(Comparison<T> comparer, bool reverse = false) => oc.SortInternal(comparer, reverse);
		public void ReverseSort(Comparison<T> comparer) => oc.SortInternal(comparer, true);
	}
	extension<T>(T obj) where T : class, ICloneable
	{
		public T Cloned() => obj.Clone() as T;
	}
	public static void RenewTokenSource(this CancellationTokenSource? source)
	{
		source?.Cancel();
		source = new();
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
public static class Conversions
{
	public static (T1, T1) Repeat2<T1>(this T1 v1) where T1 : struct
		=> (v1, v1);
	public static (T1, T1, T1) Repeat3<T1>(this T1 v1) where T1 : struct
		=> (v1, v1, v1);
	public static (T1, T1, T1, T1) Repeat4<T1>(this T1 v1) where T1 : struct
		=> (v1, v1, v1, v1);
	public static (T1, T1, T1, T1, T1) Repeat5<T1>(this T1 v1) where T1 : struct
		=> (v1, v1, v1, v1, v1);
	//private static T1[] RepeatTuple<T1>(T1 t1)
}
public class DynamicTuple<T>
{
	private readonly T[] _items;
	public DynamicTuple(params T[] items)
	{
		_items = items;
		ValueTuple<int, int> tup = (1, 2);
		
	}
	public ref T this[int index] => ref _items[index];
	//public T this[int index] => _items[index];
	public int Length => _items.Length;

	public void Deconstruct(out T first, out T second)
	{
		first = _items.Length > 0 ? _items[0] : default!; 
		second = _items.Length > 1 ? _items[1] : default!;
	}
	public void Deconstruct(out T first, out T second, out T third)
	{
		first = _items.Length > 0 ? _items[0] : default!;
		second = _items.Length > 1 ? _items[1] : default!;
		third = _items.Length > 2 ? _items[2] : default!;
	}
	public void Deconstruct(out T a, out T b, out T c, out T d)
	{
		a	= _items.Length > 0		? _items[0]		: default!;
		b	= _items.Length > 1		? _items[1]		: default!;
		c	= _items.Length > 2		? _items[2]		: default!;
		d	= _items.Length > 3		? _items[3]		: default!;
	}
	public void Deconstruct(out T a, out T b, out T c, out T d, out T e)
	{
		a = _items.Length > 0 ? _items[0] : default!;
		b = _items.Length > 1 ? _items[1] : default!;
		c = _items.Length > 2 ? _items[2] : default!;
		d = _items.Length > 3 ? _items[3] : default!;
		e = _items.Length > 4 ? _items[4] : default!;
	}
	public void Deconstruct(out T a, out T b, out T c, out T d, out T e, out T f)
	{
		a = _items.Length > 0 ? _items[0] : default!;
		b = _items.Length > 1 ? _items[1] : default!;
		c = _items.Length > 2 ? _items[2] : default!;
		d = _items.Length > 3 ? _items[3] : default!;
		e = _items.Length > 4 ? _items[4] : default!;
		f = _items.Length > 5 ? _items[5] : default!;
	}
	public void Deconstruct(out T a, out T b, out T c, out T d, out T e, out T f, out T g)
	{
		a = _items.Length > 0 ? _items[0] : default!;
		b = _items.Length > 1 ? _items[1] : default!;
		c = _items.Length > 2 ? _items[2] : default!;
		d = _items.Length > 3 ? _items[3] : default!;
		e = _items.Length > 4 ? _items[4] : default!;
		f = _items.Length > 5 ? _items[5] : default!;
		g = _items.Length > 6 ? _items[6] : default!;
	}
}