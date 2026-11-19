using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

using Windows.Foundation;
using Windows.UI;

using TreeView = Microsoft.UI.Xaml.Controls.TreeView;

namespace HackPDM.Common;

/// <summary>
/// Draws continuous branching visual guide lines (<c>│</c>, <c>├─</c>, <c>└─</c>) and expanded-parent stubs
/// into the indent gutter to the left of each <see cref="TreeViewItem"/> row, highlighting the path
/// leading to the currently selected directory.
/// </summary>
public sealed partial class TreeBranchGuides : Canvas
{
	private sealed class TreeRenderState
	{
		public bool ShowLines { get; set; } = true;
		public double? BaseChevronCenterX { get; set; }
		public double? BaseContentOriginX { get; set; }
		public double? DetectedIndent { get; set; }
		public TreeViewNode? SelectedNode { get; private set; }
		public TreeViewNode?[] SelectedAncestors { get; private set; } = [];
		public Brush? PassiveBrush { get; set; }
		public Brush? ActiveBrush { get; set; }
		public readonly List<WeakReference<TreeBranchGuides>> ActiveControls = [];

		public void SetSelectedNode( TreeViewNode? selected )
		{
			SelectedNode = selected;
			if( selected is null || selected.Depth < 0 )
			{
				SelectedAncestors = [];
				return;
			}
			int depth = selected.Depth;
			TreeViewNode?[] chain = new TreeViewNode?[ depth + 1 ];
			for( TreeViewNode? cur = selected; cur is not null && cur.Depth >= 0; cur = cur.Parent )
			{
				if( cur.Depth < chain.Length )
					chain[ cur.Depth ] = cur;
			}
			SelectedAncestors = chain;
		}
	}

	private static readonly ConditionalWeakTable<TreeView, TreeRenderState> TreeStates = new();

	public static readonly DependencyProperty NodeDataProperty = DependencyProperty.Register(
		nameof( NodeData ), typeof( TreeData ), typeof( TreeBranchGuides ),
		new PropertyMetadata( null, OnNodeDataChanged ) );

	public static readonly DependencyProperty BranchRevisionProperty = DependencyProperty.Register(
		nameof( BranchRevision ), typeof( int ), typeof( TreeBranchGuides ),
		new PropertyMetadata( 0, OnBranchPropertyChanged ) );

	public static readonly DependencyProperty IndentWidthProperty = DependencyProperty.Register(
		nameof( IndentWidth ), typeof( double ), typeof( TreeBranchGuides ),
		new PropertyMetadata( 18.0, OnBranchPropertyChanged ) );

	private TreeData? _subscribed;
	private bool _isSubscribed;
	private TreeView? _registeredTree;
	private TreeViewItem? _cachedItem;
	private readonly List<Rectangle> _rectPool = [];
	private bool _redrawQueued;

	public TreeData? NodeData
	{
		get => (TreeData?)GetValue( NodeDataProperty );
		set => SetValue( NodeDataProperty, value );
	}

	public int BranchRevision
	{
		get => (int)GetValue( BranchRevisionProperty );
		set => SetValue( BranchRevisionProperty, value );
	}

	public double IndentWidth
	{
		get => (double)GetValue( IndentWidthProperty );
		set => SetValue( IndentWidthProperty, value );
	}

	public TreeBranchGuides()
	{
		Width = 0;
		HorizontalAlignment = HorizontalAlignment.Left;
		VerticalAlignment = VerticalAlignment.Stretch;
		IsHitTestVisible = false;

		Loaded += ( _, _ ) =>
		{
			_cachedItem = FindAncestor<TreeViewItem>( this );
			SubscribeData();
			RegisterInTreeState();
			QueueRedraw();
		};
		Unloaded += ( _, _ ) =>
		{
			_cachedItem = null;
			UnsubscribeData();
			UnregisterFromTreeState();
		};
		SizeChanged += ( _, _ ) => QueueRedraw();
	}

	/// <summary>Updates branch-line visibility and selected-path highlighting on every realized item in <paramref name="tree"/>.</summary>
	public static void RefreshTree( TreeView? tree, bool? showLines = null, TreeViewNode? selectedNode = null, bool updateSelection = false )
	{
		if( tree is null )
			return;
		var state = TreeStates.GetOrCreateValue( tree );
		if( showLines is bool show )
			state.ShowLines = show;
		if( updateSelection )
			state.SetSelectedNode( selectedNode );
		state.PassiveBrush = ResolvePassiveBrush();
		state.ActiveBrush = ResolveActiveBrush();

		state.ActiveControls.RemoveAll( static r =>
		{
			if( !r.TryGetTarget( out var control ) )
				return true;
			control.QueueRedraw();
			return false;
		} );
	}

