using HackPDM.Common;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Settings;

public sealed partial class HackSettingsView : Page
{
	public HackSettingsViewModel ViewModel { get; } = AppComposition.CreateHackSettings();

	public HackSettingsView()
	{
		InitializeComponent();
		ViewModel.Saved += () => this.Window?.Close();
	}
}
