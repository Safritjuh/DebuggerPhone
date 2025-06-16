using System;
using System.Linq;
using System.Text;

namespace WindowsSipPhone.SipCore
{    /// <summary>
    /// JSIP-style message factory for creating SIP messages with proper validation
    /// </summary>
    public class SipMessageFactory
    {
        private readonly string _localIp;
        private readonly int _localPort;
        private readonly string _userAgent;
        private readonly string _username;
        
        public SipMessageFactory(string localIp, string username, int localPort = 5060, string userAgent = "Windows-SIP-Phone/1.0")
        {
            _localIp = localIp;
            _localPort = localPort;
            _userAgent = userAgent;
            _username = username;
        }
        
        /// <summary>
        /// Creates a REGISTER request
        /// </summary>
        public string CreateRegisterRequest(string username, string serverHost, int serverPort, 
            uint sequenceNumber, string? authorization = null, int expires = 300)
        {
            var callId = GenerateCallId();
            var fromTag = GenerateTag();
            var branch = GenerateBranch();
            
            var message = new StringBuilder();
            message.AppendLine($"REGISTER sip:{serverHost}:{serverPort} SIP/2.0");
            message.AppendLine($"Via: SIP/2.0/TCP {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"From: <sip:{username}@{serverHost}>;tag={fromTag}");
            message.AppendLine($"To: <sip:{username}@{serverHost}>");
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {sequenceNumber} REGISTER");
            message.AppendLine($"Contact: <sip:{username}@{_localIp}:{_localPort}>");
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine($"Expires: {expires}");
            
            if (!string.IsNullOrEmpty(authorization))
            {
                message.AppendLine($"Authorization: {authorization}");
            }
            
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            return message.ToString();
        }
        
        /// <summary>
        /// Creates an INVITE request
        /// </summary>
        public string CreateInviteRequest(string username, string targetNumber, string serverHost, int serverPort,
            uint sequenceNumber, string callId, string fromTag, string? sdpContent = null)
        {
            var branch = GenerateBranch();
            var contentLength = string.IsNullOrEmpty(sdpContent) ? 0 : Encoding.UTF8.GetByteCount(sdpContent);
              var message = new StringBuilder();
            message.AppendLine($"INVITE sip:{targetNumber}@{serverHost}:{serverPort} SIP/2.0");
            message.AppendLine($"Via: SIP/2.0/TCP {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"From: <sip:{username}@{_localIp}>;tag={fromTag}");
            message.AppendLine($"To: <sip:{targetNumber}@{serverHost}>");
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {sequenceNumber} INVITE");
            message.AppendLine($"Contact: <sip:{username}@{_localIp}:{_localPort}>");
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine($"Max-Forwards: 70");
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message.AppendLine("Content-Type: application/sdp");
            }
            
            message.AppendLine($"Content-Length: {contentLength}");
            message.AppendLine();
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message.Append(sdpContent);
            }
            
            return message.ToString();
        }          /// <summary>
        /// Creates a BYE request
        /// RFC 3261 Compliant: BYE requests MUST NOT contain SDP content
        /// </summary>
        public string CreateByeRequest(SipDialog dialog, uint sequenceNumber)
        {
            var branch = GenerateBranch();
            var requestUri = !string.IsNullOrEmpty(dialog.RemoteTarget) ? dialog.RemoteTarget : dialog.RemoteUri;
            
            var message = new StringBuilder();
            message.AppendLine($"BYE {requestUri} SIP/2.0");
            message.AppendLine($"Via: SIP/2.0/TCP {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"From: <{dialog.LocalUri}>;tag={dialog.LocalTag}");
            message.AppendLine($"To: <{dialog.RemoteUri}>;tag={dialog.RemoteTag}");
            message.AppendLine($"Call-ID: {dialog.CallId}");
            message.AppendLine($"CSeq: {sequenceNumber} BYE");
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            return message.ToString();
        }
        
        /// <summary>
        /// Creates an ACK request
        /// </summary>
        public string CreateAckRequest(SipDialog dialog, uint sequenceNumber, string requestUri)
        {
            var branch = GenerateBranch();
            
            var message = new StringBuilder();
            message.AppendLine($"ACK {requestUri} SIP/2.0");
            message.AppendLine($"Via: SIP/2.0/TCP {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"From: <{dialog.LocalUri}>;tag={dialog.LocalTag}");
            message.AppendLine($"To: <{dialog.RemoteUri}>;tag={dialog.RemoteTag}");
            message.AppendLine($"Call-ID: {dialog.CallId}");
            message.AppendLine($"CSeq: {sequenceNumber} ACK");
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            return message.ToString();
        }        /// <summary>
        /// Creates an ACK request with simpler signature for JSIP integration  
        /// </summary>
        public string CreateAckRequest(string callId, string localTag, string remoteTag, string requestUri, uint sequenceNumber, string username = "")
        {
            var branch = GenerateBranch();
              // Add transport parameter to Request-URI for TCP compliance
            var ackRequestUri = requestUri;
            if (!ackRequestUri.Contains("transport=tcp"))
            {
                ackRequestUri = requestUri.Contains(";") 
                    ? $"{requestUri};transport=tcp" 
                    : $"{requestUri};transport=tcp";
            }
            
            // Extract target user and server from Request-URI for proper To header
            var targetUser = ExtractUserFromRequestUri(requestUri);
            var targetServer = ExtractServerFromRequestUri(requestUri);
            var toHeaderUri = $"{targetUser}@{targetServer}";
            
            var message = new StringBuilder();
            message.AppendLine($"ACK {ackRequestUri} SIP/2.0");
            message.AppendLine($"Via: SIP/2.0/TCP {_localIp}:{_localPort};branch={branch}");
            
            // CRITICAL FIX: From header must include username and match INVITE exactly
            // RFC 3261: ACK From and To headers must be identical to INVITE
            var fromHeader = !string.IsNullOrEmpty(username) 
                ? $"From: <sip:{username}@{_localIp}>;tag={localTag}"
                : $"From: <sip:{_localIp}>;tag={localTag}";
            message.AppendLine(fromHeader);
            
            // Fixed To header to match server format from 200 OK
            var toHeader = $"To: <sip:{toHeaderUri}>;tag={remoteTag}";
            message.AppendLine(toHeader);
            
            message.AppendLine($"Call-ID: {callId}");
            message.AppendLine($"CSeq: {sequenceNumber} ACK");
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine($"Max-Forwards: 70");
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            return message.ToString();
        }
          /// <summary>
        /// Extracts target user@host from a SIP URI for To header construction
        /// </summary>
        private static string ExtractTargetFromRequestUri(string requestUri)
        {
            // Extract user@host from sip:user@host:port
            if (requestUri.StartsWith("sip:"))
            {
                var userHost = requestUri.Substring(4); // Remove "sip:"
                var portIndex = userHost.LastIndexOf(':');
                if (portIndex > 0 && userHost.Substring(portIndex + 1).All(char.IsDigit))
                {
                    // Remove port number
                    userHost = userHost.Substring(0, portIndex);
                }
                return userHost;
            }
            return requestUri;
        }
        
        /// <summary>
        /// Extracts user part from SIP URI (e.g., "101" from "sip:101@192.168.1.180:5060")
        /// </summary>
        private static string ExtractUserFromRequestUri(string requestUri)
        {
            if (requestUri.StartsWith("sip:"))
            {
                var userHost = requestUri.Substring(4); // Remove "sip:"
                var atIndex = userHost.IndexOf('@');
                if (atIndex > 0)
                {
                    return userHost.Substring(0, atIndex);
                }
            }
            return "";
        }
        
        /// <summary>
        /// Extracts server part from SIP URI (e.g., "192.168.1.180" from "sip:101@192.168.1.180:5060")
        /// </summary>
        private static string ExtractServerFromRequestUri(string requestUri)
        {
            if (requestUri.StartsWith("sip:"))
            {
                var userHost = requestUri.Substring(4); // Remove "sip:"
                var atIndex = userHost.IndexOf('@');
                if (atIndex > 0)
                {
                    var hostPart = userHost.Substring(atIndex + 1);
                    var portIndex = hostPart.LastIndexOf(':');
                    if (portIndex > 0 && hostPart.Substring(portIndex + 1).All(char.IsDigit))
                    {
                        // Remove port number
                        hostPart = hostPart.Substring(0, portIndex);
                    }
                    return hostPart;
                }
            }
            return "";
        }
          /// <summary>
        /// Creates a 200 OK response
        /// </summary>
        public string Create200OkResponse(string originalRequest, string? sdpContent = null, string? toTag = null)
        {
            var headers = ParseRequestHeaders(originalRequest);
            var contentLength = string.IsNullOrEmpty(sdpContent) ? 0 : Encoding.UTF8.GetByteCount(sdpContent);
            
            var message = new StringBuilder();
            message.AppendLine("SIP/2.0 200 OK");
            
            // Access headers with error checking and fallback
            if (headers.ContainsKey("Via"))
                message.AppendLine(headers["Via"]);
            else
                throw new ArgumentException("Original request missing Via header");
                
            if (headers.ContainsKey("From"))
                message.AppendLine(headers["From"]);
            else
                throw new ArgumentException("Original request missing From header");
              // Add tag to To header if provided
            var toHeader = headers.ContainsKey("To") ? headers["To"] : throw new ArgumentException("Original request missing To header");
            if (!string.IsNullOrEmpty(toTag) && !toHeader.Contains("tag="))
            {
                // Extract the header value (after "To: ") to append the tag
                var toValue = toHeader.StartsWith("To: ") ? toHeader.Substring(4) : toHeader;
                toHeader = $"To: {toValue};tag={toTag}";
            }
            message.AppendLine(toHeader);
            
            if (headers.ContainsKey("Call-ID"))
                message.AppendLine(headers["Call-ID"]);
            else
                throw new ArgumentException("Original request missing Call-ID header");
                
            if (headers.ContainsKey("CSeq"))
                message.AppendLine(headers["CSeq"]);
            else
                throw new ArgumentException("Original request missing CSeq header");
                
            message.AppendLine($"Contact: <sip:{_username}@{_localIp}:{_localPort}>");
            message.AppendLine($"User-Agent: {_userAgent}");
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message.AppendLine("Content-Type: application/sdp");
            }
            
            message.AppendLine($"Content-Length: {contentLength}");
            message.AppendLine();
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message.Append(sdpContent);
            }
            
            return message.ToString();
        }        /// <summary>
        /// Creates a 180 Ringing response
        /// </summary>
        public string Create180RingingResponse(string originalRequest, string? toTag = null)
        {
            var headers = ParseRequestHeaders(originalRequest);
            
            var message = new StringBuilder();
            message.AppendLine("SIP/2.0 180 Ringing");
            
            // Access headers with error checking and fallback
            if (headers.ContainsKey("Via"))
                message.AppendLine(headers["Via"]);
            else
                throw new ArgumentException("Original request missing Via header");
                
            if (headers.ContainsKey("From"))
                message.AppendLine(headers["From"]);
            else
                throw new ArgumentException("Original request missing From header");
            
            // Add tag to To header if provided
            var toHeader = headers.ContainsKey("To") ? headers["To"] : throw new ArgumentException("Original request missing To header");
            if (!string.IsNullOrEmpty(toTag) && !toHeader.Contains("tag="))
            {
                // Extract the header value (after "To: ") to append the tag
                var toValue = toHeader.StartsWith("To: ") ? toHeader.Substring(4) : toHeader;
                toHeader = $"To: {toValue};tag={toTag}";
            }
            message.AppendLine(toHeader);
            
            if (headers.ContainsKey("Call-ID"))
                message.AppendLine(headers["Call-ID"]);
            else
                throw new ArgumentException("Original request missing Call-ID header");
                
            if (headers.ContainsKey("CSeq"))
                message.AppendLine(headers["CSeq"]);
            else
                throw new ArgumentException("Original request missing CSeq header");
                
            message.AppendLine($"Contact: <sip:{_username}@{_localIp}:{_localPort}>");
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            return message.ToString();
        }        /// <summary>
        /// Creates a 486 Busy Here response
        /// </summary>
        public string Create486BusyResponse(string originalRequest, string? toTag = null)
        {
            var headers = ParseRequestHeaders(originalRequest);
            
            var message = new StringBuilder();
            message.AppendLine("SIP/2.0 486 Busy Here");
            
            // Access headers with error checking and fallback
            if (headers.ContainsKey("Via"))
                message.AppendLine(headers["Via"]);
            else
                throw new ArgumentException("Original request missing Via header");
                
            if (headers.ContainsKey("From"))
                message.AppendLine(headers["From"]);
            else
                throw new ArgumentException("Original request missing From header");
            
            // Add tag to To header if provided
            var toHeader = headers.ContainsKey("To") ? headers["To"] : throw new ArgumentException("Original request missing To header");
            if (!string.IsNullOrEmpty(toTag) && !toHeader.Contains("tag="))
            {
                // Extract the header value (after "To: ") to append the tag
                var toValue = toHeader.StartsWith("To: ") ? toHeader.Substring(4) : toHeader;
                toHeader = $"To: {toValue};tag={toTag}";
            }
            message.AppendLine(toHeader);
            
            if (headers.ContainsKey("Call-ID"))
                message.AppendLine(headers["Call-ID"]);
            else
                throw new ArgumentException("Original request missing Call-ID header");
                
            if (headers.ContainsKey("CSeq"))
                message.AppendLine(headers["CSeq"]);
            else
                throw new ArgumentException("Original request missing CSeq header");
                
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            return message.ToString();
        }
        
