using System;
using System.Collections.Generic;
using System.Linq;

using HackPDM.Core.Graph;
using HackPDM.Shared.GlobalData;

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
/// Draws a laid-out dependency graph as a node editor: a box per file (colored by file type, with its
/// size and folder), ports and links from a parent's output to a child's input. Colors and shapes come
/// from <see cref="Theme"/>. Drag anywhere to pan, use the mouse wheel to zoom around the pointer.
/// </summary>
public sealed partial class NodeGraphView : Grid
{
	/// <summary>The zoom range.</summary>
	public const double MinZoom = 0.1, MaxZoom = 4;

	private const double GridSpacing = 32, GridReach = 3000;

	private readonly Canvas _canvas = new();
	private readonly CompositeTransform _transform = new();
	private readonly RectangleGeometry _clip = new();
	private readonly Dictionary<int, Border> _boxes = [];
	private readonly List<(EdgeRoute Route, Path Line, Path? Glow)> _links = [];
	private GraphLayoutResult _layout = GraphLayoutResult.Empty;
	private GraphTheme _theme = GraphTheme.Default;

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
		Background = Brush( _theme.Palette.Backdrop );
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

	/// <summary>
	/// The colors and shapes. Setting it recolors the background; the boxes change on the next
	/// <see cref="Show"/>, since a different form needs a new layout (<see cref="GraphForm.Layout"/>).
	/// </summary>
	public GraphTheme Theme
	{
		get => _theme;
		set
		{
			_theme = value;
			Background = Brush( value.Palette.Backdrop );
		}
	}

	/// <summary>The current zoom (1 = 100%).</summary>
	public double Zoom => _transform.ScaleX;

	/// <summary>The graph on screen.</summary>
	public GraphLayoutResult Layout => _layout;

	/// <summary>The selected file; null when none.</summary>
	public GraphNode? SelectedNode { get; private set; }

