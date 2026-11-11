using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HackPDM.Core;
using HackPDM.Core.General;
using HackPDM.Core.Hack;
using HackPDM.Domain.Representation;
using HackPDM.Infrastructure.Odoo.Models;
using HackPDM.Mvvm.Models.Common;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Types;


using EntryRow = HackPDM.Mvvm.Models.Hack.EntryRow;

namespace HackPDM.Mvvm.ViewModels.Hack;

/// <summary>What the view should reload after a view-model operation.</summary>
[Flags]
public enum FileManagerRefresh
{
	None = 0,
	Entries = 1,
	Tree = 2,
	All = Entries | Tree,
}

/// <summary>
/// State and commands for the Hack file manager page. Holds no references to controls:
/// the view pushes selection in, invokes commands, and reacts to <see cref="RefreshRequested"/>
/// and <see cref="GroupingChanged"/>.
/// </summary>
public partial class HackFileManagerViewModel : OperationViewModel
{
	private readonly IPdmWorkflowService _workflow;
	private readonly IEntryFileService _files;
	private readonly IEntryDetailsService _details;
	private readonly IDirectoryTreeService _directoryTree;
	private readonly IEntryListService _entryList;

	private CancellationTokenSource _detailsCancellation = new();

	public HackFileManagerViewModel(
		IPdmWorkflowService workflow,
		IEntryFileService files,
		IEntryDetailsService details,
		IDialogService dialogs,
		IOperationReporter reporter,
		IDirectoryTreeService directoryTree,
		IEntryListService entryList ) : base( dialogs, reporter )
	{
		_workflow = workflow;
		_files = files;
		_details = details;
		_directoryTree = directoryTree;
		_entryList = entryList;
		_detailTabs = new()
		{
			[ StorageBox.HISTORY_TAB ] = async entry => OHistories.ReplaceAll( await _details.GetHistoryAsync( entry ) ),
			[ StorageBox.PARENT_TAB ] = async entry => OParents.ReplaceAll( await _details.GetParentsAsync( entry ) ),
			[ StorageBox.CHILD_TAB ] = async entry => OChildren.ReplaceAll( await _details.GetChildrenAsync( entry ) ),
			[ StorageBox.PROPERTIES_TAB ] = async entry => OProperties.ReplaceAll( await _details.GetPropertiesAsync( entry ) ),
			[ StorageBox.INFO_TAB ] = async entry => OVersions.ReplaceAll( await _details.GetVersionInfoAsync( entry ) ),
		};
	}

	/// <summary>How each detail tab is filled for an entry.</summary>
	private readonly Dictionary<string, Func<EntryRow, Task>> _detailTabs;

	#region Events for the view

	/// <summary>Raised when the tree and/or entry list must be rebuilt from the server.</summary>
	public event Action<FileManagerRefresh>? RefreshRequested;
	/// <summary>Raised after the entry grouping or sort order changed, so the view can rebind the grid.</summary>
	public event Action? GroupingChanged;
	/// <summary>Raised with the freshly loaded directory tree; the view turns it into tree nodes.</summary>
	public event Func<DirectoryNode, Task>? TreeLoaded;
	/// <summary>Raised when another page (search) asks to show a file: tree segments below root, then the file name.</summary>
	public event Func<IReadOnlyList<string>, string?, Task>? LocateRequested;

	private void Refresh( FileManagerRefresh what ) => RefreshRequested?.Invoke( what );

	#endregion
	#region Load state (read by the view, SearchOdoo and the smoke tests)

	public bool IsLoaded { get; set; }
	public bool IsTreeLoaded { get; set; }
	public bool IsListLoaded { get; set; }
	public bool IsFiltered { get; set; } = true;
	public bool HackLoaded { get; set; }


	/// <summary>True while the directory tree is being fetched; drives the loading ring.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( IsPreviewVisible ) )]
	public partial bool IsTreeLoading { get; set; }

	/// <summary>The preview image shares its slot with the loading ring.</summary>
	public bool IsPreviewVisible => !IsTreeLoading;

	#endregion
	#region Bindable state

	[ObservableProperty] public partial string EntriesLabel { get; set; } = "Entries: None";
	[ObservableProperty] public partial string EntriesLabelLocal { get; set; } = "LO: None";
	[ObservableProperty] public partial string EntriesLabelRemote { get; set; } = "RO: None";

