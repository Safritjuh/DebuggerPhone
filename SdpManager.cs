using System;
using System.Net;
using System.Text;

namespace WindowsSipPhone
{
    /// <summary>
    /// Manages SDP (Session Description Protocol) for SIP call negotiation
    /// Handles creation and parsing of SDP offers/answers for audio streams
    /// </summary>
    public static class SdpManager
    {        /// <summary>
        /// Creates an SDP offer for an outbound call with G.711 A-law preference and DTMF support
        /// </summary>
        public static string CreateSdpOffer(string localIp, int rtpPort)
        {
            var sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var version = sessionId; // Use same value for simplicity
            
            var sdp = new StringBuilder();
            
            // Session Description
            sdp.AppendLine("v=0"); // Version
            sdp.AppendLine($"o=user {sessionId} {version} IN IP4 {localIp}"); // Origin
            sdp.AppendLine("s=Windows SIP Phone Call"); // Session Name
            sdp.AppendLine($"c=IN IP4 {localIp}"); // Connection Information
            sdp.AppendLine("t=0 0"); // Time Description (0 0 = permanent session)
            
            // Media Description for Audio - G.711 A-law preferred (payload 8 first)
            sdp.AppendLine($"m=audio {rtpPort} RTP/AVP 8 0 101"); // Media (port, protocol, codecs)
            
            // Codec definitions - A-law first for preference
            sdp.AppendLine("a=rtpmap:8 PCMA/8000"); // G.711 A-law codec (primary preference)
            sdp.AppendLine("a=rtpmap:0 PCMU/8000"); // G.711 µ-law codec (fallback)
            sdp.AppendLine("a=rtpmap:101 telephone-event/8000"); // DTMF support
            
            // DTMF configuration
            sdp.AppendLine("a=fmtp:101 0-15"); // DTMF events 0-9, *, #, A, B, C, D
            
            // Audio direction
            sdp.AppendLine("a=sendrecv"); // Send and receive audio
            
            return sdp.ToString();
        }        /// <summary>
        /// Creates an SDP answer for an incoming call
        /// </summary>
        public static string CreateSdpAnswer(string localIp, int rtpPort, string offerSdp)
        {
            // For now, create a simple answer similar to offer
            // In a full implementation, this would parse the offer and negotiate codecs
            return CreateSdpOffer(localIp, rtpPort);
        }        /// <summary>
        /// Creates an SDP offer with inactive media direction for call hold
        /// </summary>
        public static string CreateInactiveSdpOffer(string localIp, int rtpPort)
        {
            var sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var version = sessionId; // Use same value for simplicity
            
            var sdp = new StringBuilder();
            
            // Session Description
            sdp.AppendLine("v=0"); // Version
            sdp.AppendLine($"o=user {sessionId} {version} IN IP4 {localIp}"); // Origin
            sdp.AppendLine("s=Windows SIP Phone Call (On Hold)"); // Session Name
            sdp.AppendLine($"c=IN IP4 {localIp}"); // Connection Information
            sdp.AppendLine("t=0 0"); // Time Description (0 0 = permanent session)
            
            // Media Description for Audio - G.711 A-law preferred (payload 8 first)
            sdp.AppendLine($"m=audio {rtpPort} RTP/AVP 8 0 101"); // Media (port, protocol, codecs)
            
            // Codec definitions - A-law first for preference
            sdp.AppendLine("a=rtpmap:8 PCMA/8000"); // G.711 A-law codec (primary preference)
            sdp.AppendLine("a=rtpmap:0 PCMU/8000"); // G.711 µ-law codec (fallback)
            sdp.AppendLine("a=rtpmap:101 telephone-event/8000"); // DTMF support
            
            // DTMF configuration
            sdp.AppendLine("a=fmtp:101 0-15"); // DTMF events 0-9, *, #, A, B, C, D
            
            // Audio direction - INACTIVE for hold
            sdp.AppendLine("a=inactive"); // Hold - no send or receive audio
            
            return sdp.ToString();
        }

