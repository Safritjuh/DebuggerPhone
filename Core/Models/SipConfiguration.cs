using System.Text.Json;
using System.IO;
using System;
using System.Linq;

namespace WindowsSipPhone.Core.Models
{
    /// <summary>
    /// Configuration model for SIP settings persistence
    /// </summary>
    public class SipConfiguration
{
    public string Username { get; set; } = string.Empty;
    public string ServerHost { get; set; } = "192.168.1.180";
    public string ServerPort { get; set; } = "5060";
    public string Transport { get; set; } = "TCP";
    public bool RememberCredentials { get; set; } = false;
    public bool AutoRegisterOnStartup { get; set; } = false;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    
    // SIP Profile System
    public string SelectedProfileName { get; set; } = "Generic";
    public List<SipProfile> CustomProfiles { get; set; } = new();
    
    /// <summary>
    /// Gets the currently selected SIP profile
    /// </summary>
    public SipProfile GetSelectedProfile()
    {
        // First check custom profiles
        var customProfile = CustomProfiles.FirstOrDefault(p => p.Name.Equals(SelectedProfileName, StringComparison.OrdinalIgnoreCase));
        if (customProfile != null)
        {
            return customProfile;
        }
        
        // Then check predefined profiles
        var predefinedProfile = SipProfile.GetPredefinedProfile(SelectedProfileName);
        if (predefinedProfile != null)
        {
            return predefinedProfile;
        }
        
        // Fallback to default
        return SipProfile.GetDefaultProfile();
    }
    
    /// <summary>
    /// Load configuration from file, return default if not found
    /// </summary>
    public static SipConfiguration Load()
    {
        try
        {
            var configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<SipConfiguration>(json);
                return config ?? new SipConfiguration();
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with defaults
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
        }
        
        return new SipConfiguration();
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
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
      /// <summary>
    /// Get the configuration file path in user's AppData
    /// </summary>
    private static string GetConfigFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "WindowsSipPhone", "config.json");
    }
}
}