	/// <summary>Preview of the selected entry or version; the view turns it into an image.</summary>
	[ObservableProperty] public partial EntryPreview? Preview { get; set; }

	/// <summary>Show deleted ("inactive") entries in the list.</summary>
	[ObservableProperty] public partial bool ShowInactive { get; set; }
	partial void OnShowInactiveChanged( bool value )
	{
		Entries.Clear();
		Refresh( FileManagerRefresh.Entries );
	}

	/// <summary>Group the entry list (by status, or by the field picked in "Group By").</summary>
	[ObservableProperty] public partial bool IsGroupingEnabled { get; set; }
	partial void OnIsGroupingEnabledChanged( bool value ) => GroupingChanged?.Invoke();

	/// <summary>Rows of the selected directory.</summary>
	public RangeObservableCollection<EntryRow> Entries { get; } = new();

	private static readonly Func<EntryRow, IComparable?> GroupByStatus = row => row.Status;
	private Func<EntryRow, IComparable?> _groupKey = GroupByStatus;

	/// <summary>Key the entry list is grouped by, or null when grouping is off.</summary>
	public Func<EntryRow, IComparable?>? ActiveGroupKey => IsGroupingEnabled ? _groupKey : null;

	public RangeObservableCollection<HistoryRow> OHistories { get; } = [];
	public RangeObservableCollection<ParentRow> OParents { get; } = [];
	public RangeObservableCollection<ChildrenRow> OChildren { get; } = [];
	public RangeObservableCollection<PropertiesRow> OProperties { get; } = [];
	public RangeObservableCollection<VersionRow> OVersions { get; } = [];

	#endregion
	#region Selection (pushed in by the view)

	/// <summary>The directory selected in the tree.</summary>
	public DirectoryRef? SelectedDirectory
	{
		get;
		set
		{
			field = value;
			LastSelectedNodePath = value?.TreePath;
		}
	}

	/// <summary>Tree path ("root\a\b") of the selected directory, used to restore selection after a reload.</summary>
	public string? LastSelectedNodePath { get; set; }

	/// <summary>Rows selected in the entry list.</summary>
	public IReadOnlyList<EntryRow> SelectedEntries { get; set; } = [];

	/// <summary>Version selected in the History tab.</summary>
	public HistoryRow? SelectedHistory { get; set; }

	/// <summary>Name of the visible detail tab (one of the StorageBox *_TAB constants).</summary>
	public string? SelectedDetailTab { get; set; }

	private string SelectedLocalDirectory => _files.ResolveLocalDirectory( LastSelectedNodePath );
	private int SelectedDirectoryId => SelectedDirectory?.DirectoryId ?? 0;

	private static int[] RemoteIds( IEnumerable<EntryRow> rows ) => RemoteIds( rows.Select( r => r.Id ) );

	#endregion
	/// <summary>Loads the Odoo metadata the page needs before the tree is built.</summary>
	public void Initialize() => _workflow.PreloadMetadata();

	#region Loading the tree and the entry list

	/// <summary>Fetches the directory tree and raises <see cref="TreeLoaded"/>.</summary>
	public async Task LoadDirectoryTreeAsync()
	{
		IsTreeLoaded = false;
		IsTreeLoading = true;
		try
		{
			if( await _directoryTree.LoadAsync() is { } root && TreeLoaded is { } handler )
				await handler( root );
		}
		finally
		{
			IsTreeLoading = false;
			IsTreeLoaded = true;
		}
	}

	/// <summary>Loads the rows of <see cref="SelectedDirectory"/> into <see cref="Entries"/>.</summary>
	public async Task LoadEntriesAsync( CancellationToken token = default )
	{
		IsListLoaded = false;
		try
		{
			await AsyncHelper.WaitUntil( () => IsTreeLoaded, 100, -1, token );
			Entries.Clear();
			if( SelectedDirectory is not { } directory )
				return;

			EntryListResult result = await _entryList.LoadAsync( directory.DirectoryId, directory.TreePath, ShowInactive, IsFiltered, token );
			token.ThrowIfCancellationRequested();

			UpdateEntryLabels( result );
			Entries.ReplaceAll( result.Rows );
			_groupKey = GroupByStatus;
			GroupingChanged?.Invoke();
		}
		catch( OperationCanceledException ) { }
		catch( Exception e )
		{
			// stderr too: Debug output is invisible outside a debugger (e.g. the smoke-test log)
			Console.Error.WriteLine( $"loading entries failed: {e}" );
		}
		finally
		{
			// RestartEntries, SearchOdoo and the smoke tests wait on this flag
			if( !token.IsCancellationRequested )
				IsListLoaded = true;
		}
	}

