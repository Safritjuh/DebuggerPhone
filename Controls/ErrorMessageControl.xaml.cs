using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfColor = System.Windows.Media.Color;

namespace WindowsSipPhone.Controls
{
    public partial class ErrorMessageControl : System.Windows.Controls.UserControl
    {
        private DispatcherTimer _autoDismissTimer;
        private int _countdownSeconds;
        private Action? _retryAction;
        private string _detailedMessage = "";

        public enum ErrorSeverity
        {
            Info,
            Warning,
            Error,
            Critical
        }

        public event EventHandler? ErrorDismissed;
        public event EventHandler? RetryRequested;
        public event EventHandler<string>? DetailsRequested;

        public ErrorMessageControl()
        {
            InitializeComponent();
            
            _autoDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoDismissTimer.Tick += AutoDismissTimer_Tick;
        }

        public void ShowError(string title, string message, ErrorSeverity severity = ErrorSeverity.Error, 
                             Action? retryAction = null, string detailedMessage = "", int autoDismissSeconds = 0)
        {
            ErrorTitle.Text = title;
            ErrorMessage.Text = message;
            _retryAction = retryAction;
            _detailedMessage = detailedMessage;

            // Set icon and colors based on severity
            SetErrorAppearance(severity);

            // Show/hide action buttons
            ErrorActions.Visibility = (retryAction != null || !string.IsNullOrEmpty(detailedMessage)) 
                                     ? Visibility.Visible 
                                     : Visibility.Collapsed;
            
            RetryButton.Visibility = retryAction != null ? Visibility.Visible : Visibility.Collapsed;
            DetailsButton.Visibility = !string.IsNullOrEmpty(detailedMessage) ? Visibility.Visible : Visibility.Collapsed;

            // Setup auto-dismiss if requested
            if (autoDismissSeconds > 0)
            {
                _countdownSeconds = autoDismissSeconds;
                CountdownText.Text = $"{_countdownSeconds}s";
                CountdownText.Visibility = Visibility.Visible;
                _autoDismissTimer.Start();
            }
            else
            {
                CountdownText.Visibility = Visibility.Collapsed;
            }

            // Show the error
            ErrorBorder.Visibility = Visibility.Visible;

            Console.WriteLine($"[ERROR UI] Showing {severity} error: {title} - {message}");
        }

        public void ShowInfo(string title, string message, int autoDismissSeconds = 5)
        {
            ShowError(title, message, ErrorSeverity.Info, null, "", autoDismissSeconds);
        }

        public void ShowWarning(string title, string message, Action? retryAction = null, int autoDismissSeconds = 10)
        {
            ShowError(title, message, ErrorSeverity.Warning, retryAction, "", autoDismissSeconds);
        }

        public void ShowCriticalError(string title, string message, Action? retryAction = null, string detailedMessage = "")
        {
            ShowError(title, message, ErrorSeverity.Critical, retryAction, detailedMessage, 0); // No auto-dismiss for critical errors
        }

        private void SetErrorAppearance(ErrorSeverity severity)
        {
            switch (severity)
            {
                case ErrorSeverity.Info:
                    ErrorIcon.Text = "ℹ️";                    ErrorBorder.Background = new SolidColorBrush(WpfColor.FromArgb(20, 59, 130, 246)); // Light blue
                    ErrorBorder.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(59, 130, 246)); // Blue
                    ErrorTitle.Foreground = new SolidColorBrush(WpfColor.FromRgb(59, 130, 246));
                    ErrorMessage.Foreground = new SolidColorBrush(WpfColor.FromRgb(59, 130, 246));
                    break;

                case ErrorSeverity.Warning:
                    ErrorIcon.Text = "⚠️";                    ErrorBorder.Background = new SolidColorBrush(WpfColor.FromArgb(20, 251, 191, 36)); // Light yellow
                    ErrorBorder.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(251, 191, 36)); // Yellow
                    ErrorTitle.Foreground = new SolidColorBrush(WpfColor.FromRgb(180, 140, 0)); // Darker yellow
                    ErrorMessage.Foreground = new SolidColorBrush(WpfColor.FromRgb(180, 140, 0));
                    break;

