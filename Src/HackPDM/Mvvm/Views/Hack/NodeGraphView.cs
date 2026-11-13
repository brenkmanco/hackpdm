using System;
using System.Collections.Generic;

using HackPDM.Core.Graph;
using HackPDM.Shared.GlobalData;

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

using Windows.Foundation;
using Windows.UI;

using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>
/// Draws a laid-out dependency graph as a node editor: a box per file (header colored by file type, size
/// and folder below), orange ports and curved links from a parent's output to a child's input. Drag
/// anywhere to pan, use the mouse wheel to zoom around the pointer.
/// </summary>
public sealed partial class NodeGraphView : Grid
{
	/// <summary>The zoom range.</summary>
	public const double MinZoom = 0.1, MaxZoom = 4;

	private static readonly Color Backdrop = Color.FromArgb( 255, 30, 30, 32 );
	private static readonly Color BodyColor = Color.FromArgb( 255, 46, 46, 50 );
	private static readonly Color PortColor = Color.FromArgb( 255, 240, 140, 30 );
	private static readonly Color LinkColor = Color.FromArgb( 200, 200, 200, 205 );
	private static readonly Color LinkHighlight = Color.FromArgb( 255, 255, 170, 60 );
	private static readonly Color RootBorder = Color.FromArgb( 255, 255, 160, 40 );
	private static readonly Color SelectedBorder = Colors.White;
	private static readonly Color NormalBorder = Color.FromArgb( 255, 20, 20, 22 );
	private const double PortSize = 11, HeaderHeight = 26;

	private readonly Canvas _canvas = new();
	private readonly CompositeTransform _transform = new();
	private readonly RectangleGeometry _clip = new();
	private readonly Dictionary<int, Border> _boxes = [];
	private readonly List<(EdgeRoute Route, Path Path)> _links = [];
	private GraphLayoutResult _layout = GraphLayoutResult.Empty;

	private Point? _pressAt;
	private Point _translateAtPress;
	private bool _panning;

	/// <summary>A file's box was clicked (null when the background was clicked).</summary>
	public event Action<GraphNode?>? NodeSelected;

	/// <summary>A file's box was double-clicked.</summary>
	public event Action<GraphNode>? NodeInvoked;

	/// <summary>The zoom changed (the new zoom, 1 = 100%).</summary>
	public event Action<double>? ZoomChanged;

	public NodeGraphView()
	{
		Background = new SolidColorBrush( Backdrop );
		Clip = _clip;
		_canvas.RenderTransform = _transform;
		_canvas.HorizontalAlignment = HorizontalAlignment.Left;
		_canvas.VerticalAlignment = VerticalAlignment.Top;
		Children.Add( _canvas );

		SizeChanged += ( _, e ) => _clip.Rect = new Rect( 0, 0, e.NewSize.Width, e.NewSize.Height );
		PointerPressed += OnPointerPressed;
		PointerMoved += OnPointerMoved;
		PointerReleased += OnPointerReleased;
		PointerCaptureLost += ( _, _ ) => EndPress();
		PointerWheelChanged += OnPointerWheelChanged;
		Tapped += ( _, _ ) => Select( null );
	}

	/// <summary>The current zoom (1 = 100%).</summary>
	public double Zoom => _transform.ScaleX;

	/// <summary>The graph on screen.</summary>
	public GraphLayoutResult Layout => _layout;

	/// <summary>The selected file; null when none.</summary>
	public GraphNode? SelectedNode { get; private set; }

	/// <summary>Replaces the drawing with <paramref name="layout"/> and fits it to the view.</summary>
	public void Show( GraphLayoutResult layout )
	{
		_layout = layout;
		_canvas.Children.Clear();
		_boxes.Clear();
		_links.Clear();
		SelectedNode = null;
		_canvas.Width = layout.Width;
		_canvas.Height = layout.Height;

		// links under the boxes
		foreach( EdgeRoute route in layout.Edges )
		{
			Path path = Link( route );
			_links.Add( (route, path) );
			_canvas.Children.Add( path );
		}
		foreach( NodeBox box in layout.Nodes )
		{
			Border border = Box( box );
			_boxes[ box.Node.Id ] = border;
			_canvas.Children.Add( border );
			if( HasInput( box ) )
				_canvas.Children.Add( Port( box.InPort ) );
			if( HasOutput( box ) )
				_canvas.Children.Add( Port( box.OutPort ) );
		}
		Fit();
	}

