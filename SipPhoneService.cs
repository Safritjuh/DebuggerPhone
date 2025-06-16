using System;
using System.Threading.Tasks;

namespace WindowsSipPhone;

/// <summary>
/// Core SIP Phone Service - handles all SIP protocol operations
/// Uses SimpleSipClient for RFC 3261 compliant SIP communication
/// </summary>
public class SipPhoneService : IDisposable
{
    private SimpleSipClient? _sipClient;
    private bool _isRegistered = false;
    private string _serverAddress = "";
    private string _username = "";
    private int _port = 5060;    public bool IsRegistered 
    { 
        get 
        {
            var serviceRegistered = _isRegistered;
            var clientRegistered = _sipClient?.IsRegistered ?? false;
            
            // Return true if both service and client agree on registration
            // This ensures consistency between layers
            return serviceRegistered && clientRegistered;
        }
    }
    public string ServerAddress => _serverAddress;
    public SimpleSipClient? SipClient => _sipClient;
    public RtpAudioManager? RtpAudioManager => _sipClient?.AudioManager;
    
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? CallStateChanged;
    
    public SipPhoneService()
    {
        Console.WriteLine("SIP Phone Service initialized");
    }
      public async Task RegisterAsync(string username, string password, string server, int port, string userAgent = "Windows-SIP-Phone/2.0", int expires = 300)
    {
        _username = username;
        _serverAddress = server;
        _port = port;
        
        StatusChanged?.Invoke(this, "Connecting to SIP server...");
        MessageReceived?.Invoke(this, $"Attempting registration to {server}:{port} with user {username}");
        StatusChanged?.Invoke(this, $"🔍 DEBUG: Registration expires set to: {expires} seconds");
        
        try
        {
            // Disconnect existing client if any
            if (_sipClient != null)
            {
                _sipClient.Disconnect();
                _sipClient = null;
            }
              // Create new SIP client
            _sipClient = new SimpleSipClient(server, port, username, password, userAgent);
              // Wire up events
            _sipClient.StatusChanged += (s, status) => StatusChanged?.Invoke(this, status);
            _sipClient.MessageReceived += (s, message) => MessageReceived?.Invoke(this, message);
            _sipClient.IncomingCall += (s, callInfo) => CallStateChanged?.Invoke(this, $"Incoming call: {callInfo}");
            _sipClient.CallStateChanged += (s, callState) => CallStateChanged?.Invoke(this, callState);            // Connect to server
            var connected = await _sipClient.ConnectAsync();
            if (connected)
            {
                StatusChanged?.Invoke(this, "🔍 DEBUG: SIP client connected, attempting registration...");
                
                // Register with server using provided expires value
                var registered = await _sipClient.RegisterAsync(expires);
                StatusChanged?.Invoke(this, $"🔍 DEBUG: SIP client RegisterAsync returned: {registered}");
                StatusChanged?.Invoke(this, $"🔍 DEBUG: SIP client IsRegistered: {_sipClient.IsRegistered}");
                
                _isRegistered = registered && _sipClient.IsRegistered;
                StatusChanged?.Invoke(this, $"🔍 DEBUG: Service _isRegistered set to: {_isRegistered} (registered={registered} && client.IsRegistered={_sipClient.IsRegistered})");
                
                if (_isRegistered)
                {
                    StatusChanged?.Invoke(this, "✅ Registration successful - ready to make calls");
                }
                else
                {
                    StatusChanged?.Invoke(this, "❌ Registration failed - check credentials and server");
                }
            }
            else
            {
                StatusChanged?.Invoke(this, "❌ Failed to connect to SIP server");
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Registration error: {ex.Message}");
            MessageReceived?.Invoke(this, $"ERROR: {ex.Message}");
        }
    }public async Task UnregisterAsync()
    {
        if (_sipClient != null && _isRegistered)
        {
            StatusChanged?.Invoke(this, "Sending SIP unregister request...");
            
            try
            {
                // Use the RegistrationManager for proper SIP unregistration
                var unregisterResult = await _sipClient.UnregisterAsync();
                
                if (unregisterResult)
                {
                    StatusChanged?.Invoke(this, "✅ SIP unregistration successful");
                }
                else
                {
                    StatusChanged?.Invoke(this, "⚠️ SIP unregistration failed, disconnecting anyway");
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"⚠️ Unregister error: {ex.Message}, disconnecting anyway");
            }
            finally
            {
                // Always disconnect and cleanup regardless of unregister result
                _sipClient.Disconnect();
                _sipClient = null;
                _isRegistered = false;
            }
        }
        else if (_sipClient != null)
        {
            // Not registered, just disconnect
            StatusChanged?.Invoke(this, "Disconnecting from SIP server...");
            _sipClient.Disconnect();
            _sipClient = null;
            _isRegistered = false;
        }
        
        StatusChanged?.Invoke(this, "✅ Unregistered successfully");
        await Task.CompletedTask;
    }
      public async Task MakeCallAsync(string targetNumber)
    {
        StatusChanged?.Invoke(this, $"🔍 DEBUG: MakeCallAsync called - Service _isRegistered: {_isRegistered}");
        StatusChanged?.Invoke(this, $"🔍 DEBUG: SipClient null? {_sipClient == null}");
        if (_sipClient != null)
        {
            StatusChanged?.Invoke(this, $"🔍 DEBUG: SipClient.IsRegistered: {_sipClient.IsRegistered}");
        }
        
        if (_sipClient == null || !_isRegistered)
        {
            StatusChanged?.Invoke(this, $"Not registered - cannot make call (sipClient={_sipClient != null}, isRegistered={_isRegistered})");
            return;
        }
        
        try
        {
            StatusChanged?.Invoke(this, $"Calling {targetNumber}...");
            CallStateChanged?.Invoke(this, $"Outgoing call to {targetNumber}");
            
            await _sipClient.MakeCallAsync(targetNumber);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Call failed: {ex.Message}");
            CallStateChanged?.Invoke(this, "Call failed");
        }
    }
    
    public async Task HangupAsync()
    {
        if (_sipClient != null)
        {
            await _sipClient.HangupAsync();
            CallStateChanged?.Invoke(this, "Call ended");
        }
    }
      public async Task HangupCallAsync()
    {
        await HangupAsync();
    }
      public async Task AcceptIncomingCallAsync()
    {
        Console.WriteLine($"[SIP SERVICE DEBUG] AcceptIncomingCallAsync called");
        
        if (_sipClient == null || !_isRegistered)
        {
            Console.WriteLine($"[SIP SERVICE DEBUG] Cannot accept call - client: {_sipClient != null}, registered: {_isRegistered}");
            StatusChanged?.Invoke(this, "Not registered - cannot accept call");
            return;
        }
        
        try
        {
            Console.WriteLine($"[SIP SERVICE DEBUG] Accepting incoming call through SIP client");
            StatusChanged?.Invoke(this, "Accepting incoming call...");
            await _sipClient.AcceptIncomingCallAsync();
            Console.WriteLine($"[SIP SERVICE DEBUG] SIP client AcceptIncomingCallAsync completed");
            CallStateChanged?.Invoke(this, "Call accepted");        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SIP SERVICE DEBUG] Exception in AcceptIncomingCallAsync: {ex.Message}");
            Console.WriteLine($"[SIP SERVICE DEBUG] Stack trace: {ex.StackTrace}");
            StatusChanged?.Invoke(this, $"Failed to accept call: {ex.Message}");
            CallStateChanged?.Invoke(this, "Call accept failed");
        }
    }
    
