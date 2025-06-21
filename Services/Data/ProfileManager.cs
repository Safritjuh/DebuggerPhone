using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WindowsSipPhone.Models;

namespace WindowsSipPhone.Utils
{
    /// <summary>
    /// Utility class for managing SIP profiles (import/export/validation)
    /// </summary>
    public static class ProfileManager
    {
        /// <summary>
        /// Export a profile to INI file
        /// </summary>
        public static void ExportProfileToIni(SipProfile profile, string filePath)
        {
            try
            {
                SipProfile.SaveProfileToIniFile(profile, filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export profile to INI: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Import a profile from INI file
        /// </summary>
        public static SipProfile ImportProfileFromIni(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Profile file not found: {filePath}");
                
                var data = IniFileHandler.ReadIniFile(filePath);
                
                if (!data.ContainsKey("Profile"))
                    throw new InvalidDataException("Invalid INI file format - missing Profile section");
                
                var profile = new SipProfile
                {
                    Name = IniFileHandler.GetValue(data, "Profile", "Name", ""),
                    Description = IniFileHandler.GetValue(data, "Profile", "Description", ""),
                    IsCustom = true, // Mark as custom since it's imported
                    
                    // Connection settings
                    RegistrationExpiry = IniFileHandler.GetIntValue(data, "Connection", "RegistrationExpiry", 3600),
                    RequireKeepAlive = IniFileHandler.GetBoolValue(data, "Connection", "RequireKeepAlive", false),
                    KeepAliveInterval = IniFileHandler.GetIntValue(data, "Connection", "KeepAliveInterval", 30),
                    Transport = IniFileHandler.GetValue(data, "Connection", "Transport", "TCP"),
                    DefaultPort = IniFileHandler.GetIntValue(data, "Connection", "DefaultPort", 5060),
                    
                    // Protocol settings
                    UserAgentString = IniFileHandler.GetValue(data, "Protocol", "UserAgentString", "Windows-SIP-Phone/2.0"),
                    UseShortHeaders = IniFileHandler.GetBoolValue(data, "Protocol", "UseShortHeaders", false),
                    SendPreciseTimers = IniFileHandler.GetBoolValue(data, "Protocol", "SendPreciseTimers", true),
                    
                    // Media settings
                    PreferredCodecs = IniFileHandler.GetListValue(data, "Media", "PreferredCodecs", new List<string> { "PCMU", "PCMA" }),
                    RequireSTUN = IniFileHandler.GetBoolValue(data, "Media", "RequireSTUN", false),
                    STUNServer = IniFileHandler.GetValue(data, "Media", "STUNServer", ""),
                    
                    // Custom headers
                    CustomHeaders = IniFileHandler.GetDictionaryValue(data, "CustomHeaders", "Header_")
                };
                
                if (string.IsNullOrWhiteSpace(profile.Name))
                    throw new InvalidDataException("Profile name is required");
                
                // Validate the imported profile
                ValidateProfile(profile);
                
                return profile;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException || ex is InvalidDataException))
            {
                throw new InvalidOperationException($"Failed to import profile from INI: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Export a profile to JSON file (legacy support)
        /// </summary>
        public static void ExportProfile(SipProfile profile, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export profile: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Import a profile from JSON file (legacy support)
        /// </summary>
        public static SipProfile ImportProfile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Profile file not found: {filePath}");
                
                var json = File.ReadAllText(filePath);
                var profile = JsonSerializer.Deserialize<SipProfile>(json);
                
                if (profile == null)
                    throw new InvalidDataException("Failed to deserialize profile data");
                
                // Mark as custom since it's imported
                profile.IsCustom = true;
                
                // Validate the imported profile
                ValidateProfile(profile);
                
                return profile;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException || ex is InvalidDataException))
            {
                throw new InvalidOperationException($"Failed to import profile: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Export all profiles to a directory as INI files
        /// </summary>
        public static void ExportAllProfilesToIni(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            
            var profiles = SipProfile.GetPredefinedProfiles();
            
            foreach (var profile in profiles)
            {
                var fileName = $"{profile.Name.Replace(" ", "_")}.ini";
                var filePath = Path.Combine(directoryPath, fileName);
                ExportProfileToIni(profile, filePath);
            }
        }
        
        /// <summary>
        /// Export all profiles to a directory (legacy JSON support)
        /// </summary>
        public static void ExportAllProfiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            
            var profiles = SipProfile.GetPredefinedProfiles();
            
            foreach (var profile in profiles)
            {
                var fileName = $"{profile.Name.Replace(" ", "_")}_Profile.json";
                var filePath = Path.Combine(directoryPath, fileName);
                ExportProfile(profile, filePath);
            }
        }
        
        /// <summary>
        /// Validate a SIP profile for common issues
        /// </summary>
        public static void ValidateProfile(SipProfile profile)
        {
            var errors = new List<string>();
            
            if (string.IsNullOrWhiteSpace(profile.Name))
                errors.Add("Profile name is required");
            
            if (profile.RegistrationExpiry <= 0 || profile.RegistrationExpiry > 86400)
                errors.Add("Registration expiry must be between 1 and 86400 seconds");
            
            if (profile.RequireKeepAlive && (profile.KeepAliveInterval <= 0 || profile.KeepAliveInterval > 300))
                errors.Add("Keep-alive interval must be between 1 and 300 seconds when enabled");
            
            if (string.IsNullOrWhiteSpace(profile.Transport))
                errors.Add("Transport protocol is required");
            else if (!IsValidTransport(profile.Transport))
                errors.Add("Transport must be TCP, UDP, or TLS");
            
            if (string.IsNullOrWhiteSpace(profile.UserAgentString))
                errors.Add("User-Agent string is required");
            
            if (profile.DefaultPort <= 0 || profile.DefaultPort > 65535)
                errors.Add("Default port must be between 1 and 65535");
            
            // Validate custom headers
            foreach (var header in profile.CustomHeaders)
            {
                if (string.IsNullOrWhiteSpace(header.Key))
                    errors.Add("Custom header names cannot be empty");
                
                if (header.Key.Contains(" ") || header.Key.Contains(":"))
                    errors.Add($"Invalid header name: {header.Key}");
            }
            
            if (errors.Count > 0)
                throw new ArgumentException($"Profile validation failed:\n- {string.Join("\n- ", errors)}");
        }
        
        private static bool IsValidTransport(string transport)
        {
            var validTransports = new[] { "TCP", "UDP", "TLS" };
            return Array.Exists(validTransports, t => t.Equals(transport, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Get profile compatibility information
        /// </summary>
        public static string GetProfileCompatibilityInfo(SipProfile profile)
        {
            var info = new List<string>();
            
            switch (profile.Name.ToLower())
            {
                case "avaya ip office":
                    info.Add("✓ Optimized for Avaya IP Office systems");
                    info.Add("✓ Long registration intervals reduce server load");
                    info.Add("✓ Compatible with Avaya-specific extensions");
                    break;
                    
                case "cloud generic":
                    info.Add("✓ Works with most cloud SIP providers");
                    info.Add("✓ Keep-alive prevents NAT timeouts"); 
                    info.Add("✓ Short registration intervals for better reliability");
                    break;
                    
                case "freeswitch":
                    info.Add("✓ Optimized for FreeSWITCH platforms");
                    info.Add("✓ Supports advanced codec negotiation");
                    info.Add("✓ Compatible with FreeSWITCH-specific features");
                    break;
                    
                case "cisco":
                    info.Add("✓ Optimized for Cisco systems");
                    info.Add("✓ G.729 codec preference for bandwidth efficiency");
                    info.Add("✓ Compatible with Cisco-specific protocols");
                    break;
                    
                default:
                    info.Add("✓ Generic profile works with most SIP systems");
                    info.Add("✓ RFC 3261 compliant settings");
                    info.Add("✓ Balanced configuration for broad compatibility");
                    break;
            }
            
            return string.Join("\n", info);
        }
    }
}