using System;
using System.Collections;

using WinUI.TableView;

using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>Binds the entry rows to the TableView and applies the view model's grouping key.</summary>
internal static class EntryListGrouping
{
	/// <summary>
	/// TableView must always receive the flat row list: handing it a grouped CollectionViewSource breaks
	/// its internal CollectionView and crashes the page. Grouping goes through TableView's GroupDescriptions.
	/// </summary>
	public static void Apply( TableView grid, IList rows, Func<EntryRow, IComparable?>? groupKey )
	{
		if( !ReferenceEquals( grid.ItemsSource, rows ) )
			grid.ItemsSource = rows;
#if WINDOWS
		var current = grid.GroupDescriptions.Count == 1 ? grid.GroupDescriptions[ 0 ] as SelectorGroupDescription : null;
		if( groupKey is null )
		{
			if( grid.GroupDescriptions.Count > 0 )
				grid.GroupDescriptions.Clear();
			return;
		}
		if( current is not null && ReferenceEquals( current.Selector, groupKey ) )
			return;

		grid.GroupDescriptions.Clear();
		grid.GroupDescriptions.Add( new SelectorGroupDescription( groupKey, item => item is EntryRow row ? groupKey( row ) : null ) );
#endif
	}

#if WINDOWS
	private sealed class SelectorGroupDescription( object selector, Func<object?, object?> valueDelegate )
		: GroupDescription( null, SortDirection.Ascending, GroupKeyComparer.Instance, valueDelegate )
	{
		public object Selector { get; } = selector;
	}

	/// <summary>Orders group keys of arbitrary (possibly non-comparable or mixed) types without throwing.</summary>
	private sealed class GroupKeyComparer : IComparer
	{
		public static readonly GroupKeyComparer Instance = new();
		public int Compare( object? x, object? y )
		{
			if( ReferenceEquals( x, y ) ) return 0;
			if( x is null ) return -1;
			if( y is null ) return 1;
			if( x.GetType() == y.GetType() && x is IComparable cx )
				return cx.CompareTo( y );
			return string.CompareOrdinal( x.ToString(), y.ToString() );
		}
	}
#endif
}
