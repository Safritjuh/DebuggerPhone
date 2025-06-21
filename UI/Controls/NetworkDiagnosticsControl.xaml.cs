using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsSipPhone.UI.Controls
{
    public partial class NetworkDiagnosticsControl : System.Windows.Controls.UserControl
    {
        private DispatcherTimer _pingTimer;
        private bool _isRunningDiagnostics = false;

        public class DiagnosticResult
        {
            public string TestName { get; set; } = "";
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public long ResponseTime { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        public event EventHandler<DiagnosticResult>? DiagnosticCompleted;

        public NetworkDiagnosticsControl()
        {
            InitializeComponent();
            
            // Setup periodic ping timer (disabled by default)
            _pingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _pingTimer.Tick += PingTimer_Tick;
        }

        public async void RunDiagnosticsButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (_isRunningDiagnostics)
            {
                StopDiagnostics();
                return;
            }

            await StartDiagnostics();
        }

        private async Task StartDiagnostics()
        {
            _isRunningDiagnostics = true;
            RunDiagnosticsButton.Content = "⏹️ Stop Diagnostics";
            DiagnosticsStatus.Text = "🔄 Running diagnostics...";
            DiagnosticsStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)); // Blue

            Console.WriteLine("[NETWORK DIAGNOSTICS] Starting comprehensive network diagnostics...");

            try
            {
                // Test 1: Internet Connectivity
                await TestInternetConnectivity();
                
                // Test 2: DNS Resolution
                await TestDnsResolution();
                
                // Test 3: SIP Server Connectivity
                await TestSipServerConnectivity();
                
                // Test 4: Network Interface Status
                TestNetworkInterfaces();
                
                // Start continuous monitoring if all tests pass
                _pingTimer.Start();
                
                DiagnosticsStatus.Text = "✅ Diagnostics completed - Monitoring active";
                DiagnosticsStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)); // Green
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK DIAGNOSTICS] Error during diagnostics: {ex.Message}");
                DiagnosticsStatus.Text = $"❌ Diagnostics failed: {ex.Message}";
                DiagnosticsStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red
                _isRunningDiagnostics = false;
                RunDiagnosticsButton.Content = "🔧 Run Diagnostics";
            }
        }

        private void StopDiagnostics()
        {
            _isRunningDiagnostics = false;
            _pingTimer.Stop();
            RunDiagnosticsButton.Content = "🔧 Run Diagnostics";
            DiagnosticsStatus.Text = "⏹️ Diagnostics stopped";
            DiagnosticsStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(106, 106, 106)); // Gray
            Console.WriteLine("[NETWORK DIAGNOSTICS] Diagnostics stopped by user");
        }

        private async Task TestInternetConnectivity()
        {
            var result = new DiagnosticResult { TestName = "Internet Connectivity" };
            
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 5000); // Google DNS
                
                result.Success = reply.Status == IPStatus.Success;
                result.ResponseTime = reply.RoundtripTime;
                result.Message = result.Success 
                    ? $"Connected ({reply.RoundtripTime}ms)"
                    : $"Failed: {reply.Status}";
                    
                Console.WriteLine($"[NETWORK DIAGNOSTICS] Internet connectivity: {result.Message}");
                UpdateTestResult("InternetTest", result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error: {ex.Message}";
                Console.WriteLine($"[NETWORK DIAGNOSTICS] Internet test error: {ex.Message}");
                UpdateTestResult("InternetTest", result);
            }
            
            DiagnosticCompleted?.Invoke(this, result);
        }

        private async Task TestDnsResolution()
        {
            var result = new DiagnosticResult { TestName = "DNS Resolution" };
            
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var hostEntry = await System.Net.Dns.GetHostEntryAsync("google.com");
                stopwatch.Stop();
                
                result.Success = hostEntry.AddressList.Length > 0;
                result.ResponseTime = stopwatch.ElapsedMilliseconds;
                result.Message = result.Success 
                    ? $"Resolved ({stopwatch.ElapsedMilliseconds}ms)"
                    : "Failed to resolve";
                    
                Console.WriteLine($"[NETWORK DIAGNOSTICS] DNS resolution: {result.Message}");
                UpdateTestResult("DnsTest", result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error: {ex.Message}";
                Console.WriteLine($"[NETWORK DIAGNOSTICS] DNS test error: {ex.Message}");
                UpdateTestResult("DnsTest", result);
            }
            
            DiagnosticCompleted?.Invoke(this, result);
        }

        private async Task TestSipServerConnectivity()
        {
            var result = new DiagnosticResult { TestName = "SIP Server Connectivity" };
            var sipServer = "192.168.1.180"; // From test credentials
            
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(sipServer, 5000);
                
                result.Success = reply.Status == IPStatus.Success;
                result.ResponseTime = reply.RoundtripTime;
                result.Message = result.Success 
                    ? $"SIP server reachable ({reply.RoundtripTime}ms)"
                    : $"SIP server unreachable: {reply.Status}";
                    
                Console.WriteLine($"[NETWORK DIAGNOSTICS] SIP server connectivity: {result.Message}");
                UpdateTestResult("SipServerTest", result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error: {ex.Message}";
                Console.WriteLine($"[NETWORK DIAGNOSTICS] SIP server test error: {ex.Message}");
                UpdateTestResult("SipServerTest", result);
            }
            
            DiagnosticCompleted?.Invoke(this, result);
        }

        private void TestNetworkInterfaces()
        {
            var result = new DiagnosticResult { TestName = "Network Interfaces" };
            
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                int activeInterfaces = 0;
                
                foreach (var iface in interfaces)
                {
                    if (iface.OperationalStatus == OperationalStatus.Up && 
                        iface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        activeInterfaces++;
                        Console.WriteLine($"[NETWORK DIAGNOSTICS] Active interface: {iface.Name} ({iface.NetworkInterfaceType})");
                    }
                }
                
                result.Success = activeInterfaces > 0;
                result.Message = $"{activeInterfaces} active interface(s)";
                result.ResponseTime = 0;
                
                Console.WriteLine($"[NETWORK DIAGNOSTICS] Network interfaces: {result.Message}");
                UpdateTestResult("InterfacesTest", result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error: {ex.Message}";
                Console.WriteLine($"[NETWORK DIAGNOSTICS] Interface test error: {ex.Message}");
                UpdateTestResult("InterfacesTest", result);
            }
            
            DiagnosticCompleted?.Invoke(this, result);
        }

        private void UpdateTestResult(string testName, DiagnosticResult result)
        {
            // Update UI based on test name
            var statusIcon = result.Success ? "✅" : "❌";
            var statusColor = result.Success 
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94))  // Green
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red

            switch (testName)
            {
                case "InternetTest":
                    InternetStatus.Text = $"{statusIcon} {result.Message}";
                    InternetStatus.Foreground = statusColor;
                    break;
                case "DnsTest":
                    DnsStatus.Text = $"{statusIcon} {result.Message}";
                    DnsStatus.Foreground = statusColor;
                    break;
                case "SipServerTest":
                    SipServerStatus.Text = $"{statusIcon} {result.Message}";
                    SipServerStatus.Foreground = statusColor;
                    break;
                case "InterfacesTest":
                    InterfacesStatus.Text = $"{statusIcon} {result.Message}";
                    InterfacesStatus.Foreground = statusColor;
                    break;
            }
        }

        private async void PingTimer_Tick(object? sender, EventArgs e)
        {
            // Periodic ping to SIP server to maintain connectivity monitoring
            if (_isRunningDiagnostics)
            {
                await TestSipServerConnectivity();
            }
        }

        private void GenerateReportButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateDiagnosticReport();
        }

        private void GenerateDiagnosticReport()
        {
            var report = $@"
📋 NETWORK DIAGNOSTIC REPORT
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
==============================================

🌐 Internet Connectivity: {InternetStatus.Text}
🔍 DNS Resolution: {DnsStatus.Text}  
📞 SIP Server (192.168.1.180): {SipServerStatus.Text}
🔌 Network Interfaces: {InterfacesStatus.Text}

📊 System Information:
- OS: {Environment.OSVersion}
- Machine: {Environment.MachineName}
- User: {Environment.UserName}

==============================================
Report generated by SIP Phone Diagnostics
";

            Console.WriteLine("[NETWORK DIAGNOSTICS] Diagnostic Report:");
            Console.WriteLine(report);
            
            // Show report in a message box for now
            System.Windows.MessageBox.Show(report, "Network Diagnostic Report", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Clean up timer
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _pingTimer?.Stop();
        }
    }
}
