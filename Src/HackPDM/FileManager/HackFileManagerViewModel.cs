using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo.Models;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml.Data;

namespace HackPDM.FileManager;

/// <summary>View model for the Hack File Manager page: tree, entries, grouping, sorting, details, preview, and PDM commands.</summary>
[Bindable]
public sealed partial class HackFileManagerViewModel : OperationViewModel, IDisposable
{
	private const int MaxSingleRowUpdates = 25;
	private const int BulkReloadChangeThreshold = 200;
	private const int TreeUpdateCooldownMs = 50;
	private const int OverflowRefreshCooldownMs = 500;
	private static readonly Func<EntryRow, IComparable?> GroupByStatus = static row => row.Status;

	private readonly FileManagerState _state;
	private readonly IPdmWorkflowService _workflow;
	private readonly IEntryFileService _files;
	private readonly IEntryDetailsService _details;
	private readonly IDirectoryTreeService _directoryTree;
	private readonly IEntryListService _entryList;
	private readonly ILocalFileWatcher _localFiles;
	private readonly IFilePickerService? _pickers;
	private readonly Dictionary<string, Func<EntryRow, CancellationToken, Task>> _detailTabs;
	private readonly SemaphoreSlim _localChangesGate = new( 1, 1 );
	private readonly Lock _watcherQueueGate = new();
	private readonly Lock _treeLock = new();
	private readonly Dictionary<string, LocalFileChange> _queuedWatcherChanges = new( PwaPaths.PathComparer );
	private readonly Timer _watcherCooldownTimer;
	private readonly List<LocalFileChange> _pendingChangesDuringLoad = [];
	private readonly CancellationTokenSource _pageCancellation = new();
	private readonly bool _restoring;
	private readonly string? _selectionFolder;

	private CancellationTokenSource? _treeLoadCancellation;
	private CancellationTokenSource? _detailsCancellation;
	private CancellationTokenSource? _previewCancellation;
	private CancellationTokenSource? _sortCancellation;
	private TaskCompletionSource _treeLoadedTcs = new( TaskCreationOptions.RunContinuationsAsynchronously );
	private TaskCompletionSource _listLoadedTcs = new( TaskCreationOptions.RunContinuationsAsynchronously );
	private IReadOnlyList<string> _selectionToRestore;
	private Func<EntryRow, IComparable?> _groupKey = GroupByStatus;
	private int _entriesLoadGeneration;
	private int _previewVersionGeneration;
	private bool _active;
	private bool _queuedWatcherOverflow;
	private bool _watcherDrainScheduled;
	private bool _watcherScheduledForOverflow;
	private bool _watcherDrainRunning;
	private long _lastTreePushTick = long.MinValue / 2;
	private bool _isTreeLoadRunning;
	private bool _pendingTreeReload;
	private bool _pendingTreeReloadFromServer;

	public HackFileManagerViewModel(
		FileManagerState state,
		IPdmWorkflowService workflow,
		IEntryFileService files,
		IEntryDetailsService details,
		IDialogService dialogs,
		IOperationReporter reporter,
		IDirectoryTreeService directoryTree,
		IEntryListService entryList,
		ILocalFileWatcher localFiles,
		IFilePickerService? pickers = null ) : base( dialogs, reporter )
	{
		_state = state;
		_workflow = workflow;
		_files = files;
		_details = details;
		_directoryTree = directoryTree;
		_entryList = entryList;
		_localFiles = localFiles;
		_pickers = pickers;
		_detailTabs = new()
		{
			[ PdmConstants.HistoryTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetHistoryAsync( entry, ct );
				ct.ThrowIfCancellationRequested();
				OHistories.ReplaceAll( rows );
			},
			[ PdmConstants.ParentTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetParentsAsync( entry, ct );
				ct.ThrowIfCancellationRequested();
				OParents.ReplaceAll( rows );
			},
			[ PdmConstants.ChildTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetChildrenAsync( entry, ct );
				ct.ThrowIfCancellationRequested();
				OChildren.ReplaceAll( rows );
			},
			[ PdmConstants.PropertiesTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetPropertiesAsync( entry, ct );
				ct.ThrowIfCancellationRequested();
				OProperties.ReplaceAll( rows );
			},
			[ PdmConstants.InfoTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetVersionInfoAsync( entry, ct );
				ct.ThrowIfCancellationRequested();
				OVersions.ReplaceAll( rows );
			},
		};

		_restoring = true;
		if( state.GroupField is null )
		{
			state.IsGroupingEnabled = UIStorage.ColumnsInfo.GroupingEnabled;
			string? configuredGroup = UIStorage.ColumnsInfo.GroupBy?.FieldName;
			state.GroupField = !string.IsNullOrWhiteSpace( configuredGroup ) && EntryRowKeys.TryGetGroupKey( configuredGroup, out _ )
				? configuredGroup
				: nameof( EntryRow.Status );
		}
		ShowInactive = state.ShowInactive;
		IsGroupingEnabled = state.IsGroupingEnabled;
		GroupField = state.GroupField;
		if( EntryRowKeys.TryGetGroupKey( state.GroupField, out var groupKey ) )
			_groupKey = groupKey;
		_selectionToRestore = state.SelectedEntryNames;
		_selectionFolder = state.SelectedPath;
		_restoring = false;
		_watcherCooldownTimer = new Timer( _ => UiDispatcher.Post( () => _ = DrainWatcherQueueAsync() ), null, Timeout.Infinite, Timeout.Infinite );
	}

	public event Action<FileManagerRefresh>? RefreshRequested;
	public event Action? GroupingChanged;
	public event Func<DirectoryNode, Task>? TreeLoaded;
	public event Func<IReadOnlyList<string>, string?, Task>? LocateRequested;
	public event Action<string, LocalChangeKind>? FolderChanged;
	public event Action<bool>? LiveBatchApplying;
	public event Action<IReadOnlyDictionary<EntryRow, EntryRow?>>? RowsReplaced;

	public bool IsDisposed { get; private set; }
	public bool HasKeptTree => _state.Tree is not null && ( _state.Stale & FileManagerRefresh.Tree ) == 0;
	public bool IsEntriesStale => ( _state.Stale & FileManagerRefresh.Entries ) != 0;

	[ObservableProperty] public partial bool IsTreeLoaded { get; set; }
	partial void OnIsTreeLoadedChanged( bool value )
	{
		if( value )
			_treeLoadedTcs.TrySetResult();
		else if( _treeLoadedTcs.Task.IsCompleted )
			_treeLoadedTcs = new( TaskCreationOptions.RunContinuationsAsynchronously );
	}

	[ObservableProperty] public partial bool IsListLoaded { get; set; }
	partial void OnIsListLoadedChanged( bool value )
	{
		if( value )
			_listLoadedTcs.TrySetResult();
		else if( _listLoadedTcs.Task.IsCompleted )
			_listLoadedTcs = new( TaskCreationOptions.RunContinuationsAsynchronously );
	}

