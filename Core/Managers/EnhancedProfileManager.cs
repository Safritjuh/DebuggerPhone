using System;
using System.Collections.Generic;
using System.IO;
using WindowsSipPhone.Core.Interfaces;
using WindowsSipPhone.Core.Models;
using WindowsSipPhone.Core.SipHandlers;
using WindowsSipPhone.Core.Utilities;

namespace WindowsSipPhone.Core.Managers
{    /// <summary>
    /// Enhanced Profile Manager with provider-specific SIP handling support
    /// Implements IMP-016: Profile-Specific SIP Handling and Provider Optimization
    /// </summary>
    public class EnhancedProfileManager
    {
        private readonly Dictionary<string, ISipProfileHandler> _profileHandlers;
        private readonly Dictionary<string, string> _profileNameToFileMap;
        private SipProfileConfiguration? _currentConfig;
        private ISipProfileHandler? _currentHandler;
        private SimpleSipClient? _sipClient;
          public event EventHandler<string>? ProfileChanged;
        public event EventHandler<string>? ProfileLoaded;
        public event EventHandler<string>? ProfileError;
        
        public EnhancedProfileManager()
        {
            _profileHandlers = new Dictionary<string, ISipProfileHandler>();
            _profileNameToFileMap = new Dictionary<string, string>();
            InitializeProfileHandlers();
        }
        
        /// <summary>
        /// Gets the currently active profile configuration
        /// </summary>
        public SipProfileConfiguration? CurrentConfig => _currentConfig;
        
        /// <summary>
        /// Gets the currently active profile handler
        /// </summary>
        public ISipProfileHandler? CurrentHandler => _currentHandler;
        
        /// <summary>
        /// Sets the SIP client instance to configure
        /// </summary>
        /// <param name="sipClient">The SIP client instance</param>
        public void SetSipClient(SimpleSipClient sipClient)
        {
            _sipClient = sipClient;
            Console.WriteLine($"[ENHANCED PROFILE MANAGER] SIP client set");
        }
        
