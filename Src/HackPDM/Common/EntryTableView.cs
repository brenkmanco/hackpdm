using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using HackPDM.Themes;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.System;
using Windows.UI;
using Windows.UI.Core;

using WinUI.TableView;

namespace HackPDM.Common;

/// <summary>
/// <see cref="TableView"/> subclass with deterministic, visual-tree-based row selection on Uno Skia and WinUI 3.
/// <para>
/// <c>WinUI.TableView</c> 1.5.1 intercepts left-clicks inside <c>TableViewCell.OnPointerPressed</c> and routes
/// <c>SelectionMode="Extended"</c> through <c>StartDragSelection</c>/<c>GetRowsInDragRect</c>, which depends on
/// cached <c>TableViewRow.Position</c> coordinates that can be stale or zero on Uno — causing clicks to deselect
/// everything or select the wrong rows. This control cancels that drag-selection state immediately and applies
/// standard Extended/Single/Multiple selection directly from the clicked <see cref="TableViewRow"/> container.
/// </para>
/// </summary>
public sealed partial class EntryTableView : TableView
{
	private sealed class RowHighlightHost
	{
		public readonly Border Indicator;
		public readonly Border Overlay;
		public bool IsAttached;
		public bool DeferredHooked;
		public int DeferredGeneration;
		public RoutedEventHandler? LoadedHandler;
		public object? PendingItem;
		public bool? AppliedSelected;
		public bool? AppliedHovered;
		public HighlightPalette? AppliedPalette;

		public RowHighlightHost()
		{
			Indicator = new Border
			{
				IsHitTestVisible = false,
				Width = 3,
				Height = 14,
				CornerRadius = new CornerRadius( 1.5 ),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness( 2, 0, 0, 0 ),
				Visibility = Visibility.Collapsed,
			};
			Overlay = new Border
			{
				IsHitTestVisible = false,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				CornerRadius = new CornerRadius( 3 ),
				Margin = new Thickness( 2, 1, 2, 1 ),
				Visibility = Visibility.Collapsed,
				Child = Indicator,
			};
		}
	}

	private sealed class HighlightPalette(
		SolidColorBrush hover,
		SolidColorBrush selected,
		SolidColorBrush selectedHover,
		SolidColorBrush indicator )
	{
		public SolidColorBrush Hover { get; } = hover;
		public SolidColorBrush Selected { get; } = selected;
		public SolidColorBrush SelectedHover { get; } = selectedHover;
		public SolidColorBrush Indicator { get; } = indicator;
	}

	private static bool _highlightCacheDirty = true;
	private static bool _cachedDark;
	private static Color _cachedAccent;
	private static SolidColorBrush? _transparentBrush;
	private static SolidColorBrush? _hoverBrush;
	private static SolidColorBrush? _selectedBrush;
	private static SolidColorBrush? _selectedHoverBrush;
	private static SolidColorBrush? _indicatorBrush;
	private static HighlightPalette? _cachedPalette;

	private readonly ConditionalWeakTable<TableViewRow, RowHighlightHost> _rowHosts = new();
	private readonly HashSet<TableViewRow> _realizedRows = [];
	private TableViewRow? _hoveredRow;
	private bool _themeSubscribed;

	private int _anchorIndex = -1;
	private bool _isUpdatingSelection;
	private bool _clearJustAddedScheduled;
	private readonly HashSet<object> _justAddedByTableView = [];
	private List<object>? _expectedSelectionAfterRelease;

	/// <summary>True while a batched programmatic or pointer selection change is mutating <see cref="ListViewBase.SelectedItems"/>.</summary>
	public bool IsUpdatingSelection => _isUpdatingSelection;

	/// <summary>Raised once after a batched selection update completes so listeners can sync once instead of once per row.</summary>
	public event EventHandler? SelectionBatchCompleted;