        /// <summary>
        /// Creates a generic SIP response
        /// </summary>
        public string CreateResponse(int statusCode, string reasonPhrase, string callId, string localTag, string remoteTag, 
            string via, string from, string to, string cseq)
        {
            var message = new StringBuilder();
            message.AppendLine($"SIP/2.0 {statusCode} {reasonPhrase}");
            message.AppendLine(via);
            message.AppendLine(from);
            
            // Add tag to To header if provided and not already present
            var toHeader = to;
            if (!string.IsNullOrEmpty(localTag) && !toHeader.Contains("tag="))
            {
                toHeader = toHeader.TrimEnd('>') + $";tag={localTag}>";
            }
            message.AppendLine(toHeader);
              message.AppendLine($"Call-ID: {callId}");
            message.AppendLine(cseq);
            message.AppendLine($"Contact: <sip:{_username}@{_localIp}:{_localPort}>");
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine("Content-Length: 0");
            message.AppendLine();
            
            return message.ToString();
        }
        
        /// <summary>
        /// Creates a SIP response with SDP content
        /// </summary>
        public string CreateResponseWithSdp(int statusCode, string reasonPhrase, string callId, string localTag, string remoteTag,
            string via, string from, string to, string cseq, string sdpContent)
        {
            var contentLength = string.IsNullOrEmpty(sdpContent) ? 0 : Encoding.UTF8.GetByteCount(sdpContent);
            
            var message = new StringBuilder();
            message.AppendLine($"SIP/2.0 {statusCode} {reasonPhrase}");
            message.AppendLine(via);
            message.AppendLine(from);
            
            // Add tag to To header if provided and not already present
            var toHeader = to;
            if (!string.IsNullOrEmpty(localTag) && !toHeader.Contains("tag="))
            {
                toHeader = toHeader.TrimEnd('>') + $";tag={localTag}>";
            }
            message.AppendLine(toHeader);
              message.AppendLine($"Call-ID: {callId}");
            message.AppendLine(cseq);
            message.AppendLine($"Contact: <sip:{_username}@{_localIp}:{_localPort}>");
            message.AppendLine($"User-Agent: {_userAgent}");
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message.AppendLine("Content-Type: application/sdp");
            }
            
