using System;
using System.Collections.Generic;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.System;
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
public partial class EntryTableView : TableView
{
	private int _anchorIndex = -1;
	private bool _isUpdatingSelection;
	private bool _clearJustAddedScheduled;
	private readonly HashSet<object> _justAddedByTableView = [];
	private List<object>? _expectedSelectionAfterRelease;

	public EntryTableView()
	{
		AddHandler( PointerPressedEvent, new PointerEventHandler( OnRowPointerPressed ), handledEventsToo: true );
		AddHandler( PointerReleasedEvent, new PointerEventHandler( OnRowPointerReleased ), handledEventsToo: true );
		SelectionChanged += OnTableSelectionChanged;
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
				ApplySelection( [ item ], anchorIndex: index, reconcileOnRelease: false );
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

		switch( SelectionMode )
		{
			case ListViewSelectionMode.Single:
				ApplySelection( [ item ], anchorIndex: index, reconcileOnRelease: true );
				break;

			case ListViewSelectionMode.Multiple:
				ToggleItemSelection( item, index );
				break;

			case ListViewSelectionMode.Extended:
				ApplyExtendedSelection( item, index, ctrl, shift );
				break;
		}

		_justAddedByTableView.Clear();
		row.Focus( FocusState.Pointer );
		e.Handled = true;
	}

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
		base.OnPointerReleased( e );
		base.OnPointerCaptureLost( e );
	}

	protected override void OnPointerCanceled( PointerRoutedEventArgs e )
	{
		_expectedSelectionAfterRelease = null;
		base.OnPointerReleased( e );
		base.OnPointerCanceled( e );
	}

	private void ApplyExtendedSelection( object item, int index, bool ctrl, bool shift )
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
			ApplySelection( target, anchorIndex: anchor, reconcileOnRelease: true );
			return;
		}

		if( ctrl )
		{
			ToggleItemSelection( item, index );
			return;
		}

		ApplySelection( [ item ], anchorIndex: index, reconcileOnRelease: true );
	}

	private void ToggleItemSelection( object item, int index )
	{
		bool wasSelectedBeforePress = SelectedItems.Contains( item ) && !_justAddedByTableView.Contains( item );
		List<object> target = [];
		foreach( object existing in SelectedItems )
		{
			if( wasSelectedBeforePress && ReferenceEquals( existing, item ) )
				continue;
			target.Add( existing );
		}
		if( !wasSelectedBeforePress && !target.Contains( item ) )
			target.Add( item );

		ApplySelection( target, anchorIndex: index, reconcileOnRelease: true );
	}

	private void ApplySelection( List<object> target, int anchorIndex, bool reconcileOnRelease )
	{
		_anchorIndex = anchorIndex;
		_expectedSelectionAfterRelease = reconcileOnRelease ? target : null;

		if( SelectionMatches( target ) )
			return;

		_isUpdatingSelection = true;
		try
		{
			if( SelectionMode == ListViewSelectionMode.Single )
			{
				SelectedItem = target.Count > 0 ? target[ 0 ] : null;
			}
			else
			{
				SelectedItems.Clear();
				foreach( object item in target )
					SelectedItems.Add( item );
			}
		}
		finally
		{
			_isUpdatingSelection = false;
		}
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
		return InputKeyboardSource.GetKeyStateForCurrentThread( key ).HasFlag( CoreVirtualKeyStates.Down );
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
