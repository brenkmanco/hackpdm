using HackPDM.Shared.GlobalData;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>Moves the main window to one of its pages.</summary>
public interface INavigationService
{
	void NavigateTo( NavigatePageMenu page );
}
