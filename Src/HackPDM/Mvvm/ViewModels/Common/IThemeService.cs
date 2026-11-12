using System;

using HackPDM.Mvvm.Models.Settings;

namespace HackPDM.Mvvm.ViewModels.Common;

/// <summary>Applies, edits and saves the app theme. Every change shows immediately in all open windows.</summary>
public interface IThemeService
{
	/// <summary>The base mode currently applied (and saved by <see cref="Save"/>).</summary>
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

	/// <summary>Switches the base mode on every open window and raises <see cref="ThemeChanged"/>.</summary>
	/// <param name="mode">The new mode; setting the current mode does nothing.</param>
	/// <remarks>Not saved until <see cref="Save"/>. Choosing a mode other than the startup one sets <see cref="NeedsRestart"/>.</remarks>
	void SetMode( AppThemeMode mode );

	/// <summary>Reads the color a theme brush currently has.</summary>
	/// <param name="key">The theme resource key of a SolidColorBrush (see <see cref="ThemeColorCatalog"/>).</param>
	/// <returns>The color <paramref name="key"/> currently has in the active theme; null if the platform doesn't define it.</returns>
	ArgbColor? GetColor( string key );

	/// <summary>Whether <paramref name="key"/> has an override in the active theme.</summary>
	/// <param name="key">The theme resource key.</param>
	/// <returns><see langword="true"/> when the key has an override, saved or not yet saved.</returns>
	bool IsOverridden( string key );

	/// <summary>Overrides <paramref name="key"/> in the running theme (ignored while <see cref="NeedsRestart"/>).</summary>
	/// <param name="key">The theme resource key; ignored when the platform doesn't define it.</param>
	/// <param name="color">The new color.</param>
	/// <remarks>Recolors the shared brush in place, so every control using it updates at once. Not saved until <see cref="Save"/>.</remarks>
	void SetColor( string key, ArgbColor color );

	/// <summary>Restores <paramref name="key"/> in the active theme to the built-in color.</summary>
	/// <param name="key">The theme resource key; does nothing when it has no override or while <see cref="NeedsRestart"/>.</param>
	void ResetColor( string key );

	/// <summary>Restores every color of the active theme.</summary>
	/// <remarks>Does nothing while <see cref="NeedsRestart"/>. Not saved until <see cref="Save"/>.</remarks>
	void ResetAll();

	/// <summary>Discards unsaved changes: re-applies the theme as last saved.</summary>
	/// <remarks>Reads AppConfig.json again and replaces the in-memory theme settings with what it holds.</remarks>
	/// <exception cref="System.Text.Json.JsonException">The settings file is not valid JSON.</exception>
	/// <exception cref="System.IO.IOException">The settings file cannot be read.</exception>
	void Revert();

	/// <summary>Writes the theme to <see cref="SavePath"/>.</summary>
	/// <remarks>Saves all of the app's in-memory UI settings, not only the theme.</remarks>
	/// <exception cref="System.IO.IOException">The settings file cannot be written.</exception>
	/// <exception cref="System.UnauthorizedAccessException">The settings file is not writable by this user.</exception>
	void Save();
}