        private void InitializeProfileHandlers()
        {
            Console.WriteLine($"[ENHANCED PROFILE MANAGER] Initializing profile handlers");
            
            _profileHandlers.Clear();
            _profileHandlers.Add("Avaya_Aura", new AvayaProfileHandler());
            _profileHandlers.Add("Avaya_IP_Office", new AvayaProfileHandler()); // Same handler for both Avaya types
            _profileHandlers.Add("Elevate", new ElevateProfileHandler());
            _profileHandlers.Add("Generic", new GenericProfileHandler());
            
            Console.WriteLine($"[ENHANCED PROFILE MANAGER] Initialized {_profileHandlers.Count} profile handlers");
            foreach (var handler in _profileHandlers)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] - {handler.Key}: {handler.Value.GetType().Name}");
            }
        }
          /// <summary>
        /// Loads a profile from the Profiles folder
        /// </summary>
        /// <param name="profileName">Display name of the profile (from dropdown)</param>
        public void LoadProfile(string profileName)
        {
            try
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] Loading profile: {profileName}");
                
                // Use the mapping to find the actual filename
                string actualFileName;
                if (_profileNameToFileMap.ContainsKey(profileName))
                {
                    actualFileName = _profileNameToFileMap[profileName];
                    Console.WriteLine($"[ENHANCED PROFILE MANAGER] Mapped display name '{profileName}' to file '{actualFileName}'");
                }
                else
                {
                    // Fallback to original behavior for backwards compatibility
                    actualFileName = profileName;
                    Console.WriteLine($"[ENHANCED PROFILE MANAGER] No mapping found for '{profileName}', using as filename");
                }
                
                var profilePath = Path.Combine("Profiles", $"{actualFileName}.ini");
                if (!File.Exists(profilePath))
                {
                    throw new FileNotFoundException($"Profile file not found: {profilePath}");
                }
                
                // Parse the INI file
                var iniData = IniFileHandler.ReadIniFile(profilePath);
                
                // Create configuration from INI data
                var config = SipProfileConfiguration.ParseFromIni(iniData);
                
                // Get the appropriate handler using the actual filename
                var handler = GetProfileHandler(actualFileName);
                
                if (handler != null)
                {
                    _currentHandler = handler;
                    _currentConfig = config;
                    
                    // Configure the SIP client if available
                    if (_sipClient != null)
                    {
                        handler.ConfigureSipClient(_sipClient, config);
                    }
                    
                    Console.WriteLine($"[ENHANCED PROFILE MANAGER] Profile loaded successfully: {profileName}");
                    Console.WriteLine($"[ENHANCED PROFILE MANAGER] Using handler: {handler.GetType().Name}");
                    
                    ProfileLoaded?.Invoke(this, profileName);
                    ProfileChanged?.Invoke(this, profileName);
                }
                else
                {
                    throw new InvalidOperationException($"No handler found for profile: {actualFileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] Error loading profile {profileName}: {ex.Message}");
                ProfileError?.Invoke(this, $"Failed to load profile {profileName}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Gets the appropriate handler for a profile
        /// </summary>
        /// <param name="profileName">The profile name</param>
        /// <returns>The profile handler or null if not found</returns>
        private ISipProfileHandler? GetProfileHandler(string profileName)
        {
            if (_profileHandlers.ContainsKey(profileName))
            {
                return _profileHandlers[profileName];
            }
            
            // Fallback to generic handler for unknown profiles
            Console.WriteLine($"[ENHANCED PROFILE MANAGER] No specific handler for {profileName}, using Generic handler");
            return _profileHandlers.ContainsKey("Generic") ? _profileHandlers["Generic"] : null;
        }
        
        /// <summary>
        /// Handles incoming SIP messages using the current profile handler
        /// </summary>
        /// <param name="message">The raw SIP message</param>
        /// <param name="messageType">The type of message (INVITE, REGISTER response, etc.)</param>
        public void HandleIncomingMessage(string message, string messageType)
        {
            if (_currentHandler == null)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] No profile handler loaded for incoming {messageType}");
                return;
            }
            
            Console.WriteLine($"[ENHANCED PROFILE MANAGER] Processing incoming {messageType} with {_currentHandler.ProfileName} handler");
            
            try
            {
                switch (messageType.ToUpper())
                {
                    case "INVITE":
                        _currentHandler.HandleIncomingInvite(message);
                        break;
                        
                    case "REGISTER_RESPONSE":
                        var statusCode = ExtractStatusCode(message);
                        _currentHandler.HandleRegistrationResponse(message, statusCode);
                        break;
                        
                    default:
                        var responseStatusCode = ExtractStatusCode(message);
                        _currentHandler.HandleIncomingResponse(message, responseStatusCode);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] Error handling incoming {messageType}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Processes outgoing SIP messages using the current profile handler
        /// </summary>
        /// <param name="message">The raw SIP message</param>
        /// <param name="messageType">The type of message (REGISTER, INVITE, etc.)</param>
        /// <returns>The processed message</returns>
        public string ProcessOutgoingMessage(string message, string messageType)
        {
            if (_currentHandler == null)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] No profile handler loaded for outgoing {messageType}");
                return message;
            }
            
            Console.WriteLine($"[ENHANCED PROFILE MANAGER] Processing outgoing {messageType} with {_currentHandler.ProfileName} handler");
            
            try
            {
                return _currentHandler.ProcessOutgoingMessage(message, messageType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] Error processing outgoing {messageType}: {ex.Message}");
                return message; // Return original message on error
            }
        }
        
        /// <summary>
        /// Gets custom headers from the current profile handler
        /// </summary>
        /// <returns>Dictionary of custom headers</returns>
        public Dictionary<string, string> GetActiveProfileHeaders()
        {
            return _currentHandler?.GetCustomHeaders() ?? new Dictionary<string, string>();
        }
        
        /// <summary>
        /// Validates registration using the current profile handler
        /// </summary>
        /// <param name="responseMessage">The registration response</param>
        /// <param name="statusCode">The status code</param>
        /// <returns>True if registration is valid</returns>
        public bool ValidateRegistration(string responseMessage, string statusCode)
        {
            if (_currentHandler == null)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] No profile handler loaded for registration validation");
                return statusCode == "200"; // Basic validation
            }
            
            return _currentHandler.ValidateRegistration(responseMessage, statusCode);
        }
        
        /// <summary>
        /// Gets preferred codecs from the current profile handler
        /// </summary>
        /// <returns>List of preferred codecs</returns>
        public List<string> GetPreferredCodecs()
        {
            return _currentHandler?.GetPreferredCodecs() ?? new List<string> { "G711" };
        }
        
        /// <summary>
        /// Checks if custom routing is required for a destination
        /// </summary>
        /// <param name="destination">The destination to check</param>
        /// <returns>True if custom routing is required</returns>
        public bool RequiresCustomRouting(string destination)
        {
            return _currentHandler?.RequiresCustomRouting(destination) ?? false;
        }        /// <summary>
        /// Gets available profile names from the Profiles folder
        /// </summary>
        /// <returns>List of available profile display names</returns>
        public List<string> GetAvailableProfiles()
        {
            var profiles = new List<string>();
            
            try
            {
                // Clear the mapping first
                _profileNameToFileMap.Clear();
                
                var profilesPath = "Profiles";
                if (Directory.Exists(profilesPath))
                {
                    var iniFiles = Directory.GetFiles(profilesPath, "*.ini");
                    foreach (var file in iniFiles)
                    {
                        try
                        {
                            var fileName = Path.GetFileNameWithoutExtension(file);
                            
                            // Read the actual profile name from the INI file instead of using filename
                            var data = IniFileHandler.ReadIniFile(file);
                            if (data.ContainsKey("Profile"))
                            {
                                var profileName = IniFileHandler.GetValue(data, "Profile", "Name", "");
                                if (!string.IsNullOrWhiteSpace(profileName))
                                {
                                    profiles.Add(profileName);
                                    _profileNameToFileMap[profileName] = fileName;
                                    Console.WriteLine($"[ENHANCED PROFILE MANAGER] Mapped '{profileName}' -> '{fileName}'");
                                }
                                else
                                {
                                    // Fallback to filename if name is not specified
                                    profiles.Add(fileName);
                                    _profileNameToFileMap[fileName] = fileName;
                                    Console.WriteLine($"[ENHANCED PROFILE MANAGER] Profile {file} has no name, using filename: {fileName}");
                                }
                            }
                            else
                            {
                                // Fallback to filename if Profile section is missing
                                profiles.Add(fileName);
                                _profileNameToFileMap[fileName] = fileName;
                                Console.WriteLine($"[ENHANCED PROFILE MANAGER] Profile {file} has no Profile section, using filename: {fileName}");
                            }
                        }
                        catch (Exception fileEx)
                        {
                            Console.WriteLine($"[ENHANCED PROFILE MANAGER] Error reading profile file {file}: {fileEx.Message}");
                            // Add filename as fallback for corrupted files
                            var fallbackName = Path.GetFileNameWithoutExtension(file);
                            profiles.Add(fallbackName);
                            _profileNameToFileMap[fallbackName] = fallbackName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] Error getting available profiles: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Extracts status code from SIP response message
        /// </summary>
        /// <param name="message">The SIP response message</param>
        /// <returns>The status code as string</returns>
        private string ExtractStatusCode(string message)
        {
            try
            {
                var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    var statusLine = lines[0];
                    var parts = statusLine.Split(' ');
                    if (parts.Length >= 2)
                    {
                        return parts[1];
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ENHANCED PROFILE MANAGER] Error extracting status code: {ex.Message}");
            }
            
            return "0"; // Default fallback
        }
    }
}
