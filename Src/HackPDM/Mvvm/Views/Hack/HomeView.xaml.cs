using System;
using System.Collections.Generic;
using System.Linq;

using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Views.Common.Controls;
using HackPDM.Mvvm.Views.Odoo;
using HackPDM.Mvvm.Views.Settings;
using HackPDM.Mvvm.Views.Tools;
using HackPDM.Shared.GlobalData;
using HackPDM.Shell;

using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>
/// The main window's page: a navigation menu and the frame showing the chosen page. Each menu item's
/// Tag is its <see cref="NavigatePageMenu"/> name; <see cref="NavigationService"/> selects items from
/// view models.
/// </summary>
public sealed partial class HomeView : Page
{
	/// <summary>Each page and the window size and title it uses.</summary>
	private static readonly Dictionary<NavigatePageMenu, (Type Page, string Config)> Pages = new()
	{
		[ NavigatePageMenu.HackFileManager ] = ( typeof( HackFileManagerView ), "HackFileManager" ),
		[ NavigatePageMenu.ProfileManager ] = ( typeof( ProfileManagerView ), "ProfileManager" ),
		[ NavigatePageMenu.JsonEditor ] = ( typeof( JsonEditorView ), "DynamicJsonForm" ),
		[ NavigatePageMenu.Settings ] = ( typeof( ApplicationSettingsView ), "AppSettings" ),
	};

	public HomeView()
	{
		InitializeComponent();
		AppComposition.Navigation.Attach( this );
		HomeNavigator.SelectedItem = ItemFor( NavigatePageMenu.ProfileManager );
	}

	/// <summary>The frame that hosts the page selected in the navigation menu.</summary>
	internal Frame ContentFrame => NavFrame;

	/// <summary>Selects the page's menu item, which shows the page.</summary>
	internal void Select( NavigatePageMenu page )
		=> HomeNavigator.SelectedItem = page == NavigatePageMenu.Settings
			? HomeNavigator.SettingsItem
			: ItemFor( page ) ?? ItemFor( NavigatePageMenu.ProfileManager );

	private NavigationViewItem? ItemFor( NavigatePageMenu page )
		=> HomeNavigator.MenuItems.OfType<NavigationViewItem>().FirstOrDefault( item => item.Tag as string == page.ToString() );

	private void HomeNavigator_SelectionChanged( NavigationView sender, NavigationViewSelectionChangedEventArgs args )
	{
		if( args.IsSettingsSelected )
			Show( NavigatePageMenu.Settings );
		else if( args.SelectedItem is NavigationViewItem { Tag: string tag } && Enum.TryParse( tag, out NavigatePageMenu page ) )
			Show( page );
	}

	private void Show( NavigatePageMenu page )
	{
		if( page == NavigatePageMenu.Configuration )
		{
			ShowConfiguration();
			return;
		}
		if( page == NavigatePageMenu.HackFileManager && !AppComposition.Session.IsLoggedIn )
		{
			Navigate( typeof( NotLoggedInView ), "NotLoggedIn" );
			return;
		}
		var (type, config) = Pages[ page ];
		Navigate( type, config );
	}

	private void Navigate( Type page, string config )
	{
		NavFrame.Navigate( page );
		if( HackApp.Window is { } window )
			WindowHelper.SetWindowConfig( window, WindowConfig.Preset( config ) );
	}

	/// <summary>The Odoo and local settings pages, one above the other.</summary>
	private void ShowConfiguration()
		// navigated to like every other page, so the page it replaces is told it was left
		=> Navigate( typeof( ConfigurationView ), "ConfigSettings" );

	private void HomeNavigator_BackRequested( NavigationView sender, NavigationViewBackRequestedEventArgs args )
	{
		if( NavFrame.CanGoBack )
			NavFrame.GoBack();
	}
}
