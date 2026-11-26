using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using HackPDM.Core.Configuration;
using HackPDM.Core.Pdm;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HackPDM.Common;

public sealed record WindowPreset(
	string Title,
	int Width,
	int Height,
	AppWindowPresenterKind Kind = AppWindowPresenterKind.Overlapped,
	int X = 0,
	int Y = 0,
	bool HasSavedPosition = false,
	bool HasSavedSize = false,
	bool IsMaximized = false,
	int MinWidth = 400,
	int MinHeight = 280 );

public static partial class WindowHelper
{
	private const string DefaultWindowPreset = "TemplateWindow";
	private const long ProgrammaticSuppressMs = 250;
	private const int SaveDebounceMs = 250;

	public static class Presets
	{
		public const string MainWindow = WindowPresetKeys.MainWindow;
		public const string ProfileManager = nameof( NavigatePageMenu.ProfileManager );
		public const string HackFileManager = nameof( NavigatePageMenu.HackFileManager );
		public const string DependencyGraph = nameof( NavigatePageMenu.DependencyGraph );
		public const string ConfigSettings = "ConfigSettings";
		public const string AppSettings = "AppSettings";
		public const string NotLoggedIn = "NotLoggedIn";
		public const string MessageBox = nameof( HackPDM.Common.MessageBox );
		public const string StatusDialogView = nameof( HackPDM.Status.StatusDialogView );
		public const string StatusDialogViewMinimal = WindowPresetKeys.StatusDialogViewMinimal;
		public const string SearchOdooView = nameof( HackPDM.Search.SearchOdooView );
		public const string SearchOdooViewMinimal = WindowPresetKeys.SearchOdooViewMinimal;
		public const string OdooFileTypeManagerView = nameof( HackPDM.Settings.OdooFileTypeManagerView );
		public const string PipWindow = WindowPresetKeys.PipWindow;
	}

	private static readonly ConditionalWeakTable<Page, Window> PageWindows = new();
	private static readonly ConditionalWeakTable<Window, WindowStateTracker> Trackers = new();
	private static readonly List<WeakReference<WindowStateTracker>> ActiveTrackers = [];
	private static readonly WindowSettingsData Defaults = new();
	private static readonly string? AppIconPath = ResolveAppIconPath();

	private static string? ResolveAppIconPath()
	{
		string iconPath = Path.Combine( AppContext.BaseDirectory, "Assets", "hackpdm-icon.ico" );
		return File.Exists( iconPath ) ? iconPath : null;
	}

	private static (int MinWidth, int MinHeight) MinimumDimensionsFor( string name ) => name switch
	{
		WindowPresetKeys.MainWindow
			or WindowPresetKeys.HackFileManager
			or WindowPresetKeys.DependencyGraph => ( 760, 520 ),
		WindowPresetKeys.ProfileManager
			or WindowPresetKeys.ConfigSettings
			or WindowPresetKeys.NotLoggedIn
			or WindowPresetKeys.AppSettings
			or WindowPresetKeys.ApplicationSettingsView
			or WindowPresetKeys.OdooFileTypeManagerView => ( 640, 440 ),
		WindowPresetKeys.StatusDialogView
			or WindowPresetKeys.SearchOdooView => ( 680, 440 ),
		WindowPresetKeys.StatusDialogViewMinimal =>
#if WINDOWS
			( 560, 140 ),
#else
			( 560, 320 ),
#endif
		WindowPresetKeys.SearchOdooViewMinimal => ( 560, 220 ),
		WindowPresetKeys.MessageBox => ( 380, 200 ),
		WindowPresetKeys.PipWindow => ( 260, 200 ),
		_ => ( 480, 320 ),
	};

