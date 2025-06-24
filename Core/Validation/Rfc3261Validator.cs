using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsSipPhone.Core.Validation
{
    /// <summary>
    /// RFC 3261 SIP Message Validator
    /// Provides comprehensive validation of SIP messages for RFC 3261 compliance
    /// </summary>
    public class Rfc3261Validator
    {
        private static readonly string[] MandatoryRequestHeaders = 
        {
            "Via", "From", "To", "Call-ID", "CSeq", "Max-Forwards"
        };

        private static readonly string[] MandatoryResponseHeaders = 
        {
            "Via", "From", "To", "Call-ID", "CSeq"
        };        private static readonly Dictionary<string, string[]> MethodSpecificHeaders = new()
        {
            { "REGISTER", new string[] { "Contact", "Expires" } },
            { "INVITE", new string[] { "Contact" } },
            { "BYE", new string[] { } },
            { "ACK", new string[] { } },
            { "CANCEL", new string[] { } }
        };

        /// <summary>
        /// Validates a complete SIP message for RFC 3261 compliance
        /// </summary>
        public ValidationResult ValidateMessage(string sipMessage)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(sipMessage))
            {
                result.AddError(ValidationSeverity.Critical, "SIP message is null or empty");
                return result;
            }

            try
            {
                // Determine if this is a request or response
                var lines = sipMessage.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0)
                {
                    result.AddError(ValidationSeverity.Critical, "Invalid SIP message format");
                    return result;
                }

                var firstLine = lines[0].Trim();
                bool isRequest = !firstLine.StartsWith("SIP/");

                // Validate message structure
                result.Errors.AddRange(ValidateMessageStructure(sipMessage));

                // Validate start line
                if (isRequest)
                {
                    result.Errors.AddRange(ValidateRequestLine(firstLine));
                }
                else
                {
                    result.Errors.AddRange(ValidateStatusLine(firstLine));
                }

                // Validate headers
                result.Errors.AddRange(ValidateHeaders(sipMessage, isRequest));

                // Validate specific header formats
                result.Errors.AddRange(ValidateHeaderFormats(sipMessage));

                // Validate Content-Length accuracy
                result.Errors.AddRange(ValidateContentLength(sipMessage));

                // Validate Via branch parameters
                result.Errors.AddRange(ValidateViaBranch(sipMessage));

                // Validate method-specific requirements
                if (isRequest)
                {
                    var method = firstLine.Split(' ')[0];
                    result.Errors.AddRange(ValidateMethodSpecific(sipMessage, method));
                }

                // Validate SDP if present
                result.Errors.AddRange(ValidateSdpContent(sipMessage));

                result.IsValid = !result.Errors.Any(e => e.Severity == ValidationSeverity.Critical);
            }
            catch (Exception ex)
            {
                result.AddError(ValidationSeverity.Critical, $"Validation exception: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validates basic SIP message structure
        /// </summary>
        private List<ValidationError> ValidateMessageStructure(string sipMessage)
        {
            var errors = new List<ValidationError>();

            // Check for proper CRLF line endings
            if (sipMessage.Contains("\n") && !sipMessage.Contains("\r\n"))
            {
                errors.Add(new ValidationError(ValidationSeverity.Major, 
                    "SIP message should use CRLF (\\r\\n) line endings, not just LF (\\n)"));
            }

            // Check for empty line separating headers from body
            if (!sipMessage.Contains("\r\n\r\n"))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "SIP message missing empty line between headers and body"));
            }

            // Check message size (RFC 3261 doesn't specify limits, but practical limits apply)
            if (sipMessage.Length > 65536) // 64KB practical limit
            {
                errors.Add(new ValidationError(ValidationSeverity.Warning, 
                    "SIP message exceeds practical size limit (64KB)"));
            }

            return errors;
        }

        /// <summary>
        /// Validates SIP request line format
        /// </summary>
        private List<ValidationError> ValidateRequestLine(string requestLine)
        {
            var errors = new List<ValidationError>();

            // Request line format: METHOD REQUEST-URI SIP/2.0
            var parts = requestLine.Split(' ');
            if (parts.Length != 3)
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Invalid request line format. Expected: METHOD REQUEST-URI SIP/2.0"));
                return errors;
            }

            var method = parts[0];
            var requestUri = parts[1];
            var sipVersion = parts[2];

            // Validate method
            var validMethods = new[] { "INVITE", "ACK", "BYE", "CANCEL", "REGISTER", "OPTIONS", "INFO", "UPDATE", "REFER" };
            if (!validMethods.Contains(method.ToUpper()))
            {
                errors.Add(new ValidationError(ValidationSeverity.Warning, 
                    $"Unknown or non-standard SIP method: {method}"));
            }

            // Validate Request-URI format
            if (!requestUri.StartsWith("sip:") && !requestUri.StartsWith("sips:"))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Request-URI must be a SIP or SIPS URI"));
            }

            // Validate SIP version
            if (sipVersion != "SIP/2.0")
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    $"Invalid SIP version: {sipVersion}. Must be SIP/2.0"));
            }

            return errors;
        }

        /// <summary>
        /// Validates SIP status line format
        /// </summary>
        private List<ValidationError> ValidateStatusLine(string statusLine)
        {
            var errors = new List<ValidationError>();

            // Status line format: SIP/2.0 STATUS-CODE REASON-PHRASE
            var parts = statusLine.Split(' ', 3);
            if (parts.Length < 3)
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Invalid status line format. Expected: SIP/2.0 STATUS-CODE REASON-PHRASE"));
                return errors;
            }

            var sipVersion = parts[0];
            var statusCode = parts[1];
            var reasonPhrase = parts[2];

            // Validate SIP version
            if (sipVersion != "SIP/2.0")
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    $"Invalid SIP version: {sipVersion}. Must be SIP/2.0"));
            }

            // Validate status code
            if (!int.TryParse(statusCode, out int code) || code < 100 || code > 699)
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    $"Invalid status code: {statusCode}. Must be between 100-699"));
            }

            // Validate reason phrase (basic check - should be present)
            if (string.IsNullOrWhiteSpace(reasonPhrase))
            {
                errors.Add(new ValidationError(ValidationSeverity.Major, 
                    "Status line missing reason phrase"));
            }

            return errors;
        }

        /// <summary>
        /// Validates presence of mandatory headers
        /// </summary>
        private List<ValidationError> ValidateHeaders(string sipMessage, bool isRequest)
        {
            var errors = new List<ValidationError>();
            var headers = ExtractHeaders(sipMessage);
            var mandatoryHeaders = isRequest ? MandatoryRequestHeaders : MandatoryResponseHeaders;

            foreach (var header in mandatoryHeaders)
            {
                if (!headers.ContainsKey(header.ToLower()))
                {
                    errors.Add(new ValidationError(ValidationSeverity.Critical, 
                        $"Missing mandatory header: {header}"));
                }
            }

            // Check for duplicate headers that should be unique
            var uniqueHeaders = new[] { "call-id", "cseq", "max-forwards", "content-length" };
            foreach (var header in uniqueHeaders)
            {
                if (headers.ContainsKey(header) && headers[header].Count > 1)
                {
                    errors.Add(new ValidationError(ValidationSeverity.Major, 
                        $"Duplicate header not allowed: {header}"));
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates specific header formats
        /// </summary>
        private List<ValidationError> ValidateHeaderFormats(string sipMessage)
        {
            var errors = new List<ValidationError>();
            var headers = ExtractHeaders(sipMessage);

            // Validate Via header format
            if (headers.ContainsKey("via"))
            {
                foreach (var via in headers["via"])
                {
                    errors.AddRange(ValidateViaHeader(via));
                }
            }

            // Validate From header format
            if (headers.ContainsKey("from"))
            {
                errors.AddRange(ValidateFromToHeader(headers["from"][0], "From"));
            }

            // Validate To header format
            if (headers.ContainsKey("to"))
            {
                errors.AddRange(ValidateFromToHeader(headers["to"][0], "To"));
            }

            // Validate CSeq header format
            if (headers.ContainsKey("cseq"))
            {
                errors.AddRange(ValidateCSeqHeader(headers["cseq"][0]));
            }

            // Validate Contact header format
            if (headers.ContainsKey("contact"))
            {
                foreach (var contact in headers["contact"])
                {
                    errors.AddRange(ValidateContactHeader(contact));
                }
            }

            // Validate Content-Type header format
            if (headers.ContainsKey("content-type"))
            {
                errors.AddRange(ValidateContentTypeHeader(headers["content-type"][0]));
            }

            return errors;
        }

        /// <summary>
        /// Validates Via header format and branch parameter
        /// </summary>
        private List<ValidationError> ValidateViaHeader(string via)
        {
            var errors = new List<ValidationError>();

            // Via header format: Via: SIP/2.0/protocol host:port;parameters
            var viaRegex = new Regex(@"SIP/2\.0/(UDP|TCP|TLS|SCTP|WS|WSS)\s+([^;]+)(.*)");
            var match = viaRegex.Match(via);

            if (!match.Success)
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    $"Invalid Via header format: {via}"));
                return errors;
            }

            var protocol = match.Groups[1].Value;
            var hostPort = match.Groups[2].Value;
            var parameters = match.Groups[3].Value;

            // Validate protocol
            var validProtocols = new[] { "UDP", "TCP", "TLS", "SCTP", "WS", "WSS" };
            if (!validProtocols.Contains(protocol.ToUpper()))
            {
                errors.Add(new ValidationError(ValidationSeverity.Warning, 
                    $"Non-standard transport protocol in Via header: {protocol}"));
            }

            // Validate host:port format
            if (string.IsNullOrWhiteSpace(hostPort))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Via header missing host"));
            }

            // Check for branch parameter (mandatory for RFC 3261 compliance)
            if (!parameters.Contains("branch="))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Via header missing branch parameter"));
            }
            else
            {
                // Validate branch parameter format
                var branchMatch = Regex.Match(parameters, @"branch=([^;]+)");
                if (branchMatch.Success)
                {
                    var branch = branchMatch.Groups[1].Value;
                    if (!branch.StartsWith("z9hG4bK"))
                    {
                        errors.Add(new ValidationError(ValidationSeverity.Major, 
                            "Branch parameter should start with 'z9hG4bK' magic cookie for RFC 3261 compliance"));
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates From/To header format
        /// </summary>
        private List<ValidationError> ValidateFromToHeader(string header, string headerName)
        {
            var errors = new List<ValidationError>();

            // From/To format: "Display Name" <sip:user@domain>;tag=value
            // or: sip:user@domain;tag=value

            if (string.IsNullOrWhiteSpace(header))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    $"{headerName} header is empty"));
                return errors;
            }

            // Check for SIP URI
            if (!header.Contains("sip:") && !header.Contains("sips:"))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    $"{headerName} header must contain a SIP or SIPS URI"));
            }

            // For From header, tag parameter is mandatory in requests
            // For To header, tag parameter is added in responses
            if (headerName == "From" && !header.Contains("tag="))
            {
                errors.Add(new ValidationError(ValidationSeverity.Major, 
                    "From header should contain tag parameter"));
            }

            return errors;
        }

        /// <summary>
        /// Validates CSeq header format
        /// </summary>
        private List<ValidationError> ValidateCSeqHeader(string cseq)
        {
            var errors = new List<ValidationError>();

            // CSeq format: sequence-number method
            var parts = cseq.Trim().Split(' ');
            if (parts.Length != 2)
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "CSeq header must contain sequence number and method"));
                return errors;
            }

            // Validate sequence number
            if (!uint.TryParse(parts[0], out _))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "CSeq sequence number must be a valid unsigned integer"));
            }

            // Validate method
            var method = parts[1];
            var validMethods = new[] { "INVITE", "ACK", "BYE", "CANCEL", "REGISTER", "OPTIONS", "INFO", "UPDATE", "REFER" };
            if (!validMethods.Contains(method.ToUpper()))
            {
                errors.Add(new ValidationError(ValidationSeverity.Warning, 
                    $"Unknown or non-standard method in CSeq header: {method}"));
            }

            return errors;
        }

        /// <summary>
        /// Validates Contact header format
        /// </summary>
        private List<ValidationError> ValidateContactHeader(string contact)
        {
            var errors = new List<ValidationError>();

            if (contact.Trim() == "*")
            {
                // Special case for REGISTER with Contact: *
                return errors;
            }

            // Contact should contain a SIP URI
            if (!contact.Contains("sip:") && !contact.Contains("sips:"))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Contact header must contain a SIP or SIPS URI"));
            }

            return errors;
        }

        /// <summary>
        /// Validates Content-Type header format
        /// </summary>
        private List<ValidationError> ValidateContentTypeHeader(string contentType)
        {
            var errors = new List<ValidationError>();

            // Content-Type format: type/subtype;parameters
            var mediaTypeRegex = new Regex(@"^([a-zA-Z0-9][a-zA-Z0-9!#$&\-\^]*)/([a-zA-Z0-9][a-zA-Z0-9!#$&\-\^]*)");
            
            if (!mediaTypeRegex.IsMatch(contentType))
            {
                errors.Add(new ValidationError(ValidationSeverity.Major, 
                    $"Invalid Content-Type format: {contentType}"));
            }

            return errors;
        }

        /// <summary>
        /// Validates Content-Length accuracy
        /// </summary>
        private List<ValidationError> ValidateContentLength(string sipMessage)
        {
            var errors = new List<ValidationError>();

            var headers = ExtractHeaders(sipMessage);
            if (!headers.ContainsKey("content-length"))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Missing Content-Length header"));
                return errors;
            }

            var contentLengthValue = headers["content-length"][0];
            if (!int.TryParse(contentLengthValue, out int declaredLength))
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Content-Length header must be a valid integer"));
                return errors;
            }

            // Extract body and calculate actual length
            var bodyStartIndex = sipMessage.IndexOf("\r\n\r\n");
            if (bodyStartIndex >= 0)
            {
                var body = sipMessage.Substring(bodyStartIndex + 4);
                var actualLength = Encoding.UTF8.GetByteCount(body);

                if (actualLength != declaredLength)
                {
                    errors.Add(new ValidationError(ValidationSeverity.Critical, 
                        $"Content-Length mismatch: declared {declaredLength}, actual {actualLength}"));
                }
            }
            else if (declaredLength > 0)
            {
                errors.Add(new ValidationError(ValidationSeverity.Critical, 
                    "Content-Length > 0 but no message body found"));
            }

            return errors;
        }

        /// <summary>
        /// Validates Via branch parameter for proper format
        /// </summary>
        private List<ValidationError> ValidateViaBranch(string sipMessage)
        {
            var errors = new List<ValidationError>();
            var headers = ExtractHeaders(sipMessage);

            if (headers.ContainsKey("via"))
            {
                foreach (var via in headers["via"])
                {
                    var branchMatch = Regex.Match(via, @"branch=([^;]+)");
                    if (branchMatch.Success)
                    {
                        var branch = branchMatch.Groups[1].Value;
                        
                        // RFC 3261 magic cookie
                        if (!branch.StartsWith("z9hG4bK"))
                        {
                            errors.Add(new ValidationError(ValidationSeverity.Major, 
                                "Branch parameter should start with 'z9hG4bK' magic cookie"));
                        }

                        // Branch should be unique and sufficiently random
                        if (branch.Length < 16) // Minimum reasonable length
                        {
                            errors.Add(new ValidationError(ValidationSeverity.Warning, 
                                "Branch parameter should be longer for better uniqueness"));
                        }
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates method-specific requirements
        /// </summary>
        private List<ValidationError> ValidateMethodSpecific(string sipMessage, string method)
        {
            var errors = new List<ValidationError>();
            var headers = ExtractHeaders(sipMessage);

            if (MethodSpecificHeaders.ContainsKey(method.ToUpper()))
            {
                var requiredHeaders = MethodSpecificHeaders[method.ToUpper()];
                foreach (var header in requiredHeaders)
                {
                    if (!headers.ContainsKey(header.ToLower()))
                    {
                        errors.Add(new ValidationError(ValidationSeverity.Major, 
                            $"{method} request missing required header: {header}"));
                    }
                }
            }

            // REGISTER-specific validation
            if (method.ToUpper() == "REGISTER")
            {
                if (headers.ContainsKey("contact"))
                {
                    var contact = headers["contact"][0];
                    if (contact != "*" && !contact.Contains("expires=") && !headers.ContainsKey("expires"))
                    {
                        errors.Add(new ValidationError(ValidationSeverity.Warning, 
                            "REGISTER should specify expiration time in Contact or Expires header"));
                    }
                }
            }

            // INVITE-specific validation
            if (method.ToUpper() == "INVITE")
            {
                // INVITE should have SDP unless it's a re-INVITE
                if (!headers.ContainsKey("content-type"))
                {
                    errors.Add(new ValidationError(ValidationSeverity.Warning, 
                        "INVITE request should typically contain SDP offer"));
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates SDP content if present
        /// </summary>
        private List<ValidationError> ValidateSdpContent(string sipMessage)
        {
            var errors = new List<ValidationError>();
            var headers = ExtractHeaders(sipMessage);

            if (headers.ContainsKey("content-type"))
            {
                var contentType = headers["content-type"][0];
                if (contentType.Contains("application/sdp"))
                {
                    var bodyStartIndex = sipMessage.IndexOf("\r\n\r\n");
                    if (bodyStartIndex >= 0)
                    {
                        var body = sipMessage.Substring(bodyStartIndex + 4);
                        errors.AddRange(ValidateSdpFormat(body));
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates basic SDP format
        /// </summary>
        private List<ValidationError> ValidateSdpFormat(string sdp)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(sdp))
            {
                return errors;
            }

            var lines = sdp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var mandatorySdpLines = new[] { "v=", "o=", "s=", "c=", "t=" };
            
            foreach (var mandatory in mandatorySdpLines)
            {
                if (!lines.Any(line => line.StartsWith(mandatory)))
                {
                    errors.Add(new ValidationError(ValidationSeverity.Major, 
                        $"SDP missing mandatory line: {mandatory}"));
                }
            }

            // Validate SDP line format
            foreach (var line in lines)
            {
                if (line.Length < 2 || line[1] != '=')
                {
                    errors.Add(new ValidationError(ValidationSeverity.Major, 
                        $"Invalid SDP line format: {line}"));
                }
            }

            return errors;
        }

        /// <summary>
        /// Extracts headers from SIP message into a dictionary
        /// </summary>
        private Dictionary<string, List<string>> ExtractHeaders(string sipMessage)
        {
            var headers = new Dictionary<string, List<string>>();
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

                    if (!headers.ContainsKey(headerName))
                    {
                        headers[headerName] = new List<string>();
                    }
                    headers[headerName].Add(headerValue);
                }
            }

            return headers;
        }
    }

    /// <summary>
    /// Validation result containing errors and overall validity
    /// </summary>
    public class ValidationResult
    {
        public List<ValidationError> Errors { get; } = new();
        public bool IsValid { get; set; } = true;

        public void AddError(ValidationSeverity severity, string message)
        {
            Errors.Add(new ValidationError(severity, message));
        }

        public bool HasCriticalErrors => Errors.Any(e => e.Severity == ValidationSeverity.Critical);
        public bool HasMajorErrors => Errors.Any(e => e.Severity == ValidationSeverity.Major);
        public bool HasWarnings => Errors.Any(e => e.Severity == ValidationSeverity.Warning);
    }

    /// <summary>
    /// Individual validation error
    /// </summary>
    public class ValidationError
    {
        public ValidationSeverity Severity { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public ValidationError(ValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"[{Severity}] {Message}";
        }
    }

    /// <summary>
    /// Validation error severity levels
    /// </summary>
    public enum ValidationSeverity
    {
        Warning,    // Non-critical issues that may affect interoperability
        Major,      // Significant issues that should be fixed
        Critical    // Issues that violate RFC 3261 and may break functionality
    }
}
