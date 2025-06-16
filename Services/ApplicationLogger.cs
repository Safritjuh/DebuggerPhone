using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using WindowsSipPhone.Models;

namespace WindowsSipPhone.Services
{
    /// <summary>
    /// Comprehensive application logging service that tracks:
    /// - All errors and exceptions
    /// - Button clicks and UI interactions
    /// - Application actions and state changes
    /// - System diagnostics (audio devices, SIP registration)
    /// </summary>
    public class ApplicationLogger : INotifyPropertyChanged
    {
        private static ApplicationLogger? _instance;
        private static readonly object _lock = new object();
        
        private bool _isLoggingEnabled;
        private readonly ObservableCollection<LogEntry> _logEntries;
        private readonly StringBuilder _logBuffer;
        private readonly string _logDirectory;
        private readonly Dispatcher _dispatcher;

        public static ApplicationLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ApplicationLogger();
                    }
                }
                return _instance;
            }
        }

        private ApplicationLogger()
        {
            _logEntries = new ObservableCollection<LogEntry>();
            _logBuffer = new StringBuilder();
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            
            // Create logs directory
            _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                       "WindowsSipPhone", "Logs");
            Directory.CreateDirectory(_logDirectory);
            
            // Start with logging disabled by default
            _isLoggingEnabled = false;
            
            // Initialize system state logging
            InitializeSystemState();
        }

        #region Properties

        public bool IsLoggingEnabled
        {
            get => _isLoggingEnabled;
            set
            {
                if (_isLoggingEnabled != value)
                {
                    _isLoggingEnabled = value;
                    OnPropertyChanged(nameof(IsLoggingEnabled));
                    
                    if (value)
                    {
                        LogAction("SYSTEM", "Logging enabled");
                        RefreshSystemState();
                    }
                    else
                    {
                        LogAction("SYSTEM", "Logging disabled");
                    }
                }
            }
        }

        public ObservableCollection<LogEntry> LogEntries => _logEntries;

        public int TotalEntries => _logEntries.Count;
        public int ErrorCount => GetEntryCountByType(LogEntryType.Error);
        public int ButtonClickCount => GetEntryCountByType(LogEntryType.ButtonClick);
        public int ActionCount => GetEntryCountByType(LogEntryType.Action);

        #endregion

        #region Public Logging Methods

        /// <summary>
        /// Log an error or exception
        /// </summary>
        public void LogError(string source, string message, Exception? exception = null)
        {
            if (!_isLoggingEnabled) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogEntryType.Error,
                Source = source,
                Message = message,
                Details = exception?.ToString() ?? string.Empty,
                Level = LogLevel.Error
            };

            AddLogEntry(logEntry);
        }

        /// <summary>
        /// Log a button click or UI interaction
        /// </summary>
        public void LogButtonClick(string buttonName, string location, string additionalInfo = "")
        {
            if (!_isLoggingEnabled) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogEntryType.ButtonClick,
                Source = location,
                Message = $"Button clicked: {buttonName}",
                Details = additionalInfo,
                Level = LogLevel.Info
            };

            AddLogEntry(logEntry);
        }

        /// <summary>
        /// Log an application action or state change
        /// </summary>
        public void LogAction(string source, string action, string details = "")
        {
            if (!_isLoggingEnabled) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogEntryType.Action,
                Source = source,
                Message = action,
                Details = details,
                Level = LogLevel.Info
            };

            AddLogEntry(logEntry);
        }

        /// <summary>
        /// Log system information (audio devices, SIP state, etc.)
        /// </summary>
        public void LogSystemInfo(string category, string information, string details = "")
        {
            if (!_isLoggingEnabled) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogEntryType.SystemInfo,
                Source = category,
                Message = information,
                Details = details,
                Level = LogLevel.Info
            };

            AddLogEntry(logEntry);
        }

        /// <summary>
        /// Log warning messages
        /// </summary>
        public void LogWarning(string source, string message, string details = "")
        {
            if (!_isLoggingEnabled) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = LogEntryType.Warning,
                Source = source,
                Message = message,
                Details = details,
                Level = LogLevel.Warning
            };

            AddLogEntry(logEntry);
        }

        #endregion

        #region System State Tracking

        private void InitializeSystemState()
        {
            if (!_isLoggingEnabled) return;

            Task.Run(async () =>
            {
                await CollectSystemInformation();
            });
        }

        public void RefreshSystemState()
        {
            if (!_isLoggingEnabled) return;

            LogAction("SYSTEM", "Refreshing system state");
            Task.Run(async () =>
            {
                await CollectSystemInformation();
            });
        }        private Task CollectSystemInformation()
        {
            try
            {
                // Audio device information
                CollectAudioDeviceInfo();
                
                // SIP registration state
                CollectSipRegistrationState();
                
                // Network information
                CollectNetworkInfo();
                
                // Application state
                CollectApplicationState();
            }
            catch (Exception ex)
            {
                LogError("SYSTEM", "Failed to collect system information", ex);
            }
            
            return Task.CompletedTask;
        }

        private void CollectAudioDeviceInfo()
        {
            try
            {
                LogSystemInfo("AUDIO", "=== AUDIO DEVICE STATUS ===");
                
                // Input devices
                LogSystemInfo("AUDIO", $"Input devices available: {WaveIn.DeviceCount}");
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    LogSystemInfo("AUDIO", $"Input Device {i}: {capabilities.ProductName}", 
                        $"Channels: {capabilities.Channels}, SampleRate: Various");
                }

                // Output devices
                LogSystemInfo("AUDIO", $"Output devices available: {WaveOut.DeviceCount}");
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    LogSystemInfo("AUDIO", $"Output Device {i}: {capabilities.ProductName}", 
                        $"Channels: {capabilities.Channels}, Support: Various");
                }
            }
            catch (Exception ex)
            {
                LogError("AUDIO", "Failed to collect audio device information", ex);
            }
        }

        private void CollectSipRegistrationState()
        {
            try
            {
                LogSystemInfo("SIP", "=== SIP REGISTRATION STATUS ===");
                
                // This would be connected to your actual SIP service
                // For now, we'll add placeholder logic
                var sipService = System.Windows.Application.Current?.MainWindow?.DataContext as dynamic;
                
                if (sipService != null)
                {
                    LogSystemInfo("SIP", "SIP Service: Active");
                    // Add more SIP state information as available
                }
                else
                {
                    LogSystemInfo("SIP", "SIP Service: Not initialized");
                }
            }
            catch (Exception ex)
            {
                LogError("SIP", "Failed to collect SIP registration state", ex);
            }
        }

        private void CollectNetworkInfo()
        {
            try
            {
                LogSystemInfo("NETWORK", "=== NETWORK STATUS ===");
                LogSystemInfo("NETWORK", $"Machine Name: {Environment.MachineName}");
                LogSystemInfo("NETWORK", $"User Domain: {Environment.UserDomainName}");
                LogSystemInfo("NETWORK", $"OS Version: {Environment.OSVersion}");
            }
            catch (Exception ex)
            {
                LogError("NETWORK", "Failed to collect network information", ex);
            }
        }

        private void CollectApplicationState()
        {
            try
            {
                LogSystemInfo("APP", "=== APPLICATION STATE ===");
                LogSystemInfo("APP", $"Application started: {Process.GetCurrentProcess().StartTime}");
                LogSystemInfo("APP", $"Working memory: {Environment.WorkingSet / (1024 * 1024)} MB");
                LogSystemInfo("APP", $"Managed memory: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
                LogSystemInfo("APP", $"Thread count: {Process.GetCurrentProcess().Threads.Count}");
            }
            catch (Exception ex)
            {
                LogError("APP", "Failed to collect application state", ex);
            }
        }

        #endregion

        #region Log Management

        private void AddLogEntry(LogEntry entry)
        {
            _dispatcher.BeginInvoke(() =>
            {
                _logEntries.Insert(0, entry); // Add to top for newest first
                
                // Keep only last 1000 entries to prevent memory issues
                while (_logEntries.Count > 1000)
                {
                    _logEntries.RemoveAt(_logEntries.Count - 1);
                }

                // Add to buffer for file export
                _logBuffer.AppendLine(entry.ToString());

                OnPropertyChanged(nameof(TotalEntries));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(ButtonClickCount));
                OnPropertyChanged(nameof(ActionCount));
            });
        }

        private int GetEntryCountByType(LogEntryType type)
        {
            int count = 0;
            foreach (var entry in _logEntries)
            {
                if (entry.Type == type) count++;
            }
            return count;
        }

        /// <summary>
        /// Clear all log entries
        /// </summary>
        public void ClearLogs()
        {
            _dispatcher.BeginInvoke(() =>
            {
                _logEntries.Clear();
                _logBuffer.Clear();
                OnPropertyChanged(nameof(TotalEntries));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(ButtonClickCount));
                OnPropertyChanged(nameof(ActionCount));
            });

            LogAction("SYSTEM", "Log entries cleared");
        }

        #endregion

        #region File Export

        /// <summary>
        /// Write current log to file
        /// </summary>
        public async Task<string> WriteLogToFile()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"SipPhone_Log_{timestamp}.txt";
                var filePath = Path.Combine(_logDirectory, fileName);

                var logContent = new StringBuilder();
                
                // Header with system state
                logContent.AppendLine("=".PadRight(80, '='));
                logContent.AppendLine($"Windows SIP Phone - Application Log");
                logContent.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logContent.AppendLine($"Total Entries: {TotalEntries}");
                logContent.AppendLine($"Errors: {ErrorCount}, Actions: {ActionCount}, Button Clicks: {ButtonClickCount}");
                logContent.AppendLine("=".PadRight(80, '='));
                logContent.AppendLine();

                // Add all log entries (newest first)
                foreach (var entry in _logEntries)
                {
                    logContent.AppendLine(entry.ToDetailedString());
                    logContent.AppendLine(); // Empty line between entries
                }

                await File.WriteAllTextAsync(filePath, logContent.ToString());
                
                LogAction("EXPORT", $"Log exported to file: {fileName}");
                return filePath;
            }
            catch (Exception ex)
            {
                LogError("EXPORT", "Failed to write log to file", ex);
                throw;
            }
        }

        /// <summary>
        /// Get the logs directory path
        /// </summary>
        public string GetLogDirectory() => _logDirectory;

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