	public static WindowPreset Preset( string name )
	{
		WindowConfigData? saved = UIStorage.GlobalData.Win.PresetWindowConfig.GetValueOrDefault( name );
		WindowConfigData? fallback = Defaults.PresetWindowConfig.GetValueOrDefault( name );
		(int minW, int minH) = MinimumDimensionsFor( name );

		int defaultW = fallback?.PositionAndSize.z is > 0 ? fallback.PositionAndSize.z : 900;
		int defaultH = fallback?.PositionAndSize.w is > 0 ? fallback.PositionAndSize.w : 620;

		bool savedSizeValid = saved is not null && saved.PositionAndSize.z >= minW && saved.PositionAndSize.w >= minH;
		int width = savedSizeValid ? saved!.PositionAndSize.z : defaultW;
		int height = savedSizeValid ? saved!.PositionAndSize.w : defaultH;

		bool hasSavedSize = saved is { HasSavedSize: true } && savedSizeValid;
		bool hasSavedPos = saved is { HasSavedPosition: true };
		bool isMaximized = saved is { IsMaximized: true };
		string title = !string.IsNullOrWhiteSpace( saved?.Title )
			? saved.Title
			: ( fallback?.Title ?? "Window" );
		WindowKind kind = saved?.WindowKind ?? fallback?.WindowKind ?? WindowKind.Overlapped;

		return new WindowPreset(
			Title: title,
			Width: width,
			Height: height,
			Kind: ToPresenterKind( kind ),
			X: hasSavedPos ? saved!.PositionAndSize.x : 0,
			Y: hasSavedPos ? saved!.PositionAndSize.y : 0,
			HasSavedPosition: hasSavedPos,
			HasSavedSize: hasSavedSize,
			IsMaximized: isMaximized,
			MinWidth: minW,
			MinHeight: minH );
	}

	private static AppWindowPresenterKind ToPresenterKind( WindowKind kind ) => kind switch
	{
		WindowKind.CompactOverlay => AppWindowPresenterKind.CompactOverlay,
		WindowKind.FullScreen => AppWindowPresenterKind.FullScreen,
		WindowKind.Default => AppWindowPresenterKind.Default,
		_ => AppWindowPresenterKind.Overlapped,
	};

	public static Window? WindowOf( Page page )
		=> PageWindows.TryGetValue( page, out Window? window ) ? window : null;

	public static void UnregisterPageWindow( Page page ) => PageWindows.Remove( page );

	/// <summary>Applies the bundled HackPDM icon to the window titlebar and taskbar when supported.</summary>
	public static void ApplyAppIcon( Window window )
	{
		if( AppIconPath is null )
			return;
		try
		{
			window.AppWindow?.SetIcon( AppIconPath );
		}
		catch
		{
		}
	}

#if WINDOWS
	[LibraryImport( "user32.dll" )]
	private static partial uint GetDpiForWindow( IntPtr hWnd );

	[LibraryImport( "user32.dll" )]
	private static partial uint GetDpiForSystem();
#endif

	internal static double GetDpiScale( Window window )
	{
#if WINDOWS
		try
		{
			IntPtr hwnd = window.IntPtrHandle;
			uint dpi = hwnd != IntPtr.Zero ? GetDpiForWindow( hwnd ) : 0;
			if( dpi == 0 )
				dpi = GetDpiForSystem();
			if( dpi > 0 )
				return Math.Clamp( dpi / 96.0, 1.0, 4.0 );
		}
		catch
		{
		}
#else
		_ = window;
#endif
		return 1.0;
	}

	internal static int DipToPhysical( int dip, double scale )
		=> (int)Math.Round( Math.Max( 1, dip ) * Math.Max( 1.0, scale ) );

	internal static int PhysicalToDip( int px, double scale )
		=> scale > 0 ? (int)Math.Round( px / scale ) : px;

	private static bool IsWindowMaximized( AppWindow appWindow )
	{
		try
		{
			return appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
		}
		catch
		{
			return false;
		}
	}

	public static void ResizeWindow( Window window, int widthDip, int heightDip )
	{
		if( window.AppWindow is not { } appWindow )
			return;
		if( IsWindowMaximized( appWindow ) )
			return;

		int clampedDipW = Math.Max( 200, widthDip );
		int clampedDipH = Math.Max( 150, heightDip );
		if( Trackers.TryGetValue( window, out WindowStateTracker? tracker ) )
			tracker.BeginProgrammaticChange( ( clampedDipW, clampedDipH ) );

		double scale = GetDpiScale( window );
		int physW = DipToPhysical( clampedDipW, scale );
		int physH = DipToPhysical( clampedDipH, scale );
		var current = appWindow.Position;
		var (targetX, targetY, targetW, targetH) = FitToWorkArea(
			appWindow,
			current,
			current.X,
			current.Y,
			hasSavedPosition: false,
			physW,
			physH );
		if( targetX != current.X || targetY != current.Y )
			appWindow.Move( new Windows.Graphics.PointInt32 { X = targetX, Y = targetY } );
		appWindow.Resize( new Windows.Graphics.SizeInt32 { Width = targetW, Height = targetH } );
		X11WindowSize.TryResize( window, targetW, targetH );
	}

