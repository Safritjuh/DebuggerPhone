using System;
using System.Windows;
using System.Windows.Controls;
using WindowsSipPhone.UI.Pages;
using WindowsSipPhone.UI.Windows;
using WindowsSipPhone.UI.Dialogs;

namespace WindowsSipPhone.UI.Windows
{    public partial class SettingsWindow : Window
    {
        private SipSettingsPage? _sipSettingsPage;
        private AudioSettingsPage? _audioSettingsPage;
        private SipPhoneService? _sipService;
        private SipMessagesWindow? _messagesWindow = null;
        private KeyboardShortcutService? _keyboardService;        private UI.Windows.MainWindow? _mainWindow;        // Removed _themeComboBox field - theme switching functionality removed per BUG-015
        private System.Windows.Controls.Button? _enableLoggingButton;
        private System.Windows.Controls.ComboBox? _ringtoneComboBox;        private IRingtoneService? _ringtoneService;

        public SettingsWindow(SipPhoneService? sipService = null, KeyboardShortcutService? keyboardService = null, UI.Windows.MainWindow? mainWindow = null, IRingtoneService? ringtoneService = null){
            InitializeComponent();
              _sipService = sipService;
            _keyboardService = keyboardService;
            _mainWindow = mainWindow;
            _ringtoneService = ringtoneService;
            
            // Initialize pages
            _sipSettingsPage = new SipSettingsPage();
            _audioSettingsPage = new AudioSettingsPage();
            
            // Set up the SIP service connection
            if (_sipService != null && _sipSettingsPage != null)
            {
                _sipSettingsPage.SipService = _sipService;
            }
              // Show SIP Settings by default
            ShowSipSettings();
            
            // Initialize logging button state (will be called again when debug tools are shown)
            // This ensures the button state is correct when the debug tools section is accessed
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button)
            {
                // Update button selection
                UpdateButtonSelection(button);
                
                // Navigate based on tag
                switch (button.Tag?.ToString())
                {
                    case "SipSettings":
                        ShowSipSettings();
                        break;
                    case "AudioSettings":
                        ShowAudioSettings();
                        break;
                    case "AppSettings":
                        ShowAppSettings();
                        break;
                    case "DebugTools":
                        ShowDebugTools();
                        break;
                }
            }
        }

