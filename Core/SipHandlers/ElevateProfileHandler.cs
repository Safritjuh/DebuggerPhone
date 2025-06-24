using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WindowsSipPhone.Core.Interfaces;
using WindowsSipPhone.Core.Models;

namespace WindowsSipPhone.Core.SipHandlers
{
    /// <summary>
    /// Provider-specific SIP handler for Elevate Communications (cloud-based)
    /// Implements IMP-016: Profile-Specific SIP Handling and Provider Optimization
    /// </summary>
    public class ElevateProfileHandler : ISipProfileHandler
    {
        public string ProfileName => "Elevate";
        
        public void ConfigureSipClient(SimpleSipClient client, SipProfileConfiguration config)
        {
            Console.WriteLine($"[ELEVATE HANDLER] Configuring SIP client with Elevate-specific settings");
            
            // Elevate-specific configurations for cloud environment
            Console.WriteLine($"[ELEVATE HANDLER] UserAgent: {config.CustomUserAgent}");
            Console.WriteLine($"[ELEVATE HANDLER] PreferredTransport: {config.PreferredTransport} (cloud-optimized)");
            Console.WriteLine($"[ELEVATE HANDLER] RegistrationRefresh: {config.RegistrationRefreshInterval}s (shorter for cloud)");
            Console.WriteLine($"[ELEVATE HANDLER] EnableICE: {config.EnableICE}");
            Console.WriteLine($"[ELEVATE HANDLER] EnableSTUN: {config.EnableSTUN}");
            Console.WriteLine($"[ELEVATE HANDLER] EnableWebRTC: {config.EnableWebRTC}");
            
            // Log Elevate-specific headers that will be added
            var customHeaders = GetCustomHeaders();
            foreach (var header in customHeaders)
            {
                Console.WriteLine($"[ELEVATE HANDLER] Custom Header: {header.Key} = {header.Value}");
            }
        }
        
        public Dictionary<string, string> GetCustomHeaders()
        {
            return new Dictionary<string, string>
            {
                { "X-Elevate-Client-Version", "1.0" },
                { "X-Elevate-Platform", "Desktop" },
                { "X-Elevate-Network-Type", "broadband" },
                { "X-Elevate-Session-Type", "voice" }
            };
        }
        
        public void HandleIncomingInvite(string inviteMessage)
        {
            Console.WriteLine($"[ELEVATE HANDLER] Processing incoming INVITE with Elevate cloud-specific logic");
            
            // Check for Elevate-specific headers
            if (inviteMessage.Contains("X-Elevate-Call-ID:"))
            {
                var callIdMatch = Regex.Match(inviteMessage, @"X-Elevate-Call-ID:\s*(.+)", RegexOptions.IgnoreCase);
                if (callIdMatch.Success)
                {
                    var elevateCallId = callIdMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"[ELEVATE HANDLER] Found Elevate Call ID: {elevateCallId}");
                }
            }
            
            // Handle WebRTC-style SDP processing
            if (inviteMessage.Contains("Content-Type: application/sdp"))
            {
                Console.WriteLine($"[ELEVATE HANDLER] Processing Elevate WebRTC-compatible SDP");
                
                // Check for ICE candidates
                if (inviteMessage.Contains("a=candidate:"))
                {
                    Console.WriteLine($"[ELEVATE HANDLER] ICE candidates detected in SDP");
                }
                
                // Check for DTLS fingerprint
                if (inviteMessage.Contains("a=fingerprint:"))
                {
                    Console.WriteLine($"[ELEVATE HANDLER] DTLS fingerprint detected for secure media");
                }
            }
        }
        