	private static void OnNodeDataChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
	{
		var guide = (TreeBranchGuides)d;
		guide.UnsubscribeData();
		guide._subscribed = e.NewValue as TreeData;
		if( guide.IsLoaded )
			guide.SubscribeData();
		guide.RegisterInTreeState();
		guide.QueueRedraw();
	}

	private void SubscribeData()
	{
		if( _isSubscribed || _subscribed is null )
			return;
		_subscribed.PropertyChanged += OnNodePropertyChanged;
		_isSubscribed = true;
	}

	private void UnsubscribeData()
	{
		if( !_isSubscribed || _subscribed is null )
			return;
		_subscribed.PropertyChanged -= OnNodePropertyChanged;
		_isSubscribed = false;
	}

	private static void OnBranchPropertyChanged( DependencyObject d, DependencyPropertyChangedEventArgs e )
		=> ( (TreeBranchGuides)d ).QueueRedraw();

	private void OnNodePropertyChanged( object? sender, PropertyChangedEventArgs e )
	{
		if( e.PropertyName is nameof( TreeData.BranchRevision )
			or nameof( TreeData.IsExpanded )
			or nameof( TreeData.IsLastSibling )
			or nameof( TreeData.AncestorContinuations ) )
		{
			QueueRedraw();
		}
	}

	private void RegisterInTreeState()
	{
		TreeView? tree = NodeData?.ParentTree ?? FindAncestor<TreeView>( this );
		if( ReferenceEquals( tree, _registeredTree ) )
			return;
		UnregisterFromTreeState();
		if( tree is null )
			return;
		_registeredTree = tree;
		var state = TreeStates.GetOrCreateValue( tree );
		state.ActiveControls.Add( new WeakReference<TreeBranchGuides>( this ) );
	}

	private void UnregisterFromTreeState()
	{
		if( _registeredTree is null )
			return;
		if( TreeStates.TryGetValue( _registeredTree, out var state ) )
			state.ActiveControls.RemoveAll( r => !r.TryGetTarget( out var target ) || ReferenceEquals( target, this ) );
		_registeredTree = null;
	}

	private void QueueRedraw()
	{
		if( _redrawQueued )
			return;
		_redrawQueued = true;
		if( DispatcherQueue is { } dq )
		{
			dq.TryEnqueue( () =>
			{
				_redrawQueued = false;
				Redraw();
			} );
		}
		else
		{
			_redrawQueued = false;
			Redraw();
		}
	}

	private void HideUnusedRects( int usedCount )
	{
		for( int i = usedCount; i < _rectPool.Count; i++ )
		{
			if( _rectPool[ i ].Visibility != Visibility.Collapsed )
				_rectPool[ i ].Visibility = Visibility.Collapsed;
		}
	}

