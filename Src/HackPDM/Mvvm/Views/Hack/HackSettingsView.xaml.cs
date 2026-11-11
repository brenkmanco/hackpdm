using HackPDM.Mvvm.ViewModels.Hack;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Shell;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>The local folder settings; the view model loads, validates and saves them.</summary>
public sealed partial class HackSettingsView : Page
{
	public HackSettingsViewModel ViewModel { get; } = AppComposition.CreateHackSettings();

	public HackSettingsView()
	{
		InitializeComponent();
		ViewModel.Saved += () => this.Window?.Close();
	}
}
