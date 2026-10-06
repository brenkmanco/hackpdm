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
	public static HashSet<T> AddAll<T>(this HashSet<T> hashset, IEnumerable<T> values)
	{
		foreach (T value in values)
		{
			hashset.Add(value);
		}
		return hashset;
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
		public IEnumerable this[Range range]
		{
			get
			{
				for( int i = range.Start.Value; i < range.End.Value; i++ )
				{
					yield return source[ i ];
				}
			}
		}
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
	    public IEnumerable<TOut> SelectNotDefault<TIn, TOut>(Func<TIn, TOut?> selector) where TOut : IEquatable<TOut>
	    {
		    foreach (TIn obj in source.OfType<TIn>())
		    {
			    var item = selector(obj);
			    if (item is { } clean && !clean.Equals(default)) yield return clean;
		    }
	    }
    }
    extension(Hashtable ht)
    {
	    public bool TryGetValue(object key, out object? value)
	    {
		    value = ht[key];
		    if (value != null) return true;
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
    }

	/// <param name="str">The string.</param>
	extension(string str)
	{
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
		public void Sort<TKey>(Func<T, TKey> keySelector, bool reverse = false) => oc.SortInternal(keySelector, reverse);
		
		public void ReverseSort(Comparison<T> comparer) => oc.SortInternal(comparer, true);
		public void ReverseSort<TKey>( Func<T, TKey> keySelector ) => oc.SortInternal( keySelector, true );

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
		private void SortInternal<TKey>(Func<T, TKey> keySelector, bool reverse = false)
		{
			var ocSort = reverse ? oc.OrderByDescending(keySelector).ToList() :
			oc.OrderBy( keySelector ).ToList();

			ocSort.ForEach( item =>
			{
				int currentIndex = oc.IndexOf(item);
				int targetIndex = oc.IndexOf(item);
				if( currentIndex != targetIndex )
				{
					oc.Move( currentIndex, targetIndex );
				}
			} );
		}
    }
	extension<T>(IEnumerable<T?> array)
    {
		public (IEnumerable<TOut?>, IEnumerable<TOut2?>) SegmentSelectDiffWhere<TOut, TOut2>(Func<T?, int, (bool, TOut?, TOut2?)> predicateSelect, bool skipNullItem = true)
	    {
		    var segmentList = SegmentSelectDiffInternal(array, predicateSelect, skipNullItem);
		    return (segmentList.SkipSelect(item => (!item.Item1, item.Item2)), segmentList.SkipSelect(item => (item.Item1, item.Item3)));
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