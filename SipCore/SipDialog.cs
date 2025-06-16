using System;

namespace WindowsSipPhone.SipCore
{
    /// <summary>
    /// Represents a SIP dialog as defined in RFC 3261 Section 12
    /// A dialog is a peer-to-peer SIP relationship between two UAs that persists for some time
    /// </summary>
    public class SipDialog
    {
        public string CallId { get; set; } = string.Empty;
        public string LocalTag { get; set; } = string.Empty;
        public string RemoteTag { get; set; } = string.Empty;
        public DialogState State { get; set; } = DialogState.Null;
        public string LocalUri { get; set; } = string.Empty;
        public string RemoteUri { get; set; } = string.Empty;
        public string RemoteTarget { get; set; } = string.Empty; // Contact header URI
        public uint LocalSequenceNumber { get; set; } = 1;
        public uint RemoteSequenceNumber { get; set; } = 0;
        public string LocalContact { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime LastActivity { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Route set for this dialog (from Record-Route headers)
        /// </summary>
        public List<string> RouteSet { get; set; } = new();
        
        /// <summary>
        /// SDP information for this dialog
        /// </summary>
        public string? LocalSdp { get; set; }
        public string? RemoteSdp { get; set; }
        
        /// <summary>
        /// Creates a new SIP dialog
        /// </summary>
        public SipDialog(string callId, string localTag, string localUri, string localContact)
        {
            CallId = callId;
            LocalTag = localTag;
            LocalUri = localUri;
            LocalContact = localContact;
            LocalSequenceNumber = (uint)new Random().Next(1, 100000);
        }
        
        /// <summary>
        /// Gets the dialog ID as defined in RFC 3261
        /// </summary>
        public string DialogId => $"{CallId}-{LocalTag}-{RemoteTag}";
        
        /// <summary>
        /// Updates dialog state based on received response
        /// </summary>
        public void UpdateFromResponse(int statusCode, string toHeader, string? contactHeader = null)
        {
            LastActivity = DateTime.Now;
            
            // Extract remote tag from To header
            if (string.IsNullOrEmpty(RemoteTag) && toHeader.Contains("tag="))
            {
                var tagStart = toHeader.IndexOf("tag=") + 4;
                var tagEnd = toHeader.IndexOf(';', tagStart);
                if (tagEnd == -1) tagEnd = toHeader.Length;
                RemoteTag = toHeader.Substring(tagStart, tagEnd - tagStart).Trim();
            }
            
            // Update remote target from Contact header
            if (!string.IsNullOrEmpty(contactHeader))
            {
                RemoteTarget = ExtractUriFromContactHeader(contactHeader);
            }
            
            // Update dialog state based on status code
            switch (statusCode)
            {
                case int code when code >= 100 && code < 200:
                    if (State == DialogState.Null)
                        State = DialogState.Early;
                    break;
                    
                case int code when code >= 200 && code < 300:
                    if (State == DialogState.Early || State == DialogState.Null)
                        State = DialogState.Confirmed;
                    break;
                    
                case int code when code >= 300:
                    if (State == DialogState.Early)
                        State = DialogState.Terminated;
                    break;
            }
        }
        
        /// <summary>
        /// Updates dialog from incoming request
        /// </summary>
        public void UpdateFromRequest(string method, uint cseqNumber)
        {
            LastActivity = DateTime.Now;
            
            if (cseqNumber > RemoteSequenceNumber)
            {
                RemoteSequenceNumber = cseqNumber;
            }
            
            if (method.Equals("BYE", StringComparison.OrdinalIgnoreCase))
            {
                State = DialogState.Terminated;
            }
        }
        
        /// <summary>
        /// Updates dialog state (compatibility method)
        /// </summary>
        public void UpdateState(SipDialogState newState)
        {
            State = ConvertDialogState(newState);
            LastActivity = DateTime.Now;
        }
        
        /// <summary>
        /// Converts SipDialogState to DialogState
        /// </summary>
        private DialogState ConvertDialogState(SipDialogState state)
        {
            return state switch
            {
                SipDialogState.Null => DialogState.Null,
                SipDialogState.Early => DialogState.Early,
                SipDialogState.Confirmed => DialogState.Confirmed,
                SipDialogState.Terminated => DialogState.Terminated,
                _ => DialogState.Null
            };
        }
        
        /// <summary>
        /// Gets the next local sequence number
        /// </summary>
        public uint GetNextLocalSequenceNumber()
        {
            return ++LocalSequenceNumber;
        }
        
        /// <summary>
        /// Checks if dialog is established (confirmed state)
        /// </summary>
        public bool IsEstablished => State == DialogState.Confirmed;
        
        /// <summary>
        /// Checks if dialog is terminated
        /// </summary>
        public bool IsTerminated => State == DialogState.Terminated;
        
        /// <summary>
        /// Extracts SIP URI from Contact header
        /// </summary>
        private static string ExtractUriFromContactHeader(string contactHeader)
        {
            // Handle format: Contact: <sip:user@host:port>
            var uriStart = contactHeader.IndexOf('<');
            var uriEnd = contactHeader.IndexOf('>');
            
            if (uriStart != -1 && uriEnd != -1 && uriEnd > uriStart)
            {
                return contactHeader.Substring(uriStart + 1, uriEnd - uriStart - 1);
            }
            
            // Handle format without brackets
            var parts = contactHeader.Split(':', 2);
            if (parts.Length > 1)
            {
                return parts[1].Split(';')[0].Trim();
            }
            
            return contactHeader.Trim();
        }
        
        public override string ToString()
        {
            return $"Dialog[{DialogId}] State={State} LocalSeq={LocalSequenceNumber} RemoteSeq={RemoteSequenceNumber}";
        }
    }
    
    /// <summary>
    /// SIP Dialog states as defined in RFC 3261
    /// </summary>
    public enum DialogState
    {
        /// <summary>
        /// No dialog exists
        /// </summary>
        Null,
        
        /// <summary>
        /// Dialog created by 1xx response (except 100)
        /// </summary>
        Early,
        
        /// <summary>
        /// Dialog confirmed by 2xx response and ACK
        /// </summary>
        Confirmed,
        
        /// <summary>
        /// Dialog terminated by BYE or error response
        /// </summary>
        Terminated
    }
    
    /// <summary>
    /// Compatibility alias for DialogState
    /// </summary>
    public enum SipDialogState
    {
        /// <summary>
        /// No dialog exists
        /// </summary>
        Null,
        
        /// <summary>
        /// Dialog created by 1xx response (except 100)
        /// </summary>
        Early,
        
        /// <summary>
        /// Dialog confirmed by 2xx response and ACK
        /// </summary>
        Confirmed,
        
        /// <summary>
        /// Dialog terminated by BYE or error response
        /// </summary>
        Terminated
    }
}
