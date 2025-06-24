using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WindowsSipPhone.Core.Models;
using WindowsSipPhone.Core.Validation;
using WindowsSipPhone.SipCore;

namespace WindowsSipPhone.Core.Protocol
{
    /// <summary>
    /// RFC 3261 Compliant SIP Message Factory
    /// Enhanced version that addresses all compliance issues identified in the audit
    /// </summary>
    public class EnhancedSipMessageFactory
    {
        private readonly string _localIp;
        private readonly int _localPort;
        private readonly string _userAgent;
        private readonly string _username;
        private readonly SipProfile _profile;
        private readonly Rfc3261Validator _validator;
        
        // RFC 3261 compliant User-Agent string format
        private const string DefaultUserAgent = "Windows-SIP-Phone/1.0.0 (Windows; RFC3261-Compliant)";
        
        public EnhancedSipMessageFactory(string localIp, string username, int localPort = 5060, 
            string? userAgent = null, SipProfile? profile = null)
        {
            _localIp = localIp;
            _localPort = localPort;
            _userAgent = userAgent ?? DefaultUserAgent;
            _username = username;
            _profile = profile ?? SipProfile.GetDefaultProfile();
            _validator = new Rfc3261Validator();
        }

        /// <summary>
        /// Creates a fully RFC 3261 compliant REGISTER request
        /// </summary>
        public string CreateRegisterRequest(string username, string serverHost, int serverPort, 
            uint sequenceNumber, string? authorization = null, int expires = 300)
        {
            var callId = GenerateCallId();
            var fromTag = GenerateTag();
            var branch = GenerateRfc3261Branch();
            
            var transport = _profile.Transport.ToUpper();
            var userAgent = _profile.UserAgentString;
            var profileExpires = expires == 300 ? _profile.RegistrationExpiry : expires;
            
            var message = new StringBuilder();
            
            // Start line
            message.AppendLine($"REGISTER sip:{serverHost}:{serverPort} SIP/2.0");
            
            // Mandatory headers in RFC 3261 recommended order
            message.AppendLine($"Via: SIP/2.0/{transport} {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine($"From: <sip:{username}@{serverHost}>;tag={fromTag}");
            message.AppendLine($"To: <sip:{username}@{serverHost}>");
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {sequenceNumber} REGISTER");
            message.AppendLine($"Contact: <sip:{username}@{_localIp}:{_localPort}>");
            
            // Authentication header if provided
            if (!string.IsNullOrEmpty(authorization))
            {
                message.AppendLine($"Authorization: {authorization}");
            }
            
            // RFC 3261 recommended headers for REGISTER
            message.AppendLine($"Allow: INVITE, ACK, BYE, CANCEL, OPTIONS, INFO, UPDATE, REFER");
            message.AppendLine($"Supported: replaces, timer");
            message.AppendLine($"User-Agent: {userAgent}");
            message.AppendLine($"Expires: {profileExpires}");
            
            // Add custom headers from profile
            foreach (var header in _profile.CustomHeaders)
            {
                message.AppendLine($"{header.Key}: {header.Value}");
            }
            
            // Date header (RFC 3261 recommended)
            message.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss} GMT");
            
            // Content headers
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            var sipMessage = message.ToString();
            
            // Validate the created message
            var validationResult = _validator.ValidateMessage(sipMessage);
            if (validationResult.HasCriticalErrors)
            {
                throw new InvalidOperationException(
                    $"Generated REGISTER message failed RFC 3261 validation: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            }
            
            return sipMessage;
        }

        /// <summary>
        /// Creates a fully RFC 3261 compliant INVITE request
        /// </summary>
        public string CreateInviteRequest(string username, string targetNumber, string serverHost, int serverPort,
            uint sequenceNumber, string callId, string fromTag, string? sdpContent = null)
        {
            var branch = GenerateRfc3261Branch();
            var transport = _profile.Transport.ToUpper();
            var userAgent = _profile.UserAgentString;
            
            var message = new StringBuilder();
            
            // Start line
            message.AppendLine($"INVITE sip:{targetNumber}@{serverHost}:{serverPort} SIP/2.0");
            
            // Mandatory headers in RFC 3261 recommended order
            message.AppendLine($"Via: SIP/2.0/{transport} {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine($"From: <sip:{username}@{_localIp}>;tag={fromTag}");
            message.AppendLine($"To: <sip:{targetNumber}@{serverHost}>");
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {sequenceNumber} INVITE");
            message.AppendLine($"Contact: <sip:{username}@{_localIp}:{_localPort}>");
            
            // RFC 3261 recommended headers for INVITE
            message.AppendLine($"Allow: INVITE, ACK, BYE, CANCEL, OPTIONS, INFO, UPDATE, REFER");
            message.AppendLine($"Supported: replaces, timer");
            message.AppendLine($"User-Agent: {userAgent}");
            
            // Add custom headers from profile
            foreach (var header in _profile.CustomHeaders)
            {
                message.AppendLine($"{header.Key}: {header.Value}");
            }
            
            // Date header (RFC 3261 recommended)
            message.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss} GMT");
            
            // Content headers and body
            if (!string.IsNullOrEmpty(sdpContent))
            {
                var contentLength = Encoding.UTF8.GetByteCount(sdpContent); // RFC 3261 compliant byte count
                message.AppendLine("Content-Type: application/sdp");
                message.AppendLine($"Content-Length: {contentLength}");
                message.AppendLine();
                message.Append(sdpContent);
            }
            else
            {
                message.AppendLine("Content-Length: 0");
                message.AppendLine();
            }
            
            var sipMessage = message.ToString();
            
            // Validate the created message
            var validationResult = _validator.ValidateMessage(sipMessage);
            if (validationResult.HasCriticalErrors)
            {
                throw new InvalidOperationException(
                    $"Generated INVITE message failed RFC 3261 validation: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            }
            
            return sipMessage;
        }

        /// <summary>
        /// Creates a fully RFC 3261 compliant BYE request
        /// </summary>
        public string CreateByeRequest(SipDialog dialog, uint sequenceNumber)
        {
            var branch = GenerateRfc3261Branch();
            var transport = _profile.Transport.ToUpper();
            var userAgent = _profile.UserAgentString;
            
            var requestUri = !string.IsNullOrEmpty(dialog.RemoteTarget) 
                ? dialog.RemoteTarget 
                : dialog.RemoteUri;
            
            var message = new StringBuilder();
            
            // Start line
            message.AppendLine($"BYE {requestUri} SIP/2.0");
            
            // Mandatory headers in RFC 3261 recommended order
            message.AppendLine($"Via: SIP/2.0/{transport} {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine($"From: <sip:{_username}@{_localIp}>;tag={dialog.LocalTag}");
            message.AppendLine($"To: <{dialog.RemoteUri}>;tag={dialog.RemoteTag}");
            message.AppendLine($"Call-ID: {dialog.CallId}");
            message.AppendLine($"CSeq: {sequenceNumber} BYE");
            
            // Route headers if present in dialog
            foreach (var route in dialog.RouteSet)
            {
                message.AppendLine($"Route: {route}");
            }
            
            // RFC 3261 recommended headers
            message.AppendLine($"User-Agent: {userAgent}");
            
            // Date header (RFC 3261 recommended)
            message.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss} GMT");
            
            // RFC 3261: BYE requests MUST NOT contain SDP content
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            var sipMessage = message.ToString();
            
            // Validate the created message
            var validationResult = _validator.ValidateMessage(sipMessage);
            if (validationResult.HasCriticalErrors)
            {
                throw new InvalidOperationException(
                    $"Generated BYE message failed RFC 3261 validation: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            }
            
            return sipMessage;
        }

        /// <summary>
        /// Creates a fully RFC 3261 compliant ACK request
        /// </summary>
        public string CreateAckRequest(string callId, string localTag, string remoteTag, 
            string requestUri, uint sequenceNumber, string username)
        {
            var branch = GenerateRfc3261Branch();
            var transport = _profile.Transport.ToUpper();
            var userAgent = _profile.UserAgentString;
            
            var message = new StringBuilder();
            
            // Start line
            message.AppendLine($"ACK {requestUri} SIP/2.0");
            
            // Mandatory headers in RFC 3261 recommended order
            message.AppendLine($"Via: SIP/2.0/{transport} {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine($"From: <sip:{username}@{_localIp}>;tag={localTag}");
            message.AppendLine($"To: <{requestUri}>;tag={remoteTag}");
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {sequenceNumber} ACK");
            
            // RFC 3261 recommended headers
            message.AppendLine($"User-Agent: {userAgent}");
            
            // Date header (RFC 3261 recommended)
            message.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss} GMT");
            
            // RFC 3261: ACK to 2xx responses should not contain SDP unless specifically needed
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            var sipMessage = message.ToString();
            
            // Validate the created message
            var validationResult = _validator.ValidateMessage(sipMessage);
            if (validationResult.HasCriticalErrors)
            {
                throw new InvalidOperationException(
                    $"Generated ACK message failed RFC 3261 validation: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            }
            
            return sipMessage;
        }

        /// <summary>
        /// Creates a fully RFC 3261 compliant SIP response
        /// </summary>
        public string CreateResponse(int statusCode, string reasonPhrase, string callId, 
            string localTag, string remoteTag, string via, string from, string to, string cseq, 
            string? sdpContent = null)
        {
            var userAgent = _profile.UserAgentString;
            var message = new StringBuilder();
            
            // Status line
            message.AppendLine($"SIP/2.0 {statusCode} {reasonPhrase}");
            
            // Mandatory headers - maintain order from request
            message.AppendLine($"Via: {via}");
            message.AppendLine($"From: {from}");
            
            // Add tag to To header if this is the first response in the dialog
            var toHeader = to;
            if (!to.Contains("tag=") && !string.IsNullOrEmpty(localTag))
            {
                // Add local tag to To header for responses
                if (to.EndsWith(">"))
                {
                    toHeader = to.TrimEnd('>') + $";tag={localTag}>";
                }
                else
                {
                    toHeader = to + $";tag={localTag}";
                }
            }
            message.AppendLine($"To: {toHeader}");
            
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {cseq}");
            
            // Response-specific headers
            if (statusCode >= 200 && statusCode < 300)
            {
                // 2xx responses should include Contact header
                message.AppendLine($"Contact: <sip:{_username}@{_localIp}:{_localPort}>");
            }
            
            // RFC 3261 recommended headers
            message.AppendLine($"Server: {userAgent}");
            message.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss} GMT");
            
            // Content headers and body
            if (!string.IsNullOrEmpty(sdpContent))
            {
                var contentLength = Encoding.UTF8.GetByteCount(sdpContent); // RFC 3261 compliant byte count
                message.AppendLine("Content-Type: application/sdp");
                message.AppendLine($"Content-Length: {contentLength}");
                message.AppendLine();
                message.Append(sdpContent);
            }
            else
            {
                message.AppendLine("Content-Length: 0");
                message.AppendLine();
            }
            
            var sipMessage = message.ToString();
            
            // Validate the created message
            var validationResult = _validator.ValidateMessage(sipMessage);
            if (validationResult.HasCriticalErrors)
            {
                throw new InvalidOperationException(
                    $"Generated response message failed RFC 3261 validation: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            }
            
            return sipMessage;
        }

        /// <summary>
        /// Creates a 180 Ringing response with proper RFC 3261 compliance
        /// </summary>
        public string Create180RingingResponse(string originalInvite, string localTag)
        {
            var headers = ExtractHeadersFromMessage(originalInvite);
            
            return CreateResponse(180, "Ringing", 
                headers["call-id"], localTag, "", 
                headers["via"], headers["from"], headers["to"], headers["cseq"]);
        }

        /// <summary>
        /// Creates a 200 OK response to INVITE with SDP
        /// </summary>
        public string Create200OkResponse(string originalInvite, string sdpContent, string localTag)
        {
            var headers = ExtractHeadersFromMessage(originalInvite);
            
            return CreateResponse(200, "OK", 
                headers["call-id"], localTag, "", 
                headers["via"], headers["from"], headers["to"], headers["cseq"], 
                sdpContent);
        }

        /// <summary>
        /// Creates an UPDATE request for session modification
        /// </summary>
        public string CreateUpdateRequest(SipDialog dialog, uint sequenceNumber, string? sdpContent = null)
        {
            var branch = GenerateRfc3261Branch();
            var transport = _profile.Transport.ToUpper();
            var userAgent = _profile.UserAgentString;
            
            var requestUri = !string.IsNullOrEmpty(dialog.RemoteTarget) 
                ? dialog.RemoteTarget 
                : dialog.RemoteUri;
            
            var message = new StringBuilder();
            
            // Start line
            message.AppendLine($"UPDATE {requestUri} SIP/2.0");
            
            // Mandatory headers in RFC 3261 recommended order
            message.AppendLine($"Via: SIP/2.0/{transport} {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine($"From: <sip:{_username}@{_localIp}>;tag={dialog.LocalTag}");
            message.AppendLine($"To: <{dialog.RemoteUri}>;tag={dialog.RemoteTag}");
            message.AppendLine($"Call-ID: {dialog.CallId}");
            message.AppendLine($"CSeq: {sequenceNumber} UPDATE");
            message.AppendLine($"Contact: <sip:{_username}@{_localIp}:{_localPort}>");
            
            // Route headers if present in dialog
            foreach (var route in dialog.RouteSet)
            {
                message.AppendLine($"Route: {route}");
            }
            
            // RFC 3261 recommended headers
            message.AppendLine($"User-Agent: {userAgent}");
            message.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss} GMT");
            
            // Content headers and body
            if (!string.IsNullOrEmpty(sdpContent))
            {
                var contentLength = Encoding.UTF8.GetByteCount(sdpContent);
                message.AppendLine("Content-Type: application/sdp");
                message.AppendLine($"Content-Length: {contentLength}");
                message.AppendLine();
                message.Append(sdpContent);
            }
            else
            {
                message.AppendLine("Content-Length: 0");
                message.AppendLine();
            }
            
            var sipMessage = message.ToString();
            
            // Validate the created message
            var validationResult = _validator.ValidateMessage(sipMessage);
            if (validationResult.HasCriticalErrors)
            {
                throw new InvalidOperationException(
                    $"Generated UPDATE message failed RFC 3261 validation: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            }
            
            return sipMessage;
        }

        /// <summary>
        /// Generates RFC 3261 compliant branch parameter
        /// </summary>
        private string GenerateRfc3261Branch()
        {
            // RFC 3261 Section 8.1.1.7: Branch parameter must start with "z9hG4bK"
            // followed by a unique identifier
            return $"z9hG4bK{Guid.NewGuid():N}{DateTime.UtcNow.Ticks:X}";
        }

        /// <summary>
        /// Generates RFC 3261 compliant Call-ID
        /// </summary>
        private string GenerateCallId()
        {
            // RFC 3261 Section 20.8: Call-ID should be globally unique
            return $"{Guid.NewGuid():N}@{_localIp}";
        }

        /// <summary>
        /// Generates RFC 3261 compliant tag parameter
        /// </summary>
        private string GenerateTag()
        {
            // RFC 3261 Section 19.3: Tag should be cryptographically random
            return Guid.NewGuid().ToString("N")[..16]; // 16 hex characters for good randomness
        }

        /// <summary>
        /// Extracts headers from a SIP message for response generation
        /// </summary>
        private Dictionary<string, string> ExtractHeadersFromMessage(string sipMessage)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = sipMessage.Split(new[] { "\r\n" }, StringSplitOptions.None);
            
            for (int i = 1; i < lines.Length; i++) // Skip start line
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    break; // End of headers

                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var headerName = line.Substring(0, colonIndex).Trim().ToLower();
                    var headerValue = line.Substring(colonIndex + 1).Trim();
                    headers[headerName] = headerValue;
                }
            }

            return headers;
        }

        /// <summary>
        /// Validates a SIP message using the RFC 3261 validator
        /// </summary>
        public ValidationResult ValidateMessage(string sipMessage)
        {
            return _validator.ValidateMessage(sipMessage);
        }

        /// <summary>
        /// Gets validation summary for diagnostics
        /// </summary>
        public string GetValidationSummary(string sipMessage)
        {
            var result = _validator.ValidateMessage(sipMessage);
            var summary = new StringBuilder();
            
            summary.AppendLine($"RFC 3261 Validation Summary:");
            summary.AppendLine($"Valid: {result.IsValid}");
            summary.AppendLine($"Critical Errors: {result.Errors.Count(e => e.Severity == ValidationSeverity.Critical)}");
            summary.AppendLine($"Major Issues: {result.Errors.Count(e => e.Severity == ValidationSeverity.Major)}");
            summary.AppendLine($"Warnings: {result.Errors.Count(e => e.Severity == ValidationSeverity.Warning)}");
            
            if (result.Errors.Any())
            {
                summary.AppendLine("\nIssues Found:");
                foreach (var error in result.Errors)
                {
                    summary.AppendLine($"  {error}");
                }
            }
            
            return summary.ToString();
        }

        /// <summary>
        /// Creates a fully RFC 3261 compliant BYE request
        /// </summary>
        public string CreateByeRequest(string targetUri, string callId, string fromTag, string toTag, 
            uint sequenceNumber, string? routeSet = null)
        {
            var branch = GenerateRfc3261Branch();
            var transport = _profile.Transport.ToUpper();
            var userAgent = _profile.UserAgentString;
            
            var message = new StringBuilder();
            
            // Start line - RFC 3261 Section 12.2.1.1 (Request-URI should be Contact URI from dialog)
            message.AppendLine($"BYE {targetUri} SIP/2.0");
            
            // Mandatory headers in RFC 3261 recommended order
            message.AppendLine($"Via: SIP/2.0/{transport} {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine($"From: <sip:{_username}@{_localIp}>;tag={fromTag}");
            message.AppendLine($"To: <sip:{ExtractUserFromUri(targetUri)}@{ExtractHostFromUri(targetUri)}>;tag={toTag}");
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {sequenceNumber} BYE");
            
            // Route headers if route set exists
            if (!string.IsNullOrEmpty(routeSet))
            {
                message.AppendLine($"Route: {routeSet}");
            }
            
            // RFC 3261 recommended headers
            message.AppendLine($"User-Agent: {userAgent}");
            
            // Date header (RFC 3261 recommended)
            message.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss} GMT");
            
            // RFC 3261: BYE requests MUST NOT contain SDP content
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            var sipMessage = message.ToString();
            
            // Validate the created message
            var validationResult = _validator.ValidateMessage(sipMessage);
            if (validationResult.HasCriticalErrors)
            {
                throw new InvalidOperationException(
                    $"Generated BYE message failed RFC 3261 validation: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            }
            
            return sipMessage;
        }

        /// <summary>
        /// Creates a fully RFC 3261 compliant ACK request
        /// </summary>
        public string CreateAckRequest(string targetUri, string callId, string fromTag, string toTag,
            uint sequenceNumber, string viaHeader, string? routeSet = null)
        {
            var transport = _profile.Transport.ToUpper();
            var userAgent = _profile.UserAgentString;
            
            var message = new StringBuilder();
            
            // Start line
            message.AppendLine($"ACK {targetUri} SIP/2.0");
            
            // Mandatory headers in RFC 3261 recommended order
            // RFC 3261 Section 17.1.1.3: ACK uses same Via as original INVITE
            message.AppendLine($"Via: {viaHeader}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine($"From: <sip:{_username}@{_localIp}>;tag={fromTag}");
            message.AppendLine($"To: <sip:{ExtractUserFromUri(targetUri)}@{ExtractHostFromUri(targetUri)}>;tag={toTag}");
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {sequenceNumber} ACK");
            
            // Route headers if route set exists
            if (!string.IsNullOrEmpty(routeSet))
            {
                message.AppendLine($"Route: {routeSet}");
            }
            
            // RFC 3261 recommended headers
            message.AppendLine($"User-Agent: {userAgent}");
            
            // Content headers
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            return message.ToString();
        }

        /// <summary>
        /// Creates RFC 3261 compliant response messages
        /// </summary>
        public string CreateResponse(int statusCode, string reasonPhrase, string requestMessage, 
            string? sdpContent = null, Dictionary<string, string>? additionalHeaders = null)
        {
            var message = new StringBuilder();
            
            // Status line
            message.AppendLine($"SIP/2.0 {statusCode} {reasonPhrase}");
            
            // Copy mandatory headers from request (RFC 3261 Section 8.2.6)
            var via = ExtractHeaderValue(requestMessage, "Via");
            var from = ExtractHeaderValue(requestMessage, "From");
            var to = ExtractHeaderValue(requestMessage, "To");
            var callId = ExtractHeaderValue(requestMessage, "Call-ID");
            var cseq = ExtractHeaderValue(requestMessage, "CSeq");
            
            // Add local tag to To header if not present and this is a final response
            if (statusCode >= 200 && !to.Contains("tag="))
            {
                var localTag = GenerateTag();
                to = to.TrimEnd('>') + $";tag={localTag}>";
            }
            
            // Mandatory response headers in RFC 3261 recommended order
            message.AppendLine($"Via: {via}");
            message.AppendLine($"From: {from}");
            message.AppendLine($"To: {to}");
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {cseq}");
            
            // Contact header for dialog-creating responses
            if (statusCode >= 200 && statusCode < 300)
            {
                message.AppendLine($"Contact: <sip:{_username}@{_localIp}:{_localPort}>");
            }
            
            // Additional headers
            if (additionalHeaders != null)
            {
                foreach (var header in additionalHeaders)
                {
                    message.AppendLine($"{header.Key}: {header.Value}");
                }
            }
            
            // Server header
            message.AppendLine($"Server: {_userAgent}");
            
            // Date header
            message.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss} GMT");
            
            // Content headers and body
            if (!string.IsNullOrEmpty(sdpContent))
            {
                var contentLength = Encoding.UTF8.GetByteCount(sdpContent);
                message.AppendLine("Content-Type: application/sdp");
                message.AppendLine($"Content-Length: {contentLength}");
                message.AppendLine();
                message.Append(sdpContent);
            }
            else
            {
                message.AppendLine("Content-Length: 0");
                message.AppendLine();
            }
            
            return message.ToString();
        }

        // Helper methods for URI manipulation
        private string ExtractUserFromUri(string uri)
        {
            if (uri.StartsWith("sip:") || uri.StartsWith("sips:"))
            {
                var userHost = uri.Split(':')[1];
                var atIndex = userHost.IndexOf('@');
                return atIndex > 0 ? userHost.Substring(0, atIndex) : userHost;
            }
            return "unknown";
        }
        
        private string ExtractHostFromUri(string uri)
        {
            if (uri.StartsWith("sip:") || uri.StartsWith("sips:"))
            {
                var userHost = uri.Split(':', 2)[1];
                var atIndex = userHost.IndexOf('@');
                if (atIndex >= 0)
                {
                    var hostPort = userHost.Substring(atIndex + 1);
                    var portIndex = hostPort.IndexOf(':');
                    return portIndex > 0 ? hostPort.Substring(0, portIndex) : hostPort;
                }
                return userHost;
            }
            return "unknown";
        }
        
        private string ExtractHeaderValue(string sipMessage, string headerName)
        {
            var lines = sipMessage.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase))
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex + 1 < line.Length)
                    {
                        return line.Substring(colonIndex + 1).Trim();
                    }
                }
            }
            return string.Empty;
        }
    }
}
