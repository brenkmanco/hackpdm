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

	public static int Compare( this int? a, int? b ) => Nullable.Compare( a, b );
	public static IList AddRange( this IList list, IEnumerable items )
	{
		foreach( var item in items ) {
			list.Add( item );
		}
		return list;
	}
	public static T GetAssign<T>( this T obj, Func<T> func ) where T : class
	{
		obj ??= func();
		return obj;
	}


	public static HashSet<T> AddAll<T>( this HashSet<T> hashset, IEnumerable<T> values )
	{
		foreach( T value in values ) {
			hashset.Add( value );
		}
		return hashset;
	}
	public static List<T> TakeAndRemove<T>( this List<T> source, Func<T, bool> predicate )
	{
		var takenElements = source.Where(predicate).ToList();

		// Remove the elements that match the predicate
		foreach( var element in takenElements ) {
			source.Remove( element );
		}

		return takenElements;
	}

	#region	ArrayList
	public static bool Any<T>( this ArrayList source, Func<T, bool> predicate )
	{
		foreach( T obj in source.OfType<T>() ) {
			if( predicate( obj ) ) {
				return true;
			}
		}
		return false;
	}
	public static IEnumerable<TOut> Select<TIn, TOut>( this ArrayList source, Func<TIn, TOut> selector )
	{
		foreach( TIn obj in source.OfType<TIn>() ) {
			yield return selector( obj );
		}
	}
	public static IEnumerable<TOut> SelectNotDefault<TIn, TOut>( this ArrayList source, Func<TIn, TOut?> selector ) where TOut : IEquatable<TOut>
	{
		foreach( TIn obj in source.OfType<TIn>() ) {
			var item = selector(obj);
			if( item is { } clean && !clean.Equals( default ) )
				yield return clean;
		}
	}
	#endregion
	#region	Hashtable
	public static bool TryGetValue( this Hashtable ht, object key, out object? value )
	{
		value = ht[ key ];
		if( value != null )
			return true;
		return false;
	}
	public static bool TryGetValue<TKey, TVal>( this Hashtable ht, TKey key, out TVal? value ) where TKey : notnull
	{
		if( ht[ key ] is TVal t ) {
			value = t;
			return true;
		}
		value = default;
		return false;
	}
	#endregion
	#region string
	public static TArray Split<TArray>( this string str, string delimiter = " ", StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries )
		where TArray : IList, new()
	{
		string[] strSplit = str.Split([.. delimiter]);
		TArray tarray = new();
		foreach( string s in strSplit ) {
			tarray.Add( s );
		}
		return tarray;
	}
	#endregion
	#region FileInfo
	public static bool MoveFile( this FileInfo file, string toPath )
	{
		try {
			if( !Directory.Exists( toPath ) && !Directory.CreateDirectory( toPath ).Exists )
				return false;

			string toFilePath = Path.Combine(toPath, file.Name);

			if( file.Exists ) {
				var newFile = file.CopyTo(toFilePath, true);
				file.Delete();
				file = newFile;
			} else
				return false;
		} catch {
			return false;
		}
		return true;
	}


	public static FileInfo CopyFile( this FileInfo file, string toPath )
	{
		try {
			if( !Directory.Exists( toPath ) && !Directory.CreateDirectory( toPath ).Exists )
				return null;

			string toFilePath = Path.Combine(toPath, file.Name);

			if( file.Exists ) {
				return file.CopyTo( toFilePath, true );
			} else
				return null;
		} catch { }
		return null;
	}
	#endregion
	#region Observable<T>
	public static void Sort<T>( this ObservableCollection<T> oc, Comparison<T> comparer, bool reverse = false ) => oc.SortInternal( comparer, reverse );
	public static void Sort<T, TKey>( this ObservableCollection<T> oc, Func<T, TKey> keySelector, bool reverse = false ) => oc.SortInternal( keySelector, reverse );
	public static void ReverseSort<T>( this ObservableCollection<T> oc, Comparison<T> comparer ) => oc.SortInternal( comparer, true );
	public static void ReverseSort<T, TKey>( this ObservableCollection<T> oc, Func<T, TKey> keySelector ) => oc.SortInternal( keySelector, true );
	private static void SortInternal<T>( this ObservableCollection<T> oc, Comparison<T> comparer, bool reverse = false )
	{
		// Step 1: Create a sorted snapshot
		var sorted = oc.ToList();
		sorted.Sort( ( a, b ) => {
			int result = comparer(a, b);
			return reverse ? -result : result;
		} );

		// Step 2: Reorder the original collection to match the sorted snapshot
		for( int i = 0; i < sorted.Count; i++ ) {
			var item = sorted[i];
			int currentIndex = oc.IndexOf(item);
			if( currentIndex != i ) {
				oc.Move( currentIndex, i );
			}
		}

		Debug.WriteLine( "Finished Sorting" );
	}
	private static void SortInternal<T, TKey>( this ObservableCollection<T> oc, Func<T, TKey> keySelector, bool reverse = false )
	{
		var ocSort = reverse ? oc.OrderByDescending(keySelector).ToList() :
			oc.OrderBy( keySelector ).ToList();

		ocSort.ForEach( item => {
			int currentIndex = oc.IndexOf(item);
			int targetIndex = oc.IndexOf(item);
			if( currentIndex != targetIndex ) {
				oc.Move( currentIndex, targetIndex );
			}
		} );
	}
	#endregion
	#region IEnumerable<T?>
	public static (IEnumerable<TOut?>, IEnumerable<TOut2?>) SegmentSelectDiffWhere<T, TOut, TOut2>( this IEnumerable<T?> array, Func<T?, int, (bool, TOut?, TOut2?)> predicateSelect, bool skipNullItem = true )
	{
		var segmentList = SegmentSelectDiffInternal(array, predicateSelect, skipNullItem);
		return (segmentList.SkipSelect( item => (!item.Item1, item.Item2) ), segmentList.SkipSelect( item => (item.Item1, item.Item3) ));
	}
	public static (List<T?>, List<T?>) SegmentWhere<T>( this IEnumerable<T?> array, Predicate<T?> predicate )
	{
		(List<T?>, List<T?>) items = new(new(), new());
		foreach( T? item in array ) {
			if( predicate( item ) )
				items.Item1.Add( item );
			else
				items.Item2.Add( item );
		}
		return items;
	}
	public static IEnumerable<TOut> SkipNullSelect<T, TOut>( this IEnumerable<T?> array, Func<T, TOut> func )
	{
		foreach( T? item in array ) {
			if( item is T notnullItem )
				yield return func( notnullItem );
		}
	}
	private static IEnumerable<(bool, TOut?)> SegmentSelectInternal<T, TOut>( this IEnumerable<T?> array, Func<T?, (bool, TOut?)> predicateSelect, bool skipNullItem = true )
	{
		foreach( T? item in array ) {
			if( skipNullItem && item is null )
				continue;

			var value = predicateSelect(item);

			yield return (value.Item1, value.Item2);
		}
	}
	private static IEnumerable<(bool, TOut?)> SegmentSelectInternal<T, TOut>( this IEnumerable<T?> array, Func<T?, int, (bool, TOut?)> predicateSelect, bool skipNullItem = true )
	{
		int index = -1;
		foreach( T? item in array ) {
			checked {
				index++;
			}
			if( skipNullItem && item is null )
				continue;

			var value = predicateSelect(item, index);

			yield return (value.Item1, value.Item2);
		}
	}
	private static IEnumerable<(bool, TOut?, TOut2?)> SegmentSelectDiffInternal<T, TOut, TOut2>( this IEnumerable<T?> array, Func<T?, int, (bool, TOut?, TOut2?)> predicateSelect, bool skipNullItem = true )
	{
		int index = -1;
		foreach( T? item in array ) {
			checked {
				index++;
			}
			if( skipNullItem && item is null )
				continue;

			var value = predicateSelect(item, index);

			yield return (value.Item1, value.Item2, value.Item3);
		}
	}
	#endregion
	#region IEnumerable<T>
	public static IEnumerable<TOut> SkipSelect<T, TOut>( this IEnumerable<T> source, Func<T, bool> predicate, Func<T, TOut> selector )
	{
		foreach( T obj in source ) {
			if( !predicate( obj ) ) {
				yield return selector( obj );
			}
		}
	}
	public static IEnumerable<TOut?> SkipSelect<T, TOut>( this IEnumerable<T> source, Func<T, (bool, TOut)> predicateSelector )
	{
		foreach( T obj in source ) {
			var result = predicateSelector(obj);
			if( !result.Item1 ) {
				yield return result.Item2;
			}
		}
	}
	public static TOut? FirstOrDefaultSelect<T, TOut>( this IEnumerable<T> source, Func<T, (bool, TOut)> predicateSelect )
	{
		foreach( T item in source ) {
			var result = predicateSelect(item);
			if( result.Item1 )
				return result.Item2;
		}
		return default;
	}
	public static IEnumerable<(T, T2)> PopulateZip<T, T2>( this IEnumerable<T> source, Func<T, T2> func )
	{
		foreach( T obj in source ) {
			yield return (obj, func( obj ));
		}
	}
	#endregion
	#region IEnumerable
	public static TOut? FirstOrDefaultSelect<TOut>( this IEnumerable source, Func<object, (bool, TOut)> predicateSelect )
		=> source.Cast<object>().FirstOrDefaultSelect( predicateSelect );
	public static IEnumerable<object> Flatten( this IEnumerable source )
	{
		foreach( object obj in source ) {
			if( obj is IEnumerable ie ) {
				foreach( var nestedItem in ie.Flatten() ) {
					yield return nestedItem;
				}
			} else {
				yield return obj;
			}
		}
	}
	#endregion
}
public static class ExtensionConvertMethods
{
	public static HackFile[] ToHackArray( this IEnumerable<FileInfo> fileInfos )
		=> [ .. fileInfos.Select( file => new HackFile( file ) ) ];
	public static ArrayList ToArrayListIDs<T>( this IEnumerable<T> source ) where T : HpBaseModel, new()
	{
		ArrayList ids = [];
		foreach( T model in source ) {
			ids.Add( model.id );
		}
		return ids;
	}