	private void UpdateEntryLabels( EntryListResult result )
	{
		static string Count( int count ) => count == 0 ? "None" : count.ToString();
		EntriesLabel = $"Entries: {Count( result.RemoteCount + result.LocalOnlyCount )}";
		EntriesLabelLocal = $"LO: {Count( result.LocalOnlyCount )}";
		EntriesLabelRemote = $"RO: {Count( result.RemoteCount - result.LocalAndRemoteCount )}";
	}

	/// <summary>Asks the view to select the folder at <paramref name="segmentsBelowRoot"/> and the file in it.</summary>
	public Task RequestLocateAsync( IReadOnlyList<string> segmentsBelowRoot, string? fileName )
		=> LocateRequested?.Invoke( segmentsBelowRoot, fileName ) ?? Task.CompletedTask;

	#endregion

	#region Entry details and preview

	public void ClearDetails()
	{
		OHistories.Clear();
		OParents.Clear();
		OChildren.Clear();
		OProperties.Clear();
		OVersions.Clear();
	}

	/// <summary>Loads the visible detail tab and the preview for <paramref name="entry"/>; a newer call cancels an older one.</summary>
	public async Task ShowEntryDetailsAsync( EntryRow? entry )
	{
		await _detailsCancellation.CancelAsync();
		_detailsCancellation = new();
		CancellationToken token = _detailsCancellation.Token;

		ClearDetails();
		Preview = null;
		if( entry is null )
			return;

		try
		{
			await LoadDetailTabAsync( entry, token );

			byte[]? image = entry.LatestId is int versionId ? await _details.GetPreviewImageAsync( versionId ) : null;
			// a local-only file, or a version committed without a preview: use the local file's own preview
			string? localPath = entry.LocalFile is { Exists: true } local ? local.FullName : null;
			if( image is not { Length: > 0 } && localPath is not null )
				image = await _details.GetLocalPreviewImageAsync( localPath );

			EntryPreview? preview = image is { Length: > 0 }
				? new EntryPreview( image, null )
				: entry.Status is FileStatus.Lo
					? new EntryPreview( null, localPath )
					: null;

			if( !token.IsCancellationRequested && preview is not null )
				Preview = preview;
		}
		catch( OperationCanceledException ) { }
		catch( Exception e )
		{
			System.Diagnostics.Debug.WriteLine( $"loading {SelectedDetailTab} for {entry.Name} failed: {e}" );
		}
	}

	private async Task LoadDetailTabAsync( EntryRow entry, CancellationToken token )
	{
		if( SelectedDetailTab is { } tab && _detailTabs.TryGetValue( tab, out var fill ) )
			await fill( entry );
		token.ThrowIfCancellationRequested();
	}

	/// <summary>Shows the preview image of a specific version (history/parent/child row selection).</summary>
	public async Task PreviewVersionAsync( int? versionId )
	{
		if( versionId is null or 0 )
			return;
		if( await _details.GetPreviewImageAsync( versionId ) is { Length: > 0 } bytes )
			Preview = new EntryPreview( bytes, null );
	}

	#endregion
	#region Entry list: grouping and sorting

	/// <summary>Regroups the entry list by one of <see cref="EntryRowKeys.Names"/>.</summary>
	[RelayCommand]
	private void GroupBy( string? propertyName )
	{
		if( !EntryRowKeys.TryGetGroupKey( propertyName, out var key ) )
			return;
		_groupKey = key;
		GroupingChanged?.Invoke();
	}

	public void SortEntries( string? fieldName, bool descending )
	{
		if( !EntryRowKeys.TryGetSortKey( fieldName, out var key ) )
			return;

		Entries.Sort( key, descending );
		GroupingChanged?.Invoke();
	}

