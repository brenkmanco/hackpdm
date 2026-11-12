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

/// <summary>General-purpose extension methods for collections, strings and files.</summary>
public static class ExtensionMethods
{
	
	/// <summary>Compares two nullable ints, with <see langword="null"/> ordered before any value.</summary>
	/// <param name="a">The first value.</param>
	/// <param name="b">The second value.</param>
	/// <returns>Less than 0, 0 or greater than 0, as <see cref="System.Nullable.Compare{T}(T?, T?)"/>.</returns>
	public static int Compare(this int? a, int? b) => Nullable.Compare(a, b);
    /// <summary>Adds every item of a sequence to a non-generic list.</summary>
    /// <param name="list">The list to add to.</param>
    /// <param name="items">The items to add.</param>
    /// <returns>The same <paramref name="list"/>, for chaining.</returns>
    public static IList AddRange(this IList list, IEnumerable items)
    {
        foreach (var item in items)
        {
            list.Add(item);
        }
        return list;
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
	/// <summary>Lazily flattens nested sequences into a single sequence of their leaf items.</summary>
	/// <param name="source">The sequence, whose items may themselves be sequences.</param>
	/// <returns>The non-enumerable items, depth first.</returns>
	/// <remarks>Strings are <see cref="System.Collections.IEnumerable"/> too, so a string is flattened into its characters.</remarks>
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
	/// <summary>Adds every value of a sequence to a hash set.</summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="hashset">The set to add to.</param>
	/// <param name="values">The values to add; duplicates are ignored.</param>
	/// <returns>The same <paramref name="hashset"/>, for chaining.</returns>
	public static HashSet<T> AddAll<T>(this HashSet<T> hashset, IEnumerable<T> values)
	{
		foreach (T value in values)
		{
			hashset.Add(value);
		}
		return hashset;
	}
	/// <summary>Removes the elements that match a predicate from a list and returns them.</summary>
	/// <typeparam name="TOut">The element type.</typeparam>
	/// <param name="source">The list; the matching elements are removed from it.</param>
	/// <param name="predicate">Selects the elements to take.</param>
	/// <returns>The removed elements, in their original order.</returns>
	/// <remarks>Each removal is <see cref="System.Collections.Generic.List{T}.Remove(T)"/>, which removes the first equal element.</remarks>
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
    
	/// <param name="str">The string.</param>
	extension(string str)
	{
		/// <summary>Splits the string and collects the parts into a new list of type <typeparamref name="TArray"/>.</summary>
		/// <typeparam name="TArray">The list type to create and fill.</typeparam>
		/// <param name="delimiter">The separator characters: the string splits on each character of it, not on the whole string.</param>
		/// <param name="options">Currently ignored: empty parts are kept.</param>
		/// <returns>A new <typeparamref name="TArray"/> with the parts.</returns>
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
	/// <param name="file">The file to move.</param>
	extension(FileInfo file)
	{
		/// <summary>Moves the file into another folder by copying it there (overwriting) and deleting the original.</summary>
		/// <param name="toPath">The destination folder; created when missing.</param>
		/// <returns><see langword="true"/> on success; <see langword="false"/> when the file doesn't exist or any step fails (exceptions are swallowed).</returns>
		/// <remarks>The caller's <see cref="System.IO.FileInfo"/> is not updated and still points at the old, deleted path.</remarks>
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

	}

	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="oc">The collection to sort in place.</param>
	extension<T>(ObservableCollection<T> oc)
    {
        /// <summary>Sorts the collection in place with a comparison, moving items so bindings see moves rather than a reset.</summary>
        /// <param name="comparer">The comparison.</param>
        /// <param name="reverse">Whether to sort in descending order.</param>
        public void Sort(Comparison<T> comparer, bool reverse = false) => oc.SortInternal(comparer, reverse);
		/// <summary>Sorts the collection in place by a key, moving items so bindings see moves rather than a reset.</summary>
		/// <typeparam name="TKey">The key type; compared with its default comparer.</typeparam>
		/// <param name="keySelector">Gets the sort key of an item.</param>
		/// <param name="reverse">Whether to sort in descending order.</param>
		public void Sort<TKey>(Func<T, TKey> keySelector, bool reverse = false) => oc.SortInternal(keySelector, reverse);
		

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

			for( int targetIndex = 0; targetIndex < ocSort.Count; targetIndex++ )
			{
				int currentIndex = oc.IndexOf(ocSort[targetIndex]);
				if( currentIndex != targetIndex )
				{
					oc.Move( currentIndex, targetIndex );
				}
			}
		}
    }
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="array">The source sequence.</param>
	extension<T>(IEnumerable<T?> array)
    {
		/// <summary>Splits a sequence in two, projecting each item differently depending on which side it falls.</summary>
		/// <typeparam name="TOut">The projection type of items that match.</typeparam>
		/// <typeparam name="TOut2">The projection type of items that don't match.</typeparam>
		/// <param name="predicateSelect">For an item and its index: whether it matches, its projection if it does, and its projection if it doesn't.</param>
		/// <param name="skipNullItem">Whether <see langword="null"/> items are skipped (indexes still count them).</param>
		/// <returns>The projections of the matching items, and those of the others; both are lazy.</returns>
		/// <remarks>Each returned sequence re-runs <paramref name="predicateSelect"/> over the whole source when enumerated.</remarks>
		public (IEnumerable<TOut?>, IEnumerable<TOut2?>) SegmentSelectDiffWhere<TOut, TOut2>(Func<T?, int, (bool, TOut?, TOut2?)> predicateSelect, bool skipNullItem = true)
	    {
		    var segmentList = SegmentSelectDiffInternal(array, predicateSelect, skipNullItem);
		    return (segmentList.SkipSelect(item => (!item.Item1, item.Item2)), segmentList.SkipSelect(item => (item.Item1, item.Item3)));
	    }
	    /// <summary>Partitions a sequence into the items that match a predicate and those that don't.</summary>
	    /// <param name="predicate">The test.</param>
	    /// <returns>The matching items and the other items, each in source order.</returns>
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
		/// <summary>Projects the non-null items of a sequence, lazily.</summary>
		/// <typeparam name="TOut">The projection type.</typeparam>
		/// <param name="func">The projection.</param>
		/// <returns>The projections of the items that are not <see langword="null"/>.</returns>
		public IEnumerable<TOut> SkipNullSelect<TOut>(Func<T, TOut> func) 
		{
			foreach (T? item in array)
			{
				if (item is T notnullItem) yield return func(notnullItem);
			}
		}
	}
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source sequence.</param>
    extension<T>(IEnumerable<T> source)
    {
	    /// <summary>Projects the items that do not match a predicate (a "where not, then select"), lazily.</summary>
	    /// <typeparam name="TOut">The projection type.</typeparam>
	    /// <param name="predicate">Items for which this returns <see langword="true"/> are skipped.</param>
	    /// <param name="selector">The projection of the kept items.</param>
	    /// <returns>The projections of the items not skipped.</returns>
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
	    /// <summary>Computes a skip flag and a value for each item in one call, and yields the values of the items not skipped.</summary>
	    /// <typeparam name="TOut">The value type.</typeparam>
	    /// <param name="predicateSelector">Returns (skip, value) for an item.</param>
	    /// <returns>The values whose skip flag was <see langword="false"/>, lazily.</returns>
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
		/// <summary>Finds the first item that matches and returns a value computed with the match.</summary>
		/// <typeparam name="TOut">The value type.</typeparam>
		/// <param name="predicateSelect">Returns (matches, value) for an item.</param>
		/// <returns>The value for the first matching item, or <c>default</c> when none matches.</returns>
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
		/// <summary>Pairs each item with a value computed from it, lazily.</summary>
		/// <typeparam name="T2">The computed value's type.</typeparam>
		/// <param name="func">Computes the value for an item.</param>
		/// <returns>(item, value) pairs in source order.</returns>
		public IEnumerable<(T, T2)> PopulateZip<T2>(Func<T, T2> func)
	    {
		    foreach (T obj in source)
		    {
			    yield return (obj, func(obj));
		    }
	    }
    }
	/// <param name="source">The source sequence.</param>
	extension(IEnumerable source)
	{
		/// <summary>Finds the first item of a non-generic sequence that matches and returns a value computed with the match.</summary>
		/// <typeparam name="TOut">The value type.</typeparam>
		/// <param name="predicateSelect">Returns (matches, value) for an item.</param>
		/// <returns>The value for the first matching item, or <c>default</c> when none matches.</returns>
		public TOut? FirstOrDefaultSelect<TOut>( Func<object, (bool, TOut)> predicateSelect )
			=> source.Cast<object>().FirstOrDefaultSelect( predicateSelect );
	}

}
/// <summary>Extension methods that convert sequences to other collection types.</summary>
public static class ExtensionConvertMethods
{
	/// <summary>Creates a <see cref="HackPDM.Core.Hack.HackFile"/> for each file.</summary>
	/// <param name="fileInfos">The files.</param>
	/// <returns>The files' descriptions, in order.</returns>
	/// <remarks>Reads and hashes every existing file (see <see cref="HackPDM.Core.Hack.HackFile(System.IO.FileInfo)"/>).</remarks>
	public static HackFile[] ToHackArray(this IEnumerable<FileInfo> fileInfos)
		=> [.. fileInfos.Select(file => new HackFile(file))];
	
	extension(IEnumerable items)
    {
    }
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="list">The source sequence.</param>
    extension<T>(IEnumerable<T> list)
    {
	    /// <summary>Copies the sequence into a new <see cref="HackPDM.Domain.Helper.ConcurrentSet{T}"/>.</summary>
	    /// <returns>A new set holding the distinct items.</returns>
	    public ConcurrentSet<T> ToConcurrentSet()
	    {
		    ConcurrentSet<T> set = [.. list];
		    return set;
	    }

    }
}

/// <summary>Extension methods over spans.</summary>
public static class UnsafeExtensions
{
	/// <typeparam name="TIn">The type of the pairs' first element.</typeparam>
	/// <typeparam name="TOut">The type of the pairs' second element.</typeparam>
	/// <param name="span">The pairs.</param>
	extension<TIn, TOut>(Span<(TIn, TOut)> span)
	{
		/// <summary>Copies the second element of each pair in the span into a new array.</summary>
		/// <returns>The second elements, in order.</returns>
		public TOut[] SelectSecond()
		{
			TOut[] temp = new TOut[span.Length];
			for (int i = 0; i < span.Length; i++)
				temp[i] = span[i].Item2;
			return temp;
		}
	}
}
