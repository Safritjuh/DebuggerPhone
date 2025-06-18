using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsSipPhone.Models
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
        /// Gets all predefined SIP profiles
        /// </summary>
        public static List<SipProfile> GetPredefinedProfiles()
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
                
                // Avaya IP Office optimized
                new SipProfile
                {
                    Name = "Avaya IP Office",
                    Description = "Optimized for Avaya IP Office systems with longer registration intervals",
                    IsCustom = false,
                    RegistrationExpiry = 3600,
                    RequireKeepAlive = false,
                    Transport = "TCP",
                    UserAgentString = "Windows-SIP-Phone/2.0 (Avaya)",
                    UseShortHeaders = false,
                    PreferredCodecs = new List<string> { "PCMU", "PCMA" },
                    RequireSTUN = false,
                    SendPreciseTimers = true,
                    DefaultPort = 5060
                },
                
                // Cloud Generic (shorter registration, keep-alive)
                new SipProfile
                {
                    Name = "Cloud Generic",
                    Description = "Optimized for cloud SIP providers with keep-alive and shorter registration",
                    IsCustom = false,
                    RegistrationExpiry = 300,
                    RequireKeepAlive = true,
                    KeepAliveInterval = 30,
                    Transport = "TCP",
                    UserAgentString = "Windows-SIP-Phone/2.0 (Cloud)",
                    UseShortHeaders = false,
                    PreferredCodecs = new List<string> { "G722", "PCMU", "PCMA" },
                    RequireSTUN = false,
                    SendPreciseTimers = true,
                    DefaultPort = 5060
                },
                
                // FreeSWITCH optimized
                new SipProfile
                {
                    Name = "FreeSWITCH",
                    Description = "Optimized for FreeSWITCH platforms with flexible timers",
                    IsCustom = false,
                    RegistrationExpiry = 1800,
                    RequireKeepAlive = false,
                    Transport = "TCP",
                    UserAgentString = "Windows-SIP-Phone/2.0 (FreeSWITCH)",
                    UseShortHeaders = false,
                    PreferredCodecs = new List<string> { "G722", "PCMU", "PCMA" },
                    RequireSTUN = false,
                    SendPreciseTimers = true,
                    DefaultPort = 5060
                },
                
                // Cisco optimized
                new SipProfile
                {
                    Name = "Cisco",
                    Description = "Optimized for Cisco systems with platform-specific settings",
                    IsCustom = false,
                    RegistrationExpiry = 3600,
                    RequireKeepAlive = false,
                    Transport = "TCP",
                    UserAgentString = "Windows-SIP-Phone/2.0 (Cisco)",
                    UseShortHeaders = false,
                    PreferredCodecs = new List<string> { "G729", "PCMU", "PCMA" },
                    RequireSTUN = false,
                    SendPreciseTimers = true,
                    DefaultPort = 5060
                }
            };
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
        
        /// <summary>
        /// Creates a copy of this profile
        /// </summary>
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