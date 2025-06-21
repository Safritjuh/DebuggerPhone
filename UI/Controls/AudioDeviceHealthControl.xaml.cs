using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;
using WpfColor = System.Windows.Media.Color;

namespace WindowsSipPhone.Controls
{    public partial class AudioDeviceHealthControl : System.Windows.Controls.UserControl
    {
        private DispatcherTimer _qualityMonitorTimer;
        private DispatcherTimer _levelMeterTimer;
        private bool _isMonitoring = false;
        private Random _random = new Random(); // For mock audio levels

        public class AudioHealthResult
        {
            public bool InputDeviceAvailable { get; set; }
            public bool OutputDeviceAvailable { get; set; }
            public string InputDeviceName { get; set; } = "";
            public string OutputDeviceName { get; set; } = "";
            public int SampleRate { get; set; }
            public int Latency { get; set; }
            public int BufferSize { get; set; }
            public string ErrorMessage { get; set; } = "";
        }

        public event EventHandler<AudioHealthResult>? HealthCheckCompleted;

        public AudioDeviceHealthControl()
        {
            InitializeComponent();
            
            // Setup quality monitoring timer
            _qualityMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _qualityMonitorTimer.Tick += QualityMonitorTimer_Tick;
            
            // Setup level meter timer
            _levelMeterTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _levelMeterTimer.Tick += LevelMeterTimer_Tick;
            
            InitializeAudioDevices();
        }

        private void InitializeAudioDevices()
        {
            try
            {
                // Get current audio devices
                var inputDevices = WaveInEvent.DeviceCount;
                var outputDevices = WaveOut.DeviceCount;
                
                if (inputDevices > 0)
                {
                    var inputCaps = WaveInEvent.GetCapabilities(0);
                    InputDeviceText.Text = inputCaps.ProductName;
                    InputStatusText.Text = "✅ Available";
                    InputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
                }
                else
                {
                    InputDeviceText.Text = "None available";
                    InputStatusText.Text = "❌ Not available";
                    InputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
                }
                
                if (outputDevices > 0)
                {
                    var outputCaps = WaveOut.GetCapabilities(0);
                    OutputDeviceText.Text = outputCaps.ProductName;
                    OutputStatusText.Text = "✅ Available";
                    OutputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
                }
                else
                {
                    OutputDeviceText.Text = "None available";
                    OutputStatusText.Text = "❌ Not available";
                    OutputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
                }
                
                // Set default device specifications
                SampleRateText.Text = "48 kHz";
                LatencyText.Text = "< 20ms";
                BufferSizeText.Text = "1024 samples";
                
                Console.WriteLine($"[AUDIO HEALTH] Initialized - Input devices: {inputDevices}, Output devices: {outputDevices}");
            }
            catch (Exception ex)
            {
                ShowError($"Error initializing audio devices: {ex.Message}");
                Console.WriteLine($"[AUDIO HEALTH] Initialization error: {ex.Message}");
            }
        }

        private async void TestInputButton_Click(object sender, RoutedEventArgs e)
        {
            await TestInputDevice();
        }

        private async void TestOutputButton_Click(object sender, RoutedEventArgs e)
        {
            await TestOutputDevice();
        }

        private async Task TestInputDevice()
        {
            try
            {
                TestInputButton.Content = "🔄 Testing...";
                TestInputButton.IsEnabled = false;
                
                Console.WriteLine("[AUDIO HEALTH] Testing input device...");
                
                // Test input device by attempting to create WaveInEvent
                using var waveIn = new WaveInEvent();
                waveIn.WaveFormat = new WaveFormat(48000, 16, 1);
                
                var testResult = await Task.Run(() =>
                {
                    try
                    {
                        waveIn.StartRecording();
                        System.Threading.Thread.Sleep(500); // Record for 0.5 seconds
                        waveIn.StopRecording();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
                
                if (testResult)
                {
                    InputStatusText.Text = "✅ Test passed";
                    InputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
                    Console.WriteLine("[AUDIO HEALTH] Input device test passed");
                }
                else
                {
                    InputStatusText.Text = "❌ Test failed";
                    InputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
                    Console.WriteLine("[AUDIO HEALTH] Input device test failed");
                }
            }
            catch (Exception ex)
            {
                InputStatusText.Text = "❌ Test error";
                InputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
                ShowError($"Input device test error: {ex.Message}");
                Console.WriteLine($"[AUDIO HEALTH] Input test error: {ex.Message}");
            }
            finally
            {
                TestInputButton.Content = "🔄 Test";
                TestInputButton.IsEnabled = true;
            }
        }

        private async Task TestOutputDevice()
        {
            try
            {
                TestOutputButton.Content = "🔄 Testing...";
                TestOutputButton.IsEnabled = false;
                
                Console.WriteLine("[AUDIO HEALTH] Testing output device...");
                
                // Test output device by attempting to create WaveOut
                using var waveOut = new WaveOut();
                
                var testResult = await Task.Run(() =>
                {
                    try
                    {
                        // Create a simple test tone
                        var waveFormat = new WaveFormat(48000, 16, 1);
                        var bufferedWaveProvider = new BufferedWaveProvider(waveFormat);
                        
                        // Generate a brief test tone (silent for now to avoid disturbing users)
                        var buffer = new byte[waveFormat.AverageBytesPerSecond / 10]; // 0.1 second of silence
                        bufferedWaveProvider.AddSamples(buffer, 0, buffer.Length);
                        
                        waveOut.Init(bufferedWaveProvider);
                        waveOut.Play();
                        System.Threading.Thread.Sleep(200);
                        waveOut.Stop();
                        
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
                
                if (testResult)
                {
                    OutputStatusText.Text = "✅ Test passed";
                    OutputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
                    Console.WriteLine("[AUDIO HEALTH] Output device test passed");
                }
                else
                {
                    OutputStatusText.Text = "❌ Test failed";
                    OutputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
                    Console.WriteLine("[AUDIO HEALTH] Output device test failed");
                }
            }
            catch (Exception ex)
            {
                OutputStatusText.Text = "❌ Test error";
                OutputStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
                ShowError($"Output device test error: {ex.Message}");
                Console.WriteLine($"[AUDIO HEALTH] Output test error: {ex.Message}");
            }
            finally
            {
                TestOutputButton.Content = "🔄 Test";
                TestOutputButton.IsEnabled = true;
            }
        }

        private void QualityMonitorToggle_Checked(object sender, RoutedEventArgs e)
        {
            StartQualityMonitoring();
        }

        private void QualityMonitorToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            StopQualityMonitoring();
        }

        private void StartQualityMonitoring()
        {
            _isMonitoring = true;
            _qualityMonitorTimer.Start();
            _levelMeterTimer.Start();
            
            AudioLevelsPanel.Visibility = Visibility.Visible;
            QualityStatusText.Text = "✅ Monitoring active";
            QualityStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
            
            Console.WriteLine("[AUDIO HEALTH] Quality monitoring started");
        }

        private void StopQualityMonitoring()
        {
            _isMonitoring = false;
            _qualityMonitorTimer.Stop();
            _levelMeterTimer.Stop();
            
            AudioLevelsPanel.Visibility = Visibility.Collapsed;
            QualityStatusText.Text = "⏸️ Disabled";
            QualityStatusText.Foreground = new SolidColorBrush(WpfColor.FromRgb(106, 106, 106)); // Gray
            
            Console.WriteLine("[AUDIO HEALTH] Quality monitoring stopped");
        }

        private void QualityMonitorTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isMonitoring) return;
            
            // Perform quality checks
            var result = new AudioHealthResult
            {
                InputDeviceAvailable = WaveInEvent.DeviceCount > 0,
                OutputDeviceAvailable = WaveOut.DeviceCount > 0,
                SampleRate = 48000,
                Latency = 20,
                BufferSize = 1024
            };
            
            if (WaveInEvent.DeviceCount > 0)
            {
                result.InputDeviceName = WaveInEvent.GetCapabilities(0).ProductName;
            }
            
            if (WaveOut.DeviceCount > 0)
            {
                result.OutputDeviceName = WaveOut.GetCapabilities(0).ProductName;
            }
            
            HealthCheckCompleted?.Invoke(this, result);
        }

        private void LevelMeterTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isMonitoring) return;
            
            // Mock audio levels for demonstration
            // In a real implementation, this would get actual audio levels from the audio pipeline
            var inputLevel = _random.Next(0, 80); // Mock input level
            var outputLevel = _random.Next(0, 60); // Mock output level
            
            InputLevelBar.Value = inputLevel;
            InputLevelText.Text = $"{inputLevel}%";
            
            OutputLevelBar.Value = outputLevel;
            OutputLevelText.Text = $"{outputLevel}%";
            
            // Color-code the progress bars based on level
            UpdateLevelBarColor(InputLevelBar, inputLevel);
            UpdateLevelBarColor(OutputLevelBar, outputLevel);
        }

        private void UpdateLevelBarColor(System.Windows.Controls.ProgressBar bar, int level)
        {
            if (level < 30)
            {
                bar.Foreground = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94)); // Green
            }
            else if (level < 70)
            {
                bar.Foreground = new SolidColorBrush(WpfColor.FromRgb(251, 191, 36)); // Yellow
            }
            else
            {
                bar.Foreground = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68)); // Red
            }
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
        }

        public void UpdateDeviceStatus(string inputDevice, string outputDevice)
        {
            InputDeviceText.Text = inputDevice;
            OutputDeviceText.Text = outputDevice;
        }

        public void SetAudioSpecs(int sampleRate, int latency, int bufferSize)
        {
            SampleRateText.Text = $"{sampleRate / 1000} kHz";
            LatencyText.Text = $"{latency}ms";
            BufferSizeText.Text = $"{bufferSize} samples";
        }
    }
}
