using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using NAudio.Wave;
using System.Net.NetworkInformation;

namespace WindowsSipPhone.Services.Diagnostics
{
    public class DiagnosticReportGenerator
    {
        public class DiagnosticReport
        {
            public DateTime ReportDate { get; set; }
            public string ApplicationVersion { get; set; } = "";
            public SystemInfo System { get; set; } = new SystemInfo();
            public NetworkInfo Network { get; set; } = new NetworkInfo();
            public AudioInfo Audio { get; set; } = new AudioInfo();
            public SipInfo Sip { get; set; } = new SipInfo();
            public string ErrorMessages { get; set; } = "";
            public string Recommendations { get; set; } = "";
        }

        public class SystemInfo
        {
            public string OperatingSystem { get; set; } = "";
            public string DotNetVersion { get; set; } = "";
            public string Architecture { get; set; } = "";
            public long AvailableMemory { get; set; }
            public double CpuUsage { get; set; }
        }

        public class NetworkInfo
        {
            public bool InternetConnected { get; set; }
            public string PublicIP { get; set; } = "";
            public string LocalIP { get; set; } = "";
            public bool DnsWorking { get; set; }
            public long PingLatency { get; set; }
            public string NetworkInterface { get; set; } = "";
        }

        public class AudioInfo
        {
            public int InputDevices { get; set; }
            public int OutputDevices { get; set; }
            public string DefaultInputDevice { get; set; } = "";
            public string DefaultOutputDevice { get; set; } = "";
            public bool InputTested { get; set; }
            public bool OutputTested { get; set; }
            public int SampleRate { get; set; }
            public int Latency { get; set; }
        }

        public class SipInfo
        {
            public string ServerAddress { get; set; } = "";
            public bool ServerReachable { get; set; }
            public bool Registered { get; set; }
            public string RegistrationStatus { get; set; } = "";
            public DateTime LastRegistration { get; set; }
            public int CallsToday { get; set; }
            public int CallFailures { get; set; }
        }

        public async Task<DiagnosticReport> GenerateReportAsync()
        {
            var report = new DiagnosticReport
            {
                ReportDate = DateTime.Now,
                ApplicationVersion = GetApplicationVersion()
            };

            Console.WriteLine("[DIAGNOSTIC REPORT] Generating comprehensive diagnostic report...");

            try
            {
                // Gather system information
                report.System = await GatherSystemInfoAsync();
                
                // Gather network information
                report.Network = await GatherNetworkInfoAsync();
                
                // Gather audio information
                report.Audio = await GatherAudioInfoAsync();
                
                // Gather SIP information
                report.Sip = await GatherSipInfoAsync();
                
                // Generate recommendations
                report.Recommendations = GenerateRecommendations(report);
                
                Console.WriteLine("[DIAGNOSTIC REPORT] Report generation completed successfully");
            }
            catch (Exception ex)
            {
                report.ErrorMessages = $"Error generating report: {ex.Message}";
                Console.WriteLine($"[DIAGNOSTIC REPORT] Error: {ex.Message}");
            }

            return report;
        }

        private string GetApplicationVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private async Task<SystemInfo> GatherSystemInfoAsync()
        {
            return await Task.Run(() =>
            {
                var systemInfo = new SystemInfo();
                
                try
                {
                    systemInfo.OperatingSystem = Environment.OSVersion.ToString();
                    systemInfo.DotNetVersion = Environment.Version.ToString();
                    systemInfo.Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                    
                    // Get available memory (approximation)
                    var gc = GC.GetTotalMemory(false);
                    systemInfo.AvailableMemory = gc;
                    
                    Console.WriteLine($"[DIAGNOSTIC REPORT] System info gathered: {systemInfo.OperatingSystem}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DIAGNOSTIC REPORT] Error gathering system info: {ex.Message}");
                }
                
                return systemInfo;
            });
        }

