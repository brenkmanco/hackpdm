using System;
using System.IO;
using HackPDM.Abstractions;
using HackPDM.Core.Configuration;
using HackPDM.Shared.GlobalData;
using HackPDM.UI.Compatibility;
using HackPDM.UI.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;


using ProfileManager = HackPDM.UI.Forms.Settings.ProfileManager;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class HackApp : Application
{
	public static IServiceProvider? Services { get; private set; }
    public static Window? Window;
    public static Frame? RootFrame;
    
    // To fix CS0121, fully qualify the InitializeComponent() call to specify the correct method.
    // If your project has both a generated partial method and a user-defined method, use the global:: prefix.

    public HackApp()
    {
        InitializeComponent();
        ConfigureServices();
        Setup();
    }

    private void ConfigureServices()
    {
	    var services = new ServiceCollection();
	    
	    services.AddSingleton<ISettingsProvider, ModernSettingsProvider>();
	    services.AddSingleton<ISettingsProvider>(provider =>
	    {
		    var inner = provider.GetRequiredService<ISettingsProvider>();
		    return new CoreSettings(inner);
	    });
	    

	    Services =  services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
		Window.SetWindowType(AppWindowPresenterKind.Overlapped);
		var rootFrame = new Frame();
        Window.Activate();
        Window.Content = rootFrame;
        rootFrame.Navigate(typeof(ProfileManager));
    }
    private static void Setup ()
    {
		if (StorageBox.TemporaryPath == null) return;
        if (!Directory.Exists(StorageBox.TemporaryPath)) Directory.CreateDirectory(StorageBox.TemporaryPath);
		//Notifier.FileCheckLoop(); // start file check loop in background
	}
}