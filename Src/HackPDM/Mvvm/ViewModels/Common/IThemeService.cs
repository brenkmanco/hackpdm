using System;

using HackPDM.Mvvm.Models.Settings;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>Applies, edits and saves the app theme. Every change shows immediately in all open windows.</summary>
public interface IThemeService
{
	AppThemeMode Mode { get; }

	/// <summary>Whether the app is running with the dark theme: the theme whose colors are being edited.</summary>
	bool IsDarkActive { get; }

	/// <summary>
	/// The base mode was changed since startup: it previews now, but the new theme's colors can only be
	/// edited (and menus and dialogs only follow it) after a restart.
	/// </summary>
	bool NeedsRestart { get; }

	/// <summary>Where <see cref="Save"/> writes (AppConfig.json, loaded automatically at startup).</summary>
	string SavePath { get; }

	/// <summary>Raised after the mode or any color changes.</summary>
	event Action? ThemeChanged;

	void SetMode( AppThemeMode mode );

	/// <summary>The color <paramref name="key"/> currently has in the active theme; null if the platform doesn't define it.</summary>
	ArgbColor? GetColor( string key );

	/// <summary>Whether <paramref name="key"/> has a saved override in the active theme.</summary>
	bool IsOverridden( string key );

	/// <summary>Overrides <paramref name="key"/> in the running theme (ignored while <see cref="NeedsRestart"/>).</summary>
	void SetColor( string key, ArgbColor color );

	/// <summary>Restores <paramref name="key"/> in the active theme to the built-in color.</summary>
	void ResetColor( string key );

	/// <summary>Restores every color of the active theme.</summary>
	void ResetAll();

	/// <summary>Discards unsaved changes: re-applies the theme as last saved.</summary>
	void Revert();

	/// <summary>Writes the theme to <see cref="SavePath"/>.</summary>
	void Save();
}
