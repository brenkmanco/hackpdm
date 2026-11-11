using CommunityToolkit.Mvvm.ComponentModel;

namespace HackPDM.Mvvm.ViewModels.Hack;

public partial class HomeViewModel : ObservableObject
{
	public string SelectedMenuItemTag { get; set; } = "ProfileManager";
}