	[ObservableProperty] public partial bool IsTreeLoading { get; set; }
	[ObservableProperty] public partial string TreeLoadingStatus { get; set; } = "Syncing server & local folders…";

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( ShowEmptyState ) )]
	public partial bool IsEntriesLoading { get; set; }

	[ObservableProperty] public partial string EntriesLoadingStatus { get; set; } = "Loading folder contents…";
	[ObservableProperty] public partial bool IsEntriesProgressIndeterminate { get; set; } = true;
	[ObservableProperty] public partial double EntriesLoadingPercent { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( ShowEmptyState ) )]
	public partial bool IsEmptyDirectory { get; set; }

	public bool ShowEmptyState => !IsEntriesLoading && IsEmptyDirectory;
	[ObservableProperty] public partial bool IsDetailsLoading { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsPreviewVisible ) )]
	[NotifyPropertyChangedFor( nameof( IsNoPreviewVisible ) )]
	[NotifyPropertyChangedFor( nameof( IsPreviewBusy ) )]
	[NotifyPropertyChangedFor( nameof( PreviewStatusText ) )]
	public partial bool IsPreviewLoading { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsPreviewVisible ) )]
	[NotifyPropertyChangedFor( nameof( IsNoPreviewVisible ) )]
	[NotifyPropertyChangedFor( nameof( IsPreviewBusy ) )]
	[NotifyPropertyChangedFor( nameof( PreviewStatusText ) )]
	public partial bool IsPreviewDecoding { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsPreviewVisible ) )]
	[NotifyPropertyChangedFor( nameof( IsNoPreviewVisible ) )]
	public partial EntryPreview? Preview { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsPreviewVisible ) )]
	[NotifyPropertyChangedFor( nameof( IsNoPreviewVisible ) )]
	public partial bool HasPreviewImage { get; set; }

	partial void OnPreviewChanged( EntryPreview? value )
	{
		if( value is null )
		{
			HasPreviewImage = false;
			IsPreviewDecoding = false;
		}
	}

	public bool IsPreviewVisible => !IsPreviewLoading && !IsPreviewDecoding && Preview is not null && HasPreviewImage;
	public bool IsNoPreviewVisible => !IsPreviewVisible;
	public bool IsPreviewBusy => IsPreviewLoading || IsPreviewDecoding;
	public string PreviewStatusText => IsPreviewBusy ? "Loading preview…" : "No preview";

	[ObservableProperty] public partial string EntriesLabel { get; set; } = "Entries: 0";
	[ObservableProperty] public partial string EntriesLabelLocal { get; set; } = "Local Only: 0";
	[ObservableProperty] public partial string EntriesLabelRemote { get; set; } = "Remote Only: 0";
	[ObservableProperty] public partial string EntriesLabelModified { get; set; } = "Modified / Checked Out: 0";
	[ObservableProperty] public partial string SelectionLabel { get; set; } = string.Empty;
	[ObservableProperty] public partial string ChangesTabHeader { get; set; } = "Changes";
	[ObservableProperty] public partial string LocalChangesHeader { get; set; } = "Local Changes (0)";
	[ObservableProperty] public partial string RemoteChangesHeader { get; set; } = "Server Updates (0)";

	[ObservableProperty] public partial bool ShowInactive { get; set; }
	partial void OnShowInactiveChanged( bool value )
	{
		if( _restoring )
			return;
		_state.ShowInactive = value;
		RefreshRequested?.Invoke( FileManagerRefresh.Entries );
	}

	[ObservableProperty] public partial bool IsGroupingEnabled { get; set; }
	partial void OnIsGroupingEnabledChanged( bool value )
	{
		if( _restoring )
			return;
		_state.IsGroupingEnabled = value;
		GroupingChanged?.Invoke();
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( ActiveGroupFieldName ) )]
	[NotifyPropertyChangedFor( nameof( GroupedByLabelText ) )]
	public partial string? GroupField { get; set; }

	partial void OnGroupFieldChanged( string? value )
	{
		if( !_restoring )
			_state.GroupField = value;
	}

	public RangeObservableCollection<EntryRow> Entries { get; } = [];
	public RangeObservableCollection<EntryRow> LocalChanges { get; } = [];
	public RangeObservableCollection<EntryRow> RemoteChanges { get; } = [];
	public RangeObservableCollection<HistoryRow> OHistories { get; } = [];
	public RangeObservableCollection<ParentRow> OParents { get; } = [];
	public RangeObservableCollection<ChildrenRow> OChildren { get; } = [];
	public RangeObservableCollection<PropertiesRow> OProperties { get; } = [];
	public RangeObservableCollection<VersionRow> OVersions { get; } = [];

	public Func<EntryRow, IComparable?>? ActiveGroupKey => IsGroupingEnabled ? _groupKey : null;
	public string ActiveGroupFieldName => GroupField ?? nameof( EntryRow.Status );
	public string GroupedByLabelText => $"Grouped by {ActiveGroupFieldName}";
	public IReadOnlySet<string> ExpandedPaths => _state.ExpandedPaths;
	public (string Field, bool Descending)? EntrySort => _state.Sort;
	public HistoryRow? SelectedHistory { get; set; }

	public DirectoryRef? SelectedDirectory
	{
		get => _state.SelectedDirectory;
		set
		{
			_state.SelectedDirectory = value;
			LastSelectedNodePath = value?.TreePath;
		}
	}

	public string? LastSelectedNodePath
	{
		get => _state.SelectedPath;
		set => _state.SelectedPath = value;
	}

	public string? SelectedDetailTab
	{
		get => _state.SelectedDetailTab;
		set => _state.SelectedDetailTab = value;
	}

	[ObservableProperty]
	public partial IReadOnlyList<EntryRow> SelectedEntries { get; set; } = [];

	partial void OnSelectedEntriesChanged( IReadOnlyList<EntryRow> value )
	{
		if( IsListLoaded && !IsDisposed )
		{
			List<string> names = new( value.Count );
			for( int i = 0; i < value.Count; i++ )
			{
				if( value[ i ].Name is { } name )
					names.Add( name );
			}
			_state.SelectedEntryNames = names;
		}
		UpdateSelectionLabel( value );
	}

	private string SelectedLocalDirectory => _files.ResolveLocalDirectory( LastSelectedNodePath );
	private static int[] RemoteIds( IEnumerable<EntryRow> rows ) => RemoteIds( rows.Select( r => r.Id ) );

	public Task WaitForTreeLoadedAsync( CancellationToken token = default )
		=> IsTreeLoaded ? Task.CompletedTask : _treeLoadedTcs.Task.WaitAsync( token );

	public Task WaitForListLoadedAsync( CancellationToken token = default )
		=> IsListLoaded ? Task.CompletedTask : _listLoadedTcs.Task.WaitAsync( token );

	internal Task<int> CountTreeAsync( DirectoryNode root, CancellationToken token = default )
		=> Task.Run( () =>
		{
			lock( _treeLock )
				return root.CountRecursive();
		}, token );

	internal Task<(DirectoryNode? Filtered, int Count)> FilterTreeAsync( DirectoryNode root, string query, CancellationToken token = default )
		=> Task.Run( () =>
		{
			token.ThrowIfCancellationRequested();
			lock( _treeLock )
			{
				DirectoryNode? filtered = string.IsNullOrEmpty( query ) ? root : root.Filter( query );
				int count = filtered?.CountRecursive() ?? 0;
				return (filtered, count);
			}
		}, token );

	public (IReadOnlyList<string> Segments, string? FileName)? Activate()
	{
		if( !_active && !IsDisposed )
		{
			_active = true;
			_state.LivePages++;
			_state.LocateRequested += ForwardLocateAsync;
			_localFiles.Changed += OnLocalFilesChanged;
			_localFiles.Overflowed += OnLocalChangesLost;
			_localFiles.Watch( PwaPaths.Vault );
		}
		return _state.TakePendingLocate();
	}

	private Task ForwardLocateAsync( IReadOnlyList<string> segments, string? fileName )
		=> LocateRequested?.Invoke( segments, fileName ) ?? Task.CompletedTask;

	public void Dispose()
	{
		if( IsDisposed )
			return;
		IsDisposed = true;
		if( _active )
		{
			_active = false;
			_state.LivePages--;
			_state.LocateRequested -= ForwardLocateAsync;
			_localFiles.Changed -= OnLocalFilesChanged;
			_localFiles.Overflowed -= OnLocalChangesLost;
		}
		try { _pageCancellation.Cancel(); }
		finally { _pageCancellation.Dispose(); }
		_treeLoadedTcs.TrySetCanceled();
		_listLoadedTcs.TrySetCanceled();
		lock( _watcherQueueGate )
		{
			_queuedWatcherChanges.Clear();
			_queuedWatcherOverflow = false;
			_watcherDrainScheduled = false;
			_watcherScheduledForOverflow = false;
			_watcherCooldownTimer.Dispose();
		}
		CtsHelper.CancelAndDispose( ref _treeLoadCancellation );
		CtsHelper.CancelAndDispose( ref _detailsCancellation );
		CtsHelper.CancelAndDispose( ref _previewCancellation );
		CtsHelper.CancelAndDispose( ref _sortCancellation );
		_localChangesGate.Dispose();
		GC.SuppressFinalize( this );
	}

	public void RefreshFromServer()
	{
		_state.MarkStale( FileManagerRefresh.All );
		RefreshRequested?.Invoke( FileManagerRefresh.All );
	}

	private void Refresh( FileManagerRefresh what )
	{
		_state.MarkStale( what );
		RefreshRequested?.Invoke( what );
	}

	public void SetExpanded( string? treePath, bool expanded )
	{
		if( string.IsNullOrEmpty( treePath ) || IsDisposed )
			return;
		if( expanded )
			_state.ExpandedPaths.Add( treePath );
		else
			_state.ExpandedPaths.Remove( treePath );
	}

	public IReadOnlyList<EntryRow> TakeSelectionToRestore()
	{
		IReadOnlyList<string> names = _selectionToRestore;
		_selectionToRestore = [];
		if( names.Count == 0 || !string.Equals( LastSelectedNodePath, _selectionFolder, StringComparison.OrdinalIgnoreCase ) )
			return [];
		HashSet<string> nameSet = new( names, StringComparer.Ordinal );
		List<EntryRow> matched = new( Math.Min( names.Count, Entries.Count ) );
		for( int i = 0; i < Entries.Count; i++ )
		{
			if( Entries[ i ].Name is { } name && nameSet.Contains( name ) )
				matched.Add( Entries[ i ] );
		}
		return matched;
	}

	private void UpdateSelectionLabel( IReadOnlyList<EntryRow> selected )
	{
		if( selected.Count == 0 )
		{
			SelectionLabel = string.Empty;
			return;
		}
		long totalBytes = 0;
		for( int i = 0; i < selected.Count; i++ )
			totalBytes += selected[ i ].Size ?? 0L;
		string sizeText = totalBytes > 0 ? $" ({ByteSize.Format( totalBytes, " " )})" : string.Empty;
		SelectionLabel = selected.Count == 1
			? $"1 selected · {selected[ 0 ].Name}{sizeText}"
			: $"{selected.Count} selected{sizeText}";
	}

	public async Task LoadDirectoryTreeAsync( bool fromServer = false )
	{
		if( IsDisposed )
			return;
		if( _isTreeLoadRunning )
		{
			_pendingTreeReload = true;
			_pendingTreeReloadFromServer |= fromServer;
			CtsHelper.CancelAndDispose( ref _treeLoadCancellation );
			return;
		}
		_isTreeLoadRunning = true;
		try
		{
			bool nextFromServer = fromServer;
			while( !IsDisposed )
			{
				_pendingTreeReload = false;
				_pendingTreeReloadFromServer = false;
				IsTreeLoaded = false;
				IsTreeLoading = true;
				TreeLoadingStatus = !nextFromServer && HasKeptTree ? "Restoring folder tree…" : "Fetching folder structure from Odoo…";
				CancellationTokenSource iterCts = CtsHelper.Replace( ref _treeLoadCancellation );
				CancellationTokenSource? linked = null;
				try
				{
					linked = CancellationTokenSource.CreateLinkedTokenSource( iterCts.Token, _pageCancellation.Token );
					CancellationToken token = linked.Token;
					IProgress<string> progress = new UiThreadProgress<string>( status =>
					{
						if( !IsDisposed && IsTreeLoading )
							TreeLoadingStatus = status;
					} );
					DirectoryNode? root = !nextFromServer && HasKeptTree
						? _state.Tree
						: await Task.Run( () => _directoryTree.LoadAsync( progress, token ), token );
					token.ThrowIfCancellationRequested();
					if( root is not null && !ReferenceEquals( root, _state.Tree ) )
						_state.TreeRefreshed( root );
					if( root is not null && !IsDisposed && TreeLoaded is { } handler )
						await handler( root );
				}
				catch( OperationCanceledException ) when( _pendingTreeReload && !IsDisposed && !_pageCancellation.IsCancellationRequested )
				{
					nextFromServer = _pendingTreeReloadFromServer;
					continue;
				}
				catch( OperationCanceledException ) { break; }
				catch( ObjectDisposedException ) { break; }
				catch( Exception ex )
				{
					HackLogger.Error( "FileManager", $"Loading directory tree failed: {ex.Message}", ex );
					break;
				}
				finally
				{
					linked?.Dispose();
				}

				if( !_pendingTreeReload )
					break;
				nextFromServer = _pendingTreeReloadFromServer;
			}
		}
		finally
		{
			CtsHelper.CancelAndDispose( ref _treeLoadCancellation );
			if( !IsDisposed )
			{
				IsTreeLoading = false;
				IsTreeLoaded = true;
			}
			_isTreeLoadRunning = false;
		}
	}

	public async Task LoadEntriesAsync( CancellationToken token = default )
	{
		if( IsDisposed )
			return;
		IsListLoaded = false;
		CtsHelper.CancelAndDispose( ref _sortCancellation );
		int generation = Interlocked.Increment( ref _entriesLoadGeneration );
		_pendingChangesDuringLoad.Clear();
		CancellationTokenSource linked;
		try
		{
			linked = CancellationTokenSource.CreateLinkedTokenSource( token, _pageCancellation.Token );
		}
		catch( ObjectDisposedException )
		{
			return;
		}
		using CancellationTokenSource linkedGuard = linked;
		token = linked.Token;

		if( SelectedDirectory is { } target && !_state.TryGetEntries( target.TreePath, ShowInactive, out _ ) )
		{
			EntriesLoadingStatus = $"Opening {target.Name}…";
			IsEntriesProgressIndeterminate = true;
			EntriesLoadingPercent = 0;
			IsEmptyDirectory = false;
			IsEntriesLoading = true;
		}

		try
		{
			await WaitForTreeLoadedAsync( token );
			if( SelectedDirectory is not { } directory )
			{
				Entries.ClearIfNonEmpty();
				SelectedEntries = [];
				ClearDetails();
				Preview = null;
				IsDetailsLoading = false;
				IsPreviewLoading = false;
				IsEmptyDirectory = false;
				return;
			}

			bool includeDeleted = ShowInactive;
			if( !_state.TryGetEntries( directory.TreePath, includeDeleted, out EntryListResult? result ) )
			{
				Entries.ClearIfNonEmpty();
				EntriesLoadingStatus = $"Fetching {directory.Name}…";
				IsEntriesProgressIndeterminate = true;
				IsEmptyDirectory = false;
				IsEntriesLoading = true;

				IProgress<EntryLoadProgress> progress = new UiThreadProgress<EntryLoadProgress>( p =>
				{
					if( IsDisposed || generation != Volatile.Read( ref _entriesLoadGeneration ) )
						return;
					EntriesLoadingStatus = p.Phase;
					IsEntriesProgressIndeterminate = p.Total <= 0;
					if( p.Total > 0 )
						EntriesLoadingPercent = Math.Clamp( p.Completed * 100.0 / p.Total, 0, 100 );
				} );

				result = await Task.Run(
					() => _entryList.LoadAsync( directory.DirectoryId, directory.TreePath, includeDeleted, filterUnknownTypes: true, progress, token ),
					token );
				token.ThrowIfCancellationRequested();
				_state.RememberEntries( directory.TreePath, includeDeleted, result );
			}

			var sort = _state.Sort;
			var prepared = await Task.Run( () => PrepareEntriesPresentation( result, sort ), token );
			token.ThrowIfCancellationRequested();
			Entries.ReplaceAll( prepared.Rows );
			ApplyEntryLabels( result, prepared.LocalPending, prepared.RemotePending, prepared.ModifiedCount );
			IsEmptyDirectory = Entries.Count == 0;
			GroupingChanged?.Invoke();
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception e )
		{
			HackLogger.Error( "FileManager", $"Loading entries failed: {e.Message}", e );
		}
		finally
		{
			if( !IsDisposed )
			{
				bool currentGeneration = generation == Volatile.Read( ref _entriesLoadGeneration );
				if( currentGeneration )
					IsEntriesLoading = false;
				if( !linked.IsCancellationRequested )
				{
					IsListLoaded = true;
					if( _pendingChangesDuringLoad.Count > 0 )
					{
						List<LocalFileChange> replay = [ .. _pendingChangesDuringLoad ];
						_pendingChangesDuringLoad.Clear();
						_ = ApplyLocalChangesAsync( replay );
					}
				}
				else if( currentGeneration )
				{
					_pendingChangesDuringLoad.Clear();
				}
			}
		}
	}

	private static (List<EntryRow> Rows, List<EntryRow> LocalPending, List<EntryRow> RemotePending, int ModifiedCount) PrepareEntriesPresentation(
		EntryListResult result,
		(string Field, bool Descending)? sort )
	{
		List<EntryRow> rows = sort is var (field, descending) && EntryRowKeys.TryGetSortKey( field, out var sortKey )
			? SortRows( result.Rows, sortKey, descending )
			: result.Rows;

		(List<EntryRow> localPending, List<EntryRow> remotePending, int modifiedCount) = CollectChangeBuckets( rows );
		return (rows, localPending, remotePending, modifiedCount);
	}

	private static (List<EntryRow> LocalPending, List<EntryRow> RemotePending, int ModifiedCount) CollectChangeBuckets( IReadOnlyList<EntryRow> rows )
	{
		List<EntryRow> localPending = [];
		List<EntryRow> remotePending = [];
		int modifiedCount = 0;

		for( int i = 0; i < rows.Count; i++ )
		{
			EntryRow r = rows[ i ];
			switch( r.Status )
			{
				case FileStatus.Lo:
					localPending.Add( r );
					break;
				case FileStatus.Lm or FileStatus.Cm:
					localPending.Add( r );
					modifiedCount++;
					break;
				case FileStatus.Ro or FileStatus.Nv or FileStatus.Co or FileStatus.Dt:
					remotePending.Add( r );
					break;
			}
		}
		return (localPending, remotePending, modifiedCount);
	}

	private static List<EntryRow> SortRows( IEnumerable<EntryRow> rows, Func<EntryRow, IComparable?> keySelector, bool descending )
		=> descending
			? [ .. rows.OrderByDescending( keySelector, SafeComparableComparer.Instance ) ]
			: [ .. rows.OrderBy( keySelector, SafeComparableComparer.Instance ) ];

	private void ApplyEntryLabels(
		EntryListResult result,
		List<EntryRow> localPending,
		List<EntryRow> remotePending,
		int modifiedCount )
	{
		int total = result.RemoteCount + result.LocalOnlyCount;
		int remoteOnly = Math.Max( 0, result.RemoteCount - result.LocalAndRemoteCount );

		EntriesLabel = $"Entries: {total}";
		EntriesLabelLocal = $"Local Only: {result.LocalOnlyCount}";
		EntriesLabelRemote = $"Remote Only: {remoteOnly}";
		EntriesLabelModified = $"Modified / Checked Out: {modifiedCount}";

		LocalChanges.ReplaceAll( localPending );
		RemoteChanges.ReplaceAll( remotePending );
		LocalChangesHeader = $"Local Changes ({localPending.Count})";
		RemoteChangesHeader = $"Server Updates ({remotePending.Count})";
		int pendingCount = localPending.Count + remotePending.Count;
		ChangesTabHeader = pendingCount > 0 ? $"Changes ({pendingCount})" : "Changes";
	}

	private void OnLocalFilesChanged( IReadOnlyList<LocalFileChange> batch )
	{
		if( IsDisposed || batch.Count == 0 )
			return;
		lock( _watcherQueueGate )
		{
			if( IsDisposed )
				return;
			if( !_queuedWatcherOverflow )
			{
				if( batch.Count > BulkReloadChangeThreshold || _queuedWatcherChanges.Count + batch.Count > BulkReloadChangeThreshold )
				{
					_queuedWatcherChanges.Clear();
					_queuedWatcherOverflow = true;
				}
				else
				{
					for( int i = 0; i < batch.Count; i++ )
					{
						LocalFileChange incoming = batch[ i ];
						if( _queuedWatcherChanges.TryGetValue( incoming.FullPath, out LocalFileChange? existing ) )
						{
							LocalChangeKind? merged = LocalChangeBatcher.Merge( existing.Kind, incoming.Kind );
							if( merged is { } k )
								_queuedWatcherChanges[ incoming.FullPath ] = new( incoming.FullPath, k, existing.IsDirectory || incoming.IsDirectory );
							else
								_queuedWatcherChanges.Remove( incoming.FullPath );
						}
						else
						{
							_queuedWatcherChanges[ incoming.FullPath ] = incoming;
						}
					}
				}
			}
			ScheduleWatcherDrainLocked();
		}
	}

	private void OnLocalChangesLost()
	{
		if( IsDisposed )
			return;
		lock( _watcherQueueGate )
		{
			if( IsDisposed )
				return;
			_queuedWatcherChanges.Clear();
			_queuedWatcherOverflow = true;
			ScheduleWatcherDrainLocked();
		}
	}

	private void ScheduleWatcherDrainLocked()
	{
		if( _watcherDrainRunning || ( !_queuedWatcherOverflow && _queuedWatcherChanges.Count == 0 ) )
			return;

		if( _watcherDrainScheduled )
		{
			if( _queuedWatcherOverflow && !_watcherScheduledForOverflow )
			{
				_watcherScheduledForOverflow = true;
				if( UiDispatcher.Current is not InlineUiDispatcher )
				{
					long remainingMs = OverflowRefreshCooldownMs - ( Environment.TickCount64 - _lastTreePushTick );
					if( remainingMs > 0 )
						_watcherCooldownTimer.Change( (int)remainingMs, Timeout.Infinite );
				}
			}
			return;
		}

		_watcherDrainScheduled = true;
		_watcherScheduledForOverflow = _queuedWatcherOverflow;
		if( UiDispatcher.Current is InlineUiDispatcher )
		{
			UiDispatcher.Post( () => _ = DrainWatcherQueueAsync() );
			return;
		}

		int cooldownMs = _queuedWatcherOverflow ? OverflowRefreshCooldownMs : TreeUpdateCooldownMs;
		long elapsed = Environment.TickCount64 - _lastTreePushTick;
		if( elapsed >= cooldownMs )
			UiDispatcher.Post( () => _ = DrainWatcherQueueAsync() );
		else
			_watcherCooldownTimer.Change( (int)( cooldownMs - elapsed ), Timeout.Infinite );
	}

	private async Task DrainWatcherQueueAsync()
	{
		if( IsDisposed )
			return;

		bool overflow;
		List<LocalFileChange>? batch = null;
		lock( _watcherQueueGate )
		{
			if( IsDisposed || _watcherDrainRunning )
			{
				_watcherDrainScheduled = false;
				_watcherScheduledForOverflow = false;
				return;
			}
			overflow = _queuedWatcherOverflow;
			if( overflow && UiDispatcher.Current is not InlineUiDispatcher )
			{
				long remainingMs = OverflowRefreshCooldownMs - ( Environment.TickCount64 - _lastTreePushTick );
				if( remainingMs > 0 )
				{
					_watcherDrainScheduled = true;
					_watcherScheduledForOverflow = true;
					_watcherCooldownTimer.Change( (int)remainingMs, Timeout.Infinite );
					return;
				}
			}
			_watcherDrainScheduled = false;
			_watcherScheduledForOverflow = false;
			_queuedWatcherOverflow = false;
			if( !overflow && _queuedWatcherChanges.Count > 0 )
			{
				batch = [ .. _queuedWatcherChanges.Values ];
				_queuedWatcherChanges.Clear();
			}
			if( !overflow && batch is null )
				return;
			_watcherDrainRunning = true;
		}

		try
		{
			if( overflow )
				Refresh( FileManagerRefresh.All );
			else if( batch is not null )
				await ApplyLocalChangesAsync( batch );
		}
		catch( Exception ex )
		{
			HackLogger.Error( "FileManager", $"Draining local file changes failed: {ex.Message}", ex );
		}
		finally
		{
			lock( _watcherQueueGate )
			{
				_lastTreePushTick = Environment.TickCount64;
				_watcherDrainRunning = false;
				if( !IsDisposed && ( _queuedWatcherOverflow || _queuedWatcherChanges.Count > 0 ) )
					ScheduleWatcherDrainLocked();
			}
		}
	}

	public async Task ApplyLocalChangesAsync( IReadOnlyList<LocalFileChange> batch )
	{
		if( IsDisposed || batch.Count == 0 )
			return;
		if( batch.Count > BulkReloadChangeThreshold )
		{
			Refresh( FileManagerRefresh.All );
			return;
		}
		bool entered = false;
		try
		{
			await _localChangesGate.WaitAsync( _pageCancellation.Token );
			entered = true;
			if( IsDisposed )
				return;

			string? listed = IsListLoaded ? LastSelectedNodePath : null;
			string? activeFolder = LastSelectedNodePath;
			bool entriesLoading = !IsListLoaded && activeFolder is not null;
			DirectoryNode? treeSnapshot = _state.Tree;

			var classified = await Task.Run( () =>
			{
				List<LocalFileChange> here = [];
				List<LocalFileChange> pending = [];
				Dictionary<string, LocalChangeKind> elsewhere = new( StringComparer.OrdinalIgnoreCase );
				bool treeChanged = false;
				bool reloadCurrentEntries = false;

				foreach( LocalFileChange change in batch )
				{
					string? itemServerPath = PwaPaths.ToServerName( change.FullPath );
					string? parentFolder = PwaPaths.ToServerName( Path.GetDirectoryName( change.FullPath ) );
					if( itemServerPath is null && parentFolder is null )
						continue;

					string[]? itemSegments = null;
					bool isDirectory = change.IsDirectory || ( change.Kind != LocalChangeKind.Deleted && Directory.Exists( change.FullPath ) );
					if( !isDirectory && change.Kind == LocalChangeKind.Deleted && treeSnapshot is not null )
					{
						itemSegments = PwaPaths.Segments( itemServerPath );
						if( itemSegments.Length > 0 )
						{
							lock( _treeLock )
								isDirectory = treeSnapshot.FindBySegments( itemSegments ) is not null;
						}
					}

					if( isDirectory )
					{
						itemSegments ??= PwaPaths.Segments( itemServerPath );
						if( itemSegments.Length > 0 && treeSnapshot is not null )
						{
							lock( _treeLock )
							{
								if( change.Kind == LocalChangeKind.Created )
									treeChanged |= treeSnapshot.EnsureLocalPath( itemSegments );
								else if( change.Kind == LocalChangeKind.Deleted )
								{
									treeChanged |= treeSnapshot.MarkLocalDeleted( itemSegments );
									if( activeFolder is not null && PwaPaths.IsSameOrAncestorTreePath( itemServerPath, activeFolder ) )
										reloadCurrentEntries = true;
								}
							}
						}
						if( parentFolder is not null && ( listed is null || !PwaPaths.SameTreePath( parentFolder, listed ) ) )
							MergeElsewhere( elsewhere, parentFolder, change.Kind );
						continue;
					}

					if( parentFolder is null )
						continue;
					if( listed is not null && PwaPaths.SameTreePath( parentFolder, listed ) )
						here.Add( change );
					else if( entriesLoading && PwaPaths.SameTreePath( parentFolder, activeFolder ) )
						pending.Add( change );
					else
						MergeElsewhere( elsewhere, parentFolder, change.Kind );
				}

				string? fallbackSelectedPath = null;
				if( treeChanged && treeSnapshot is not null && activeFolder is not null )
				{
					string[] selectedSegments = PwaPaths.Segments( activeFolder );
					lock( _treeLock )
					{
						if( treeSnapshot.FindBySegments( selectedSegments ) is null )
						{
							int len = selectedSegments.Length - 1;
							while( len > 0 && treeSnapshot.FindBySegments( selectedSegments.AsSpan( 0, len ) ) is null )
								len--;
							fallbackSelectedPath = len == 0
								? treeSnapshot.Name
								: string.Join( PwaPaths.NameSeparator, [ treeSnapshot.Name, .. selectedSegments[ ..len ] ] );
							reloadCurrentEntries = true;
						}
					}
				}

				return (Here: here, Pending: pending, Elsewhere: elsewhere, TreeChanged: treeChanged, ReloadCurrentEntries: reloadCurrentEntries, FallbackSelectedPath: fallbackSelectedPath);
			}, _pageCancellation.Token );

			if( IsDisposed )
				return;

			if( classified.Pending.Count > 0 )
			{
				if( IsListLoaded && PwaPaths.SameTreePath( LastSelectedNodePath, activeFolder ) )
				{
					listed ??= LastSelectedNodePath;
					classified.Here.AddRange( classified.Pending );
				}
				else if( !IsListLoaded )
				{
					_pendingChangesDuringLoad.AddRange( classified.Pending );
				}
			}

			if( classified.FallbackSelectedPath is not null )
				LastSelectedNodePath = classified.FallbackSelectedPath;

			if( classified.TreeChanged && treeSnapshot is not null && !IsDisposed )
			{
				if( TreeLoaded is { } treeHandler )
					await treeHandler( treeSnapshot );
			}

			foreach( var (folder, kind) in classified.Elsewhere )
				FolderChanged?.Invoke( folder, kind );

			if( classified.ReloadCurrentEntries || classified.Here.Count > MaxSingleRowUpdates )
			{
				Refresh( FileManagerRefresh.Entries );
				return;
			}
			if( classified.Here.Count == 0 || listed is null )
				return;

			// Deduplicate paths targeting the same file name while preserving batch order.
			Dictionary<string, string> uniquePathsByName = new( PwaPaths.PathComparer );
			foreach( LocalFileChange change in classified.Here )
				uniquePathsByName[ Path.GetFileName( change.FullPath ) ] = change.FullPath;

			int generation = Volatile.Read( ref _entriesLoadGeneration );
			List<EntryRow> currentEntries = [ .. Entries ];
			var sort = _state.Sort;

			var batchUpdate = await Task.Run(
				() => BuildLiveBatchAsync( currentEntries, uniquePathsByName, sort, _pageCancellation.Token ),
				_pageCancellation.Token );

			if( IsDisposed || !IsListLoaded || !PwaPaths.SameTreePath( LastSelectedNodePath, listed ) || generation != Volatile.Read( ref _entriesLoadGeneration ) )
				return;
			if( !batchUpdate.Changed || batchUpdate.Result is null )
				return;

			LiveBatchApplying?.Invoke( true );
			try
			{
				Entries.ReplaceAll( batchUpdate.Result.Rows );
				_state.RememberEntries( LastSelectedNodePath, ShowInactive, batchUpdate.Result );
				ApplyEntryLabels( batchUpdate.Result, batchUpdate.LocalPending!, batchUpdate.RemotePending!, batchUpdate.ModifiedCount );
				IsEmptyDirectory = IsListLoaded && SelectedDirectory is not null && Entries.Count == 0;
				RowsReplaced?.Invoke( batchUpdate.Replacements! );
				GroupingChanged?.Invoke();
			}
			finally
			{
				LiveBatchApplying?.Invoke( false );
			}
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception e )
		{
			HackLogger.Error( "FileManager", $"Live file update failed: {e.Message}", e );
		}
		finally
		{
			if( entered )
			{
				try { _localChangesGate.Release(); }
				catch( ObjectDisposedException ) { }
			}
		}
	}

	private async Task<(
		bool Changed,
		EntryListResult? Result,
		List<EntryRow>? LocalPending,
		List<EntryRow>? RemotePending,
		int ModifiedCount,
		Dictionary<EntryRow, EntryRow?>? Replacements)> BuildLiveBatchAsync(
		List<EntryRow> currentEntries,
		Dictionary<string, string> uniquePathsByName,
		(string Field, bool Descending)? sort,
		CancellationToken token )
	{
		Dictionary<string, int> indexByName = new( currentEntries.Count, StringComparer.OrdinalIgnoreCase );
		for( int i = 0; i < currentEntries.Count; i++ )
		{
			if( currentEntries[ i ].Name is { } n && !indexByName.ContainsKey( n ) )
				indexByName[ n ] = i;
		}

		List<(int ExistingIndex, EntryRow? OldRow, string LocalPath)> workItems = new( uniquePathsByName.Count );
		foreach( var (fileName, localPath) in uniquePathsByName )
		{
			int idx = indexByName.TryGetValue( fileName, out int foundIdx ) ? foundIdx : -1;
			EntryRow? old = idx >= 0 ? currentEntries[ idx ] : null;
			workItems.Add( (idx, old, localPath) );
		}

		EntryRow?[] refreshedRows = new EntryRow?[ workItems.Count ];
		await Parallel.ForEachAsync(
			Enumerable.Range( 0, workItems.Count ),
			new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp( workItems.Count, 1, 8 ), CancellationToken = token },
			async ( i, _ ) =>
			{
				refreshedRows[ i ] = await _entryList.RefreshRowAsync( workItems[ i ].OldRow, workItems[ i ].LocalPath, filterUnknownTypes: true ).ConfigureAwait( false );
			} );

		token.ThrowIfCancellationRequested();

		Dictionary<EntryRow, EntryRow?> replacements = [];
		HashSet<int> removedIndices = [];
		List<EntryRow> addedRows = [];
		bool changed = false;

		for( int i = 0; i < workItems.Count; i++ )
		{
			var (idx, oldRow, _) = workItems[ i ];
			EntryRow? nextRow = refreshedRows[ i ];
			if( nextRow is null )
			{
				if( idx < 0 )
					continue;
				removedIndices.Add( idx );
				replacements[ oldRow! ] = null;
				changed = true;
			}
			else if( idx >= 0 )
			{
				currentEntries[ idx ] = nextRow;
				replacements[ oldRow! ] = nextRow;
				changed = true;
			}
			else
			{
				addedRows.Add( nextRow );
				changed = true;
			}
		}

		if( !changed )
			return default;

		List<EntryRow> updatedRows = new( currentEntries.Count - removedIndices.Count + addedRows.Count );
		for( int i = 0; i < currentEntries.Count; i++ )
		{
			if( !removedIndices.Contains( i ) )
				updatedRows.Add( currentEntries[ i ] );
		}
		updatedRows.AddRange( addedRows );

		if( sort is var (field, descending) && EntryRowKeys.TryGetSortKey( field, out var sortKey ) )
			updatedRows = SortRows( updatedRows, sortKey, descending );

		int remoteCount = 0;
		int localOnlyCount = 0;
		int localAndRemoteCount = 0;
		for( int i = 0; i < updatedRows.Count; i++ )
		{
			EntryRow r = updatedRows[ i ];
			if( r.Id is > 0 )
				remoteCount++;
			if( r.ReprType == EntryReprType.Local )
				localOnlyCount++;
			else if( r.ReprType == EntryReprType.Both )
				localAndRemoteCount++;
		}

		EntryListResult result = new( updatedRows, remoteCount, localOnlyCount, localAndRemoteCount );
		(List<EntryRow> localPending, List<EntryRow> remotePending, int modifiedCount) = CollectChangeBuckets( updatedRows );
		return (true, result, localPending, remotePending, modifiedCount, replacements);
	}

	private static void MergeElsewhere( Dictionary<string, LocalChangeKind> map, string folder, LocalChangeKind kind )
		=> map[ folder ] = map.TryGetValue( folder, out LocalChangeKind prev ) && prev != kind ? LocalChangeKind.Updated : kind;

	public void ClearDetails()
	{
		SelectedHistory = null;
		OHistories.ClearIfNonEmpty();
		OParents.ClearIfNonEmpty();
		OChildren.ClearIfNonEmpty();
		OProperties.ClearIfNonEmpty();
		OVersions.ClearIfNonEmpty();
	}

	public async Task ShowEntryDetailsAsync( EntryRow? entry )
	{
		if( IsDisposed )
			return;

		CancellationTokenSource nextDetails = CtsHelper.Replace( ref _detailsCancellation );
		CancellationTokenSource nextPreview = CtsHelper.Replace( ref _previewCancellation );
		CancellationTokenSource? linkedDetails = null;
		CancellationTokenSource? linkedPreview = null;
		try
		{
			linkedDetails = CancellationTokenSource.CreateLinkedTokenSource( nextDetails.Token, _pageCancellation.Token );
			linkedPreview = CancellationTokenSource.CreateLinkedTokenSource( nextPreview.Token, _pageCancellation.Token );
		}
		catch( ObjectDisposedException )
		{
			linkedDetails?.Dispose();
			linkedPreview?.Dispose();
			return;
		}
		using CancellationTokenSource detailsGuard = linkedDetails;
		using CancellationTokenSource previewGuard = linkedPreview;
		int previewGeneration = Interlocked.Increment( ref _previewVersionGeneration );

		ClearDetails();
		Preview = null;
		if( entry is null )
		{
			IsDetailsLoading = false;
			IsPreviewLoading = false;
			return;
		}

		IsDetailsLoading = true;
		IsPreviewLoading = true;
		try
		{
			await Task.WhenAll(
				LoadDetailTabAsync( entry, linkedDetails.Token ),
				LoadEntryPreviewAsync( entry, previewGeneration, linkedPreview.Token ) );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Showing details for '{entry.Name}' failed: {ex.Message}", ex );
		}
	}

	public async Task LoadSelectedDetailTabAsync( EntryRow? entry )
	{
		if( IsDisposed )
			return;

		CancellationTokenSource nextDetails = CtsHelper.Replace( ref _detailsCancellation );
		CancellationTokenSource? linkedDetails = null;
		try
		{
			linkedDetails = CancellationTokenSource.CreateLinkedTokenSource( nextDetails.Token, _pageCancellation.Token );
		}
		catch( ObjectDisposedException )
		{
			linkedDetails?.Dispose();
			return;
		}
		using CancellationTokenSource detailsGuard = linkedDetails;

		ClearDetails();
		if( entry is null )
		{
			IsDetailsLoading = false;
			return;
		}

		IsDetailsLoading = true;
		try
		{
			await LoadDetailTabAsync( entry, linkedDetails.Token );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Switching detail tab for '{entry.Name}' failed: {ex.Message}", ex );
		}
	}

	private async Task LoadDetailTabAsync( EntryRow entry, CancellationToken token )
	{
		try
		{
			if( SelectedDetailTab is { } tab && _detailTabs.TryGetValue( tab, out var fill ) )
				await fill( entry, token );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception e )
		{
			HackLogger.Warn( "FileManager", $"Loading {SelectedDetailTab} for '{entry.Name}' failed: {e.Message}", e );
		}
		finally
		{
			if( !token.IsCancellationRequested && !IsDisposed )
				IsDetailsLoading = false;
		}
	}

	private async Task LoadEntryPreviewAsync( EntryRow entry, int generation, CancellationToken token )
	{
		try
		{
			token.ThrowIfCancellationRequested();
			byte[]? image = entry.LatestId is int versionId
				? await _details.GetPreviewImageAsync( versionId, token )
				: null;
			token.ThrowIfCancellationRequested();

			string? localPath = null;
			if( image is not { Length: > 0 } )
			{
				localPath = await Task.Run( () => entry.LocalFile is { Exists: true } local ? local.FullName : null, token );
				token.ThrowIfCancellationRequested();
				if( localPath is not null )
				{
					image = await _details.GetLocalPreviewImageAsync( localPath, token );
					token.ThrowIfCancellationRequested();
				}
			}

			EntryPreview? preview = image is { Length: > 0 }
				? new EntryPreview( image, null )
				: entry.Status is FileStatus.Lo
					? new EntryPreview( null, localPath )
					: null;
			if( !token.IsCancellationRequested
				&& !IsDisposed
				&& generation == Volatile.Read( ref _previewVersionGeneration )
				&& preview is not null )
				Preview = preview;
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception e )
		{
			HackLogger.Warn( "FileManager", $"Loading preview for '{entry.Name}' failed: {e.Message}", e );
		}
		finally
		{
			if( !token.IsCancellationRequested
				&& !IsDisposed
				&& generation == Volatile.Read( ref _previewVersionGeneration ) )
				IsPreviewLoading = false;
		}
	}

	public async Task PreviewVersionAsync( int? versionId, string? localPath = null )
	{
		if( IsDisposed )
			return;
		CancellationTokenSource nextPreview = CtsHelper.Replace( ref _previewCancellation );
		CancellationTokenSource? linkedPreview = null;
		try
		{
			linkedPreview = CancellationTokenSource.CreateLinkedTokenSource( nextPreview.Token, _pageCancellation.Token );
		}
		catch( ObjectDisposedException )
		{
			linkedPreview?.Dispose();
			return;
		}
		using CancellationTokenSource previewGuard = linkedPreview;
		CancellationToken token = linkedPreview.Token;
		int generation = Interlocked.Increment( ref _previewVersionGeneration );

		Preview = null;
		if( versionId is null or 0 && string.IsNullOrEmpty( localPath ) )
		{
			IsPreviewLoading = false;
			return;
		}

		IsPreviewLoading = true;
		try
		{
			token.ThrowIfCancellationRequested();
			byte[]? bytes = versionId is > 0
				? await _details.GetPreviewImageAsync( versionId, token )
				: null;
			token.ThrowIfCancellationRequested();
			if( bytes is not { Length: > 0 }
				&& !string.IsNullOrEmpty( localPath )
				&& await Task.Run( () => File.Exists( localPath ), token ) )
			{
				bytes = await _details.GetLocalPreviewImageAsync( localPath, token );
			}

			if( !IsDisposed && !token.IsCancellationRequested && generation == Volatile.Read( ref _previewVersionGeneration ) )
				Preview = bytes is { Length: > 0 } ? new EntryPreview( bytes, null ) : null;
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception e )
		{
			HackLogger.Warn( "FileManager", $"Loading preview for version {versionId} failed: {e.Message}", e );
		}
		finally
		{
			if( !IsDisposed && !token.IsCancellationRequested && generation == Volatile.Read( ref _previewVersionGeneration ) )
				IsPreviewLoading = false;
		}
	}

	[RelayCommand]
	private void GroupBy( string? propertyName )
	{
		if( !EntryRowKeys.TryGetGroupKey( propertyName, out var key ) )
			return;
		_groupKey = key;
		GroupField = propertyName;
		if( !IsGroupingEnabled )
			IsGroupingEnabled = true;
		else
			GroupingChanged?.Invoke();
	}

	public async Task SortEntriesAsync( string? fieldName, bool descending )
	{
		if( IsDisposed || !EntryRowKeys.TryGetSortKey( fieldName, out var key ) )
			return;

		_state.Sort = (fieldName!, descending);
		CancellationTokenSource nextSort = CtsHelper.Replace( ref _sortCancellation );
		CancellationTokenSource? linked = null;
		try
		{
			linked = CancellationTokenSource.CreateLinkedTokenSource( nextSort.Token, _pageCancellation.Token );
		}
		catch( ObjectDisposedException )
		{
			linked?.Dispose();
			return;
		}
		using CancellationTokenSource linkedGuard = linked;
		CancellationToken token = linked.Token;

		int generation = Volatile.Read( ref _entriesLoadGeneration );
		List<EntryRow> snapshot = [ .. Entries ];
		try
		{
			List<EntryRow> sorted = await Task.Run( () => SortRows( snapshot, key, descending ), token );
			if( IsDisposed || token.IsCancellationRequested || generation != Volatile.Read( ref _entriesLoadGeneration ) )
				return;

			LiveBatchApplying?.Invoke( true );
			try
			{
				Entries.ReplaceAll( sorted );
				RowsReplaced?.Invoke( new Dictionary<EntryRow, EntryRow?>() );
				GroupingChanged?.Invoke();
			}
			finally
			{
				LiveBatchApplying?.Invoke( false );
			}
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		catch( Exception ex )
		{
			HackLogger.Warn( "FileManager", $"Sorting entries by '{fieldName}' failed: {ex.Message}", ex );
		}
	}

	private async Task RunOnDirectoryAsync(
		string what,
		bool withSubdirectories,
		bool withDeleted,
		Func<IReadOnlyList<int>, Task> operation,
		string? completedMessage = null )
	{
		if( SelectedDirectory is not { DirectoryId: > 0 } directory )
		{
			await Dialogs.ShowMessageAsync( "Current directory doesn't exist remotely." );
			return;
		}
		await RunOperationAsync( async () =>
		{
			Reporter.Log( StatusMessage.PROCESSING, $"{what} all entries within directory ({directory.Name}, id: {directory.DirectoryId})" );
			using( _localFiles.Suspend() )
			{
				IReadOnlyList<int> entryIds = await _workflow.GetDirectoryEntryIdsAsync( directory.DirectoryId, withSubdirectories, withDeleted );
				if( entryIds.Count == 0 )
				{
					Reporter.Log( StatusMessage.INFO, $"No remote entries found in directory ({directory.Name}, id: {directory.DirectoryId})." );
					return;
				}
				await operation( entryIds );
			}
			if( !IsDisposed )
				Refresh( FileManagerRefresh.All );
		}, completedMessage );
	}

	[RelayCommand] private Task GetLatestDirectoryAsync() => RunOnDirectoryAsync( "Getting the latest of", true, ShowInactive, _workflow.GetLatestAsync );
	[RelayCommand] private Task GetLatestDirectoryTopAsync() => RunOnDirectoryAsync( "Getting the latest of", false, ShowInactive, _workflow.GetLatestAsync );
	[RelayCommand] private Task CheckoutDirectoryAsync() => RunOnDirectoryAsync( "Checking out", true, false, _workflow.CheckoutLatestAsync );
	[RelayCommand] private Task UndoCheckoutDirectoryAsync() => RunOnDirectoryAsync( "Undoing the check out of", true, false, _workflow.UnCheckoutAsync );
	[RelayCommand] private Task LogicalDeleteDirectoryAsync() => RunOnDirectoryAsync( "Deleting", true, false, _workflow.LogicalDeleteAsync, "Completed!" );

	[RelayCommand] private Task RestoreDirectoryAsync() => RestoreDirectoryCoreAsync( withSubdirectories: false );
	[RelayCommand] private Task RestoreDirectoryRecursiveAsync() => RestoreDirectoryCoreAsync( withSubdirectories: true );

	private async Task RestoreDirectoryCoreAsync( bool withSubdirectories )
	{
		if( SelectedDirectory is not { DirectoryId: > 0 } directory )
		{
			await Dialogs.ShowMessageAsync( "Current directory doesn't exist remotely." );
			return;
		}
		await RunOperationAsync( async () =>
		{
			using( _localFiles.Suspend() )
				await _workflow.RestoreDeletedInDirectoryAsync( directory.DirectoryId, withSubdirectories );
			if( !IsDisposed )
				Refresh( FileManagerRefresh.All );
		}, "Completed!" );
	}

	[RelayCommand]
	private async Task CommitDirectoryAsync()
	{
		if( SelectedDirectory is null || SelectedDirectory.IsRemoteOnly )
			return;
		await RunOperationAsync( async () =>
		{
			bool committed = false;
			using( _localFiles.Suspend() )
			{
				if( await _workflow.CollectFolderCommitFilesAsync( SelectedLocalDirectory ) is { Count: > 0 } files )
				{
					await _workflow.CommitAsync( files );
					committed = true;
				}
			}
			if( committed && !IsDisposed )
				Refresh( FileManagerRefresh.All );
		} );
	}

	[RelayCommand]
	private Task OpenDirectoryAsync()
	{
		string directory = SelectedLocalDirectory;
		return Task.Run( () => _files.OpenFolder( directory ) );
	}

	[RelayCommand]
	private async Task LocalDeleteDirectoryAsync()
	{
		if( SelectedDirectory is null || SelectedDirectory.IsRemoteOnly )
			return;
		string directory = SelectedLocalDirectory;
		if( await Task.Run( () => _files.CountFiles( directory ) ) is not int count )
			return;
		if( await Dialogs.ConfirmAsync( $"Are you sure you want to delete this directory and ({count}) files inside?", "Delete Directory", MessageBoxIcon.Warning ) )
		{
			using( _localFiles.Suspend() )
				await Task.Run( () => _files.DeleteDirectory( directory ) );
			await ApplyLocalChangesAsync( new LocalFileChange[] { new( directory, LocalChangeKind.Deleted, IsDirectory: true ) } );
		}
	}

	private Task RunOnSelectedIdsAsync(
		IReadOnlyList<int> ids,
		Func<IReadOnlyList<int>, Task> operation,
		FileManagerRefresh refresh,
		string? completedMessage = null )
		=> ids.Count == 0
			? Task.CompletedTask
			: RunOperationAsync( async () =>
			{
				using( _localFiles.Suspend() )
					await operation( ids );
				if( !IsDisposed )
					Refresh( refresh );
			}, completedMessage );

	[RelayCommand] private Task GetLatestSelectedAsync() => RunOnSelectedIdsAsync( RemoteIds( SelectedEntries ), _workflow.GetLatestAsync, FileManagerRefresh.All );
	[RelayCommand] private Task CheckoutSelectedAsync() => RunOnSelectedIdsAsync( RemoteIds( SelectedEntries.Where( e => e.Checkout is null ) ), _workflow.CheckoutLatestAsync, FileManagerRefresh.Entries );
	[RelayCommand] private Task UndoCheckoutSelectedAsync() => RunOnSelectedIdsAsync( RemoteIds( SelectedEntries.Where( e => e.Checkout is not null ) ), _workflow.UnCheckoutAsync, FileManagerRefresh.Entries );
	[RelayCommand] private Task LogicalDeleteSelectedAsync() => RunOnSelectedIdsAsync( RemoteIds( SelectedEntries ), _workflow.LogicalDeleteAsync, FileManagerRefresh.All, "Completed!" );

	[RelayCommand]
	private Task CommitSelectedAsync() => RunOperationAsync( async () =>
	{
		bool committed = false;
		using( _localFiles.Suspend() )
		{
			HashSet<HackFile> files = await _workflow.CollectCommitFilesAsync( SelectedEntries );
			if( files.Count > 0 )
			{
				await _workflow.CommitAsync( files );
				committed = true;
			}
		}
		if( committed && !IsDisposed )
			Refresh( FileManagerRefresh.Entries );
	} );

	[RelayCommand] private Task OpenSelectedAsync() => OpenSelectedAsync( EntryOpenMode.Open );
	[RelayCommand] private Task OpenSelectedLatestRemoteAsync() => OpenSelectedAsync( EntryOpenMode.LatestRemote );
	[RelayCommand] private Task OpenSelectedLatestLocalAsync() => OpenSelectedAsync( EntryOpenMode.LatestLocal );

	[RelayCommand]
	private Task OpenSelectedFoldersAsync()
	{
		IReadOnlyList<EntryRow> selection = SelectedEntries;
		return Task.Run( () => _files.OpenContainingFolders( selection ) );
	}

	private async Task OpenSelectedAsync( EntryOpenMode mode )
	{
		IReadOnlyList<EntryRow> selection = SelectedEntries;
		IReadOnlyList<string> errors = await Task.Run( () => _files.OpenEntriesAsync( selection, mode ), _pageCancellation.Token );
		if( errors.Count > 0 )
			await Dialogs.ShowMessageAsync( string.Join( Environment.NewLine, errors ) );
	}

	[RelayCommand]
	private async Task LocalDeleteSelectedAsync()
	{
		string directory = SelectedLocalDirectory;
		IReadOnlyList<EntryRow> selection = SelectedEntries;
		List<FileInfo> files = await Task.Run( () => _files.FindLocalFiles( directory, selection ) );
		if( files.Count == 0 )
			return;
		string message = files.Count > 10
			? $"Are you sure you want to delete ({files.Count}) files?"
			: $"Are you sure you want to delete these files?\n{string.Join( Environment.NewLine, files.Select( f => f.FullName ) )}";
		if( !await Dialogs.ConfirmAsync( message, "Delete Files", MessageBoxIcon.Warning ) )
			return;
		using( _localFiles.Suspend() )
			await Task.Run( () => _files.DeleteFiles( files ) );
		if( !IsDisposed )
			Refresh( FileManagerRefresh.Entries );
	}

	[RelayCommand]
	private async Task PermanentDeleteSelectedAsync()
	{
		if( !await _workflow.IsCurrentUserPdmAdminAsync() )
		{
			await Dialogs.ShowMessageAsync( "Permanent delete requires PDM Administrator permissions.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		IReadOnlyList<int> ids = RemoteIds( SelectedEntries );
		if( ids.Count == 0 )
		{
			await Dialogs.ShowMessageAsync( "No entries to delete." );
			return;
		}
		if( !await Dialogs.ConfirmAsync(
			$"Permanently delete {ids.Count} entries from the database?\nThis will permanently delete all versions, properties, and relationships.",
			"Permanent Delete", MessageBoxIcon.Warning ) )
			return;

		bool deleted = false;
		await RunOperationAsync( async () => deleted = await _workflow.PermanentDeleteAsync( ids ) );
		if( !deleted )
		{
			await Dialogs.ShowMessageAsync( "Was unable to delete entries.", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error );
			return;
		}
		await Dialogs.ShowMessageAsync( "Completed!" );
		Refresh( FileManagerRefresh.All );
	}

	[RelayCommand]
	private async Task UploadPreviewImageAsync()
	{
		if( _pickers is null )
			return;

		EntryRow? entry = SelectedEntries is [ { } single ] ? single : null;
		int? targetVersionId = entry is null
			? null
			: SelectedHistory?.Version is int histId and > 0
				? histId
				: entry.LatestId is int latestId and > 0
					? latestId
					: null;
		if( targetVersionId is not int versionId )
		{
			await Dialogs.ShowMessageAsync(
				"Select a single committed file (or a version in History) to upload a preview image.",
				"Upload Preview Image", MessageBoxButtons.OK, MessageBoxIcon.Information );
			return;
		}

		string? filePath = await _pickers.PickOpenFileAsync( PreviewImageValidator.SupportedExtensions );
		if( string.IsNullOrEmpty( filePath ) )
			return;

		byte[] bytes;
		try { bytes = await File.ReadAllBytesAsync( filePath, _pageCancellation.Token ); }
		catch( OperationCanceledException ) { return; }
		catch( Exception ex )
		{
			await Dialogs.ShowMessageAsync( $"Could not read the selected image: {ex.Message}", "Upload Preview Image", MessageBoxButtons.OK, MessageBoxIcon.Error );
			return;
		}

		string? validationError = await Task.Run( () => PreviewImageValidator.Validate( bytes ), _pageCancellation.Token );
		if( validationError is not null )
		{
			await Dialogs.ShowMessageAsync( validationError, "Invalid Image", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		HackPDM.Core.Odoo.Result<bool> result;
		try { result = await _details.UploadPreviewImageAsync( versionId, bytes, _pageCancellation.Token ); }
		catch( OperationCanceledException ) { return; }
		if( !result.Ok || !result.Value )
		{
			await Dialogs.ShowMessageAsync( result.Error ?? "Could not upload the preview image.", "Upload Preview Image", MessageBoxButtons.OK, MessageBoxIcon.Error );
			return;
		}

		Preview = new EntryPreview( bytes, null );
		Reporter.Log( StatusMessage.SUCCESS, $"Updated preview image for {entry?.Name ?? $"version {versionId}"} (v{versionId})." );
	}

	private async Task WithHistoryVersionAsync( Func<HpVersion, Task> act )
	{
		int versionId = SelectedHistory?.Version ?? 0;
		if( await Task.Run( () => _files.GetVersionAsync( versionId ), _pageCancellation.Token ) is { } version )
			await act( version );
	}

	[RelayCommand]
	private Task DownloadHistoryAsync() => WithHistoryVersionAsync( async version =>
	{
		bool hasConflict = await _files.HasConflictingLocalCopyAsync( version, _pageCancellation.Token );
		if( !hasConflict )
		{
			await Task.Run( () => _files.DownloadVersionAsync( version, toTemp: false ), _pageCancellation.Token );
			return;
		}
		DialogResult answer = await Dialogs.ShowMessageAsync(
			"File exists as a different version.\nRetry:\tDownload to Temporary Folder\nIgnore:\tOverwrite current version\nAbort:\tCancel",
			"File Version Conflict", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning );
		if( answer is DialogResult.Retry or DialogResult.Ignore )
			await Task.Run( () => _files.DownloadVersionAsync( version, toTemp: answer is DialogResult.Retry ), _pageCancellation.Token );
	} );

	[RelayCommand] private Task DownloadHistoryToTempAsync() => WithHistoryVersionAsync( v => Task.Run( () => _files.DownloadVersionAsync( v, toTemp: true ), _pageCancellation.Token ) );
	[RelayCommand] private Task DownloadHistoryOverwriteAsync() => WithHistoryVersionAsync( v => Task.Run( () => _files.DownloadVersionAsync( v, toTemp: false ), _pageCancellation.Token ) );
	[RelayCommand] private Task OpenHistoryFromTempAsync() => WithHistoryVersionAsync( v => OpenHistoryAsync( v, toTemp: true ) );
	[RelayCommand] private Task OpenHistoryOverwriteAsync() => WithHistoryVersionAsync( v => OpenHistoryAsync( v, toTemp: false ) );
	[RelayCommand] private Task MoveHistoryToTempAsync() => WithHistoryVersionAsync( v => MoveHistoryAsync( v, toTemp: true ) );
	[RelayCommand] private Task MoveHistoryToWorkingAsync() => WithHistoryVersionAsync( v => MoveHistoryAsync( v, toTemp: false ) );

	private async Task OpenHistoryAsync( HpVersion version, bool toTemp )
	{
		string folder = await Task.Run( () => _files.DownloadVersionAsync( version, toTemp ), _pageCancellation.Token );
		string filePath = Path.Combine( folder, version.name ?? "" );
		await Task.Run( () => _files.OpenFile( filePath ), _pageCancellation.Token );
	}

	private async Task MoveHistoryAsync( HpVersion version, bool toTemp )
	{
		var (plan, fromExists, toExists) = await Task.Run( () =>
		{
			VersionMovePlan movePlan = _files.PlanMove( version, toTemp );
			return (movePlan, movePlan.From.Exists, movePlan.To.Exists);
		}, _pageCancellation.Token );

		string target = toTemp ? "temporary" : "current";
		string prefix = fromExists ? "" : $"File doesn't exist in {plan.From.DirectoryName}.\n";
		string verb = fromExists ? "move" : "download";
		string message = toExists
			? $"{prefix}Would you like to {verb} this version to {target} and overwrite that version?"
			: $"{prefix}Would you like to {verb} this version to {target}?";
		string caption = ( fromExists ? "Move" : "Download" ) + ( toExists ? " & Overwrite" : "" );

		if( !await Dialogs.ConfirmAsync( message, caption, toExists ? MessageBoxIcon.Warning : MessageBoxIcon.Question ) )
			return;
		using( _localFiles.Suspend() )
			await Task.Run( () => _files.ExecuteMoveAsync( version, plan ), _pageCancellation.Token );
		if( !IsDisposed )
			Refresh( FileManagerRefresh.Entries );
	}

	[RelayCommand]
	private async Task DownloadHistoryWithDependenciesAsync()
	{
		if( SelectedHistory is not { Version: not 0 } row || await _workflow.PlanVersionDownloadAsync( row.Version ) is not { } plan )
			return;

		HpVersion v = plan.Version;
		string intro = plan.IsLatest
			? "You are trying to download the latest version and dependencies. Continue?"
			: "You are trying to download a previous version and dependencies. Continue?";
		StringBuilder details = new StringBuilder()
			.AppendLine( "version:" )
			.AppendLine( $"\tName = {v.name}" )
			.AppendLine( $"\tID = {v.id}" )
			.AppendLine( $"\tSize = {v.file_size}" )
			.AppendLine( $"\tChecksum = {v.checksum}" )
			.AppendLine( $"\tMod Date = {v.file_modify_stamp}" )
			.Append( $"\tWin DL Path = {v.WinPathway}" );

		string ids = string.Join( ", ", plan.VersionIds );
		if( await Dialogs.ShowMessageAsync( $"{intro}\nVersion IDs: {ids}\n{details}", "Version Download", MessageBoxButtons.YesNoCancel ) != DialogResult.Yes )
			return;

		IReadOnlyList<int> failed = [];
		await RunOperationAsync( async () =>
		{
			using( _localFiles.Suspend() )
				failed = await _workflow.DownloadVersionsAsync( plan.VersionIds );
			if( !IsDisposed )
				Refresh( FileManagerRefresh.All );
		} );
		if( failed.Count > 0 )
			await Dialogs.ShowMessageAsync( $"Failed to download version IDs: {string.Join( ", ", failed )}" );
	}
}
