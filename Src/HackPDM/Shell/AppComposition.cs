using HackPDM.Core.Configuration;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Mvvm.Models.Hack;
using HackPDM.Mvvm.Models.Odoo;
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
/// Composition root: the one place that picks concrete models and views for the view models, and
/// holds the app's long-lived view models (one of each). Models and view models only see
/// interfaces; nothing below this layer news up a view.
/// </summary>
public static class AppComposition
{
	// ---------------------------------------------------------------- settings and services

	/// <summary>The app's persisted key/value settings.</summary>
	public static CoreSettings Settings => field ??= new( new ModernSettingsProvider() );

	/// <summary>Gives the Hack and Odoo defaults their settings store. Call once at startup.</summary>
	public static void InitializeDefaults()
	{
		_ = new HackDefaults( Settings );
		_ = new OdooDefaults( Settings );
	}

	/// <summary>Dialogs and windows shown on behalf of view models.</summary>
	public static IDialogService Dialogs => field ??= new DialogService();

	public static IFilePickerService FilePickers => field ??= new FilePickerService();

	/// <summary>The home page's menu; the home page attaches itself when built.</summary>
	public static NavigationService Navigation => field ??= new();

	/// <summary>App-wide progress reporter, shared so the status window can cancel whatever is running.</summary>
	public static StatusWindowReporter Reporter => field ??= new( Dialogs, () => StatusLog );

	/// <summary>App theme, loaded from AppConfig.json. First use needs the app settings (after services are configured).</summary>
	public static ThemeService Theme => field ??= new( UIStorage.GlobalData.Theme );

	public static IPdmWorkflowService Workflow => field ??= new PdmWorkflowService( Reporter );

	/// <summary>Hooks the app up to the UI thread. Call once the window's dispatcher exists.</summary>
	public static void Initialize( Microsoft.UI.Dispatching.DispatcherQueue queue )
		=> UiDispatcher.Current = new DispatcherQueueUiDispatcher( queue );

	// ---------------------------------------------------------------- view models

	/// <summary>The status window's log and progress, kept while the window is closed.</summary>
	public static StatusDialogViewModel StatusLog => field ??= new( Dialogs, () => Reporter.Cancel() );

	public static ProfileManagerViewModel ProfileManager => field ??= new( Dialogs, FilePickers, Navigation );

	public static HackFileManagerViewModel FileManager => field ??= new(
		Workflow,
		new EntryFileService(),
		new EntryDetailsService(),
		Dialogs,
		Reporter,
		new DirectoryTreeService(),
		new EntryListService() );

	/// <summary>A settings page's view model, fresh each time the page is built (it loads the current values).</summary>
	public static HackSettingsViewModel CreateHackSettings() => new( Dialogs );

	public static OdooSettingsViewModel CreateOdooSettings() => new( Dialogs );

	public static ThemeEditorViewModel CreateThemeEditor() => new( Theme );

	public static SearchOdooViewModel SearchOdoo => field ??= new(
		new EntrySearchService(),
		Workflow,
		new EntryFileService(),
		Dialogs,
		Reporter,
		( segments, fileName ) => FileManager.RequestLocateAsync( segments, fileName ) );
}