	/// <summary>Zooms and moves the drawing so the whole graph is visible and centered (never above 100%).</summary>
	public void Fit()
	{
		if( ActualWidth <= 0 || ActualHeight <= 0 || _layout.Width <= 0 )
		{
			SetView( 1, 0, 0 );
			return;
		}
		double zoom = Math.Clamp( Math.Min( ActualWidth / _layout.Width, ActualHeight / _layout.Height ), MinZoom, 1 );
		SetView( zoom, ( ActualWidth - _layout.Width * zoom ) / 2, ( ActualHeight - _layout.Height * zoom ) / 2 );
	}

	/// <summary>Zooms by <paramref name="factor"/> around the view's center.</summary>
	public void ZoomBy( double factor ) => ZoomAt( factor, new Point( ActualWidth / 2, ActualHeight / 2 ) );

	/// <summary>Zooms by <paramref name="factor"/> keeping the drawing under <paramref name="at"/> (view coordinates) in place.</summary>
	public void ZoomAt( double factor, Point at )
	{
		double old = Zoom;
		double zoom = Math.Clamp( old * factor, MinZoom, MaxZoom );
		double ratio = zoom / old;
		SetView( zoom, at.X - ( at.X - _transform.TranslateX ) * ratio, at.Y - ( at.Y - _transform.TranslateY ) * ratio );
	}

	/// <summary>Moves the drawing by (<paramref name="dx"/>, <paramref name="dy"/>) view pixels.</summary>
	public void PanBy( double dx, double dy ) => SetView( Zoom, _transform.TranslateX + dx, _transform.TranslateY + dy );

	/// <summary>Where the drawing is: its zoom and offset.</summary>
	public (double Zoom, double X, double Y) View => (Zoom, _transform.TranslateX, _transform.TranslateY);

	/// <summary>Selects file <paramref name="id"/>'s box, as a click would; false when it is not in the graph.</summary>
	public bool SelectNode( int id )
	{
		if( _layout.Find( id ) is not { } box )
			return false;
		Select( box.Node );
		return true;
	}

	/// <summary>Moves the drawing so file <paramref name="id"/>'s box is on screen and not under the bottom <paramref name="bottomInset"/> pixels.</summary>
	public void EnsureVisible( int id, double bottomInset = 0 )
	{
		if( _layout.Find( id ) is not { } box || ActualWidth <= 0 )
			return;
		const double pad = 20;
		double z = Zoom;
		double left = box.X * z + _transform.TranslateX, top = box.Y * z + _transform.TranslateY;
		double right = left + box.Width * z, bottom = top + box.Height * z;
		double visibleBottom = Math.Max( ActualHeight - bottomInset, box.Height * z + 2 * pad );
		double dx = left < pad ? pad - left : right > ActualWidth - pad ? ActualWidth - pad - right : 0;
		double dy = top < pad ? pad - top : bottom > visibleBottom - pad ? visibleBottom - pad - bottom : 0;
		if( dx != 0 || dy != 0 )
			PanBy( dx, dy );
	}

	private void SetView( double zoom, double x, double y )
	{
		bool zoomed = Math.Abs( zoom - _transform.ScaleX ) > 1e-9;
		_transform.ScaleX = _transform.ScaleY = zoom;
		_transform.TranslateX = x;
		_transform.TranslateY = y;
		if( zoomed )
			ZoomChanged?.Invoke( zoom );
	}

	// ------------------------------------------------------------------ input