        public void HandleRegistrationResponse(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[ELEVATE HANDLER] Processing registration response: {statusCode}");
            
            // Elevate cloud-specific registration validation
            if (statusCode == "200")
            {
                // Check for Elevate cloud headers
                if (responseMessage.Contains("X-Elevate-Server:"))
                {
                    var serverMatch = Regex.Match(responseMessage, @"X-Elevate-Server:\s*(.+)", RegexOptions.IgnoreCase);
                    if (serverMatch.Success)
                    {
                        var server = serverMatch.Groups[1].Value.Trim();
                        Console.WriteLine($"[ELEVATE HANDLER] Elevate server: {server}");
                    }
                }
                
                // Validate cloud contact handling
                var contactMatch = Regex.Match(responseMessage, @"Contact:\s*(.+)", RegexOptions.IgnoreCase);
                if (contactMatch.Success)
                {
                    var contact = contactMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"[ELEVATE HANDLER] Cloud Contact URI: {contact}");
                }
            }
        }
        
        public void HandleIncomingResponse(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[ELEVATE HANDLER] Processing incoming response: {statusCode}");
            
            // Handle Elevate cloud-specific response processing
            switch (statusCode)
            {
                case "100":
                    Console.WriteLine($"[ELEVATE HANDLER] Handling Elevate cloud 100 Trying response");
                    break;
                    
                case "180":
                    Console.WriteLine($"[ELEVATE HANDLER] Handling Elevate cloud 180 Ringing response");
                    // Check for early media
                    if (responseMessage.Contains("Content-Type: application/sdp"))
                    {
                        Console.WriteLine($"[ELEVATE HANDLER] Early media detected from cloud");
                    }
                    break;
                    
                case "200":
                    Console.WriteLine($"[ELEVATE HANDLER] Handling Elevate cloud 200 OK response");
                    break;
            }
        }
        
        public bool ValidateRegistration(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[ELEVATE HANDLER] Validating Elevate cloud registration");
            
            if (statusCode != "200")
            {
                return false;
            }
            
            // Elevate cloud-specific validation rules
            bool isValid = true;
            
            // Check for required Contact header
            if (!responseMessage.Contains("Contact:"))
            {
                Console.WriteLine($"[ELEVATE HANDLER] Validation failed: Missing Contact header");
                isValid = false;
            }
            
            // Validate shorter expires header for cloud environment
            var expiresMatch = Regex.Match(responseMessage, @"Expires:\s*(\d+)", RegexOptions.IgnoreCase);
            if (expiresMatch.Success && int.TryParse(expiresMatch.Groups[1].Value, out int expires))
            {
                if (expires > 600)
                {
                    Console.WriteLine($"[ELEVATE HANDLER] Warning: Long expiry time {expires}s for cloud service");
                }
                if (expires < 60)
                {
                    Console.WriteLine($"[ELEVATE HANDLER] Warning: Very short expiry time {expires}s");
                }
            }
            
            Console.WriteLine($"[ELEVATE HANDLER] Registration validation result: {isValid}");
            return isValid;
        }
        
        public bool RequiresCustomRouting(string destination)
        {
            Console.WriteLine($"[ELEVATE HANDLER] Checking if custom cloud routing required for: {destination}");
            
            // Elevate cloud might use different routing logic
            bool requiresCustom = false;
            
            // Check for international numbers that might need cloud routing
            if (destination.StartsWith("+") || destination.StartsWith("00"))
            {
                Console.WriteLine($"[ELEVATE HANDLER] International number detected: {destination}");
                requiresCustom = true;
            }
            
            // Check for extension patterns
            if (destination.Length >= 3 && destination.Length <= 4 && destination.All(char.IsDigit))
            {
                Console.WriteLine($"[ELEVATE HANDLER] Extension number detected: {destination}");
                requiresCustom = true;
            }
            
            return requiresCustom;
        }
        
        public List<string> GetPreferredCodecs()
        {
            // Elevate cloud typically supports these codecs for better quality over internet
            return new List<string> { "Opus", "G722", "G711" };
        }
        
        public string? GenerateCustomSDP(string requestMessage)
        {
            Console.WriteLine($"[ELEVATE HANDLER] Generating Elevate cloud-optimized SDP");
            
            // Return null for now - use default SDP generation
            // In the future, we could add Elevate-specific SDP attributes here like:
            // - ICE candidates for NAT traversal
            // - DTLS fingerprints for security
            // - Opus codec preferences
            return null;
        }
        
        public string ProcessOutgoingMessage(string message, string messageType)
        {
            Console.WriteLine($"[ELEVATE HANDLER] Processing outgoing {messageType} message for cloud");
            
            // Add Elevate-specific headers to outgoing messages
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
                        Console.WriteLine($"[ELEVATE HANDLER] Added cloud header: {header.Key}: {header.Value}");
                    }
                }
                
                // For INVITE messages, add cloud-specific optimizations
                if (messageType == "INVITE")
                {
                    // Add supported header for cloud features
                    if (!headers.Contains("Supported:"))
                    {
                        headers += "\r\nSupported: replaces, timer, path";
                        Console.WriteLine($"[ELEVATE HANDLER] Added cloud Supported header");
                    }
                }
                
                modifiedMessage = headers + body;
            }
            
            return modifiedMessage;
        }
    }
}