	extension( ArrayList list )
	{
		public T[] ToArray<T>() => [ .. list.Cast<T>() ];
	public HashSet<T> ToHashSet<T>() => [ .. list.Cast<T>() ];
}
extension( IEnumerable items )

	{

		public ConcurrentBag<T> ToConcurrentBag<T>()
	{
		return items.Cast<object>().ToConcurrentBag<T>();
	}
}
extension<T>( IEnumerable < T > list )

	{

		public ConcurrentSet<T> ToConcurrentSet()
{
	ConcurrentSet<T> set = [.. list];
	return set;
}

public ArrayList ToArrayList()
{
	if( list == null )
		throw new ArgumentNullException( nameof( list ) );

	return [ .. list ];
}
    }
}

public static class UnsafeExtensions
{
	public delegate ref TOut RefSelector<TIn, TOut>( ref TIn input );
	extension<T>(Span<T> span)
	{
		public void TakeWhile( Predicate<T> predicate )
	{
		for( int i = 0; i < span.Length; i++ ) {
			ref var item = ref span[i];
			if( predicate( item ) ) {
				// list[i] = item;
			}
		}
	}
	public Span<T> Skip( Func<T, bool> predicate )
	{
		T[] temp = new T[span.Length];
		int count = 0;
		for( int i = 0; i < span.Length; i++ ) {
			T val = span[i]; // safe indexing
			if( !predicate( val ) ) {
				temp[ count++ ] = val;
			}
		}
		return temp.AsSpan( 0, count );
	}
	public ref TOut[] RefSelect<TOut>(
		RefSelector<T, TOut> selector,
		ref TOut[] unpopOuts )
	{
		for( int i = 0; i < span.Length; i++ ) {
			ref T val = ref span[i];
			ref TOut unpopItem = ref selector(ref val);
			unpopOuts[ i ] = unpopItem;
		}
		return ref unpopOuts;
	}
	public Span<T> SkipUnsafe( Func<T, bool> predicate )
	{
		T[] temp = new T[span.Length];
		int count = 0;

		ref T start = ref MemoryMarshal.GetReference(span);

		for( int i = 0; i < span.Length; i++ ) {
			T val = Unsafe.Add(ref start, i);
			if( !predicate( val ) ) {
				temp[ count++ ] = val;
			}
		}

		return temp.AsSpan( 0, count );
	}
}
extension<TIn, TOut>( Span < (TIn, TOut) > span )
	{
		public TIn[] SelectFirst()
{
	TIn[] temp = new TIn[span.Length];
	for( int i = 0; i < span.Length; i++ ) {
		ref TIn item = ref span[i].Item1;
		temp[ i ] = item;
	}
	return temp;
}
public TOut[] SelectSecond()
{
	TOut[] temp = new TOut[span.Length];
	for( int i = 0; i < span.Length; i++ ) {
		ref TOut item = ref span[i].Item2;
		temp[ i ] = item;
	}
	return temp;
}
public Span<TOut> SelectSecondSpan() => span.SelectSecond();
	}
}