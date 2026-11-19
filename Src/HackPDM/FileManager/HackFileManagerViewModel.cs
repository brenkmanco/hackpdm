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
	private readonly List<LocalFileChange> _pendingChangesDuringLoad = [];
	private readonly CancellationTokenSource _pageCancellation = new();
	private readonly bool _restoring;
	private readonly string? _selectionFolder;

	private CancellationTokenSource? _detailsCancellation;
	private IReadOnlyList<string> _selectionToRestore;
	private Func<EntryRow, IComparable?> _groupKey = GroupByStatus;
	private int _entriesLoadGeneration;
	private int _previewVersionGeneration;
	private bool _active;

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
				var rows = await _details.GetHistoryAsync( entry );
				ct.ThrowIfCancellationRequested();
				OHistories.ReplaceAll( rows );
			},
			[ PdmConstants.ParentTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetParentsAsync( entry );
				ct.ThrowIfCancellationRequested();
				OParents.ReplaceAll( rows );
			},
			[ PdmConstants.ChildTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetChildrenAsync( entry );
				ct.ThrowIfCancellationRequested();
				OChildren.ReplaceAll( rows );
			},
			[ PdmConstants.PropertiesTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetPropertiesAsync( entry );
				ct.ThrowIfCancellationRequested();
				OProperties.ReplaceAll( rows );
			},
			[ PdmConstants.InfoTab ] = async ( entry, ct ) =>
			{
				var rows = await _details.GetVersionInfoAsync( entry );
				ct.ThrowIfCancellationRequested();
				OVersions.ReplaceAll( rows );
			},
		};

		_restoring = true;
		ShowInactive = state.ShowInactive;
		IsGroupingEnabled = state.IsGroupingEnabled;
		if( EntryRowKeys.TryGetGroupKey( state.GroupField, out var groupKey ) )
			_groupKey = groupKey;
		_selectionToRestore = state.SelectedEntryNames;
		_selectionFolder = state.SelectedPath;
		_restoring = false;
	}

	public event Action<FileManagerRefresh>? RefreshRequested;
	public event Action? GroupingChanged;
	public event Func<DirectoryNode, Task>? TreeLoaded;
	public event Func<IReadOnlyList<string>, string?, Task>? LocateRequested;
	public event Action<string, LocalChangeKind>? FolderChanged;
	public event Action<EntryRow?, EntryRow?>? RowReplaced;

	public bool IsDisposed { get; private set; }
	public bool HasKeptTree => _state.Tree is not null && ( _state.Stale & FileManagerRefresh.Tree ) == 0;
	public bool IsTreeLoaded { get; set; }
	public bool IsListLoaded { get; set; }
	public bool IsFiltered { get; set; } = true;

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
	[NotifyPropertyChangedFor( nameof( PreviewStatusText ) )]
	public partial bool IsPreviewLoading { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsPreviewVisible ) )]
	public partial EntryPreview? Preview { get; set; }

	public bool IsPreviewVisible => !IsPreviewLoading && Preview is not null;
	public string PreviewStatusText => IsPreviewLoading ? "Loading preview…" : "No preview";

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
		Entries.Clear();
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

	public RangeObservableCollection<EntryRow> Entries { get; } = [];
	public RangeObservableCollection<EntryRow> LocalChanges { get; } = [];
	public RangeObservableCollection<EntryRow> RemoteChanges { get; } = [];
	public RangeObservableCollection<HistoryRow> OHistories { get; } = [];
	public RangeObservableCollection<ParentRow> OParents { get; } = [];
	public RangeObservableCollection<ChildrenRow> OChildren { get; } = [];
	public RangeObservableCollection<PropertiesRow> OProperties { get; } = [];
	public RangeObservableCollection<VersionRow> OVersions { get; } = [];

	public Func<EntryRow, IComparable?>? ActiveGroupKey => IsGroupingEnabled ? _groupKey : null;
	public string ActiveGroupFieldName => _state.GroupField ?? nameof( EntryRow.Status );
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

	public IReadOnlyList<EntryRow> SelectedEntries
	{
		get;
		set
		{
			field = value;
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
	} = [];

	private string SelectedLocalDirectory => _files.ResolveLocalDirectory( LastSelectedNodePath );
	private int SelectedDirectoryId => SelectedDirectory?.DirectoryId ?? 0;
	private static int[] RemoteIds( IEnumerable<EntryRow> rows ) => RemoteIds( rows.Select( r => r.Id ) );

	public void Initialize() => _workflow.PreloadMetadata();

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
		CtsHelper.CancelAndDispose( ref _detailsCancellation );
		_localChangesGate.Dispose();
		GC.SuppressFinalize( this );
	}

	public void RefreshFromServer()
	{
		_state.MarkStale( FileManagerRefresh.All );
		RefreshRequested?.Invoke( FileManagerRefresh.Tree );
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
		IsTreeLoaded = false;
		IsTreeLoading = true;
		TreeLoadingStatus = !fromServer && HasKeptTree ? "Restoring folder tree…" : "Fetching folder structure from Odoo…";
		try
		{
			IProgress<string> progress = new UiThreadProgress<string>( status =>
			{
				if( !IsDisposed && IsTreeLoading )
					TreeLoadingStatus = status;
			} );
			DirectoryNode? root = !fromServer && HasKeptTree
				? _state.Tree
				: await _directoryTree.LoadAsync( progress, _pageCancellation.Token );
			if( root is not null && !ReferenceEquals( root, _state.Tree ) )
				_state.TreeRefreshed( root );
			if( root is not null && !IsDisposed && TreeLoaded is { } handler )
				await handler( root );
		}
		catch( OperationCanceledException ) { }
		catch( ObjectDisposedException ) { }
		finally
		{
			if( !IsDisposed )
			{
				IsTreeLoading = false;
				IsTreeLoaded = true;
			}
		}
	}

	public async Task LoadEntriesAsync( CancellationToken token = default )
	{
		if( IsDisposed )
			return;
		IsListLoaded = false;
		int generation = Interlocked.Increment( ref _entriesLoadGeneration );
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
			await AsyncHelper.WaitUntil( () => IsTreeLoaded, 100, -1, token );
			if( SelectedDirectory is not { } directory )
			{
				Entries.ClearIfNonEmpty();
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

				result = await _entryList.LoadAsync( directory.DirectoryId, directory.TreePath, includeDeleted, IsFiltered, progress, token );
				token.ThrowIfCancellationRequested();
				_state.RememberEntries( directory.TreePath, includeDeleted, result );
			}

			if( _state.Sort is var (field, descending) && EntryRowKeys.TryGetSortKey( field, out var sortKey ) )
				Entries.ReplaceSorted( result.Rows, sortKey, descending );
			else
				Entries.ReplaceAll( result.Rows );
			UpdateEntryLabels( result );
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
				if( generation == Volatile.Read( ref _entriesLoadGeneration ) )
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
			}
		}
	}

	private void UpdateEntryLabels( EntryListResult result )
	{
		int total = result.RemoteCount + result.LocalOnlyCount;
		int remoteOnly = Math.Max( 0, result.RemoteCount - result.LocalAndRemoteCount );
		List<EntryRow> localPending = [];
		List<EntryRow> remotePending = [];
		int modifiedCount = 0;

		for( int i = 0; i < result.Rows.Count; i++ )
		{
			EntryRow r = result.Rows[ i ];
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
		=> UiDispatcher.Post( () => _ = ApplyLocalChangesAsync( batch ) );

	private void OnLocalChangesLost()
		=> UiDispatcher.Post( () => { if( !IsDisposed ) Refresh( FileManagerRefresh.All ); } );

	public async Task ApplyLocalChangesAsync( IReadOnlyList<LocalFileChange> batch )
	{
		if( IsDisposed || batch.Count == 0 )
			return;
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

			List<LocalFileChange> here = [];
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
				bool isDirectory = change.IsDirectory || Directory.Exists( change.FullPath );
				if( !isDirectory && change.Kind == LocalChangeKind.Deleted && _state.Tree is not null )
				{
					itemSegments = PwaPaths.Segments( itemServerPath );
					isDirectory = itemSegments.Length > 0 && _state.Tree.FindBySegments( itemSegments ) is not null;
				}

				if( isDirectory )
				{
					itemSegments ??= PwaPaths.Segments( itemServerPath );
					if( itemSegments.Length > 0 && _state.Tree is { } tree )
					{
						if( change.Kind == LocalChangeKind.Created )
							treeChanged |= tree.EnsureLocalPath( itemSegments );
						else if( change.Kind == LocalChangeKind.Deleted )
						{
							treeChanged |= tree.MarkLocalDeleted( itemSegments );
							if( activeFolder is not null && PwaPaths.IsSameOrAncestorTreePath( itemServerPath, activeFolder ) )
								reloadCurrentEntries = true;
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
					_pendingChangesDuringLoad.Add( change );
				else
					MergeElsewhere( elsewhere, parentFolder, change.Kind );
			}

			if( treeChanged && _state.Tree is { } updatedTree && !IsDisposed )
			{
				if( LastSelectedNodePath is { } selectedPath )
				{
					string[] selectedSegments = PwaPaths.Segments( selectedPath );
					if( updatedTree.FindBySegments( selectedSegments ) is null )
					{
						int len = selectedSegments.Length - 1;
						while( len > 0 && updatedTree.FindBySegments( selectedSegments.AsSpan( 0, len ) ) is null )
							len--;
						LastSelectedNodePath = len == 0
							? updatedTree.Name
							: string.Join( PwaPaths.NameSeparator, [ updatedTree.Name, .. selectedSegments[ ..len ] ] );
						reloadCurrentEntries = true;
					}
				}
				if( TreeLoaded is { } treeHandler )
					await treeHandler( updatedTree );
			}

			foreach( var (folder, kind) in elsewhere )
				FolderChanged?.Invoke( folder, kind );

			if( reloadCurrentEntries || here.Count > MaxSingleRowUpdates )
			{
				Refresh( FileManagerRefresh.Entries );
				return;
			}

			bool changed = false;
			foreach( LocalFileChange change in here )
				changed |= await RefreshRowAsync( change.FullPath, listed! );
			if( changed && !IsDisposed )
			{
				if( _state.Sort is var (field, descending) && EntryRowKeys.TryGetSortKey( field, out var sortKey ) )
					Entries.Sort( sortKey, descending );
				KeepListedRows();
				IsEmptyDirectory = IsListLoaded && SelectedDirectory is not null && Entries.Count == 0;
				GroupingChanged?.Invoke();
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

	private static void MergeElsewhere( Dictionary<string, LocalChangeKind> map, string folder, LocalChangeKind kind )
		=> map[ folder ] = map.TryGetValue( folder, out LocalChangeKind prev ) && prev != kind ? LocalChangeKind.Updated : kind;

	private async Task<bool> RefreshRowAsync( string localPath, string listedFolder )
	{
		string name = Path.GetFileName( localPath );
		EntryRow? old = FindEntryByName( name, out _ );
		EntryRow? row = await _entryList.RefreshRowAsync( old, localPath, IsFiltered );
		if( IsDisposed || !IsListLoaded || !PwaPaths.SameTreePath( LastSelectedNodePath, listedFolder ) )
			return false;

		int index = old is null ? -1 : Entries.IndexOf( old );
		if( index < 0 )
			FindEntryByName( name, out index );
		EntryRow? replaced = index >= 0 ? Entries[ index ] : null;
		if( row is null )
		{
			if( index < 0 )
				return false;
			Entries.RemoveAt( index );
		}
		else if( index >= 0 )
		{
			Entries[ index ] = row;
		}
		else
		{
			Entries.Add( row );
		}
		RowReplaced?.Invoke( replaced, row );
		return true;
	}

	private EntryRow? FindEntryByName( string name, out int index )
	{
		for( int i = 0; i < Entries.Count; i++ )
		{
			if( string.Equals( Entries[ i ].Name, name, StringComparison.OrdinalIgnoreCase ) )
			{
				index = i;
				return Entries[ i ];
			}
		}
		index = -1;
		return null;
	}

	private void KeepListedRows()
	{
		List<EntryRow> rows = new( Entries.Count );
		int remoteCount = 0;
		int localOnlyCount = 0;
		int localAndRemoteCount = 0;
		for( int i = 0; i < Entries.Count; i++ )
		{
			EntryRow r = Entries[ i ];
			rows.Add( r );
			if( r.Id is > 0 )
				remoteCount++;
			if( r.ReprType == EntryReprType.Local )
				localOnlyCount++;
			else if( r.ReprType == EntryReprType.Both )
				localAndRemoteCount++;
		}
		EntryListResult result = new( rows, remoteCount, localOnlyCount, localAndRemoteCount );
		_state.RememberEntries( LastSelectedNodePath, ShowInactive, result );
		UpdateEntryLabels( result );
	}

	public void ClearDetails()
	{
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
		CancellationTokenSource linked;
		try
		{
			linked = CancellationTokenSource.CreateLinkedTokenSource( nextDetails.Token, _pageCancellation.Token );
		}
		catch( ObjectDisposedException )
		{
			return;
		}
		using CancellationTokenSource linkedGuard = linked;
		CancellationToken token = linked.Token;
		Interlocked.Increment( ref _previewVersionGeneration );

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
		await Task.WhenAll(
			LoadDetailTabAsync( entry, token ),
			LoadEntryPreviewAsync( entry, token ) );
	}

	private async Task LoadDetailTabAsync( EntryRow entry, CancellationToken token )
	{
		try
		{
			if( SelectedDetailTab is { } tab && _detailTabs.TryGetValue( tab, out var fill ) )
				await fill( entry, token );
		}
		catch( OperationCanceledException ) { }
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

	private async Task LoadEntryPreviewAsync( EntryRow entry, CancellationToken token )
	{
		try
		{
			byte[]? image = entry.LatestId is int versionId ? await _details.GetPreviewImageAsync( versionId ) : null;
			token.ThrowIfCancellationRequested();
			string? localPath = entry.LocalFile is { Exists: true } local ? local.FullName : null;
			if( image is not { Length: > 0 } && localPath is not null )
			{
				image = await _details.GetLocalPreviewImageAsync( localPath );
				token.ThrowIfCancellationRequested();
			}

			EntryPreview? preview = image is { Length: > 0 }
				? new EntryPreview( image, null )
				: entry.Status is FileStatus.Lo
					? new EntryPreview( null, localPath )
					: null;
			if( !token.IsCancellationRequested && !IsDisposed && preview is not null )
				Preview = preview;
		}
		catch( OperationCanceledException ) { }
		catch( Exception e )
		{
			HackLogger.Warn( "FileManager", $"Loading preview for '{entry.Name}' failed: {e.Message}", e );
		}
		finally
		{
			if( !token.IsCancellationRequested && !IsDisposed )
				IsPreviewLoading = false;
		}
	}

	public async Task PreviewVersionAsync( int? versionId )
	{
		if( IsDisposed || versionId is null or 0 )
			return;
		int generation = Interlocked.Increment( ref _previewVersionGeneration );
		try
		{
			if( await _details.GetPreviewImageAsync( versionId ) is { Length: > 0 } bytes
				&& !IsDisposed
				&& generation == Volatile.Read( ref _previewVersionGeneration ) )
			{
				Preview = new EntryPreview( bytes, null );
			}
		}
		catch( Exception e )
		{
			HackLogger.Warn( "FileManager", $"Loading preview for version {versionId} failed: {e.Message}", e );
		}
	}

	[RelayCommand]
	private void GroupBy( string? propertyName )
	{
		if( !EntryRowKeys.TryGetGroupKey( propertyName, out var key ) )
			return;
		_groupKey = key;
		_state.GroupField = propertyName;
		OnPropertyChanged( nameof( ActiveGroupFieldName ) );
		if( !IsGroupingEnabled )
			IsGroupingEnabled = true;
		else
			GroupingChanged?.Invoke();
	}

	public void SortEntries( string? fieldName, bool descending )
	{
		if( !EntryRowKeys.TryGetSortKey( fieldName, out var key ) )
			return;
		Entries.Sort( key, descending );
		_state.Sort = ( fieldName!, descending );
		GroupingChanged?.Invoke();
	}

	private async Task RunOnDirectoryAsync(
		string what,
		bool withSubdirectories,
		bool withDeleted,
		Func<IReadOnlyList<int>, Task> operation,
		string? completedMessage = null )
	{
		if( SelectedDirectory is not { } directory )
		{
			await Dialogs.ShowMessageAsync( "Current directory doesn't exist remotely." );
			return;
		}
		await RunOperationAsync( async () =>
		{
			Reporter.Log( StatusMessage.PROCESSING, $"{what} all entries within directory ({directory.Name}, id: {SelectedDirectoryId})" );
			using( _localFiles.Suspend() )
			{
				IReadOnlyList<int> entryIds = await _workflow.GetDirectoryEntryIdsAsync( SelectedDirectoryId, withSubdirectories, withDeleted );
				if( entryIds.Count == 0 )
				{
					Reporter.Log( StatusMessage.INFO, $"No remote entries found in directory ({directory.Name}, id: {SelectedDirectoryId})." );
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
		if( SelectedDirectory is null )
		{
			await Dialogs.ShowMessageAsync( "Current directory doesn't exist remotely." );
			return;
		}
		await RunOperationAsync( async () =>
		{
			using( _localFiles.Suspend() )
				await _workflow.RestoreDeletedInDirectoryAsync( SelectedDirectoryId, withSubdirectories );
			if( !IsDisposed )
				Refresh( FileManagerRefresh.All );
		}, "Completed!" );
	}

	[RelayCommand]
	private async Task CommitDirectoryAsync()
	{
		if( SelectedDirectory?.IsRemoteOnly is true )
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

	[RelayCommand] private void OpenDirectory() => _files.OpenFolder( SelectedLocalDirectory );

	[RelayCommand]
	private async Task LocalDeleteDirectoryAsync()
	{
		string directory = SelectedLocalDirectory;
		if( await Task.Run( () => _files.CountFiles( directory ) ) is not int count )
			return;
		if( await Dialogs.ConfirmAsync( $"Are you sure you want to delete this directory and ({count}) files inside?", "Delete Directory", MessageBoxIcon.Warning ) )
		{
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
	[RelayCommand] private void OpenSelectedFolders() => _files.OpenContainingFolders( SelectedEntries );

	private async Task OpenSelectedAsync( EntryOpenMode mode )
	{
		IReadOnlyList<string> errors = await _files.OpenEntriesAsync( SelectedEntries, mode );
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
		int? targetVersionId = SelectedHistory?.Version is int histId and > 0
			? histId
			: entry?.LatestId is int latestId and > 0
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
		try { bytes = await File.ReadAllBytesAsync( filePath ); }
		catch( Exception ex )
		{
			await Dialogs.ShowMessageAsync( $"Could not read the selected image: {ex.Message}", "Upload Preview Image", MessageBoxButtons.OK, MessageBoxIcon.Error );
			return;
		}

		if( PreviewImageValidator.Validate( bytes ) is { } validationError )
		{
			await Dialogs.ShowMessageAsync( validationError, "Invalid Image", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		var result = await _details.UploadPreviewImageAsync( versionId, bytes );
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
		if( await _files.GetVersionAsync( SelectedHistory?.Version ?? 0 ) is { } version )
			await act( version );
	}

	[RelayCommand]
	private Task DownloadHistoryAsync() => WithHistoryVersionAsync( async version =>
	{
		if( !_files.HasConflictingLocalCopy( version ) )
		{
			await _files.DownloadVersionAsync( version, toTemp: false );
			return;
		}
		DialogResult answer = await Dialogs.ShowMessageAsync(
			"File exists as a different version.\nRetry:\tDownload to Temporary Folder\nIgnore:\tOverwrite current version\nAbort:\tCancel",
			"File Version Conflict", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning );
		if( answer is DialogResult.Retry or DialogResult.Ignore )
			await _files.DownloadVersionAsync( version, toTemp: answer is DialogResult.Retry );
	} );

	[RelayCommand] private Task DownloadHistoryToTempAsync() => WithHistoryVersionAsync( v => _files.DownloadVersionAsync( v, toTemp: true ) );
	[RelayCommand] private Task DownloadHistoryOverwriteAsync() => WithHistoryVersionAsync( v => _files.DownloadVersionAsync( v, toTemp: false ) );
	[RelayCommand] private Task OpenHistoryFromTempAsync() => WithHistoryVersionAsync( v => OpenHistoryAsync( v, toTemp: true ) );
	[RelayCommand] private Task OpenHistoryOverwriteAsync() => WithHistoryVersionAsync( v => OpenHistoryAsync( v, toTemp: false ) );
	[RelayCommand] private Task MoveHistoryToTempAsync() => WithHistoryVersionAsync( v => MoveHistoryAsync( v, toTemp: true ) );
	[RelayCommand] private Task MoveHistoryToWorkingAsync() => WithHistoryVersionAsync( v => MoveHistoryAsync( v, toTemp: false ) );

	private async Task OpenHistoryAsync( HpVersion version, bool toTemp )
	{
		string folder = await _files.DownloadVersionAsync( version, toTemp );
		_files.OpenFile( Path.Combine( folder, version.name ?? "" ) );
	}

	private async Task MoveHistoryAsync( HpVersion version, bool toTemp )
	{
		VersionMovePlan plan = _files.PlanMove( version, toTemp );
		string target = toTemp ? "temporary" : "current";
		string prefix = plan.From.Exists ? "" : $"File doesn't exist in {plan.From.DirectoryName}.\n";
		string verb = plan.From.Exists ? "move" : "download";
		string message = plan.To.Exists
			? $"{prefix}Would you like to {verb} this version to {target} and overwrite that version?"
			: $"{prefix}Would you like to {verb} this version to {target}?";
		string caption = ( plan.From.Exists ? "Move" : "Download" ) + ( plan.To.Exists ? " & Overwrite" : "" );

		if( !await Dialogs.ConfirmAsync( message, caption, plan.To.Exists ? MessageBoxIcon.Warning : MessageBoxIcon.Question ) )
			return;
		using( _localFiles.Suspend() )
			await _files.ExecuteMoveAsync( version, plan );
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
