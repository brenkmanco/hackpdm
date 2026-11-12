using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using HackPDM.Core;
using HackPDM.Core.Configuration;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Mvvm.Models.Odoo;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Models.Tools;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.ViewModels.Hack;
using HackPDM.Mvvm.ViewModels.Odoo;
using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Common.Settings;
using HackPDM.Mvvm.Views.Common.Theming;
using HackPDM.Mvvm.Views.Dialogs;

namespace HackPDM.Shell;

/// <summary>
/// Composition root: the one place that picks concrete models and views for the view models. Models
/// and view models only see interfaces; nothing below this layer news up a view.
/// <para>
/// Lifetimes: the services and the models that hold a page's context (<see cref="Session"/>,
/// <see cref="FileManagerState"/>, <see cref="JsonDraft"/>) live for the whole run, so that leaving a page and
/// coming back shows what it showed. A page's view model is made by a <c>Create…</c> factory when the
/// page is entered and disposed when it is left. The few view models still held here back windows,
/// not pages (the status window, the search window).
/// </para>
/// </summary>
public static class AppComposition
{
	// ---------------------------------------------------------------- settings and services

	/// <summary>The app's persisted key/value settings.</summary>
	public static CoreSettings Settings => field ??= new( AppSettingsStore.Open() );

	/// <summary>Gives the Hack and Odoo defaults their settings store. Call once at startup.</summary>
	/// <remarks>Creating the defaults registers them as <c>HackDefaults.Instance</c> and <see cref="OdooDefaults.Instance"/>.</remarks>
	public static void InitializeDefaults()
	{
		_ = new HackDefaults( Settings );
		_ = new OdooDefaults( Settings );
	}

	/// <summary>Dialogs and windows shown on behalf of view models.</summary>
	public static IDialogService Dialogs => field ??= new DialogService();

	/// <summary>The system file pickers.</summary>
	public static IFilePickerService FilePickers => field ??= new FilePickerService();

	/// <summary>The home page's menu; the home page attaches itself when built.</summary>
	public static NavigationService Navigation => field ??= new();

	/// <summary>App-wide progress reporter, shared so the status window can cancel whatever is running.</summary>
	public static StatusWindowReporter Reporter => field ??= new( Dialogs, () => StatusLog );

	/// <summary>App theme, loaded from AppConfig.json. First use needs the app settings (after services are configured).</summary>
	public static ThemeService Theme => field ??= new( UIStorage.GlobalData.Theme );

	/// <summary>The PDM workflows (get latest, checkout, commit, delete), reporting through <see cref="Reporter"/>.</summary>
	public static IPdmWorkflowService Workflow => field ??= new PdmWorkflowService( Reporter );

	/// <summary>Hooks the app up to the UI thread. Call once the window's dispatcher exists.</summary>
	/// <param name="queue">The main window's dispatcher queue; becomes <see cref="UiDispatcher.Current"/>.</param>
	public static void Initialize( Microsoft.UI.Dispatching.DispatcherQueue queue )
		=> UiDispatcher.Current = new DispatcherQueueUiDispatcher( queue );

	// ---------------------------------------------------------------- models kept for the whole run

	/// <summary>Whether the user is logged in (the Profile Manager page's model).</summary>
	public static LoginSession Session => field ??= new();

	/// <summary>The file manager's tree, selection and rows, kept between visits of the page.</summary>
	public static FileManagerState FileManagerState => field ??= new();

	/// <summary>
	/// Watches the working folder for the whole run. The file manager page applies the changes while it is
	/// shown; meanwhile they only mark its kept rows stale.
	/// </summary>
	public static LocalFileWatcher LocalFiles => field ??= CreateLocalFileWatcher();

	private static LocalFileWatcher CreateLocalFileWatcher()
	{
		LocalFileWatcher watcher = new();
		watcher.Changed += batch => UiDispatcher.Post( () => FileManagerState.LocalFilesChanged(
			[ .. batch.Select( change => PwaPaths.ToServerName( System.IO.Path.GetDirectoryName( change.FullPath ) ) ) ] ) );
		watcher.Overflowed += () => UiDispatcher.Post( () => FileManagerState.LocalFilesChanged( null ) );
		return watcher;
	}

	/// <summary>Unsaved edits of the JSON editor, kept between visits of the page.</summary>
	public static JsonEditorDraft JsonDraft => field ??= new();

	// ---------------------------------------------------------------- view models

	/// <summary>The status window's log and progress, kept while the window is closed.</summary>
	public static StatusDialogViewModel StatusLog => field ??= new( Dialogs, () => Reporter.Cancel() );

	/// <summary>The Profile Manager page's view model, made per visit. Logging in as another account forgets the file manager's context.</summary>
	/// <returns>A new view model sharing the run-wide <see cref="Session"/>.</returns>
	public static ProfileManagerViewModel CreateProfileManager()
		=> new( Session, Dialogs, FilePickers, Navigation, () => FileManagerState.Reset() );

	/// <summary>The file manager page's view model, made per visit from <see cref="FileManagerState"/>.</summary>
	/// <returns>A new view model; the caller disposes it when the page is left.</returns>
	public static HackFileManagerViewModel CreateFileManager() => new(
		FileManagerState,
		Workflow,
		new EntryFileService(),
		new EntryDetailsService(),
		Dialogs,
		Reporter,
		new DirectoryTreeService(),
		new EntryListService(),
		LocalFiles );

	/// <summary>The Hack settings page's view model, fresh each time the page is built (it loads the current values).</summary>
	/// <returns>A new view model holding the current local folder settings.</returns>
	public static HackSettingsViewModel CreateHackSettings() => new( Dialogs );

	/// <summary>The Odoo settings page's view model, fresh each time the page is built (it loads the current values).</summary>
	/// <returns>A new view model holding the current Odoo settings.</returns>
	public static OdooSettingsViewModel CreateOdooSettings() => new( Dialogs );

	/// <summary>The theme editor page's view model, made per visit.</summary>
	/// <returns>A new view model editing the run-wide <see cref="Theme"/>.</returns>
	public static ThemeEditorViewModel CreateThemeEditor() => new( Theme );

	/// <summary>The search window's state, kept for the whole run; locating a result switches to the file manager.</summary>
	public static SearchOdooViewModel SearchOdoo => field ??= new(
		new EntrySearchService(),
		Workflow,
		new EntryFileService(),
		Dialogs,
		Reporter,
		LocateInFileManagerAsync );

	/// <summary>Shows a file in the file manager: right away when its page is shown, else after switching to it.</summary>
	private static Task LocateInFileManagerAsync( IReadOnlyList<string> segments, string? fileName )
	{
		Task located = FileManagerState.RequestLocateAsync( segments, fileName );
		Navigation.NavigateTo( HackPDM.Shared.GlobalData.NavigatePageMenu.HackFileManager );
		return located;
	}
}
