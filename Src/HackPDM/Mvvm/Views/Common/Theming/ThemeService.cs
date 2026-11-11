using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

using HackPDM.Mvvm.Models.Settings;
using HackPDM.Mvvm.ViewModels.Common;
using HackPDM.Mvvm.Views.Common.Settings;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Color = Windows.UI.Color;

namespace HackPDM.Mvvm.Views.Common.Theming;

/// <summary>
/// Applies the saved <see cref="ThemeSettings"/>: sets the light/dark mode on every window and
/// recolors the shared Fluent theme brushes in place, so controls update live without reloading.
/// </summary>
/// <remarks>
/// Uno resolves theme resources against the theme the application started with (the light and dark
/// dictionaries share brush instances), so only that theme's colors can be edited live. Switching the
/// base mode previews immediately on every window; editing the other theme's colors needs a restart.
/// </remarks>
public sealed class ThemeService : IThemeService
{
	private const string PageBackgroundKey = "ApplicationPageBackgroundThemeBrush";

	// window roots the theme is applied to (weak: windows close without telling us)
	private readonly List<WeakReference<FrameworkElement>> _roots = [];
	// roots whose background we set (they had none), so it follows the theme
	private readonly ConditionalWeakTable<FrameworkElement, object> _ownedBackgrounds = [];
	// built-in colors, captured before the first override, so a reset can restore them
	private readonly Dictionary<string, Color> _originals = [];

	// the theme the application is running with: the only one whose colors can be changed live
	private readonly bool _runningDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
	private readonly AppThemeMode _startupMode;

	private ThemeSettings _settings;

	public ThemeService( ThemeSettings settings )
	{
		_settings = settings;
		_startupMode = settings.Mode;
		ApplyOverrides();
	}

	public AppThemeMode Mode => _settings.Mode;

	public bool IsDarkActive => _runningDark;

	public bool NeedsRestart => _settings.Mode != _startupMode;

	public string SavePath => AppConfigStore.FilePath;

	public event Action? ThemeChanged;

	#region Startup

	/// <summary>
	/// The mode saved in AppConfig.json, read directly (the full settings can't load this early).
	/// Called from the App constructor, where the application theme can still be set.
	/// </summary>
	public static AppThemeMode ReadSavedMode()
	{
		try
		{
			// only the mode: the full settings need converters for types that can't load this early
			string path = AppConfigStore.FilePath;
			if( !File.Exists( path ) )
				return new ThemeSettings().Mode;
			using JsonDocument doc = JsonDocument.Parse( File.ReadAllText( path ) );
			return doc.RootElement.TryGetProperty( "Theme", out var theme )
				&& theme.TryGetProperty( nameof( ThemeSettings.Mode ), out var mode )
				&& Enum.TryParse( mode.GetString(), out AppThemeMode parsed )
					? parsed
					: new ThemeSettings().Mode;
		}
		catch
		{
			return new ThemeSettings().Mode;
		}
	}

	/// <summary>Applies the theme to <paramref name="window"/>'s content. Call after setting <see cref="Window.Content"/>.</summary>
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

	public void SetMode( AppThemeMode mode )
	{
		if( _settings.Mode == mode )
			return;
		_settings.Mode = mode;
		foreach( var root in LiveRoots() )
			ApplyMode( root );
		ThemeChanged?.Invoke();
	}

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

	public ArgbColor? GetColor( string key )
		=> FindBrush( key ) is { } brush ? ToArgb( brush.Color ) : null;

	public bool IsOverridden( string key ) => _settings.OverridesFor( _runningDark ).ContainsKey( key );

	public void SetColor( string key, ArgbColor color )
	{
		if( NeedsRestart || !Recolor( key, color ) )
			return;
		_settings.OverridesFor( _runningDark )[ key ] = color.ToString();
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

	public void ResetColor( string key )
	{
		if( NeedsRestart || !_settings.OverridesFor( _runningDark ).Remove( key ) )
			return;
		Restore( key );
		RefreshOwnedBackgrounds();
		ThemeChanged?.Invoke();
	}

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

	public void Revert()
	{
		// restore everything, then re-apply the theme as saved on disk
		foreach( string key in _originals.Keys.ToList() )
			Restore( key );
		_settings = AppConfigStore.Load().Theme;
		UIStorage.GlobalData.Theme = _settings;
		ApplyOverrides();
		foreach( var root in LiveRoots() )
			ApplyMode( root );
		ThemeChanged?.Invoke();
	}

	public void Save()
	{
		UIStorage.GlobalData.Theme = _settings;
		AppConfigStore.Save( UIStorage.GlobalData );
	}

	private void ApplyOverrides()
	{
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

	private void Restore( string key )
	{
		if( _originals.TryGetValue( key, out var original ) && FindBrush( key ) is { } brush )
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