	public static void EnsureMinimumSize( Window window, int minWidthDip, int minHeightDip )
	{
		if( window.AppWindow is not { } appWindow )
			return;
		if( IsWindowMaximized( appWindow ) )
			return;
		if( minWidthDip <= 0 && minHeightDip <= 0 )
			return;

		double scale = GetDpiScale( window );
		var sizePx = appWindow.Size;
		int currentDipW = sizePx.Width > 0 ? PhysicalToDip( sizePx.Width, scale ) : 0;
		int currentDipH = sizePx.Height > 0 ? PhysicalToDip( sizePx.Height, scale ) : 0;
		int targetW = minWidthDip > 0 ? Math.Max( currentDipW, minWidthDip ) : currentDipW;
		int targetH = minHeightDip > 0 ? Math.Max( currentDipH, minHeightDip ) : currentDipH;
		if( targetW > currentDipW || targetH > currentDipH )
			ResizeWindow( window, targetW, targetH );
	}

	public static T CreateWindow<T>( string? presetName = null, bool activated = true, bool withFrame = true, bool persistBounds = true ) where T : Window, new()
	{
		string resolvedKey = presetName ?? DefaultWindowPreset;
		T window = new();
		ApplyAppIcon( window );
		if( withFrame )
			window.Content = new Frame();
		WindowPreset preset = Preset( resolvedKey );
		ApplyPreset( window, preset );
		if( persistBounds )
			TrackWindow( window, resolvedKey );
		if( activated )
		{
			window.Activate();
			FinishActivation( window, preset );
		}
		AppComposition.Theme.Attach( window );
		return window;
	}

	public static void CreateWindowAndPage<T>( out T page, out Window window, bool activated = true, string? presetName = null, bool persistBounds = true ) where T : Page, new()
	{
		page = new T();
		window = CreateWindowForExistingPage( page, activated, presetName, persistBounds );
	}

	public static Window CreateWindowForExistingPage( Page page, bool activated = true, string? presetName = null, bool persistBounds = true )
	{
		string resolvedKey = presetName ?? PresetKeyFor( page );
		Window window = new();
		ApplyAppIcon( window );
		window.Content = new Frame { Content = page };
		WindowPreset preset = Preset( resolvedKey );
		ApplyPreset( window, preset );
		if( persistBounds )
			TrackWindow( window, resolvedKey );
		if( activated )
		{
			window.Activate();
			FinishActivation( window, preset );
		}
		AppComposition.Theme.Attach( window );
		PageWindows.AddOrUpdate( page, window );
		return window;
	}

	private static string PresetKeyFor( Page page ) => page switch
	{
		HackPDM.Status.StatusDialogView => Presets.StatusDialogView,
		HackPDM.Search.SearchOdooView => Presets.SearchOdooView,
		HackPDM.Settings.OdooFileTypeManagerView => Presets.OdooFileTypeManagerView,
		HackPDM.Settings.ApplicationSettingsView => Presets.AppSettings,
		HackPDM.FileManager.HackFileManagerView => Presets.HackFileManager,
		HackPDM.Graph.DependencyGraphView => Presets.DependencyGraph,
		HackPDM.Login.ProfileManagerView => Presets.ProfileManager,
		HackPDM.Settings.ConfigurationView => Presets.ConfigSettings,
		HackPDM.Shell.NotLoggedInView => Presets.NotLoggedIn,
		HackPDM.Common.MessageBox => Presets.MessageBox,
		_ => DefaultWindowPreset,
	};

