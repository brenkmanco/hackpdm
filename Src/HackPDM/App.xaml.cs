using System;
using System.IO;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.Core.Theming;
using HackPDM.Shell;
using HackPDM.Themes;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM;

/// <summary>Application entry point, crash handlers, startup theme setup, and main window lifecycle.</summary>
public sealed partial class HackApp : Application
{
	public static HomeView? HomePage;
	public static Window? Window;
	public static Frame? RootFrame;

	public HackApp()
	{
		InitializeComponent();
		RegisterCrashHandlers();
		var (palette, form) = ThemeService.ReadSavedTheme();
		RequestedTheme = ThemePresets.Palette( palette ).IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
		ThemeService.ApplyStartupResources( Resources, palette, form );
		if( !string.IsNullOrEmpty( PdmConstants.TemporaryPath ) )
			Directory.CreateDirectory( PdmConstants.TemporaryPath );
	}

	private void RegisterCrashHandlers()
	{
		UnhandledException += ( _, e ) =>
		{
			try
			{
				string? dump = HackLogger.WriteCrashDump(
					"App.UnhandledException",
					e.Exception,
					ClientUpdate.CurrentText,
					context: $"ActivePage={HomePage?.GetType().Name ?? "none"}" );
				HackLogger.Critical( "App", $"Fatal UI unhandled exception; crash dump: {dump ?? "(unavailable)"}" );
			}
			catch { }
			TerminateOnCrash();
		};

		AppDomain.CurrentDomain.UnhandledException += ( _, e ) =>
		{
			try
			{
				HackLogger.WriteCrashDump(
					"AppDomain.UnhandledException",
					e.ExceptionObject as Exception,
					ClientUpdate.CurrentText,
					context: $"IsTerminating={e.IsTerminating}" );
			}
			catch { }
			TerminateOnCrash();
		};

		TaskScheduler.UnobservedTaskException += ( _, e ) =>
		{
			HackLogger.Error( "TaskScheduler", "Unobserved task exception", e.Exception );
			e.SetObserved();
		};
	}

	private static void TerminateOnCrash()
	{
		try { AppComposition.LocalFiles.Dispose(); } catch { }
		Environment.Exit( 1 );
	}

	protected override void OnLaunched( LaunchActivatedEventArgs args )
	{
		Window = new MainWindow();
		Window.SetWindowType( AppWindowPresenterKind.Overlapped );
		Window.Closed += Window_Closed;
		Window.Activate();

		AppComposition.Initialize( Window.DispatcherQueue );
		_ = AppComposition.Settings;
		AppComposition.InitializeDefaults();
		HackLogger.Info( "App", $"HackPDM {ClientUpdate.CurrentText} started (UI config loaded={UIStorage.GlobalData.IsLoadedFromFile}, logs={HackLogger.LogDirectory})" );

		HomePage = new HomeView();
		RootFrame = new Frame { Content = HomePage };
		Window.Content = RootFrame;
		AppComposition.Theme.Attach( Window );

#if SMOKE_TESTS
		if( HackPDM.SmokeTests.SmokeTestRunner.IsEnabled )
			HackPDM.SmokeTests.SmokeTestRunner.Start();
#endif
	}

	private void Window_Closed( object sender, WindowEventArgs args )
	{
		try { AppComposition.LocalFiles.Dispose(); } catch { }
		try { UIStorage.Save(); } catch { }
#if SMOKE_TESTS
		if( HackPDM.SmokeTests.SmokeTestRunner.IsEnabled )
			return;
#endif
		Environment.Exit( 0 );
	}
}
