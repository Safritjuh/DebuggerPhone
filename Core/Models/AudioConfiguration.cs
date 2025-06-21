using System;
using System.Text.Json;
using System.IO;

namespace WindowsSipPhone.Core.Models
{
    /// <summary>
    /// Configuration model for Audio settings persistence
    /// </summary>
    public class AudioConfiguration
    {
        public int InputDeviceId { get; set; } = -1; // -1 = System Default
        public string InputDeviceName { get; set; } = "System Default";
        public int OutputDeviceId { get; set; } = -1; // -1 = System Default  
        public string OutputDeviceName { get; set; } = "System Default";
        public double InputVolume { get; set; } = 0.8;
        public double OutputVolume { get; set; } = 0.8;
        public bool InputMuted { get; set; } = false;
        public bool OutputMuted { get; set; } = false;
        public bool EchoCancellation { get; set; } = true;
        public bool NoiseSuppression { get; set; } = true;
        public bool AutoGainControl { get; set; } = true;
        public int SampleRate { get; set; } = 8000;
        public int BufferSize { get; set; } = 20;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Load configuration from file, return default if not found
        /// </summary>
        public static AudioConfiguration Load()
        {
            try
            {
                var configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AudioConfiguration>(json);
                    return config ?? new AudioConfiguration();
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with defaults
                System.Diagnostics.Debug.WriteLine($"Failed to load audio config: {ex.Message}");
            }
            
            return new AudioConfiguration();
        }
        
        /// <summary>
        /// Save configuration to file
        /// </summary>
        public void Save()
        {
            try
            {
                var configPath = GetConfigFilePath();
                var configDir = Path.GetDirectoryName(configPath);
                
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir!);
                }
                
                LastUpdated = DateTime.Now;
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save audio config: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get the configuration file path in user's AppData
        /// </summary>
        private static string GetConfigFilePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataPath, "WindowsSipPhone", "audio-config.json");
        }
        
        /// <summary>
        /// Convert to AudioSettings object for UI compatibility
        /// </summary>
        public Pages.AudioSettings ToAudioSettings()
        {
            return new Pages.AudioSettings
            {
                InputDevice = new Pages.AudioDevice
                {
                    Id = InputDeviceId,
                    Name = InputDeviceName,
                    IsDefault = InputDeviceId == -1
                },
                OutputDevice = new Pages.AudioDevice
                {
                    Id = OutputDeviceId,
                    Name = OutputDeviceName,
                    IsDefault = OutputDeviceId == -1
                },
                InputVolume = InputVolume,
                OutputVolume = OutputVolume,
                InputMuted = InputMuted,
                OutputMuted = OutputMuted,
                EchoCancellation = EchoCancellation,
                NoiseSuppression = NoiseSuppression,
                AutoGainControl = AutoGainControl,
                SampleRate = SampleRate,
                BufferSize = BufferSize
            };
        }
        
        /// <summary>
        /// Create from AudioSettings object
        /// </summary>
        public static AudioConfiguration FromAudioSettings(Pages.AudioSettings settings)
        {
            return new AudioConfiguration
            {
                InputDeviceId = settings.InputDevice?.Id ?? -1,
                InputDeviceName = settings.InputDevice?.Name ?? "System Default",
                OutputDeviceId = settings.OutputDevice?.Id ?? -1,
                OutputDeviceName = settings.OutputDevice?.Name ?? "System Default",
                InputVolume = settings.InputVolume,
                OutputVolume = settings.OutputVolume,
                InputMuted = settings.InputMuted,
                OutputMuted = settings.OutputMuted,
                EchoCancellation = settings.EchoCancellation,
                NoiseSuppression = settings.NoiseSuppression,
                AutoGainControl = settings.AutoGainControl,
                SampleRate = settings.SampleRate,
                BufferSize = settings.BufferSize
            };
        }
    }
}
