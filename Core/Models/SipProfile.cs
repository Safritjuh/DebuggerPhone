using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WindowsSipPhone.Core.Utilities;

namespace WindowsSipPhone.Core.Models
{
    /// <summary>
    /// SIP Profile configuration for platform-specific settings
    /// </summary>
    public class SipProfile
    {
        // Profile Identity
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsCustom { get; set; } = false;
        
        // Connection Settings
        public int RegistrationExpiry { get; set; } = 3600;
        public bool RequireKeepAlive { get; set; } = false;
        public int KeepAliveInterval { get; set; } = 30;
        public string Transport { get; set; } = "TCP";
        
        // Protocol Settings
        public string UserAgentString { get; set; } = "Windows-SIP-Phone/2.0";
        public bool UseShortHeaders { get; set; } = false;
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
        
        // Media Settings  
        public List<string> PreferredCodecs { get; set; } = new();
        public bool RequireSTUN { get; set; } = false;
        public string STUNServer { get; set; } = "";
        
        // Timing & Behavior
        public bool SendPreciseTimers { get; set; } = true;
        public int DefaultPort { get; set; } = 5060;
        
        /// <summary>
        /// Gets all predefined SIP profiles from INI files
        /// </summary>
        public static List<SipProfile> GetPredefinedProfiles()
        {
            var profiles = new List<SipProfile>();
            
            // Try to load from INI files first
            try
            {
                var profiles_iniProfiles = LoadProfilesFromIniFiles();
                if (profiles_iniProfiles.Count > 0)
                    return profiles_iniProfiles;
            }            catch (Exception)
            {
                // Fall back to hardcoded profiles
            }
            
            // Fallback to hardcoded profiles if INI files are not available
            return GetHardcodedProfiles();
        }        
        /// <summary>
        /// Load profiles from INI files in the Profiles directory
        /// </summary>
        private static List<SipProfile> LoadProfilesFromIniFiles()
        {
            var profiles = new List<SipProfile>();
            
            // Start from the application directory and work backwards to find the source profiles folder
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string? profilesDirectory = null;
            
            // Try to find the source profiles directory (for development and user editing)
            var currentDir = new DirectoryInfo(baseDirectory);
            while (currentDir != null)
            {
                var testProfilesDir = Path.Combine(currentDir.FullName, "profiles");
                if (Directory.Exists(testProfilesDir))
                {
                    profilesDirectory = testProfilesDir;
                    break;
                }
                currentDir = currentDir.Parent;
            }
            
            // Fallback: look in the output directory if source not found
            if (profilesDirectory == null)
            {
                profilesDirectory = Path.Combine(baseDirectory, "Profiles");
                if (!Directory.Exists(profilesDirectory))
                {
                    // Final fallback: current working directory
                    profilesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "profiles");                    if (!Directory.Exists(profilesDirectory))
                        return profiles; // Return empty list if no profiles directory found
                }
            }
              // Load all INI files in the profiles directory
            var iniFiles = Directory.GetFiles(profilesDirectory, "*.ini", SearchOption.TopDirectoryOnly);
            
