using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfColor = System.Windows.Media.Color;

namespace WindowsSipPhone.Controls
{
    public partial class SipServerHealthControl : System.Windows.Controls.UserControl
    {
        private DispatcherTimer _healthCheckTimer;
        private bool _autoReconnectEnabled = false;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 5;

        public class SipHealthResult
        {
            public bool IsConnected { get; set; }
            public bool IsRegistered { get; set; }
            public string LastResponse { get; set; } = "";
            public DateTime LastCheck { get; set; } = DateTime.Now;
            public string ErrorMessage { get; set; } = "";
        }

        public event EventHandler<SipHealthResult>? HealthCheckCompleted;

        public SipServerHealthControl()
        {
            InitializeComponent();
            
            // Setup health check timer
            _healthCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _healthCheckTimer.Tick += HealthCheckTimer_Tick;
            
            UpdateUI();
        }

        public async void TestConnectionButton_Click(object? sender, RoutedEventArgs? e)
        {
            await PerformHealthCheck();
        }

        private void AutoReconnectToggle_Checked(object sender, RoutedEventArgs e)
        {
            _autoReconnectEnabled = true;
            _healthCheckTimer.Start();
            ReconnectionStatusText.Text = "✅ Enabled - Monitoring every 30s";
            ReconnectionStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
            Console.WriteLine("[SIP HEALTH] Auto-reconnection enabled");
        }

        private void AutoReconnectToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoReconnectEnabled = false;
            _healthCheckTimer.Stop();
            ReconnectionStatusText.Text = "⏸️ Disabled";
            ReconnectionStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(106, 106, 106)); // Gray
            Console.WriteLine("[SIP HEALTH] Auto-reconnection disabled");
        }

        private async void HealthCheckTimer_Tick(object? sender, EventArgs e)
        {
            await PerformHealthCheck();
        }

        private async Task PerformHealthCheck()
        {
            try
            {
                TestConnectionButton.Content = "🔄 Testing...";
                TestConnectionButton.IsEnabled = false;
                
                var result = new SipHealthResult();
                
                // Get SIP server configuration (placeholder - would integrate with actual SIP client)
                var sipServer = GetSipServerAddress();
                if (string.IsNullOrEmpty(sipServer))
                {
                    result.ErrorMessage = "No SIP server configured";
                    UpdateUIWithResult(result);
                    return;
                }
                
                ServerAddressText.Text = $"Server: {sipServer}";
                
                // Test network connectivity to SIP server
                result.IsConnected = await TestSipServerConnectivity(sipServer);
                
                if (result.IsConnected)
                {
                    // Test SIP registration status (placeholder - would integrate with actual SIP client)
                    result.IsRegistered = await TestSipRegistration();
                    result.LastResponse = result.IsRegistered ? "200 OK" : "Registration Failed";
                }
                else
                {
                    result.ErrorMessage = "Cannot reach SIP server";
                }
                
                UpdateUIWithResult(result);
                HealthCheckCompleted?.Invoke(this, result);
                
                // Handle auto-reconnection if enabled
                if (_autoReconnectEnabled && !result.IsRegistered)
                {
                    await HandleAutoReconnection();
                }
            }
            catch (Exception ex)
            {
                var errorResult = new SipHealthResult
                {
                    ErrorMessage = $"Health check failed: {ex.Message}"
                };
                UpdateUIWithResult(errorResult);
                Console.WriteLine($"[SIP HEALTH] Health check error: {ex.Message}");
            }
            finally
            {
                TestConnectionButton.Content = "🔄 Test";
                TestConnectionButton.IsEnabled = true;
            }
        }

        private async Task<bool> TestSipServerConnectivity(string server)
        {
            try
            {
                // Extract hostname from SIP URI if needed
                var hostname = server.Contains("://") ? new Uri(server).Host : server;
                
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(hostname, 5000);
                
                Console.WriteLine($"[SIP HEALTH] Ping to {hostname}: {reply.Status}, {reply.RoundtripTime}ms");
                return reply.Status == IPStatus.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SIP HEALTH] Error pinging SIP server: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestSipRegistration()
        {
            // Placeholder for actual SIP registration test
            // This would integrate with the main SIP client to check registration status
            await Task.Delay(500); // Simulate registration check
            
            // Return mock result for now - in real implementation, this would check:
            // - SIP registration status from SimpleSipClient
            // - Last registration response code
            // - Registration expiry time
            
            return false; // Mock: not registered
        }

        private async Task HandleAutoReconnection()
        {
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                Console.WriteLine($"[SIP HEALTH] Maximum reconnection attempts ({MaxReconnectAttempts}) reached");
                AutoReconnectToggle.IsChecked = false; // Disable auto-reconnect
                return;
            }

            _reconnectAttempts++;
            Console.WriteLine($"[SIP HEALTH] Attempting reconnection #{_reconnectAttempts}");
            
            ReconnectionStatusText.Text = $"🔄 Reconnecting... (Attempt {_reconnectAttempts}/{MaxReconnectAttempts})";
            ReconnectionStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(59, 130, 246)); // Blue
            
            // Placeholder for actual reconnection logic
            // This would integrate with SimpleSipClient to:
            // - Restart SIP registration
            // - Re-establish RTP connections
            // - Notify user of reconnection status
            
            await Task.Delay(2000); // Simulate reconnection attempt
        }

        private string GetSipServerAddress()
        {
            // Placeholder - would get from application settings
            // In real implementation, this would read from:
            // - Application configuration
            // - User settings
            // - SIP account configuration
            
            return "sip.example.com:5060"; // Mock server
        }

        private void UpdateUI()
        {
            ServerAddressText.Text = $"Server: {GetSipServerAddress()}";
            ConnectionStatusText.Text = "⏸️ Not tested";
            RegistrationStatusText.Text = "❓ Unknown";
            LastResponseText.Text = "None";
        }

        private void UpdateUIWithResult(SipHealthResult result)
        {
            // Update connection status
            if (result.IsConnected)
            {
                ConnectionStatusText.Text = "✅ Connected";
                ConnectionStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
            }
            else
            {
                ConnectionStatusText.Text = "❌ Disconnected";
                ConnectionStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
            }

            // Update registration status
            if (result.IsRegistered)
            {
                RegistrationStatusText.Text = "✅ Registered";
                RegistrationStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
                _reconnectAttempts = 0; // Reset reconnection attempts on successful registration
            }
            else
            {
                RegistrationStatusText.Text = "❌ Not registered";
                RegistrationStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
            }

            // Update last response
            LastResponseText.Text = !string.IsNullOrEmpty(result.LastResponse) ? result.LastResponse : "None";

            // Show/hide error message
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                ErrorMessageText.Text = result.ErrorMessage;
                ErrorMessageText.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorMessageText.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateSipServerAddress(string serverAddress)
        {
            ServerAddressText.Text = $"Server: {serverAddress}";
        }

        public void SetRegistrationStatus(bool isRegistered, string response = "")
        {
            var result = new SipHealthResult
            {
                IsConnected = true, // Assume connected if we have registration info
                IsRegistered = isRegistered,
                LastResponse = response
            };
            UpdateUIWithResult(result);
        }
    }
}
