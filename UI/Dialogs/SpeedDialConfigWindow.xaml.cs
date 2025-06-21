using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WindowsSipPhone.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for SpeedDialConfigWindow.xaml
    /// </summary>
    public partial class SpeedDialConfigWindow : Window
    {
        private readonly KeyboardShortcutService _shortcutService;
        private readonly Dictionary<string, System.Windows.Controls.TextBox> _speedDialTextBoxes = new();

        public SpeedDialConfigWindow(KeyboardShortcutService shortcutService)
        {
            InitializeComponent();
            _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
            
            CreateSpeedDialEntries();
            LoadCurrentSettings();
        }

        private void CreateSpeedDialEntries()
        {
            SpeedDialPanel.Children.Clear();
            _speedDialTextBoxes.Clear();

            for (int i = 1; i <= 12; i++)
            {
                var functionKey = $"F{i}";
                
                // Create container for each speed dial entry
                var border = new Border
                {
                    Background = System.Windows.Media.Brushes.White,
                    BorderBrush = System.Windows.Media.Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(15)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

                // Function key label
                var keyLabel = new TextBlock
                {
                    Text = functionKey,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = System.Windows.Media.Brushes.DarkBlue
                };
                Grid.SetColumn(keyLabel, 0);

                // Number input textbox
                var numberTextBox = new System.Windows.Controls.TextBox
                {
                    Style = (Style)FindResource("ModernTextBoxStyle"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 0),
                    ToolTip = $"Enter phone number for {functionKey} speed dial"
                };
                Grid.SetColumn(numberTextBox, 1);
                _speedDialTextBoxes[functionKey] = numberTextBox;

                // Description label
                var descriptionLabel = new TextBlock
                {
                    Text = GetDefaultDescription(functionKey),
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(5, 0, 10, 0)
                };
                Grid.SetColumn(descriptionLabel, 2);

                // Clear button
                var clearButton = new System.Windows.Controls.Button
                {
                    Content = "🗑️",
                    Width = 30,
                    Height = 30,
                    ToolTip = "Clear this speed dial",
                    Background = System.Windows.Media.Brushes.LightCoral,
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                clearButton.Click += (s, e) => ClearSpeedDial(functionKey);
                Grid.SetColumn(clearButton, 3);

                grid.Children.Add(keyLabel);
                grid.Children.Add(numberTextBox);
                grid.Children.Add(descriptionLabel);
                grid.Children.Add(clearButton);

                border.Child = grid;
                SpeedDialPanel.Children.Add(border);
            }
        }

        private string GetDefaultDescription(string functionKey)
        {
            return functionKey switch
            {
                "F1" => "Extension 101",
                "F2" => "Extension 102", 
                "F3" => "Extension 103",
                "F4" => "Extension 104",
                "F5" => "Extension 105",
                "F6" => "Extension 106",
                "F7" => "Extension 107",
                "F8" => "Extension 108",
                "F9" => "Emergency",
                "F10" => "Information",
                "F11" => "Operator",
                "F12" => "Custom",
                _ => "Speed Dial"
            };
        }

        private void LoadCurrentSettings()
        {
            try
            {
                var speedDialNumbers = _shortcutService.GetAllSpeedDialNumbers();
                
                foreach (var kvp in speedDialNumbers)
                {
                    if (_speedDialTextBoxes.TryGetValue(kvp.Key, out var textBox))
                    {
                        textBox.Text = kvp.Value ?? "";
                    }
                }

                Console.WriteLine("[SPEED DIAL] Current settings loaded");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading speed dial settings: {ex.Message}", 
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ClearSpeedDial(string functionKey)
        {
            if (_speedDialTextBoxes.TryGetValue(functionKey, out var textBox))
            {
                textBox.Text = "";
            }
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "This will reset all speed dial numbers to default values. Continue?",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Set default values
                var defaults = new Dictionary<string, string>
                {
                    ["F1"] = "101",
                    ["F2"] = "102",
                    ["F3"] = "103",
                    ["F4"] = "104",
                    ["F5"] = "105",
                    ["F6"] = "106",
                    ["F7"] = "107",
                    ["F8"] = "108",
                    ["F9"] = "911",
                    ["F10"] = "411",
                    ["F11"] = "0",
                    ["F12"] = ""
                };

                foreach (var kvp in defaults)
                {
                    if (_speedDialTextBoxes.TryGetValue(kvp.Key, out var textBox))
                    {
                        textBox.Text = kvp.Value;
                    }
                }

                System.Windows.MessageBox.Show("Speed dial settings reset to defaults.", 
                    "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate and save all speed dial numbers
                foreach (var kvp in _speedDialTextBoxes)
                {
                    var number = kvp.Value.Text.Trim();
                    
                    // Basic validation
                    if (!string.IsNullOrEmpty(number) && !IsValidPhoneNumber(number))
                    {
                        System.Windows.MessageBox.Show($"Invalid phone number for {kvp.Key}: '{number}'\n\n" +
                            "Phone numbers should contain only digits, spaces, +, -, (, ), *, and #", 
                            "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        kvp.Value.Focus();
                        return;
                    }

                    _shortcutService.SetSpeedDialNumber(kvp.Key, number);
                }

                // TODO: Save to configuration file for persistence
                SaveToConfiguration();

                System.Windows.MessageBox.Show("Speed dial configuration saved successfully!", 
                    "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving speed dial configuration: {ex.Message}", 
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsValidPhoneNumber(string number)
        {
            if (string.IsNullOrEmpty(number)) return true;
            
            // Allow digits, spaces, +, -, (, ), *, #
            foreach (char c in number)
            {
                if (!char.IsDigit(c) && c != ' ' && c != '+' && c != '-' && 
                    c != '(' && c != ')' && c != '*' && c != '#')
                {
                    return false;
                }
            }
            
            return true;
        }

        private void SaveToConfiguration()
        {
            try
            {
                // TODO: Implement configuration persistence
                // For now, just log the action
                Console.WriteLine("[SPEED DIAL] Configuration saved to keyboard shortcut service");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SPEED DIAL] ⚠️ Error saving configuration: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // Handle Escape key to close dialog
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Cancel_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }
    }
}
