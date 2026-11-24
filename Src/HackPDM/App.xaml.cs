using System;
using System.IO;
using System.Threading;
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

	private bool _closeSaveCompleted;
	private bool _closeSaveInProgress;

	public HackApp()
	{
		InitializeComponent();
#if !WINDOWS
		SupplementalBindableMetadataProvider.Install();
#endif
		RegisterCrashHandlers();
		var (palette, form) = ThemeService.ReadSavedTheme();
		RequestedTheme = ThemePresets.Palette( palette ).IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
		ThemeService.ApplyStartupResources( Resources, palette, form );
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
					context: $"ActivePage={HomePage?.ContentFrame.CurrentSourcePageType?.Name ?? "none"}" );
				HackLogger.Critical( "App", $"Fatal UI unhandled exception; crash dump: {dump ?? "(unavailable)"}" );
			}
			catch { }
			TerminateIfNotSmokeTest();
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
			TerminateIfNotSmokeTest();
		};

		TaskScheduler.UnobservedTaskException += ( _, e ) =>
		{
			HackLogger.Error( "TaskScheduler", "Unobserved task exception", e.Exception );
			e.SetObserved();
		};
	}

	private static bool IsSmokeTestActive()
	{
#if SMOKE_TESTS
		return HackPDM.SmokeTests.SmokeTestRunner.IsEnabled;
#else
		return false;
#endif
	}

	private static void StartSmokeTestsIfEnabled()
	{
#if SMOKE_TESTS
		if( HackPDM.SmokeTests.SmokeTestRunner.IsEnabled )
			HackPDM.SmokeTests.SmokeTestRunner.Start();
#endif
	}

	private static void TerminateIfNotSmokeTest()
	{
		if( IsSmokeTestActive() )
			return;
		try { AppComposition.DisposeLocalFilesIfCreated(); } catch { }
		Environment.Exit( 1 );
	}

	protected override void OnLaunched( LaunchActivatedEventArgs args )
	{
		MainWindow mainWindow = new();
		Window = mainWindow;
		Window.SetWindowType( AppWindowPresenterKind.Overlapped );
		if( Window.AppWindow is { } appWindow )
			appWindow.Closing += MainWindow_Closing;
		Window.Closed += Window_Closed;
		Window.Activate();

		AppComposition.Initialize( Window.DispatcherQueue );
		_ = AppComposition.Settings;
		AppComposition.InitializeDefaults();
		if( !string.IsNullOrEmpty( PdmConstants.TemporaryPath ) )
		{
			string tempPath = PdmConstants.TemporaryPath;
			_ = Task.Run( () =>
			{
				try { Directory.CreateDirectory( tempPath ); }
				catch( Exception ex ) { HackLogger.Warn( "App", $"Could not create temporary directory '{tempPath}': {ex.Message}", ex ); }
			} );
		}
		HackLogger.Info( "App", $"HackPDM {ClientUpdate.CurrentText} started (UI config loaded={UIStorage.GlobalData.IsLoadedFromFile}, logs={HackLogger.LogDirectory})" );

		HomePage = new HomeView();
		mainWindow.ContentFrame.Content = HomePage;
		AppComposition.Theme.Attach( Window );

		StartSmokeTestsIfEnabled();
	}

	private async void MainWindow_Closing( AppWindow sender, AppWindowClosingEventArgs args )
	{
		if( _closeSaveCompleted || IsSmokeTestActive() )
			return;

		args.Cancel = true;
		if( _closeSaveInProgress )
			return;

		_closeSaveInProgress = true;
		try
		{
			await Task.Run( UIStorage.SaveSafe );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "App", $"Could not save UI config on close: {ex.Message}", ex );
		}
		finally
		{
			_closeSaveCompleted = true;
			_closeSaveInProgress = false;
		}
		Window?.Close();
	}

	private void Window_Closed( object sender, WindowEventArgs args )
	{
		try { AppComposition.DisposeLocalFilesIfCreated(); } catch { }
		if( IsSmokeTestActive() )
		{
			if( !_closeSaveCompleted )
			{
				_closeSaveCompleted = true;
				_ = Task.Run( UIStorage.SaveSafe );
			}
			return;
		}

		if( _closeSaveCompleted )
		{
			Environment.Exit( 0 );
			return;
		}

		// Fallback if Closed arrived without AppWindow.Closing: flush on a foreground thread before exiting.
		_closeSaveCompleted = true;
		Thread saveThread = new( static () =>
		{
			UIStorage.SaveSafe();
			Environment.Exit( 0 );
		} )
		{
			IsBackground = false,
			Name = "UIStorageShutdownSave",
		};
		saveThread.Start();
	}
}
