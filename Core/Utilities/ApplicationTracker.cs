using System;
using System.Windows;
using System.Windows.Controls;
using WindowsSipPhone.Services;

namespace WindowsSipPhone.Utils
{
    /// <summary>
    /// Helper class for tracking UI interactions and application events
    /// </summary>
    public static class ApplicationTracker
    {
        private static ApplicationLogger Logger => ApplicationLogger.Instance;

        /// <summary>
        /// Track a button click
        /// </summary>
        public static void TrackButtonClick(string buttonName, string location, string additionalInfo = "")
        {
            Logger.LogButtonClick(buttonName, location, additionalInfo);
        }

        /// <summary>
        /// Track an application action
        /// </summary>
        public static void TrackAction(string source, string action, string details = "")
        {
            Logger.LogAction(source, action, details);
        }

        /// <summary>
        /// Track an error
        /// </summary>
        public static void TrackError(string source, string message, Exception? exception = null)
        {
            Logger.LogError(source, message, exception);
        }

        /// <summary>
        /// Track a warning
        /// </summary>
        public static void TrackWarning(string source, string message, string details = "")
        {
            Logger.LogWarning(source, message, details);
        }

        /// <summary>
        /// Track system information
        /// </summary>
        public static void TrackSystemInfo(string category, string information, string details = "")
        {
            Logger.LogSystemInfo(category, information, details);
        }        /// <summary>
        /// Extension method to automatically track button clicks
        /// </summary>
        public static void TrackClick(this System.Windows.Controls.Button button, string location, string additionalInfo = "")
        {
            var buttonName = button.Content?.ToString() ?? button.Name ?? "Unknown Button";
            TrackButtonClick(buttonName, location, additionalInfo);
        }

        /// <summary>
        /// Extension method to automatically track menu item clicks
        /// </summary>
        public static void TrackClick(this System.Windows.Controls.MenuItem menuItem, string location, string additionalInfo = "")
        {
            var itemName = menuItem.Header?.ToString() ?? menuItem.Name ?? "Unknown Menu Item";
            TrackButtonClick(itemName, location, additionalInfo);
        }

        /// <summary>
        /// Track application startup
        /// </summary>
        public static void TrackApplicationStart()
        {
            TrackAction("APPLICATION", "Application started", $"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
        }

        /// <summary>
        /// Track application shutdown
        /// </summary>
        public static void TrackApplicationStop()
        {
            TrackAction("APPLICATION", "Application stopping", "User requested shutdown");
        }

        /// <summary>
        /// Track SIP events
        /// </summary>
        public static void TrackSipEvent(string eventType, string details = "")
        {
            TrackAction("SIP", eventType, details);
        }

        /// <summary>
        /// Track audio events
        /// </summary>
        public static void TrackAudioEvent(string eventType, string details = "")
        {
            TrackAction("AUDIO", eventType, details);
        }

        /// <summary>
        /// Track network events
        /// </summary>
        public static void TrackNetworkEvent(string eventType, string details = "")
        {
            TrackAction("NETWORK", eventType, details);
        }

        /// <summary>
        /// Track UI events (window open/close, page navigation, etc.)
        /// </summary>
        public static void TrackUIEvent(string eventType, string details = "")
        {
            TrackAction("UI", eventType, details);
        }

        /// <summary>
        /// Wrapper for try-catch blocks that automatically logs exceptions
        /// </summary>
        public static void SafeExecute(string operationName, string source, Action operation)
        {
            try
            {
                operation();
                TrackAction(source, $"{operationName} completed successfully");
            }
            catch (Exception ex)
            {
                TrackError(source, $"{operationName} failed", ex);
                throw; // Re-throw to maintain original exception handling
            }
        }

        /// <summary>
        /// Async wrapper for try-catch blocks that automatically logs exceptions
        /// </summary>
        public static async System.Threading.Tasks.Task SafeExecuteAsync(string operationName, string source, Func<System.Threading.Tasks.Task> operation)
        {
            try
            {
                await operation();
                TrackAction(source, $"{operationName} completed successfully");
            }
            catch (Exception ex)
            {
                TrackError(source, $"{operationName} failed", ex);
                throw; // Re-throw to maintain original exception handling
            }
        }
    }
}
