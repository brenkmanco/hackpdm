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
public partial class HackFileManagerViewModel : ObservableObject
{
	private readonly IPdmWorkflowService _workflow;
	private readonly IEntryFileService _files;
	private readonly IEntryDetailsService _details;
	private readonly IDialogService _dialogs;
	private readonly IOperationReporter _reporter;
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
		IEntryListService entryList )
	{
		_workflow = workflow;
		_files = files;
		_details = details;
		_dialogs = dialogs;
		_reporter = reporter;
		_directoryTree = directoryTree;
		_entryList = entryList;
	}

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

	private static readonly Func<EntryRow, object> GroupByStatus = row => row.Status;
	private Func<EntryRow, object> _groupKey = GroupByStatus;

	/// <summary>Key the entry list is grouped by, or null when grouping is off.</summary>
	public Func<EntryRow, object>? ActiveGroupKey => IsGroupingEnabled ? _groupKey : null;

	public ObservableCollection<HistoryRow> OHistories { get; } = [];
	public ObservableCollection<ParentRow> OParents { get; } = [];
	public ObservableCollection<ChildrenRow> OChildren { get; } = [];
	public ObservableCollection<PropertiesRow> OProperties { get; } = [];
	public ObservableCollection<VersionRow> OVersions { get; } = [];

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

	private static ArrayList RemoteIds( IEnumerable<EntryRow> rows )
		=> [ .. rows.Where( r => r.Id is not ( null or 0 ) ).Select( r => (object)r.Id!.Value ) ];

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
		switch( SelectedDetailTab )
		{
			case StorageBox.HISTORY_TAB:
				Fill( OHistories, await _details.GetHistoryAsync( entry ), token );
				break;
			case StorageBox.PARENT_TAB:
				Fill( OParents, await _details.GetParentsAsync( entry ), token );
				break;
			case StorageBox.CHILD_TAB:
				Fill( OChildren, await _details.GetChildrenAsync( entry ), token );
				break;
			case StorageBox.PROPERTIES_TAB:
				Fill( OProperties, await _details.GetPropertiesAsync( entry ), token );
				break;
			case StorageBox.INFO_TAB:
				Fill( OVersions, await _details.GetVersionInfoAsync( entry ), token );
				break;
		}
	}

	private static void Fill<T>( ObservableCollection<T> target, IEnumerable<T> rows, CancellationToken token )
	{
		token.ThrowIfCancellationRequested();
		target.Clear();
		foreach( T row in rows )
			target.Add( row );
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

	/// <summary>Regroups the entry list by the named <see cref="EntryRow"/> property.</summary>
	[RelayCommand]
	private void GroupBy( string? propertyName )
	{
		if( string.IsNullOrEmpty( propertyName ) )
			return;
		var property = typeof( EntryRow ).GetProperty( propertyName );
		if( property is null )
			return;

		_groupKey = entry => property.GetValue( entry ) switch
		{
			DateTime date => date.ToShortDateString(),
			var value => value?.ToString() ?? string.Empty,
		};
		GroupingChanged?.Invoke();
	}

	public void SortEntries( string? fieldName, bool descending )
	{
		if( !EntryRowSorters.TryGet( fieldName, out var key ) )
			return;

		Entries.Sort( key, descending );
		GroupingChanged?.Invoke();
	}

	#endregion
	#region Directory (tree) commands

	private async Task<ArrayList?> SelectedDirectoryEntryIdsAsync( bool withSubdirectories, bool withDeleted = false )
		=> await _workflow.GetDirectoryEntryIdsAsync( SelectedDirectoryId, withSubdirectories, withDeleted );

	private async Task GetLatestForDirectoryAsync( bool withSubdirectories )
	{
		if( SelectedDirectory is null )
		{
			await _dialogs.ShowMessageAsync( "current directory doesn't exist remotely" );
			return;
		}

		_reporter.ShowWindow();
		_reporter.Log( StatusMessage.PROCESSING,
			$"Retrieving all entries and their associated dependencies within directory ({SelectedDirectory.Name}, id: {SelectedDirectoryId})" );

		ArrayList? ids = await SelectedDirectoryEntryIdsAsync( withSubdirectories, ShowInactive );
		await _workflow.GetLatestAsync( ids ?? [] );
	}

	[RelayCommand]
	private Task GetLatestDirectoryAsync() => GetLatestForDirectoryAsync( withSubdirectories: true );

	[RelayCommand]
	private Task GetLatestDirectoryTopAsync() => GetLatestForDirectoryAsync( withSubdirectories: false );

	[RelayCommand]
	private async Task CheckoutDirectoryAsync()
	{
		await GetLatestForDirectoryAsync( withSubdirectories: true );
		ArrayList? ids = await SelectedDirectoryEntryIdsAsync( withSubdirectories: true );
		await _workflow.CheckoutAsync( ids ?? [] );
	}

	[RelayCommand]
	private async Task UndoCheckoutDirectoryAsync()
	{
		_reporter.ShowWindow();
		ArrayList? ids = await SelectedDirectoryEntryIdsAsync( withSubdirectories: true );
		await _workflow.UnCheckoutAsync( ids ?? [] );
	}

	[RelayCommand]
	private async Task CommitDirectoryAsync()
	{
		if( SelectedDirectory?.IsRemoteOnly is true )
			return;

		_reporter.ShowWindow();
		List<HackFile>? files = _workflow.CollectFolderCommitFiles( SelectedLocalDirectory );
		if( files is { Count: > 0 } )
			await _workflow.CommitAsync( files );
	}

	[RelayCommand]
	private void OpenDirectory() => _files.OpenFolder( SelectedLocalDirectory );

	[RelayCommand]
	private async Task LocalDeleteDirectoryAsync()
	{
		string directory = SelectedLocalDirectory;
		if( _files.CountFiles( directory ) is not int count )
			return;

		if( await _dialogs.ConfirmAsync( $"Are you sure you want to delete this directory and ({count}) files inside?", "Delete Directory", MessageBoxIcon.Warning ) )
			_files.DeleteDirectory( directory );
	}

	[RelayCommand]
	private async Task LogicalDeleteDirectoryAsync()
	{
		_reporter.ShowWindow();
		ArrayList? ids = await SelectedDirectoryEntryIdsAsync( withSubdirectories: true );
		await _workflow.LogicalDeleteAsync( ids ?? [] );
		await CompletedAsync( FileManagerRefresh.All );
	}

	[RelayCommand]
	private async Task RestoreDirectoryAsync()
	{
		_reporter.ShowWindow();
		await _workflow.RestoreDeletedInDirectoryAsync( SelectedDirectoryId );
		await CompletedAsync( FileManagerRefresh.All );
	}

	[RelayCommand]
	private Task RestoreDirectoryRecursiveAsync() => _dialogs.ShowMessageAsync( "Not Implemented Yet" );

	#endregion
	#region Entry list commands

	[RelayCommand]
	private async Task GetLatestSelectedAsync()
	{
		_reporter.ShowWindow();
		await _workflow.GetLatestAsync( RemoteIds( SelectedEntries ) );
	}

	[RelayCommand]
	private async Task CheckoutSelectedAsync()
	{
		IReadOnlyList<EntryRow> selected = SelectedEntries;
		await GetLatestSelectedAsync();
		await _workflow.CheckoutAsync( RemoteIds( selected.Where( e => e.Checkout is null ) ) );
	}

	[RelayCommand]
	private async Task UndoCheckoutSelectedAsync()
	{
		ArrayList ids = RemoteIds( SelectedEntries.Where( e => e.Checkout is not null ) );
		if( ids.Count == 0 )
			return;

		_reporter.ShowWindow();
		await _workflow.UnCheckoutAsync( ids );
	}

	[RelayCommand]
	private async Task CommitSelectedAsync()
	{
		_reporter.ShowWindow();
		HashSet<HackFile> files = await _workflow.CollectCommitFilesAsync( SelectedEntries );
		if( files.Count > 0 )
			await _workflow.CommitAsync( files );
	}

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
			await _dialogs.ShowMessageAsync( string.Join( Environment.NewLine, errors ) );
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

		if( await _dialogs.ConfirmAsync( message, "Delete Files", MessageBoxIcon.Warning ) )
			_files.DeleteFiles( files );
		Refresh( FileManagerRefresh.Entries );
	}

	[RelayCommand]
	private async Task LogicalDeleteSelectedAsync()
	{
		_reporter.ShowWindow();
		await _workflow.LogicalDeleteAsync( RemoteIds( SelectedEntries ) );
		await CompletedAsync( FileManagerRefresh.All );
	}

	[RelayCommand]
	private async Task PermanentDeleteSelectedAsync()
	{
#if DEBUG
		ArrayList ids = RemoteIds( SelectedEntries );
		if( ids.Count == 0 )
		{
			await _dialogs.ShowMessageAsync( "No entries to delete" );
			return;
		}

		bool confirmed = await _dialogs.ConfirmAsync(
			$"Are you sure you want to permanently delete {ids.Count} entries from the database?\n" +
			"This will also permanently delete all associative versions, version properties, and version relationships",
			"Delete Entries and Other Records?", MessageBoxIcon.Warning );
		if( !confirmed )
			return;

		_reporter.ShowWindow();
		if( !await _workflow.PermanentDeleteAsync( ids ) )
		{
			await _dialogs.ShowMessageAsync( "Was unable to delete entries", "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error );
			return;
		}
		await CompletedAsync( FileManagerRefresh.All );
#else
		await _dialogs.ShowMessageAsync( "Permanent delete is only available in debug builds" );
#endif
	}

	[RelayCommand]
	private async Task UploadIconAsync()
	{
		if( SelectedEntries is not [ { FullName: not null } entry ] || !FileStatusPolicy.CanUploadIcon( entry.Status ) )
			return;
		await _dialogs.ShowMessageAsync( "Uploading icons is not implemented yet" );
	}

	#endregion
	#region History commands

	private Task<HpVersion?> SelectedHistoryVersionAsync()
		=> _files.GetVersionAsync( SelectedHistory?.Version ?? 0 );

	/// <summary>Downloads the selected history version, asking where to put it if a different local copy exists.</summary>
	[RelayCommand]
	private async Task DownloadHistoryAsync()
	{
		if( await SelectedHistoryVersionAsync() is not { } version )
			return;

		if( !_files.HasConflictingLocalCopy( version ) )
		{
			_files.DownloadVersion( version, toTemp: false );
			return;
		}

		DialogResult answer = await _dialogs.ShowMessageAsync(
			"File exists as a different version.\n" +
			"Retry:\tDownload in the Temporary Folder\n" +
			"Ignore:\tOverwrite the current version\n" +
			"Abort:\tCancel download",
			"File Version Conflict", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning );

		switch( answer )
		{
			case DialogResult.Retry:
				_files.DownloadVersion( version, toTemp: true );
				break;
			case DialogResult.Ignore:
				_files.DownloadVersion( version, toTemp: false );
				break;
		}
	}

	[RelayCommand] private Task DownloadHistoryToTempAsync() => DownloadHistoryToFolderAsync( toTemp: true );
	[RelayCommand] private Task DownloadHistoryOverwriteAsync() => DownloadHistoryToFolderAsync( toTemp: false );
	[RelayCommand] private Task OpenHistoryFromTempAsync() => OpenHistoryAsync( toTemp: true );
	[RelayCommand] private Task OpenHistoryOverwriteAsync() => OpenHistoryAsync( toTemp: false );
	[RelayCommand] private Task MoveHistoryToTempAsync() => MoveHistoryAsync( toTemp: true );
	[RelayCommand] private Task MoveHistoryToWorkingAsync() => MoveHistoryAsync( toTemp: false );

	private async Task DownloadHistoryToFolderAsync( bool toTemp )
	{
		if( await SelectedHistoryVersionAsync() is { } version )
			_files.DownloadVersion( version, toTemp );
	}

	private async Task OpenHistoryAsync( bool toTemp )
	{
		if( await SelectedHistoryVersionAsync() is not { } version )
			return;

		string folder = _files.DownloadVersion( version, toTemp );
		_files.OpenFile( Path.Combine( folder, version.name ?? "" ) );
	}

	private async Task MoveHistoryAsync( bool toTemp )
	{
		if( await SelectedHistoryVersionAsync() is not { } version )
			return;

		VersionMovePlan plan = _files.PlanMove( version, toTemp );
		string target = toTemp ? "temporary" : "current";
		string prefix = plan.From.Exists ? "" : $"file doesn't exist in {plan.From.DirectoryName}.\n";
		string verb = plan.From.Exists ? "move" : "download";
		string message = plan.To.Exists
			? $"{prefix}Would you like to {verb} this version to {target} and overwrite that version?"
			: $"{prefix}Would you like to {verb} this version to {target}?";
		string caption = ( plan.From.Exists ? "Move" : "Download" ) + ( plan.To.Exists ? " & Overwrite" : "" );

		if( await _dialogs.ConfirmAsync( message, caption, plan.To.Exists ? MessageBoxIcon.Warning : MessageBoxIcon.Question ) )
			_files.ExecuteMove( version, plan );
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

		string ids = string.Join( ", ", plan.VersionIds.Cast<object>() );
		if( await _dialogs.ShowMessageAsync( $"{intro}\n this will download version ids: {ids}\n{details}", "Version Download", MessageBoxButtons.YesNoCancel ) != DialogResult.Yes )
			return;

		IReadOnlyList<int> failed = await _workflow.DownloadVersionsAsync( plan.VersionIds );
		if( failed.Count > 0 )
			await _dialogs.ShowMessageAsync( $"failed to download version ids: {string.Join( ", ", failed )}" );
	}

	#endregion
	#region Helpers

	private async Task CompletedAsync( FileManagerRefresh refresh )
	{
		await _dialogs.ShowMessageAsync( "Completed!" );
		Refresh( refresh );
	}

	#endregion
}
