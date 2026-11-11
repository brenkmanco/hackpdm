using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

using HackPDM.Shared.GlobalData;
using HackPDM.Mvvm.Views.Common.Controls;

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
using HackPDM.Shell;
using HackPDM.Mvvm.Views.Odoo;
using HackPDM.Mvvm.Views.Settings;
using HackPDM.Mvvm.Views.Tools;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM.Mvvm.Views.Hack;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class HomeView : Page
{
    private static NavigationView? _Navigator { get; set; }
    private static Dictionary<NavigatePageMenu, NavigationViewItem?> _NavItems { get; set; } = [];
    private static NavigationViewItem? _SelectNav { get; set; }
    public HomeView()
    {
        InitializeComponent();
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
    /// <summary>The frame that hosts the page selected in the navigation menu.</summary>
    internal Frame ContentFrame => NavFrame;
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
            NavFrame.Navigate( typeof( ApplicationSettingsView ) );
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

					odooFrame.Navigate(typeof(OdooSettingsView));
                    hackFrame.Navigate(typeof(HackSettingsView));                    
                    
                    WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig("ConfigSettings")!);
                    break;
                }
            case nameof(ProfileNav):
            {
                NavFrame.Navigate(typeof(ProfileManagerView));
				WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig("ProfileManager"));

				break;
            }
            case nameof(HackNav):
            {
				if (!ProfileManagerView.IsLoggedIn)
                {
                    NavFrame.Navigate(typeof(NotLoggedInView));
					WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig("NotLoggedIn"));
					return;
                }

                NavFrame.Navigate(typeof(HackFileManagerView));
				WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig("HackFileManager"));
				break;
            }
            case nameof(JsonEditNav):
            {
                NavFrame.Navigate(typeof(JsonEditorView));
                WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig("DynamicJsonForm"));
                break;
            }
        }
	}

	private void HomeNavigator_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
	{
        Debug.WriteLine("Back requested");
	}
}
