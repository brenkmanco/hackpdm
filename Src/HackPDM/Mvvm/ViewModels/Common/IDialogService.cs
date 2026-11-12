using System.Threading.Tasks;

using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>
/// Lets view models ask the user things and open the status window without touching any UI types.
/// </summary>
public interface IDialogService
{
	/// <summary>Shows a message with an OK button.</summary>
	/// <param name="message">The text to show.</param>
	/// <returns>The button the user pressed to close the message.</returns>
	Task<DialogResult> ShowMessageAsync( string message );

	/// <summary>Shows a message with a caption, a choice of buttons and an icon.</summary>
	/// <param name="message">The text to show.</param>
	/// <param name="caption">The title of the message box.</param>
	/// <param name="buttons">Which buttons to offer.</param>
	/// <param name="icon">The icon shown beside the text.</param>
	/// <returns>The button the user pressed to close the message.</returns>
	Task<DialogResult> ShowMessageAsync( string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon = MessageBoxIcon.None );

	/// <summary>Asks a yes/no/cancel question; true only when the user answered Yes (or OK).</summary>
	/// <param name="message">The question.</param>
	/// <param name="caption">The title of the message box.</param>
	/// <param name="icon">The icon shown beside the question.</param>
	/// <returns><see langword="true"/> for Yes or OK; <see langword="false"/> for No, Cancel or closing the box.</returns>
	Task<bool> ConfirmAsync( string message, string caption, MessageBoxIcon icon = MessageBoxIcon.Question );

	/// <summary>Opens the status/progress window that long-running PDM operations log into.</summary>
	/// <remarks>There is only one status window: when it is already open it is brought to the front.</remarks>
	void ShowStatusWindow();
}