        /// <summary>
        /// Creates an SDP offer with sendonly media direction for call hold
        /// Alternative to inactive - allows sending audio but not receiving
        /// </summary>
        public static string CreateSendonlySdpOffer(string localIp, int rtpPort)
        {
            var sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var version = sessionId; // Use same value for simplicity
            
            var sdp = new StringBuilder();
            
            // Session Description
            sdp.AppendLine("v=0"); // Version
            sdp.AppendLine($"o=user {sessionId} {version} IN IP4 {localIp}"); // Origin
            sdp.AppendLine("s=Windows SIP Phone Call (Send Only)"); // Session Name
            sdp.AppendLine($"c=IN IP4 {localIp}"); // Connection Information
            sdp.AppendLine("t=0 0"); // Time Description (0 0 = permanent session)
            
            // Media Description for Audio - G.711 A-law preferred (payload 8 first)
            sdp.AppendLine($"m=audio {rtpPort} RTP/AVP 8 0 101"); // Media (port, protocol, codecs)
            
            // Codec definitions - A-law first for preference
            sdp.AppendLine("a=rtpmap:8 PCMA/8000"); // G.711 A-law codec (primary preference)
            sdp.AppendLine("a=rtpmap:0 PCMU/8000"); // G.711 µ-law codec (fallback)
            sdp.AppendLine("a=rtpmap:101 telephone-event/8000"); // DTMF support
            
            // DTMF configuration
            sdp.AppendLine("a=fmtp:101 0-15"); // DTMF events 0-9, *, #, A, B, C, D
            
            // Audio direction - SENDONLY for hold
            sdp.AppendLine("a=sendonly"); // Hold - send audio but don't receive
            
            return sdp.ToString();
        }/// <summary>
        /// Parses SDP content to extract media information
        /// </summary>
        public static SdpInfo? ParseSdpContent(string sdpContent)
        {
            if (string.IsNullOrEmpty(sdpContent))
            {
                Console.WriteLine($"[SDP DEBUG] ParseSdpContent called with empty content");
                return null;
            }

            Console.WriteLine($"[SDP DEBUG] ==========================================");
            Console.WriteLine($"[SDP DEBUG] Parsing SDP content:");
            Console.WriteLine($"[SDP DEBUG] ==========================================");
            Console.WriteLine(sdpContent);
            Console.WriteLine($"[SDP DEBUG] ==========================================");

            var sdpInfo = new SdpInfo();
            var lines = sdpContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var firstOfferedPayloadType = -1;

            foreach (var line in lines)
            {
                Console.WriteLine($"[SDP DEBUG] Processing line: '{line}'");
                
                if (line.StartsWith("c="))
                {
                    // Connection information: c=IN IP4 192.168.1.100
                    Console.WriteLine($"[SDP DEBUG] Found connection line: '{line}'");
                    var parts = line.Split(' ');
                    if (parts.Length >= 3)
                    {
                        sdpInfo.RemoteIp = parts[2];
                        Console.WriteLine($"[SDP DEBUG] *** EXTRACTED REMOTE IP: '{sdpInfo.RemoteIp}' ***");
                    }
                    else
                    {
                        Console.WriteLine($"[SDP DEBUG] ERROR: Connection line has insufficient parts: {parts.Length}");
                    }
                }
                else if (line.StartsWith("m=audio"))
                {
                    // Media description: m=audio 5004 RTP/AVP 0 8
                    Console.WriteLine($"[SDP DEBUG] Found media line: '{line}'");
                    var parts = line.Split(' ');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var port))
                    {
                        sdpInfo.RemoteRtpPort = port;
                        sdpInfo.HasAudio = true;
                        Console.WriteLine($"[SDP DEBUG] *** EXTRACTED REMOTE RTP PORT: {sdpInfo.RemoteRtpPort} ***");
                        
                        // Extract the first offered payload type for codec preference
                        if (parts.Length >= 4 && int.TryParse(parts[3], out var payloadType))
                        {
                            firstOfferedPayloadType = payloadType;
                            Console.WriteLine($"[SDP DEBUG] First offered payload type: {firstOfferedPayloadType}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[SDP DEBUG] ERROR: Could not parse RTP port from media line");
                    }
                }else if (line.StartsWith("a=rtpmap:"))
                {
                    // Codec mapping: a=rtpmap:0 PCMU/8000, a=rtpmap:8 PCMA/8000, a=rtpmap:101 telephone-event/8000
                    var parts = line.Split(' ');
                    if (parts.Length >= 2)
                    {
                        var payloadPart = parts[0].Split(':');
                        var codecPart = parts[1];
                        
                        if (payloadPart.Length >= 2 && int.TryParse(payloadPart[1], out var payloadType))
                        {
                            // Prioritize A-law (PCMA) over µ-law (PCMU)
                            if (codecPart.Contains("PCMA") && (firstOfferedPayloadType == payloadType || string.IsNullOrEmpty(sdpInfo.AudioCodec) || sdpInfo.AudioCodec == "PCMU"))
                            {
                                sdpInfo.AudioCodec = "PCMA";
                                sdpInfo.PayloadType = payloadType;
                            }
                            else if (codecPart.Contains("PCMU") && string.IsNullOrEmpty(sdpInfo.AudioCodec))
                            {
                                sdpInfo.AudioCodec = "PCMU";
                                sdpInfo.PayloadType = payloadType;
                            }
                            else if (codecPart.Contains("telephone-event"))
                            {
                                sdpInfo.HasDtmf = true;
                                sdpInfo.DtmfPayloadType = payloadType;
                            }
                        }
                    }
                }
            }            // Default fallback if no codec was detected
            if (string.IsNullOrEmpty(sdpInfo.AudioCodec))
            {
                sdpInfo.AudioCodec = "PCMU";
                sdpInfo.PayloadType = 0;
                Console.WriteLine($"[SDP DEBUG] Using default codec: PCMU, payload type: 0");
            }

