using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Settings;

public sealed partial class StatusSettingsView : Page
{
	public StatusSettingsViewModel ViewModel { get; } = AppComposition.CreateStatusSettings();

	public StatusSettingsView()
	{
		InitializeComponent();
	}
}