	#endregion
	#region Directory (tree) commands

	private async Task<IReadOnlyList<int>> SelectedDirectoryEntryIdsAsync( bool withSubdirectories, bool withDeleted = false )
		=> await _workflow.GetDirectoryEntryIdsAsync( SelectedDirectoryId, withSubdirectories, withDeleted );

	/// <summary>Runs <paramref name="operation"/> on the selected directory's entries; tells the user when no remote directory is selected.</summary>
	private async Task RunOnDirectoryAsync( string what, bool withSubdirectories, bool withDeleted, Func<IReadOnlyList<int>, Task> operation, string? completedMessage = null )
	{
		if( SelectedDirectory is not { } directory )
		{
			await Dialogs.ShowMessageAsync( "current directory doesn't exist remotely" );
			return;
		}
		await RunOperationAsync( async () =>
		{
			Reporter.Log( StatusMessage.PROCESSING, $"{what} all entries within directory ({directory.Name}, id: {SelectedDirectoryId})" );
			await operation( await SelectedDirectoryEntryIdsAsync( withSubdirectories, withDeleted ) );
		}, completedMessage );
	}

	[RelayCommand]
	private Task GetLatestDirectoryAsync() => RunOnDirectoryAsync( "Getting the latest of", true, ShowInactive, _workflow.GetLatestAsync );

	[RelayCommand]
	private Task GetLatestDirectoryTopAsync() => RunOnDirectoryAsync( "Getting the latest of", false, ShowInactive, _workflow.GetLatestAsync );

	[RelayCommand]
	private Task CheckoutDirectoryAsync() => RunOnDirectoryAsync( "Checking out", true, false, _workflow.CheckoutLatestAsync );

	[RelayCommand]
	private Task UndoCheckoutDirectoryAsync() => RunOnDirectoryAsync( "Undoing the check out of", true, false, _workflow.UnCheckoutAsync );

	[RelayCommand]
	private async Task LogicalDeleteDirectoryAsync()
	{
		await RunOnDirectoryAsync( "Deleting", true, false, _workflow.LogicalDeleteAsync, "Completed!" );
		Refresh( FileManagerRefresh.All );
	}

	[RelayCommand]
	private async Task RestoreDirectoryAsync()
	{
		await RunOperationAsync( () => _workflow.RestoreDeletedInDirectoryAsync( SelectedDirectoryId ), "Completed!" );
		Refresh( FileManagerRefresh.All );
	}

	[RelayCommand]
	private Task RestoreDirectoryRecursiveAsync() => Dialogs.ShowMessageAsync( "Not Implemented Yet" );

	[RelayCommand]
	private async Task CommitDirectoryAsync()
	{
		if( SelectedDirectory?.IsRemoteOnly is true )
			return;
		await RunOperationAsync( async () =>
		{
			if( await _workflow.CollectFolderCommitFilesAsync( SelectedLocalDirectory ) is { Count: > 0 } files )
				await _workflow.CommitAsync( files );
		} );
	}

	[RelayCommand]
	private void OpenDirectory() => _files.OpenFolder( SelectedLocalDirectory );

	[RelayCommand]
	private async Task LocalDeleteDirectoryAsync()
	{
		string directory = SelectedLocalDirectory;
		if( _files.CountFiles( directory ) is not int count )
			return;

		if( await Dialogs.ConfirmAsync( $"Are you sure you want to delete this directory and ({count}) files inside?", "Delete Directory", MessageBoxIcon.Warning ) )
			_files.DeleteDirectory( directory );
	}

	#endregion
	#region Entry list commands

	[RelayCommand]
	private Task GetLatestSelectedAsync() => RunOnIdsAsync( RemoteIds( SelectedEntries ), _workflow.GetLatestAsync );

	[RelayCommand]
	private Task CheckoutSelectedAsync()
		=> RunOnIdsAsync( RemoteIds( SelectedEntries.Where( e => e.Checkout is null ) ), _workflow.CheckoutLatestAsync );

	[RelayCommand]
	private Task UndoCheckoutSelectedAsync()
		=> RunOnIdsAsync( RemoteIds( SelectedEntries.Where( e => e.Checkout is not null ) ), _workflow.UnCheckoutAsync );

