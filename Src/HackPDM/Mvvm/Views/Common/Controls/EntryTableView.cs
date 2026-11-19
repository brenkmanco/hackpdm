using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

using WinUI.TableView;

namespace HackPDM.Mvvm.Views.Common.Controls;

/// <summary>
/// <see cref="TableView"/> wrapper that fixes three <c>WinUI.TableView</c> 1.5.1 selection quirks on Uno/WinUI:
/// <list type="bullet">
/// <item>Clears stale <c>SelectedItems</c> on unmodified left-clicks when <c>TableView</c>'s internal <c>SelectedRanges</c> is out of sync (e.g. after programmatic selection).</item>
/// <item>Suppresses accidental zero-threshold drag-selection on 1–2 px click jitter (and disables drag-select when <c>ShowDragRectangle</c> is false).</item>
/// <item>Ends any in-progress drag selection on <c>PointerCaptureLost</c> / <c>PointerCanceled</c> so selection never gets stuck.</item>
/// </list>
/// </summary>
public partial class EntryTableView : TableView
{
	private const double DragThresholdSq = 64.0; // 8 px

	private Point _pressPoint;
	private bool _leftPressActive;
	private bool _dragThresholdMet;

	protected override void OnPointerPressed( PointerRoutedEventArgs e )
	{
		var point = e.GetCurrentPoint( this );
		if( point.Properties.IsLeftButtonPressed )
		{
			_pressPoint = point.Position;
			_leftPressActive = true;
			_dragThresholdMet = false;

			if( SelectionMode == ListViewSelectionMode.Extended
				&& !HasMultiSelectModifier( e )
				&& FindAncestorRow( e.OriginalSource as DependencyObject ) is { } row
				&& ItemFromContainer( row ) is { } clickedItem
				&& SelectedItems.Count > 0
				&& ( SelectedItems.Count > 1 || !ReferenceEquals( SelectedItems[ 0 ], clickedItem ) ) )
			{
				// WinUI.TableView's DeselectAllItems() short-circuits when its private SelectedRanges
				// is empty (after programmatic SelectedItem/SelectedItems.Add), leaving old items selected.
				SelectedItems.Clear();
			}
		}

		base.OnPointerPressed( e );
	}

	protected override void OnPointerMoved( PointerRoutedEventArgs e )
	{
		if( _leftPressActive )
		{
			if( !ShowDragRectangle )
				return;

			if( !_dragThresholdMet )
			{
				Point now = e.GetCurrentPoint( this ).Position;
				double dx = now.X - _pressPoint.X;
				double dy = now.Y - _pressPoint.Y;
				if( dx * dx + dy * dy < DragThresholdSq )
					return;
				_dragThresholdMet = true;
			}
		}

		base.OnPointerMoved( e );
	}

	protected override void OnPointerReleased( PointerRoutedEventArgs e )
	{
		_leftPressActive = false;
		_dragThresholdMet = false;
		base.OnPointerReleased( e );
	}

	protected override void OnPointerCaptureLost( PointerRoutedEventArgs e )
	{
		if( _leftPressActive )
		{
			_leftPressActive = false;
			_dragThresholdMet = false;
			base.OnPointerReleased( e );
		}
		base.OnPointerCaptureLost( e );
	}

	protected override void OnPointerCanceled( PointerRoutedEventArgs e )
	{
		if( _leftPressActive )
		{
			_leftPressActive = false;
			_dragThresholdMet = false;
			base.OnPointerReleased( e );
		}
		base.OnPointerCanceled( e );
	}

	private static bool HasMultiSelectModifier( PointerRoutedEventArgs e )
	{
		if( ( e.KeyModifiers & ( VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift ) ) != 0 )
			return true;
		var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread( VirtualKey.Control );
		var shift = InputKeyboardSource.GetKeyStateForCurrentThread( VirtualKey.Shift );
		return ctrl.HasFlag( CoreVirtualKeyStates.Down ) || shift.HasFlag( CoreVirtualKeyStates.Down );
	}

	private TableViewRow? FindAncestorRow( DependencyObject? source )
	{
		DependencyObject? current = source;
		while( current is not null && !ReferenceEquals( current, this ) )
		{
			if( current is TableViewRow row )
				return row;
			if( current is TableViewHeaderRow or TableViewColumnHeader or ScrollBar )
				return null;
			current = VisualTreeHelper.GetParent( current );
		}
		return null;
	}
}