            Console.WriteLine($"[SDP DEBUG] ==========================================");
            Console.WriteLine($"[SDP DEBUG] FINAL SDP PARSING RESULTS:");
            Console.WriteLine($"[SDP DEBUG] - Remote IP: '{sdpInfo.RemoteIp}'");
            Console.WriteLine($"[SDP DEBUG] - Remote RTP Port: {sdpInfo.RemoteRtpPort}");
            Console.WriteLine($"[SDP DEBUG] - Audio Codec: '{sdpInfo.AudioCodec}'");
            Console.WriteLine($"[SDP DEBUG] - Payload Type: {sdpInfo.PayloadType}");
            Console.WriteLine($"[SDP DEBUG] - Has Audio: {sdpInfo.HasAudio}");
            Console.WriteLine($"[SDP DEBUG] - Has DTMF: {sdpInfo.HasDtmf}");
            Console.WriteLine($"[SDP DEBUG] ==========================================");

            return sdpInfo.HasAudio ? sdpInfo : null;
        }

        /// <summary>
        /// Validates if an SDP contains audio media
        /// </summary>
        public static bool HasAudioMedia(string sdpContent)
        {
            return sdpContent.Contains("m=audio") && sdpContent.Contains("RTP/AVP");
        }

        /// <summary>
        /// Gets the content length for an SDP string (for SIP Content-Length header)
        /// </summary>
        public static int GetSdpLength(string sdpContent)
        {
            return Encoding.UTF8.GetByteCount(sdpContent);
        }
    }    /// <summary>
    /// Contains parsed SDP information for media negotiation
    /// </summary>
    public class SdpInfo
    {
        public string RemoteIp { get; set; } = "";
        public int RemoteRtpPort { get; set; } = 0;
        public string AudioCodec { get; set; } = "PCMA"; // Default to A-law
        public int PayloadType { get; set; } = 8; // Default to A-law payload type
        public bool HasAudio { get; set; } = false;
        public bool HasDtmf { get; set; } = false;
        public int DtmfPayloadType { get; set; } = 101; // Standard DTMF payload type
        
        public override string ToString()
        {
            return $"Audio: {HasAudio}, IP: {RemoteIp}:{RemoteRtpPort}, Codec: {AudioCodec} (PT:{PayloadType}), DTMF: {HasDtmf} (PT:{DtmfPayloadType})";
        }
    }
}