        private async Task<NetworkInfo> GatherNetworkInfoAsync()
        {
            var networkInfo = new NetworkInfo();
            
            try
            {
                // Test internet connectivity
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 5000);
                networkInfo.InternetConnected = reply.Status == IPStatus.Success;
                networkInfo.PingLatency = reply.RoundtripTime;
                
                // Get local IP
                var localIPs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName());
                foreach (var ip in localIPs)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        networkInfo.LocalIP = ip.ToString();
                        break;
                    }
                }
                
                // Test DNS
                try
                {
                    var dnsResult = await System.Net.Dns.GetHostAddressesAsync("google.com");
                    networkInfo.DnsWorking = dnsResult.Length > 0;
                }
                catch
                {
                    networkInfo.DnsWorking = false;
                }
                
                // Get network interface info
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var iface in interfaces)
                {
                    if (iface.OperationalStatus == OperationalStatus.Up && 
                        iface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        networkInfo.NetworkInterface = $"{iface.Name} ({iface.NetworkInterfaceType})";
                        break;
                    }
                }
                
                Console.WriteLine($"[DIAGNOSTIC REPORT] Network info gathered: Internet={networkInfo.InternetConnected}, Ping={networkInfo.PingLatency}ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DIAGNOSTIC REPORT] Error gathering network info: {ex.Message}");
            }
            
            return networkInfo;
        }

        private async Task<AudioInfo> GatherAudioInfoAsync()
        {
            return await Task.Run(() =>
            {
                var audioInfo = new AudioInfo();
                
                try
                {
                    // Get device counts
                    audioInfo.InputDevices = WaveInEvent.DeviceCount;
                    audioInfo.OutputDevices = WaveOut.DeviceCount;
                    
                    // Get default devices
                    if (audioInfo.InputDevices > 0)
                    {
                        var inputCaps = WaveInEvent.GetCapabilities(0);
                        audioInfo.DefaultInputDevice = inputCaps.ProductName;
                    }
                    
                    if (audioInfo.OutputDevices > 0)
                    {
                        var outputCaps = WaveOut.GetCapabilities(0);
                        audioInfo.DefaultOutputDevice = outputCaps.ProductName;
                    }
                    
                    // Test devices
                    audioInfo.InputTested = TestInputDevice();
                    audioInfo.OutputTested = TestOutputDevice();
                    
                    // Set default audio specs
                    audioInfo.SampleRate = 48000;
                    audioInfo.Latency = 20;
                    
                    Console.WriteLine($"[DIAGNOSTIC REPORT] Audio info gathered: Input={audioInfo.InputDevices}, Output={audioInfo.OutputDevices}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DIAGNOSTIC REPORT] Error gathering audio info: {ex.Message}");
                }
                
                return audioInfo;
            });
        }

        private bool TestInputDevice()
        {
            try
            {
                using var waveIn = new WaveInEvent();
                waveIn.WaveFormat = new WaveFormat(48000, 16, 1);
                waveIn.StartRecording();
                System.Threading.Thread.Sleep(100);
                waveIn.StopRecording();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TestOutputDevice()
        {
            try
            {
                using var waveOut = new WaveOut();
                var waveFormat = new WaveFormat(48000, 16, 1);
                var bufferedWaveProvider = new BufferedWaveProvider(waveFormat);
                var buffer = new byte[waveFormat.AverageBytesPerSecond / 10];
                bufferedWaveProvider.AddSamples(buffer, 0, buffer.Length);
                waveOut.Init(bufferedWaveProvider);
                waveOut.Play();
                System.Threading.Thread.Sleep(100);
                waveOut.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<SipInfo> GatherSipInfoAsync()
        {
            return await Task.Run(() =>
            {
                var sipInfo = new SipInfo();
                
                try
                {
                    // Placeholder for SIP information gathering
                    // In a real implementation, this would integrate with SimpleSipClient
                    sipInfo.ServerAddress = "sip.example.com:5060"; // Mock
                    sipInfo.ServerReachable = false; // Would test actual server
                    sipInfo.Registered = false; // Would check registration status
                    sipInfo.RegistrationStatus = "Not Registered";
                    sipInfo.LastRegistration = DateTime.MinValue;
                    sipInfo.CallsToday = 0; // Would get from call history
                    sipInfo.CallFailures = 0; // Would get from error logs
                    
                    Console.WriteLine($"[DIAGNOSTIC REPORT] SIP info gathered: Server={sipInfo.ServerAddress}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DIAGNOSTIC REPORT] Error gathering SIP info: {ex.Message}");
                }
                
                return sipInfo;
            });
        }

        private string GenerateRecommendations(DiagnosticReport report)
        {
            var recommendations = new StringBuilder();
            
            // Network recommendations
            if (!report.Network.InternetConnected)
            {
                recommendations.AppendLine("❌ Internet Connection: Check your network connection and try again.");
            }
            else if (report.Network.PingLatency > 100)
            {
                recommendations.AppendLine("⚠️ High Network Latency: Consider using a wired connection for better call quality.");
            }
            
            if (!report.Network.DnsWorking)
            {
                recommendations.AppendLine("❌ DNS Issues: Check DNS settings or try using alternative DNS servers (8.8.8.8, 1.1.1.1).");
            }
            
            // Audio recommendations
            if (report.Audio.InputDevices == 0)
            {
                recommendations.AppendLine("❌ No Input Devices: Connect a microphone or headset to make calls.");
            }
            else if (!report.Audio.InputTested)
            {
                recommendations.AppendLine("⚠️ Input Device Issues: Test your microphone or try selecting a different input device.");
            }
            
            if (report.Audio.OutputDevices == 0)
            {
                recommendations.AppendLine("❌ No Output Devices: Connect speakers or headset to hear calls.");
            }
            else if (!report.Audio.OutputTested)
            {
                recommendations.AppendLine("⚠️ Output Device Issues: Test your speakers or try selecting a different output device.");
            }
            
            // SIP recommendations
            if (!report.Sip.ServerReachable)
            {
                recommendations.AppendLine("❌ SIP Server Unreachable: Check server address and network firewall settings.");
            }
            
            if (!report.Sip.Registered)
            {
                recommendations.AppendLine("⚠️ SIP Registration: Check your SIP credentials and server configuration.");
            }
            
            // General recommendations
            if (recommendations.Length == 0)
            {
                recommendations.AppendLine("✅ System Status: All systems appear to be functioning normally.");
                recommendations.AppendLine("💡 Tip: Regular network and audio testing can help prevent call issues.");
            }
            
            return recommendations.ToString();
        }

        public async Task<string> SaveReportToFileAsync(DiagnosticReport report, string? filePath = null)
        {
            try
            {
                filePath ??= Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"SipPhone_Diagnostic_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                );
                
                var content = FormatReportAsText(report);
                await File.WriteAllTextAsync(filePath, content);
                
                Console.WriteLine($"[DIAGNOSTIC REPORT] Report saved to: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DIAGNOSTIC REPORT] Error saving report: {ex.Message}");
                throw;
            }
        }

        private string FormatReportAsText(DiagnosticReport report)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=====================================");
            sb.AppendLine("    SIP PHONE DIAGNOSTIC REPORT");
            sb.AppendLine("=====================================");
            sb.AppendLine($"Report Date: {report.ReportDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Application Version: {report.ApplicationVersion}");
            sb.AppendLine();
            
            // System Information
            sb.AppendLine("SYSTEM INFORMATION");
            sb.AppendLine("------------------");
            sb.AppendLine($"Operating System: {report.System.OperatingSystem}");
            sb.AppendLine($".NET Version: {report.System.DotNetVersion}");
            sb.AppendLine($"Architecture: {report.System.Architecture}");
            sb.AppendLine($"Available Memory: {report.System.AvailableMemory / 1024 / 1024} MB");
            sb.AppendLine();
            
            // Network Information
            sb.AppendLine("NETWORK INFORMATION");
            sb.AppendLine("-------------------");
            sb.AppendLine($"Internet Connected: {(report.Network.InternetConnected ? "Yes" : "No")}");
            sb.AppendLine($"Local IP Address: {report.Network.LocalIP}");
            sb.AppendLine($"DNS Working: {(report.Network.DnsWorking ? "Yes" : "No")}");
            sb.AppendLine($"Ping Latency: {report.Network.PingLatency}ms");
            sb.AppendLine($"Network Interface: {report.Network.NetworkInterface}");
            sb.AppendLine();
            
            // Audio Information
            sb.AppendLine("AUDIO INFORMATION");
            sb.AppendLine("-----------------");
            sb.AppendLine($"Input Devices: {report.Audio.InputDevices}");
            sb.AppendLine($"Output Devices: {report.Audio.OutputDevices}");
            sb.AppendLine($"Default Input: {report.Audio.DefaultInputDevice}");
            sb.AppendLine($"Default Output: {report.Audio.DefaultOutputDevice}");
            sb.AppendLine($"Input Test: {(report.Audio.InputTested ? "Passed" : "Failed")}");
            sb.AppendLine($"Output Test: {(report.Audio.OutputTested ? "Passed" : "Failed")}");
            sb.AppendLine($"Sample Rate: {report.Audio.SampleRate} Hz");
            sb.AppendLine($"Latency: {report.Audio.Latency}ms");
            sb.AppendLine();
            
            // SIP Information
            sb.AppendLine("SIP INFORMATION");
            sb.AppendLine("---------------");
            sb.AppendLine($"Server Address: {report.Sip.ServerAddress}");
            sb.AppendLine($"Server Reachable: {(report.Sip.ServerReachable ? "Yes" : "No")}");
            sb.AppendLine($"Registration Status: {report.Sip.RegistrationStatus}");
            sb.AppendLine($"Last Registration: {(report.Sip.LastRegistration == DateTime.MinValue ? "Never" : report.Sip.LastRegistration.ToString())}");
            sb.AppendLine($"Calls Today: {report.Sip.CallsToday}");
            sb.AppendLine($"Call Failures: {report.Sip.CallFailures}");
            sb.AppendLine();
            
            // Recommendations
            sb.AppendLine("RECOMMENDATIONS");
            sb.AppendLine("---------------");
            sb.AppendLine(report.Recommendations);
            
            // Error Messages
            if (!string.IsNullOrEmpty(report.ErrorMessages))
            {
                sb.AppendLine("ERROR MESSAGES");
                sb.AppendLine("--------------");
                sb.AppendLine(report.ErrorMessages);
            }
            
            sb.AppendLine();
            sb.AppendLine("=====================================");
            sb.AppendLine("Report generated by Windows SIP Phone");
            sb.AppendLine("=====================================");
            
            return sb.ToString();
        }
    }
}
