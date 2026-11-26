using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Graph;
using HackPDM.Core.Odoo;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

using DetailRow = HackPDM.Core.Services.EntryRow;

namespace HackPDM.Graph;

/// <summary>Converts any non-null boxed property value to its display string without reflection.</summary>
public sealed partial class ValueTextConverter : IValueConverter
{
	public object Convert( object? value, Type targetType, object? parameter, string language )
		=> value?.ToString() ?? string.Empty;

	public object ConvertBack( object? value, Type targetType, object? parameter, string language )
		=> throw new NotSupportedException();
}

/// <summary>A file in the dependency page's list.</summary>
[Bindable]
public sealed class GraphEntryItem( int id, string name )
{
	public int Id { get; } = id;
	public string Name { get; } = name;

	public override string ToString() => Name;
}

public enum GraphDetailTab
{
	History,
	WhereUsed,
	Dependents,
	Properties,
	Info,
}

/// <summary>View model for <see cref="DependencyGraphView"/>: folder tree state, entries, dependency graph layout, and file details.</summary>
[Bindable]
public sealed partial class DependencyGraphViewModel : ObservableObject, IDisposable
{
	private static readonly char[] PathSeparators = [ '\\', '/' ];

	private readonly IEntryDetailsService _details;
	private readonly IPreviewImageLoader _previews;
	private readonly CancellationTokenSource _lifetimeCts = new();
	private readonly Dictionary<int, GraphNode> _nodesById = [];
	private readonly Dictionary<int, List<int>> _childrenByParent = [];
	private readonly Dictionary<int, List<int>> _parentsByChild = [];

	private CancellationTokenSource? _treeLoadCts;
	private CancellationTokenSource? _entriesLoadCts;
	private CancellationTokenSource? _graphLoadCts;
	private CancellationTokenSource? _detailLoadCts;
	private CancellationTokenSource? _previewLoadCts;
	private CancellationTokenSource? _themeLayoutCts;
	private long _previewGeneration;

	private DependencyGraph _graph = DependencyGraph.Empty;
	private IReadOnlyList<int> _roots = [];
	private GraphNode? _detailNode;
	private GraphForm _currentForm = GraphTheme.Default.Form;
	private GraphDetailTab _selectedDetailTab = GraphDetailTab.History;
	private bool _suppressDirectionReload;

	public DependencyGraphViewModel( IEntryDetailsService details, IPreviewImageLoader previews )
	{
		_details = details;
		_previews = previews;
	}

	public event Action<GraphLayoutResult>? GraphArranged;
	public event Action<GraphEntryItem>? SelectEntryInListRequested;
	public event Action? ClearEntryListSelectionRequested;
	public event Action<double>? ZoomRequested;
	public event Action? FitRequested;

	public RangeObservableCollection<TreeData> BreadcrumbPath { get; } = [];
	public RangeObservableCollection<GraphEntryItem> Entries { get; } = [];
	public RangeObservableCollection<HistoryRow> HistoryRows { get; } = [];
	public RangeObservableCollection<ParentRow> ParentRows { get; } = [];
	public RangeObservableCollection<ChildrenRow> ChildRows { get; } = [];
	public RangeObservableCollection<PropertiesRow> PropertyRows { get; } = [];
	public RangeObservableCollection<VersionRow> VersionRows { get; } = [];

	public SolidColorBrush InfoHeaderBrush { get; } = new();

	public bool IsDisposed { get; private set; }
	public Task? PendingLoad { get; set; }
	public Task? DetailsLoad { get; private set; }
	public IReadOnlyList<int> Roots => _graph.Roots;
	public GraphNode? DetailNode => _detailNode;

