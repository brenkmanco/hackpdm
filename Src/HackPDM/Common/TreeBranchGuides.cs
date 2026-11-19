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
		public TreeViewNode? SelectedNode { get; set; }
		public readonly List<WeakReference<TreeBranchGuides>> ActiveControls = [];
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
	private TreeView? _registeredTree;
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
			RegisterInTreeState();
			QueueRedraw();
		};
		Unloaded += ( _, _ ) => UnregisterFromTreeState();
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
			state.SelectedNode = selectedNode;

		state.ActiveControls.RemoveAll( r =>
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
		if( guide._subscribed is { } oldData )
			oldData.PropertyChanged -= guide.OnNodePropertyChanged;
		guide._subscribed = e.NewValue as TreeData;
		if( guide._subscribed is { } newData )
			newData.PropertyChanged += guide.OnNodePropertyChanged;
		guide.RegisterInTreeState();
		guide.QueueRedraw();
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

	private void Redraw()
	{
		Children.Clear();

		if( NodeData is not { Node: { } node, IsLinked: true } data )
			return;

		TreeViewItem? item = FindAncestor<TreeViewItem>( this );
		TreeView? tree = data.ParentTree ?? FindAncestor<TreeView>( item ?? (DependencyObject)this );
		if( item is null || tree is null )
			return;

		var state = TreeStates.GetOrCreateValue( tree );
		if( !state.ShowLines )
			return;

		double rowHeight = item.ActualHeight > 0 ? item.ActualHeight : ActualHeight;
		if( rowHeight <= 0 )
			return;

		Point originInItem;
		try
		{
			originInItem = TransformToVisual( item ).TransformPoint( new Point( 0, 0 ) );
		}
		catch
		{
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
			if( data.HasSubfolders
				&& FindDescendantByName( item, "ExpandCollapseChevron" ) is { ActualWidth: > 0, Visibility: Visibility.Visible } chevron )
			{
				try
				{
					Point chevPos = chevron.TransformToVisual( item ).TransformPoint( new Point( 0, 0 ) );
					double baseCenter = chevPos.X + chevron.ActualWidth / 2.0;
					if( state.BaseChevronCenterX is null || Math.Abs( state.BaseChevronCenterX.Value - baseCenter ) > 0.5 )
					{
						state.BaseChevronCenterX = baseCenter;
						geometryChanged = true;
					}
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
			foreach( var weak in state.ActiveControls )
			{
				if( weak.TryGetTarget( out var peer ) && !ReferenceEquals( peer, this ) )
					peer.QueueRedraw();
			}
		}

		double indent = state.DetectedIndent ?? Math.Max( 12.0, IndentWidth );
		double baseChevronCenterX = state.BaseChevronCenterX
			?? Math.Max( 12.0, ( state.BaseContentOriginX ?? ( contentStartX - depth * indent ) ) - 20.0 );

		Brush passiveBrush = ResolvePassiveBrush();
		Brush activeBrush = ResolveActiveBrush();
		TreeViewNode? selectedNode = state.SelectedNode ?? tree.SelectedNode;

		double midY = Math.Floor( topOffsetY + rowHeight / 2.0 );
		double bottomY = topOffsetY + rowHeight;

		// 1. Vertical pass-through lines for ancestors c = 0 .. depth - 2
		bool[] continuations = data.AncestorContinuations;
		for( int c = 0; c < depth - 1 && c < continuations.Length; c++ )
		{
			if( !continuations[ c ] )
				continue;
			double colX = Math.Round( baseChevronCenterX + c * indent );
			bool active = IsTrunkActiveThroughNode( node, c, selectedNode );
			AddRect( colX - contentStartX, topOffsetY, 1.0, rowHeight, active ? activeBrush : passiveBrush, active ? 0.72 : 0.42 );
		}

		// 2. Own parent connector (depth >= 1) at column depth - 1
		if( depth >= 1 )
		{
			int col = depth - 1;
			double colX = Math.Round( baseChevronCenterX + col * indent );
			bool isSelected = ReferenceEquals( node, selectedNode );
			bool isSelectedAncestor = IsAncestorOf( node, selectedNode );
			bool activeTop = isSelected || isSelectedAncestor || IsTrunkActiveThroughNode( node, col, selectedNode );
			bool activeHorizontal = isSelected || isSelectedAncestor;
			bool activeBottom = !data.IsLastSibling && IsTrunkActiveThroughNode( node, col, selectedNode );

			// Top half vertical stem (0 -> midY)
			AddRect(
				colX - contentStartX,
				topOffsetY,
				1.0,
				Math.Max( 0, midY - topOffsetY + 1 ),
				activeTop ? activeBrush : passiveBrush,
				activeTop ? 0.85 : 0.42 );

			// Bottom half vertical stem (midY -> bottom) when not the last child
			if( !data.IsLastSibling )
			{
				AddRect(
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
			AddRect(
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
				bool activeStub = IsAncestorOf( node, selectedNode );
				AddRect(
					ownColX - contentStartX,
					stubTop,
					1.0,
					stubHeight,
					activeStub ? activeBrush : passiveBrush,
					activeStub ? 0.85 : 0.42 );
			}
		}
	}

	private void AddRect( double left, double top, double width, double height, Brush brush, double opacity )
	{
		if( width <= 0 || height <= 0 )
			return;
		var rect = new Rectangle
		{
			Width = width,
			Height = height,
			Fill = brush,
			Opacity = opacity,
			IsHitTestVisible = false,
		};
		SetLeft( rect, left );
		SetTop( rect, top );
		Children.Add( rect );
	}

	private static bool IsAncestorOf( TreeViewNode candidate, TreeViewNode? target )
	{
		for( TreeViewNode? cur = target?.Parent; cur is not null; cur = cur.Parent )
		{
			if( ReferenceEquals( cur, candidate ) )
				return true;
		}
		return false;
	}

	/// <summary>
	/// Returns true when the vertical line at <paramref name="colDepth"/> passing through <paramref name="rowNode"/>
	/// is above the branch containing <paramref name="selectedNode"/> (so the accent trunk leads cleanly to it).
	/// </summary>
	private static bool IsTrunkActiveThroughNode( TreeViewNode rowNode, int colDepth, TreeViewNode? selectedNode )
	{
		if( selectedNode is null || selectedNode.Depth < colDepth + 1 )
			return false;

		int targetAncestorDepth = colDepth + 1;
		TreeViewNode? rowAncestor = AncestorAtDepth( rowNode, targetAncestorDepth );
		TreeViewNode? selAncestor = AncestorAtDepth( selectedNode, targetAncestorDepth );
		if( rowAncestor is null || selAncestor is null || !ReferenceEquals( rowAncestor.Parent, selAncestor.Parent ) )
			return false;
		if( ReferenceEquals( rowAncestor, selAncestor ) )
			return false;

		TreeViewNode? sharedParent = rowAncestor.Parent;
		if( sharedParent is null )
			return false;
		int rowIdx = sharedParent.Children.IndexOf( rowAncestor );
		int selIdx = sharedParent.Children.IndexOf( selAncestor );
		return rowIdx >= 0 && selIdx > rowIdx;
	}

	private static TreeViewNode? AncestorAtDepth( TreeViewNode? node, int targetDepth )
	{
		TreeViewNode? cur = node;
		while( cur is not null && cur.Depth > targetDepth )
			cur = cur.Parent;
		return cur is not null && cur.Depth == targetDepth ? cur : null;
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
