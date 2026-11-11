using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Shell;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>Login and profile files; everything happens in <see cref="ProfileManagerViewModel"/>.</summary>
public sealed partial class ProfileManagerView : Page
{
	public ProfileManagerViewModel ViewModel => AppComposition.ProfileManager;

	public ProfileManagerView()
	{
		InitializeComponent();
		HackApp.Window?.Title = "Profile Manager - HackPDM";
	}
}