	public EntryTableView()
	{
		AddHandler( PointerPressedEvent, new PointerEventHandler( OnRowPointerPressed ), handledEventsToo: true );
		AddHandler( PointerReleasedEvent, new PointerEventHandler( OnRowPointerReleased ), handledEventsToo: true );
		AddHandler( PointerMovedEvent, new PointerEventHandler( OnRowPointerMoved ), handledEventsToo: true );
		AddHandler( PointerExitedEvent, new PointerEventHandler( OnTablePointerExited ), handledEventsToo: true );
		SelectionChanged += OnTableSelectionChanged;
		ContainerContentChanging += OnRowContainerContentChanging;
		Loaded += OnEntryTableLoaded;
		Unloaded += OnEntryTableUnloaded;
	}

	/// <summary>Clears cached row highlight brushes so they rebuild against the active theme palette on next render.</summary>
	public static void InvalidateHighlightBrushes() => _highlightCacheDirty = true;

	private void OnEntryTableLoaded( object sender, RoutedEventArgs e )
	{
		if( !_themeSubscribed && Application.Current is not null )
		{
			_themeSubscribed = true;
			AppComposition.Theme.ThemeChanged += OnAppThemeChanged;
		}
		RefreshAllRowHighlights();
	}

	private void OnEntryTableUnloaded( object sender, RoutedEventArgs e )
	{
		if( _themeSubscribed && Application.Current is not null )
		{
			_themeSubscribed = false;
			AppComposition.Theme.ThemeChanged -= OnAppThemeChanged;
		}
		SetHoveredRow( null );
	}

	private void OnAppThemeChanged()
	{
		InvalidateHighlightBrushes();
		RefreshAllRowHighlights();
	}

	private void OnTableSelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( _isUpdatingSelection )
			return;

		// Track items added by TableView's own OnAnyPointerPressed (which runs on TableViewCell
		// synchronously before our bubbled PointerPressed handler) so Ctrl+click toggling behaves
		// identically on both Uno Skia and WinAppSDK.
		if( e.AddedItems is { Count: > 0 } )
		{
			foreach( object added in e.AddedItems )
				_justAddedByTableView.Add( added );
			ScheduleClearJustAdded();

			object lastAdded = e.AddedItems[ e.AddedItems.Count - 1 ];
			int idx = Items.IndexOf( lastAdded );
			if( idx >= 0 )
				_anchorIndex = idx;
		}

