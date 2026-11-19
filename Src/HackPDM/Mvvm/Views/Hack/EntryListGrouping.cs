using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using WinUI.TableView;

using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>Header row for one group in the file manager's grouped list view.</summary>
public sealed class EntryGroupHeader : INotifyPropertyChanged
{
	private bool _isExpanded;

	public EntryGroupHeader( object? key, string displayKey, IReadOnlyList<EntryRow> items, bool isExpanded )
	{
		Key = key;
		DisplayKey = displayKey;
		Items = items;
		_isExpanded = isExpanded;
	}

	public object? Key { get; }
	public string DisplayKey { get; }
	public IReadOnlyList<EntryRow> Items { get; }
	public int Count => Items.Count;
	public string CountText => Count == 1 ? "1 item" : $"{Count} items";
	public string ChevronGlyph => IsExpanded ? "\uE70D" : "\uE76C";

	public bool IsExpanded
	{
		get => _isExpanded;
		set
		{
			if( _isExpanded == value )
				return;
			_isExpanded = value;
			PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsExpanded ) ) );
			PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ChevronGlyph ) ) );
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>Picks the group header template for <see cref="EntryGroupHeader"/> and the entry template for <see cref="EntryRow"/>.</summary>
public sealed class EntryGroupTemplateSelector : DataTemplateSelector
{
	public DataTemplate? GroupHeaderTemplate { get; set; }
	public DataTemplate? EntryRowTemplate { get; set; }

	protected override DataTemplate? SelectTemplateCore( object item )
		=> item is EntryGroupHeader ? GroupHeaderTemplate : EntryRowTemplate;

	protected override DataTemplate? SelectTemplateCore( object item, DependencyObject container )
		=> SelectTemplateCore( item );
}

/// <summary>Binds the entry rows to the TableView and applies the view model's grouping key.</summary>
internal static class EntryListGrouping
{
	/// <summary>
	/// TableView must always receive the flat row list: handing it a grouped CollectionViewSource breaks
	/// its internal CollectionView and crashes the page. Grouping goes through TableView's GroupDescriptions
	/// on Windows, and through the grouped ListView on non-Windows targets.
	/// </summary>
	/// <param name="grid">The entry list.</param>
	/// <param name="rows">The flat row list; set as the items source when it isn't already.</param>
	/// <param name="groupKey">The key to group rows by; null removes the grouping.</param>
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
		{
			grid.RefreshGrouping();
			return;
		}

		grid.GroupDescriptions.Clear();
		grid.GroupDescriptions.Add( new SelectorGroupDescription( groupKey, item => item is EntryRow row ? groupKey( row ) : null ) );
#endif
	}

	/// <summary>Groups <paramref name="rows"/> by <paramref name="groupKey"/> in key order, preserving row order within each group.</summary>
	public static List<EntryGroupHeader> BuildGroups(
		IEnumerable<EntryRow> rows,
		Func<EntryRow, IComparable?> groupKey,
		IReadOnlySet<string>? collapsedKeys = null,
		bool defaultCollapsed = false )
	{
		List<(object? Key, string DisplayKey, List<EntryRow> Items)> buckets = [];
		Dictionary<string, int> byDisplay = new( StringComparer.Ordinal );

		foreach( EntryRow row in rows )
		{
			object? raw = groupKey( row );
			string display = FormatGroupKey( raw );
			if( !byDisplay.TryGetValue( display, out int index ) )
			{
				index = buckets.Count;
				byDisplay[ display ] = index;
				buckets.Add( (raw, display, []) );
			}
			buckets[ index ].Items.Add( row );
		}

		buckets.Sort( ( a, b ) => GroupKeyComparer.Instance.Compare( a.Key, b.Key ) );

		List<EntryGroupHeader> result = new( buckets.Count );
		foreach( var (key, display, items) in buckets )
		{
			bool collapsed = collapsedKeys is not null
				? collapsedKeys.Contains( display )
				: defaultCollapsed;
			result.Add( new EntryGroupHeader( key, display, items, isExpanded: !collapsed ) );
		}
		return result;
	}

	/// <summary>Flattens group headers and the rows of expanded groups into a single list for a virtualized ListView.</summary>
	public static List<object> FlattenVisible( IEnumerable<EntryGroupHeader> groups )
	{
		List<object> flat = [];
		foreach( EntryGroupHeader group in groups )
		{
			flat.Add( group );
			if( group.IsExpanded )
				flat.AddRange( group.Items );
		}
		return flat;
	}

	public static string FormatGroupKey( object? value ) => value switch
	{
		null => "(None)",
		string s when string.IsNullOrWhiteSpace( s ) => "(None)",
		DateTime dt => dt.ToLocalTime().ToString( "yyyy-MM-dd", CultureInfo.InvariantCulture ),
		_ => value.ToString() is { Length: > 0 } text ? text : "(None)",
	};

#if WINDOWS
	private sealed class SelectorGroupDescription( object selector, Func<object?, object?> valueDelegate )
		: GroupDescription( null, SortDirection.Ascending, GroupKeyComparer.Instance, valueDelegate )
	{
		public object Selector { get; } = selector;
	}
#endif

	/// <summary>Orders group keys of arbitrary (possibly non-comparable or mixed) types without throwing.</summary>
	internal sealed class GroupKeyComparer : IComparer
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
}
