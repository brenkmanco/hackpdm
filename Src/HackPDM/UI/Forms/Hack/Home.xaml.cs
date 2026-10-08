using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
using HackPDM.UI.Forms.Helper;
using HackPDM.UI.Forms.Odoo;
using HackPDM.UI.Forms.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.UI.Forms.Hack;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class Home : Page
{
    private static NavigationView? _Navigator { get; set; }
    private static Dictionary<NavigatePageMenu, NavigationViewItem?> _NavItems { get; set; } = [];
    private static NavigationViewItem? _SelectNav { get; set; }
    private static MainWindow? Window { get; set; }
    public Home()
    {
        InitializeComponent();
        Window = HackApp.Window as MainWindow;
        _Navigator = HomeNavigator;
        _SelectNav = ProfileNav;
        _NavItems = new Dictionary<NavigatePageMenu, NavigationViewItem?>(
            HomeNavigator.MenuItems.OfType<NavigationViewItem?>().Select(navItem =>
            {
                return navItem?.Name switch
                {
                    nameof(HackNav) => new KeyValuePair<NavigatePageMenu, NavigationViewItem?>(NavigatePageMenu.HackFileManager, HackNav),
                    nameof(ProfileNav) => new KeyValuePair<NavigatePageMenu, NavigationViewItem?>(NavigatePageMenu.ProfileManager, ProfileNav),
                    nameof(HackOdooNav) => new KeyValuePair<NavigatePageMenu, NavigationViewItem?>(NavigatePageMenu.Configuration, HackOdooNav),
                    nameof(JsonEditNav) => new KeyValuePair<NavigatePageMenu, NavigationViewItem?>(NavigatePageMenu.JsonEditor, JsonEditNav),
                    _ => new KeyValuePair<NavigatePageMenu, NavigationViewItem?>(NavigatePageMenu.Settings, null!),
                };
            })
        );
        
        _Navigator.SelectedItem = _NavItems[NavigatePageMenu.ProfileManager];
    }
    public static void NavigateToPage(NavigatePageMenu pageMenu)
    {
		switch (pageMenu)
		{
			case NavigatePageMenu.HackFileManager:
				_SelectNav = _NavItems[NavigatePageMenu.HackFileManager];
				break;
			case NavigatePageMenu.ProfileManager:
				_SelectNav = _NavItems[NavigatePageMenu.ProfileManager];
				break;
			case NavigatePageMenu.Configuration:
				_SelectNav = _NavItems[NavigatePageMenu.Configuration];
				break;
			case NavigatePageMenu.JsonEditor:
				_SelectNav = _NavItems[NavigatePageMenu.JsonEditor];
				break;
			case NavigatePageMenu.Settings:
				//Navigator?.SelectedItem
				break;
			default:
				break;
		}
        _Navigator?.SelectedItem = _SelectNav ?? _NavItems[NavigatePageMenu.ProfileManager];
	}
	private void HomeNavigator_SelectionChanged( NavigationView sender, NavigationViewSelectionChangedEventArgs args )
	{
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate( typeof( ApplicationSettingsPage ) );
            return;
        }
        if (args.SelectedItem is not NavigationViewItem nvi) return;

        switch (nvi.Name)
        {
            case nameof(HackOdooNav):
                {
                    //var odooSetPage = InstanceManager.GetAPage<OdooSettings>();
                    //var hackSetPage = InstanceManager.GetAPage<HackSettings>();

                    StackPanel stackSettings = new();
                    ScrollView scrollView = new();

                    Frame odooFrame = new();
                    Frame hackFrame = new();

                    NavFrame.Content = scrollView;
                    scrollView.Content = stackSettings;

                    stackSettings.Children.Add(odooFrame);
                    stackSettings.Children.Add(hackFrame);

                    ArrayList param =
					[
						HackApp.CoreSettings,
                    ];

					odooFrame.Navigate(typeof(OdooSettings));
                    hackFrame.Navigate(typeof(HackSettings));                    
                    
                    WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig("ConfigSettings")!);
                    break;
                }
            case nameof(ProfileNav):
            {
                NavFrame.Navigate(typeof(ProfileManager));
				WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig(nameof(ProfileManager)));

				break;
            }
            case nameof(HackNav):
            {
				if (!ProfileManager.IsLoggedIn)
                {
                    NavFrame.Navigate(typeof(NotLoggedIn));
					WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig(nameof(NotLoggedIn)));
					return;
                }

                NavFrame.Navigate(typeof(HackFileManager));
				WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig(nameof(HackFileManager)));
				break;
            }
            case nameof(JsonEditNav):
            {
                NavFrame.Navigate(typeof(DynamicJsonForm));
                WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig(nameof(DynamicJsonForm)));
                break;
            }
        }
	}

	private void HomeNavigator_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
	{
        Debug.WriteLine("Back requested");
	}
}
