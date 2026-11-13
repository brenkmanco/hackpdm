using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

using HackPDM.Core.Graph;
using HackPDM.Core.Theming;
using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.Views.Common.Settings;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;

namespace HackPDM.Mvvm.Views.Common.Theming;

/// <summary>
/// Applies the saved <see cref="ThemeSettings"/>: the color preset (<see cref="PaletteName"/>) sets the light/dark
/// mode on every window and recolors the shared Fluent theme brushes in place, so controls update live; the
/// user's own color changes go on top. The shape preset (<see cref="FormName"/>) sets control corners and density
/// at startup (<see cref="ApplyStartupResources"/>) and the app's panel borders.
/// </summary>
/// <remarks>
/// Uno resolves theme resources against the theme the application started with (the light and dark
/// dictionaries share brush instances), so only that theme's colors can be edited live. Switching the
/// base mode previews immediately on every window; editing the other theme's colors needs a restart.
/// </remarks>
public sealed class ThemeService : IThemeService
{
	private const string PageBackgroundKey = "ApplicationPageBackgroundThemeBrush";

	/// <summary>The app's own panel resources (folder tree, lists, graph frame); defaults in App.xaml, set from the presets.</summary>
	public const string PanelStrokeKey = "HackPanelStrokeBrush", PanelCornerKey = "HackPanelCornerRadius", PanelThicknessKey = "HackPanelBorderThickness";

	/// <summary>Picks the theme the app starts with instead of the saved one (smoke runs only): "Palette:Form".</summary>
	public const string SmokeThemeVariable = "HACKPDM_SMOKE_THEME";

	// window roots the theme is applied to (weak: windows close without telling us)
	private readonly List<WeakReference<FrameworkElement>> _roots = [];
	// roots whose background we set (they had none), so it follows the theme
	private readonly ConditionalWeakTable<FrameworkElement, object> _ownedBackgrounds = [];
	// built-in colors, captured before the first override, so a reset can restore them
	private readonly Dictionary<string, Color> _originals = [];

	// the theme the application is running with: the only one whose colors can be changed live
	private readonly bool _runningDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
	private readonly AppThemeMode _startupMode;
	private readonly FormName _startupForm;

	private ThemeSettings _settings;

	/// <summary>Creates the service and applies the saved color overrides of the running theme.</summary>
	/// <param name="settings">The saved theme settings; the service edits this instance.</param>
	/// <remarks>Must be created on the UI thread after the application exists, since it reads <see cref="Application.Current"/>.</remarks>
	public ThemeService( ThemeSettings settings )
	{
		_settings = settings;
		// the app started with the preset's base mode and shape (see the App constructor)
		var (palette, form) = StartupTheme( settings );
		_settings.Palette = palette;
		_settings.Form = form;
		_settings.Mode = ThemePresets.Palette( palette ).IsDark ? AppThemeMode.Dark : AppThemeMode.Light;
		_startupMode = _settings.Mode;
		_startupForm = form;
		ApplyOverrides();
		UpdatePanelBrush();
	}

	/// <inheritdoc/>
	public PaletteName Palette => _settings.EffectivePalette;

	/// <inheritdoc/>
	public FormName Form => _settings.Form;

	/// <inheritdoc/>
	public bool StyleNeedsRestart => _settings.Form != _startupForm;

	/// <inheritdoc/>
	public GraphTheme GraphTheme => GraphThemes.From( Palette, Form );

	/// <inheritdoc/>
	public AppThemeMode Mode => _settings.Mode;

	/// <inheritdoc/>
	public bool IsDarkActive => _runningDark;

	/// <inheritdoc/>
	public bool NeedsRestart => _settings.Mode != _startupMode;

	/// <inheritdoc/>
	public string SavePath => AppConfigStore.FilePath;

	/// <inheritdoc/>
	public event Action? ThemeChanged;

	#region Startup

	/// <summary>
	/// The presets saved in AppConfig.json, read directly (the full settings can't load this early).
	/// Called from the App constructor, where the application theme can still be set.
	/// </summary>
	/// <returns>The saved palette and form; for settings from before the presets, Light when the saved mode was Light,
	/// else Dark, and Default. Missing or unreadable files give Dark / Default (errors are swallowed). In a smoke run,
	/// <see cref="SmokeThemeVariable"/> wins.</returns>
	public static (PaletteName Palette, FormName Form) ReadSavedTheme()
	{
		try
		{
			string path = AppConfigStore.FilePath;
			if( !File.Exists( path ) )
				return StartupTheme( new ThemeSettings() );
			using JsonDocument doc = JsonDocument.Parse( File.ReadAllText( path ) );
			ThemeSettings saved = new();
			if( doc.RootElement.TryGetProperty( "Theme", out var theme ) )
			{
				if( theme.TryGetProperty( nameof( ThemeSettings.Mode ), out var mode ) && Enum.TryParse( mode.GetString(), out AppThemeMode m ) )
					saved.Mode = m;
				if( theme.TryGetProperty( nameof( ThemeSettings.Palette ), out var palette ) && palette.ValueKind == JsonValueKind.String )
					saved.Palette = ThemePresets.ParsePalette( palette.GetString(), saved.EffectivePalette );
				if( theme.TryGetProperty( nameof( ThemeSettings.Form ), out var form ) && form.ValueKind == JsonValueKind.String )
					saved.Form = ThemePresets.ParseForm( form.GetString() );
			}
			return StartupTheme( saved );
		}
		catch
		{
			return StartupTheme( new ThemeSettings() );
		}
	}