            foreach (var iniFile in iniFiles)
            {                try
                {                    var profile = LoadProfileFromIniFile(iniFile);
                    if (profile != null)
                    {
                        profiles.Add(profile);
                    }
                }                catch (Exception)
                {
                    // Silently skip files that can't be loaded
                }
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Load a single profile from an INI file
        /// </summary>
        private static SipProfile? LoadProfileFromIniFile(string filePath)
        {
            var data = IniFileHandler.ReadIniFile(filePath);
            
            if (!data.ContainsKey("Profile"))
                return null;
            
            var profile = new SipProfile
            {
                Name = IniFileHandler.GetValue(data, "Profile", "Name", ""),
                Description = IniFileHandler.GetValue(data, "Profile", "Description", ""),
                IsCustom = IniFileHandler.GetBoolValue(data, "Profile", "IsCustom", false),
                
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
            
            return string.IsNullOrWhiteSpace(profile.Name) ? null : profile;
        }
        
        /// <summary>
        /// Save a profile to an INI file
        /// </summary>
        public static void SaveProfileToIniFile(SipProfile profile, string filePath)
        {
            var data = new Dictionary<string, Dictionary<string, string>>();
            
            // Profile section
            data["Profile"] = new Dictionary<string, string>
            {
                { "Name", profile.Name },
                { "Description", profile.Description },
                { "IsCustom", profile.IsCustom.ToString().ToLower() }
            };
            
            // Connection section
            data["Connection"] = new Dictionary<string, string>
            {
                { "RegistrationExpiry", profile.RegistrationExpiry.ToString() },
                { "RequireKeepAlive", profile.RequireKeepAlive.ToString().ToLower() },
                { "KeepAliveInterval", profile.KeepAliveInterval.ToString() },
                { "Transport", profile.Transport },
                { "DefaultPort", profile.DefaultPort.ToString() }
            };
            
            // Protocol section
            data["Protocol"] = new Dictionary<string, string>
            {
                { "UserAgentString", profile.UserAgentString },
                { "UseShortHeaders", profile.UseShortHeaders.ToString().ToLower() },
                { "SendPreciseTimers", profile.SendPreciseTimers.ToString().ToLower() }
            };
            
            // Media section
            data["Media"] = new Dictionary<string, string>
            {
                { "PreferredCodecs", string.Join(",", profile.PreferredCodecs) },
                { "RequireSTUN", profile.RequireSTUN.ToString().ToLower() },
                { "STUNServer", profile.STUNServer }
            };
            
            // Custom headers section
            data["CustomHeaders"] = new Dictionary<string, string>();
            foreach (var header in profile.CustomHeaders)
            {
                data["CustomHeaders"][$"Header_{header.Key}"] = header.Value;
            }
            
            IniFileHandler.WriteIniFile(filePath, data);
        }
          /// <summary>
        /// Fallback method that returns hardcoded profiles if INI files are not available
        /// </summary>
        private static List<SipProfile> GetHardcodedProfiles()
        {
            return new List<SipProfile>
            {
                // Generic profile (default current behavior)
                new SipProfile
                {
                    Name = "Generic",
                    Description = "Default generic SIP settings compatible with most platforms",
                    IsCustom = false,
                    RegistrationExpiry = 300,
                    RequireKeepAlive = false,
                    Transport = "TCP",
                    UserAgentString = "Windows-SIP-Phone/2.0",
                    UseShortHeaders = false,
                    PreferredCodecs = new List<string> { "PCMU", "PCMA" },
                    RequireSTUN = false,
                    SendPreciseTimers = true,
                    DefaultPort = 5060
                },
                
                // Avaya IP Office - Updated settings per requirement
                new SipProfile
                {
                    Name = "Avaya IP Office",
                    Description = "Intermedia Elevate platform for BYOD",
                    IsCustom = false,
                    RegistrationExpiry = 180,
                    RequireKeepAlive = false,
                    Transport = "TCP",
                    UserAgentString = "SIP TEST Phone",
                    UseShortHeaders = false,
                    PreferredCodecs = new List<string> { "PCMA" },
                    RequireSTUN = false,
                    SendPreciseTimers = true,
                    DefaultPort = 5060
                },
                
                // Elevate - New profile for Intermedia Elevate platform
                new SipProfile
                {
                    Name = "Elevate",
                    Description = "Intermedia Elevate platform for BYOD",
                    IsCustom = false,
                    RegistrationExpiry = 300,
                    RequireKeepAlive = false,
                    Transport = "TCP",
                    UserAgentString = "SIP TEST Phone",
                    UseShortHeaders = false,
                    PreferredCodecs = new List<string> { "PCMA", "G722" },
                    RequireSTUN = false,
                    SendPreciseTimers = true,
                    DefaultPort = 5060
                },
                
                // Avaya Aura - New profile for Avaya Aura systems
                new SipProfile
                {
                    Name = "Avaya Aura",
                    Description = "Profile for Avaya Aura systems",
                    IsCustom = false,
                    RegistrationExpiry = 300,
                    RequireKeepAlive = false,
                    Transport = "TCP",
                    UserAgentString = "SIP TEST Phone",
                    UseShortHeaders = false,
                    PreferredCodecs = new List<string> { "PCMA" },
                    RequireSTUN = false,
                    SendPreciseTimers = true,
                    DefaultPort = 5060
                }
            };
        }
        
        /// <summary>
        /// Create default INI profile files if they don't exist
        /// </summary>
        public static void CreateDefaultProfilesIfNeeded()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var profilesDirectory = Path.Combine(baseDirectory, "Profiles");
            
            // Try current directory as fallback
            if (!Directory.Exists(profilesDirectory))
            {
                profilesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Profiles");
            }
            
            if (!Directory.Exists(profilesDirectory))
            {
                Directory.CreateDirectory(profilesDirectory);
            }
            
            var hardcodedProfiles = GetHardcodedProfiles();
            
            foreach (var profile in hardcodedProfiles)
            {
                var fileName = profile.Name.Replace(" ", "_") + ".ini";
                var filePath = Path.Combine(profilesDirectory, fileName);
                
                if (!File.Exists(filePath))
                {
                    SaveProfileToIniFile(profile, filePath);
                }
            }
        }
        
        /// <summary>
        /// Gets a predefined profile by name
        /// </summary>
        public static SipProfile? GetPredefinedProfile(string name)
        {
            return GetPredefinedProfiles().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Gets the default generic profile
        /// </summary>
        public static SipProfile GetDefaultProfile()
        {
            return GetPredefinedProfile("Generic") ?? new SipProfile();
        }
        
        public SipProfile Clone()
        {
            return new SipProfile
            {
                Name = Name,
                Description = Description,
                IsCustom = IsCustom,
                RegistrationExpiry = RegistrationExpiry,
                RequireKeepAlive = RequireKeepAlive,
                KeepAliveInterval = KeepAliveInterval,
                Transport = Transport,
                UserAgentString = UserAgentString,
                UseShortHeaders = UseShortHeaders,
                CustomHeaders = new Dictionary<string, string>(CustomHeaders),
                PreferredCodecs = new List<string>(PreferredCodecs),
                RequireSTUN = RequireSTUN,
                STUNServer = STUNServer,
                SendPreciseTimers = SendPreciseTimers,
                DefaultPort = DefaultPort
            };
        }
    }
}