	private void OnPointerPressed( object sender, PointerRoutedEventArgs e )
	{
		var point = e.GetCurrentPoint( this );
		if( !point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed )
			return;
		_pressAt = point.Position;
		_translateAtPress = new Point( _transform.TranslateX, _transform.TranslateY );
		_panning = false;
	}

	private void OnPointerMoved( object sender, PointerRoutedEventArgs e )
	{
		if( _pressAt is not Point start )
			return;
		Point now = e.GetCurrentPoint( this ).Position;
		double dx = now.X - start.X, dy = now.Y - start.Y;
		// a click stays a click until the pointer really moves
		if( !_panning && dx * dx + dy * dy < 16 )
			return;
		if( !_panning )
		{
			_panning = true;
			CapturePointer( e.Pointer );
		}
		SetView( Zoom, _translateAtPress.X + dx, _translateAtPress.Y + dy );
		e.Handled = true;
	}

	private void OnPointerReleased( object sender, PointerRoutedEventArgs e )
	{
		if( _panning )
			e.Handled = true;
		ReleasePointerCapture( e.Pointer );
		EndPress();
	}

	private void EndPress()
	{
		_pressAt = null;
		_panning = false;
	}

	private void OnPointerWheelChanged( object sender, PointerRoutedEventArgs e )
	{
		var point = e.GetCurrentPoint( this );
		int delta = point.Properties.MouseWheelDelta;
		if( delta == 0 )
			return;
		ZoomAt( Math.Pow( 1.15, delta / 120.0 ), point.Position );
		e.Handled = true;
	}

	// ------------------------------------------------------------------ drawing

	private void Select( GraphNode? node )
	{
		SelectedNode = node;
		foreach( var (id, border) in _boxes )
		{
			bool selected = node?.Id == id;
			bool root = _layout.Find( id )?.IsRoot == true;
			border.BorderBrush = new SolidColorBrush( selected ? SelectedBorder : root ? RootBorder : NormalBorder );
		}
		foreach( var (route, path) in _links )
		{
			bool lit = node is not null && ( route.Edge.Parent == node.Id || route.Edge.Child == node.Id );
			path.Stroke = new SolidColorBrush( lit ? LinkHighlight : LinkColor );
			path.StrokeThickness = lit ? 3 : 2;
		}
		NodeSelected?.Invoke( node );
	}

	private bool HasInput( NodeBox box )
	{
		foreach( EdgeRoute e in _layout.Edges )
			if( e.Edge.Child == box.Node.Id )
				return true;
		return false;
	}

	private bool HasOutput( NodeBox box )
	{
		foreach( EdgeRoute e in _layout.Edges )
			if( e.Edge.Parent == box.Node.Id )
				return true;
		return false;
	}

	/// <summary>The header color for a file type.</summary>
	public static Color HeaderColor( GraphNode node ) => node.Deleted
		? Color.FromArgb( 255, 140, 40, 40 )
		: node.Ext switch
		{
			"sldasm" or "asm" or "iam" => Color.FromArgb( 255, 46, 125, 70 ),
			"sldprt" or "prt" or "ipt" => Color.FromArgb( 255, 40, 95, 170 ),
			"slddrw" or "drw" or "idw" or "dwg" or "dxf" => Color.FromArgb( 255, 120, 60, 150 ),
			"pdf" => Color.FromArgb( 255, 170, 70, 50 ),
			_ => Color.FromArgb( 255, 85, 95, 105 ),
		};

	private Border Box( NodeBox box )
	{
		GraphNode node = box.Node;
		string folder = System.IO.Path.GetDirectoryName( node.Path.Replace( '\\', '/' ) )?.Replace( '/', '\\' ) ?? "";
		double fontScale = 1 + GraphLayout.SizeFactor( node.Size ) * 0.35;

		Grid content = new();
		content.RowDefinitions.Add( new RowDefinition { Height = new GridLength( HeaderHeight * fontScale ) } );
		content.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );

