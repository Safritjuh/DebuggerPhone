using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.Wave;

namespace WindowsSipPhone.UI.Pages
{
    /// <summary>
    /// Audio device selection and configuration page - simplified version
    /// </summary>
    public partial class AudioSettingsPage : System.Windows.Controls.UserControl
    {
        private DispatcherTimer? _levelMeterTimer;
          // Microphone test functionality
        private WaveInEvent? _recordingDevice;
        private WaveOutEvent? _playbackDevice;
        private List<byte> _recordedBytes = new();
        private bool _isRecording = false;

        public ObservableCollection<AudioDevice> InputDevices { get; } = new();
        public ObservableCollection<AudioDevice> OutputDevices { get; } = new();

        // Events for device selection changes
        public event EventHandler<AudioDevice>? InputDeviceChanged;
        public event EventHandler<AudioDevice>? OutputDeviceChanged;
        public event EventHandler<AudioSettings>? SettingsChanged;

        public AudioSettingsPage()
        {
            try
            {
                InitializeComponent();
                LoadAudioDevices();
                InitializeLevelMeters();
                LoadCurrentSettings();
                
                // Setup cleanup when control is unloaded
                Unloaded += AudioSettingsPage_Unloaded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO PAGE ERROR] Failed to initialize AudioSettingsPage: {ex.Message}");
                Console.WriteLine($"[AUDIO PAGE ERROR] Stack trace: {ex.StackTrace}");
                
                // Still initialize the component to prevent total failure
                InitializeComponent();
                
                // Show error in UI
                try
                {
                    // If the XAML loaded successfully, we can show error message
                    System.Windows.MessageBox.Show($"Audio settings could not be fully initialized: {ex.Message}\n\nSome audio features may not work correctly.", 
                        "Audio Initialization Warning", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Warning);
                }
                catch
                {
                    // If even showing the message box fails, just continue
                    Console.WriteLine("[AUDIO PAGE ERROR] Could not show error dialog");
                }
            }
        }        private void AudioSettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup
            _levelMeterTimer?.Stop();
            _recordingDevice?.Dispose();
            _playbackDevice?.Dispose();
        }

        private void LoadAudioDevices()
        {
            try
            {
                // Clear existing devices
                InputDevices.Clear();
                OutputDevices.Clear();

                // Enumerate input devices (microphones)
                for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                {
                    var capabilities = WaveInEvent.GetCapabilities(i);
                    InputDevices.Add(new AudioDevice
                    {
                        Id = i,
                        Name = capabilities.ProductName,
                        IsDefault = i == 0
                    });
                }

                // Enumerate output devices (speakers)
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    OutputDevices.Add(new AudioDevice
                    {
                        Id = i,
                        Name = capabilities.ProductName,
                        IsDefault = i == 0
                    });
                }

                // Add "Default" options at the beginning
                if (InputDevices.Count > 0)
                {
                    InputDevices.Insert(0, new AudioDevice { Id = -1, Name = "System Default", IsDefault = true });
                }
                if (OutputDevices.Count > 0)
                {
                    OutputDevices.Insert(0, new AudioDevice { Id = -1, Name = "System Default", IsDefault = true });
                }

                // Bind to combo boxes
                MicrophoneComboBox.ItemsSource = InputDevices;
                SpeakerComboBox.ItemsSource = OutputDevices;

                // Select defaults
                MicrophoneComboBox.SelectedIndex = 0;
                SpeakerComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading audio devices: {ex.Message}", "Audio Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private void InitializeLevelMeters()
        {
            _levelMeterTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // Update every 50ms
            };
            _levelMeterTimer.Tick += UpdateLevelMeters;
            _levelMeterTimer.Start();
        }        private void LoadCurrentSettings()
        {
            // Load basic audio settings
            if (MicrophoneVolumeSlider != null)
                MicrophoneVolumeSlider.Value = 80;
            if (SpeakerVolumeSlider != null)
                SpeakerVolumeSlider.Value = 80;
            
            // Update volume labels
            if (MicrophoneVolumeLabel != null)
                MicrophoneVolumeLabel.Text = "80%";
            if (SpeakerVolumeLabel != null)
                SpeakerVolumeLabel.Text = "80%";
        }        private void UpdateLevelMeters(object? sender, EventArgs e)
        {
            // Update microphone level meter with mock data for now
            var random = new Random();
            var micLevel = random.NextDouble() * 0.3; // Mock level
            
            // Update UI level meter
            if (MicrophoneLevelBar != null)
            {
                MicrophoneLevelBar.Width = micLevel * 200; // Scale to meter width
            }
        }

        // Event Handlers
        private void MicrophoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MicrophoneComboBox.SelectedItem is AudioDevice device)
            {
                InputDeviceChanged?.Invoke(this, device);
                Console.WriteLine($"[AUDIO] Selected microphone: {device.Name}");
            }
        }

        private void SpeakerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpeakerComboBox.SelectedItem is AudioDevice device)
            {
                OutputDeviceChanged?.Invoke(this, device);
                Console.WriteLine($"[AUDIO] Selected speaker: {device.Name}");
            }
        }

        private void MicrophoneMute_Changed(object sender, RoutedEventArgs e)
        {
            var isMuted = MicrophoneMuteCheckBox.IsChecked ?? false;
            Console.WriteLine($"[AUDIO] Microphone muted: {isMuted}");
        }        private void MicrophoneVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MicrophoneVolumeLabel != null)
            {
                MicrophoneVolumeLabel.Text = $"{(int)e.NewValue}%";
            }
            Console.WriteLine($"[AUDIO] Microphone volume: {(int)e.NewValue}%");
        }

        private void SpeakerMute_Changed(object sender, RoutedEventArgs e)
        {
            var isMuted = SpeakerMuteCheckBox.IsChecked ?? false;
            Console.WriteLine($"[AUDIO] Speaker muted: {isMuted}");
        }        private void SpeakerVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpeakerVolumeLabel != null)
            {
                SpeakerVolumeLabel.Text = $"{(int)e.NewValue}%";
            }
            Console.WriteLine($"[AUDIO] Speaker volume: {(int)e.NewValue}%");
        }

        // Microphone test functionality
        private async void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _recordedBytes.Clear();
                
                _recordingDevice = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(8000, 16, 1),
                    BufferMilliseconds = 20
                };
                
                _recordingDevice.DataAvailable += RecordingDevice_DataAvailable;
                _recordingDevice.RecordingStopped += RecordingDevice_RecordingStopped;
                
                _recordingDevice.StartRecording();
                _isRecording = true;
                
                StartRecordingButton.IsEnabled = false;
                StopRecordingButton.IsEnabled = true;
                PlayBackButton.IsEnabled = false;
                
                MicrophoneTestStatusLabel.Text = "Recording... Speak into your microphone (3 seconds)";
                
                // Auto-stop after 3 seconds
                await Task.Delay(3000);
                if (_isRecording)
                {
                    StopRecording_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Recording failed: {ex.Message}");
                MicrophoneTestStatusLabel.Text = $"Recording failed: {ex.Message}";
            }
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recordingDevice != null && _isRecording)
                {
                    _recordingDevice.StopRecording();
                    _isRecording = false;
                }
                
                StartRecordingButton.IsEnabled = true;
                StopRecordingButton.IsEnabled = false;
                
                if (_recordedBytes.Count > 0)
                {
                    PlayBackButton.IsEnabled = true;
                    MicrophoneTestStatusLabel.Text = $"Recording complete! Recorded {_recordedBytes.Count} bytes. Click 'Play Back' to hear it.";
                }
                else
                {
                    MicrophoneTestStatusLabel.Text = "No audio data recorded. Check microphone permissions and device selection.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Stop recording failed: {ex.Message}");
                MicrophoneTestStatusLabel.Text = $"Stop recording failed: {ex.Message}";
            }
        }

        private void PlayBack_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recordedBytes.Count == 0)
                {
                    MicrophoneTestStatusLabel.Text = "No audio data to play back.";
                    return;
                }
                
                // Create a simple playback
                var waveFormat = new WaveFormat(8000, 16, 1);
                var provider = new RawSourceWaveStream(new MemoryStream(_recordedBytes.ToArray()), waveFormat);
                
                _playbackDevice = new WaveOutEvent();
                _playbackDevice.Init(provider);
                _playbackDevice.PlaybackStopped += (s, e) =>
                {
                    _playbackDevice?.Dispose();
                    _playbackDevice = null;
                    MicrophoneTestStatusLabel.Text = "Playback complete.";
                };
                
                _playbackDevice.Play();
                MicrophoneTestStatusLabel.Text = "Playing back recorded audio...";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Playback failed: {ex.Message}");
                MicrophoneTestStatusLabel.Text = $"Playback failed: {ex.Message}";
            }
        }

        private void RecordingDevice_DataAvailable(object? sender, WaveInEventArgs e)
        {
            _recordedBytes.AddRange(e.Buffer.Take(e.BytesRecorded));
        }

        private void RecordingDevice_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            _recordingDevice?.Dispose();
            _recordingDevice = null;
        }

        private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
        {
            // Reset to default values
            MicrophoneComboBox.SelectedIndex = 0;
            SpeakerComboBox.SelectedIndex = 0;
            MicrophoneVolumeSlider.Value = 80;
            SpeakerVolumeSlider.Value = 80;
            MicrophoneMuteCheckBox.IsChecked = false;
            SpeakerMuteCheckBox.IsChecked = false;
            
            Console.WriteLine("[AUDIO] Reset to default settings");
        }        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = new AudioSettings
                {
                    InputDevice = MicrophoneComboBox.SelectedItem as AudioDevice,
                    OutputDevice = SpeakerComboBox.SelectedItem as AudioDevice,
                    InputVolume = MicrophoneVolumeSlider.Value / 100.0,
                    OutputVolume = SpeakerVolumeSlider.Value / 100.0,
                    InputMuted = MicrophoneMuteCheckBox.IsChecked ?? false,
                    OutputMuted = SpeakerMuteCheckBox.IsChecked ?? false,
                    // Set defaults for quality settings
                    EchoCancellation = true,
                    NoiseSuppression = true,
                    AutoGainControl = true,
                    SampleRate = 8000,
                    BufferSize = 20
                };
                
                SettingsChanged?.Invoke(this, settings);
                Console.WriteLine("[AUDIO] Applied audio settings");
                
                System.Windows.MessageBox.Show("Audio settings applied successfully!", "Settings Applied", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Apply settings failed: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to apply settings: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Audio device representation
    /// </summary>
    public class AudioDevice
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsDefault { get; set; }
    }    /// <summary>
    /// Audio settings data structure
    /// </summary>
    public class AudioSettings
    {
        public AudioDevice? InputDevice { get; set; }
        public AudioDevice? OutputDevice { get; set; }
        public double InputVolume { get; set; } = 0.8;
        public double OutputVolume { get; set; } = 0.8;
        public bool InputMuted { get; set; } = false;
        public bool OutputMuted { get; set; } = false;
        public bool EchoCancellation { get; set; } = true;
        public bool NoiseSuppression { get; set; } = true;
        public bool AutoGainControl { get; set; } = true;
        public int SampleRate { get; set; } = 8000;
        public int BufferSize { get; set; } = 20;
    }
}
