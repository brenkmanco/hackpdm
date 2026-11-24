using System;

using HackPDM.Core.Services;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Login;

public sealed partial class ProfileManagerView : Page
{
	public ProfileManagerViewModel ViewModel { get; } = AppComposition.CreateProfileManager();

	public ProfileManagerView()
	{
		InitializeComponent();
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		ViewModel.Attach();
		ViewModel.RefreshConnectionSummary();
	}

	private void OnUnloaded( object sender, RoutedEventArgs e )
		=> ViewModel.Detach();

	private void ProfileManStatusList_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( !ProfileManStatusList.IsUpdatingSelection )
			ViewModel.SelectedStatusMessage = ProfileManStatusList.SelectedItem as BasicStatusMessage;
	}

	private void ProfileManStatusList_SelectionBatchCompleted( object? sender, EventArgs e )
		=> ViewModel.SelectedStatusMessage = ProfileManStatusList.SelectedItem as BasicStatusMessage;

	private void OpenStatusLog_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Dialogs.ShowStatusWindow();
}
