using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsSipPhone.Commands;
using WindowsSipPhone.Utils;
using WindowsSipPhone.Services;
using WindowsSipPhone.Models;
using System.Globalization;
using System.Windows.Data;

namespace WindowsSipPhone.Pages
{
    /// <summary>
    /// Interaction logic for DiagnosticsPage.xaml
    /// </summary>
    public partial class DiagnosticsPage : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {        private readonly DispatcherTimer _refreshTimer;
        private bool _isTracingEnabled;
        private string _selectedFilter = "All";
        private string _connectionStatus = "Disconnected";
        private System.Windows.Media.Brush _statusBrush = System.Windows.Media.Brushes.Red;
        private DiagnosticReportGenerator _reportGenerator;
          // Logging-related fields
        private ApplicationLogger _applicationLogger;
        private ObservableCollection<LogEntry> _filteredLogEntries;
        private LogEntry? _selectedLogEntry;
        private string _logFilter = "All";

        // Properties for XAML binding
        public ApplicationLogger ApplicationLogger => _applicationLogger;
        public ObservableCollection<LogEntry> FilteredLogEntries => _filteredLogEntries;
        public LogEntry? SelectedLogEntry 
        { 
            get => _selectedLogEntry; 
            set 
            { 
                _selectedLogEntry = value; 
                OnPropertyChanged(); 
            } 
        }

