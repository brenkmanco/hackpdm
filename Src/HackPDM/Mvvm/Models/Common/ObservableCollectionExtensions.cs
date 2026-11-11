using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HackPDM.Mvvm.Models.Common;

public static class ObservableCollectionExtensions
{
	/// <summary>Replaces the contents (a <c>RangeObservableCollection</c> does it with one Reset notification).</summary>
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
