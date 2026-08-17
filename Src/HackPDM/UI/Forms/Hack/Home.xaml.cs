using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

using HackPDM.Shared.GlobalData;
using HackPDM.UI.Controls;
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
    private static NavigationView? Navigator { get; set; }
    private static NavigationViewItem? HackNav {  get; set; }
    private static NavigationViewItem? ProfileNav { get; set; }
    private static NavigationViewItem? ConfigNav { get; set; }
    private static MainWindow? Window { get; set; }
    public Home()
    {
        InitializeComponent();
        Window = HackApp.Window as MainWindow;
        Navigator = HomeNavigator;
        var navmenu = HomeNavigator.MenuItems;
        HackNav = navmenu[0] as NavigationViewItem;
        // navmenu[1] is the settings header
        ProfileNav = navmenu[2] as NavigationViewItem;
        ConfigNav = navmenu[3] as NavigationViewItem;

        Navigator.SelectedItem = ProfileNav;
    }
    public static void NavigateToPage(NavigatePageMenu pageMenu)
    {
		switch (pageMenu)
		{
			case NavigatePageMenu.HackFileManager:
                Navigator?.SelectedItem = HackNav;
				break;
			case NavigatePageMenu.ProfileManager:
                Navigator?.SelectedItem = ProfileNav;
				break;
			case NavigatePageMenu.Configuration:
                Navigator?.SelectedItem = ConfigNav;
				break;
			case NavigatePageMenu.Settings:
				//Navigator?.SelectedItem
				break;
			default:
				break;
		}
	}
	private void HomeNavigator_SelectionChanged( NavigationView sender, NavigationViewSelectionChangedEventArgs args )
	{
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate( typeof( ApplicationSettingsPage ) );
            return;
        }
        if (args.SelectedItem is not NavigationViewItem nvi) return;

        switch (nvi.Content)
        {
            case "Configurations":
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
            case "Profile Manager":
                {
                    NavFrame.Content = InstanceManager.GetAPage<ProfileManager>();
					WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig(nameof(ProfileManager)));

					break;
                }
            case "Hack File Manager":
                {
					if (!ProfileManager.IsLoggedIn)
                    {
                        NavFrame.Content = InstanceManager.GetAPage<NotLoggedIn>();
						WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig(nameof(NotLoggedIn)));
						return;
                    }
                    if (!InstanceManager.TryGet<HackFileManager>(out var manager))
                    {
                        manager = HackApp.Services?.GetRequiredService<HackFileManager>();
                        InstanceManager.Register(manager);
                    }

                    if (manager?.HackLoaded is false) manager.LoadHackMan();
                    NavFrame.Content = manager;
					WindowHelper.SetWindowConfig(HackApp.Window, InstanceManager.GetConfig(nameof(HackFileManager)));
					break;
                }
        }
	}

	private void HomeNavigator_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
	{
        Debug.WriteLine("Back requested");
	}
}