	private void Redraw()
	{
		int usedRects = 0;
		if( NodeData is not { Node: { } node, IsLinked: true } data )
		{
			HideUnusedRects( 0 );
			return;
		}

		TreeViewItem? item = _cachedItem ??= FindAncestor<TreeViewItem>( this );
		TreeView? tree = data.ParentTree ?? _registeredTree ?? FindAncestor<TreeView>( item ?? (DependencyObject)this );
		if( item is null || tree is null )
		{
			HideUnusedRects( 0 );
			return;
		}

		var state = TreeStates.GetOrCreateValue( tree );
		if( !state.ShowLines )
		{
			HideUnusedRects( 0 );
			return;
		}

		double rowHeight = item.ActualHeight > 0 ? item.ActualHeight : ActualHeight;
		if( rowHeight <= 0 )
		{
			HideUnusedRects( 0 );
			return;
		}

		Point originInItem;
		try
		{
			originInItem = TransformToVisual( item ).TransformPoint( new Point( 0, 0 ) );
		}
		catch
		{
			HideUnusedRects( 0 );
			return;
		}

		double contentStartX = originInItem.X;
		double topOffsetY = -originInItem.Y;
		int depth = Math.Max( 0, data.Depth );

		// Capture stable base content origin & chevron center from depth 0, and real indent from child depths
		bool geometryChanged = false;
		if( depth == 0 )
		{
			if( state.BaseContentOriginX is null || Math.Abs( state.BaseContentOriginX.Value - contentStartX ) > 0.5 )
			{
				state.BaseContentOriginX = contentStartX;
				geometryChanged = true;
			}
			if( state.BaseChevronCenterX is null
				&& data.HasSubfolders
				&& FindDescendantByName( item, "ExpandCollapseChevron" ) is { ActualWidth: > 0, Visibility: Visibility.Visible } chevron )
			{
				try
				{
					Point chevPos = chevron.TransformToVisual( item ).TransformPoint( new Point( 0, 0 ) );
					double baseCenter = chevPos.X + chevron.ActualWidth / 2.0;
					state.BaseChevronCenterX = baseCenter;
					geometryChanged = true;
				}
				catch { }
			}
		}
		else if( state.BaseContentOriginX is double baseOrig && contentStartX > baseOrig + 4.0 )
		{
			double detected = ( contentStartX - baseOrig ) / depth;
			if( state.DetectedIndent is null || Math.Abs( state.DetectedIndent.Value - detected ) > 0.5 )
			{
				state.DetectedIndent = detected;
				geometryChanged = true;
			}
		}

		if( geometryChanged )
		{
			for( int i = 0; i < state.ActiveControls.Count; i++ )
			{
				if( state.ActiveControls[ i ].TryGetTarget( out var peer ) && !ReferenceEquals( peer, this ) )
					peer.QueueRedraw();
			}
		}

		double indent = state.DetectedIndent ?? Math.Max( 12.0, IndentWidth );
		double baseChevronCenterX = state.BaseChevronCenterX
			?? Math.Max( 12.0, ( state.BaseContentOriginX ?? ( contentStartX - depth * indent ) ) - 20.0 );

		Brush passiveBrush = state.PassiveBrush ??= ResolvePassiveBrush();
		Brush activeBrush = state.ActiveBrush ??= ResolveActiveBrush();
		TreeViewNode? selectedNode = state.SelectedNode ?? tree.SelectedNode;
		if( selectedNode is not null && !ReferenceEquals( selectedNode, state.SelectedNode ) )
			state.SetSelectedNode( selectedNode );
		TreeViewNode?[] selectedAncestors = state.SelectedAncestors;

		// Build row's ancestor chain once in O(depth) so all column checks are O(1)
		TreeViewNode?[]? rowAncestors = null;
		if( depth >= 1 && selectedAncestors.Length > 0 )
		{
			rowAncestors = new TreeViewNode?[ depth + 1 ];
			for( TreeViewNode? cur = node; cur is not null && cur.Depth >= 0; cur = cur.Parent )
			{
				if( cur.Depth <= depth )
					rowAncestors[ cur.Depth ] = cur;
			}
		}

		bool isSelected = ReferenceEquals( node, selectedNode );
		bool isSelectedAncestor = selectedAncestors.Length > depth + 1 && ReferenceEquals( selectedAncestors[ depth ], node );

		double midY = Math.Floor( topOffsetY + rowHeight / 2.0 );
		double bottomY = topOffsetY + rowHeight;

		// 1. Vertical pass-through lines for ancestors c = 0 .. depth - 2
		bool[] continuations = data.AncestorContinuations;
		for( int c = 0; c < depth - 1 && c < continuations.Length; c++ )
		{
			if( !continuations[ c ] )
				continue;
			double colX = Math.Round( baseChevronCenterX + c * indent );
			bool active = IsTrunkActiveO1( rowAncestors, c, selectedAncestors );
			PlaceRect( ref usedRects, colX - contentStartX, topOffsetY, 1.0, rowHeight, active ? activeBrush : passiveBrush, active ? 0.72 : 0.42 );
		}

		// 2. Own parent connector (depth >= 1) at column depth - 1
		if( depth >= 1 )
		{
			int col = depth - 1;
			double colX = Math.Round( baseChevronCenterX + col * indent );
			bool trunkActive = IsTrunkActiveO1( rowAncestors, col, selectedAncestors );
			bool activeTop = isSelected || isSelectedAncestor || trunkActive;
			bool activeHorizontal = isSelected || isSelectedAncestor;
			bool activeBottom = !data.IsLastSibling && trunkActive;

			// Top half vertical stem (0 -> midY)
			PlaceRect(
				ref usedRects,
				colX - contentStartX,
				topOffsetY,
				1.0,
				Math.Max( 0, midY - topOffsetY + 1 ),
				activeTop ? activeBrush : passiveBrush,
				activeTop ? 0.85 : 0.42 );

			// Bottom half vertical stem (midY -> bottom) when not the last child
			if( !data.IsLastSibling )
			{
				PlaceRect(
					ref usedRects,
					colX - contentStartX,
					midY + 1,
					1.0,
					Math.Max( 0, bottomY - ( midY + 1 ) ),
					activeBottom ? activeBrush : passiveBrush,
					activeBottom ? 0.72 : 0.42 );
			}

			// Horizontal branch arm to chevron (parent) or directly to icon (leaf)
			double armEndX = data.HasSubfolders
				? baseChevronCenterX + depth * indent - 9.0
				: contentStartX - 5.0;
			double armWidth = Math.Max( 4.0, Math.Round( armEndX - colX ) );
			PlaceRect(
				ref usedRects,
				colX - contentStartX,
				midY,
				armWidth,
				1.0,
				activeHorizontal ? activeBrush : passiveBrush,
				activeHorizontal ? 0.90 : 0.42 );
		}

		// 3. Expanded parent stub directly beneath this row's own chevron
		if( data.HasSubfolders && data.IsExpanded )
		{
			double ownColX = Math.Round( baseChevronCenterX + depth * indent );
			double stubTop = Math.Min( bottomY, midY + 8.0 );
			double stubHeight = Math.Max( 0.0, bottomY - stubTop );
			if( stubHeight > 0 )
			{
				PlaceRect(
					ref usedRects,
					ownColX - contentStartX,
					stubTop,
					1.0,
					stubHeight,
					isSelectedAncestor ? activeBrush : passiveBrush,
					isSelectedAncestor ? 0.85 : 0.42 );
			}
		}

		HideUnusedRects( usedRects );
	}