	/// <summary>The theme to start with: the saved one, or in a smoke run the one <see cref="SmokeThemeVariable"/> names.</summary>
	private static (PaletteName, FormName) StartupTheme( ThemeSettings saved )
	{
		if( HackPDM.Shell.Testing.SmokeTestRunner.IsEnabled
			&& Environment.GetEnvironmentVariable( SmokeThemeVariable )?.Split( ':' ) is [ var palette, var form ] )
			return (ThemePresets.ParsePalette( palette ), ThemePresets.ParseForm( form ));
		return (saved.EffectivePalette, saved.Form);
	}

	/// <summary>
	/// Sets the shape preset's resources before any page is built: control and overlay corners, Fluent's compact or
	/// standard density, and the app's panel corners, borders and border color. Called from the App constructor.
	/// </summary>
	/// <param name="resources">The application's resources (App.xaml, with the Compact density dictionary merged in).</param>
	/// <param name="palette">The color preset (for the panel border color).</param>
	/// <param name="form">The shape preset.</param>
	/// <remarks>Fluent controls read these when they are created, so a later change needs a restart.</remarks>
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
			brush.Color = ToColor( ThemePresets.PanelStroke( ThemePresets.Palette( palette ), f ) );
		if( !f.CompactDensity
			&& resources.MergedDictionaries.FirstOrDefault( d => d.Source?.OriginalString.EndsWith( "Compact.xaml", StringComparison.OrdinalIgnoreCase ) == true ) is { } compact )
			resources.MergedDictionaries.Remove( compact );
	}

	/// <summary>Whether Fluent's compact density is in use (the smoke tests).</summary>
	public static bool IsCompactDensity( ResourceDictionary resources )
		=> resources.MergedDictionaries.Any( d => d.Source?.OriginalString.EndsWith( "Compact.xaml", StringComparison.OrdinalIgnoreCase ) == true );

	/// <summary>Applies the theme to <paramref name="window"/>'s content. Call after setting <see cref="Window.Content"/>.</summary>
	/// <param name="window">The window; ignored when its content is not a <see cref="FrameworkElement"/>.</param>
	/// <remarks>The window is tracked weakly so later mode changes reach it. A root without a background gets one that
	/// follows the theme.</remarks>
	public void Attach( Window window )
	{
		if( window.Content is not FrameworkElement root )
			return;
		_roots.RemoveAll( r => !r.TryGetTarget( out var existing ) || ReferenceEquals( existing, root ) );
		_roots.Add( new( root ) );

		// a window with a bare root shows the platform's (often black) surface behind the text
		if( GetBackground( root ) is null )
			_ownedBackgrounds.AddOrUpdate( root, null! );
		ApplyMode( root );
	}

	#endregion

	#region Mode

	/// <inheritdoc/>
	public void SetMode( AppThemeMode mode )
	{
		if( _settings.Mode == mode )
			return;
		_settings.Mode = mode;
		foreach( var root in LiveRoots() )
			ApplyMode( root );
		ThemeChanged?.Invoke();
	}

	/// <inheritdoc/>
	public void SetPalette( PaletteName palette )
	{
		if( _settings.Palette == palette )
			return;
		// back to the built-in colors, then the new preset (and the user's own changes) on top
		foreach( string key in _originals.Keys.ToList() )
			Restore( key, toOriginal: true );
		_settings.Palette = palette;
		_settings.Mode = ThemePresets.Palette( palette ).IsDark ? AppThemeMode.Dark : AppThemeMode.Light;
		ApplyOverrides();
		UpdatePanelBrush();
		foreach( var root in LiveRoots() )
			ApplyMode( root );
		ThemeChanged?.Invoke();
	}

	/// <inheritdoc/>
	public void SetForm( FormName form )
	{
		if( _settings.Form == form )
			return;
		_settings.Form = form;
		UpdatePanelBrush();
		ThemeChanged?.Invoke();
	}

	/// <summary>Whether the palette's colors can be set now: it builds on the theme the app is running with.</summary>
	private bool PresetApplies => ThemePresets.Palette( Palette ).IsDark == _runningDark;

	/// <summary>Recolors the app's panel border brush (shared by every panel, so they change live).</summary>
	private void UpdatePanelBrush()
	{
		if( Application.Current.Resources.TryGetValue( PanelStrokeKey, out object? value ) && value is SolidColorBrush brush )
			brush.Color = ToColor( ThemePresets.PanelStroke( ThemePresets.Palette( Palette ), ThemePresets.Form( Form ) ) );
	}

	private static Color ToColor( uint argb ) => Color.FromArgb( (byte)( argb >> 24 ), (byte)( argb >> 16 ), (byte)( argb >> 8 ), (byte)argb );

	private void ApplyMode( FrameworkElement root )
	{
		root.RequestedTheme = _settings.Mode switch
		{
			AppThemeMode.Dark => ElementTheme.Dark,
			AppThemeMode.Light => ElementTheme.Light,
			_ => ElementTheme.Default,
		};
		if( _ownedBackgrounds.TryGetValue( root, out _ ) )
			SetBackground( root, new SolidColorBrush( PageBackgroundFor( root.ActualTheme ) ) );
	}

	// the page brush resolves to the running theme, so a root previewing the other theme gets the built-in color
	private Color PageBackgroundFor( ElementTheme theme )
		=> ( theme == ElementTheme.Dark ) == _runningDark && FindBrush( PageBackgroundKey ) is { } brush
			? brush.Color
			: theme == ElementTheme.Dark ? Color.FromArgb( 0xFF, 0x20, 0x20, 0x20 ) : Color.FromArgb( 0xFF, 0xF3, 0xF3, 0xF3 );

	#endregion

	#region Colors

	/// <inheritdoc/>
	public ArgbColor? GetColor( string key )
		=> FindBrush( key ) is { } brush ? ToArgb( brush.Color ) : null;

	/// <inheritdoc/>
	public bool IsOverridden( string key ) => _settings.OverridesFor( _runningDark ).ContainsKey( key );

	/// <inheritdoc/>
	public void SetColor( string key, ArgbColor color )
	{
		if( NeedsRestart || !Recolor( key, color ) )
			return;
		_settings.OverridesFor( _runningDark )[ key ] = color.ToString();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	/// <inheritdoc/>
	public void ResetColor( string key )
	{
		if( NeedsRestart || !_settings.OverridesFor( _runningDark ).Remove( key ) )
			return;
		Restore( key );
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	/// <inheritdoc/>
	public void ResetAll()
	{
		if( NeedsRestart )
			return;
		var overrides = _settings.OverridesFor( _runningDark );
		foreach( string key in overrides.Keys.ToList() )
			Restore( key );
		overrides.Clear();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	/// <inheritdoc/>
	public void Revert()
	{
		// restore everything, then re-apply the theme as saved on disk
		foreach( string key in _originals.Keys.ToList() )
			Restore( key, toOriginal: true );
		_settings = AppConfigStore.Load().Theme;
		UIStorage.GlobalData.Theme = _settings;
		ApplyOverrides();
		UpdatePanelBrush();
		foreach( var root in LiveRoots() )
			ApplyMode( root );
		ThemeChanged?.Invoke();
	}

	/// <inheritdoc/>
	public void Save()
	{
		UIStorage.GlobalData.Theme = _settings;
		AppConfigStore.Save( UIStorage.GlobalData );
	}

	/// <summary>The preset's colors, then the user's own changes; nothing while the preset needs the other base theme (a restart).</summary>
	private void ApplyOverrides()
	{
		if( !PresetApplies )
			return;
		foreach( var (key, argb) in ThemePresets.Palette( Palette ).Brushes )
			Recolor( key, new ArgbColor( (byte)( argb >> 24 ), (byte)( argb >> 16 ), (byte)( argb >> 8 ), (byte)argb ) );
		foreach( var (key, hex) in _settings.OverridesFor( _runningDark ) )
		{
			if( ArgbColor.TryParse( hex, out var color ) )
				Recolor( key, color );
		}
	}

	private bool Recolor( string key, ArgbColor color )
	{
		if( FindBrush( key ) is not { } brush )
			return false;
		_originals.TryAdd( key, brush.Color );
		brush.Color = Color.FromArgb( color.A, color.R, color.G, color.B );
		return true;
	}

	/// <summary>Puts a color back to the preset's value (or, with <paramref name="toOriginal"/> or when the preset leaves it alone, the built-in one).</summary>
	private void Restore( string key, bool toOriginal = false )
	{
		if( FindBrush( key ) is not { } brush )
			return;
		if( !toOriginal && PresetApplies && ThemePresets.Palette( Palette ).Brushes.TryGetValue( key, out uint preset ) )
			brush.Color = ToColor( preset );
		else if( _originals.TryGetValue( key, out var original ) )
			brush.Color = original;
	}

	private void RefreshOwnedBackgrounds()
	{
		foreach( var root in LiveRoots() )
		{
			if( _ownedBackgrounds.TryGetValue( root, out _ ) )
				SetBackground( root, new SolidColorBrush( PageBackgroundFor( root.ActualTheme ) ) );
		}
	}

	/// <summary>The brush instance controls share for <paramref name="key"/> in the running theme.</summary>
	private static SolidColorBrush? FindBrush( string key )
		=> Application.Current.Resources.TryGetValue( key, out object? value ) ? value as SolidColorBrush : null;

	private static ArgbColor ToArgb( Color c ) => new( c.A, c.R, c.G, c.B );

	#endregion

	#region Window roots

	private IEnumerable<FrameworkElement> LiveRoots()
	{
		_roots.RemoveAll( r => !r.TryGetTarget( out _ ) );
		foreach( var reference in _roots.ToList() )
		{
			if( reference.TryGetTarget( out var root ) )
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

	#endregion
}
