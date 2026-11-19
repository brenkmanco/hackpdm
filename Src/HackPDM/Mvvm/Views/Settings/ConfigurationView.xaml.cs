using HackPDM.Mvvm.Views.Hack;
using HackPDM.Mvvm.Views.Odoo;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>The Odoo and local settings pages, one above the other (the "Configurations" menu item).</summary>
public sealed partial class ConfigurationView : Page
{
	public ConfigurationView()
	{
		InitializeComponent();
		OdooFrame.Navigate( typeof( OdooSettingsView ) );
		HackFrame.Navigate( typeof( HackSettingsView ) );
	}
}
