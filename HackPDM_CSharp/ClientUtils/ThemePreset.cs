using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HackPDM.ClientUtils
{
    public static class ThemePreset
    {
        public static readonly Theme DefaultTheme = new()
        {
            Name = "Default",
            BackgroundColor = Color.WhiteSmoke,
            SecondaryBackgroundColor = Color.White,
            ForegroundColor = Color.Black,
            FontFamily = "Segoe UI",
            FontSize = 10,
            IsDarkMode = false,
            IsActive = true // set as active by default
        };
        public static readonly Theme DarkTheme = new()
        {
            Name = "Dark",
            BackgroundColor = Color.FromArgb(31, 31, 31),// "#1E1E1E",
            SecondaryBackgroundColor = Color.FromArgb(0, 122, 204),
            ForegroundColor = Color.FromArgb(212, 212, 212),
            FontFamily = "Segoe UI",
            FontSize = 10,
            IsDarkMode = true,
            IsActive = false
        };
        public static readonly Theme LightTheme = new()
        {
            Name = "Light",
            BackgroundColor = Color.White,
            SecondaryBackgroundColor = Color.FromArgb(255, 255, 255),
            ForegroundColor = Color.Black,
            FontFamily = "Segoe UI",
            FontSize = 10,
            IsDarkMode = false,
            IsActive = false
        };
        
        public static void AddThemes(params Theme[] themes)
        {
            Debug.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new ThemeRoot { Themes = [.. themes] }));
            if (themes == null || themes.Length == 0) throw new ArgumentNullException(nameof(themes), "No themes provided to add.");
            string jsonTheme = HackPDM.Properties.AppSettings.Default.Theme;
            ThemeRoot themeRoot = Newtonsoft.Json.JsonConvert.DeserializeObject<ThemeRoot>(jsonTheme) ?? new ThemeRoot { Themes = [] };
            foreach (var theme in themes)
            {
                if (!ValidTheme(theme)) continue; // skip invalid themes
                if (themeRoot.Themes.Any(t => t.Id == theme.Id)) continue; // skip existing themes
                theme.IsActive = false; // set new theme as inactive by default
                themeRoot.Themes.Add(theme);
            }
            string themeStr = Newtonsoft.Json.JsonConvert.SerializeObject(themeRoot);
            HackPDM.Properties.AppSettings.Default.Theme = themeStr;
            HackPDM.Properties.AppSettings.Default.Save();
        }
        public static void ClearThemes()
        {
            HackPDM.Properties.AppSettings.Default.Theme = Newtonsoft.Json.JsonConvert.SerializeObject(new ThemeRoot { Themes = [] });
            HackPDM.Properties.AppSettings.Default.Save();
        }
        public static void SetTheme(Theme theme)
        {
            if (theme == null) throw new ArgumentNullException(nameof(theme));

            string jsonTheme = HackPDM.Properties.AppSettings.Default.Theme;
            ThemeRoot themeRoot = Newtonsoft.Json.JsonConvert.DeserializeObject<ThemeRoot>(jsonTheme);
            if (!ValidTheme(theme)) return;

            // set theme properties
            bool isFound = false;
            theme.IsActive = true;

            themeRoot.Themes.ForEach(t =>
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
            
            ThemeRoot themes = new()
            {
                Themes = isFound ? [.. themeRoot.Themes] : [.. themeRoot.Themes, theme]
            };
            string themeStr = Newtonsoft.Json.JsonConvert.SerializeObject(themes);
            HackPDM.Properties.AppSettings.Default.Theme = themeStr;
            HackPDM.Properties.AppSettings.Default.Save();
        }
        public static Theme GetTheme(string presetName)
        {
            string jsonTheme = HackPDM.Properties.AppSettings.Default.Theme;
            ThemeRoot themeRoot = Newtonsoft.Json.JsonConvert.DeserializeObject<ThemeRoot>(jsonTheme);
            return themeRoot?.Themes.First(theme => theme.Name == presetName) ?? DefaultTheme;
        }
        public static Theme GetCurrentTheme()
        {
            string jsonTheme = HackPDM.Properties.AppSettings.Default.Theme;
            ThemeRoot themeRoot = Newtonsoft.Json.JsonConvert.DeserializeObject<ThemeRoot>(jsonTheme);
            if (string.IsNullOrEmpty(jsonTheme) || !themeRoot.HasThemes())
            {
                AddThemes(DefaultTheme, DarkTheme, LightTheme); // ensure default themes are added if none exist
                jsonTheme = HackPDM.Properties.AppSettings.Default.Theme; // re-fetch after adding defaults
            }
            themeRoot = Newtonsoft.Json.JsonConvert.DeserializeObject<ThemeRoot>(jsonTheme);
            try
            {
                return themeRoot?.Themes.First(theme => theme.IsActive == true) ?? DefaultTheme;
            }
            catch
            {
                return DefaultTheme; // fallback to default theme on error
            } 
        }
        public static bool AnyActiveThemes()
        {
            string jsonTheme = HackPDM.Properties.AppSettings.Default.Theme;
            ThemeRoot themeRoot = Newtonsoft.Json.JsonConvert.DeserializeObject<ThemeRoot>(jsonTheme);
            return themeRoot?.Themes.Any(theme => theme.IsActive) == true;
        }
        
        public static bool ValidTheme(Theme theme)
        {
            string jsonTheme = HackPDM.Properties.AppSettings.Default.Theme;
            ThemeRoot themeRoot = string.IsNullOrEmpty(jsonTheme) ? new ThemeRoot { } : Newtonsoft.Json.JsonConvert.DeserializeObject<ThemeRoot>(jsonTheme);

            try
            {
                if (theme == null) throw new ArgumentException($"Theme is null");
                if (string.IsNullOrEmpty(theme.Name)) throw new ArgumentException($"Theme has an invalid name of '{theme.Name}'.", nameof(theme));
                if (themeRoot?.Themes?.Any(t => t.Id == theme.Id) == true)
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
            InstalledFontCollection fonts = new();
            foreach (FontFamily family in fonts.Families)
            {
                if (string.Equals(family.Name, fontName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
    public class ThemeRoot
    {
        public List<Theme> Themes { get; set; }
        public bool HasActiveTheme() => Themes?.Any(t => t.IsActive) == true;
        public bool HasThemes() => Themes?.Count > 0;
    }
    public class Theme
    {
        public string Name { get; set; }
        public Color? BackgroundColor { get; set; }
        public Color? SecondaryBackgroundColor { get; set; }
        public Color? ForegroundColor { get; set; }
        public string FontFamily { get; set; }
        public int FontSize { get; set; }
        public bool IsDarkMode { get; set; }
        public bool IsActive { get; set; } = false;
        public int? Id { get => GetHashCode(); }
    }
}
