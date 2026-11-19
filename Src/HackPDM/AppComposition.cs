using System.Collections.Generic;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Odoo;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
using HackPDM.FileManager;
using HackPDM.Login;
using HackPDM.Search;
using HackPDM.Settings;
using HackPDM.Shell;
using HackPDM.Status;
using HackPDM.Themes;

using Microsoft.UI.Dispatching;

namespace HackPDM;

/// <summary>Composition root wiring concrete core services, theme service, session state, and view models.</summary>
public static class AppComposition
{
	private static LocalFileWatcher? _localFiles;

	public static JsonFileSettingsProvider Settings => field ??= AppSettingsStore.Open();
	public static IDialogService Dialogs => field ??= new DialogService();
	public static IFilePickerService FilePickers => field ??= new FilePickerService();
	public static NavigationService Navigation => field ??= new();
	public static ThemeService Theme => field ??= new();
	public static StatusWindowReporter Reporter => field ??= new( Dialogs, () => StatusLog );
	public static IPdmWorkflowService Workflow => field ??= new PdmWorkflowService( Reporter );
	public static IEntryFileService EntryFiles => field ??= new EntryFileService();
	public static IEntryDetailsService EntryDetails => field ??= new EntryDetailsService();
	public static LoginSession Session => field ??= CreateSession();
	public static FileManagerState FileManagerState => field ??= new();
	public static LocalFileWatcher LocalFiles => _localFiles ??= CreateLocalFileWatcher();
	public static StatusDialogViewModel StatusLog => field ??= new( Dialogs, () => Reporter.Cancel(), () => Reporter.CancelSilent() );

	public static SearchOdooViewModel SearchOdoo => field ??= new(
		new EntrySearchService(),
		Workflow,
		EntryFiles,
		Dialogs,
		Reporter,
		LocateInFileManagerAsync );

	public static void DisposeLocalFilesIfCreated()
	{
		if( _localFiles is not { } watcher )
			return;
		_localFiles = null;
		watcher.Dispose();
	}

	public static void Initialize( DispatcherQueue queue )
		=> UiDispatcher.Current = new DispatcherQueueUiDispatcher( queue );

	public static void InitializeDefaults()
	{
		_ = new HackDefaults( Settings );
		_ = new OdooDefaults( Settings );
		if( Settings.Get<string>( nameof( PdmConstants.TemporaryPath ) ) is { Length: > 0 } tempPath )
			PdmConstants.TemporaryPath = tempPath;
	}

	public static void OnVaultPathChanged()
	{
		EntryIcons.ClearServerCache();
		LocalFiles.Watch( PwaPaths.Vault );
		FileManagerState.Reset();
	}

	public static ProfileManagerViewModel CreateProfileManager()
		=> new( Session, Dialogs, FilePickers, Navigation, OnVaultPathChanged, InspectSettingsStatus );

	public static HackFileManagerViewModel CreateFileManager() => new(
		FileManagerState,
		Workflow,
		EntryFiles,
		EntryDetails,
		Dialogs,
		Reporter,
		new DirectoryTreeService(),
		new EntryListService(),
		LocalFiles,
		FilePickers );

	public static HackSettingsViewModel CreateHackSettings() => new( Dialogs, FilePickers, Session, OnVaultPathChanged );
	public static OdooSettingsViewModel CreateOdooSettings() => new( Dialogs, Session );
	public static StatusSettingsViewModel CreateStatusSettings() => new( StatusLog, Dialogs, Session );
	public static ThemeEditorViewModel CreateThemeEditor() => new( Theme, Session );

	internal static Task LocateInFileManagerAsync( IReadOnlyList<string> segments, string? fileName )
	{
		Task located = FileManagerState.RequestLocateAsync( segments, fileName );
		Navigation.NavigateTo( NavigatePageMenu.HackFileManager );
		return located;
	}

	private static LoginSession CreateSession()
	{
		LoginSession session = new();
		foreach( string error in Settings.Errors )
			session.LogError( ProfileStatusCategory.Settings, error );
		Settings.ErrorOccurred += error => session.LogError( ProfileStatusCategory.Settings, error );
		return session;
	}

	private static LocalFileWatcher CreateLocalFileWatcher()
	{
		LocalFileWatcher watcher = new();
		watcher.Changed += batch => UiDispatcher.Post( () => FileManagerState.LocalFilesChanged( batch ) );
		watcher.Overflowed += () => UiDispatcher.Post( () => FileManagerState.LocalFilesChanged( (IReadOnlyList<LocalFileChange>?)null ) );
		return watcher;
	}

	private static IReadOnlyList<(StatusMessage Status, string Category, string Message)> InspectSettingsStatus()
	{
		List<(StatusMessage, string, string)> items = [];
		if( Settings.Errors.Count > 0 )
		{
			foreach( string err in Settings.Errors )
				items.Add( ( StatusMessage.ERROR, ProfileStatusCategory.Settings, err ) );
		}
		else
		{
			string source = Settings.Existed ? AppSettingsStore.FilePath : "defaults (not yet saved)";
			items.Add( ( StatusMessage.INFO, ProfileStatusCategory.Settings, $"App settings active from {source}." ) );
		}
		return items;
	}
}
