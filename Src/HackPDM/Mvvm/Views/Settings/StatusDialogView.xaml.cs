using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Shell;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>The status window: the log and progress of the running operation, all bound to <see cref="StatusDialogViewModel"/>.</summary>
public sealed partial class StatusDialogView : Page
{
	public StatusDialogViewModel ViewModel => AppComposition.StatusLog;

	/// <summary>The window showing this page; closing it while work runs asks for confirmation.</summary>
	public Window? ParentWindow
	{
		get;
		set
		{
			if( ReferenceEquals( field, value ) )
				return;
			field?.AppWindow.Closing -= AppWindow_Closing;
			field = value;
			field?.AppWindow.Closing += AppWindow_Closing;
		}
	}

	public StatusDialogView()
	{
		InitializeComponent();
	}

	private void CmdCloseClick( object sender, RoutedEventArgs arg ) => ParentWindow?.Close();

	private void StatusSettings_Click( object sender, RoutedEventArgs arg )
		=> WindowHelper.CreateWindowPage<ApplicationSettingsView>();

	private async void AppWindow_Closing( AppWindow sender, AppWindowClosingEventArgs args )
	{
		if( !ViewModel.IsInProcess )
			return;
		// hold the close (it can't wait for the answer), then close for real if the user confirms
		args.Cancel = true;
		if( await ViewModel.ConfirmCloseAsync() )
			ParentWindow?.Close();
	}
}
