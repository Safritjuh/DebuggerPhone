using System.Collections.Generic;

namespace WindowsSipPhone.Core.Models
{
    /// <summary>
    /// Enhanced SIP profile configuration with provider-specific settings
    /// Implements IMP-016: Profile-Specific SIP Handling and Provider Optimization
    /// </summary>
    public class SipProfileConfiguration
    {
        // Basic SIP settings (existing compatibility)
        public string ServerAddress { get; set; } = "";
        public int Port { get; set; } = 5060;
        public string Protocol { get; set; } = "TCP";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        
        // Enhanced SIP handling settings
        public bool RequiresCustomAuth { get; set; } = false;
        public bool SupportsRefer { get; set; } = true;
        public bool CustomContactHeader { get; set; } = false;
        public bool RequiresFromTag { get; set; } = false;
        public string CustomUserAgent { get; set; } = "SIP-Phone/1.0";
        public List<string> SupportedCodecs { get; set; } = new List<string> { "G711" };
        public Dictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>();
        public int RegistrationRefreshInterval { get; set; } = 3600;
        public string PreferredTransport { get; set; } = "TCP";
        public bool RequiresPrack { get; set; } = false;
        public bool SupportsUpdate { get; set; } = false;
        public int MaxForwards { get; set; } = 70;
        public int SessionTimers { get; set; } = 1800;
        
        // Advanced settings
        public bool EnableSessionProgress { get; set; } = false;
        public bool RequiresReliableProvisional { get; set; } = false;
        public bool EnableICE { get; set; } = false;
        public bool EnableSTUN { get; set; } = false;
        public bool EnableWebRTC { get; set; } = false;
        public bool StrictRFCCompliance { get; set; } = false;
        public bool MinimalHeaders { get; set; } = false;
        
        /// <summary>
        /// Create configuration from existing SipProfile for backward compatibility
        /// </summary>
        /// <param name="profile">The existing SipProfile</param>
        /// <returns>SipProfileConfiguration with converted settings</returns>
        public static SipProfileConfiguration FromSipProfile(SipProfile profile)
        {
            var config = new SipProfileConfiguration
            {
                CustomUserAgent = profile.UserAgentString,
                SupportedCodecs = new List<string>(profile.PreferredCodecs),
                CustomHeaders = new Dictionary<string, string>(profile.CustomHeaders),
                RegistrationRefreshInterval = profile.RegistrationExpiry,
                PreferredTransport = profile.Transport,
                Port = profile.DefaultPort,
                EnableSTUN = profile.RequireSTUN
            };
            
            return config;
        }
        
        /// <summary>
        /// Parse enhanced configuration from INI file data
        /// </summary>
        /// <param name="iniData">Dictionary containing INI file sections and keys</param>
        /// <returns>SipProfileConfiguration with parsed settings</returns>
        public static SipProfileConfiguration ParseFromIni(Dictionary<string, Dictionary<string, string>> iniData)
        {
            var config = new SipProfileConfiguration();
            
            // Parse basic SIP settings
            if (iniData.ContainsKey("SIP"))
            {
                var sipSection = iniData["SIP"];
                config.ServerAddress = GetValue(sipSection, "ServerAddress", "");
                config.Port = GetIntValue(sipSection, "Port", 5060);
                config.Protocol = GetValue(sipSection, "Protocol", "TCP");
                config.Username = GetValue(sipSection, "Username", "");
                config.Password = GetValue(sipSection, "Password", "");
            }
            
            // Parse enhanced SIP handling settings
            if (iniData.ContainsKey("SIPHandling"))
            {
                var handlingSection = iniData["SIPHandling"];
                config.RequiresCustomAuth = GetBoolValue(handlingSection, "RequiresCustomAuth", false);
                config.SupportsRefer = GetBoolValue(handlingSection, "SupportsRefer", true);
                config.CustomContactHeader = GetBoolValue(handlingSection, "CustomContactHeader", false);
                config.RequiresFromTag = GetBoolValue(handlingSection, "RequiresFromTag", false);
                config.CustomUserAgent = GetValue(handlingSection, "CustomUserAgent", "SIP-Phone/1.0");
                config.RegistrationRefreshInterval = GetIntValue(handlingSection, "RegistrationRefreshInterval", 3600);
                config.PreferredTransport = GetValue(handlingSection, "PreferredTransport", "TCP");
                config.RequiresPrack = GetBoolValue(handlingSection, "RequiresPrack", false);
                config.SupportsUpdate = GetBoolValue(handlingSection, "SupportsUpdate", false);
                config.MaxForwards = GetIntValue(handlingSection, "MaxForwards", 70);
                config.SessionTimers = GetIntValue(handlingSection, "SessionTimers", 1800);
                config.EnableSessionProgress = GetBoolValue(handlingSection, "EnableSessionProgress", false);
                config.RequiresReliableProvisional = GetBoolValue(handlingSection, "RequiresReliableProvisional", false);
                config.EnableICE = GetBoolValue(handlingSection, "EnableICE", false);
                config.EnableSTUN = GetBoolValue(handlingSection, "EnableSTUN", false);
                config.EnableWebRTC = GetBoolValue(handlingSection, "EnableWebRTC", false);
                config.StrictRFCCompliance = GetBoolValue(handlingSection, "StrictRFCCompliance", false);
                config.MinimalHeaders = GetBoolValue(handlingSection, "MinimalHeaders", false);
                
                // Parse codecs list
                var codecsValue = GetValue(handlingSection, "SupportedCodecs", "G711");
                if (!string.IsNullOrEmpty(codecsValue))
                {
                    config.SupportedCodecs = new List<string>(codecsValue.Split(','));
                }
                
                // Parse custom headers
                var headersValue = GetValue(handlingSection, "CustomHeaders", "");
                if (!string.IsNullOrEmpty(headersValue))
                {
                    var headerNames = headersValue.Split(',');
                    config.CustomHeaders = new Dictionary<string, string>();
                    foreach (var headerName in headerNames)
                    {
                        if (!string.IsNullOrWhiteSpace(headerName))
                        {
                            config.CustomHeaders[headerName.Trim()] = "";
                        }
                    }
                }
            }
            
            return config;
        }
        
        private static string GetValue(Dictionary<string, string> section, string key, string defaultValue)
        {
            return section.ContainsKey(key) ? section[key] : defaultValue;
        }
        
        private static int GetIntValue(Dictionary<string, string> section, string key, int defaultValue)
        {
            if (section.ContainsKey(key) && int.TryParse(section[key], out int value))
            {
                return value;
            }
            return defaultValue;
        }
        
        private static bool GetBoolValue(Dictionary<string, string> section, string key, bool defaultValue)
        {
            if (section.ContainsKey(key) && bool.TryParse(section[key], out bool value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