	public static void ApplyPreset( Window window, WindowPreset preset, bool forceRestoreIfMaximized = false )
	{
		window.Title = preset.Title;
		window.SetWindowType( preset.Kind );
		if( window.AppWindow is not { } appWindow )
			return;

		if( Trackers.TryGetValue( window, out WindowStateTracker? tracker ) )
			tracker.BeginProgrammaticChange( ( preset.Width, preset.Height ) );

		if( appWindow.Presenter is OverlappedPresenter overlapped )
		{
			try
			{
				if( overlapped.State == OverlappedPresenterState.Maximized )
				{
					if( !forceRestoreIfMaximized )
						return;
					overlapped.Restore();
				}
			}
			catch
			{
			}
		}

		double scale = GetDpiScale( window );
		int physW = DipToPhysical( preset.Width, scale );
		int physH = DipToPhysical( preset.Height, scale );
		var current = appWindow.Position;
		var (targetX, targetY, targetW, targetH) = FitToWorkArea(
			appWindow,
			current,
			preset.X,
			preset.Y,
			preset.HasSavedPosition,
			physW,
			physH );
		if( targetX != current.X || targetY != current.Y )
			appWindow.Move( new Windows.Graphics.PointInt32 { X = targetX, Y = targetY } );
		appWindow.Resize( new Windows.Graphics.SizeInt32 { Width = targetW, Height = targetH } );
		X11WindowSize.TryResize( window, targetW, targetH );

		if( preset.IsMaximized && appWindow.Presenter is OverlappedPresenter { IsMaximizable: true } maxPresenter )
		{
			try { maxPresenter.Maximize(); }
			catch { }
		}
	}

	public static WindowPreset ApplyMainWindowStartupState( Window window, string initialPagePresetKey )
	{
		WindowPreset mainPreset = Preset( Presets.MainWindow );
		WindowPreset pagePreset = Preset( initialPagePresetKey );

		WindowPreset target = mainPreset.HasSavedSize || mainPreset.HasSavedPosition || mainPreset.IsMaximized
			? mainPreset with
			{
				Title = pagePreset.Title,
				Width = mainPreset.HasSavedSize ? mainPreset.Width : pagePreset.Width,
				Height = mainPreset.HasSavedSize ? mainPreset.Height : pagePreset.Height,
				MinWidth = Math.Max( mainPreset.MinWidth, pagePreset.MinWidth ),
				MinHeight = Math.Max( mainPreset.MinHeight, pagePreset.MinHeight ),
			}
			: pagePreset;

		ApplyPreset( window, target );
		return target;
	}

	public static void FinishActivation( Window window, WindowPreset preset )
	{
		if( window.AppWindow is not { } appWindow )
			return;

		if( Trackers.TryGetValue( window, out WindowStateTracker? tracker ) )
			tracker.BeginProgrammaticChange( ( preset.Width, preset.Height ) );

		if( !preset.IsMaximized )
		{
			double scale = GetDpiScale( window );
			int physW = DipToPhysical( preset.Width, scale );
			int physH = DipToPhysical( preset.Height, scale );
			var current = appWindow.Position;
			var (_, _, targetW, targetH) = FitToWorkArea(
				appWindow,
				current,
				preset.X,
				preset.Y,
				preset.HasSavedPosition,
				physW,
				physH );
			X11WindowSize.TryResize( window, targetW, targetH );
			return;
		}

		if( appWindow.Presenter is OverlappedPresenter { IsMaximizable: true } maxPresenter )
		{
			try
			{
				if( maxPresenter.State != OverlappedPresenterState.Maximized )
					maxPresenter.Maximize();
			}
			catch
			{
			}
		}
	}

