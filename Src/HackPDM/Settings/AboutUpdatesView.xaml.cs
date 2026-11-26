using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Settings;

/// <summary>Displays application version, server update policy, and on-demand update checks.</summary>
public sealed partial class AboutUpdatesView : Page
{
	public AboutUpdatesViewModel ViewModel { get; } = new();

	public AboutUpdatesView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
		=> ViewModel.RefreshServerPolicy();

	private void OnUnloaded( object sender, RoutedEventArgs e )
		=> ViewModel.CancelPendingUpdates();
}
