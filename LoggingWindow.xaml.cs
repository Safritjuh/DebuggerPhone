using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsSipPhone.Services;
using WindowsSipPhone.Models;

namespace WindowsSipPhone
{
    public partial class LoggingWindow : Window, INotifyPropertyChanged
    {
        private readonly ApplicationLogger _applicationLogger;
        private readonly ObservableCollection<LogEntry> _filteredLogEntries = new();
        private readonly DispatcherTimer _updateTimer;
        private DateTime _lastUpdateTime = DateTime.Now;
        private int _lastLogCount = 0;
        private bool _isLoggingActive = false;

        public event PropertyChangedEventHandler? PropertyChanged;        public LoggingWindow()
        {
            InitializeComponent();
            
            _applicationLogger = ApplicationLogger.Instance;
            DataContext = this;
            
            // Setup UI bindings
            LogListView.ItemsSource = _filteredLogEntries;
            
            // Wire up event handlers
            StartLoggingButton.Click += StartLoggingButton_Click;
            StopLoggingButton.Click += StopLoggingButton_Click;
            ClearLogsButton.Click += ClearLogsButton_Click;
            SaveLogsButton.Click += SaveLogsButton_Click;
            LogLevelFilter.SelectionChanged += LogLevelFilter_SelectionChanged;
            
            // Setup update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
            
            // Subscribe to logger changes
            _applicationLogger.PropertyChanged += ApplicationLogger_PropertyChanged;
            
            // Set initial position (will be adjusted by caller if needed)
            SetInitialPosition();
            
            // Initial UI update
            UpdateUI();
            RefreshLogEntries();
        }

        private void SetInitialPosition()
        {
            // Try to position the window on the right side of the screen
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            // Position on right side of screen, leaving some margin
            Left = Math.Max(0, screenWidth - Width - 50);
            Top = Math.Max(0, (screenHeight - Height) / 2);
        }

        private void StartLoggingButton_Click(object sender, RoutedEventArgs e)
        {
            _applicationLogger.IsLoggingEnabled = true;
            _isLoggingActive = true;
            UpdateUI();
        }

        private void StopLoggingButton_Click(object sender, RoutedEventArgs e)
        {
            _applicationLogger.IsLoggingEnabled = false;
            _isLoggingActive = false;
            UpdateUI();
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            _applicationLogger.ClearLogs();
            _filteredLogEntries.Clear();
            UpdateUI();
        }        private async void SaveLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
                    FileName = $"SipPhone_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var logContent = string.Join(Environment.NewLine, 
                        _filteredLogEntries.Select(entry => 
                            $"[{entry.FullTimestamp}] [{entry.Level}] [{entry.Source}] {entry.Message}"));
                    
                    await File.WriteAllTextAsync(saveDialog.FileName, logContent);
                      System.Windows.MessageBox.Show($"Logs saved successfully to:\n{saveDialog.FileName}", 
                                  "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {                System.Windows.MessageBox.Show($"Error saving logs: {ex.Message}", 
                              "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogLevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshLogEntries();
        }

        private void ApplicationLogger_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ApplicationLogger.LogEntries))
            {
                Dispatcher.BeginInvoke(RefreshLogEntries);
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateUI();
            UpdateLogRate();
        }        private void RefreshLogEntries()
        {
            _filteredLogEntries.Clear();
            
            var selectedFilter = LogLevelFilter.SelectedItem as ComboBoxItem;
            var filterText = selectedFilter?.Content?.ToString() ?? "All";
            
            var allEntries = _applicationLogger.LogEntries.ToList();
            var filteredEntries = filterText == "All" 
                ? allEntries 
                : allEntries.Where(entry => entry.Level.ToString().Equals(filterText, StringComparison.OrdinalIgnoreCase));
            
            foreach (var entry in filteredEntries.OrderBy(e => e.Timestamp))
            {
                _filteredLogEntries.Add(entry);
            }
            
            // Auto-scroll to bottom if enabled
            if (AutoScrollCheckBox.IsChecked == true)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (LogListView.Items.Count > 0)
                    {
                        LogListView.ScrollIntoView(LogListView.Items[LogListView.Items.Count - 1]);
                    }
                }, DispatcherPriority.Background);
            }
            
            UpdateUI();
        }        private void UpdateUI()
        {
            // Update status
            LoggingStatusText.Text = _isLoggingActive ? "Active" : "Inactive";
            LoggingStatusText.Foreground = _isLoggingActive 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            
            // Update counts
            var allLogs = _applicationLogger.LogEntries.ToList();
            TotalLogsText.Text = allLogs.Count.ToString();
            FilteredLogsText.Text = _filteredLogEntries.Count.ToString();
            LogCountText.Text = $"{_filteredLogEntries.Count} entries";
            
            // Update level distribution
            DebugCountText.Text = allLogs.Count(e => e.Level == LogLevel.Debug).ToString();
            InfoCountText.Text = allLogs.Count(e => e.Level == LogLevel.Info).ToString();
            WarningCountText.Text = allLogs.Count(e => e.Level == LogLevel.Warning).ToString();
            ErrorCountText.Text = allLogs.Count(e => e.Level == LogLevel.Error).ToString();
            
            // Update button states
            StartLoggingButton.IsEnabled = !_isLoggingActive;
            StopLoggingButton.IsEnabled = _isLoggingActive;
        }

        private void UpdateLogRate()
        {
            var currentTime = DateTime.Now;
            var currentLogCount = _applicationLogger.LogEntries.Count;
            
            var timeDiff = (currentTime - _lastUpdateTime).TotalMinutes;
            var logDiff = currentLogCount - _lastLogCount;
            
            if (timeDiff > 0)
            {
                var rate = Math.Round(logDiff / timeDiff, 1);
                LogRateText.Text = $"{rate}/min";
                
                _lastUpdateTime = currentTime;
                _lastLogCount = currentLogCount;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Unsubscribe from events
            _applicationLogger.PropertyChanged -= ApplicationLogger_PropertyChanged;
            _updateTimer?.Stop();
            
            base.OnClosing(e);
        }        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
