using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WindowsSipPhone.Core.Models
{
    /// <summary>
    /// Configuration model for Speed Dial (F1-F12) mapping persistence.
    /// Mirrors the load/save pattern established by AudioConfiguration.
    /// </summary>
    public class SpeedDialConfiguration
    {
        /// <summary>
        /// Speed dial mappings keyed by WPF Key name (e.g. "F1") to phone number.
        /// </summary>
        public Dictionary<string, string> Mappings { get; set; } = new Dictionary<string, string>
        {
            { "F1", "101" },
            { "F2", "102" },
            { "F3", "103" },
            { "F4", "104" },
            { "F5", "105" },
            { "F6", "106" },
            { "F7", "107" },
            { "F8", "108" },
            { "F9", "109" },
            { "F10", "110" },
            { "F11", "111" },
            { "F12", "112" }
        };

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Load configuration from file, return defaults if not found or unreadable.
        /// </summary>
        public static SpeedDialConfiguration Load()
        {
            try
            {
                var configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<SpeedDialConfiguration>(json);
                    return config ?? new SpeedDialConfiguration();
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with defaults
                System.Diagnostics.Debug.WriteLine($"Failed to load speed dial config: {ex.Message}");
            }

            return new SpeedDialConfiguration();
        }

        /// <summary>
        /// Save configuration to file.
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
                System.Diagnostics.Debug.WriteLine($"Failed to save speed dial config: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the configuration file path in the user's AppData folder.
        /// </summary>
        private static string GetConfigFilePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataPath, "WindowsSipPhone", "speeddial-config.json");
        }
    }
}
