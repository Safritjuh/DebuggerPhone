using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WindowsSipPhone.Core.Interfaces;
using WindowsSipPhone.Core.Models;

namespace WindowsSipPhone.Core.SipHandlers
{
    /// <summary>
    /// Generic SIP handler for standard RFC 3261 compliant providers
    /// Implements IMP-016: Profile-Specific SIP Handling and Provider Optimization
    /// </summary>
    public class GenericProfileHandler : ISipProfileHandler
    {
        public string ProfileName => "Generic";
        
        public void ConfigureSipClient(SimpleSipClient client, SipProfileConfiguration config)
        {
            Console.WriteLine($"[GENERIC HANDLER] Configuring SIP client with RFC 3261 compliance");
            
            // Generic RFC 3261 configurations
            Console.WriteLine($"[GENERIC HANDLER] UserAgent: {config.CustomUserAgent}");
            Console.WriteLine($"[GENERIC HANDLER] StrictRFCCompliance: {config.StrictRFCCompliance}");
            Console.WriteLine($"[GENERIC HANDLER] MinimalHeaders: {config.MinimalHeaders}");
            Console.WriteLine($"[GENERIC HANDLER] PreferredTransport: {config.PreferredTransport}");
            Console.WriteLine($"[GENERIC HANDLER] RegistrationRefresh: {config.RegistrationRefreshInterval}s");
            
            // Log that we're using minimal headers
            Console.WriteLine($"[GENERIC HANDLER] Using minimal custom headers for maximum compatibility");
        }
        
        public Dictionary<string, string> GetCustomHeaders()
        {
            // Generic handler uses minimal custom headers for maximum compatibility
            return new Dictionary<string, string>();
        }
        
        public void HandleIncomingInvite(string inviteMessage)
        {
            Console.WriteLine($"[GENERIC HANDLER] Processing incoming INVITE with standard RFC 3261 logic");
            
            // Standard RFC 3261 processing only
            // No provider-specific customizations
            
            // Basic validation of required headers
            var requiredHeaders = new[] { "Via:", "From:", "To:", "Call-ID:", "CSeq:", "Max-Forwards:" };
            foreach (var header in requiredHeaders)
            {
                if (!inviteMessage.Contains(header))
                {
                    Console.WriteLine($"[GENERIC HANDLER] Warning: Missing required header {header}");
                }
            }
            
            // Standard SDP processing
            if (inviteMessage.Contains("Content-Type: application/sdp"))
            {
                Console.WriteLine($"[GENERIC HANDLER] Processing standard SDP");
            }
        }
        
        public void HandleRegistrationResponse(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[GENERIC HANDLER] Processing registration response: {statusCode}");
            
            // Standard RFC 3261 registration validation
            if (statusCode == "200")
            {
                Console.WriteLine($"[GENERIC HANDLER] Registration successful - standard processing");
                
                // Validate standard contact handling
                var contactMatch = Regex.Match(responseMessage, @"Contact:\s*(.+)", RegexOptions.IgnoreCase);
                if (contactMatch.Success)
                {
                    var contact = contactMatch.Groups[1].Value.Trim();
                    Console.WriteLine($"[GENERIC HANDLER] Contact URI: {contact}");
                }
            }
        }
        
        public void HandleIncomingResponse(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[GENERIC HANDLER] Processing incoming response: {statusCode}");
            
            // Handle standard RFC 3261 response processing
            switch (statusCode)
            {
                case "100":
                    Console.WriteLine($"[GENERIC HANDLER] Handling standard 100 Trying response");
                    break;
                    
                case "180":
                    Console.WriteLine($"[GENERIC HANDLER] Handling standard 180 Ringing response");
                    break;
                    
                case "200":
                    Console.WriteLine($"[GENERIC HANDLER] Handling standard 200 OK response");
                    break;
                    
                case "401":
                case "407":
                    Console.WriteLine($"[GENERIC HANDLER] Handling authentication challenge: {statusCode}");
                    break;
                    
                default:
                    if (statusCode.StartsWith("4") || statusCode.StartsWith("5") || statusCode.StartsWith("6"))
                    {
                        Console.WriteLine($"[GENERIC HANDLER] Handling error response: {statusCode}");
                    }
                    break;
            }
        }
        
