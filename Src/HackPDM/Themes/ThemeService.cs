using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using HackPDM.Common;
using HackPDM.Core.Configuration;
using HackPDM.Core.Graph;
using HackPDM.Core.Theming;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;

namespace HackPDM.Themes;

/// <summary>Applies, edits, and saves color/shape presets and per-brush overrides across all open windows.</summary>
public sealed class ThemeService
{
	private const string PageBackgroundKey = "ApplicationPageBackgroundThemeBrush";
	private const int ColorDragCoalesceMs = 30;
	public const string PanelStrokeKey = "HackPanelStrokeBrush";
	public const string PanelCornerKey = "HackPanelCornerRadius";
	public const string PanelThicknessKey = "HackPanelBorderThickness";
	public const string SmokeThemeVariable = "HACKPDM_SMOKE_THEME";

	private readonly List<WeakReference<FrameworkElement>> _roots = [];
	private readonly ConditionalWeakTable<FrameworkElement, SolidColorBrush> _ownedBackgrounds = [];
	private readonly Dictionary<string, Color> _originals = [];

	private readonly bool _runningDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
	private readonly AppThemeMode _startupMode;
	private readonly FormName _startupForm;
	private ThemeSettings _savedSettings;
	private ThemeSettings _settings;
	private DispatcherQueueTimer? _coalesceTimer;
	private bool _themeChangePending;

	public ThemeService()
	{
		_settings = CloneSettings( UIStorage.GlobalData.Theme );
		var (palette, form) = StartupTheme( _settings );
		_settings.Palette = palette;
		_settings.Form = form;
		_settings.Mode = ThemePresets.Palette( palette ).IsDark ? AppThemeMode.Dark : AppThemeMode.Light;
		_savedSettings = CloneSettings( _settings );
		_startupMode = _settings.Mode;
		_startupForm = form;
		ApplyOverrides();
		UpdatePanelBrush();
		UpdateTableRowHighlightBrushes();
	}

	public PaletteName Palette => _settings.EffectivePalette;
	public FormName Form => _settings.Form;
	public bool StyleNeedsRestart => _settings.Form != _startupForm;
	public GraphTheme GraphTheme => GraphThemes.From( Palette, Form );
	public bool ShowTreeBranchLines => _settings.ShowTreeBranchLines;
	public bool IsDarkActive => _runningDark;
	public bool NeedsRestart => _settings.Mode != _startupMode;
	public bool HasUnsavedChanges => !SettingsEqual( _settings, _savedSettings );
	public string SavePath => AppSettingsStore.FilePath;

	public event Action? ThemeChanged;

