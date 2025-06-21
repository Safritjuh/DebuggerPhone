using System;
using System.Windows;
using System.Windows.Controls;
using WindowsSipPhone.Themes;

namespace WindowsSipPhone.Controls
{
    public partial class ThemeToggleControl : System.Windows.Controls.UserControl
    {
        public ThemeToggleControl()
        {
            InitializeComponent();
            InitializeThemeSettings();
            
            // Subscribe to theme changes
            ThemeManager.Instance.ThemeChanged += OnThemeChanged;
        }

        private void InitializeThemeSettings()
        {
            // Set current theme in combo box
            var currentTheme = ThemeManager.Instance.CurrentTheme;
            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag.ToString() == currentTheme.ToString())
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }

            UpdateQuickToggleButton();
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var themeTag = selectedItem.Tag.ToString();
                if (Enum.TryParse<ThemeManager.ThemeType>(themeTag, out var theme))
                {
                    Console.WriteLine($"[THEME DEBUG] User selected theme: {theme}");
                    ThemeManager.Instance.SetTheme(theme);
                }
            }
        }

        private void QuickToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var currentTheme = ThemeManager.Instance.CurrentTheme;
            var newTheme = currentTheme == ThemeManager.ThemeType.Light 
                ? ThemeManager.ThemeType.Dark 
                : ThemeManager.ThemeType.Light;

            Console.WriteLine($"[THEME DEBUG] Quick toggle: {currentTheme} -> {newTheme}");
            ThemeManager.Instance.SetTheme(newTheme);

            // Update combo box selection
            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag.ToString() == newTheme.ToString())
                {
                    ThemeComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void OnThemeChanged(object? sender, ThemeManager.ThemeType newTheme)
        {
            UpdateQuickToggleButton();
            Console.WriteLine($"[THEME DEBUG] Theme changed to: {newTheme}");
        }

        private void UpdateQuickToggleButton()
        {
            var currentTheme = ThemeManager.Instance.CurrentTheme;
            
            // Update button content and tooltip based on current theme
            switch (currentTheme)
            {
                case ThemeManager.ThemeType.Light:
                    QuickToggleButton.Content = "🌙";
                    QuickToggleButton.ToolTip = "Switch to Dark theme";
                    break;
                case ThemeManager.ThemeType.Dark:
                    QuickToggleButton.Content = "☀️";
                    QuickToggleButton.ToolTip = "Switch to Light theme";
                    break;
                case ThemeManager.ThemeType.Auto:
                    QuickToggleButton.Content = "🖥️";
                    QuickToggleButton.ToolTip = "Using system theme (click to toggle)";
                    break;
            }
        }

        // Clean up event subscription
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
        }
    }
}
