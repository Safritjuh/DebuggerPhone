using System;
using System.Windows.Media;

namespace WindowsSipPhone.Models
{
    /// <summary>
    /// Represents a single log entry in the application logging system
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogEntryType Type { get; set; }
        public LogLevel Level { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Get formatted timestamp for display
        /// </summary>
        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

        /// <summary>
        /// Get formatted date and time for detailed view
        /// </summary>
        public string FullTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");

        /// <summary>
        /// Get icon based on log entry type
        /// </summary>
        public string TypeIcon => Type switch
        {
            LogEntryType.Error => "❌",
            LogEntryType.Warning => "⚠️",
            LogEntryType.ButtonClick => "🖱️",
            LogEntryType.Action => "⚡",
            LogEntryType.SystemInfo => "ℹ️",
            LogEntryType.SipMessage => "📞",
            _ => "📝"
        };        /// <summary>
        /// Get color based on log level
        /// </summary>
        public System.Windows.Media.Brush LevelColor => Level switch
        {
            LogLevel.Error => System.Windows.Media.Brushes.Red,
            LogLevel.Warning => System.Windows.Media.Brushes.Orange,
            LogLevel.Info => System.Windows.Media.Brushes.Blue,
            LogLevel.Debug => System.Windows.Media.Brushes.Gray,
            _ => System.Windows.Media.Brushes.Black
        };        /// <summary>
        /// Get background color based on log entry type
        /// </summary>
        public System.Windows.Media.Brush BackgroundColor => Type switch
        {
            LogEntryType.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 235, 235)), // Light red
            LogEntryType.Warning => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 248, 220)), // Light yellow
            LogEntryType.SystemInfo => new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 244, 255)), // Light blue
            _ => System.Windows.Media.Brushes.White
        };

        /// <summary>
        /// Get display text for UI
        /// </summary>
        public string DisplayText => $"[{Source}] {Message}";

        /// <summary>
        /// Get short summary for list view
        /// </summary>
        public string Summary
        {
            get
            {
                var summary = Message.Length > 80 ? Message.Substring(0, 77) + "..." : Message;
                return $"{TypeIcon} {summary}";
            }
        }

        /// <summary>
        /// Convert to string for file export
        /// </summary>
        public override string ToString()
        {
            return $"{FullTimestamp} [{Level}] [{Type}] [{Source}] {Message}";
        }

        /// <summary>
        /// Convert to detailed string for file export with details
        /// </summary>
        public string ToDetailedString()
        {
            var result = ToString();
            if (!string.IsNullOrEmpty(Details))
            {
                result += Environment.NewLine + "Details: " + Details;
            }
            return result;
        }
    }

    /// <summary>
    /// Types of log entries
    /// </summary>
    public enum LogEntryType
    {
        Error,
        Warning,
        Action,
        ButtonClick,
        SystemInfo,
        SipMessage,
        NetworkEvent,
        AudioEvent
    }

    /// <summary>
    /// Log severity levels
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}