        public string LoggingButtonText => _applicationLogger?.IsLoggingEnabled == true ? "🔴 Disable Logging" : "🟢 Enable Logging";
        public System.Windows.Media.Brush LoggingButtonColor => _applicationLogger?.IsLoggingEnabled == true ? 
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)) : 
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96));
        public string LoggingStatusText => _applicationLogger?.IsLoggingEnabled == true ? "ACTIVE" : "INACTIVE";
        public System.Windows.Media.Brush LoggingStatusColor => _applicationLogger?.IsLoggingEnabled == true ? 
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)) : 
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(149, 165, 166));
        public System.Windows.Media.Brush LoggingStatusBackground => _applicationLogger?.IsLoggingEnabled == true ? 
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(213, 245, 227)) : 
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 240, 241));

        public string FilteredEntriesText => $"Showing {_filteredLogEntries.Count} of {_applicationLogger?.LogEntries.Count ?? 0} entries";
        public string LogDirectory => _applicationLogger?.GetLogDirectory() ?? "Unknown";public DiagnosticsPage()
        {
            InitializeComponent();
            DataContext = this;
            
            SipMessages = new ObservableCollection<SipMessageEntry>();
            NetworkStatistics = new NetworkStatisticsModel();
            _reportGenerator = new DiagnosticReportGenerator();
            
            // Initialize logging
            _applicationLogger = ApplicationLogger.Instance;
            _filteredLogEntries = new ObservableCollection<LogEntry>();
            
            // Subscribe to logging events
            _applicationLogger.PropertyChanged += OnApplicationLoggerPropertyChanged;
            _applicationLogger.LogEntries.CollectionChanged += OnLogEntriesCollectionChanged;
            
            // Start refresh timer for real-time updates
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
            
            // Initialize with some sample data
            InitializeSampleData();
              // Initialize commands
            StartTracingCommand = new RelayCommand(StartTracing);
            StopTracingCommand = new RelayCommand(StopTracing);
            ClearMessagesCommand = new RelayCommand(ClearMessages);
            ExportLogsCommand = new RelayCommand(ExportLogs);
            RefreshNetworkCommand = new RelayCommand(async () => await Task.Run(RefreshNetworkStats));
            PingServerCommand = new RelayCommand(async () => await Task.Run(PingServer));
            FilterMessagesCommand = new RelayCommand<string>(filter => FilterMessages(filter ?? "All"));
            
            // Subscribe to diagnostic events
            SetupDiagnosticEvents();
            
            Console.WriteLine("[DIAGNOSTICS PAGE] Initialized with enhanced diagnostic controls");
        }

        private void SetupDiagnosticEvents()
        {
            // Subscribe to network diagnostics events
            NetworkDiagnostics.DiagnosticCompleted += OnNetworkDiagnosticCompleted;
            
            // Subscribe to SIP health events
            SipServerHealth.HealthCheckCompleted += OnSipHealthCheckCompleted;
            
            // Subscribe to audio health events
            AudioDeviceHealth.HealthCheckCompleted += OnAudioHealthCheckCompleted;
            
            // Subscribe to error message events
            ErrorMessage.ErrorDismissed += OnErrorDismissed;
            ErrorMessage.RetryRequested += OnErrorRetryRequested;
            ErrorMessage.DetailsRequested += OnErrorDetailsRequested;
        }

        private void OnNetworkDiagnosticCompleted(object? sender, Controls.NetworkDiagnosticsControl.DiagnosticResult e)
        {
            Console.WriteLine($"[DIAGNOSTICS PAGE] Network diagnostic completed: {e.TestName} - {(e.Success ? "Success" : "Failed")}");
            
            if (!e.Success)
            {                ErrorMessage.ShowNetworkError($"network test '{e.TestName}'", () => 
                {
                    // Retry network diagnostics
                    Task.Run(async () => await Dispatcher.InvokeAsync(() => NetworkDiagnostics.RunDiagnosticsButton_Click(null, null)));
                });
            }
        }

        private void OnSipHealthCheckCompleted(object? sender, Controls.SipServerHealthControl.SipHealthResult e)
        {
            Console.WriteLine($"[DIAGNOSTICS PAGE] SIP health check completed: Connected={e.IsConnected}, Registered={e.IsRegistered}");
            
            if (!e.IsConnected)
            {
                ErrorMessage.ShowSipServerError("Unknown Server", () =>
                {
                    // Retry SIP connection
                    Task.Run(async () => await Dispatcher.InvokeAsync(() => SipServerHealth.TestConnectionButton_Click(null, null)));
                });
            }
        }

        private void OnAudioHealthCheckCompleted(object? sender, Controls.AudioDeviceHealthControl.AudioHealthResult e)
        {
            Console.WriteLine($"[DIAGNOSTICS PAGE] Audio health check completed: Input={e.InputDeviceAvailable}, Output={e.OutputDeviceAvailable}");
            
            if (!e.InputDeviceAvailable)
            {
                ErrorMessage.ShowAudioDeviceError("input", () =>
                {
                    // Retry audio device detection
                    AudioDeviceHealth.InitializeComponent();
                });
            }
            else if (!e.OutputDeviceAvailable)
            {
                ErrorMessage.ShowAudioDeviceError("output", () =>
                {
                    // Retry audio device detection
                    AudioDeviceHealth.InitializeComponent();
                });
            }
        }

        private void OnErrorDismissed(object? sender, EventArgs e)
        {
            Console.WriteLine("[DIAGNOSTICS PAGE] Error message dismissed by user");
        }

        private void OnErrorRetryRequested(object? sender, EventArgs e)
        {
            Console.WriteLine("[DIAGNOSTICS PAGE] Error retry requested by user");
        }

        private void OnErrorDetailsRequested(object? sender, string details)
        {
            Console.WriteLine($"[DIAGNOSTICS PAGE] Error details requested: {details}");
        }

        private async void GenerateReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GenerateReportButton.Content = "🔄 Generating...";
                GenerateReportButton.IsEnabled = false;
                
                Console.WriteLine("[DIAGNOSTICS PAGE] Generating comprehensive diagnostic report...");
                
                var report = await _reportGenerator.GenerateReportAsync();
                var filePath = await _reportGenerator.SaveReportToFileAsync(report);
                
                ErrorMessage.ShowInfo("Report Generated", $"Diagnostic report saved to desktop: {Path.GetFileName(filePath)}", 5);
                
                Console.WriteLine($"[DIAGNOSTICS PAGE] Report generated and saved to: {filePath}");
            }
            catch (Exception ex)
            {
                ErrorMessage.ShowError("Report Generation Failed", $"Failed to generate diagnostic report: {ex.Message}");
                Console.WriteLine($"[DIAGNOSTICS PAGE] Report generation error: {ex.Message}");
            }
            finally
            {
                GenerateReportButton.Content = "📋 Generate Full Report";
                GenerateReportButton.IsEnabled = true;
            }
        }

        private void OpenReportFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                Process.Start("explorer.exe", desktopPath);
                Console.WriteLine($"[DIAGNOSTICS PAGE] Opened reports folder: {desktopPath}");
            }
            catch (Exception ex)
            {
                ErrorMessage.ShowError("Cannot Open Folder", $"Failed to open reports folder: {ex.Message}");
                Console.WriteLine($"[DIAGNOSTICS PAGE] Error opening reports folder: {ex.Message}");
            }
        }

        #region Properties

        public ObservableCollection<SipMessageEntry> SipMessages { get; set; }
        public NetworkStatisticsModel NetworkStatistics { get; set; }

        public bool IsTracingEnabled
        {
            get => _isTracingEnabled;
            set
            {
                _isTracingEnabled = value;
                OnPropertyChanged();
            }
        }

        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                _selectedFilter = value;
                OnPropertyChanged();
                FilterMessages(value);
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;            set
            {
                _connectionStatus = value;
                OnPropertyChanged();
                StatusBrush = value.Contains("Connected") ? System.Windows.Media.Brushes.Green : 
                             value.Contains("Connecting") ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.Red;
            }
        }        public System.Windows.Media.Brush StatusBrush
        {
            get => _statusBrush;
            set
            {
                _statusBrush = value;
                OnPropertyChanged();
            }        }

        #endregion

        #region Commands

        public ICommand StartTracingCommand { get; }
        public ICommand StopTracingCommand { get; }
        public ICommand ClearMessagesCommand { get; }
        public ICommand ExportLogsCommand { get; }
        public ICommand RefreshNetworkCommand { get; }
        public ICommand PingServerCommand { get; }
        public ICommand FilterMessagesCommand { get; }

        #endregion

        #region Command Implementations

        private void StartTracing()
        {
            IsTracingEnabled = true;
            AddSipMessage("INFO", "SIP Tracing Started", "System", DateTime.Now);
            
            // TODO: Connect to actual SIP service for real tracing
            // For now, simulate with timer
            StartSimulatedTracing();
        }

        private void StopTracing()
        {
            IsTracingEnabled = false;
            AddSipMessage("INFO", "SIP Tracing Stopped", "System", DateTime.Now);
        }

        private void ClearMessages()
        {
            SipMessages.Clear();
            AddSipMessage("INFO", "Message log cleared", "System", DateTime.Now);
        }

        private void ExportLogs()
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"SipTrace_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var content = string.Join("\n", SipMessages.Select(m => 
                        $"[{m.Timestamp:yyyy-MM-dd HH:mm:ss}] {m.Type} - {m.Direction}: {m.Message}"));
                      System.IO.File.WriteAllText(saveDialog.FileName, content);
                    System.Windows.MessageBox.Show($"Logs exported to {saveDialog.FileName}", "Export Complete", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to export logs: {ex.Message}", "Export Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async void RefreshNetworkStats()
        {
            await UpdateNetworkStatistics();
        }

        private async void PingServer()
        {
            try
            {
                ConnectionStatus = "Pinging server...";
                
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync("192.168.1.180", 5000);
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        ConnectionStatus = $"Ping successful: {reply.RoundtripTime}ms";
                        NetworkStatistics.PingLatency = $"{reply.RoundtripTime}ms";
                        AddSipMessage("INFO", $"Ping to server successful: {reply.RoundtripTime}ms", "Network", DateTime.Now);
                    }
                    else
                    {
                        ConnectionStatus = $"Ping failed: {reply.Status}";
                        AddSipMessage("ERROR", $"Ping to server failed: {reply.Status}", "Network", DateTime.Now);
                    }
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Ping error";
                AddSipMessage("ERROR", $"Ping error: {ex.Message}", "Network", DateTime.Now);
            }
        }

        private void FilterMessages(string filter)
        {
            // TODO: Implement actual filtering logic
            // For now, just update the selected filter
            SelectedFilter = filter;
              // In a real implementation, you would filter the ObservableCollection
            // or use a CollectionView with filtering
        }

        #endregion

        #region Helper Methods

        public void AddSipMessage(string type, string message, string direction, DateTime timestamp)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                SipMessages.Insert(0, new SipMessageEntry
                {
                    Type = type,
                    Message = message,
                    Direction = direction,
                    Timestamp = timestamp,
                    TypeBrush = GetBrushForType(type)
                });

                // Keep only last 1000 messages to prevent memory issues
                while (SipMessages.Count > 1000)
                {
                    SipMessages.RemoveAt(SipMessages.Count - 1);
                }
            });
        }

        private System.Windows.Media.Brush GetBrushForType(string type)
        {            return type switch
            {
                "REGISTER" => System.Windows.Media.Brushes.Blue,
                "INVITE" => System.Windows.Media.Brushes.Green,
                "BYE" => System.Windows.Media.Brushes.Orange,
                "ERROR" => System.Windows.Media.Brushes.Red,
                "INFO" => System.Windows.Media.Brushes.Gray,
                _ => System.Windows.Media.Brushes.Black
            };
        }

        private void InitializeSampleData()
        {
            // Add some sample SIP messages for demonstration
            AddSipMessage("REGISTER", "REGISTER sip:192.168.1.180 SIP/2.0", "Outbound", DateTime.Now.AddMinutes(-5));
            AddSipMessage("INFO", "200 OK", "Inbound", DateTime.Now.AddMinutes(-4));
            AddSipMessage("INVITE", "INVITE sip:104@192.168.1.180 SIP/2.0", "Outbound", DateTime.Now.AddMinutes(-3));
            AddSipMessage("INFO", "180 Ringing", "Inbound", DateTime.Now.AddMinutes(-2));
            AddSipMessage("INFO", "200 OK", "Inbound", DateTime.Now.AddMinutes(-1));
            
            // Initialize network statistics
            _ = UpdateNetworkStatistics();
        }

        private void StartSimulatedTracing()
        {
            if (!IsTracingEnabled) return;

            Task.Run(async () =>
            {
                var random = new Random();
                var messageTypes = new[] { "REGISTER", "INVITE", "BYE", "OPTIONS", "INFO" };
                var directions = new[] { "Inbound", "Outbound" };

                while (IsTracingEnabled)
                {
                    await Task.Delay(random.Next(2000, 8000)); // Random delay between 2-8 seconds
                    
                    if (!IsTracingEnabled) break;

                    var type = messageTypes[random.Next(messageTypes.Length)];
                    var direction = directions[random.Next(directions.Length)];
                    var message = GenerateRandomSipMessage(type, direction);
                    
                    AddSipMessage(type, message, direction, DateTime.Now);
                }
            });
        }

        private string GenerateRandomSipMessage(string type, string direction)
        {
            var random = new Random();
            var callIds = new[] { "12345@192.168.1.100", "67890@192.168.1.101", "54321@192.168.1.102" };
            var users = new[] { "103", "104", "105", "106" };
            
            return type switch
            {
                "REGISTER" => $"REGISTER sip:192.168.1.180 SIP/2.0\r\nCall-ID: {callIds[random.Next(callIds.Length)]}\r\nFrom: {users[random.Next(users.Length)]}",
                "INVITE" => $"INVITE sip:{users[random.Next(users.Length)]}@192.168.1.180 SIP/2.0\r\nCall-ID: {callIds[random.Next(callIds.Length)]}",
                "BYE" => $"BYE sip:{users[random.Next(users.Length)]}@192.168.1.180 SIP/2.0\r\nCall-ID: {callIds[random.Next(callIds.Length)]}",
                "OPTIONS" => $"OPTIONS sip:192.168.1.180 SIP/2.0\r\nCall-ID: {callIds[random.Next(callIds.Length)]}",
                _ => $"{type} - {direction} message at {DateTime.Now:HH:mm:ss}"
            };
        }        private Task UpdateNetworkStatistics()
        {
            try
            {
                // Get network interface statistics
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();

                if (networkInterfaces.Any())
                {
                    var primaryInterface = networkInterfaces.First();
                    var stats = primaryInterface.GetIPv4Statistics();
                    
                    NetworkStatistics.BytesSent = FormatBytes(stats.BytesSent);
                    NetworkStatistics.BytesReceived = FormatBytes(stats.BytesReceived);
                    NetworkStatistics.PacketsSent = stats.UnicastPacketsSent.ToString();
                    NetworkStatistics.PacketsReceived = stats.UnicastPacketsReceived.ToString();
                }

                // Update other statistics
                NetworkStatistics.ActiveConnections = SipMessages.Count(m => m.Type == "INVITE").ToString();
                NetworkStatistics.TotalMessages = SipMessages.Count.ToString();
                NetworkStatistics.LastUpdate = DateTime.Now.ToString("HH:mm:ss");
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                AddSipMessage("ERROR", $"Failed to update network statistics: {ex.Message}", "System", DateTime.Now);
                return Task.CompletedTask;
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            // Update network statistics periodically
            if (DateTime.Now.Second % 10 == 0) // Every 10 seconds
            {
                _ = UpdateNetworkStatistics();
            }
        }

        #endregion

        #region Logging Event Handlers

        private void OnApplicationLoggerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Update UI properties when logger properties change
            OnPropertyChanged(nameof(LoggingButtonText));
            OnPropertyChanged(nameof(LoggingButtonColor));
            OnPropertyChanged(nameof(LoggingStatusText));
            OnPropertyChanged(nameof(LoggingStatusColor));
            OnPropertyChanged(nameof(LoggingStatusBackground));
        }

        private void OnLogEntriesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Update filtered entries when log entries change
            FilterLogEntries();
        }

        #endregion

        #region Logging Button Handlers

        private void ToggleLoggingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _applicationLogger.IsLoggingEnabled = !_applicationLogger.IsLoggingEnabled;
                
                // Log this button click itself (if logging is now enabled)
                if (_applicationLogger.IsLoggingEnabled)
                {
                    _applicationLogger.LogButtonClick("Toggle Logging", "DiagnosticsPage", 
                        $"Logging enabled by user");
                }
                
                Console.WriteLine($"[DIAGNOSTICS PAGE] Logging {(_applicationLogger.IsLoggingEnabled ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to toggle logging: {ex.Message}", "Error", 
                               System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async void WriteLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _applicationLogger.LogButtonClick("Write Log", "DiagnosticsPage", 
                    $"User requested log export with {_applicationLogger.TotalEntries} entries");

                var filePath = await _applicationLogger.WriteLogToFile();
                
                System.Windows.MessageBox.Show($"Log file written successfully!\n\nFile: {Path.GetFileName(filePath)}\nLocation: {Path.GetDirectoryName(filePath)}", 
                               "Log Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                Console.WriteLine($"[DIAGNOSTICS PAGE] Log exported to: {filePath}");
            }
            catch (Exception ex)
            {
                _applicationLogger.LogError("DiagnosticsPage", "Failed to write log file", ex);
                System.Windows.MessageBox.Show($"Failed to write log file: {ex.Message}", "Export Error", 
                               System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show("Are you sure you want to clear all log entries?\n\nThis action cannot be undone.", 
                                           "Clear Log Entries", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    var entryCount = _applicationLogger.TotalEntries;
                    _applicationLogger.LogAction("DiagnosticsPage", $"User cleared {entryCount} log entries");
                    _applicationLogger.ClearLogs();
                    
                    Console.WriteLine($"[DIAGNOSTICS PAGE] Log entries cleared by user");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to clear log entries: {ex.Message}", "Error", 
                               System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is string filter)
                {
                    _logFilter = filter;
                    FilterLogEntries();
                    
                    _applicationLogger.LogButtonClick($"Filter: {filter}", "DiagnosticsPage", 
                        $"Applied log filter: {filter}");
                    
                    Console.WriteLine($"[DIAGNOSTICS PAGE] Log filter changed to: {filter}");
                }
            }
            catch (Exception ex)
            {
                _applicationLogger.LogError("DiagnosticsPage", "Failed to apply log filter", ex);
            }
        }

        private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logDirectory = _applicationLogger.GetLogDirectory();
                Process.Start("explorer.exe", logDirectory);
                
                _applicationLogger.LogButtonClick("Open Log Folder", "DiagnosticsPage", 
                    $"Opened log directory: {logDirectory}");
                
                Console.WriteLine($"[DIAGNOSTICS PAGE] Opened log directory: {logDirectory}");
            }
            catch (Exception ex)
            {
                _applicationLogger.LogError("DiagnosticsPage", "Failed to open log directory", ex);
                System.Windows.MessageBox.Show($"Failed to open log directory: {ex.Message}", "Error", 
                               System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion

        #region Log Filtering

        private void FilterLogEntries()
        {
            try
            {
                _filteredLogEntries.Clear();
                
                var entries = _logFilter == "All" ? 
                    _applicationLogger.LogEntries : 
                    _applicationLogger.LogEntries.Where(e => e.Type.ToString() == _logFilter);
                
                foreach (var entry in entries)
                {
                    _filteredLogEntries.Add(entry);
                }
                
                OnPropertyChanged(nameof(FilteredEntriesText));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DIAGNOSTICS PAGE] Error filtering log entries: {ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region Data Models

    public class SipMessageEntry
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public System.Windows.Media.Brush TypeBrush { get; set; } = System.Windows.Media.Brushes.Black;
    }

    public class NetworkStatisticsModel : INotifyPropertyChanged
    {
        private string _pingLatency = "N/A";
        private string _bytesSent = "0 B";
        private string _bytesReceived = "0 B";
        private string _packetsSent = "0";
        private string _packetsReceived = "0";
        private string _activeConnections = "0";
        private string _totalMessages = "0";
        private string _lastUpdate = "Never";

        public string PingLatency
        {
            get => _pingLatency;
            set { _pingLatency = value; OnPropertyChanged(); }
        }

        public string BytesSent
        {
            get => _bytesSent;
            set { _bytesSent = value; OnPropertyChanged(); }
        }

        public string BytesReceived
        {
            get => _bytesReceived;
            set { _bytesReceived = value; OnPropertyChanged(); }
        }

        public string PacketsSent
        {
            get => _packetsSent;
            set { _packetsSent = value; OnPropertyChanged(); }
        }

        public string PacketsReceived
        {
            get => _packetsReceived;
            set { _packetsReceived = value; OnPropertyChanged(); }
        }

        public string ActiveConnections
        {
            get => _activeConnections;
            set { _activeConnections = value; OnPropertyChanged(); }
        }

        public string TotalMessages
        {
            get => _totalMessages;
            set { _totalMessages = value; OnPropertyChanged(); }
        }        public string LastUpdate
        {
            get => _lastUpdate;
            set { _lastUpdate = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}