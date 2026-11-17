using HackPDM.Mvvm.ViewModels.Settings;
using HackPDM.Shell;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Settings;

/// <summary>Status window settings page (history length, auto-close, clear logs).</summary>
public sealed partial class StatusSettingsView : Page
{
	public StatusSettingsViewModel ViewModel { get; } = AppComposition.CreateStatusSettings();

	public StatusSettingsView()
	{
		InitializeComponent();
	}
}
