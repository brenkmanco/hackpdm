using System.ComponentModel;

using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Login;

public sealed partial class ProfileManagerView : Page
{
	private Button? _bannerActionButton;

	public ProfileManagerViewModel ViewModel { get; } = AppComposition.CreateProfileManager();

	public ProfileManagerView()
	{
		InitializeComponent();
		if( HackApp.Window is { } window )
			window.Title = "Profile Manager - HackPDM";
		ViewModel.FilteredStatus.CollectionChanged += ( _, _ ) => UpdateEmptyHint();
		ViewModel.PropertyChanged += ViewModel_PropertyChanged;
		Loaded += OnLoaded;
		Unloaded += ( _, _ ) => ViewModel.Detach();
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		ViewModel.Attach();
		ViewModel.RefreshConnectionSummary();
		UpdateEmptyHint();
		UpdateBannerAppearance();
	}

	private void ViewModel_PropertyChanged( object? sender, PropertyChangedEventArgs e )
	{
		if( e.PropertyName is nameof( ProfileManagerViewModel.BannerStatus )
			or nameof( ProfileManagerViewModel.ShowOpenConfigurationsAction )
			or nameof( ProfileManagerViewModel.HasBanner ) )
		{
			UpdateBannerAppearance();
		}
	}

	private void UpdateBannerAppearance()
	{
		StatusBannerBar.Severity = ViewModel.BannerStatus switch
		{
			StatusMessage.ERROR => InfoBarSeverity.Error,
			StatusMessage.WARNING => InfoBarSeverity.Warning,
			StatusMessage.SUCCESS => InfoBarSeverity.Success,
			_ => InfoBarSeverity.Informational,
		};

		if( ViewModel.ShowOpenConfigurationsAction )
		{
			_bannerActionButton ??= CreateConfigureActionButton();
			StatusBannerBar.ActionButton = _bannerActionButton;
		}
		else
		{
			StatusBannerBar.ActionButton = null!;
		}
	}

	private Button CreateConfigureActionButton()
	{
		Button button = new() { Content = "Open Configurations…" };
		button.Click += ( _, _ ) => ViewModel.OpenSettingsCommand.Execute( null );
		return button;
	}

	private void UpdateEmptyHint()
		=> EmptyConnectionHint.Visibility = ViewModel.FilteredStatus.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

	private void ProfileManStatusList_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( ProfileManStatusList.SelectedItem is BasicStatusMessage selected )
		{
			SelectedMessageText.Text = $"[{selected.TimestampText}] [{selected.Status}] {selected.Category}: {selected.Message}";
			SelectedMessageCard.Visibility = Visibility.Visible;
		}
		else
		{
			SelectedMessageText.Text = string.Empty;
			SelectedMessageCard.Visibility = Visibility.Collapsed;
		}
	}

	private void OpenStatusLog_Click( object sender, RoutedEventArgs e )
		=> AppComposition.Dialogs.ShowStatusWindow();
}
