# SIP Flow Documentation

This document provides comprehensive details on the SIP (Session Initiation Protocol) flows implemented in the Windows SIP Phone application, including registration and call setup processes.

## 📋 Table of Contents

- [SIP Registration Flow](#sip-registration-flow)
- [SIP Call Setup Flow](#sip-call-setup-flow)
- [SIP Authentication Process](#sip-authentication-process)
- [Message Sequence Diagrams](#message-sequence-diagrams)
- [Protocol Implementation Details](#protocol-implementation-details)
- [Error Handling and Edge Cases](#error-handling-and-edge-cases)

## 🔐 SIP Registration Flow

The SIP registration process establishes the client's presence with the SIP server and enables incoming calls.

### Registration Sequence

```
Client                    SIP Server
  |                          |
  |--- REGISTER -----------> |  Step 1: Initial registration request
  |                          |
  |<-- 401 Unauthorized ---- |  Step 2: Server requests authentication
  |                          |
  |--- REGISTER (Auth) ----> |  Step 3: Registration with credentials
  |                          |
  |<-- 200 OK -------------- |  Step 4: Registration successful
  |                          |
  |<-- OPTIONS ------------- |  Step 5: Keep-alive (optional)
  |                          |
  |--- 200 OK -------------> |  Step 6: Keep-alive response
```

### Detailed Message Flow

#### Step 1: Initial REGISTER Request
```sip
REGISTER sip:yourdomain.com SIP/2.0
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-abc123
Max-Forwards: 70
From: <sip:user@yourdomain.com>;tag=1234567890
To: <sip:user@yourdomain.com>
Call-ID: call-12345@192.168.1.100
CSeq: 1 REGISTER
Contact: <sip:user@192.168.1.100:5060>;expires=3600
User-Agent: WindowsSipPhone/1.0
Content-Length: 0
```

#### Step 2: 401 Unauthorized Response
```sip
SIP/2.0 401 Unauthorized
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-abc123
From: <sip:user@yourdomain.com>;tag=1234567890
To: <sip:user@yourdomain.com>;tag=server-tag-456
Call-ID: call-12345@192.168.1.100
CSeq: 1 REGISTER
WWW-Authenticate: Digest realm="yourdomain.com", nonce="randomnonce123", algorithm=MD5
Content-Length: 0
```

#### Step 3: Authenticated REGISTER Request
```sip
REGISTER sip:yourdomain.com SIP/2.0
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-def456
Max-Forwards: 70
From: <sip:user@yourdomain.com>;tag=1234567890
To: <sip:user@yourdomain.com>
Call-ID: call-12345@192.168.1.100
CSeq: 2 REGISTER
Contact: <sip:user@192.168.1.100:5060>;expires=3600
Authorization: Digest username="user", realm="yourdomain.com", nonce="randomnonce123", uri="sip:yourdomain.com", response="calculated-md5-hash", algorithm=MD5
User-Agent: WindowsSipPhone/1.0
Content-Length: 0
```

#### Step 4: 200 OK Registration Success
```sip
SIP/2.0 200 OK
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-def456
From: <sip:user@yourdomain.com>;tag=1234567890
To: <sip:user@yourdomain.com>;tag=server-tag-456
Call-ID: call-12345@192.168.1.100
CSeq: 2 REGISTER
Contact: <sip:user@192.168.1.100:5060>;expires=3600
Server: Asterisk/18.0.0
Content-Length: 0
```

## 📞 SIP Call Setup Flow

The call setup process initiates and establishes audio sessions between two SIP endpoints.

### Outbound Call Sequence

```
Caller                    SIP Server               Callee
  |                          |                        |
  |--- INVITE -------------> |--- INVITE -----------> |  Step 1: Call initiation
  |                          |                        |
  |<-- 100 Trying ---------- |<-- 100 Trying -------- |  Step 2: Call processing
  |                          |                        |
  |<-- 180 Ringing --------- |<-- 180 Ringing ------- |  Step 3: Callee ringing
  |                          |                        |
  |<-- 200 OK -------------- |<-- 200 OK ------------ |  Step 4: Call answered
  |                          |                        |
  |--- ACK ----------------> |--- ACK --------------> |  Step 5: Acknowledge
  |                          |                        |
  |<========= RTP AUDIO STREAM =========>             |  Step 6: Media session
  |                          |                        |
  |--- BYE ----------------> |--- BYE --------------> |  Step 7: Call termination
  |                          |                        |
  |<-- 200 OK -------------- |<-- 200 OK ------------ |  Step 8: Termination confirmed
```

### Detailed Call Setup Messages

#### Step 1: INVITE Request
```sip
INVITE sip:callee@yourdomain.com SIP/2.0
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-invite123
Max-Forwards: 70
From: <sip:caller@yourdomain.com>;tag=caller-tag-789
To: <sip:callee@yourdomain.com>
Call-ID: call-invite-67890@192.168.1.100
CSeq: 1 INVITE
Contact: <sip:caller@192.168.1.100:5060>
User-Agent: WindowsSipPhone/1.0
Content-Type: application/sdp
Content-Length: 425

v=0
o=caller 123456789 987654321 IN IP4 192.168.1.100
s=SIP Call
c=IN IP4 192.168.1.100
t=0 0
m=audio 8000 RTP/AVP 0 8 18 101
a=rtpmap:0 PCMU/8000
a=rtpmap:8 PCMA/8000
a=rtpmap:18 G729/8000
a=rtpmap:101 telephone-event/8000
a=fmtp:101 0-16
a=sendrecv
```

#### Step 2: 100 Trying Response
```sip
SIP/2.0 100 Trying
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-invite123
From: <sip:caller@yourdomain.com>;tag=caller-tag-789
To: <sip:callee@yourdomain.com>
Call-ID: call-invite-67890@192.168.1.100
CSeq: 1 INVITE
Server: Asterisk/18.0.0
Content-Length: 0
```

#### Step 3: 180 Ringing Response
```sip
SIP/2.0 180 Ringing
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-invite123
From: <sip:caller@yourdomain.com>;tag=caller-tag-789
To: <sip:callee@yourdomain.com>;tag=callee-tag-321
Call-ID: call-invite-67890@192.168.1.100
CSeq: 1 INVITE
Contact: <sip:callee@192.168.1.200:5060>
Server: Asterisk/18.0.0
Content-Length: 0
```

#### Step 4: 200 OK Call Answered
```sip
SIP/2.0 200 OK
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-invite123
From: <sip:caller@yourdomain.com>;tag=caller-tag-789
To: <sip:callee@yourdomain.com>;tag=callee-tag-321
Call-ID: call-invite-67890@192.168.1.100
CSeq: 1 INVITE
Contact: <sip:callee@192.168.1.200:5060>
Server: Asterisk/18.0.0
Content-Type: application/sdp
Content-Length: 380

v=0
o=callee 987654321 123456789 IN IP4 192.168.1.200
s=SIP Call
c=IN IP4 192.168.1.200
t=0 0
m=audio 8002 RTP/AVP 0 8 101
a=rtpmap:0 PCMU/8000
a=rtpmap:8 PCMA/8000
a=rtpmap:101 telephone-event/8000
a=fmtp:101 0-16
a=sendrecv
```

#### Step 5: ACK Confirmation
```sip
ACK sip:callee@192.168.1.200:5060 SIP/2.0
Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK-ack456
Max-Forwards: 70
From: <sip:caller@yourdomain.com>;tag=caller-tag-789
To: <sip:callee@yourdomain.com>;tag=callee-tag-321
Call-ID: call-invite-67890@192.168.1.100
CSeq: 1 ACK
User-Agent: WindowsSipPhone/1.0
Content-Length: 0
```

## 🔑 SIP Authentication Process

The application implements SIP Digest Authentication as per RFC 3261.

### Authentication Algorithm (MD5)

```csharp
// Implementation in SipDigestAuth.cs
public string CalculateResponse(string username, string realm, string password, 
                              string nonce, string method, string uri)
{
    // HA1 = MD5(username:realm:password)
    string ha1 = CalculateMD5Hash($"{username}:{realm}:{password}");
    
    // HA2 = MD5(method:uri)
    string ha2 = CalculateMD5Hash($"{method}:{uri}");
    
    // Response = MD5(HA1:nonce:HA2)
    string response = CalculateMD5Hash($"{ha1}:{nonce}:{ha2}");
    
    return response;
}
```

### Authentication Header Format
```
Authorization: Digest username="user", 
                     realm="yourdomain.com", 
                     nonce="server-generated-nonce", 
                     uri="sip:yourdomain.com", 
                     response="calculated-md5-hash", 
                     algorithm=MD5
```

## 📊 Message Sequence Diagrams

### Complete Registration Flow
```
┌─────────┐                    ┌─────────────┐
│ Client  │                    │ SIP Server  │
└─────────┘                    └─────────────┘
     │                                │
     │ 1. REGISTER (no auth)          │
     │ ─────────────────────────────> │
     │                                │
     │ 2. 401 Unauthorized            │
     │    WWW-Authenticate: Digest    │
     │ <───────────────────────────── │
     │                                │
     │ 3. REGISTER (with auth)        │
     │    Authorization: Digest       │
     │ ─────────────────────────────> │
     │                                │
     │ 4. 200 OK                      │
     │    Registration successful     │
     │ <───────────────────────────── │
     │                                │
     │ 5. OPTIONS (keep-alive)        │
     │ <───────────────────────────── │
     │                                │
     │ 6. 200 OK                      │
     │ ─────────────────────────────> │
```

### Complete Call Setup Flow
```
┌─────────┐     ┌─────────────┐     ┌─────────┐
│ Caller  │     │ SIP Server  │     │ Callee  │
└─────────┘     └─────────────┘     └─────────┘
     │                │                   │
     │ 1. INVITE       │                   │
     │ ──────────────> │ 1. INVITE         │
     │                 │ ────────────────> │
     │                 │                   │
     │ 2. 100 Trying   │ 2. 100 Trying     │
     │ <────────────── │ <──────────────── │
     │                 │                   │
     │ 3. 180 Ringing  │ 3. 180 Ringing    │
     │ <────────────── │ <──────────────── │
     │                 │                   │
     │ 4. 200 OK       │ 4. 200 OK         │
     │    (with SDP)   │    (with SDP)     │
     │ <────────────── │ <──────────────── │
     │                 │                   │
     │ 5. ACK          │ 5. ACK            │
     │ ──────────────> │ ────────────────> │
     │                 │                   │
     │ ════════════════ RTP AUDIO ════════════════│
     │                 │                   │
     │ 6. BYE          │ 6. BYE            │
     │ ──────────────> │ ────────────────> │
     │                 │                   │
     │ 7. 200 OK       │ 7. 200 OK         │
     │ <────────────── │ <──────────────── │
```

## 🛠️ Protocol Implementation Details

### SDP (Session Description Protocol) Handling

The application creates and parses SDP offers/answers for media negotiation:

```csharp
// SDP Offer Creation (SdpManager.cs)
public string CreateSdpOffer(string localIpAddress, int rtpPort, List<string> supportedCodecs)
{
    var sdp = new StringBuilder();
    sdp.AppendLine("v=0");
    sdp.AppendLine($"o=WindowsSipPhone {DateTime.Now.Ticks} {DateTime.Now.Ticks + 1} IN IP4 {localIpAddress}");
    sdp.AppendLine("s=SIP Call");
    sdp.AppendLine($"c=IN IP4 {localIpAddress}");
    sdp.AppendLine("t=0 0");
    
    // Audio media line
    sdp.AppendLine($"m=audio {rtpPort} RTP/AVP 0 8 18 101");
    sdp.AppendLine("a=rtpmap:0 PCMU/8000");
    sdp.AppendLine("a=rtpmap:8 PCMA/8000");
    sdp.AppendLine("a=rtpmap:18 G729/8000");
    sdp.AppendLine("a=rtpmap:101 telephone-event/8000");
    sdp.AppendLine("a=fmtp:101 0-16");
    sdp.AppendLine("a=sendrecv");
    
    return sdp.ToString();
}
```

### Supported Audio Codecs

| Codec | Payload Type | Sample Rate | Description |
|-------|--------------|-------------|-------------|
| PCMU (μ-law) | 0 | 8000 Hz | G.711 μ-law |
| PCMA (A-law) | 8 | 8000 Hz | G.711 A-law |
| G729 | 18 | 8000 Hz | G.729 (optional) |
| telephone-event | 101 | 8000 Hz | DTMF tones |

### RTP Audio Stream Management

```csharp
// RTP Session Setup
public void StartRtpSession(string remoteIp, int remotePort, int localPort)
{
    // Create RTP session for audio transmission
    var rtpSession = new RtpSession(false, false, false);
    rtpSession.Start(localPort);
    
    // Set remote endpoint
    var remoteEndpoint = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
    rtpSession.SetDestination(SDPMediaTypesEnum.audio, remoteEndpoint, remoteEndpoint);
    
    // Start audio capture and playback
    StartAudioCapture();
    StartAudioPlayback();
}
```

## ⚠️ Error Handling and Edge Cases

### Common SIP Response Codes

| Code | Description | Handling |
|------|-------------|----------|
| 100 | Trying | Continue waiting for final response |
| 180 | Ringing | Show ringing indication to user |
| 200 | OK | Process successful response |
| 401 | Unauthorized | Implement authentication challenge |
| 404 | Not Found | Show "User not found" error |
| 408 | Request Timeout | Retry or show timeout error |
| 486 | Busy Here | Show "User busy" indication |
| 603 | Decline | Show "Call declined" message |

### Network Error Handling

```csharp
// Connection timeout handling
public async Task<bool> RegisterWithTimeoutAsync(int timeoutSeconds = 30)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
    
    try
    {
        await RegisterAsync(cts.Token);
        return true;
    }
    catch (OperationCanceledException)
    {
        throw new TimeoutException($"Registration timed out after {timeoutSeconds} seconds");
    }
    catch (Exception ex)
    {
        LogError($"Registration failed: {ex.Message}");
        return false;
    }
}
```

### Audio Quality Monitoring

The application includes comprehensive audio quality monitoring:

```csharp
// Audio quality metrics (from test infrastructure)
public class AudioQualityMetrics
{
    public double SignalToNoiseRatio { get; set; }
    public double DynamicRange { get; set; }
    public Dictionary<string, double> FrequencyDistribution { get; set; }
    public double QualityScore { get; set; } // 0-100 scale
    public bool PassesQualityThreshold => QualityScore >= 75.0;
}
```

## 🚀 Performance Characteristics

### Registration Performance SLAs
- **Registration Time**: < 5 seconds under normal network conditions
- **Re-registration**: < 3 seconds for keep-alive scenarios
- **Authentication Processing**: < 100ms for digest calculation

### Call Setup Performance SLAs
- **Call Setup Time**: < 3 seconds for successful connections
- **DTMF Response**: < 50ms for touch-tone processing
- **Audio Latency**: < 150ms end-to-end (network dependent)

### Memory and Resource Usage
- **Idle Memory Usage**: < 50MB when registered but not in call
- **Active Call Memory**: < 100MB during audio session
- **Network Bandwidth**: 64-128 kbps per active call (codec dependent)

## 📋 Integration Testing Coverage

The application includes comprehensive integration tests covering:

✅ **SIP Server Connectivity**: Verify connection to Asterisk SIP server  
✅ **Registration Flow**: End-to-end registration with authentication  
✅ **SDP Offer/Answer**: Media negotiation validation  
✅ **Digest Authentication**: Complete MD5 authentication process  
✅ **Call Recording**: Audio capture and quality analysis  
✅ **Protocol Compliance**: SIP message format validation  

For detailed testing procedures, see [TESTING_GUIDE.md](TESTING_GUIDE.md).

---

This documentation provides a complete technical reference for the SIP protocol implementation in the Windows SIP Phone application. For implementation details, see the source code in `SipPhoneService.cs`, `SdpManager.cs`, and `SipDigestAuth.cs`.