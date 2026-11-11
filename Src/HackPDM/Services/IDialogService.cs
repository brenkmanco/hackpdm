using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Services;

/// <summary>
/// Lets view models ask the user things and open the status window without touching any UI types.
/// </summary>
public interface IDialogService
{
	Task<DialogResult> ShowMessageAsync( string message );
	Task<DialogResult> ShowMessageAsync( string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon = MessageBoxIcon.None );

	/// <summary>Asks a yes/no/cancel question; true only when the user answered Yes (or OK).</summary>
	Task<bool> ConfirmAsync( string message, string caption, MessageBoxIcon icon = MessageBoxIcon.Question );

	/// <summary>Opens the status/progress window that long-running PDM operations log into.</summary>
	void ShowStatusWindow();
}
