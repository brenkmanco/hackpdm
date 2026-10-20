using CommunityToolkit.Mvvm.ComponentModel;

namespace HackPDM.ViewModels;

public partial class HomeViewModel : ObservableObject
{
	public string SelectedMenuItemTag { get; set; } = "ProfileManager";
}
