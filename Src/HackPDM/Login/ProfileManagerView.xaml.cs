using System;
using System.Collections.Specialized;
using System.ComponentModel;

using HackPDM.Common;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Login;

public sealed partial class ProfileManagerView : Page
{
	private Button? _bannerActionButton;
	private bool _subscribed;

	public ProfileManagerViewModel ViewModel { get; } = AppComposition.CreateProfileManager();

	public ProfileManagerView()
	{
		InitializeComponent();
		if( HackApp.Window is { } window )
			window.Title = "Profile Manager - HackPDM";
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	private void OnLoaded( object sender, RoutedEventArgs e )
	{
		if( !_subscribed )
		{
			_subscribed = true;
			ViewModel.FilteredStatus.CollectionChanged += OnFilteredStatusChanged;
			ViewModel.PropertyChanged += ViewModel_PropertyChanged;
		}
		ViewModel.Attach();
		ViewModel.RefreshConnectionSummary();
		UpdateEmptyHint();
		UpdateBannerAppearance();
	}

	private void OnUnloaded( object sender, RoutedEventArgs e )
	{
		if( _subscribed )
		{
			_subscribed = false;
			ViewModel.FilteredStatus.CollectionChanged -= OnFilteredStatusChanged;
			ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
		}
		ViewModel.Detach();
	}

	private void OnFilteredStatusChanged( object? sender, NotifyCollectionChangedEventArgs e ) => UpdateEmptyHint();

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
			if( !ReferenceEquals( StatusBannerBar.ActionButton, _bannerActionButton ) )
				StatusBannerBar.ActionButton = _bannerActionButton;
		}
		else if( StatusBannerBar.ActionButton is not null )
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
	{
		bool empty = ViewModel.FilteredStatus.Count == 0;
		EmptyConnectionHint.Visibility = empty.ToVisibility();
		if( empty )
		{
			SelectedMessageText.Text = string.Empty;
			SelectedMessageCard.Visibility = Visibility.Collapsed;
		}
	}

	private void ProfileManStatusList_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		if( !ProfileManStatusList.IsUpdatingSelection )
			UpdateSelectedMessageCard();
	}

	private void ProfileManStatusList_SelectionBatchCompleted( object? sender, EventArgs e )
		=> UpdateSelectedMessageCard();

	private void UpdateSelectedMessageCard()
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
