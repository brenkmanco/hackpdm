using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;
using HackPDM.UI.Forms.Settings;

using MessageBox = HackPDM.UI.Forms.Helper.MessageBox;
using WindowHelper = HackPDM.UI.Controls.WindowHelper;

namespace HackPDM.Services;

/// <summary>Default <see cref="IDialogService"/> backed by the app's MessageBox page and StatusDialog window.</summary>
public sealed class DialogService : IDialogService
{
	public Task<DialogResult> ShowMessageAsync( string message )
		=> MessageBox.ShowAsync( message );

	public Task<DialogResult> ShowMessageAsync( string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon = MessageBoxIcon.None )
		=> MessageBox.ShowAsync( message, caption, buttons, icon );

	public async Task<bool> ConfirmAsync( string message, string caption, MessageBoxIcon icon = MessageBoxIcon.Question )
		=> await MessageBox.ShowAsync( message, caption, MessageBoxButtons.YesNoCancel, icon ) is DialogResult.Yes or DialogResult.OK;

	public void ShowStatusWindow()
		=> WindowHelper.CreateWindowAndPage<StatusDialog>( out _, out _ );
}