	private void PlaceRect( ref int usedCount, double left, double top, double width, double height, Brush brush, double opacity )
	{
		if( width <= 0 || height <= 0 )
			return;
		Rectangle rect;
		if( usedCount < _rectPool.Count )
		{
			rect = _rectPool[ usedCount ];
			if( rect.Visibility != Visibility.Visible )
				rect.Visibility = Visibility.Visible;
		}
		else
		{
			rect = new Rectangle { IsHitTestVisible = false };
			_rectPool.Add( rect );
			Children.Add( rect );
		}
		usedCount++;
		rect.Width = width;
		rect.Height = height;
		rect.Fill = brush;
		rect.Opacity = opacity;
		SetLeft( rect, left );
		SetTop( rect, top );
	}

	/// <summary>
	/// O(1) check whether the vertical line at <paramref name="colDepth"/> passing through the row
	/// sits above the branch containing the selected node.
	/// </summary>
	private static bool IsTrunkActiveO1( TreeViewNode?[]? rowAncestors, int colDepth, TreeViewNode?[] selectedAncestors )
	{
		int targetDepth = colDepth + 1;
		if( rowAncestors is null || targetDepth >= rowAncestors.Length || targetDepth >= selectedAncestors.Length )
			return false;
		TreeViewNode? rowAncestor = rowAncestors[ targetDepth ];
		TreeViewNode? selAncestor = selectedAncestors[ targetDepth ];
		if( rowAncestor is null || selAncestor is null || ReferenceEquals( rowAncestor, selAncestor ) )
			return false;
		if( !ReferenceEquals( rowAncestor.Parent, selAncestor.Parent ) )
			return false;
		return selAncestor.LinkedData.SiblingIndex > rowAncestor.LinkedData.SiblingIndex;
	}

	private static readonly SolidColorBrush PassiveFallbackBrush = new( Color.FromArgb( 255, 140, 140, 140 ) );
	private static readonly SolidColorBrush ActiveFallbackBrush = new( Color.FromArgb( 255, 76, 194, 255 ) );

	private static Brush ResolveBrush( string primaryKey, string secondaryKey, Brush fallback )
	{
		var res = Application.Current.Resources;
		if( res.TryGetValue( primaryKey, out object? a ) && a is Brush ba )
			return ba;
		if( res.TryGetValue( secondaryKey, out object? b ) && b is Brush bb )
			return bb;
		return fallback;
	}

	private static Brush ResolvePassiveBrush()
		=> ResolveBrush( "TextFillColorSecondaryBrush", "DividerStrokeColorDefaultBrush", PassiveFallbackBrush );

	private static Brush ResolveActiveBrush()
		=> ResolveBrush( "AccentTextFillColorPrimaryBrush", "AccentFillColorDefaultBrush", ActiveFallbackBrush );

	private static T? FindAncestor<T>( DependencyObject? start ) where T : class
	{
		for( DependencyObject? cur = start; cur is not null; cur = VisualTreeHelper.GetParent( cur ) )
		{
			if( cur is T match && !ReferenceEquals( cur, start ) )
				return match;
		}
		return null;
	}

	private static FrameworkElement? FindDescendantByName( DependencyObject root, string name )
	{
		int count = VisualTreeHelper.GetChildrenCount( root );
		for( int i = 0; i < count; i++ )
		{
			DependencyObject child = VisualTreeHelper.GetChild( root, i );
			if( child is FrameworkElement fe && fe.Name == name )
				return fe;
			if( FindDescendantByName( child, name ) is { } nested )
				return nested;
		}
		return null;
	}
}
