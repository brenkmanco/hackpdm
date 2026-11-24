using System;

using HackPDM.Core.Configuration;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Windows.System;

namespace HackPDM.Settings;

public sealed partial class OdooSettingsView : Page
{
	public OdooSettingsViewModel ViewModel { get; } = AppComposition.CreateOdooSettings();

	public OdooSettingsView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
		=> _ = ViewModel.LoadCredentialsAsync();

	private void OnUnloaded( object sender, RoutedEventArgs e )
		=> ViewModel.CancelPendingLoads();

	private async void TxtOdooPass_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		if( e.Key == VirtualKey.Enter && ViewModel.SubmitCommand.CanExecute( null ) )
		{
			e.Handled = true;
			try
			{
				await ViewModel.SubmitCommand.ExecuteAsync( null );
			}
			catch( Exception ex )
			{
				HackLogger.Warn( "OdooSettings", $"Submitting Odoo settings failed: {ex.Message}", ex );
			}
		}
	}
}
