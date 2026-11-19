using System;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
using Windows.UI;

namespace HackPDM.Common;

/// <summary>Cross-platform vertical or horizontal pane splitter.</summary>
public sealed partial class PaneSplitter : UserControl
{
	public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
		nameof( Orientation ), typeof( Orientation ), typeof( PaneSplitter ),
		new PropertyMetadata( Orientation.Vertical, ( d, _ ) => ( (PaneSplitter)d ).ApplyOrientation() ) );

	private static readonly SolidColorBrush IdleThumbBrush = new( Color.FromArgb( 110, 140, 140, 140 ) );
	private static readonly SolidColorBrush HoverThumbBrush = new( Color.FromArgb( 200, 180, 180, 180 ) );
	private static readonly SolidColorBrush HitSurfaceBrush = new( Color.FromArgb( 1, 0, 0, 0 ) );

	private readonly Border _gripBar;
	private bool _dragging;
	private bool _hovered;
	private Point _lastPoint;

	public Orientation Orientation
	{
		get => (Orientation)GetValue( OrientationProperty );
		set => SetValue( OrientationProperty, value );
	}

	public event Action<double>? DragDelta;
	public event Action? DragCompleted;

	public PaneSplitter()
	{
		_gripBar = new Border
		{
			CornerRadius = new CornerRadius( 2 ),
			Background = IdleThumbBrush,
			Opacity = 0.55,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
		};

		Grid root = new() { Background = HitSurfaceBrush };
		root.Children.Add( _gripBar );
		Content = root;

		ApplyOrientation();

		PointerEntered += ( _, _ ) => { _hovered = true; UpdateVisualState(); };
		PointerExited += ( _, _ ) => { _hovered = false; UpdateVisualState(); };
		PointerPressed += OnPointerPressed;
		PointerMoved += OnPointerMoved;
		PointerReleased += OnPointerReleased;
		PointerCaptureLost += OnPointerReleased;
	}

	private void ApplyOrientation()
	{
		bool isVertical = Orientation == Orientation.Vertical;
		Width = isVertical ? 8 : double.NaN;
		Height = isVertical ? double.NaN : 8;
		HorizontalAlignment = HorizontalAlignment.Stretch;
		VerticalAlignment = VerticalAlignment.Stretch;
		_gripBar.Width = isVertical ? 2 : 36;
		_gripBar.Height = isVertical ? 36 : 2;
		try { ProtectedCursor = InputSystemCursor.Create( isVertical ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth ); }
		catch { }
		UpdateVisualState();
	}

	private void UpdateVisualState()
	{
		bool active = _dragging || _hovered;
		_gripBar.Background = active ? ResolveAccentBrush() : IdleThumbBrush;
		_gripBar.Opacity = _dragging ? 1.0 : _hovered ? 0.85 : 0.45;
		double length = _dragging ? 56 : _hovered ? 46 : 32;
		if( Orientation == Orientation.Vertical )
			_gripBar.Height = length;
		else
			_gripBar.Width = length;
	}

	private void OnPointerPressed( object sender, PointerRoutedEventArgs e )
	{
		if( !e.GetCurrentPoint( this ).Properties.IsLeftButtonPressed )
			return;
		_dragging = true;
		_lastPoint = e.GetCurrentPoint( null ).Position;
		CapturePointer( e.Pointer );
		UpdateVisualState();
		e.Handled = true;
	}

	private void OnPointerMoved( object sender, PointerRoutedEventArgs e )
	{
		if( !_dragging )
			return;
		Point current = e.GetCurrentPoint( null ).Position;
		double delta = Orientation == Orientation.Vertical ? current.X - _lastPoint.X : current.Y - _lastPoint.Y;
		if( Math.Abs( delta ) >= 0.5 )
		{
			_lastPoint = current;
			DragDelta?.Invoke( delta );
		}
		e.Handled = true;
	}

	private void OnPointerReleased( object sender, PointerRoutedEventArgs e )
	{
		if( !_dragging )
			return;
		_dragging = false;
		ReleasePointerCaptures();
		UpdateVisualState();
		DragCompleted?.Invoke();
		e.Handled = true;
	}

	private static Brush ResolveAccentBrush()
		=> ThemeResources.GetBrush( "AccentFillColorDefaultBrush", HoverThumbBrush );
}