	public static (PaletteName Palette, FormName Form) ReadSavedTheme()
	{
		try
		{
			return StartupTheme( AppSettingsStore.LoadUiConfig().Theme );
		}
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( ThemeService ), $"Could not read saved theme; falling back to defaults: {ex.Message}", ex );
			return StartupTheme( new ThemeSettings() );
		}
	}

	private static (PaletteName, FormName) StartupTheme( ThemeSettings saved )
	{
#if SMOKE_TESTS
		if( HackPDM.SmokeTests.SmokeTestRunner.IsEnabled
			&& Environment.GetEnvironmentVariable( SmokeThemeVariable )?.Split( ':' ) is [ var palette, var form ] )
			return ( ThemePresets.ParsePalette( palette ), ThemePresets.ParseForm( form ) );
#endif
		return ( saved.EffectivePalette, saved.Form );
	}

	public static void ApplyStartupResources( ResourceDictionary resources, PaletteName palette, FormName form )
	{
		AppForm f = ThemePresets.Form( form );
		if( form != FormName.Default )
		{
			resources[ "ControlCornerRadius" ] = new CornerRadius( f.ControlCornerRadius );
			resources[ "OverlayCornerRadius" ] = new CornerRadius( f.OverlayCornerRadius );
		}
		resources[ PanelCornerKey ] = new CornerRadius( f.PanelCornerRadius );
		resources[ PanelThicknessKey ] = new Thickness( f.PanelBorderThickness );
		if( resources.TryGetValue( PanelStrokeKey, out object? stroke ) && stroke is SolidColorBrush brush )
			brush.Color = UiColors.ToUiColor( ThemePresets.PanelStroke( ThemePresets.Palette( palette ), f ) );
		if( !f.CompactDensity
			&& resources.MergedDictionaries.FirstOrDefault( d => d.Source?.OriginalString.EndsWith( "Compact.xaml", StringComparison.OrdinalIgnoreCase ) == true ) is { } compact )
			resources.MergedDictionaries.Remove( compact );
	}

	public static bool IsCompactDensity( ResourceDictionary resources )
		=> resources.MergedDictionaries.Any( d => d.Source?.OriginalString.EndsWith( "Compact.xaml", StringComparison.OrdinalIgnoreCase ) == true );

	public void Attach( Window window )
	{
		if( window.Content is not FrameworkElement root )
			return;
		_roots.RemoveAll( r => !r.TryGetTarget( out var existing ) || ReferenceEquals( existing, root ) );
		_roots.Add( new( root ) );

		if( GetBackground( root ) is null )
		{
			SolidColorBrush owned = new();
			_ownedBackgrounds.AddOrUpdate( root, owned );
			SetBackground( root, owned );
		}
		ApplyMode( root );
	}

	public void SetPalette( PaletteName palette )
	{
		if( _settings.Palette == palette )
			return;
		CancelDeferredThemeChanged();
		foreach( string key in _originals.Keys )
			Restore( key, toOriginal: true );
		_settings.Palette = palette;
		_settings.Mode = ThemePresets.Palette( palette ).IsDark ? AppThemeMode.Dark : AppThemeMode.Light;
		ApplyOverrides();
		UpdatePanelBrush();
		UpdateTableRowHighlightBrushes();
		foreach( var root in LiveRoots() )
			ApplyMode( root );
		ThemeChanged?.Invoke();
	}

	public void SetForm( FormName form )
	{
		if( _settings.Form == form )
			return;
		CancelDeferredThemeChanged();
		_settings.Form = form;
		UpdatePanelBrush();
		UpdateTableRowHighlightBrushes();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	public void SetShowTreeBranchLines( bool show, bool persistImmediately = true )
	{
		bool changed = _settings.ShowTreeBranchLines != show;
		if( changed )
			_settings.ShowTreeBranchLines = show;
		if( persistImmediately )
		{
			_savedSettings.ShowTreeBranchLines = show;
			UIStorage.GlobalData.Theme.ShowTreeBranchLines = show;
		}
		if( changed )
			ThemeChanged?.Invoke();
	}

	private bool PresetApplies => ThemePresets.Palette( Palette ).IsDark == _runningDark;

	private void UpdatePanelBrush()
	{
		if( ThemeResources.TryGetBrush<SolidColorBrush>( PanelStrokeKey ) is { } brush )
			brush.Color = UiColors.ToUiColor( ThemePresets.PanelStroke( ThemePresets.Palette( Palette ), ThemePresets.Form( Form ) ) );
	}

	private void UpdateTableRowHighlightBrushes()
	{
		Color accent = ThemeResources.TryGetBrush<SolidColorBrush>( "AccentFillColorDefaultBrush" )?.Color
			?? ( _runningDark ? Color.FromArgb( 0xFF, 0x60, 0xCD, 0xFF ) : Color.FromArgb( 0xFF, 0x00, 0x5F, 0xB8 ) );

		byte selectedAlpha = _runningDark ? (byte)0x3D : (byte)0x2E;
		byte selectedHoverAlpha = _runningDark ? (byte)0x54 : (byte)0x40;
		byte selectedPressedAlpha = _runningDark ? (byte)0x33 : (byte)0x24;

		if( ThemeResources.TryGetBrush<SolidColorBrush>( "TableViewRowBackgroundSelected" ) is { } selected )
			selected.Color = Color.FromArgb( selectedAlpha, accent.R, accent.G, accent.B );
		if( ThemeResources.TryGetBrush<SolidColorBrush>( "TableViewRowBackgroundSelectedPointerOver" ) is { } selectedOver )
			selectedOver.Color = Color.FromArgb( selectedHoverAlpha, accent.R, accent.G, accent.B );
		if( ThemeResources.TryGetBrush<SolidColorBrush>( "TableViewRowBackgroundSelectedPressed" ) is { } selectedPressed )
			selectedPressed.Color = Color.FromArgb( selectedPressedAlpha, accent.R, accent.G, accent.B );
	}

	private void ApplyMode( FrameworkElement root )
	{
		root.RequestedTheme = _settings.Mode switch
		{
			AppThemeMode.Dark => ElementTheme.Dark,
			AppThemeMode.Light => ElementTheme.Light,
			_ => ElementTheme.Default,
		};
		if( _ownedBackgrounds.TryGetValue( root, out SolidColorBrush? owned ) )
			owned.Color = PageBackgroundFor( root.ActualTheme );
	}

	private Color PageBackgroundFor( ElementTheme theme )
		=> ( theme == ElementTheme.Dark ) == _runningDark && ThemeResources.TryGetBrush<SolidColorBrush>( PageBackgroundKey ) is { } brush
			? brush.Color
			: theme == ElementTheme.Dark ? Color.FromArgb( 0xFF, 0x20, 0x20, 0x20 ) : Color.FromArgb( 0xFF, 0xF3, 0xF3, 0xF3 );

	public ArgbColor? GetColor( string key )
		=> ThemeResources.TryGetBrush<SolidColorBrush>( key ) is { } brush ? brush.Color.ToArgbColor() : null;

	public bool IsOverridden( string key ) => _settings.OverridesFor( _runningDark ).ContainsKey( key );

	public void SetColor( string key, ArgbColor color )
	{
		if( NeedsRestart || !Recolor( key, color ) )
			return;
		_settings.OverridesFor( _runningDark )[ key ] = color.ToString();
		ScheduleCoalescedThemeChanged();
	}

	public void ResetColor( string key )
	{
		if( NeedsRestart || !_settings.OverridesFor( _runningDark ).Remove( key ) )
			return;
		CancelDeferredThemeChanged();
		Restore( key );
		UpdateTableRowHighlightBrushes();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	public void ResetAll()
	{
		if( NeedsRestart )
			return;
		CancelDeferredThemeChanged();
		var overrides = _settings.OverridesFor( _runningDark );
		foreach( string key in overrides.Keys )
			Restore( key );
		overrides.Clear();
		UpdateTableRowHighlightBrushes();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	public void Revert()
	{
		CancelDeferredThemeChanged();
		foreach( string key in _originals.Keys )
			Restore( key, toOriginal: true );
		_settings = CloneSettings( _savedSettings );
		ApplyOverrides();
		UpdatePanelBrush();
		UpdateTableRowHighlightBrushes();
		foreach( var root in LiveRoots() )
			ApplyMode( root );
		ThemeChanged?.Invoke();
	}

	public void Save()
	{
		FlushDeferredThemeChanged();
		_savedSettings = CloneSettings( _settings );
		UIStorage.GlobalData.Theme = CloneSettings( _settings );
		UIStorage.Save();
	}

	public async Task SaveAsync()
	{
		FlushDeferredThemeChanged();
		ThemeSettings snapshot = CloneSettings( _settings );
		ThemeSettings previousGlobal = UIStorage.GlobalData.Theme;
		UIStorage.GlobalData.Theme = CloneSettings( snapshot );
		try
		{
			await Task.Run( UIStorage.Save );
			_savedSettings = snapshot;
		}
		catch
		{
			UIStorage.GlobalData.Theme = previousGlobal;
			throw;
		}
	}

	private void ScheduleCoalescedThemeChanged()
	{
		_themeChangePending = true;
		if( _coalesceTimer is null )
		{
			if( DispatcherQueue.GetForCurrentThread() is not { } queue )
			{
				FlushDeferredThemeChanged();
				return;
			}
			_coalesceTimer = queue.CreateTimer();
			_coalesceTimer.Interval = TimeSpan.FromMilliseconds( ColorDragCoalesceMs );
			_coalesceTimer.IsRepeating = false;
			_coalesceTimer.Tick += OnCoalesceTimerTick;
		}
		if( !_coalesceTimer.IsRunning )
			_coalesceTimer.Start();
	}

	private void OnCoalesceTimerTick( DispatcherQueueTimer sender, object args )
		=> FlushDeferredThemeChanged();

	private void CancelDeferredThemeChanged()
	{
		_coalesceTimer?.Stop();
		_themeChangePending = false;
	}

	private void FlushDeferredThemeChanged()
	{
		_coalesceTimer?.Stop();
		if( !_themeChangePending )
			return;
		_themeChangePending = false;
		UpdateTableRowHighlightBrushes();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	private static ThemeSettings CloneSettings( ThemeSettings source ) => new()
	{
		Mode = source.Mode,
		Palette = source.Palette,
		Form = source.Form,
		ShowTreeBranchLines = source.ShowTreeBranchLines,
		Light = new Dictionary<string, string>( source.Light ?? [], StringComparer.Ordinal ),
		Dark = new Dictionary<string, string>( source.Dark ?? [], StringComparer.Ordinal ),
	};

	private static bool SettingsEqual( ThemeSettings a, ThemeSettings b )
		=> a.Mode == b.Mode
			&& a.Palette == b.Palette
			&& a.Form == b.Form
			&& a.ShowTreeBranchLines == b.ShowTreeBranchLines
			&& DictionariesEqual( a.Light, b.Light )
			&& DictionariesEqual( a.Dark, b.Dark );

	private static bool DictionariesEqual( Dictionary<string, string>? a, Dictionary<string, string>? b )
	{
		int countA = a?.Count ?? 0;
		int countB = b?.Count ?? 0;
		if( countA != countB )
			return false;
		if( countA == 0 )
			return true;
		foreach( var (key, valA) in a! )
		{
			if( !b!.TryGetValue( key, out string? valB ) || !string.Equals( valA, valB, StringComparison.Ordinal ) )
				return false;
		}
		return true;
	}

	private void ApplyOverrides()
	{
		if( !PresetApplies )
			return;
		foreach( var (key, argb) in ThemePresets.Palette( Palette ).Brushes )
			Recolor( key, ArgbColor.FromPacked( argb ) );
		foreach( var (key, hex) in _settings.OverridesFor( _runningDark ) )
		{
			if( ArgbColor.TryParse( hex, out var color ) )
				Recolor( key, color );
		}
	}

	private bool Recolor( string key, ArgbColor color )
	{
		if( ThemeResources.TryGetBrush<SolidColorBrush>( key ) is not { } brush )
			return false;
		_originals.TryAdd( key, brush.Color );
		brush.Color = color.ToUiColor();
		return true;
	}

	private void Restore( string key, bool toOriginal = false )
	{
		if( ThemeResources.TryGetBrush<SolidColorBrush>( key ) is not { } brush )
			return;
		if( !toOriginal && PresetApplies && ThemePresets.Palette( Palette ).Brushes.TryGetValue( key, out uint preset ) )
			brush.Color = UiColors.ToUiColor( preset );
		else if( _originals.TryGetValue( key, out var original ) )
			brush.Color = original;
	}

	private void RefreshOwnedBackgrounds()
	{
		foreach( var root in LiveRoots() )
		{
			if( _ownedBackgrounds.TryGetValue( root, out SolidColorBrush? owned ) )
				owned.Color = PageBackgroundFor( root.ActualTheme );
		}
	}

	private IEnumerable<FrameworkElement> LiveRoots()
	{
		_roots.RemoveAll( static r => !r.TryGetTarget( out _ ) );
		for( int i = 0; i < _roots.Count; i++ )
		{
			if( _roots[ i ].TryGetTarget( out var root ) )
				yield return root;
		}
	}

	private static Brush? GetBackground( FrameworkElement element ) => element switch
	{
		Panel panel => panel.Background,
		Control control => control.Background,
		Border border => border.Background,
		_ => null,
	};

	private static void SetBackground( FrameworkElement element, Brush brush )
	{
		switch( element )
		{
			case Panel panel: panel.Background = brush; break;
			case Control control: control.Background = brush; break;
			case Border border: border.Background = brush; break;
		}
	}
}