	[ObservableProperty] public partial bool IsTreeLoading { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsBusy ) )]
	public partial bool IsEntriesLoading { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsBusy ) )]
	public partial bool IsGraphLoading { get; set; }

	[ObservableProperty] public partial bool IsGraphBusyOverlayVisible { get; set; }
	[ObservableProperty] public partial bool ShowHint { get; set; } = true;
	[ObservableProperty] public partial string EntryCountText { get; set; } = string.Empty;
	[ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
	[ObservableProperty] public partial string ZoomText { get; set; } = "100%";

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsChildrenDirection ) )]
	[NotifyPropertyChangedFor( nameof( IsParentsDirection ) )]
	[NotifyPropertyChangedFor( nameof( IsBothDirection ) )]
	public partial GraphDirection Direction { get; set; } = GraphDirection.Children;

	public bool IsChildrenDirection
	{
		get => Direction == GraphDirection.Children;
		set { if( value ) Direction = GraphDirection.Children; }
	}

	public bool IsParentsDirection
	{
		get => Direction == GraphDirection.Parents;
		set { if( value ) Direction = GraphDirection.Parents; }
	}

	public bool IsBothDirection
	{
		get => Direction == GraphDirection.Both;
		set { if( value ) Direction = GraphDirection.Both; }
	}

	partial void OnDirectionChanged( GraphDirection value )
	{
		if( !IsDisposed && !_suppressDirectionReload )
			PendingLoad = LoadGraphAsync();
	}

	public bool IsBusy => IsEntriesLoading || IsGraphLoading;

	[ObservableProperty] public partial bool IsDetailsOpen { get; set; }
	[ObservableProperty] public partial string InfoName { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoType { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoSize { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoVersions { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoState { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoFolder { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoPathToolTip { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoModified { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoUses { get; set; } = string.Empty;
	[ObservableProperty] public partial string InfoUsedBy { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor( nameof( LocateDetailCommand ) )]
	public partial bool CanLocateDetail { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsNoPreviewVisible ) )]
	public partial ImageSource? PreviewImage { get; set; }

	[ObservableProperty] public partial string PreviewStatusText { get; set; } = "no preview";

	public bool IsNoPreviewVisible => PreviewImage is null;

	public string DetailsText => string.Join( " | ",
		PreviewStatusText, InfoName, InfoType, InfoSize, InfoVersions, InfoState, InfoFolder, InfoModified, InfoUses, InfoUsedBy );

	public void SetCurrentForm( GraphForm form ) => _currentForm = form;

	public void SetSelectedDetailTab( GraphDetailTab tab, bool reloadIfOpen = true )
	{
		_selectedDetailTab = tab;
		if( reloadIfOpen && _detailNode is not null )
			_ = LoadDetailTabAsync();
	}

	public void SetDirectionWithoutReload( GraphDirection direction )
	{
		_suppressDirectionReload = true;
		try { Direction = direction; }
		finally { _suppressDirectionReload = false; }
	}

	public void UpdateZoom( double zoom ) => ZoomText = $"{zoom * 100:0}%";

	public void SetRoots( IReadOnlyList<int> ids, bool debounce = false )
	{
		_roots = ids;
		PendingLoad = LoadGraphAsync( debounce );
	}

	public async Task<DirectoryNode?> LoadDirectoryTreeAsync()
	{
		if( IsDisposed )
			return null;
		(CancellationTokenSource opCts, CancellationTokenSource? linkedCts, CancellationToken token) = BeginOperation( ref _treeLoadCts );
		using CancellationTokenSource? linkedGuard = linkedCts;
		if( token.IsCancellationRequested )
			return null;

		IsTreeLoading = true;
		try
		{
			DirectoryNode? existing = AppComposition.FileManagerState.Tree;
			DirectoryNode? root = existing ?? await Task.Run( () => new DirectoryTreeService().LoadAsync( progress: null, token ), token );
			if( IsDisposed || token.IsCancellationRequested )
				return null;
			if( root is null )
			{
				StatusText = "The folder tree could not be read from the server.";
				return null;
			}
			AppComposition.FileManagerState.Tree ??= root;
			return root;
		}
		catch( OperationCanceledException )
		{
			return null;
		}
		catch( ObjectDisposedException )
		{
			return null;
		}
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"Loading folder tree failed: {ex.Message}", ex );
			if( !IsDisposed && ReferenceEquals( Volatile.Read( ref _treeLoadCts ), opCts ) )
				StatusText = $"The folder tree could not be read: {ex.Message}";
			return null;
		}
		finally
		{
			if( !IsDisposed && ReferenceEquals( Volatile.Read( ref _treeLoadCts ), opCts ) )
				IsTreeLoading = false;
		}
	}

	public async Task LoadEntriesAsync( int? directoryId, bool debounce = false )
	{
		if( IsDisposed )
			return;
		(CancellationTokenSource opCts, CancellationTokenSource? linkedCts, CancellationToken token) = BeginOperation( ref _entriesLoadCts );
		using CancellationTokenSource? linkedGuard = linkedCts;

		Entries.ClearIfNonEmpty();
		EntryCountText = string.Empty;
		if( directoryId is not int id || id == 0 )
		{
			IsEntriesLoading = false;
			EntryCountText = "This folder is not on the server.";
			return;
		}

		IsEntriesLoading = true;
		try
		{
			if( debounce )
				await Task.Delay( 75, token );
			if( IsDisposed || token.IsCancellationRequested )
				return;

			(bool ok, string? error, List<GraphEntryItem>? items) = await Task.Run( async () =>
			{
				Result<IReadOnlyList<RemoteEntryDto>> rows = await HpDirectory.GetEntriesAsync( id, token: token ).ConfigureAwait( false );
				token.ThrowIfCancellationRequested();
				if( !rows.Ok )
					return ( false, rows.Error, (List<GraphEntryItem>?)null );
				List<GraphEntryItem> list =
				[
					.. rows.Value!
						.Where( static r => !r.Deleted )
						.OrderBy( static r => r.Name, StringComparer.OrdinalIgnoreCase )
						.Select( static r => new GraphEntryItem( r.Id, r.Name ) )
				];
				return ( true, (string?)null, list );
			}, token );

			if( IsDisposed || token.IsCancellationRequested )
				return;
			if( !ok )
			{
				EntryCountText = $"The files could not be read: {error}";
				return;
			}
			Entries.ReplaceAll( items! );
			EntryCountText = Entries.Count == 1 ? "1 file" : $"{Entries.Count} files";
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"Loading folder entries failed: {ex.Message}", ex );
			if( !IsDisposed && ReferenceEquals( Volatile.Read( ref _entriesLoadCts ), opCts ) )
				EntryCountText = $"The files could not be read: {ex.Message}";
		}
		finally
		{
			if( !IsDisposed && ReferenceEquals( Volatile.Read( ref _entriesLoadCts ), opCts ) )
				IsEntriesLoading = false;
		}
	}

	public void FocusNode( GraphNode node )
	{
		for( int i = 0; i < Entries.Count; i++ )
		{
			if( Entries[ i ].Id == node.Id )
			{
				SelectEntryInListRequested?.Invoke( Entries[ i ] );
				return;
			}
		}
		ClearEntryListSelectionRequested?.Invoke();
		SetRoots( [ node.Id ] );
	}

	public async Task LoadGraphAsync( bool debounce = false )
	{
		if( IsDisposed )
			return;
		IReadOnlyList<int> roots = _roots;
		if( roots.Count == 0 )
		{
			CtsHelper.CancelAndDispose( ref _graphLoadCts );
			ClearGraphState();
			IsGraphLoading = false;
			IsGraphBusyOverlayVisible = false;
			ShowHint = true;
			CloseDetails();
			GraphArranged?.Invoke( GraphLayoutResult.Empty );
			UpdateStatus( null );
			return;
		}

		(CancellationTokenSource opCts, CancellationTokenSource? linkedCts, CancellationToken token) = BeginOperation( ref _graphLoadCts );
		using CancellationTokenSource? linkedGuard = linkedCts;
		if( token.IsCancellationRequested )
			return;

		GraphDirection direction = Direction;
		LayoutOptions formLayout = _currentForm.Layout;
		try
		{
			if( debounce )
				await Task.Delay( 150, token );
			if( IsDisposed || token.IsCancellationRequested )
				return;

			IsGraphLoading = true;
			IsGraphBusyOverlayVisible = true;

			Result<DependencyGraph> graphResult = await DependencyGraphLoader.LoadAsync( roots, direction, token );
			if( IsDisposed || token.IsCancellationRequested )
				return;
			if( !graphResult.Ok )
			{
				StatusText = $"The dependencies could not be read: {graphResult.Error}";
				return;
			}

			DependencyGraph loaded = graphResult.Value!;
			var prepared = await Task.Run( () =>
			{
				GraphLayoutResult arranged = GraphLayout.Arrange( loaded, direction, formLayout );
				var (nodesById, childrenByParent, parentsByChild) = IndexGraph( loaded );
				return (arranged, nodesById, childrenByParent, parentsByChild);
			}, token );

			if( IsDisposed || token.IsCancellationRequested )
				return;

			_graph = loaded;
			ReplaceIndices( prepared.nodesById, prepared.childrenByParent, prepared.parentsByChild );
			ShowHint = _graph.Nodes.Count == 0;
			GraphArranged?.Invoke( prepared.arranged );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"Loading dependency graph failed: {ex.Message}", ex );
			if( !IsDisposed && ReferenceEquals( Volatile.Read( ref _graphLoadCts ), opCts ) )
				StatusText = $"The dependencies could not be read: {ex.Message}";
		}
		finally
		{
			if( !IsDisposed && ReferenceEquals( Volatile.Read( ref _graphLoadCts ), opCts ) )
			{
				IsGraphBusyOverlayVisible = false;
				IsGraphLoading = false;
			}
		}
	}

	public async Task<GraphLayoutResult?> ArrangeForThemeAsync( GraphTheme prev, GraphTheme next, GraphLayoutResult currentLayout )
	{
		if( IsDisposed )
			return null;
		_currentForm = next.Form;
		if( _graph.Nodes.Count == 0 )
		{
			CtsHelper.CancelAndDispose( ref _themeLayoutCts );
			return null;
		}
		if( next.Form.Layout == prev.Form.Layout && currentLayout.Nodes.Count > 0 )
		{
			CtsHelper.CancelAndDispose( ref _themeLayoutCts );
			return currentLayout;
		}

		(_, CancellationTokenSource? linkedCts, CancellationToken token) = BeginOperation( ref _themeLayoutCts );
		using CancellationTokenSource? linkedGuard = linkedCts;
		if( token.IsCancellationRequested )
			return null;

		DependencyGraph snapshot = _graph;
		GraphDirection dir = Direction;
		try
		{
			GraphLayoutResult arranged = await Task.Run( () => GraphLayout.Arrange( snapshot, dir, next.Form.Layout ), token );
			if( IsDisposed || token.IsCancellationRequested || !ReferenceEquals( _graph, snapshot ) )
				return null;
			return arranged;
		}
		catch( OperationCanceledException )
		{
			return null;
		}
		catch( ObjectDisposedException )
		{
			return null;
		}
	}

	public void UpdateStatus( GraphNode? selectedNode )
	{
		if( _graph.Nodes.Count == 0 )
		{
			StatusText = string.Empty;
			return;
		}
		int others = _graph.Nodes.Count - _graph.Roots.Count;
		string what = Direction switch
		{
			GraphDirection.Children => others == 1 ? "1 file used" : $"{others} files used",
			GraphDirection.Parents => others == 1 ? "used by 1 file" : $"used by {others} files",
			_ => others == 1 ? "1 related file" : $"{others} related files",
		};
		string text = $"{_graph.Roots.Count} selected · {what} · {_graph.Edges.Count} links";
		if( _graph.Truncated )
			text += " · stopped early (too many files), not everything is shown";
		if( selectedNode is { } node )
			text += $"   |   {node.Path}  ({ByteSize.Format( node.Size, " " )})";
		StatusText = text;
	}

	public Task ShowDetailsAsync( GraphNode? node, Color headerColor )
	{
		if( IsDisposed )
			return Task.CompletedTask;
		if( node is null )
		{
			CloseDetails();
			return Task.CompletedTask;
		}

		bool same = _detailNode?.Id == node.Id && IsDetailsOpen;
		_detailNode = node;
		FillInfoCard( node, headerColor, keepPreview: same );
		IsDetailsOpen = true;
		if( same )
			return DetailsLoad ?? Task.CompletedTask;
		return DetailsLoad = LoadDetailsAsync( node );
	}

	[RelayCommand]
	public void CloseDetails()
	{
		CtsHelper.CancelAndDispose( ref _detailLoadCts );
		CtsHelper.CancelAndDispose( ref _previewLoadCts );
		Interlocked.Increment( ref _previewGeneration );
		_detailNode = null;
		IsDetailsOpen = false;
	}

	[RelayCommand]
	private void FocusDetailNode()
	{
		if( _detailNode is { } node )
			FocusNode( node );
	}

	[RelayCommand( CanExecute = nameof( CanLocateDetail ) )]
	private async Task LocateDetailAsync()
	{
		if( IsDisposed || _detailNode is not { } node )
			return;
		try
		{
			string[] parts = node.Path.Split( PathSeparators, StringSplitOptions.RemoveEmptyEntries );
			if( parts.Length >= 2 )
				await AppComposition.LocateInFileManagerAsync( parts[ 1..^1 ], node.Name );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"Locating '{node.Name}' in file manager failed: {ex.Message}", ex );
		}
	}

	[RelayCommand] private void ZoomIn() => ZoomRequested?.Invoke( 1.25 );
	[RelayCommand] private void ZoomOut() => ZoomRequested?.Invoke( 0.8 );
	[RelayCommand] private void Fit() => FitRequested?.Invoke();

	public Task LoadDetailTabAsync()
	{
		if( IsDisposed )
			return Task.CompletedTask;
		(CancellationTokenSource opCts, CancellationTokenSource? linkedCts, CancellationToken token) = BeginOperation( ref _detailLoadCts );
		ClearDetailTables();
		if( _detailNode is not { } node || token.IsCancellationRequested )
		{
			linkedCts?.Dispose();
			return Task.CompletedTask;
		}
		return DetailsLoad = FillDetailTabCoreAsync( node, _selectedDetailTab, opCts, linkedCts, token );
	}

	private async Task LoadDetailsAsync( GraphNode node )
	{
		// Replace both CTSs upfront before any await so rapid node switches cannot cancel a newer preview.
		(CancellationTokenSource detailOp, CancellationTokenSource? detailLinked, CancellationToken detailToken) = BeginOperation( ref _detailLoadCts );
		(CancellationTokenSource previewOp, CancellationTokenSource? previewLinked, CancellationToken previewToken) = BeginOperation( ref _previewLoadCts );
		long previewGen = Interlocked.Increment( ref _previewGeneration );

		ClearDetailTables();
		if( detailToken.IsCancellationRequested || previewToken.IsCancellationRequested )
		{
			detailLinked?.Dispose();
			previewLinked?.Dispose();
			return;
		}

		await Task.WhenAll(
			FillDetailTabCoreAsync( node, _selectedDetailTab, detailOp, detailLinked, detailToken ),
			LoadPreviewAsync( node, previewGen, previewOp, previewLinked, previewToken ) );
	}

	private async Task FillDetailTabCoreAsync(
		GraphNode node,
		GraphDetailTab tab,
		CancellationTokenSource opCts,
		CancellationTokenSource? linkedCts,
		CancellationToken token )
	{
		using CancellationTokenSource? linkedGuard = linkedCts;
		DetailRow row = new() { Id = node.Id, LatestId = node.VersionId > 0 ? node.VersionId : null };
		try
		{
			switch( tab )
			{
				case GraphDetailTab.History:
					Apply( HistoryRows, await _details.GetHistoryAsync( row, token ) );
					break;
				case GraphDetailTab.WhereUsed:
					Apply( ParentRows, await _details.GetParentsAsync( row, token ) );
					break;
				case GraphDetailTab.Dependents:
					Apply( ChildRows, await _details.GetChildrenAsync( row, token ) );
					break;
				case GraphDetailTab.Properties:
					Apply( PropertyRows, await _details.GetPropertiesAsync( row, token ) );
					break;
				case GraphDetailTab.Info:
					Apply( VersionRows, await _details.GetVersionInfoAsync( row, token ) );
					break;
			}
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"{tab} of '{node.Name}' failed: {ex.Message}", ex );
		}

		void Apply<T>( RangeObservableCollection<T> target, List<T> rows )
		{
			if( !IsDisposed && !token.IsCancellationRequested && ReferenceEquals( Volatile.Read( ref _detailLoadCts ), opCts ) && _detailNode?.Id == node.Id )
				target.ReplaceAll( rows );
		}
	}

	private async Task LoadPreviewAsync(
		GraphNode node,
		long previewGen,
		CancellationTokenSource opCts,
		CancellationTokenSource? linkedCts,
		CancellationToken token )
	{
		using CancellationTokenSource? linkedGuard = linkedCts;
		try
		{
			byte[]? bytes = node.VersionId > 0
				? await _details.GetPreviewImageAsync( node.VersionId, token )
				: null;
			if( IsDisposed || token.IsCancellationRequested || previewGen != Volatile.Read( ref _previewGeneration ) || _detailNode?.Id != node.Id )
				return;

			ImageSource? image = await _previews.FromBytesAsync( bytes );
			if( IsDisposed || token.IsCancellationRequested || previewGen != Volatile.Read( ref _previewGeneration ) || _detailNode?.Id != node.Id )
				return;

			PreviewImage = image;
			PreviewStatusText = "no preview";
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( NavigatePageMenu.DependencyGraph ), $"Preview of '{node.Name}' failed: {ex.Message}", ex );
			if( !IsDisposed && !token.IsCancellationRequested && ReferenceEquals( Volatile.Read( ref _previewLoadCts ), opCts ) && _detailNode?.Id == node.Id )
				PreviewStatusText = "no preview";
		}
	}

	private void FillInfoCard( GraphNode node, Color headerColor, bool keepPreview )
	{
		InfoHeaderBrush.Color = headerColor;
		InfoName = node.Name;
		string type = node.TypeName.Length > 0 ? node.TypeName : node.Ext.ToUpperInvariant() + " file";
		InfoType = node.Category.Length > 0 ? $"{type} · {node.Category}" : type;
		InfoSize = ByteSize.Format( node.Size, " " );
		InfoVersions = node.Versions switch
		{
			0 => "no versions",
			1 => $"1 version (id {node.VersionId})",
			_ => $"{node.Versions} versions, latest id {node.VersionId}",
		};
		InfoState = node.Deleted ? "Deleted"
			: node.CheckedOut ? $"Checked out by {( node.CheckoutUser.Length > 0 ? node.CheckoutUser : "someone" )}"
			: "Not checked out";

		int cut = node.Path.LastIndexOfAny( PathSeparators );
		InfoFolder = cut > 0 ? node.Path[ ..cut ] : node.Path;
		InfoPathToolTip = node.Path;
		InfoModified = DateTime.TryParse( node.Modified, CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal, out DateTime changed ) ? changed.ToLocalTime().ToString( "g" ) : "unknown";

		InfoUses = Related( _childrenByParent.GetValueOrDefault( node.Id ), GraphDirection.Parents );
		InfoUsedBy = Related( _parentsByChild.GetValueOrDefault( node.Id ), GraphDirection.Children );
		string[] pathParts = node.Path.Split( PathSeparators, StringSplitOptions.RemoveEmptyEntries );
		CanLocateDetail = !node.Deleted && pathParts.Length >= 2;

		if( keepPreview )
			return;
		PreviewImage = null;
		PreviewStatusText = "loading…";
	}

	private string Related( IReadOnlyList<int>? ids, GraphDirection hiddenIn )
	{
		if( ids is null || ids.Count == 0 )
			return Direction == hiddenIn ? "not shown in this view" : "nothing";

		HashSet<int> seen = [];
		List<string> names = new( ids.Count );
		for( int i = 0; i < ids.Count; i++ )
		{
			int id = ids[ i ];
			if( seen.Add( id ) && _nodesById.TryGetValue( id, out GraphNode? found ) )
				names.Add( found.Name );
		}
		if( names.Count == 0 )
			return Direction == hiddenIn ? "not shown in this view" : "nothing";
		names.Sort( StringComparer.OrdinalIgnoreCase );
		string list = string.Join( ", ", names.Take( 4 ) ) + ( names.Count > 4 ? $", +{names.Count - 4} more" : "" );
		return names.Count == 1 ? list : $"{names.Count} files: {list}";
	}

	private void ClearDetailTables()
	{
		HistoryRows.ClearIfNonEmpty();
		ParentRows.ClearIfNonEmpty();
		ChildRows.ClearIfNonEmpty();
		PropertyRows.ClearIfNonEmpty();
		VersionRows.ClearIfNonEmpty();
	}

	private void ClearGraphState()
	{
		_graph = DependencyGraph.Empty;
		_nodesById.Clear();
		_childrenByParent.Clear();
		_parentsByChild.Clear();
	}

	private void ReplaceIndices(
		Dictionary<int, GraphNode> nodesById,
		Dictionary<int, List<int>> childrenByParent,
		Dictionary<int, List<int>> parentsByChild )
	{
		_nodesById.Clear();
		foreach( var kv in nodesById )
			_nodesById[ kv.Key ] = kv.Value;

		_childrenByParent.Clear();
		foreach( var kv in childrenByParent )
			_childrenByParent[ kv.Key ] = kv.Value;

		_parentsByChild.Clear();
		foreach( var kv in parentsByChild )
			_parentsByChild[ kv.Key ] = kv.Value;
	}

	private static (Dictionary<int, GraphNode> NodesById, Dictionary<int, List<int>> ChildrenByParent, Dictionary<int, List<int>> ParentsByChild) IndexGraph( DependencyGraph graph )
	{
		Dictionary<int, GraphNode> nodesById = new( graph.Nodes.Count );
		for( int i = 0; i < graph.Nodes.Count; i++ )
			nodesById[ graph.Nodes[ i ].Id ] = graph.Nodes[ i ];

		Dictionary<int, List<int>> childrenByParent = [];
		Dictionary<int, List<int>> parentsByChild = [];
		for( int i = 0; i < graph.Edges.Count; i++ )
		{
			GraphEdge edge = graph.Edges[ i ];
			if( !childrenByParent.TryGetValue( edge.Parent, out List<int>? children ) )
				childrenByParent[ edge.Parent ] = children = [];
			children.Add( edge.Child );

			if( !parentsByChild.TryGetValue( edge.Child, out List<int>? parents ) )
				parentsByChild[ edge.Child ] = parents = [];
			parents.Add( edge.Parent );
		}
		return (nodesById, childrenByParent, parentsByChild);
	}

	private (CancellationTokenSource OpCts, CancellationTokenSource? LinkedCts, CancellationToken Token) BeginOperation( ref CancellationTokenSource? slot )
	{
		CancellationTokenSource opCts = CtsHelper.Replace( ref slot );
		try
		{
			CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource( opCts.Token, _lifetimeCts.Token );
			return (opCts, linked, linked.Token);
		}
		catch( ObjectDisposedException )
		{
			return (opCts, null, new CancellationToken( canceled: true ));
		}
	}

	public void Dispose()
	{
		if( IsDisposed )
			return;
		IsDisposed = true;
		try { _lifetimeCts.Cancel(); }
		finally { _lifetimeCts.Dispose(); }
		CtsHelper.CancelAndDispose( ref _treeLoadCts );
		CtsHelper.CancelAndDispose( ref _entriesLoadCts );
		CtsHelper.CancelAndDispose( ref _graphLoadCts );
		CtsHelper.CancelAndDispose( ref _detailLoadCts );
		CtsHelper.CancelAndDispose( ref _previewLoadCts );
		CtsHelper.CancelAndDispose( ref _themeLayoutCts );
		GC.SuppressFinalize( this );
	}
}