        private void UpdateButtonSelection(System.Windows.Controls.Button selectedButton)
        {
            // Reset all buttons
            ResetButtonStyles();
            
            // Highlight selected button
            selectedButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));
            selectedButton.Foreground = System.Windows.Media.Brushes.White;
        }

        private void ResetButtonStyles()
        {
            var buttons = new[] { SipSettingsButton, AudioSettingsButton, AppSettingsButton, DebugToolsButton };
            
            foreach (var button in buttons)
            {
                button.Background = System.Windows.Media.Brushes.Transparent;
                button.Foreground = System.Windows.Media.Brushes.Black;
            }
        }        private void ShowSipSettings()
        {
            ContentArea.Content = _sipSettingsPage;
            UpdateButtonSelection(SipSettingsButton);
        }

        private void ShowAudioSettings()
        {
            ContentArea.Content = _audioSettingsPage;
            UpdateButtonSelection(AudioSettingsButton);
        }

        private void ShowAppSettings()
        {
            ContentArea.Content = CreateAppSettingsPlaceholder();
            UpdateButtonSelection(AppSettingsButton);
        }

        private void ShowDebugTools()
        {
            ContentArea.Content = CreateDebugToolsPlaceholder();
            UpdateButtonSelection(DebugToolsButton);
        }private System.Windows.Controls.UserControl CreateAppSettingsPlaceholder()
        {
            var userControl = new System.Windows.Controls.UserControl();
            
            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });            
            // Header
            var headerBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(155, 89, 182)), // Purple
                Padding = new Thickness(20, 15, 20, 15)
            };
            System.Windows.Controls.Grid.SetRow(headerBorder, 0);
            
            var headerStack = new System.Windows.Controls.StackPanel();
            var headerTitle = new System.Windows.Controls.TextBlock
            {
                Text = "⚙️ Application Settings",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White
            };            var headerSubtitle = new System.Windows.Controls.TextBlock
            {
                Text = "Configure shortcuts and application preferences",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 222, 240)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            
            headerStack.Children.Add(headerTitle);
            headerStack.Children.Add(headerSubtitle);
            headerBorder.Child = headerStack;
            grid.Children.Add(headerBorder);
            
            // Content
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Margin = new Thickness(20)
            };
            System.Windows.Controls.Grid.SetRow(scrollViewer, 1);
            
            var stackPanel = new System.Windows.Controls.StackPanel();
              // Theme Settings Section - REMOVED per BUG-015
            // Theme switching functionality has been removed as it was not functional
            
            // Keyboard Shortcuts Section
            var shortcutsSection = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var shortcutsSectionContent = new System.Windows.Controls.StackPanel();
            
            var shortcutsTitle = new System.Windows.Controls.TextBlock
            {
                Text = "⌨️ Keyboard Shortcuts",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var shortcutsDescription = new System.Windows.Controls.TextBlock
            {
                Text = "Configure F1-F12 speed dial and other keyboard shortcuts",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 140, 141)),
                Margin = new Thickness(0, 0, 0, 15)
            };
              var configureShortcutsButton = new System.Windows.Controls.Button
            {
                Content = "Configure Shortcuts",
                Height = 35,
                Width = 150,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 87, 34)),
                Foreground = System.Windows.Media.Brushes.White
            };
            configureShortcutsButton.Click += ConfigureShortcuts_Click;
            
            shortcutsSectionContent.Children.Add(shortcutsTitle);
            shortcutsSectionContent.Children.Add(shortcutsDescription);
            shortcutsSectionContent.Children.Add(configureShortcutsButton);
            shortcutsSection.Child = shortcutsSectionContent;
            stackPanel.Children.Add(shortcutsSection);
            
            // Startup Settings Section
            var startupSection = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var startupSectionContent = new System.Windows.Controls.StackPanel();
            
            var startupTitle = new System.Windows.Controls.TextBlock
            {
                Text = "🚀 Startup Settings",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var startupDescription = new System.Windows.Controls.TextBlock
            {
                Text = "Configure application startup behavior and preferences",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 140, 141)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var autoRegisterCheck = new System.Windows.Controls.CheckBox
            {
                Content = "Auto-register on startup",
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var startMinimizedCheck = new System.Windows.Controls.CheckBox
            {
                Content = "Start minimized to system tray",
                Margin = new Thickness(0, 0, 0, 20)
            };
              startupSectionContent.Children.Add(startupTitle);
            startupSectionContent.Children.Add(startupDescription);
            startupSectionContent.Children.Add(autoRegisterCheck);
            startupSectionContent.Children.Add(startMinimizedCheck);
            startupSection.Child = startupSectionContent;
            stackPanel.Children.Add(startupSection);
            
            // Ringtone Settings Section
            var ringtoneSection = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var ringtoneSectionContent = new System.Windows.Controls.StackPanel();
            
            var ringtoneTitle = new System.Windows.Controls.TextBlock
            {
                Text = "🔔 Ringtone Settings",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var ringtoneDescription = new System.Windows.Controls.TextBlock
            {
                Text = "Select the ringtone to play for incoming calls",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 140, 141)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var ringtoneGrid = new System.Windows.Controls.Grid();
            ringtoneGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(120) });
            ringtoneGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ringtoneGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(80) });
            
            var ringtoneLabel = new System.Windows.Controls.TextBlock
            {
                Text = "Ringtone:",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Medium
            };
            System.Windows.Controls.Grid.SetColumn(ringtoneLabel, 0);
              var ringtoneCombo = new System.Windows.Controls.ComboBox
            {
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0)
            };
            ringtoneCombo.Items.Add("Default Ring");
            ringtoneCombo.Items.Add("Classic Phone");
            ringtoneCombo.Items.Add("Modern Chime");
            ringtoneCombo.Items.Add("Old School Bell");
            ringtoneCombo.Items.Add("Notification Sound");
            ringtoneCombo.SelectedIndex = 0; // Default selection
              // Store reference for later use
            _ringtoneComboBox = ringtoneCombo;
            
            // Initialize selection from RingtoneService
            if (_ringtoneService != null)
            {
                var currentRingtone = _ringtoneService.SelectedRingtone;
                for (int i = 0; i < ringtoneCombo.Items.Count; i++)
                {
                    if (ringtoneCombo.Items[i].ToString() == currentRingtone)
                    {
                        ringtoneCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            
            // Handle selection changes
            ringtoneCombo.SelectionChanged += (s, e) =>
            {
                if (_ringtoneService != null && ringtoneCombo.SelectedItem != null)
                {
                    _ringtoneService.SelectedRingtone = ringtoneCombo.SelectedItem.ToString()!;
                }
            };
            
            System.Windows.Controls.Grid.SetColumn(ringtoneCombo, 1);
            
            var testRingtoneButton = new System.Windows.Controls.Button
            {
                Content = "🔊 Test",
                Height = 35,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12
            };
            testRingtoneButton.Click += TestRingtone_Click;
            System.Windows.Controls.Grid.SetColumn(testRingtoneButton, 2);
            
            ringtoneGrid.Children.Add(ringtoneLabel);
            ringtoneGrid.Children.Add(ringtoneCombo);
            ringtoneGrid.Children.Add(testRingtoneButton);
            
            ringtoneSectionContent.Children.Add(ringtoneTitle);
            ringtoneSectionContent.Children.Add(ringtoneDescription);
            ringtoneSectionContent.Children.Add(ringtoneGrid);
            ringtoneSection.Child = ringtoneSectionContent;
            stackPanel.Children.Add(ringtoneSection);
            
            scrollViewer.Content = stackPanel;
            grid.Children.Add(scrollViewer);
            userControl.Content = grid;
            return userControl;
        }        private System.Windows.Controls.UserControl CreateDebugToolsPlaceholder()
        {
            var userControl = new System.Windows.Controls.UserControl();
            
            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });            
            // Header
            var headerBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)), // Blue
                Padding = new Thickness(20, 15, 20, 15)
            };
            System.Windows.Controls.Grid.SetRow(headerBorder, 0);
            
            var headerStack = new System.Windows.Controls.StackPanel();
            var headerTitle = new System.Windows.Controls.TextBlock
            {
                Text = "🐛 Debug Tools",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White
            };
            var headerSubtitle = new System.Windows.Controls.TextBlock
            {
                Text = "SIP debugging, logging, and diagnostic tools",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(213, 245, 255)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            
            headerStack.Children.Add(headerTitle);
            headerStack.Children.Add(headerSubtitle);
            headerBorder.Child = headerStack;
            grid.Children.Add(headerBorder);
            
            // Content
            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                Margin = new Thickness(20)
            };
            System.Windows.Controls.Grid.SetRow(scrollViewer, 1);
            
            var stackPanel = new System.Windows.Controls.StackPanel();
            
            // SIP Debug Section
            var debugSection = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var debugSectionContent = new System.Windows.Controls.StackPanel();
            
            var debugTitle = new System.Windows.Controls.TextBlock
            {
                Text = "� SIP Debug",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var debugDescription = new System.Windows.Controls.TextBlock
            {
                Text = "Monitor SIP messages and protocol communications for troubleshooting",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 140, 141)),
                Margin = new Thickness(0, 0, 0, 15)
            };
              var sipDebugButton = new System.Windows.Controls.Button
            {
                Content = "📊 Open SIP Messages",
                Height = 35,
                Width = 160,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                Foreground = System.Windows.Media.Brushes.White
            };
            sipDebugButton.Click += SipDebug_Click;
            
            debugSectionContent.Children.Add(debugTitle);
            debugSectionContent.Children.Add(debugDescription);
            debugSectionContent.Children.Add(sipDebugButton);
            debugSection.Child = debugSectionContent;
            stackPanel.Children.Add(debugSection);
            
            // Additional debug tools section
            var loggingSection = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var loggingSectionContent = new System.Windows.Controls.StackPanel();
            
            var loggingTitle = new System.Windows.Controls.TextBlock
            {
                Text = "📝 Logging & Diagnostics",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 62, 80)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            var loggingDescription = new System.Windows.Controls.TextBlock
            {
                Text = "Configure logging levels and diagnostic output for troubleshooting",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 140, 141)),
                Margin = new Thickness(0, 0, 0, 15)
            };            _enableLoggingButton = new System.Windows.Controls.Button
            {
                Content = "Enable Detailed Logging",
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 5, 10, 5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Medium,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _enableLoggingButton.Click += EnableLoggingButton_Click;
            
            // Initialize button state based on current logging window visibility
            if (_mainWindow != null)
            {
                bool isLoggingVisible = _mainWindow.IsLoggingWindowVisible;
                _enableLoggingButton.Content = isLoggingVisible ? "Disable Detailed Logging" : "Enable Detailed Logging";
                
                // Set initial button color based on state
                if (isLoggingVisible)
                {
                    _enableLoggingButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); // Red for "disable"
                }
                else
                {
                    _enableLoggingButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)); // Blue for "enable"
                }
            }
            
            var logLevelGrid = new System.Windows.Controls.Grid();            logLevelGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(100) });
            logLevelGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var logLevelLabel = new System.Windows.Controls.TextBlock
            {
                Text = "Log Level:",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Medium
            };
            System.Windows.Controls.Grid.SetColumn(logLevelLabel, 0);
            
            var logLevelCombo = new System.Windows.Controls.ComboBox
            {
                Height = 35,
                Margin = new Thickness(0, 0, 0, 20)
            };
            logLevelCombo.Items.Add("Error");
            logLevelCombo.Items.Add("Warning"); 
            logLevelCombo.Items.Add("Info");
            logLevelCombo.Items.Add("Debug");
            logLevelCombo.Items.Add("Verbose");
            logLevelCombo.SelectedIndex = 2; // Info by default
            System.Windows.Controls.Grid.SetColumn(logLevelCombo, 1);
            
            logLevelGrid.Children.Add(logLevelLabel);
            logLevelGrid.Children.Add(logLevelCombo);
            
            loggingSectionContent.Children.Add(loggingTitle);
            loggingSectionContent.Children.Add(loggingDescription);
            loggingSectionContent.Children.Add(_enableLoggingButton);            loggingSectionContent.Children.Add(logLevelGrid);
            loggingSection.Child = loggingSectionContent;
            stackPanel.Children.Add(loggingSection);
            
            scrollViewer.Content = stackPanel;
            grid.Children.Add(scrollViewer);
            userControl.Content = grid;
            return userControl;
        }        // Theme-related methods removed per BUG-015
        // Theme switching functionality was non-functional and has been removed

        private void ConfigureShortcuts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open the Speed Dial Configuration window
                if (_keyboardService != null)
                {
                    var speedDialWindow = new SpeedDialConfigWindow(_keyboardService);
                    speedDialWindow.Owner = this;
                    speedDialWindow.ShowDialog();
                }                else
                {
                    System.Windows.MessageBox.Show("Keyboard shortcuts service is not available.", "Error", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening keyboard shortcuts configuration: {ex.Message}", 
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SipDebug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open or bring to front the SIP debugging window
                if (_messagesWindow == null || _messagesWindow.IsClosed)
                {
                    _messagesWindow = new SipMessagesWindow();
                    // Remove Owner to make it non-modal
                    
                    // Connect SIP service to the messages window for real-time debugging
                    if (_sipService != null)
                    {
                        // Subscribe to SIP service events for real-time message capture
                        _sipService.MessageReceived += (sender, message) => 
                        {
                            _messagesWindow?.AddSipMessage("INCOMING", message);
                        };
                        
                        // Update window with current connection info if registered
                        if (_sipService.IsRegistered)
                        {
                            _messagesWindow.UpdateConnectionStatus(true);
                            _messagesWindow.AddSipMessage("SYSTEM", $"Connected to {_sipService.ServerAddress}");
                        }
                    }
                    
                    _messagesWindow.Show();
                    
                    // Position the window to avoid overlap
                    if (_mainWindow != null)
                    {
                        _messagesWindow.Left = _mainWindow.Left + _mainWindow.Width + 10;
                        _messagesWindow.Top = _mainWindow.Top + 100; // Offset slightly from logging window
                    }
                }                else
                {
                    // Bring existing window to front
                    _messagesWindow.Activate();
                    _messagesWindow.WindowState = WindowState.Normal;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening SIP debug window: {ex.Message}", 
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings and close (for non-modal window)
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Close without saving (for non-modal window)
            Close();
        }        // InitializeThemeSelection method removed per BUG-015
        // Theme functionality has been removed as it was non-functional

        private void EnableLoggingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_mainWindow != null)
                {
                    if (_mainWindow.IsLoggingWindowVisible)
                    {
                        // Hide the logging window
                        _mainWindow.HideLoggingWindow();
                    }
                    else
                    {
                        // Show the logging window
                        _mainWindow.ShowLoggingWindow();
                    }
                    
                    // Update button appearance after toggle
                    UpdateLoggingButtonState();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error toggling logging window: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateLoggingButtonState()
        {
            if (_enableLoggingButton != null && _mainWindow != null)
            {
                bool isLoggingVisible = _mainWindow.IsLoggingWindowVisible;
                _enableLoggingButton.Content = isLoggingVisible ? "Disable Detailed Logging" : "Enable Detailed Logging";
                
                // Update button color based on state
                if (isLoggingVisible)
                {
                    _enableLoggingButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); // Red for "disable"
                }
                else
                {
                    _enableLoggingButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)); // Blue for "enable"
                }
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            UpdateLoggingButtonState();
        }        private void TestRingtone_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_ringtoneComboBox == null || _ringtoneService == null)
                    return;
                    
                var selectedRingtone = _ringtoneComboBox.SelectedItem?.ToString() ?? "Default Ring";
                
                // Use the RingtoneService to play the selected ringtone
                _ringtoneService.PlayRingtone(selectedRingtone);
                
                // Show feedback to user
                System.Windows.MessageBox.Show($"Playing ringtone: {selectedRingtone}", "Ringtone Test", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error testing ringtone: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }        }
    }
}