	public static void ApplyMainWindowPagePreset( Window window, string pagePresetKey, int minWidthDip = 0, int minHeightDip = 0 )
	{
		WindowPreset pagePreset = Preset( pagePresetKey );
		window.Title = pagePreset.Title;
		if( window.AppWindow is not { } appWindow )
			return;
		if( IsWindowMaximized( appWindow ) )
			return;

		WindowConfigData? mainCfg = UIStorage.GlobalData.Win.PresetWindowConfig.GetValueOrDefault( Presets.MainWindow );
		bool userPinnedSize = mainCfg is { HasSavedSize: true };

		int reqMinW = Math.Max( pagePreset.MinWidth, minWidthDip );
		int reqMinH = Math.Max( pagePreset.MinHeight, minHeightDip );

		if( userPinnedSize )
		{
			EnsureMinimumSize( window, reqMinW, reqMinH );
			return;
		}

		double scale = GetDpiScale( window );
		var sizePx = appWindow.Size;
		int curDipW = sizePx.Width > 0 ? PhysicalToDip( sizePx.Width, scale ) : 0;
		int curDipH = sizePx.Height > 0 ? PhysicalToDip( sizePx.Height, scale ) : 0;

		int targetDipW = Math.Max( curDipW, Math.Max( pagePreset.Width, minWidthDip ) );
		int targetDipH = Math.Max( curDipH, Math.Max( pagePreset.Height, minHeightDip ) );
		if( targetDipW > curDipW || targetDipH > curDipH )
			ResizeWindow( window, targetDipW, targetDipH );
	}

	public static void TrackWindow( Window window, string presetKey )
	{
		if( string.IsNullOrWhiteSpace( presetKey ) )
			return;
		if( Trackers.TryGetValue( window, out WindowStateTracker? existing ) )
		{
			existing.PresetKey = presetKey;
			return;
		}
		WindowStateTracker tracker = new( window, presetKey );
		Trackers.AddOrUpdate( window, tracker );
		ActiveTrackers.Add( new WeakReference<WindowStateTracker>( tracker ) );
		tracker.Attach();
	}

	public static void RetargetTrackedWindow( Window window, string newPresetKey )
	{
		if( string.IsNullOrWhiteSpace( newPresetKey ) )
			return;
		if( Trackers.TryGetValue( window, out WindowStateTracker? existing ) )
		{
			existing.CaptureNow( save: false );
			existing.PresetKey = newPresetKey;
			return;
		}
		TrackWindow( window, newPresetKey );
	}

	public static void CaptureAllTrackedWindows()
	{
		for( int i = ActiveTrackers.Count - 1; i >= 0; i-- )
		{
			if( !ActiveTrackers[ i ].TryGetTarget( out WindowStateTracker? tracker ) || tracker.IsClosed )
			{
				ActiveTrackers.RemoveAt( i );
				continue;
			}
			tracker.CaptureNow( save: false );
		}
	}

	private static (int X, int Y, int Width, int Height) FitToWorkArea(
		AppWindow appWindow,
		Windows.Graphics.PointInt32 current,
		int desiredX,
		int desiredY,
		bool hasSavedPosition,
		int physicalWidth,
		int physicalHeight )
	{
		int targetX = hasSavedPosition ? desiredX : current.X;
		int targetY = hasSavedPosition ? desiredY : current.Y;
		int targetW = Math.Max( 200, physicalWidth );
		int targetH = Math.Max( 150, physicalHeight );
		try
		{
#if WINDOWS
			DisplayArea? area = hasSavedPosition
				? DisplayArea.GetFromRect( new Windows.Graphics.RectInt32( desiredX, desiredY, targetW, targetH ), DisplayAreaFallback.Primary )
				: DisplayArea.GetFromWindowId( appWindow.Id, DisplayAreaFallback.Primary );
			if( area is { WorkArea: { Width: > 0, Height: > 0 } work } )
			{
				targetW = Math.Min( targetW, work.Width );
				targetH = Math.Min( targetH, work.Height );
				targetX = Math.Clamp( targetX, work.X, work.X + work.Width - targetW );
				targetY = Math.Clamp( targetY, work.Y, work.Y + work.Height - targetH );
			}
#else
			_ = appWindow;
			if( X11WindowSize.TryGetScreenSize( out int screenW, out int screenH ) )
			{
				targetW = Math.Min( targetW, screenW );
				targetH = Math.Min( targetH, screenH );
				targetX = Math.Clamp( targetX, 0, Math.Max( 0, screenW - targetW ) );
				targetY = Math.Clamp( targetY, 0, Math.Max( 0, screenH - targetH ) );
			}
#endif
		}
		catch
		{
		}
		return ( targetX, targetY, targetW, targetH );
	}

