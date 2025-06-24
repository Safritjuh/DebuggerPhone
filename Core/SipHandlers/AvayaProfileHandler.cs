using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WindowsSipPhone.Core.Interfaces;
using WindowsSipPhone.Core.Models;

namespace WindowsSipPhone.Core.SipHandlers
{
    /// <summary>
    /// Provider-specific SIP handler for Avaya systems (Aura, IP Office)
    /// Implements IMP-016: Profile-Specific SIP Handling and Provider Optimization
    /// </summary>
    public class AvayaProfileHandler : ISipProfileHandler
    {
        public string ProfileName => "Avaya";
        
        public void ConfigureSipClient(SimpleSipClient client, SipProfileConfiguration config)
        {
            Console.WriteLine($"[AVAYA HANDLER] Configuring SIP client with Avaya-specific settings");
            
            // Avaya-specific configurations
            // Note: We'll need to add these properties to SimpleSipClient in integration phase
            // For now, we're preparing the configuration logic
            
            Console.WriteLine($"[AVAYA HANDLER] UserAgent: {config.CustomUserAgent}");
            Console.WriteLine($"[AVAYA HANDLER] MaxForwards: {config.MaxForwards}");
            Console.WriteLine($"[AVAYA HANDLER] RegistrationRefresh: {config.RegistrationRefreshInterval}s");
            Console.WriteLine($"[AVAYA HANDLER] PreferredTransport: {config.PreferredTransport}");
            Console.WriteLine($"[AVAYA HANDLER] RequiresPrack: {config.RequiresPrack}");
            Console.WriteLine($"[AVAYA HANDLER] SupportsUpdate: {config.SupportsUpdate}");
            
            // Log Avaya-specific headers that will be added
            var customHeaders = GetCustomHeaders();
            foreach (var header in customHeaders)
            {
                Console.WriteLine($"[AVAYA HANDLER] Custom Header: {header.Key} = {header.Value}");
            }
        }
        
        public Dictionary<string, string> GetCustomHeaders()
        {
            return new Dictionary<string, string>
            {
                { "X-Avaya-Session-ID", Guid.NewGuid().ToString() },
                { "X-Avaya-Conference-ID", "none" },
                { "P-Access-Network-Info", "IEEE-802.11" },
                { "X-Avaya-Client-Type", "Desktop-SIP-Phone" }
            };
        }
        
        public void HandleIncomingInvite(string inviteMessage)
        {
            Console.WriteLine($"[AVAYA HANDLER] Processing incoming INVITE with Avaya-specific logic");
            
            // Check for Avaya-specific headers
            if (inviteMessage.Contains("X-Avaya-Session-ID:"))
            {
                var sessionIdMatch = Regex.Match(inviteMessage, @"X-Avaya-Session-ID:\s*(.+)", RegexOptions.IgnoreCase);
                if (sessionIdMatch.Success)
                {
                    var sessionId = sessionIdMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"[AVAYA HANDLER] Found Avaya Session ID: {sessionId}");
                }
            }
            
            if (inviteMessage.Contains("X-Avaya-Conference-ID:"))
            {
                var conferenceIdMatch = Regex.Match(inviteMessage, @"X-Avaya-Conference-ID:\s*(.+)", RegexOptions.IgnoreCase);
                if (conferenceIdMatch.Success)
                {
                    var conferenceId = conferenceIdMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"[AVAYA HANDLER] Found Avaya Conference ID: {conferenceId}");
                }
            }
            