	[RelayCommand]
	private async Task LogicalDeleteSelectedAsync()
	{
		await RunOnIdsAsync( RemoteIds( SelectedEntries ), _workflow.LogicalDeleteAsync, "Completed!" );
		Refresh( FileManagerRefresh.All );
	}

	[RelayCommand]
	private Task CommitSelectedAsync() => RunOperationAsync( async () =>
	{
		HashSet<HackFile> files = await _workflow.CollectCommitFilesAsync( SelectedEntries );
		if( files.Count > 0 )
			await _workflow.CommitAsync( files );
	} );

	[RelayCommand]
	private Task OpenSelectedAsync() => OpenSelectedAsync( EntryOpenMode.Open );

	[RelayCommand]
	private Task OpenSelectedLatestRemoteAsync() => OpenSelectedAsync( EntryOpenMode.LatestRemote );

	[RelayCommand]
	private Task OpenSelectedLatestLocalAsync() => OpenSelectedAsync( EntryOpenMode.LatestLocal );

	private async Task OpenSelectedAsync( EntryOpenMode mode )
	{
		IReadOnlyList<string> errors = await _files.OpenEntriesAsync( SelectedEntries, mode );
		if( errors.Count > 0 )
			await Dialogs.ShowMessageAsync( string.Join( Environment.NewLine, errors ) );
	}

	[RelayCommand]
	private void OpenSelectedFolders() => _files.OpenContainingFolders( SelectedEntries );

	[RelayCommand]
	private async Task LocalDeleteSelectedAsync()
	{
		List<FileInfo> files = _files.FindLocalFiles( SelectedLocalDirectory, SelectedEntries );
		if( files.Count == 0 )
			return;

		string message = files.Count > 10
			? $"Are you sure you want to delete ({files.Count}) files?"
			: $"Are you sure you want to delete these files?\nfiles:\n{string.Join( Environment.NewLine, files.Select( f => f.FullName ) )}";

		if( await Dialogs.ConfirmAsync( message, "Delete Files", MessageBoxIcon.Warning ) )
			_files.DeleteFiles( files );
		Refresh( FileManagerRefresh.Entries );
	}

	[RelayCommand]
	private async Task PermanentDeleteSelectedAsync()
	{
#if DEBUG
		IReadOnlyList<int> ids = RemoteIds( SelectedEntries );
		if( ids.Count == 0 )
		{
			await Dialogs.ShowMessageAsync( "No entries to delete" );
			return;
		}

		bool confirmed = await Dialogs.ConfirmAsync(
			$"Are you sure you want to permanently delete {ids.Count} entries from the database?\n" +
			"This will also permanently delete all associative versions, version properties, and version relationships",
			"Delete Entries and Other Records?", MessageBoxIcon.Warning );
		if( !confirmed )
			return;

		bool deleted = false;
		await RunOperationAsync( async () => deleted = await _workflow.PermanentDeleteAsync( ids ) );
		if( !deleted )
		{
			await Dialogs.ShowMessageAsync( "Was unable to delete entries", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error );
			return;
		}
		await Dialogs.ShowMessageAsync( "Completed!" );
		Refresh( FileManagerRefresh.All );
#else
		await Dialogs.ShowMessageAsync( "Permanent delete is only available in debug builds" );
#endif
	}

	[RelayCommand]
	private async Task UploadIconAsync()
	{
		if( SelectedEntries is not [ { FullName: not null } entry ] || !FileStatusPolicy.CanUploadIcon( entry.Status ) )
			return;
		await Dialogs.ShowMessageAsync( "Uploading icons is not implemented yet" );
	}

	#endregion
	#region History commands

	/// <summary>Runs <paramref name="act"/> on the version selected in the History tab, when there is one.</summary>
	private async Task WithHistoryVersionAsync( Func<HpVersion, Task> act )
	{
		if( await _files.GetVersionAsync( SelectedHistory?.Version ?? 0 ) is { } version )
			await act( version );
	}