	private sealed class WindowStateTracker( Window window, string presetKey )
	{
		private readonly Window _window = window;
		private DispatcherQueueTimer? _saveTimer;
		private long _suppressUntilTick;
		private (int Width, int Height)? _lastProgrammaticDipSize;
		private bool _hasLastNormalBounds;
		private (int X, int Y, int Width, int Height) _lastNormalBounds;
		private bool _lastMaximized;

		public string PresetKey { get; set; } = presetKey;
		public bool IsClosed { get; private set; }

		public void Attach()
		{
			if( _window.AppWindow is { } appWindow )
				appWindow.Changed += OnAppWindowChanged;
			_window.Closed += OnWindowClosed;
		}

		public void BeginProgrammaticChange( (int Width, int Height)? dipSize = null )
		{
			_suppressUntilTick = Environment.TickCount64 + ProgrammaticSuppressMs;
			if( dipSize is { } sz )
				_lastProgrammaticDipSize = sz;
		}

		private bool MatchesProgrammaticSize( int dipW, int dipH )
			=> _lastProgrammaticDipSize is { } target
				&& Math.Abs( dipW - target.Width ) <= 2
				&& Math.Abs( dipH - target.Height ) <= 2;

		private void OnAppWindowChanged( AppWindow sender, AppWindowChangedEventArgs args )
		{
			if( IsClosed || ( !args.DidPositionChange && !args.DidSizeChange && !args.DidPresenterChange ) )
				return;
			if( Environment.TickCount64 < _suppressUntilTick )
				return;

			OverlappedPresenterState state = OverlappedPresenterState.Restored;
			try
			{
				if( sender.Presenter is OverlappedPresenter overlapped )
					state = overlapped.State;
			}
			catch
			{
			}

			if( state == OverlappedPresenterState.Minimized )
				return;

			WindowConfigData cfg = GetOrCreateConfig( PresetKey );
			if( state == OverlappedPresenterState.Maximized )
			{
				_lastMaximized = true;
				cfg.IsMaximized = true;
				cfg.HasSavedPosition = true;
				ScheduleSave();
				return;
			}

			_lastMaximized = false;
			cfg.IsMaximized = false;

			double scale = GetDpiScale( _window );
			var pos = sender.Position;
			var px = sender.Size;
			int dipW = px.Width > 0 ? PhysicalToDip( px.Width, scale ) : 0;
			int dipH = px.Height > 0 ? PhysicalToDip( px.Height, scale ) : 0;
			(int minW, int minH) = MinimumDimensionsFor( PresetKey );
			bool validSize = dipW >= minW && dipH >= minH;

			if( args.DidSizeChange && validSize && !MatchesProgrammaticSize( dipW, dipH ) )
			{
				_lastProgrammaticDipSize = null;
				cfg.PositionAndSize = ( pos.X, pos.Y, dipW, dipH );
				cfg.HasSavedSize = true;
				cfg.HasSavedPosition = true;
				_lastNormalBounds = ( pos.X, pos.Y, dipW, dipH );
				_hasLastNormalBounds = true;
			}
			else if( args.DidPositionChange )
			{
				int keptW = cfg.PositionAndSize.z > 0 ? cfg.PositionAndSize.z : ( validSize ? dipW : 900 );
				int keptH = cfg.PositionAndSize.w > 0 ? cfg.PositionAndSize.w : ( validSize ? dipH : 620 );
				cfg.PositionAndSize = ( pos.X, pos.Y, keptW, keptH );
				cfg.HasSavedPosition = true;
				_lastNormalBounds = ( pos.X, pos.Y, validSize ? dipW : keptW, validSize ? dipH : keptH );
				_hasLastNormalBounds = true;
			}
			else if( args.DidPresenterChange && validSize && !MatchesProgrammaticSize( dipW, dipH ) )
			{
				cfg.PositionAndSize = ( pos.X, pos.Y, dipW, dipH );
				cfg.HasSavedPosition = true;
				_lastNormalBounds = ( pos.X, pos.Y, dipW, dipH );
				_hasLastNormalBounds = true;
			}

			ScheduleSave();
		}