            // Handle Avaya proprietary SDP attributes
            if (inviteMessage.Contains("Content-Type: application/sdp"))
            {
                Console.WriteLine($"[AVAYA HANDLER] Processing Avaya SDP attributes");
                // TODO: Parse and handle Avaya-specific SDP attributes
            }
        }
        
        public void HandleRegistrationResponse(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[AVAYA HANDLER] Processing registration response: {statusCode}");
            
            // Avaya-specific registration validation
            if (statusCode == "200")
            {
                // Check for Avaya-specific registration headers
                if (responseMessage.Contains("X-Avaya-Registration:"))
                {
                    Console.WriteLine($"[AVAYA HANDLER] Avaya registration header found");
                }
                
                // Validate Avaya contact handling
                var contactMatch = Regex.Match(responseMessage, @"Contact:\s*(.+)", RegexOptions.IgnoreCase);
                if (contactMatch.Success)
                {
                    var contact = contactMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"[AVAYA HANDLER] Contact URI: {contact}");
                }
            }
        }
        
        public void HandleIncomingResponse(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[AVAYA HANDLER] Processing incoming response: {statusCode}");
            
            // Handle Avaya-specific response processing
            switch (statusCode)
            {
                case "100":
                    Console.WriteLine($"[AVAYA HANDLER] Handling Avaya 100 Trying response");
                    break;
                    
                case "180":
                    Console.WriteLine($"[AVAYA HANDLER] Handling Avaya 180 Ringing response");
                    break;
                    
                case "200":
                    Console.WriteLine($"[AVAYA HANDLER] Handling Avaya 200 OK response");
                    break;
            }
        }
        
        public bool ValidateRegistration(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[AVAYA HANDLER] Validating Avaya registration");
            
            if (statusCode != "200")
            {
                return false;
            }
            
            // Avaya-specific validation rules
            bool isValid = true;
            
            // Check for required Avaya headers or contact formats
            if (!responseMessage.Contains("Contact:"))
            {
                Console.WriteLine($"[AVAYA HANDLER] Validation failed: Missing Contact header");
                isValid = false;
            }
            
            // Validate expires header for Avaya
            var expiresMatch = Regex.Match(responseMessage, @"Expires:\s*(\d+)", RegexOptions.IgnoreCase);
            if (expiresMatch.Success && int.TryParse(expiresMatch.Groups[1].Value, out int expires))
            {
                if (expires < 300)
                {
                    Console.WriteLine($"[AVAYA HANDLER] Warning: Short expiry time {expires}s");
                }
            }
            
            Console.WriteLine($"[AVAYA HANDLER] Registration validation result: {isValid}");
            return isValid;
        }
        
        public bool RequiresCustomRouting(string destination)
        {
            Console.WriteLine($"[AVAYA HANDLER] Checking if custom routing required for: {destination}");
            
            // Avaya might require special routing for certain patterns
            // Example: Internal extensions vs external numbers
            bool requiresCustom = false;
            
            if (destination.Length <= 4 && destination.All(char.IsDigit))
            {
                // Internal extension - might need special handling
                Console.WriteLine($"[AVAYA HANDLER] Internal extension detected: {destination}");
                requiresCustom = true;
            }
            
            return requiresCustom;
        }
        
        public List<string> GetPreferredCodecs()
        {
            // Avaya typically supports these codecs in this priority order
            return new List<string> { "G711", "G722", "G729" };
        }
        
        public string? GenerateCustomSDP(string requestMessage)
        {
            Console.WriteLine($"[AVAYA HANDLER] Generating Avaya-specific SDP");
            
            // Return null for now - use default SDP generation
            // In the future, we could add Avaya-specific SDP attributes here
            return null;
        }
        
        public string ProcessOutgoingMessage(string message, string messageType)
        {
            Console.WriteLine($"[AVAYA HANDLER] Processing outgoing {messageType} message");
            
            // Add Avaya-specific headers to outgoing messages
            var customHeaders = GetCustomHeaders();
            var modifiedMessage = message;
            
            // Find the end of the headers (before the body)
            var headerEndIndex = modifiedMessage.IndexOf("\r\n\r\n");
            if (headerEndIndex >= 0)
            {
                var headers = modifiedMessage.Substring(0, headerEndIndex);
                var body = modifiedMessage.Substring(headerEndIndex);
                
                // Add custom headers before the body
                foreach (var header in customHeaders)
                {
                    if (!headers.Contains($"{header.Key}:"))
                    {
                        headers += $"\r\n{header.Key}: {header.Value}";
                        Console.WriteLine($"[AVAYA HANDLER] Added header: {header.Key}: {header.Value}");
                    }
                }
                  modifiedMessage = headers + body;
            }
            
            return modifiedMessage;
        }
    }
}
