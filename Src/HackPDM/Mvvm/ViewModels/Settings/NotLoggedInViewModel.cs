using CommunityToolkit.Mvvm.ComponentModel;

namespace HackPDM.Mvvm.ViewModels.Settings;

public partial class NotLoggedInViewModel : ObservableObject
{
	public string Message { get; set; } = "You are not logged in. Please log in through the Profile Manager.";
}
