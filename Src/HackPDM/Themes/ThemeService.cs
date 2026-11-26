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

using Uno.Themes;

using Color = Windows.UI.Color;

namespace HackPDM.Themes;

/// <summary>
/// Applies, edits, and saves color/shape presets and per-color overrides across all open windows. A color preset is a
/// Material scheme (<see cref="AppPalette.Scheme"/>): at startup it becomes Uno Material's color override and the app's
/// own surface-container colors; while running, the role brushes and the Windows (Fluent) brushes derived from the
/// scheme are recolored in place, so edits show live.
/// </summary>
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
	private IReadOnlyDictionary<string, uint> _applied;

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
		_applied = ThemePresets.Palette( palette ).Scheme;
		DropUnknownOverrides();
		ApplyScheme();
		UpdatePanelBrush();
		UpdateTableRowHighlightBrushes();
	}

	public PaletteName Palette => _settings.EffectivePalette;
	public FormName Form => _settings.Form;
	public bool StyleNeedsRestart => _settings.Form != _startupForm;
	public GraphTheme GraphTheme => GraphThemes.From( Palette, Form, _applied );
	public bool ShowTreeBranchLines => _settings.ShowTreeBranchLines;
	public bool IsDarkActive => _runningDark;
	public bool NeedsRestart => _settings.Mode != _startupMode;
	public bool HasUnsavedChanges => !SettingsEqual( _settings, _savedSettings );
	public string SavePath => AppSettingsStore.FilePath;

	public event Action? ThemeChanged;

	/// <summary>The theme settings as saved, or defaults when they can't be read.</summary>
	public static ThemeSettings ReadSavedSettings()
	{
		try
		{
			return AppSettingsStore.LoadUiConfig().Theme ?? new ThemeSettings();
		}
		catch( Exception ex )
		{
			HackLogger.Warn( nameof( ThemeService ), $"Could not read saved theme; falling back to defaults: {ex.Message}", ex );
			return new ThemeSettings();
		}
	}

	public static (PaletteName Palette, FormName Form) ReadSavedTheme() => StartupTheme( ReadSavedSettings() );

	/// <summary>The palette and form to start with (the smoke tests can ask for others).</summary>
	public static (PaletteName Palette, FormName Form) StartupTheme( ThemeSettings saved )
	{
#if SMOKE_TESTS
		if( HackPDM.SmokeTests.SmokeTestRunner.IsEnabled
			&& Environment.GetEnvironmentVariable( SmokeThemeVariable )?.Split( ':' ) is [ var palette, var form ] )
			return ( ThemePresets.ParsePalette( palette ), ThemePresets.ParseForm( form ) );
#endif
		return ( saved.EffectivePalette, saved.Form );
	}

	/// <summary>Sets up the app resources for a preset before any page loads: colors, corners, panel borders and density.</summary>
	/// <param name="resources">The application's resources (App.xaml).</param>
	/// <param name="palette">The color preset.</param>
	/// <param name="form">The shape preset.</param>
	/// <param name="overrides">Saved per-role color overrides for the base theme in use, if any.</param>
	public static void ApplyStartupResources( ResourceDictionary resources, PaletteName palette, FormName form,
		IReadOnlyDictionary<string, string>? overrides = null )
	{
		ApplySchemeResources( resources, EffectiveScheme( ThemePresets.Palette( palette ), overrides ) );
		AppForm f = ThemePresets.Form( form );
		resources[ "ControlCornerRadius" ] = new CornerRadius( f.ControlCornerRadius );
		resources[ "OverlayCornerRadius" ] = new CornerRadius( f.OverlayCornerRadius );
		resources[ PanelCornerKey ] = new CornerRadius( f.PanelCornerRadius );
		resources[ PanelThicknessKey ] = new Thickness( f.PanelBorderThickness );
		if( resources.TryGetValue( PanelStrokeKey, out object? stroke ) && stroke is SolidColorBrush brush )
			brush.Color = UiColors.ToUiColor( ThemePresets.PanelStroke( ThemePresets.Palette( palette ), f ) );
		if( !f.CompactDensity
			&& resources.MergedDictionaries.FirstOrDefault( d => d.Source?.OriginalString.EndsWith( "Compact.xaml", StringComparison.OrdinalIgnoreCase ) == true ) is { } compact )
			resources.MergedDictionaries.Remove( compact );
	}

	/// <summary>A preset's scheme with saved overrides applied; unknown roles and unreadable colors are ignored.</summary>
	public static IReadOnlyDictionary<string, uint> EffectiveScheme( AppPalette palette, IReadOnlyDictionary<string, string>? overrides )
	{
		if( overrides is null || overrides.Count == 0 )
			return palette.Scheme;
		Dictionary<string, uint> scheme = new( palette.Scheme, StringComparer.Ordinal );
		foreach( var (role, hex) in overrides )
		{
			if( MaterialRoles.IsRole( role ) && ArgbColor.TryParse( hex, out ArgbColor color ) )
				scheme[ role ] = (uint)color.A << 24 | (uint)color.R << 16 | (uint)color.G << 8 | color.B;
		}
		return scheme;
	}

	/// <summary>
	/// Hands the scheme to Uno Material as its color override (so its derived and state colors follow it) and sets
	/// the app's own surface-container colors in every theme dictionary of App.xaml.
	/// </summary>
	private static void ApplySchemeResources( ResourceDictionary resources, IReadOnlyDictionary<string, uint> scheme )
	{
		ResourceDictionary ThemeColors()
		{
			ResourceDictionary colors = new();
			foreach( string role in MaterialRoles.Names )
			{
				if( !MaterialRoles.IsAppRole( role ) )
					colors[ MaterialRoles.ColorKey( role ) ] = UiColors.ToUiColor( scheme[ role ] );
			}
			return colors;
		}
		ResourceDictionary colorOverride = new();
		colorOverride.ThemeDictionaries[ "Light" ] = ThemeColors();
		colorOverride.ThemeDictionaries[ "Dark" ] = ThemeColors();
		int themes = 0;
		foreach( BaseTheme theme in FindMaterialThemes( resources ) )
		{
			theme.ColorOverrideDictionary = colorOverride;
			themes++;
		}
		if( themes == 0 )
			HackLogger.Warn( nameof( ThemeService ), "No Material theme found in the app resources; the color preset only recolors brushes." );

		foreach( object value in resources.ThemeDictionaries.Values )
		{
			if( value is not ResourceDictionary themeDictionary )
				continue;
			foreach( string role in MaterialRoles.Names )
			{
				if( !MaterialRoles.IsAppRole( role ) )
					continue;
				Color color = UiColors.ToUiColor( scheme[ role ] );
				themeDictionary[ MaterialRoles.ColorKey( role ) ] = color;
				if( themeDictionary.TryGetValue( MaterialRoles.BrushKey( role ), out object? brush ) && brush is SolidColorBrush solid )
					solid.Color = color;
			}
		}
	}

	private static IEnumerable<BaseTheme> FindMaterialThemes( ResourceDictionary resources )
	{
		foreach( ResourceDictionary merged in resources.MergedDictionaries )
		{
			if( merged is BaseTheme theme )
				yield return theme;
			foreach( BaseTheme nested in FindMaterialThemes( merged ) )
				yield return nested;
		}
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
		_settings.Palette = palette;
		_settings.Mode = ThemePresets.Palette( palette ).IsDark ? AppThemeMode.Dark : AppThemeMode.Light;
		ApplyScheme();
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

	/// <summary>Selected table rows use Secondary Container, as in the Figma file table; hover and press add a light state layer.</summary>
	private void UpdateTableRowHighlightBrushes()
	{
		uint selected = _applied[ MaterialRoles.SecondaryContainer ];
		uint layer = _applied[ MaterialRoles.OnSecondaryContainer ] & 0x00FFFFFF;
		SetBrush( "TableViewRowBackgroundSelected", selected );
		SetBrush( "TableViewRowBackgroundSelectedPointerOver", ThemePresets.Blend( 0x14000000 | layer, selected ) );
		SetBrush( "TableViewRowBackgroundSelectedPressed", ThemePresets.Blend( 0x1F000000 | layer, selected ) );

		static void SetBrush( string key, uint argb )
		{
			if( ThemeResources.TryGetBrush<SolidColorBrush>( key ) is { } brush )
				brush.Color = UiColors.ToUiColor( argb );
		}
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

	/// <summary>The current color of a Material role (with any override), or null for an unknown role.</summary>
	public ArgbColor? GetColor( string key )
		=> MaterialRoles.IsRole( key )
			? ArgbColor.FromPacked( EffectiveScheme( ThemePresets.Palette( Palette ), _settings.OverridesFor( _runningDark ) )[ key ] )
			: null;

	public bool IsOverridden( string key ) => _settings.OverridesFor( _runningDark ).ContainsKey( key );

	public void SetColor( string key, ArgbColor color )
	{
		if( NeedsRestart || !MaterialRoles.IsRole( key ) )
			return;
		_settings.OverridesFor( _runningDark )[ key ] = color.ToString();
		ApplyScheme();
		ScheduleCoalescedThemeChanged();
	}

	public void ResetColor( string key )
	{
		if( NeedsRestart || !_settings.OverridesFor( _runningDark ).Remove( key ) )
			return;
		CancelDeferredThemeChanged();
		ApplyScheme();
		UpdateTableRowHighlightBrushes();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	public void ResetAll()
	{
		if( NeedsRestart )
			return;
		CancelDeferredThemeChanged();
		_settings.OverridesFor( _runningDark ).Clear();
		ApplyScheme();
		UpdateTableRowHighlightBrushes();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	public void Revert()
	{
		CancelDeferredThemeChanged();
		_settings = CloneSettings( _savedSettings );
		ApplyScheme();
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

	/// <summary>
	/// Recolors the role brushes and the Fluent brushes derived from them to the selected preset plus overrides.
	/// Does nothing while the preset needs the other base theme (light vs dark): that waits for a restart.
	/// </summary>
	private void ApplyScheme()
	{
		if( !PresetApplies )
			return;
		IReadOnlyDictionary<string, uint> scheme = EffectiveScheme( ThemePresets.Palette( Palette ), _settings.OverridesFor( _runningDark ) );
		foreach( string role in MaterialRoles.Names )
			Recolor( MaterialRoles.BrushKey( role ), scheme[ role ] );
		foreach( var (key, argb) in MaterialRoles.FluentBrushes( scheme ) )
			Recolor( key, argb );
		foreach( var (key, role) in MaterialRoles.AppBrushes )
			Recolor( key, scheme[ role ] );
		if( ThemeResources.TryGetBrush<LinearGradientBrush>( MaterialRoles.TabUnderlineBrushKey ) is { GradientStops.Count: >= 4 } underline )
		{
			Color primary = UiColors.ToUiColor( scheme[ MaterialRoles.Primary ] );
			underline.GradientStops[ ^2 ].Color = primary;
			underline.GradientStops[ ^1 ].Color = primary;
		}
		_applied = scheme;
	}

	private static void Recolor( string key, uint argb )
	{
		if( ThemeResources.TryGetBrush<SolidColorBrush>( key ) is { } brush )
			brush.Color = UiColors.ToUiColor( argb );
	}

	/// <summary>Removes saved overrides that are not Material roles (from before the presets became Material schemes).</summary>
	private void DropUnknownOverrides()
	{
		int dropped = 0;
		foreach( ThemeSettings settings in new[] { _settings, _savedSettings, UIStorage.GlobalData.Theme } )
		{
			foreach( Dictionary<string, string> overrides in new[] { settings.Light, settings.Dark } )
			{
				foreach( string key in overrides.Keys.Where( static k => !MaterialRoles.IsRole( k ) ).ToList() )
				{
					overrides.Remove( key );
					if( ReferenceEquals( settings, _settings ) )
						dropped++;
				}
			}
		}
		if( dropped > 0 )
			HackLogger.Info( nameof( ThemeService ), $"Reset {dropped} saved theme color(s) from the old Windows-style color list; the Theme Editor now edits Material colors." );
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