		public void CaptureNow( bool save )
		{
			StopSaveTimer();
			if( IsClosed )
				return;

			WindowConfigData cfg = GetOrCreateConfig( PresetKey );
			try
			{
				if( _window.AppWindow is { } appWindow )
				{
					OverlappedPresenterState state = OverlappedPresenterState.Restored;
					try
					{
						if( appWindow.Presenter is OverlappedPresenter overlapped )
							state = overlapped.State;
					}
					catch
					{
					}

					if( state == OverlappedPresenterState.Minimized )
					{
						cfg.IsMaximized = _lastMaximized;
						if( save )
							UIStorage.SaveSafe();
						return;
					}

					if( state == OverlappedPresenterState.Maximized )
					{
						_lastMaximized = true;
						cfg.IsMaximized = true;
						cfg.HasSavedPosition = true;
						if( save )
							UIStorage.SaveSafe();
						return;
					}

					double scale = GetDpiScale( _window );
					var pos = appWindow.Position;
					var px = appWindow.Size;
					int dipW = px.Width > 0 ? PhysicalToDip( px.Width, scale ) : 0;
					int dipH = px.Height > 0 ? PhysicalToDip( px.Height, scale ) : 0;
					(int minW, int minH) = MinimumDimensionsFor( PresetKey );

					cfg.IsMaximized = false;
					if( dipW >= minW && dipH >= minH )
					{
						bool isMain = string.Equals( PresetKey, Presets.MainWindow, StringComparison.Ordinal );
						if( isMain )
						{
							int keptW = cfg.HasSavedSize ? dipW : ( cfg.PositionAndSize.z > 0 ? cfg.PositionAndSize.z : dipW );
							int keptH = cfg.HasSavedSize ? dipH : ( cfg.PositionAndSize.w > 0 ? cfg.PositionAndSize.w : dipH );
							cfg.PositionAndSize = ( pos.X, pos.Y, keptW, keptH );
							cfg.HasSavedPosition = true;
						}
						else
						{
							cfg.PositionAndSize = ( pos.X, pos.Y, dipW, dipH );
							cfg.HasSavedPosition = true;
							if( !MatchesProgrammaticSize( dipW, dipH ) )
								cfg.HasSavedSize = true;
						}
						_lastNormalBounds = ( pos.X, pos.Y, dipW, dipH );
						_hasLastNormalBounds = true;
					}
				}
			}
			catch
			{
				cfg.IsMaximized = _lastMaximized;
				if( _hasLastNormalBounds && cfg.HasSavedSize )
					cfg.PositionAndSize = _lastNormalBounds;
			}

			if( save )
				UIStorage.SaveSafe();
		}

		private static WindowConfigData GetOrCreateConfig( string key )
		{
			Dictionary<string, WindowConfigData> map = UIStorage.GlobalData.Win.PresetWindowConfig;
			if( map.TryGetValue( key, out WindowConfigData? cfg ) )
				return cfg;

			WindowConfigData? def = Defaults.PresetWindowConfig.GetValueOrDefault( key );
			cfg = new WindowConfigData
			{
				Title = def?.Title ?? key,
				PositionAndSize = def?.PositionAndSize ?? ( 0, 0, 900, 620 ),
				WindowKind = def?.WindowKind ?? WindowKind.Overlapped,
			};
			map[ key ] = cfg;
			return cfg;
		}

		private void ScheduleSave()
		{
			if( IsClosed )
				return;
			DispatcherQueue? queue = _window.DispatcherQueue;
			if( queue is null )
			{
				UIStorage.SaveSafe();
				return;
			}
			if( _saveTimer is null )
			{
				_saveTimer = queue.CreateTimer();
				_saveTimer.Interval = TimeSpan.FromMilliseconds( SaveDebounceMs );
				_saveTimer.IsRepeating = false;
				_saveTimer.Tick += OnSaveTimerTick;
			}
			_saveTimer.Stop();
			_saveTimer.Start();
		}

		private void OnSaveTimerTick( DispatcherQueueTimer sender, object args )
		{
			if( !IsClosed )
				UIStorage.SaveSafe();
		}

		private void StopSaveTimer()
		{
			if( _saveTimer is not { } timer )
				return;
			timer.Stop();
			timer.Tick -= OnSaveTimerTick;
			_saveTimer = null;
		}

