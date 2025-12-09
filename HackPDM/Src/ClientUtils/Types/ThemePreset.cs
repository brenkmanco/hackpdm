
using System.Collections.Generic;
using System.Linq;

using HackPDM.Properties;

using Microsoft.UI.Xaml.Media;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

namespace HackPDM.Src.ClientUtils.Types;

public static class ThemePreset
{
    public static List<string> Fonts { get; } = [.. CanvasTextFormat.GetSystemFontFamilies().OrderBy(f => f)];
    public static readonly Theme DefaultTheme = new()
    {
        Name = "Default",
        BackgroundColor = Color.FromArgb(255, 235, 235, 235),
        SecondaryBackgroundColor = Color.FromArgb(255, 255, 255, 255),
        ForegroundColor = Color.FromArgb(255, 0, 0, 0),
        FontFamily = "Segoe UI",
        FontSize = 10,
        IsDarkMode = false,
        IsActive = true // set as active by default
    };
    public static readonly Theme DarkTheme = new()
    {
        Name = "Dark",
        BackgroundColor = Color.FromArgb(255, 31, 31, 31),// "#1E1E1E",
        SecondaryBackgroundColor = Color.FromArgb(255, 0, 122, 204),
        ForegroundColor = Color.FromArgb(255, 212, 212, 212),
        FontFamily = "Segoe UI",
        FontSize = 10,
        IsDarkMode = true,
        IsActive = false
    };
    public static readonly Theme LightTheme = new()
    {
        Name = "Light",
        BackgroundColor = Color.FromArgb(255, 255, 255, 255),
        SecondaryBackgroundColor = Color.FromArgb(255, 255, 255, 255),
        ForegroundColor = Color.FromArgb(255, 0, 0, 0),
        FontFamily = "Segoe UI",
        FontSize = 10,
        IsDarkMode = false,
        IsActive = false
    };
        
    public static void AddThemes(params Theme[] themes)
    {
        if (themes == null || themes.Length == 0) throw new ArgumentNullException(nameof(themes), "No themes provided to add.");
        ThemeRoot savedThemes = Settings.Get<ThemeRoot>("SavedThemes") ?? new();
        foreach (var theme in themes)
        {
            if (!ValidTheme(theme)) continue; // skip invalid themes
            if (savedThemes?.Themes.Any(t => t.Id == theme.Id) == true) continue; // skip existing themes
            theme.IsActive = false; // set new theme as inactive by default
            savedThemes?.Themes.Add(theme);
        }
        Settings.Set("SavedThemes", savedThemes);
    }
    public static void ClearThemes()
    {
        Settings.Set<ThemeRoot>("SavedThemes", new());
    }
    public static void SetTheme(Theme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));

        ThemeRoot? themes = Settings.Get<ThemeRoot>("SavedThemes");
        if (!ValidTheme(theme)) return;

        // set theme properties
        bool isFound = false;
        theme.IsActive = true;

        themes?.Themes.ForEach(t =>
        {
            if (t.Name == theme.Name)
            {
                t = theme; // update existing theme
            }
            else
            {
                t.IsActive = false; // deactivate all themes
            }
        });
            
        ThemeRoot newThemes = new()
        {
            Themes = isFound ? [.. themes?.Themes ?? []] : [..themes?.Themes ?? [], theme],
        };
        Settings.Set("SavedThemes", newThemes);
    }
    public static Theme GetTheme(string presetName)
    {
        ThemeRoot? themes = Settings.Get<ThemeRoot>("SavedThemes");
        return themes?.Themes.First(theme => theme.Name == presetName) ?? DefaultTheme;
    }
    public static Theme GetCurrentTheme()
    {
        ThemeRoot? themes = Settings.Get<ThemeRoot>("SavedThemes");
        if (themes is not null && themes.HasThemes())
        {
            AddThemes(DefaultTheme, DarkTheme, LightTheme); // ensure default themes are added if none exist
        }
        themes = Settings.Get<ThemeRoot>("SavedThemes");
        try
        {
            return themes?.Themes.First(theme => theme.IsActive == true) ?? DefaultTheme;
        }
        catch
        {
            return DefaultTheme; // fallback to default theme on error
        } 
    }
    public static bool AnyActiveThemes()
    {
        ThemeRoot? themes = Settings.Get<ThemeRoot>("SavedThemes");
        return themes?.Themes.Any(theme => theme.IsActive) == true;
    }
        
    public static bool ValidTheme(Theme theme)
    {
        ThemeRoot? themes = Settings.Get<ThemeRoot>("SavedThemes");

        try
        {
            if (themes == null) return false;
            if (theme == null) throw new ArgumentException($"Theme is null");
            if (string.IsNullOrEmpty(theme.Name)) throw new ArgumentException($"Theme has an invalid name of '{theme.Name}'.", nameof(theme));
            if (themes?.Themes?.Any(t => t.Id == theme.Id) == true)
            {
                throw new ArgumentException($"Theme already exists with the same values.", nameof(theme));
            }

            if (theme.BackgroundColor is null) throw new ArgumentException($"BackgroundColor is invalid", nameof(theme));
            if (theme.ForegroundColor is null) throw new ArgumentException($"ForegroundColor is invalid", nameof(theme));
            if (theme.SecondaryBackgroundColor is null) throw new ArgumentException($"AccentColor is invalid", nameof(theme));
            if (string.IsNullOrEmpty(theme.FontFamily) || !IsFontFamilyAvailable(theme.FontFamily)) throw new ArgumentException($"Font Family is invalid", nameof(theme));
            if (theme.FontSize <= 0) throw new ArgumentException($"Font Size is invalid", nameof(theme));
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message);
            return false;
        }

        return true;
    }
    public static bool IsFontFamilyAvailable(string fontName)
    {
            
        foreach (string family in Fonts)
        {
            if (string.Equals(family, fontName, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
public class ThemeRoot
{
    public List<Theme>? Themes { get; set; }
    public bool HasActiveTheme() => Themes?.Any(t => t.IsActive) == true;
    public bool HasThemes() => Themes?.Count > 0;
}
public class Theme
{
    public required string Name { get; set; }
    public Color? BackgroundColor { get; set; }
    public Color? SecondaryBackgroundColor { get; set; }
    public Color? ForegroundColor { get; set; }
    public string FontFamily { get; set; }
    public int FontSize { get; set; }
    public bool IsDarkMode { get; set; }
    public bool IsActive { get; set; } = false;
    public int? Id { get => GetHashCode(); }
}