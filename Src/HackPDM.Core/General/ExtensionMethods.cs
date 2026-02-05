using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using HackPDM.Core.Hack;
using HackPDM.Domain.Helper;
using HackPDM.Domain.OdooModels.Models;

namespace HackPDM.Core.General;

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
    public static IList AddRange(this IList list, IEnumerable items)
    {
        foreach (var item in items)
        {
            list.Add(item);
        }
        return list;
    }
	public static T GetAssign<T>(this T obj, Func<T> func) where T : class
    {
        obj ??= func();
        return obj;
    }
    public static T? FirstOrDefault<T>(this ArrayList? list, Predicate<T>? predicate = null) where T : class
    {
        if (list is not {Count: > 0}) return default;
        if (predicate is null) return list[0] as T;

		foreach (T obj in list.OfType<T>())
        {
            if (predicate is { } pred && pred(obj))
            {
                return obj;
            }
        }
        return default;
    }
    public static T[] Populate<T>(this T[] values, Func<T> func)
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = func();
        }
        return values;
    }
	
    private static IEnumerable<(bool, TOut?)> SegmentSelectInternal<TIn, TOut>(IEnumerable<TIn?> array, Func<TIn?, (bool, TOut?)> predicateSelect, bool skipNullItem = true)
    {
        foreach (TIn? item in array)
        {
            if (skipNullItem && item is null) continue;

            var value = predicateSelect(item);
			
            yield return (value.Item1, value.Item2);
		}
    }
	private static IEnumerable<(bool, TOut?)> SegmentSelectInternal<TIn, TOut>(IEnumerable<TIn?> array, Func<TIn?, int, (bool, TOut?)> predicateSelect, bool skipNullItem = true)
	{
        int index = -1;
		foreach (TIn? item in array)
		{
            checked
            {
                index++;
            }
			if (skipNullItem && item is null) continue;

			var value = predicateSelect(item, index);

			yield return (value.Item1, value.Item2);
		}
	}
	private static IEnumerable<(bool, TOut?, TOut2?)> SegmentSelectDiffInternal<TIn, TOut, TOut2>(IEnumerable<TIn?> array, Func<TIn?, int, (bool, TOut?, TOut2?)> predicateSelect, bool skipNullItem = true)
	{
		int index = -1;
		foreach (TIn? item in array)
		{
			checked
			{
				index++;
			}
			if (skipNullItem && item is null) continue;

			var value = predicateSelect(item, index);

			yield return (value.Item1, value.Item2, value.Item3);
		}
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
	public static HashSet<T> AddAll<T>(this HashSet<T> hashset, IEnumerable<T> values)
	{
		foreach (T value in values)
		{
			hashset.Add(value);
		}
		return hashset;
	}
	public static void RenewTokenSource(this CancellationTokenSource? source)
	{
		source?.Cancel();
		source = new();
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
    
    extension(ArrayList source)
    {
	    public bool Any<T>(Func<T, bool> predicate)
	    {
		    foreach (T obj in source.OfType<T>())
		    {
			    if (predicate(obj))
			    {
				    return true;
			    }
		    }
		    return false;
	    }

	    public IEnumerable<TOut> Select<TIn, TOut>(Func<TIn, TOut> selector)
	    {
		    foreach (TIn obj in source.OfType<TIn>())
		    {
			    yield return selector(obj);
		    }
	    }
	    public IEnumerable<Tout> SkipSelect<TIn, Tout>(Predicate<TIn> predicate, Func<TIn, Tout> selector)
	    {
		    foreach (TIn obj in source.OfType<TIn>())
		    {
			    if (!predicate(obj))
			    {
				    yield return selector(obj);
			    }
		    }
	    }

	    public IEnumerable<TOut> SelectNotDefault<TIn, TOut>(Func<TIn, TOut?> selector) where TOut : IEquatable<TOut>
	    {
		    foreach (TIn obj in source.OfType<TIn>())
		    {
			    var item = selector(obj);
			    if (item is { } clean && !clean.Equals(default)) yield return clean;
		    }
	    }

	    public IEnumerable<T> SelectMany<TIn, T>(Func<TIn, IEnumerable<T>> selector)
	    {
		    foreach (TIn item in source.OfType<TIn>())
		    {
			    foreach (var result in selector(item))
			    {
				    yield return result;
			    }
		    }
	    }
    }
    extension(Hashtable ht)
    {
	    public IEnumerable<TOut> Select<TIn, TOut>(Func<TIn, TOut> selector)
	    {
		    foreach (TIn obj in ht.Keys.OfType<TIn>())
		    {
			    yield return selector(obj);
		    }
	    }

	    public IEnumerable<TOut> SelectKeysWhere<TIn, TOut>(Func<TIn, TOut> selector, Predicate<TOut> predicate)
	    {
		    foreach (TIn obj in ht.Keys.OfType<TIn>())
		    {
			    TOut result = selector(obj);
			    bool isPredicate = predicate(result);
			    if (isPredicate) yield return selector(obj);
		    }
	    }

	    public IEnumerable<TOut> SelectKeysWhere<TIn, TOut>(Func<TIn, TOut> selector, Func<TIn, TOut, bool> predicate)
	    {
		    foreach (TIn obj in ht.Keys.OfType<TIn>())
		    {
			    TOut result = selector(obj);
			    bool isPredicate = predicate(obj, result);
			    if (isPredicate) yield return selector(obj);
		    }
	    }

	    public IEnumerable<TOut> SelectMany<TIn, TOut>(Func<TIn, IEnumerable<TOut>> selector)
	    {
		    foreach (TIn item in ht.Keys.OfType<TIn>())
		    {
			    foreach (var result in selector(item))
			    {
				    yield return result;
			    }
		    }
	    }
	    public bool TryGetValue(object key, out object? value)
	    {
		    value = ht[key];
		    if (value != null) return true;
		    return false;
	    }

	    public bool TryGetValue<T>(object key, out T? value) 
	    {
		    if (ht[key] is T t)
		    {
			    value = t;
			    return true;
		    }
		    value = default;
		    return false;
	    }

	    public bool TryGetValue<TKey, TVal>(TKey key, out TVal? value) where TKey : notnull
	    {
		    if (ht[key] is TVal t)
		    {
			    value = t;
			    return true;
		    }
		    value = default;
		    return false;
	    }
	    public Hashtable TakeWhere(Predicate<DictionaryEntry> predicate)
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
    }
	extension(DictionaryEntry entry)
	{
		public void Deconstruct<TK, TV>(out TK? key, out TV? value)
		{
			key = (TK?)entry.Key;
			value = (TV?)entry.Value;
		}
		public void Deconstruct(out object key, out object value)
		{
			key = entry.Key;
			value = entry.Value;
		}
	}

	/// <param name="str">The string.</param>
	extension(string str)
	{
		public ObservableCollection<string> SplitByPathObserve()
			=> new (str.Split(["\\", "/"], StringSplitOptions.RemoveEmptyEntries));

		public TArray Split<TArray>(string[]? delimiters = null, StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
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
		
		public IEnumerable<string> SplitBy(Func<string, char, bool> evaluater)
		{
			string currentSection = "";
			for( int i = 0; i < str.Length; i++ )
			{
				currentSection += str[i];
				if( evaluater( currentSection, str[i] ) )
				{
					yield return currentSection;
					currentSection = "";
				}
			}
			if( currentSection.Length > 0 )
				yield return currentSection;
		}

		public TArray Split<TArray>(string delimiter = " ", StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
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

		public bool GetFileEndType(out string extension)
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
		/// <returns>
		///   <c>true</c> if <paramref name="str"/> is empty space; otherwise, <c>false</c>.
		/// </returns>
		public bool IsEmptySpace()
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

		public string[] SplitByPath()
		{
			return str.Split(["\\", "/"], StringSplitOptions.RemoveEmptyEntries);
		}
	}
	extension(FileInfo file)
	{
		public bool MoveFile(string toPath)
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

		public FileInfo CopyFile(string toPath)
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
	}

	extension<T>(ObservableCollection<T> oc)
    {
        public void Sort(Comparison<T> comparer, bool reverse = false) => oc.SortInternal(comparer, reverse);
		public void ReverseSort(Comparison<T> comparer) => oc.SortInternal(comparer, true);

		private void SortInternal(Comparison<T> comparer, bool reverse = false)
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
    }
	extension<T>(T obj) where T : class, ICloneable
	{
		public T Cloned() => obj.Clone() as T;
	}
	extension<T>(IEnumerable<T?> array)
    {
	    public (IEnumerable<TOut?>, IEnumerable<TOut?>) SegmentSelectWhere<TOut>(Func<T?, (bool, TOut?)> predicateSelect, bool skipNullItem = true)
	    {
		    var segmentList = SegmentSelectInternal(array, predicateSelect, skipNullItem);
		    return (segmentList.SkipSelect(item => (!item.Item1, item.Item2)), segmentList.SkipSelect(item => item));
	    }

		public (IEnumerable<TOut?>, IEnumerable<TOut2?>) SegmentSelectDiffWhere<TOut, TOut2>(Func<T?, int, (bool, TOut?, TOut2?)> predicateSelect, bool skipNullItem = true)
	    {
		    var segmentList = SegmentSelectDiffInternal(array, predicateSelect, skipNullItem);
		    return (segmentList.SkipSelect(item => (!item.Item1, item.Item2)), segmentList.SkipSelect(item => (item.Item1, item.Item3)));
	    }

	    public (IEnumerable<TOut?>, IEnumerable<TOut?>) SegmentSelectWhere<TOut>(Func<T?, int, (bool, TOut?)> predicateSelect, bool skipNullItem = true)
	    {
		    var segmentList = SegmentSelectInternal(array, predicateSelect, skipNullItem);
		    return (segmentList.SkipSelect(item => (!item.Item1, item.Item2)), segmentList.SkipSelect(item => item));
	    }

	    public IEnumerable<TOut?> LambdaContextual<TContext, TOut>(TContext context, Func<TContext, T?, (bool, TOut?)> predicateSelect)
	    {
		    foreach (T? item in array)
		    {
			    var res = predicateSelect(context, item);
			    if (res.Item1) yield return res.Item2;
		    }
	    }

	    public (List<T?>, List<T?>) SegmentWhere(Predicate<T?> predicate)
	    {
		    (List<T?>, List<T?>) items = new(new(), new());
		    foreach (T? item in array)
		    {
			    if (predicate(item)) items.Item1.Add(item);
			    else items.Item2.Add(item);
		    }
		    return items;
	    }

		public IEnumerable<T> SkipNull()
		{
			foreach (T? item in array)
			{
				if (item is { }) yield return item;
			}
		}

		public IEnumerable<TOut> SkipNullSelect<TOut>(Func<T, TOut> func) 
		{
			foreach (T? item in array)
			{
				if (item is T notnullItem) yield return func(notnullItem);
			}
		}
	}
    extension<T>(IEnumerable<T> source)
    {
	    public IEnumerable<TOut> SkipSelect<TOut>(Func<T, bool> predicate, Func<T, TOut> selector)
	    {
		    foreach (T obj in source)
		    {
			    if (!predicate(obj))
			    {
				    yield return selector(obj);
			    }
		    }
	    }
	    public IEnumerable<TOut?> SkipSelect<TOut>(Func<T, (bool, TOut)> predicateSelector)
	    {
		    foreach (T obj in source)
		    {
			    var result = predicateSelector(obj);
			    if (!result.Item1)
			    {
				    yield return result.Item2;
			    }
		    }
	    }
	    public bool ContainsAny(IEnumerable<T> values)
	    {
		    foreach (T value in values)
		    {
			    foreach (T item in source)
			    {
				    if (item?.Equals(value) is true) return true;
			    }
		    }
		    return false;
	    }
	    public bool SelectContains<TOut>(TOut value, Func<T, TOut> selector)
	    {
		    foreach (T obj in source)
		    {
			    TOut sourceValue = selector(obj);
			    if (sourceValue?.Equals(value) is true)
				    return true;
		    }
		    return false;
	    }
	    public bool SelectContainsAny<TOut>(IEnumerable<TOut> values, Func<T, TOut> selector)
	    {
		    foreach (T obj in source)
		    {
			    TOut value = selector(obj);
			    if (values.Contains(value)) return true;
		    }
		    return false;
	    }
	    public IEnumerable<T> SkipList(IEnumerable<T> match)
	    {
		    foreach (T obj in source)
		    {
			    if (!match.Contains(obj))
			    {
				    yield return obj;
			    }
		    }
	    }
		public TOut FirstSelect<TOut>(Func<T, (bool, TOut)> predicateSelect)
		{
			foreach (T item in source)
			{
				var result = predicateSelect(item);
				if( result.Item1 )
					return result.Item2;
			}
			throw new Exception("not found in enumerable");
		}
		public TOut? FirstOrDefaultSelect<TOut>( Func<T, (bool, TOut)> predicateSelect )
		{
			foreach( T item in source )
			{
				var result = predicateSelect(item);
				if( result.Item1 )
					return result.Item2;
			}
			return default;
		}
		public IEnumerable<(T, T2)> PopulateZip<T2>(Func<T, T2> func)
	    {
		    foreach (T obj in source)
		    {
			    yield return (obj, func(obj));
		    }
	    }
    }
	extension(IEnumerable source)
	{
		public TOut? FirstOrDefaultSelect<TOut>( Func<object, (bool, TOut)> predicateSelect )
			=> source.Cast<object>().FirstOrDefaultSelect( predicateSelect );
	}

}
public static class ExtensionConvertMethods
{
	public static HackFile[] ToHackArray(this IEnumerable<FileInfo> fileInfos)
		=> [.. fileInfos.Select(file => new HackFile(file))];
	public static ArrayList ToArrayListIDs<T>(this IEnumerable<T> source) where T : HpBaseModel, new()
	{
		ArrayList ids = [];
		foreach (T model in source)
		{
			ids.Add(model.id);
		}
		return ids;
	}
	
	extension(ArrayList list)
	{
		public T[] ToArray<T>() => [.. list.Cast<T>()];
		public HashSet<T> ToHashSet<T>() => [.. list.Cast<T>()];
	}
	extension(IEnumerable items)
    {
	    public ConcurrentBag<T> ToConcurrentBag<T>()
	    {
		    return items.Cast<object>().ToConcurrentBag<T>();
	    }

	    public ConcurrentSet<T> ToConcurrentSet<T>()
		    => items.Cast<T>().ToConcurrentSet();

	    public ArrayList ToArrayList()
	    {
		    if (items == null)
			    throw new ArgumentNullException(nameof(items));

		    return [.. items];
	    }
    }
    extension<T>(IEnumerable<T> list)
    {
	    public ConcurrentSet<T> ToConcurrentSet()
	    {
		    ConcurrentSet<T> set = [.. list];
		    return set;
	    }

	    public ArrayList ToArrayList()
	    {
		    if (list == null)
			    throw new ArgumentNullException(nameof(list));

		    return [.. list];
	    }

	    public ConcurrentBag<T> ToConcurrentBag()
	    {
		    try { return [.. list]; }
		    catch { return null; }
	    }
    }

    //public static byte[] ToBytes(this Image image) => FileOperations.ImageToByteArray(image);
    //public static string ToBase64String(this Image image) => Convert.ToBase64String(image.ToBytes());
}
public static class Conversions
{
	extension<T1>(T1 v1) where T1 : struct
	{
		public (T1, T1) Repeat2() => (v1, v1);
		public (T1, T1, T1) Repeat3() => (v1, v1, v1);
		public (T1, T1, T1, T1) Repeat4() => (v1, v1, v1, v1);
		public (T1, T1, T1, T1, T1) Repeat5() => (v1, v1, v1, v1, v1);
	}

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
public static class UnsafeExtensions
{
	public delegate ref TOut RefSelector<TIn, TOut>(ref TIn input);
	extension<T>(Span<T> span)
	{
		public void TakeWhile( Predicate<T> predicate )
		{
			for( int i = 0; i < span.Length; i++ )
			{
				ref var item = ref span[i];
				if( predicate( item ) )
				{
					// list[i] = item;
				}
			}
		}
		public Span<T> Skip(Func<T, bool> predicate)
		{
			T[] temp = new T[span.Length]; int count = 0; for (int i = 0; i < span.Length; i++)
			{
				T val = span[i]; // safe indexing
				if (!predicate(val)) 
				{ 
					temp[count++] = val; 
				} 
			} 
			return temp.AsSpan(0, count); 
		}
		public ref TOut[] RefSelect<TOut>(
			RefSelector<T, TOut> selector,
			ref TOut[] unpopOuts)
		{
			for (int i = 0; i < span.Length; i++)
			{
				ref T val = ref span[i];
				ref TOut unpopItem = ref selector(ref val);
				unpopOuts[i] = unpopItem;
			}
			return ref unpopOuts;
		}
		public Span<T> SkipUnsafe(Func<T, bool> predicate) 
		{
			T[] temp = new T[span.Length];
			int count = 0;

			ref T start = ref MemoryMarshal.GetReference(span);

			for (int i = 0; i < span.Length; i++)
			{
				T val = Unsafe.Add(ref start, i);
				if (!predicate(val))
				{
					temp[count++] = val;
				}
			}

			return temp.AsSpan(0, count);
		}
	}
	extension<TIn, TOut>(Span<(TIn, TOut)> span)
	{
		public TIn[] SelectFirst()
		{
			TIn[] temp = new TIn[span.Length];
			for (int i = 0; i < span.Length; i++)
			{
				ref TIn item = ref span[i].Item1;
				temp[i] = item;
			}
			return temp;
		}
		public TOut[] SelectSecond()
		{
			TOut[] temp = new TOut[span.Length];
			for (int i = 0; i < span.Length; i++)
			{
				ref TOut item = ref span[i].Item2;
				temp[i] = item;
			}
			return temp;
		}
		public Span<TOut> SelectSecondSpan() => span.SelectSecond();
	}
}