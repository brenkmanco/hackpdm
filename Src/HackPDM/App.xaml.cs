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


using HackPDM.Core.Hack;
using HackPDM.Core.Logging;
using HackPDM.Infrastructure.Odoo;
using HackPDM.Infrastructure.Operations;
using HackPDM.Domain.OdooModels;
using HackPDM.Domain.Hack;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.Views.Dialogs;
using Microsoft.UI.Dispatching;
using HackPDM.Mvvm.Views.Common.Settings;
using System.Threading.Tasks;
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
    public static Window? Window;
    public static Frame? RootFrame;
    public static CoreSettings CoreSettings => AppComposition.Settings;

	// To fix CS0121, fully qualify the InitializeComponent() call to specify the correct method.
	// If your project has both a generated partial method and a user-defined method, use the global:: prefix.

	public HackApp()
    {
		InitializeComponent();
		RegisterCrashHandlers();
		// menus, flyouts and dialogs follow the application theme, and controls take their corners and
		// density from the resources, all of which can only be set this early
		var (palette, form) = HackPDM.Mvvm.Views.Common.Theming.ThemeService.ReadSavedTheme();
		RequestedTheme = HackPDM.Core.Theming.ThemePresets.Palette(palette).IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
		HackPDM.Mvvm.Views.Common.Theming.ThemeService.ApplyStartupResources(Resources, palette, form);
        Setup();
    }

    private void RegisterCrashHandlers()
    {
        UnhandledException += (_, e) =>
        {
            string? dump = HackLogger.WriteCrashDump(
                "App.UnhandledException",
                e.Exception,
                ClientUpdate.CurrentText,
                context: $"ActivePage={HomePage?.GetType().Name ?? "none"}" );
            HackLogger.Critical( "App", $"Fatal UI unhandled exception; crash dump: {dump ?? "(unavailable)"}" );
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Exception? ex = e.ExceptionObject as Exception;
            HackLogger.WriteCrashDump(
                "AppDomain.UnhandledException",
                ex,
                ClientUpdate.CurrentText,
                context: $"IsTerminating={e.IsTerminating}" );
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            HackLogger.Error( "TaskScheduler", "Unobserved task exception", e.Exception );
            e.SetObserved();
        };
    }

    /// <summary>Loads the settings, then shows the home page in the main window.</summary>
    private static void ConfigureServices()
    {
        _ = AppComposition.Settings; // load the saved settings before the defaults
        AppComposition.InitializeDefaults();
        HackLogger.Info( "App", $"HackPDM {ClientUpdate.CurrentText} started (UI config loaded={UIStorage.GlobalData.IsLoadedFromFile}, logs={HackLogger.LogDirectory})" );
        HomePage = new HomeView();

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

        AppComposition.Initialize(Window.DispatcherQueue);
		ConfigureServices();
		AppComposition.Theme.Attach(Window);

        // HACKPDM_SMOKE_TEST=1: drive login -> navigation -> server round trips, then exit (CI)
        if (HackPDM.Shell.Testing.SmokeTestRunner.IsEnabled)
            HackPDM.Shell.Testing.SmokeTestRunner.Start();
    }

	private void Window_Closed( object sender, WindowEventArgs args )
	{
		AppComposition.LocalFiles.Dispose();
        AppConfigStore.Save( UIStorage.GlobalData );
	}

	private static void Setup ()
    {
		if (StorageBox.TemporaryPath == null) return;
        if (!Directory.Exists(StorageBox.TemporaryPath)) Directory.CreateDirectory(StorageBox.TemporaryPath);
		//Notifier.FileCheckLoop(); // start file check loop in background
	}
}