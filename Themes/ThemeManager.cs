using System;
using System.Windows;
using System.Windows.Media;

namespace WindowsSipPhone.Themes
{
    public class ThemeManager
    {
        public enum ThemeType
        {
            Light,
            Dark,
            Auto // Follows Windows system theme
        }

        private static ThemeManager? _instance;
        public static ThemeManager Instance => _instance ??= new ThemeManager();

        public ThemeType CurrentTheme { get; private set; } = ThemeType.Light;

        public event EventHandler<ThemeType>? ThemeChanged;

        private ThemeManager()
        {
            // Initialize with system theme if possible
            DetectSystemTheme();
        }        public void SetTheme(ThemeType theme)
        {
            if (CurrentTheme == theme) 
            {
                Console.WriteLine($"[THEME DEBUG] Theme {theme} is already active, skipping");
                return;
            }

            Console.WriteLine($"[THEME DEBUG] Changing theme from {CurrentTheme} to {theme}");
            CurrentTheme = theme;
            ApplyTheme();
            ThemeChanged?.Invoke(this, theme);
            Console.WriteLine($"[THEME DEBUG] Theme change completed: {CurrentTheme}");
        }

        public void ToggleTheme()
        {
            var newTheme = CurrentTheme == ThemeType.Light ? ThemeType.Dark : ThemeType.Light;
            SetTheme(newTheme);
        }        private void ApplyTheme()
        {
            var app = System.Windows.Application.Current;
            if (app?.Resources == null) return;

            try
            {
                // Clear existing theme resources
                Console.WriteLine($"[THEME DEBUG] Applying theme: {CurrentTheme}");
                Console.WriteLine($"[THEME DEBUG] Current merged dictionaries count: {app.Resources.MergedDictionaries.Count}");
                
                // Remove any existing theme dictionaries
                for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                {
                    var dict = app.Resources.MergedDictionaries[i];
                    var source = dict.Source?.ToString() ?? "";
                    
                    if (source.Contains("LightTheme.xaml") || source.Contains("DarkTheme.xaml"))
                    {
                        Console.WriteLine($"[THEME DEBUG] Removing existing theme dictionary: {source}");
                        app.Resources.MergedDictionaries.RemoveAt(i);
                    }
                }

                // Apply new theme
                string themeFile = CurrentTheme switch
                {
                    ThemeType.Dark => "Themes/DarkTheme.xaml",
                    ThemeType.Auto => GetSystemTheme() == ThemeType.Dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml",
                    _ => "Themes/LightTheme.xaml"
                };

                Console.WriteLine($"[THEME DEBUG] Loading theme file: {themeFile}");
                
                var themeDict = new ResourceDictionary
                {
                    Source = new Uri(themeFile, UriKind.Relative)
                };
                  app.Resources.MergedDictionaries.Insert(0, themeDict);
                Console.WriteLine($"[THEME DEBUG] Theme dictionary added successfully. Total dictionaries: {app.Resources.MergedDictionaries.Count}");
                
                // Force UI refresh by updating all windows
                ForceUIRefresh();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[THEME DEBUG] Failed to apply theme: {ex.Message}");
                Console.WriteLine($"[THEME DEBUG] Stack trace: {ex.StackTrace}");
            }
        }

        private void DetectSystemTheme()
        {
            try
            {
                var systemTheme = GetSystemTheme();
                CurrentTheme = systemTheme;
            }
            catch
            {
                CurrentTheme = ThemeType.Light; // Fallback
            }
        }

        private ThemeType GetSystemTheme()
        {
            try
            {
                // Check Windows 10/11 system theme setting
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                
                if (value is int intValue)
                {
                    return intValue == 0 ? ThemeType.Dark : ThemeType.Light;
                }
            }
            catch
            {
                // Fallback for older Windows or registry access issues
            }

            return ThemeType.Light;
        }

        private void ForceUIRefresh()
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app == null) return;

                // Force refresh all windows
                foreach (Window window in app.Windows)
                {
                    if (window != null && window.IsLoaded)
                    {
                        window.InvalidateVisual();
                        window.UpdateLayout();
                        Console.WriteLine($"[THEME DEBUG] Refreshed window: {window.GetType().Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[THEME DEBUG] Error during UI refresh: {ex.Message}");
            }
        }

        // Predefined color palettes
        public static class Colors
        {
            // Light theme colors
            public static readonly SolidColorBrush LightBackground = new(System.Windows.Media.Color.FromRgb(248, 249, 250));
            public static readonly SolidColorBrush LightSurface = new(System.Windows.Media.Color.FromRgb(255, 255, 255));
            public static readonly SolidColorBrush LightPrimary = new(System.Windows.Media.Color.FromRgb(0, 120, 215));
            public static readonly SolidColorBrush LightSecondary = new(System.Windows.Media.Color.FromRgb(106, 106, 106));
            public static readonly SolidColorBrush LightText = new(System.Windows.Media.Color.FromRgb(32, 32, 32));
            public static readonly SolidColorBrush LightTextSecondary = new(System.Windows.Media.Color.FromRgb(96, 96, 96));
            public static readonly SolidColorBrush LightBorder = new(System.Windows.Media.Color.FromRgb(225, 225, 225));

            // Dark theme colors
            public static readonly SolidColorBrush DarkBackground = new(System.Windows.Media.Color.FromRgb(32, 32, 32));
            public static readonly SolidColorBrush DarkSurface = new(System.Windows.Media.Color.FromRgb(43, 43, 43));
            public static readonly SolidColorBrush DarkPrimary = new(System.Windows.Media.Color.FromRgb(96, 165, 250));
            public static readonly SolidColorBrush DarkSecondary = new(System.Windows.Media.Color.FromRgb(156, 156, 156));
            public static readonly SolidColorBrush DarkText = new(System.Windows.Media.Color.FromRgb(248, 248, 248));
            public static readonly SolidColorBrush DarkTextSecondary = new(System.Windows.Media.Color.FromRgb(192, 192, 192));
            public static readonly SolidColorBrush DarkBorder = new(System.Windows.Media.Color.FromRgb(64, 64, 64));

            // Status colors (same for both themes)
            public static readonly SolidColorBrush Success = new(System.Windows.Media.Color.FromRgb(34, 197, 94));
            public static readonly SolidColorBrush Warning = new(System.Windows.Media.Color.FromRgb(251, 191, 36));
            public static readonly SolidColorBrush Error = new(System.Windows.Media.Color.FromRgb(239, 68, 68));
            public static readonly SolidColorBrush Info = new(System.Windows.Media.Color.FromRgb(59, 130, 246));
        }
    }
}
