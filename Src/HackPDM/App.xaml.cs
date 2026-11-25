using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;
using HackPDM.Core.Services;
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
		ThemeSettings savedTheme = ThemeService.ReadSavedSettings();
		var (palette, form) = ThemeService.StartupTheme( savedTheme );
		bool dark = ThemePresets.Palette( palette ).IsDark;
		RequestedTheme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
		ThemeService.ApplyStartupResources( Resources, palette, form, savedTheme.OverridesFor( dark ) );
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
		AppComposition.Initialize( Window.DispatcherQueue );
		_ = AppComposition.Settings;
		AppComposition.InitializeDefaults();

		Window.SetWindowType( AppWindowPresenterKind.Overlapped );
		if( Window.AppWindow is { } appWindow )
			appWindow.Closing += MainWindow_Closing;
		Window.Closed += Window_Closed;

		WindowPreset startupPreset = WindowHelper.ApplyMainWindowStartupState( Window, WindowHelper.Presets.ProfileManager );
		WindowHelper.TrackWindow( Window, WindowHelper.Presets.MainWindow );
		Window.Activate();
		WindowHelper.FinishActivation( Window, startupPreset );

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
		if( AppComposition.TryGetWorkflow( out IPdmWorkflowService? workflow ) && workflow is not null )
		{
			if( workflow.HasActiveCommit )
			{
				bool confirm = await AppComposition.Dialogs.ConfirmAsync(
					"A commit operation is currently running or paused. Closing now will cancel the commit and roll back uncommitted server staging.\n\nClose anyway?",
					"Commit In Progress",
					MessageBoxIcon.Warning );
				if( !confirm )
				{
					_closeSaveInProgress = false;
					return;
				}
			}

			try
			{
				bool waitCommitCleanup = await Task.Run( workflow.PrepareForShutdown );
				AppComposition.Reporter.CancelSilent();
				if( waitCommitCleanup )
					await workflow.WaitActiveCommitCleanupAsync( TimeSpan.FromSeconds( 3 ) );
			}
			catch( Exception ex )
			{
				HackLogger.Warn( "App", $"Could not flush active workflow state on close: {ex.Message}", ex );
			}
		}

		try
		{
			HomePage?.CaptureLayoutState();
			WindowHelper.CaptureAllTrackedWindows();
		}
		catch( Exception ex )
		{
			HackLogger.Warn( "App", $"Could not capture window/layout state on close: {ex.Message}", ex );
		}

		try
		{
			await Task.Run( UIStorage.Flush );
			await HackLogger.FlushAsync();
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
				try
				{
					HomePage?.CaptureLayoutState();
					WindowHelper.CaptureAllTrackedWindows();
				}
				catch { }
				_ = Task.Run( UIStorage.Flush );
			}
			return;
		}

		if( _closeSaveCompleted )
		{
			Environment.Exit( 0 );
			return;
		}

		// Fallback if Closed arrived without AppWindow.Closing: capture & flush synchronously before exiting.
		try
		{
			if( AppComposition.TryGetWorkflow( out IPdmWorkflowService? workflow ) && workflow is not null )
			{
				workflow.PrepareForShutdown();
				AppComposition.Reporter.CancelSilent();
			}
		}
		catch { }
		try
		{
			HomePage?.CaptureLayoutState();
			WindowHelper.CaptureAllTrackedWindows();
		}
		catch { }
		_closeSaveCompleted = true;
		Thread saveThread = new( static () =>
		{
			UIStorage.Flush();
			Environment.Exit( 0 );
		} )
		{
			IsBackground = false,
			Name = "UIStorageShutdownSave",
		};
		saveThread.Start();
	}
}