            message.AppendLine($"Content-Length: {contentLength}");
            message.AppendLine();
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message.Append(sdpContent);
            }
            
            return message.ToString();
        }
        
        /// <summary>
        /// Generates a unique Call-ID
        /// </summary>
        public static string GenerateCallId()
        {
            return $"{Guid.NewGuid():N}@{Environment.MachineName}";
        }
        
        /// <summary>
        /// Generates a unique tag
        /// </summary>
        public static string GenerateTag()
        {
            return Guid.NewGuid().ToString("N")[..8];
        }
        
        /// <summary>
        /// Generates a unique branch parameter for Via header
        /// </summary>
        public static string GenerateBranch()
        {
            return $"z9hG4bK{Guid.NewGuid():N}";
        }
          /// <summary>
        /// Parses headers from a SIP request
        /// </summary>
        private static Dictionary<string, string> ParseRequestHeaders(string request)
        {
            // Use case-insensitive dictionary to handle different header casing
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = request.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines.Skip(1)) // Skip request line
            {
                if (string.IsNullOrWhiteSpace(line)) break; // End of headers
                
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var headerName = line.Substring(0, colonIndex).Trim();
                    var headerValue = line.Substring(colonIndex + 1).Trim();
                    headers[headerName] = $"{headerName}: {headerValue}";
                }
            }
            
            return headers;
        }
        
        /// <summary>
        /// Creates a re-INVITE request for call modifications (hold/resume, media changes)
        /// RFC 3261 Compliant: re-INVITE has same Call-ID and dialog as original INVITE but new sequence number
        /// </summary>
        public string CreateReInviteRequest(SipDialog dialog, uint sequenceNumber, string? sdpContent = null)
        {
            var branch = GenerateBranch();
            var requestUri = !string.IsNullOrEmpty(dialog.RemoteTarget) ? dialog.RemoteTarget : dialog.RemoteUri;
            var contentLength = string.IsNullOrEmpty(sdpContent) ? 0 : Encoding.UTF8.GetByteCount(sdpContent);
            
            var message = new StringBuilder();
            message.AppendLine($"INVITE {requestUri} SIP/2.0");
            message.AppendLine($"Via: SIP/2.0/TCP {_localIp}:{_localPort};branch={branch}");
            message.AppendLine($"From: <{dialog.LocalUri}>;tag={dialog.LocalTag}");
            message.AppendLine($"To: <{dialog.RemoteUri}>;tag={dialog.RemoteTag}");
            message.AppendLine($"Call-ID: {dialog.CallId}");
            message.AppendLine($"CSeq: {sequenceNumber} INVITE");
            message.AppendLine($"Contact: <sip:{_localIp}:{_localPort}>");
            message.AppendLine($"User-Agent: {_userAgent}");
            message.AppendLine($"Max-Forwards: 70");
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message.AppendLine("Content-Type: application/sdp");
            }
            
            message.AppendLine($"Content-Length: {contentLength}");
            message.AppendLine();
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message.Append(sdpContent);
            }
            
            return message.ToString();
        }
    }
}