		private void OnWindowClosed( object sender, WindowEventArgs args )
		{
			CaptureNow( save: !string.Equals( PresetKey, Presets.MainWindow, StringComparison.Ordinal ) );
			IsClosed = true;
			StopSaveTimer();
			try
			{
				if( _window.AppWindow is { } appWindow )
					appWindow.Changed -= OnAppWindowChanged;
			}
			catch
			{
			}
			_window.Closed -= OnWindowClosed;
		}
	}
}

public static class WindowExtensions
{
	extension( Window window )
	{
		public bool SetWindowType( AppWindowPresenterKind kind )
		{
			try
			{
				window.AppWindow.SetPresenter( kind );
				return true;
			}
			catch
			{
				return false;
			}
		}

#if WINDOWS
		public IntPtr IntPtrHandle => WinRT.Interop.WindowNative.GetWindowHandle( window );
#else
		public IntPtr IntPtrHandle => IntPtr.Zero;
#endif
	}

	extension<T>( T page ) where T : Page
	{
		public Window? Window => WindowHelper.WindowOf( page );
	}

	public static void SetGrid( this FrameworkElement element, int row, int column, int rowspan = 1, int columnspan = 1 )
	{
		Grid.SetRow( element, row );
		Grid.SetColumn( element, column );
		Grid.SetRowSpan( element, rowspan );
		Grid.SetColumnSpan( element, columnspan );
	}
}

/// <summary>Resizes X11 client windows directly on Linux where window managers ignore post-map frame resizes.</summary>
internal static partial class X11WindowSize
{
#if HAS_UNO
	private static readonly object Gate = new();
	private static IntPtr _display;
	private static bool _unavailable;
#endif

	public static bool TryResize( Window window, int width, int height )
	{
#if HAS_UNO
		if( !OperatingSystem.IsLinux() || width <= 0 || height <= 0 )
			return false;
		if( Uno.UI.Xaml.WindowHelper.GetNativeWindow( window ) is not Uno.UI.NativeElementHosting.X11NativeWindow { WindowId: var id } || id == IntPtr.Zero )
			return false;
		lock( Gate )
		{
			try
			{
				if( !EnsureDisplayLocked() )
					return false;
				_ = XResizeWindow( _display, id, (uint)width, (uint)height );
				_ = XFlush( _display );
				return true;
			}
			catch( Exception e ) when( e is DllNotFoundException or EntryPointNotFoundException )
			{
				_unavailable = true;
				return false;
			}
		}
#else
		_ = ( window, width, height );
		return false;
#endif
	}

	public static bool TryGetScreenSize( out int width, out int height )
	{
		width = 0;
		height = 0;
#if HAS_UNO
		if( !OperatingSystem.IsLinux() )
			return false;
		lock( Gate )
		{
			try
			{
				if( !EnsureDisplayLocked() )
					return false;
				int screen = XDefaultScreen( _display );
				width = XDisplayWidth( _display, screen );
				height = XDisplayHeight( _display, screen );
				return width > 0 && height > 0;
			}
			catch( Exception e ) when( e is DllNotFoundException or EntryPointNotFoundException )
			{
				_unavailable = true;
				return false;
			}
		}
#else
		return false;
#endif
	}

#if HAS_UNO
	private static bool EnsureDisplayLocked()
	{
		if( _unavailable )
			return false;
		if( _display != IntPtr.Zero )
			return true;
		_display = XOpenDisplay( null );
		if( _display == IntPtr.Zero )
		{
			_unavailable = true;
			return false;
		}
		return true;
	}

	[LibraryImport( "libX11.so.6", StringMarshalling = StringMarshalling.Utf8 )]
	private static partial IntPtr XOpenDisplay( string? name );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XResizeWindow( IntPtr display, IntPtr window, uint width, uint height );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XFlush( IntPtr display );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XDefaultScreen( IntPtr display );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XDisplayWidth( IntPtr display, int screenNumber );

	[LibraryImport( "libX11.so.6" )]
	private static partial int XDisplayHeight( IntPtr display, int screenNumber );
#endif
}
