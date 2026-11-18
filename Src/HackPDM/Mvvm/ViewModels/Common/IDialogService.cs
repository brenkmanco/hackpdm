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

	/// <summary>Opens the status/progress log (docked inside the main window by default, or focuses its detached window).</summary>
	void ShowStatusWindow();

	/// <summary>Toggles the docked status log panel open/closed inside the main window, or focuses it when detached.</summary>
	void ToggleStatusWindow() => ShowStatusWindow();

	/// <summary>Closes the status panel whether docked in the main window or detached.</summary>
	void CloseStatusWindow() { }

	/// <summary>Detaches the status panel from the main window into a floating window.</summary>
	void DetachStatusWindow() => ShowStatusWindow();

	/// <summary>Moves the detached status window back into the main window's docked extension panel.</summary>
	void DockStatusWindow() { }

	/// <summary>Opens the Odoo search panel (docked inside the main window by default, or focuses its detached window).</summary>
	void ShowSearchWindow() { }

	/// <summary>Toggles the docked search panel open/closed inside the main window, or focuses it when detached.</summary>
	void ToggleSearchWindow() => ShowSearchWindow();

	/// <summary>Closes the search panel whether docked in the main window or detached.</summary>
	void CloseSearchWindow() { }

	/// <summary>Detaches the search panel into a floating window.</summary>
	void DetachSearchWindow() => ShowSearchWindow();

	/// <summary>Moves the detached search window back into the main window's docked extension panel.</summary>
	void DockSearchWindow() { }

	/// <summary>Switches the status panel between its full 3-pane log and its compact category bar.</summary>
	void SetStatusMinimalMode( bool minimal ) { }

	/// <summary>Switches the search panel between its full query/results view and its compact search bar + quick results list.</summary>
	void SetSearchMinimalMode( bool minimal ) { }

	/// <summary>Opens (or expands) the status dialog in its full version.</summary>
	void ShowFullStatusWindow()
	{
		SetStatusMinimalMode( false );
		ShowStatusWindow();
	}

	/// <summary>Opens (or expands) the search dialog in its full version.</summary>
	void ShowFullSearchWindow()
	{
		SetSearchMinimalMode( false );
		ShowSearchWindow();
	}
}