    public async Task DeclineIncomingCallAsync()
    {
        if (_sipClient == null || !_isRegistered)
        {
            StatusChanged?.Invoke(this, "Not registered - cannot decline call");
            return;
        }
        
        try
        {
            StatusChanged?.Invoke(this, "Declining incoming call...");
            await _sipClient.DeclineIncomingCallAsync();
            CallStateChanged?.Invoke(this, "Call declined");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Failed to decline call: {ex.Message}");
            CallStateChanged?.Invoke(this, "Call decline failed");
        }    }
    
    /// <summary>
    /// Send an UPDATE request to modify session parameters
    /// </summary>
    /// <param name="sdpContent">Optional SDP content for media re-negotiation</param>
    /// <returns>True if UPDATE was sent successfully</returns>
    public async Task<bool> SendUpdateAsync(string sdpContent = "")
    {
        if (_sipClient == null || !_isRegistered)
        {
            StatusChanged?.Invoke(this, "Not registered - cannot send UPDATE");
            return false;
        }
        
        try
        {
            StatusChanged?.Invoke(this, "Sending session UPDATE...");
            return await _sipClient.SendUpdateAsync(sdpContent);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"UPDATE failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Send a REFER request for call transfer
    /// </summary>
    /// <param name="referToUri">URI to transfer the call to (e.g., "sip:1234@example.com")</param>
    /// <param name="replaces">Optional Replaces header for attended transfer</param>
    /// <returns>True if REFER was sent successfully</returns>
    public async Task<bool> SendReferAsync(string referToUri, string replaces = "")
    {
        if (_sipClient == null || !_isRegistered)
        {
            StatusChanged?.Invoke(this, "Not registered - cannot send REFER");
            return false;
        }
        
        try
        {
            StatusChanged?.Invoke(this, $"Sending call transfer to {referToUri}...");
            return await _sipClient.SendReferAsync(referToUri, replaces);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"REFER failed: {ex.Message}");
            return false;
        }    }
    
    /// <summary>
    /// Put the current call on hold using SIP re-INVITE with inactive SDP
    /// </summary>
    /// <returns>True if hold was successful</returns>
    public async Task<bool> HoldCallAsync()
    {
        Console.WriteLine("=== [SERVICE DEBUG] HOLD CALL ASYNC ===");
        Console.WriteLine($"[SERVICE DEBUG] SipClient Available: {_sipClient != null}");
        Console.WriteLine($"[SERVICE DEBUG] Service Registered: {_isRegistered}");
        Console.WriteLine($"[SERVICE DEBUG] SipClient Registered: {_sipClient?.IsRegistered ?? false}");
        
        if (_sipClient == null || !_isRegistered)
        {
            Console.WriteLine("[SERVICE DEBUG] ❌ HOLD FAILED: Not registered or no SIP client");
            StatusChanged?.Invoke(this, "Not registered - cannot hold call");
            return false;
        }
        
        try
        {
            Console.WriteLine("[SERVICE DEBUG] 🔄 Starting hold operation...");
            StatusChanged?.Invoke(this, "Putting call on hold...");
            
            bool result = await _sipClient.HoldCallAsync();
            
            Console.WriteLine($"[SERVICE DEBUG] Hold operation result: {result}");
            if (result)
            {
                Console.WriteLine("[SERVICE DEBUG] ✅ Hold operation completed successfully");
            }
            else
            {
                Console.WriteLine("[SERVICE DEBUG] ❌ Hold operation failed (returned false)");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVICE DEBUG] ❌ HOLD EXCEPTION: {ex.Message}");
            Console.WriteLine($"[SERVICE DEBUG] Exception Stack Trace: {ex.StackTrace}");
            StatusChanged?.Invoke(this, $"Hold failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Resume a call that was put on hold using SIP re-INVITE with active SDP
    /// </summary>
    /// <returns>True if resume was successful</returns>
    public async Task<bool> ResumeCallAsync()
    {
        Console.WriteLine("=== [SERVICE DEBUG] RESUME CALL ASYNC ===");
        Console.WriteLine($"[SERVICE DEBUG] SipClient Available: {_sipClient != null}");
        Console.WriteLine($"[SERVICE DEBUG] Service Registered: {_isRegistered}");
        Console.WriteLine($"[SERVICE DEBUG] SipClient Registered: {_sipClient?.IsRegistered ?? false}");
        
        if (_sipClient == null || !_isRegistered)
        {
            Console.WriteLine("[SERVICE DEBUG] ❌ RESUME FAILED: Not registered or no SIP client");
            StatusChanged?.Invoke(this, "Not registered - cannot resume call");
            return false;
        }
        
        try
        {
            Console.WriteLine("[SERVICE DEBUG] 🔄 Starting resume operation...");
            StatusChanged?.Invoke(this, "Resuming call...");
            
            bool result = await _sipClient.ResumeCallAsync();
            
            Console.WriteLine($"[SERVICE DEBUG] Resume operation result: {result}");
            if (result)
            {
                Console.WriteLine("[SERVICE DEBUG] ✅ Resume operation completed successfully");
            }
            else
            {
                Console.WriteLine("[SERVICE DEBUG] ❌ Resume operation failed (returned false)");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVICE DEBUG] ❌ RESUME EXCEPTION: {ex.Message}");
            Console.WriteLine($"[SERVICE DEBUG] Exception Stack Trace: {ex.StackTrace}");
            StatusChanged?.Invoke(this, $"Resume failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get audio manager for advanced audio control
    /// </summary>
    public RtpAudioManager? GetAudioManager()
    {
        return _sipClient?.AudioManager;
    }
    
    /// <summary>
    /// Set audio volume level (0.0 to 1.0)
    /// </summary>
    public void SetAudioVolume(double volume)
    {
        // Audio volume control is handled by the RTP audio manager
        // This method is for future extension when volume control is implemented
        Console.WriteLine($"[AUDIO] Volume set to: {volume:P0}");
    }
    
    public string GetConnectionStatus()
    {
        return $"""
            Registration Status: {(_isRegistered ? "Registered" : "Not Registered")}
            Server: {_serverAddress}:{_port}
            Username: {_username}
            Protocol: TCP (Direct SIP connection)
            
            SIP Client Status:
            - SimpleSipClient: {(_sipClient != null ? "Active" : "Inactive")}
            - Connection: {(_sipClient != null && _isRegistered ? "Connected" : "Disconnected")}
            
            Features Implemented:
            ✅ TCP SIP Transport
            ✅ SIP REGISTER Message
            ✅ SIP INVITE Message
            ✅ SIP BYE Message
            ✅ SIP UPDATE Message
            ✅ SIP REFER Message
            ✅ Message Logging & Debugging
            ✅ Digest Authentication
            ✅ RTP Audio Streaming
            
            Advanced Features:
            🔧 Session Parameter Updates (UPDATE)
            🔧 Call Transfer Support (REFER)
            🔧 Audio Controls & Settings
            """;
    }

    public void Dispose()
    {
        if (_sipClient != null)
        {
            _sipClient.Disconnect();
            _sipClient = null;
        }
        
        _isRegistered = false;
    }
}
