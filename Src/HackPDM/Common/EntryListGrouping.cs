using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

using HackPDM.Core.Services;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

using WinUI.TableView;

namespace HackPDM.Common;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class EntryGroupHeader( object? key, string displayKey, IReadOnlyList<EntryRow> items, bool isExpanded ) : INotifyPropertyChanged
{
	private static readonly PropertyChangedEventArgs IsExpandedChangedArgs = new( nameof( IsExpanded ) );
	private static readonly PropertyChangedEventArgs ChevronGlyphChangedArgs = new( nameof( ChevronGlyph ) );

	private bool _isExpanded = isExpanded;

	public object? Key { get; } = key;
	public string DisplayKey { get; } = displayKey;
	public IReadOnlyList<EntryRow> Items { get; } = items;
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
			PropertyChanged?.Invoke( this, IsExpandedChangedArgs );
			PropertyChanged?.Invoke( this, ChevronGlyphChangedArgs );
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
}

[Microsoft.UI.Xaml.Data.Bindable]
public sealed partial class EntryGroupTemplateSelector : DataTemplateSelector
{
	public DataTemplate? GroupHeaderTemplate { get; set; }
	public DataTemplate? EntryRowTemplate { get; set; }

	protected override DataTemplate? SelectTemplateCore( object item )
		=> item is EntryGroupHeader ? GroupHeaderTemplate : EntryRowTemplate;

	protected override DataTemplate? SelectTemplateCore( object item, DependencyObject container )
		=> SelectTemplateCore( item );
}

/// <summary>Applies TableView grouping on Windows and builds flattened group lists on non-Windows Uno Desktop.</summary>
internal static class EntryListGrouping
{
	public static void Apply( TableView grid, IList rows, Func<EntryRow, IComparable?>? groupKey )
	{
		if( !ReferenceEquals( grid.ItemsSource, rows ) )
			grid.ItemsSource = rows;
#if WINDOWS
		SelectorGroupDescription? current = grid.GroupDescriptions.Count == 1
			? grid.GroupDescriptions[ 0 ] as SelectorGroupDescription
			: null;
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

	public static List<EntryGroupHeader> BuildGroups(
		IEnumerable<EntryRow> rows,
		Func<EntryRow, IComparable?> groupKey,
		IReadOnlySet<string>? collapsedKeys = null,
		bool defaultCollapsed = false,
		IReadOnlySet<string>? expandedKeys = null )
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
				buckets.Add( ( raw, display, [] ) );
			}
			buckets[ index ].Items.Add( row );
		}

		buckets.Sort( static ( a, b ) => SafeComparableComparer.Instance.Compare( a.Key, b.Key ) );

		List<EntryGroupHeader> result = new( buckets.Count );
		for( int i = 0; i < buckets.Count; i++ )
		{
			var (key, display, items) = buckets[ i ];
			bool collapsed = collapsedKeys is not null && collapsedKeys.Contains( display )
				? true
				: expandedKeys is not null && expandedKeys.Contains( display )
					? false
					: defaultCollapsed;
			result.Add( new EntryGroupHeader( key, display, items, isExpanded: !collapsed ) );
		}
		return result;
	}

	public static List<object> FlattenVisible( IReadOnlyList<EntryGroupHeader> groups )
	{
		int total = groups.Count;
		for( int i = 0; i < groups.Count; i++ )
		{
			if( groups[ i ].IsExpanded )
				total += groups[ i ].Items.Count;
		}
		List<object> flat = new( total );
		for( int i = 0; i < groups.Count; i++ )
		{
			EntryGroupHeader group = groups[ i ];
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
		DateTime dt when dt.Year <= 1 => "(None)",
		DateTime dt => ( dt.Kind == DateTimeKind.Local ? dt : DateTime.SpecifyKind( dt, DateTimeKind.Utc ).ToLocalTime() )
			.ToString( "yyyy-MM-dd", CultureInfo.InvariantCulture ),
		_ => value.ToString() is { Length: > 0 } text ? text : "(None)",
	};

#if WINDOWS
	private sealed class SelectorGroupDescription( object selector, Func<object?, object?> valueDelegate )
		: GroupDescription( null, SortDirection.Ascending, SafeComparableComparer.Instance, valueDelegate )
	{
		public object Selector { get; } = selector;
	}
#endif
}
