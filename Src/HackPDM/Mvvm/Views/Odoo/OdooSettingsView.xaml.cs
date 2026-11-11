using HackPDM.Mvvm.ViewModels.Odoo;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Shell;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Odoo;

/// <summary>The Odoo server settings; the view model loads, validates and saves them.</summary>
public sealed partial class OdooSettingsView : Page
{
	public OdooSettingsViewModel ViewModel { get; } = AppComposition.CreateOdooSettings();

	public OdooSettingsView()
	{
		InitializeComponent();
		ViewModel.Saved += () => this.Window?.Close();
	}
}
