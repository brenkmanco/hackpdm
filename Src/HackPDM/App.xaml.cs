using System;
using System.IO;
using HackPDM.Abstractions;
using HackPDM.Core.Configuration;
using HackPDM.Shared.GlobalData;
using HackPDM.Shell;
using HackPDM.Mvvm.Views.Common.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;


using ProfileManager = HackPDM.Mvvm.Views.Settings.ProfileManagerView;
using HackPDM.Core.Hack;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.Hack;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Views.Dialogs;
using Microsoft.UI.Dispatching;
using HackPDM.Mvvm.Views.Common.Settings;
using System.Diagnostics;
using HackPDM.Mvvm.Views.Hack;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HackPDM;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class HackApp : Application
{
    public static HomeView? HomePage;
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

    private static void ConfigureServices()
    {
	    var services = new ServiceCollection();

        services.AddSingleton<HomeView>();

        services.AddSingleton<ModernSettingsProvider>();
        services.AddSingleton<ISettingsProvider>(provider 
            => new CoreSettings(provider.GetRequiredService<ModernSettingsProvider>()));
        services.AddSingleton<IHackDefaults, HackDefaults>();
        services.AddSingleton<IOdooDefaults, OdooDefaults>();

        services.AddSingleton<GridHelp>();

        services.AddSingleton<VMEnv>();
        services.AddSingleton(provider => provider.GetRequiredService<VMEnv>().HFM);

	    services.AddTransient<ProfileManager>();
        services.AddTransient<MessageBox>();
        services.AddTransient<HackFileManagerView>();

		Services =  services.BuildServiceProvider();

        Services.GetRequiredService<IHackDefaults>();
        Services.GetRequiredService<IOdooDefaults>();

        CoreSettings = Services.GetRequiredService<ISettingsProvider>() as CoreSettings;
        Debug.WriteLine( $"Global data loaded from file: {UIStorage.GlobalData.IsLoadedFromFile}" );
        HomePage = Services.GetRequiredService<HomeView>();

		var rootFrame = new Frame();
		Window?.Content = rootFrame;
		rootFrame.Content = HomePage;
	}

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
		Window = new MainWindow();
		Window.SetWindowType(AppWindowPresenterKind.Overlapped);
		Window.Closed += Window_Closed;
        Window.Activate();

        DispatcherQueue = Window.DispatcherQueue;
        AppComposition.Initialize();
		ConfigureServices();

        // HACKPDM_SMOKE_TEST=1: drive login -> navigation -> server round trips, then exit (CI)
        if (HackPDM.Shell.Testing.SmokeTestRunner.IsEnabled)
            HackPDM.Shell.Testing.SmokeTestRunner.Start();
    }

	private void Window_Closed( object sender, WindowEventArgs args )
	{
        UIStorage.GlobalData.SaveToFile();
	}

	private static void Setup ()
    {
		if (StorageBox.TemporaryPath == null) return;
        if (!Directory.Exists(StorageBox.TemporaryPath)) Directory.CreateDirectory(StorageBox.TemporaryPath);
		//Notifier.FileCheckLoop(); // start file check loop in background
	}
}