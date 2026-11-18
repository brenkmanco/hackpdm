using System;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
using Windows.UI;

namespace HackPDM.Mvvm.Views.Common.Controls;

/// <summary>
/// Interactive cross-platform pane splitter (supports vertical and horizontal orientation)
/// with hover/drag accent highlighting and delta events for live workspace resizing.
/// </summary>
public sealed partial class PaneSplitter : UserControl
{
	public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
		nameof( Orientation ), typeof( Orientation ), typeof( PaneSplitter ),
		new PropertyMetadata( Orientation.Vertical, OnOrientationChanged ) );

	private static readonly SolidColorBrush IdleThumbBrush = new( Color.FromArgb( 110, 140, 140, 140 ) );
	private static readonly SolidColorBrush HoverThumbBrush = new( Color.FromArgb( 200, 180, 180, 180 ) );

	private readonly Grid _root;
	private readonly Border _gripBar;
	private bool _dragging;
	private bool _hovered;
	private Point _lastPoint;

	public Orientation Orientation
	{
		get => (Orientation)GetValue( OrientationProperty );
		set => SetValue( OrientationProperty, value );
	}

	/// <summary>Raised during drag with pixel movement along the splitter's resize axis.</summary>
	public event Action<double>? DragDelta;

	/// <summary>Raised when a resize drag finishes (ideal for persisting dimensions).</summary>
	public event Action? DragCompleted;

	public PaneSplitter()
	{
		_gripBar = new Border
		{
			CornerRadius = new CornerRadius( 2 ),
			Background = IdleThumbBrush,
			Opacity = 0.55,
			IsHitTestVisible = false,
		};

		_root = new Grid
		{
			Background = new SolidColorBrush( Color.FromArgb( 1, 0, 0, 0 ) ),
		};
		_root.Children.Add( _gripBar );
		Content = _root;

		ApplyOrientation();

		PointerEntered += ( _, _ ) =>
		{
			_hovered = true;
			UpdateVisualState();
		};
		PointerExited += ( _, _ ) =>
		{
			_hovered = false;
			UpdateVisualState();
		};
		PointerPressed += OnPointerPressed;
		PointerMoved += OnPointerMoved;
		PointerReleased += OnPointerReleased;
		PointerCaptureLost += OnPointerReleased;
	}

	private static void OnOrientationChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		=> ( (PaneSplitter)d ).ApplyOrientation();

	private void ApplyOrientation()
	{
		bool isVertical = Orientation == Orientation.Vertical;
		if( isVertical )
		{
			Width = 8;
			Height = double.NaN;
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;
			_gripBar.Width = 2;
			_gripBar.Height = 36;
			_gripBar.HorizontalAlignment = HorizontalAlignment.Center;
			_gripBar.VerticalAlignment = VerticalAlignment.Center;
			TrySetCursor( InputSystemCursorShape.SizeWestEast );
		}
		else
		{
			Width = double.NaN;
			Height = 8;
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;
			_gripBar.Width = 36;
			_gripBar.Height = 2;
			_gripBar.HorizontalAlignment = HorizontalAlignment.Center;
			_gripBar.VerticalAlignment = VerticalAlignment.Center;
			TrySetCursor( InputSystemCursorShape.SizeNorthSouth );
		}
		UpdateVisualState();
	}

	private void TrySetCursor( InputSystemCursorShape shape )
	{
		try
		{
			ProtectedCursor = InputSystemCursor.Create( shape );
		}
		catch
		{
			// Some headless hosts don't implement custom cursors
		}
	}

	private void UpdateVisualState()
	{
		if( _dragging )
		{
			_gripBar.Background = ResolveAccentBrush();
			_gripBar.Opacity = 1.0;
			if( Orientation == Orientation.Vertical )
				_gripBar.Height = 56;
			else
				_gripBar.Width = 56;
		}
		else if( _hovered )
		{
			_gripBar.Background = ResolveAccentBrush();
			_gripBar.Opacity = 0.85;
			if( Orientation == Orientation.Vertical )
				_gripBar.Height = 46;
			else
				_gripBar.Width = 46;
		}
		else
		{
			_gripBar.Background = IdleThumbBrush;
			_gripBar.Opacity = 0.45;
			if( Orientation == Orientation.Vertical )
				_gripBar.Height = 32;
			else
				_gripBar.Width = 32;
		}
	}

	private void OnPointerPressed( object sender, PointerRoutedEventArgs e )
	{
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
		double delta = Orientation == Orientation.Vertical
			? current.X - _lastPoint.X
			: current.Y - _lastPoint.Y;
		_lastPoint = current;
		if( Math.Abs( delta ) >= 0.5 )
			DragDelta?.Invoke( delta );
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
	{
		if( Application.Current.Resources.TryGetValue( "AccentFillColorDefaultBrush", out object? val ) && val is Brush b )
			return b;
		return HoverThumbBrush;
	}
}
