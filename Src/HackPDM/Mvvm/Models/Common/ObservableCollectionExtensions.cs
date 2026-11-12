using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HackPDM.Mvvm.Models.Common;

public static class ObservableCollectionExtensions
{
	/// <summary>Replaces the contents of a collection with new items.</summary>
	/// <typeparam name="T">The element type of the collection.</typeparam>
	/// <param name="collection">The collection to refill.</param>
	/// <param name="items">The new contents, in order.</param>
	/// <remarks>A <see cref="RangeObservableCollection{T}"/> does it with one Reset notification; any other collection
	/// raises one notification per removal and addition.</remarks>
	public static void ReplaceAll<T>( this ObservableCollection<T> collection, IEnumerable<T> items )
	{
		if( collection is RangeObservableCollection<T> range )
		{
			range.ReplaceAll( items );
			return;
		}
		collection.Clear();
		foreach( T item in items )
			collection.Add( item );
	}
}