		Border header = new()
		{
			Background = new SolidColorBrush( HeaderColor( node ) ),
			CornerRadius = new CornerRadius( 5, 5, 0, 0 ),
			Padding = new Thickness( 10, 0, 10, 0 ),
			Child = Text( node.Name, 12.5 * fontScale, FontWeights.SemiBold, Colors.White ),
		};
		content.Children.Add( header );

		StackPanel body = new() { Padding = new Thickness( 10, 4, 10, 6 ), Spacing = 1 };
		string size = ByteSize.Format( node.Size, " " );
		string flags = ( node.CheckedOut ? "  ·  checked out" : "" ) + ( node.Deleted ? "  ·  deleted" : "" );
		body.Children.Add( Text( size + flags, 11 * fontScale, FontWeights.Normal, Color.FromArgb( 255, 220, 220, 225 ) ) );
		if( box.Height - HeaderHeight * fontScale > 40 )
			body.Children.Add( Text( folder, 10.5, FontWeights.Normal, Color.FromArgb( 255, 150, 150, 158 ) ) );
		Grid.SetRow( body, 1 );
		content.Children.Add( body );

		Border border = new()
		{
			Width = box.Width,
			Height = box.Height,
			CornerRadius = new CornerRadius( 6 ),
			BorderThickness = new Thickness( box.IsRoot ? 2.5 : 1.5 ),
			BorderBrush = new SolidColorBrush( box.IsRoot ? RootBorder : NormalBorder ),
			Background = new SolidColorBrush( BodyColor ),
			Opacity = node.Deleted ? 0.7 : 1,
			Child = content,
		};
		ToolTipService.SetToolTip( border, $"{node.Path}\n{size}{( node.Category.Length > 0 ? "  ·  " + node.Category : "" )}" );
		Canvas.SetLeft( border, box.X );
		Canvas.SetTop( border, box.Y );
		border.Tapped += ( _, e ) =>
		{
			e.Handled = true;
			Select( node );
		};
		border.DoubleTapped += ( _, e ) =>
		{
			e.Handled = true;
			NodeInvoked?.Invoke( node );
		};
		return border;
	}

	private static TextBlock Text( string text, double size, Windows.UI.Text.FontWeight weight, Color color ) => new()
	{
		Text = text,
		FontSize = size,
		FontWeight = weight,
		Foreground = new SolidColorBrush( color ),
		TextTrimming = TextTrimming.CharacterEllipsis,
		TextWrapping = TextWrapping.NoWrap,
		VerticalAlignment = VerticalAlignment.Center,
	};

	private static Ellipse Port( (double X, double Y) at )
	{
		Ellipse port = new()
		{
			Width = PortSize,
			Height = PortSize,
			Fill = new SolidColorBrush( PortColor ),
			Stroke = new SolidColorBrush( Color.FromArgb( 255, 30, 30, 32 ) ),
			StrokeThickness = 1.5,
			IsHitTestVisible = false,
		};
		Canvas.SetLeft( port, at.X - PortSize / 2 );
		Canvas.SetTop( port, at.Y - PortSize / 2 );
		return port;
	}

	/// <summary>A cubic curve leaving the parent to the right and entering the child from the left.</summary>
	private static Path Link( EdgeRoute r )
	{
		double bend = Math.Max( 50, Math.Abs( r.ToX - r.FromX ) / 2 );
		PathFigure figure = new() { StartPoint = new Point( r.FromX, r.FromY ), IsClosed = false, IsFilled = false };
		figure.Segments.Add( new BezierSegment
		{
			Point1 = new Point( r.FromX + bend, r.FromY ),
			Point2 = new Point( r.ToX - bend, r.ToY ),
			Point3 = new Point( r.ToX, r.ToY ),
		} );
		PathGeometry geometry = new();
		geometry.Figures.Add( figure );
		return new Path
		{
			Data = geometry,
			Stroke = new SolidColorBrush( LinkColor ),
			StrokeThickness = 2,
			IsHitTestVisible = false,
		};
	}
}
