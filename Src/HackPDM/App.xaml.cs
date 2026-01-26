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
using HackPDM.UI.Forms.Odoo;
using HackPDM.UI.Forms.Hack;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.Hack;
using HackPDM.UI.Forms.FormTransport;
using HackPDM.UI.Forms.Helper;
using Microsoft.UI.Dispatching;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class HackApp : Application
{
    public static Home? HomePage;
	public static IServiceProvider? Services { get; private set; }
    public static Window? Window;
    public static Frame? RootFrame;
    public static DispatcherQueue DispatcherQueue;
    public static CoreSettings CoreSettings;

	// To fix CS0121, fully qualify the InitializeComponent() call to specify the correct method.
	// If your project has both a generated partial method and a user-defined method, use the global:: prefix.

	public HackApp()
    {
        InitializeComponent();
        Setup();
    }

    private void ConfigureServices()
    {
	    var services = new ServiceCollection();

        services.AddSingleton<Home>();

        services.AddSingleton<ModernSettingsProvider>();
        services.AddSingleton<ISettingsProvider>(provider 
            => new CoreSettings(provider.GetRequiredService<ModernSettingsProvider>()));
        services.AddSingleton<IHackDefaults, HackDefaults>();
        services.AddSingleton<IOdooDefaults, OdooDefaults>();

        services.AddSingleton<TreeHelp>();
        services.AddSingleton<GridHelp>();

	    services.AddTransient<ProfileManager>();
        services.AddTransient<MessageBox>();
        services.AddTransient<HackFileManager>();

		Services =  services.BuildServiceProvider();

        Services.GetRequiredService<IHackDefaults>();
        Services.GetRequiredService<IOdooDefaults>();

        HomePage = Services.GetRequiredService<Home>();
        CoreSettings = Services.GetRequiredService<ISettingsProvider>() as CoreSettings;
	}

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ConfigureServices();

		Window = new MainWindow();
		Window.SetWindowType(AppWindowPresenterKind.Overlapped);
		var rootFrame = new Frame();
        Window.Activate();
        Window.Content = rootFrame;

        DispatcherQueue = Window.DispatcherQueue;
        rootFrame.Content = HomePage;
    }
    private static void Setup ()
    {
		if (StorageBox.TemporaryPath == null) return;
        if (!Directory.Exists(StorageBox.TemporaryPath)) Directory.CreateDirectory(StorageBox.TemporaryPath);
		//Notifier.FileCheckLoop(); // start file check loop in background
	}
}