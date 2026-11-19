using HackPDM.Common;

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
		ViewModel.Saved += () => this.Window?.Close();
	}

	private void TxtOdooPass_KeyDown( object sender, KeyRoutedEventArgs e )
	{
		if( e.Key == VirtualKey.Enter && ViewModel.SubmitCommand.CanExecute( null ) )
		{
			e.Handled = true;
			ViewModel.SubmitCommand.Execute( null );
		}
	}
}