        public bool ValidateRegistration(string responseMessage, string statusCode)
        {
            Console.WriteLine($"[GENERIC HANDLER] Validating standard RFC 3261 registration");
            
            if (statusCode != "200")
            {
                return false;
            }
            
            // Standard RFC 3261 validation rules
            bool isValid = true;
            
            // Check for required Contact header
            if (!responseMessage.Contains("Contact:"))
            {
                Console.WriteLine($"[GENERIC HANDLER] Validation failed: Missing Contact header");
                isValid = false;
            }
            
            // Validate Call-ID consistency
            if (!responseMessage.Contains("Call-ID:"))
            {
                Console.WriteLine($"[GENERIC HANDLER] Validation failed: Missing Call-ID header");
                isValid = false;
            }
            
            // Validate CSeq header
            if (!responseMessage.Contains("CSeq:"))
            {
                Console.WriteLine($"[GENERIC HANDLER] Validation failed: Missing CSeq header");
                isValid = false;
            }
            
            // Standard expires validation
            var expiresMatch = Regex.Match(responseMessage, @"Expires:\s*(\d+)", RegexOptions.IgnoreCase);
            if (expiresMatch.Success && int.TryParse(expiresMatch.Groups[1].Value, out int expires))
            {
                if (expires < 60)
                {
                    Console.WriteLine($"[GENERIC HANDLER] Warning: Very short expiry time {expires}s");
                }
                if (expires > 7200)
                {
                    Console.WriteLine($"[GENERIC HANDLER] Warning: Very long expiry time {expires}s");
                }
            }
            
            Console.WriteLine($"[GENERIC HANDLER] Registration validation result: {isValid}");
            return isValid;
        }
        
        public bool RequiresCustomRouting(string destination)
        {
            Console.WriteLine($"[GENERIC HANDLER] Standard routing for destination: {destination}");
            
            // Generic handler uses standard routing for all destinations
            return false;
        }
        
        public List<string> GetPreferredCodecs()
        {
            // Generic handler uses most widely supported codec
            return new List<string> { "G711" };
        }
        
        public string? GenerateCustomSDP(string requestMessage)
        {
            Console.WriteLine($"[GENERIC HANDLER] Using standard SDP generation");
            
            // Generic handler uses default SDP generation
            return null;
        }
        
        public string ProcessOutgoingMessage(string message, string messageType)
        {
            // Generic handler implements standard RFC 3261 processing only
            Console.WriteLine($"[GENERIC HANDLER] Processing outgoing {messageType} with RFC 3261 compliance");
            
            // No custom headers or modifications - maintain maximum compatibility
            // Just ensure the message follows RFC 3261 standards
            return message;
        }
        
        public string PreprocessIncomingMessage(string message)
        {
            // Generic handler provides standard RFC 3261 preprocessing
            Console.WriteLine($"[GENERIC HANDLER] Preprocessing incoming message with RFC 3261 standards");
            
            // Perform basic RFC 3261 validation and logging
            var lines = message.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var requestLine = lines[0];
                if (requestLine.StartsWith("SIP/2.0"))
                {
                    Console.WriteLine($"[GENERIC HANDLER] Processing SIP response: {requestLine}");
                }
                else
                {
                    var parts = requestLine.Split(' ');
                    if (parts.Length >= 3)
                    {
                        Console.WriteLine($"[GENERIC HANDLER] Processing SIP request: {parts[0]}");
                    }
                }
            }
            
            return message; // No modifications - maintain standard compliance
        }
        
        public string PostprocessOutgoingResponse(string response, string originalRequest)
        {
            // Generic handler provides standard RFC 3261 response processing
            Console.WriteLine($"[GENERIC HANDLER] Postprocessing outgoing response with RFC 3261 standards");
            
            // No custom headers or modifications for generic handler
            // Ensures maximum compatibility with any SIP provider
            return response;
        }
    }
}
