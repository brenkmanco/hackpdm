using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.Views.Settings;

using MessageBox = HackPDM.Mvvm.Views.Dialogs.MessageBox;
using WindowHelper = HackPDM.Mvvm.Views.Common.Controls.WindowHelper;

namespace HackPDM.Mvvm.Views.Dialogs;

/// <summary>Default <see cref="IDialogService"/> backed by the app's MessageBox page and StatusDialog window.</summary>
public sealed class DialogService : IDialogService
{
	public Task<DialogResult> ShowMessageAsync( string message )
		=> MessageBox.ShowAsync( message );

	public Task<DialogResult> ShowMessageAsync( string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon = MessageBoxIcon.None )
		=> MessageBox.ShowAsync( message, caption, buttons, icon );

	public async Task<bool> ConfirmAsync( string message, string caption, MessageBoxIcon icon = MessageBoxIcon.Question )
		=> await MessageBox.ShowAsync( message, caption, MessageBoxButtons.YesNoCancel, icon ) is DialogResult.Yes or DialogResult.OK;

	// the one status window: showing it again brings it to the front instead of opening another
	private Microsoft.UI.Xaml.Window? _statusWindow;

	public void ShowStatusWindow()
	{
		if( _statusWindow is { } open )
		{
			open.Activate();
			return;
		}
		WindowHelper.CreateWindowAndPage<StatusDialogView>( out var page, out var window );
		page.ParentWindow = window;
		_statusWindow = window;
		window.Closed += ( _, _ ) =>
		{
			page.ParentWindow = null;
			_statusWindow = null;
		};
	}
}