	/// <summary>Downloads the selected history version, asking where to put it if a different local copy exists.</summary>
	[RelayCommand]
	private Task DownloadHistoryAsync() => WithHistoryVersionAsync( async version =>
	{
		if( !_files.HasConflictingLocalCopy( version ) )
		{
			await _files.DownloadVersionAsync( version, toTemp: false );
			return;
		}

		DialogResult answer = await Dialogs.ShowMessageAsync(
			"File exists as a different version.\n" +
			"Retry:\tDownload in the Temporary Folder\n" +
			"Ignore:\tOverwrite the current version\n" +
			"Abort:\tCancel download",
			"File Version Conflict", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning );

		if( answer is DialogResult.Retry or DialogResult.Ignore )
			await _files.DownloadVersionAsync( version, toTemp: answer is DialogResult.Retry );
	} );

	[RelayCommand] private Task DownloadHistoryToTempAsync() => WithHistoryVersionAsync( version => _files.DownloadVersionAsync( version, toTemp: true ) );
	[RelayCommand] private Task DownloadHistoryOverwriteAsync() => WithHistoryVersionAsync( version => _files.DownloadVersionAsync( version, toTemp: false ) );
	[RelayCommand] private Task OpenHistoryFromTempAsync() => WithHistoryVersionAsync( version => OpenHistoryAsync( version, toTemp: true ) );
	[RelayCommand] private Task OpenHistoryOverwriteAsync() => WithHistoryVersionAsync( version => OpenHistoryAsync( version, toTemp: false ) );
	[RelayCommand] private Task MoveHistoryToTempAsync() => WithHistoryVersionAsync( version => MoveHistoryAsync( version, toTemp: true ) );
	[RelayCommand] private Task MoveHistoryToWorkingAsync() => WithHistoryVersionAsync( version => MoveHistoryAsync( version, toTemp: false ) );

	private async Task OpenHistoryAsync( HpVersion version, bool toTemp )
	{
		string folder = await _files.DownloadVersionAsync( version, toTemp );
		_files.OpenFile( Path.Combine( folder, version.name ?? "" ) );
	}

	private async Task MoveHistoryAsync( HpVersion version, bool toTemp )
	{
		VersionMovePlan plan = _files.PlanMove( version, toTemp );
		string target = toTemp ? "temporary" : "current";
		string prefix = plan.From.Exists ? "" : $"file doesn't exist in {plan.From.DirectoryName}.\n";
		string verb = plan.From.Exists ? "move" : "download";
		string message = plan.To.Exists
			? $"{prefix}Would you like to {verb} this version to {target} and overwrite that version?"
			: $"{prefix}Would you like to {verb} this version to {target}?";
		string caption = ( plan.From.Exists ? "Move" : "Download" ) + ( plan.To.Exists ? " & Overwrite" : "" );

		if( await Dialogs.ConfirmAsync( message, caption, plan.To.Exists ? MessageBoxIcon.Warning : MessageBoxIcon.Question ) )
			await _files.ExecuteMoveAsync( version, plan );
		Refresh( FileManagerRefresh.Entries );
	}

	/// <summary>Downloads the selected history version together with its dependencies.</summary>
	[RelayCommand]
	private async Task DownloadHistoryWithDependenciesAsync()
	{
		if( SelectedHistory is not { Version: not 0 } row )
			return;
		if( await _workflow.PlanVersionDownloadAsync( row.Version ) is not { } plan )
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
			.AppendLine( $"\tAttachID = {v.attachment_id}" )
			.AppendLine( $"\tMod Date = {v.file_modify_stamp}" )
			.AppendLine( $"\tNode ID = {v.node_id}" )
			.AppendLine( $"\tDir ID = {v.dir_id}" )
			.Append( $"\tWin DL Path = {v.WinPathway}" );

		string ids = string.Join( ", ", plan.VersionIds );
		if( await Dialogs.ShowMessageAsync( $"{intro}\n this will download version ids: {ids}\n{details}", "Version Download", MessageBoxButtons.YesNoCancel ) != DialogResult.Yes )
			return;

		IReadOnlyList<int> failed = await _workflow.DownloadVersionsAsync( plan.VersionIds );
		if( failed.Count > 0 )
			await Dialogs.ShowMessageAsync( $"failed to download version ids: {string.Join( ", ", failed )}" );
	}

	#endregion
}
