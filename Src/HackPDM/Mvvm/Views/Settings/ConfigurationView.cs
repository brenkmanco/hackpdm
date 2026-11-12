using HackPDM.Mvvm.Views.Hack;
using HackPDM.Mvvm.Views.Odoo;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>The Odoo and local settings pages, one above the other (the "Configurations" menu item).</summary>
public sealed partial class ConfigurationView : Page
{
	public ConfigurationView()
	{
		Frame odooFrame = new();
		Frame hackFrame = new();
		StackPanel stack = new();
		stack.Children.Add( odooFrame );
		stack.Children.Add( hackFrame );
		Content = new ScrollView { Content = stack };
		odooFrame.Navigate( typeof( OdooSettingsView ) );
		hackFrame.Navigate( typeof( HackSettingsView ) );
	}
}