		RefreshAllRowHighlights();
	}

	private void ScheduleClearJustAdded()
	{
		if( _clearJustAddedScheduled )
			return;
		_clearJustAddedScheduled = true;
		if( DispatcherQueue is { } queue )
		{
			queue.TryEnqueue( () =>
			{
				_clearJustAddedScheduled = false;
				_justAddedByTableView.Clear();
			} );
		}
		else
		{
			_clearJustAddedScheduled = false;
			_justAddedByTableView.Clear();
		}
	}

	private void OnRowPointerPressed( object sender, PointerRoutedEventArgs e )
	{
		if( SelectionMode == ListViewSelectionMode.None )
			return;

		if( FindAncestorRow( e.OriginalSource as DependencyObject ) is not { } row )
			return;

		int index = IndexFromContainer( row );
		object? item = ItemFromContainer( row );
		if( index < 0 || item is null )
			return;

		var props = e.GetCurrentPoint( this ).Properties;

		if( props.IsRightButtonPressed )
		{
			// Ensure right-clicking an unselected row selects it before ContextFlyout opens,
			// while preserving multi-selection when right-clicking an already-selected row.
			if( !SelectedItems.Contains( item ) )
			{
				_hoveredRow = row;
				_realizedRows.Add( row );
				ApplySelection( [ item ], anchorIndex: index, reconcileOnRelease: false, refreshIfUnchanged: true );
			}
			else
			{
				SetHoveredRow( row );
			}
			return;
		}

		if( !props.IsLeftButtonPressed )
			return;

		// End any drag-selection & pointer capture that TableViewCell.OnPointerPressed ->
		// TableView.OnAnyPointerPressed may have started, without touching SelectedItems.
		base.OnPointerReleased( e );

		bool ctrl = IsKeyDown( e, VirtualKeyModifiers.Control, VirtualKey.Control );
		bool shift = IsKeyDown( e, VirtualKeyModifiers.Shift, VirtualKey.Shift );

		TableViewRow? previousHover = _hoveredRow;
		_hoveredRow = row;
		_realizedRows.Add( row );
		bool refreshedAll = false;

		switch( SelectionMode )
		{
			case ListViewSelectionMode.Single:
				refreshedAll = ApplySelection( [ item ], anchorIndex: index, reconcileOnRelease: true, refreshIfUnchanged: false );
				break;

			case ListViewSelectionMode.Multiple:
				refreshedAll = ToggleItemSelection( item, index );
				break;

			case ListViewSelectionMode.Extended:
				refreshedAll = ApplyExtendedSelection( item, index, ctrl, shift );
				break;
		}

		if( !refreshedAll )
		{
			if( previousHover is not null && !ReferenceEquals( previousHover, row ) && _realizedRows.Contains( previousHover ) )
				UpdateRowHighlight( previousHover, previousHover.Content );
			UpdateRowHighlight( row, item );
		}

		_justAddedByTableView.Clear();
		row.Focus( FocusState.Pointer );
		e.Handled = true;
	}

	private void OnRowPointerMoved( object sender, PointerRoutedEventArgs e )
	{
		if( SelectionMode == ListViewSelectionMode.None )
		{
			SetHoveredRow( null );
			return;
		}

		SetHoveredRow( FindAncestorRow( e.OriginalSource as DependencyObject ) );
	}

	private void OnTablePointerExited( object sender, PointerRoutedEventArgs e )
		=> SetHoveredRow( null );

	private void OnRowPointerReleased( object sender, PointerRoutedEventArgs e )
	{
		if( _expectedSelectionAfterRelease is not { } expected )
			return;
		_expectedSelectionAfterRelease = null;

		// On Shift+click or row-margin clicks, Uno's SelectorItem.OnPointerReleasedPrivate may
		// run ListViewBase.OnItemClicked with a stale shift anchor; reconcile back to our selection.
		if( !SelectionMatches( expected ) )
			ApplySelection( expected, anchorIndex: _anchorIndex, reconcileOnRelease: false );
	}

	protected override void OnPointerMoved( PointerRoutedEventArgs e )
	{
		if( !ShowDragRectangle )
			return;
		base.OnPointerMoved( e );
	}

	protected override void OnPointerCaptureLost( PointerRoutedEventArgs e )
	{
		_expectedSelectionAfterRelease = null;
		SetHoveredRow( null );
		base.OnPointerReleased( e );
		base.OnPointerCaptureLost( e );
	}

	protected override void OnPointerCanceled( PointerRoutedEventArgs e )
	{
		_expectedSelectionAfterRelease = null;
		SetHoveredRow( null );
		base.OnPointerReleased( e );
		base.OnPointerCanceled( e );
	}

	protected override void PrepareContainerForItemOverride( DependencyObject element, object item )
	{
		base.PrepareContainerForItemOverride( element, item );
		if( element is TableViewRow row )
		{
			_realizedRows.Add( row );
			UpdateRowHighlight( row, item );
		}
	}

	protected override void ClearContainerForItemOverride( DependencyObject element, object item )
	{
		if( element is TableViewRow row )
		{
			_realizedRows.Remove( row );
			if( ReferenceEquals( _hoveredRow, row ) )
				_hoveredRow = null;
			ResetRowHighlight( row );
		}
		base.ClearContainerForItemOverride( element, item );
	}

	private void OnRowContainerContentChanging( ListViewBase sender, ContainerContentChangingEventArgs args )
	{
		if( args.ItemContainer is not TableViewRow row )
			return;

		if( args.InRecycleQueue )
		{
			_realizedRows.Remove( row );
			if( ReferenceEquals( _hoveredRow, row ) )
				_hoveredRow = null;
			ResetRowHighlight( row );
			return;
		}

		_realizedRows.Add( row );
		UpdateRowHighlight( row, args.Item );
	}

	private bool ApplyExtendedSelection( object item, int index, bool ctrl, bool shift )
	{
		if( shift )
		{
			int anchor = _anchorIndex >= 0 && _anchorIndex < Items.Count ? _anchorIndex : index;
			int start = Math.Min( anchor, index );
			int end = Math.Max( anchor, index );

			List<object> target = [];
			HashSet<object> seen = [];
			if( ctrl )
			{
				foreach( object existing in SelectedItems )
				{
					if( seen.Add( existing ) )
						target.Add( existing );
				}
			}
			for( int i = start; i <= end && i < Items.Count; i++ )
			{
				if( Items[ i ] is { } rowItem && seen.Add( rowItem ) )
					target.Add( rowItem );
			}
			return ApplySelection( target, anchorIndex: anchor, reconcileOnRelease: true, refreshIfUnchanged: false );
		}

		if( ctrl )
			return ToggleItemSelection( item, index );

		return ApplySelection( [ item ], anchorIndex: index, reconcileOnRelease: true, refreshIfUnchanged: false );
	}

	private bool ToggleItemSelection( object item, int index )
	{
		bool wasSelectedBeforePress = SelectedItems.Contains( item ) && !_justAddedByTableView.Contains( item );
		_anchorIndex = index;

		if( SelectionMode == ListViewSelectionMode.Single )
			return ApplySelection( wasSelectedBeforePress ? [] : [ item ], anchorIndex: index, reconcileOnRelease: true, refreshIfUnchanged: false );

		// Mutate SelectedItems in place — O(1) instead of clearing and re-adding every selected row
		bool changed = false;
		_isUpdatingSelection = true;
		try
		{
			if( wasSelectedBeforePress )
				changed = SelectedItems.Remove( item );
			else if( !SelectedItems.Contains( item ) )
			{
				SelectedItems.Add( item );
				changed = true;
			}
			_expectedSelectionAfterRelease = [ .. SelectedItems ];
		}
		finally
		{
			_isUpdatingSelection = false;
		}
		if( changed )
		{
			RefreshAllRowHighlights();
			SelectionBatchCompleted?.Invoke( this, EventArgs.Empty );
		}
		return changed;
	}

	/// <summary>Replaces <see cref="ListViewBase.SelectedItems"/> in a single guarded batch, firing <see cref="SelectionBatchCompleted"/> once.</summary>
	public void SetSelectionBatch( IReadOnlyList<object> items )
	{
		List<object> target = items is List<object> list ? list : [ .. items ];
		ApplySelection( target, anchorIndex: _anchorIndex, reconcileOnRelease: false );
	}

	private bool ApplySelection( List<object> target, int anchorIndex, bool reconcileOnRelease, bool refreshIfUnchanged = true )
	{
		_anchorIndex = anchorIndex;
		_expectedSelectionAfterRelease = reconcileOnRelease ? target : null;

		if( SelectionMatches( target ) )
		{
			if( refreshIfUnchanged )
			{
				RefreshAllRowHighlights();
				return true;
			}
			return false;
		}

		_isUpdatingSelection = true;
		try
		{
			if( SelectionMode == ListViewSelectionMode.Single )
			{
				SelectedItem = target.Count > 0 ? target[ 0 ] : null;
			}
			else if( target.Count == 1 )
			{
				SelectedItems.Clear();
				SelectedItem = target[ 0 ];
			}
			else
			{
				SelectedItems.Clear();
				for( int i = 0; i < target.Count; i++ )
					SelectedItems.Add( target[ i ] );
			}
		}
		finally
		{
			_isUpdatingSelection = false;
		}
		RefreshAllRowHighlights();
		SelectionBatchCompleted?.Invoke( this, EventArgs.Empty );
		return true;
	}

	private void SetHoveredRow( TableViewRow? next )
	{
		if( ReferenceEquals( _hoveredRow, next ) )
			return;

		TableViewRow? previous = _hoveredRow;
		_hoveredRow = next;

		if( previous is not null && _realizedRows.Contains( previous ) )
			UpdateRowHighlight( previous, previous.Content );
		if( next is not null )
		{
			_realizedRows.Add( next );
			UpdateRowHighlight( next, next.Content );
		}
	}

	private void RefreshAllRowHighlights()
	{
		if( _realizedRows.Count == 0 )
			return;

		HashSet<object>? selectedSet = SelectionMode != ListViewSelectionMode.None && SelectedItems.Count > 1
			? [ .. SelectedItems ]
			: null;
		HighlightPalette palette = EnsureHighlightPalette( this );

		foreach( TableViewRow row in _realizedRows )
		{
			object? item = row.Content;
			if( ReferenceEquals( item, DependencyProperty.UnsetValue ) )
				item = null;
			UpdateRowHighlightCore( row, item, selectedSet, palette );
		}
	}

	private void UpdateRowHighlight( TableViewRow row, object? item )
	{
		if( ReferenceEquals( item, DependencyProperty.UnsetValue ) )
			item = null;
		UpdateRowHighlightCore( row, item ?? row.Content, selectedSet: null, EnsureHighlightPalette( row ) );
	}

	private void UpdateRowHighlightCore( TableViewRow row, object? item, HashSet<object>? selectedSet, HighlightPalette palette )
	{
		RowHighlightHost host = _rowHosts.GetOrCreateValue( row );
		host.PendingItem = item;

		if( !TryEnsureHostAttached( row, host ) )
		{
			ScheduleDeferredHighlightAttach( row, host );
			return;
		}

		bool selected = IsRowSelected( row, item, selectedSet );
		bool hovered = ReferenceEquals( _hoveredRow, row ) && SelectionMode != ListViewSelectionMode.None;
		ApplyOverlayVisuals( host, selected, hovered, palette );
	}

	private void ResetRowHighlight( TableViewRow row )
	{
		if( !_rowHosts.TryGetValue( row, out RowHighlightHost? host ) )
			return;
		host.PendingItem = null;
		host.DeferredGeneration++;
		host.DeferredHooked = false;
		if( host.LoadedHandler is { } handler )
		{
			row.Loaded -= handler;
			host.LoadedHandler = null;
		}
		ApplyOverlayVisuals( host, selected: false, hovered: false, palette: null );
	}

	private static void ApplyOverlayVisuals( RowHighlightHost host, bool selected, bool hovered, HighlightPalette? palette )
	{
		if( host.AppliedSelected == selected
			&& host.AppliedHovered == hovered
			&& ReferenceEquals( host.AppliedPalette, palette ) )
		{
			return;
		}
		host.AppliedSelected = selected;
		host.AppliedHovered = hovered;
		host.AppliedPalette = palette;

		if( ( selected || hovered ) && palette is not null )
		{
			host.Overlay.Background = ( selected, hovered ) switch
			{
				( true, true ) => palette.SelectedHover,
				( true, false ) => palette.Selected,
				( false, true ) => palette.Hover,
				_ => null,
			};
			host.Overlay.Visibility = Visibility.Visible;
			if( selected )
			{
				host.Indicator.Background = palette.Indicator;
				host.Indicator.Visibility = Visibility.Visible;
			}
			else
			{
				host.Indicator.Visibility = Visibility.Collapsed;
			}
			return;
		}

		host.Indicator.Visibility = Visibility.Collapsed;
		host.Overlay.Background = null;
		host.Overlay.Visibility = Visibility.Collapsed;
	}

	private void ScheduleDeferredHighlightAttach( TableViewRow row, RowHighlightHost host )
	{
		if( host.DeferredHooked )
			return;
		host.DeferredHooked = true;
		int generation = ++host.DeferredGeneration;

		void Reapply()
		{
			if( generation != host.DeferredGeneration )
				return;
			host.DeferredHooked = false;
			if( !_realizedRows.Contains( row ) )
				return;
			object? targetItem = ReferenceEquals( host.PendingItem, DependencyProperty.UnsetValue )
				? row.Content
				: host.PendingItem ?? row.Content;
			if( !TryEnsureHostAttached( row, host ) )
				return;
			bool selected = IsRowSelected( row, targetItem, selectedSet: null );
			bool hovered = ReferenceEquals( _hoveredRow, row ) && SelectionMode != ListViewSelectionMode.None;
			ApplyOverlayVisuals( host, selected, hovered, EnsureHighlightPalette( row ) );
		}

		if( !row.IsLoaded )
		{
			RoutedEventHandler? handler = null;
			handler = ( s, _ ) =>
			{
				row.Loaded -= handler;
				if( ReferenceEquals( host.LoadedHandler, handler ) )
					host.LoadedHandler = null;
				if( generation != host.DeferredGeneration )
					return;
				host.DeferredHooked = false;
				if( !_realizedRows.Contains( row ) )
					return;
				if( TryEnsureHostAttached( row, host ) )
				{
					Reapply();
					return;
				}
				if( DispatcherQueue is { } loadedQueue )
				{
					host.DeferredHooked = true;
					if( !loadedQueue.TryEnqueue( Reapply ) )
						host.DeferredHooked = false;
				}
			};
			host.LoadedHandler = handler;
			row.Loaded += handler;
		}
		else if( DispatcherQueue is { } queue )
		{
			if( !queue.TryEnqueue( Reapply ) )
				host.DeferredHooked = false;
		}
		else
		{
			host.DeferredHooked = false;
		}
	}

	private static bool TryEnsureHostAttached( TableViewRow row, RowHighlightHost host )
	{
		if( host.IsAttached )
		{
			if( host.Overlay.Parent is not null )
				return true;
			host.IsAttached = false;
			host.AppliedSelected = null;
			host.AppliedHovered = null;
			host.AppliedPalette = null;
		}

		if( VisualTreeHelper.GetChildrenCount( row ) == 0 )
			row.ApplyTemplate();

		TableViewRowPresenter? rowPresenter = null;
		Queue<DependencyObject> queue = new();
		queue.Enqueue( row );
		while( queue.Count > 0 )
		{
			DependencyObject current = queue.Dequeue();
			if( current is TableViewRowPresenter presenter )
			{
				rowPresenter ??= presenter;
				continue;
			}
#if WINDOWS
			if( current is ListViewItemPresenter chrome )
			{
				Brush transparent = EnsureTransparentBrush();
				chrome.PointerOverBackground = transparent;
				chrome.SelectedBackground = transparent;
				chrome.SelectedPointerOverBackground = transparent;
				chrome.PressedBackground = transparent;
				chrome.SelectedPressedBackground = transparent;
				chrome.SelectionIndicatorVisualEnabled = false;
			}
#else
			if( current is Border border && border.Name is "SelectionBackground" or "SelectionIndicator" )
				border.Visibility = Visibility.Collapsed;
#endif
			int count = VisualTreeHelper.GetChildrenCount( current );
			for( int i = 0; i < count; i++ )
				queue.Enqueue( VisualTreeHelper.GetChild( current, i ) );
		}

		if( rowPresenter is null )
			return false;

		if( VisualTreeHelper.GetChildrenCount( rowPresenter ) == 0 )
			rowPresenter.ApplyTemplate();

		if( VisualTreeHelper.GetChildrenCount( rowPresenter ) == 0
			|| VisualTreeHelper.GetChild( rowPresenter, 0 ) is not Grid rootGrid )
		{
			return false;
		}

		if( host.Overlay.Parent is Panel oldParent && !ReferenceEquals( oldParent, rootGrid ) )
			oldParent.Children.Remove( host.Overlay );

		if( !rootGrid.Children.Contains( host.Overlay ) )
		{
			Grid.SetRow( host.Overlay, 0 );
			rootGrid.Children.Insert( 0, host.Overlay );
		}

		host.IsAttached = true;
		return true;
	}

	private bool IsRowSelected( TableViewRow row, object? item, HashSet<object>? selectedSet )
	{
		if( SelectionMode == ListViewSelectionMode.None )
			return false;
		if( ReferenceEquals( item, DependencyProperty.UnsetValue ) )
			item = null;
		if( selectedSet is not null )
			return item is not null ? selectedSet.Contains( item ) : row.IsSelected;
		if( item is null )
			return row.IsSelected;
		if( SelectionMode == ListViewSelectionMode.Single )
			return SelectedItem is { } single && ( ReferenceEquals( single, item ) || Equals( single, item ) );
		if( SelectedItems.Count == 0 )
			return false;
		if( SelectedItems.Count == 1 )
		{
			object? only = SelectedItems[ 0 ];
			return ReferenceEquals( only, item ) || Equals( only, item );
		}
		return SelectedItems.Contains( item );
	}

	private static SolidColorBrush EnsureTransparentBrush()
		=> _transparentBrush ??= new SolidColorBrush( Color.FromArgb( 0, 0, 0, 0 ) );

	private static HighlightPalette EnsureHighlightPalette( FrameworkElement? fallback )
	{
		bool isDark = Application.Current is not null
			? AppComposition.Theme.IsDarkActive
			: fallback?.ActualTheme == ElementTheme.Dark;
		Color accent = ( Application.Current is not null
			? ThemeResources.TryGetBrush<SolidColorBrush>( "AccentFillColorDefaultBrush" )?.Color
			: null )
			?? ( isDark ? Color.FromArgb( 0xFF, 0x60, 0xCD, 0xFF ) : Color.FromArgb( 0xFF, 0x00, 0x5F, 0xB8 ) );

		if( _highlightCacheDirty || _cachedDark != isDark || _cachedAccent != accent
			|| _hoverBrush is null || _selectedBrush is null || _selectedHoverBrush is null || _indicatorBrush is null
			|| _cachedPalette is null )
		{
			_cachedDark = isDark;
			_cachedAccent = accent;
			_highlightCacheDirty = false;

			Color hoverColor = isDark ? Color.FromArgb( 0x24, 0xFF, 0xFF, 0xFF ) : Color.FromArgb( 0x16, 0x00, 0x00, 0x00 );
			Color selectedColor = Color.FromArgb( isDark ? (byte)0x3D : (byte)0x2E, accent.R, accent.G, accent.B );
			Color selectedHoverColor = Color.FromArgb( isDark ? (byte)0x54 : (byte)0x40, accent.R, accent.G, accent.B );
			Color indicatorColor = Color.FromArgb( 0xFF, accent.R, accent.G, accent.B );

			( _hoverBrush ??= new SolidColorBrush() ).Color = hoverColor;
			( _selectedBrush ??= new SolidColorBrush() ).Color = selectedColor;
			( _selectedHoverBrush ??= new SolidColorBrush() ).Color = selectedHoverColor;
			( _indicatorBrush ??= new SolidColorBrush() ).Color = indicatorColor;
			_cachedPalette = new HighlightPalette( _hoverBrush, _selectedBrush, _selectedHoverBrush, _indicatorBrush );
		}

		return _cachedPalette;
	}

	private bool SelectionMatches( List<object> expected )
	{
		if( SelectedItems.Count != expected.Count )
			return false;
		if( expected.Count == 0 )
			return true;
		if( expected.Count == 1 )
			return ReferenceEquals( SelectedItems[ 0 ], expected[ 0 ] );

		HashSet<object> set = [ .. SelectedItems ];
		foreach( object item in expected )
		{
			if( !set.Contains( item ) )
				return false;
		}
		return true;
	}

	private static bool IsKeyDown( PointerRoutedEventArgs e, VirtualKeyModifiers modifierFlag, VirtualKey key )
	{
		if( ( e.KeyModifiers & modifierFlag ) != 0 )
			return true;
		return ( InputKeyboardSource.GetKeyStateForCurrentThread( key ) & CoreVirtualKeyStates.Down ) != 0;
	}

	private TableViewRow? FindAncestorRow( DependencyObject? source )
	{
		DependencyObject? current = source;
		while( current is not null && !ReferenceEquals( current, this ) )
		{
			if( current is TableViewRow row )
				return row;
			if( current is TableViewHeaderRow or TableViewColumnHeader or ScrollBar or ButtonBase )
				return null;
			current = VisualTreeHelper.GetParent( current );
		}
		return null;
	}
}
