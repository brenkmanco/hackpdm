using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>Moves the main window to one of its pages.</summary>
public interface INavigationService
{
	/// <summary>Shows a page in the main window, as if its menu item had been selected.</summary>
	/// <param name="page">The page to show.</param>
	/// <remarks>Does nothing until the home page has been built.</remarks>
	void NavigateTo( NavigatePageMenu page );
}