                case ErrorSeverity.Error:
                    ErrorIcon.Text = "❌";                    ErrorBorder.Background = new SolidColorBrush(WpfColor.FromArgb(20, 239, 68, 68)); // Light red
                    ErrorBorder.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
                    ErrorTitle.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68));
                    ErrorMessage.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68));
                    break;

                case ErrorSeverity.Critical:
                    ErrorIcon.Text = "🚨";                    ErrorBorder.Background = new SolidColorBrush(WpfColor.FromArgb(30, 220, 20, 60)); // Light dark red
                    ErrorBorder.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(220, 20, 60)); // Dark red
                    ErrorTitle.Foreground = new SolidColorBrush(WpfColor.FromRgb(220, 20, 60));
                    ErrorMessage.Foreground = new SolidColorBrush(WpfColor.FromRgb(220, 20, 60));
                    break;
            }
        }

        public void HideError()
        {
            _autoDismissTimer.Stop();
            ErrorBorder.Visibility = Visibility.Collapsed;
            ErrorDismissed?.Invoke(this, EventArgs.Empty);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideError();
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            RetryRequested?.Invoke(this, EventArgs.Empty);
            _retryAction?.Invoke();
            HideError();
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            DetailsRequested?.Invoke(this, _detailedMessage);
            
            // Show details in a message box for now
            // In a real implementation, this could open a dedicated details window
            System.Windows.MessageBox.Show(_detailedMessage, "Error Details", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AutoDismissTimer_Tick(object? sender, EventArgs e)
        {
            _countdownSeconds--;
            
            if (_countdownSeconds <= 0)
            {
                HideError();
            }
            else
            {
                CountdownText.Text = $"{_countdownSeconds}s";
            }
        }

        // Convenience methods for common error scenarios
        public void ShowNetworkError(string operation, Action? retryAction = null)
        {
            ShowError("Network Error", 
                     $"Failed to {operation}. Please check your internet connection.", 
                     ErrorSeverity.Error, 
                     retryAction, 
                     "This error typically occurs when:\n• Internet connection is down\n• Firewall is blocking the connection\n• Server is temporarily unavailable");
        }

        public void ShowSipServerError(string serverAddress, Action? retryAction = null)
        {
            ShowError("SIP Server Error", 
                     $"Cannot connect to SIP server: {serverAddress}", 
                     ErrorSeverity.Error, 
                     retryAction, 
                     "This error typically occurs when:\n• SIP server is down or unreachable\n• Incorrect server address or port\n• Network firewall blocking SIP traffic\n• Authentication credentials are incorrect");
        }

        public void ShowAudioDeviceError(string deviceType, Action? retryAction = null)
        {
            ShowError("Audio Device Error", 
                     $"Audio {deviceType} device is not available or has failed.", 
                     ErrorSeverity.Warning, 
                     retryAction, 
                     "This error typically occurs when:\n• Audio device is disconnected\n• Device is being used by another application\n• Audio drivers are outdated or corrupted\n• Windows audio service is not running");
        }

        public void ShowCallFailure(string reason, Action? retryAction = null)
        {
            ShowError("Call Failed", 
                     $"Unable to establish call: {reason}", 
                     ErrorSeverity.Error, 
                     retryAction, 
                     "Common call failure reasons:\n• Destination is busy or unavailable\n• Network connectivity issues\n• SIP registration problems\n• Audio device conflicts");
        }

        public void ShowRegistrationSuccess()
        {
            ShowInfo("Registration Successful", "Successfully registered with SIP server.", 3);
        }

        public void ShowConnectionRestored()
        {
            ShowInfo("Connection Restored", "Network connection has been restored.", 3);
        }
    }
}