	/// <summary>Replaces the drawing with <paramref name="layout"/> (made with <see cref="Theme"/>'s form) and fits it to the view.</summary>
	public void Show( GraphLayoutResult layout )
	{
		_layout = layout;
		_canvas.Children.Clear();
		_boxes.Clear();
		_links.Clear();
		SelectedNode = null;
		_canvas.Width = layout.Width;
		_canvas.Height = layout.Height;

		if( _theme.Form.ShowGrid && layout.Nodes.Count > 0 )
			_canvas.Children.Add( GridLines( layout ) );
		// links under the boxes
		foreach( EdgeRoute route in layout.Edges )
		{
			var (line, glow) = Link( route );
			_links.Add( (route, line, glow) );
			if( glow is not null )
				_canvas.Children.Add( glow );
			_canvas.Children.Add( line );
		}
		HashSet<int> withInput = [ .. layout.Edges.Select( e => e.Edge.Child ) ];
		HashSet<int> withOutput = [ .. layout.Edges.Select( e => e.Edge.Parent ) ];
		foreach( NodeBox box in layout.Nodes )
		{
			if( _theme.Form.ShadowDepth > 0 )
				_canvas.Children.Add( DropShadow( box ) );
			Border border = Box( box );
			_boxes[ box.Node.Id ] = border;
			_canvas.Children.Add( border );
			if( withInput.Contains( box.Node.Id ) )
				_canvas.Children.Add( Port( box.InPort ) );
			if( withOutput.Contains( box.Node.Id ) )
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

	/// <summary>The header color of <paramref name="node"/> in the current palette.</summary>
	public Color HeaderColor( GraphNode node ) => ToColor( _theme.Palette.Header( GraphThemes.KindOf( node ) ) );

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
		GraphPalette p = _theme.Palette;
		foreach( var (id, border) in _boxes )
		{
			bool root = _layout.Find( id )?.IsRoot == true;
			border.BorderBrush = Brush( node?.Id == id ? p.SelectedBorder : root ? p.RootBorder : p.BodyBorder );
		}
		foreach( var (route, line, glow) in _links )
		{
			bool lit = node is not null && ( route.Edge.Parent == node.Id || route.Edge.Child == node.Id );
			uint color = lit ? p.LinkHighlight : p.Link;
			line.Stroke = Brush( color );
			line.StrokeThickness = _theme.Form.LinkThickness * ( lit ? 1.5 : 1 );
			glow?.Stroke = Brush( WithAlpha( color, lit ? 0x70u : 0x38u ) );
		}
		NodeSelected?.Invoke( node );
	}

	private Border Box( NodeBox box )
	{
		GraphNode node = box.Node;
		GraphForm form = _theme.Form;
		GraphPalette p = _theme.Palette;
		uint header = p.Header( GraphThemes.KindOf( node ) );
		string folder = System.IO.Path.GetDirectoryName( node.Path.Replace( '\\', '/' ) )?.Replace( '/', '\\' ) ?? "";
		string size = ByteSize.Format( node.Size, " " );
		string flags = ( node.CheckedOut ? "  ·  checked out" : "" ) + ( node.Deleted ? "  ·  deleted" : "" );
		double scale = 1 + GraphLayout.SizeFactor( node.Size ) * 0.35;
		double title = form.TitleSize * scale;
		double inner = box.Height - 2 * form.BorderThickness;

		Grid content = new();
		StackPanel body = new() { Spacing = 1, VerticalAlignment = VerticalAlignment.Top };
		double used;
		switch( form.Header )
		{
			case HeaderStyle.Strip:
			{
				double strip = form.HeaderHeight;
				content.RowDefinitions.Add( new RowDefinition { Height = new GridLength( strip ) } );
				content.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
				double r = Math.Max( 0, form.CornerRadius - form.BorderThickness );
				content.Children.Add( new Border { Background = Brush( header ), CornerRadius = new CornerRadius( r, r, 0, 0 ) } );
				body.Padding = new Thickness( 12, 7, 12, 6 );
				body.Children.Add( Text( node.Name, title, FontWeights.SemiBold, p.Text ) );
				used = strip + 7 + title * 1.4;
				break;
			}
			default:
			{
				double band = Math.Min( form.HeaderHeight * scale, inner );
				content.RowDefinitions.Add( new RowDefinition { Height = new GridLength( band ) } );
				content.RowDefinitions.Add( new RowDefinition { Height = new GridLength( 1, GridUnitType.Star ) } );
				content.Children.Add( Band( node, header, title, form, size ) );
				body.Padding = new Thickness( 10, 4, 10, 6 );
				used = band + 4;
				break;
			}
		}
		if( form.ShowSizeLine && inner - used > 14 )
		{
			body.Children.Add( Text( size + flags, 11 * Math.Min( scale, 1.2 ), FontWeights.Normal, p.Text ) );
			used += 16;
		}
		if( form.ShowFolder && inner - used > 16 )
			body.Children.Add( Text( folder, 10.5, FontWeights.Normal, p.SubText ) );
		Grid.SetRow( body, 1 );
		content.Children.Add( body );

		Border border = new()
		{
			Width = box.Width,
			Height = box.Height,
			CornerRadius = new CornerRadius( form.CornerRadius ),
			BorderThickness = new Thickness( box.IsRoot ? form.RootBorderThickness : form.BorderThickness ),
			BorderBrush = Brush( box.IsRoot ? p.RootBorder : p.BodyBorder ),
			Background = Brush( p.Body ),
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

	/// <summary>The header band: the name; compact boxes add the size, fancy ones a gradient and an extension badge.</summary>
	private Border Band( GraphNode node, uint color, double titleSize, GraphForm form, string size )
	{
		double r = Math.Max( 0, form.CornerRadius - form.BorderThickness );
		Grid row = new() { ColumnSpacing = 6 };
		row.ColumnDefinitions.Add( new ColumnDefinition { Width = new GridLength( 1, GridUnitType.Star ) } );
		row.ColumnDefinitions.Add( new ColumnDefinition { Width = GridLength.Auto } );
		row.Children.Add( Text( node.Name, titleSize, FontWeights.SemiBold, _theme.Palette.HeaderText ) );

		FrameworkElement? side = null;
		if( !form.ShowSizeLine )
			side = Text( size, titleSize * 0.85, FontWeights.Normal, WithAlpha( _theme.Palette.HeaderText, 0xD0 ) );
		else if( form.Header == HeaderStyle.Gradient && node.Ext.Length > 0 )
			side = new Border
			{
				Background = Brush( 0x40FFFFFF ),
				CornerRadius = new CornerRadius( 8 ),
				Padding = new Thickness( 7, 1, 7, 2 ),
				VerticalAlignment = VerticalAlignment.Center,
				Child = Text( node.Ext.ToUpperInvariant(), 9.5, FontWeights.Bold, _theme.Palette.HeaderText ),
			};
		if( side is not null )
		{
			Grid.SetColumn( side, 1 );
			row.Children.Add( side );
		}

		Brush fill = form.Header == HeaderStyle.Gradient
			? new LinearGradientBrush
			{
				StartPoint = new Point( 0, 0 ),
				EndPoint = new Point( 1, 1 ),
				GradientStops =
				{
					new GradientStop { Color = ToColor( Lighten( color, 0.12 ) ), Offset = 0 },
					new GradientStop { Color = ToColor( Darken( color, 0.45 ) ), Offset = 1 },
				},
			}
			: Brush( color );
		return new Border
		{
			Background = fill,
			CornerRadius = new CornerRadius( r, r, 0, 0 ),
			Padding = new Thickness( 10, 0, 8, 0 ),
			Child = row,
		};
	}

	private Border DropShadow( NodeBox box )
	{
		double d = _theme.Form.ShadowDepth;
		Border shadow = new()
		{
			Width = box.Width,
			Height = box.Height,
			CornerRadius = new CornerRadius( _theme.Form.CornerRadius + 1 ),
			Background = Brush( _theme.Palette.Shadow ),
			IsHitTestVisible = false,
		};
		Canvas.SetLeft( shadow, box.X + d * 0.4 );
		Canvas.SetTop( shadow, box.Y + d );
		return shadow;
	}

	private TextBlock Text( string text, double size, Windows.UI.Text.FontWeight weight, uint color ) => new()
	{
		Text = text,
		FontSize = size,
		FontWeight = weight,
		Foreground = Brush( color ),
		TextTrimming = TextTrimming.CharacterEllipsis,
		TextWrapping = TextWrapping.NoWrap,
		VerticalAlignment = VerticalAlignment.Center,
	};

	private Ellipse Port( (double X, double Y) at )
	{
		GraphForm form = _theme.Form;
		GraphPalette p = _theme.Palette;
		double size = form.PortSize;
		Ellipse port = new()
		{
			Width = size,
			Height = size,
			Fill = Brush( form.HollowPorts ? p.Body : p.Port ),
			Stroke = Brush( form.HollowPorts ? p.Port : p.Backdrop ),
			StrokeThickness = form.HollowPorts ? 2 : 1.5,
			IsHitTestVisible = false,
		};
		Canvas.SetLeft( port, at.X - size / 2 );
		Canvas.SetTop( port, at.Y - size / 2 );
		return port;
	}

	/// <summary>The link's line, and for glowing links a wide soft line drawn under it.</summary>
	private (Path Line, Path? Glow) Link( EdgeRoute r )
	{
		GraphForm form = _theme.Form;
		Path line = new()
		{
			Data = form.Links == LinkStyle.Elbow ? Elbow( r ) : Curve( r ),
			Stroke = Brush( _theme.Palette.Link ),
			StrokeThickness = form.LinkThickness,
			IsHitTestVisible = false,
		};
		if( form.Links != LinkStyle.GlowCurve )
			return (line, null);
		Path glow = new()
		{
			Data = Curve( r ),
			Stroke = Brush( WithAlpha( _theme.Palette.Link, 0x38 ) ),
			StrokeThickness = form.LinkThickness * 4,
			IsHitTestVisible = false,
		};
		return (line, glow);
	}

	/// <summary>A cubic curve leaving the parent to the right and entering the child from the left.</summary>
	private static PathGeometry Curve( EdgeRoute r )
	{
		double bend = Math.Max( 50, Math.Abs( r.ToX - r.FromX ) / 2 );
		PathFigure figure = new() { StartPoint = new Point( r.FromX, r.FromY ), IsClosed = false, IsFilled = false };
		figure.Segments.Add( new BezierSegment
		{
			Point1 = new Point( r.FromX + bend, r.FromY ),
			Point2 = new Point( r.ToX - bend, r.ToY ),
			Point3 = new Point( r.ToX, r.ToY ),
		} );
		return Geometry( figure );
	}

	/// <summary>Right, down (or up) halfway across, right again, with rounded bends.</summary>
	private static PathGeometry Elbow( EdgeRoute r )
	{
		PathFigure figure = new() { StartPoint = new Point( r.FromX, r.FromY ), IsClosed = false, IsFilled = false };
		double mid = r.FromX + ( r.ToX - r.FromX ) / 2;
		double dy = r.ToY - r.FromY;
		double bend = Math.Min( 12, Math.Min( Math.Abs( dy ) / 2, Math.Abs( r.ToX - r.FromX ) / 4 ) );
		if( bend < 0.5 )
		{
			figure.Segments.Add( new LineSegment { Point = new Point( r.ToX, r.ToY ) } );
			return Geometry( figure );
		}
		double sign = Math.Sign( dy );
		figure.Segments.Add( new LineSegment { Point = new Point( mid - bend, r.FromY ) } );
		figure.Segments.Add( new QuadraticBezierSegment { Point1 = new Point( mid, r.FromY ), Point2 = new Point( mid, r.FromY + sign * bend ) } );
		figure.Segments.Add( new LineSegment { Point = new Point( mid, r.ToY - sign * bend ) } );
		figure.Segments.Add( new QuadraticBezierSegment { Point1 = new Point( mid, r.ToY ), Point2 = new Point( mid + bend, r.ToY ) } );
		figure.Segments.Add( new LineSegment { Point = new Point( r.ToX, r.ToY ) } );
		return Geometry( figure );
	}

	/// <summary>Grid lines behind the graph, reaching well past it so panning does not run off the grid at once.</summary>
	private Path GridLines( GraphLayoutResult layout )
	{
		PathGeometry geometry = new();
		double left = Math.Floor( -GridReach / GridSpacing ) * GridSpacing, top = left;
		double right = layout.Width + GridReach, bottom = layout.Height + GridReach;
		for( double x = left; x <= right; x += GridSpacing )
			geometry.Figures.Add( Segment( new Point( x, top ), new Point( x, bottom ) ) );
		for( double y = top; y <= bottom; y += GridSpacing )
			geometry.Figures.Add( Segment( new Point( left, y ), new Point( right, y ) ) );
		return new Path { Data = geometry, Stroke = Brush( _theme.Palette.GridLine ), StrokeThickness = 1, IsHitTestVisible = false };

		static PathFigure Segment( Point from, Point to )
		{
			PathFigure f = new() { StartPoint = from, IsClosed = false, IsFilled = false };
			f.Segments.Add( new LineSegment { Point = to } );
			return f;
		}
	}

	private static PathGeometry Geometry( PathFigure figure )
	{
		PathGeometry geometry = new();
		geometry.Figures.Add( figure );
		return geometry;
	}

	// ------------------------------------------------------------------ colors

	private static Color ToColor( uint argb ) => Color.FromArgb( (byte)( argb >> 24 ), (byte)( argb >> 16 ), (byte)( argb >> 8 ), (byte)argb );

	private static SolidColorBrush Brush( uint argb ) => new( ToColor( argb ) );

	private static uint WithAlpha( uint argb, uint alpha ) => ( argb & 0x00FFFFFF ) | ( alpha << 24 );

	private static uint Darken( uint argb, double amount ) => Mix( argb, 0xFF000000, amount );

	private static uint Lighten( uint argb, double amount ) => Mix( argb, 0xFFFFFFFF, amount );

	private static uint Mix( uint a, uint b, double t )
	{
		uint Channel( int shift ) => (uint)Math.Round( ( ( a >> shift ) & 0xFF ) * ( 1 - t ) + ( ( b >> shift ) & 0xFF ) * t ) << shift;
		return ( a & 0xFF000000 ) | Channel( 16 ) | Channel( 8 ) | Channel( 0 );
	}
}
