using HackPDM.Mvvm.Models.Hack;
using HackPDM.Mvvm.Models.Odoo;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.ViewModels.Hack;
using HackPDM.Mvvm.ViewModels.Odoo;
using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Dialogs;

namespace HackPDM.Shell;

/// <summary>
/// Composition root: the one place that picks concrete models and views for the view models.
/// Models and view models only see interfaces; nothing below this layer news up a view.
/// </summary>
public static class AppComposition
{
	/// <summary>Dialogs and windows shown on behalf of view models.</summary>
	public static IDialogService Dialogs => field ??= new DialogService();

	/// <summary>App-wide progress reporter, shared so the status window can cancel whatever is running.</summary>
	public static StatusWindowReporter Reporter => field ??= new( Dialogs, () => InstanceManager.Dialog, () => InstanceManager.Session );

	public static IPdmWorkflowService Workflow => field ??= new PdmWorkflowService( Reporter );

	/// <summary>Hooks view models up to the UI thread. Call once the window's dispatcher exists.</summary>
	public static void Initialize()
		=> UiDispatcher.Post = action => _ = SafeHelper.SafeInvokerAsync( action );

	public static HackFileManagerViewModel CreateHackFileManager() => new(
		Workflow,
		new EntryFileService(),
		new EntryDetailsService(),
		Dialogs,
		Reporter,
		new DirectoryTreeService(),
		new EntryListService() );

	public static SearchOdooViewModel CreateSearchOdoo() => new(
		new EntrySearchService(),
		Workflow,
		new EntryFileService(),
		Dialogs,
		Reporter,
		( segments, fileName ) => InstanceManager.HFM.RequestLocateAsync( segments, fileName ) );
}
