using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Settings;

public sealed partial class ConfigurationView : Page
{
	public ConfigurationView()
	{
		InitializeComponent();
		OdooFrame.Navigate( typeof( OdooSettingsView ) );
		HackFrame.Navigate( typeof( HackSettingsView ) );
	}
}
