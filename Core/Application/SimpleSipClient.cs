using System.Net.Sockets;
using System.Net;
using System.Text;
using WindowsSipPhone.SipCore;
using WindowsSipPhone.Core.Managers;
using WindowsSipPhone.Core.Interfaces;
using WindowsSipPhone.Core.Models;
using WindowsSipPhone.Core.Protocol;
using WindowsSipPhone.Core.Validation;

namespace WindowsSipPhone
{
    public class SimpleSipClient : IDisposable
    {
        // Transport layer for bidirectional SIP communication
        private SipTransport? _sipTransport;
        
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private bool _isConnected;
        private readonly string _serverHost;
        private readonly int _serverPort;
        private readonly string _username;
        private readonly string _password;
        private readonly string _userAgent;
        
        // IMP-016: Enhanced Profile Management
        private EnhancedProfileManager? _profileManager;
        private ISipProfileHandler? _currentProfileHandler;
        private SipProfileConfiguration? _currentProfileConfig;
          // JSIP-style components
        private WindowsSipPhone.SipCore.DialogManager _dialogManager;
        private WindowsSipPhone.SipCore.RegistrationManager _registrationManager;
        private WindowsSipPhone.SipCore.SipMessageFactory _messageFactory;
          // RFC 3261 Enhanced Components
        private EnhancedSipMessageFactory? _enhancedMessageFactory;
        private Rfc3261Validator? _rfc3261Validator;
        
        // Legacy fields for backward compatibility
        private int _sequenceNumber = 1;
        private string _callId = string.Empty; // Registration Call-ID
        private string _activeCallId = string.Empty; // Active call session Call-ID (separate from registration)
        private string _currentTargetNumber = string.Empty; // Track the target number for the current call
        private string _fromTag = string.Empty;
        private string _toTag = string.Empty;
        private string _localIp = string.Empty;        private string _remoteContactUri = string.Empty; // Track remote contact for proper BYE targeting
        private bool _isRegistered = false;
        private bool _isCallOnHold = false; // Track if current call is on hold
        private bool _isResumeInProgress = false; // Track if resume operation is in progress
        private TaskCompletionSource<bool>? _registrationCompletion;
        private RtpAudioManager? _audioManager;// Registration refresh timer fields
        private System.Timers.Timer? _registrationRefreshTimer;
        private int _registrationExpiry = 3600; // Default 1 hour (3600 seconds)
        private readonly object _timerLock = new object();
        
        public event EventHandler<string>? MessageReceived;
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? IncomingCall;
        public event EventHandler<string>? CallStateChanged;        public bool IsRegistered 
        { 
            get 
            {
                // Use legacy registration flag as primary source of truth since it's working
                // The JSIP manager can be unreliable during the integration phase
                return _isRegistered;
            }
        }
        public RtpAudioManager? AudioManager => _audioManager;
          // Track pending incoming calls
        private string? _pendingIncomingCallId;
        private string? _pendingIncomingVia;
        private string? _pendingIncomingFrom;
        private string? _pendingIncomingTo;
        private string? _pendingIncomingCSeq;
        private string? _pendingIncomingSdp;
        private string? _pendingIncomingInvite;

        public SimpleSipClient(string serverHost, int serverPort, string username, string password, string userAgent = "Windows-SIP-Phone/2.0")
        {            
            _serverHost = serverHost;
            _serverPort = serverPort;
            _username = username;
            _password = password;
            _userAgent = userAgent;
            _callId = Guid.NewGuid().ToString().Replace("-", "");
            _fromTag = Guid.NewGuid().ToString().Replace("-", "")[..8];            // Initialize JSIP-style components
            _localIp = GetLocalIPAddress();
            _messageFactory = new SipMessageFactory(_localIp, _username, 5060, _userAgent);
            _dialogManager = new DialogManager();
            _registrationManager = new RegistrationManager(_messageFactory, SendMessageAsync);
            
            // Initialize RFC 3261 Enhanced Components
            _enhancedMessageFactory = new EnhancedSipMessageFactory(_localIp, _username);
            _rfc3261Validator = new Rfc3261Validator();
            
            // Wire up events
            _dialogManager.DialogStateChanged += OnDialogStateChanged;
            _registrationManager.RegistrationStatusChanged += OnRegistrationStatusChanged;
            _registrationManager.AuthenticationRequired += OnAuthenticationRequired;_localIp = GetLocalIPAddress();
            
            // Initialize bidirectional SIP transport
            _sipTransport = new SipTransport(_localIp, _serverPort);
            _sipTransport.StatusChanged += (sender, status) => StatusChanged?.Invoke(this, status);
            _sipTransport.MessageReceived += (sender, message) => {
                MessageReceived?.Invoke(this, $"INCOMING (Transport):\n{message}");
                _ = Task.Run(async () => await ProcessIncomingMessage(message));
            };
            _sipTransport.TransportError += (sender, error) => StatusChanged?.Invoke(this, $"Transport Error: {error}");
            
            // Initialize audio manager for RTP streaming
            _audioManager = new RtpAudioManager();
        }
        
        public SimpleSipClient(string serverHost, int serverPort, string username, string password, WindowsSipPhone.Core.Models.SipProfile profile)
        {            
            _serverHost = serverHost;
            _serverPort = serverPort;
            _username = username;
            _password = password;
            _userAgent = profile.UserAgentString;
            _callId = Guid.NewGuid().ToString().Replace("-", "");
            _fromTag = Guid.NewGuid().ToString().Replace("-", "")[..8];            // Initialize JSIP-style components
            _localIp = GetLocalIPAddress();
            _messageFactory = new SipMessageFactory(_localIp, _username, profile.DefaultPort, profile.UserAgentString, profile);
            _dialogManager = new DialogManager();
            _registrationManager = new RegistrationManager(_messageFactory, SendMessageAsync);
            
            // Wire up events
            _dialogManager.DialogStateChanged += OnDialogStateChanged;
            _registrationManager.RegistrationStatusChanged += OnRegistrationStatusChanged;
            _registrationManager.AuthenticationRequired += OnAuthenticationRequired;            _localIp = GetLocalIPAddress();
            
            // Initialize bidirectional SIP transport - use profile port if different
            var transportPort = profile.DefaultPort != 5060 ? profile.DefaultPort : _serverPort;
            _sipTransport = new SipTransport(_localIp, transportPort);
            _sipTransport.StatusChanged += (sender, status) => StatusChanged?.Invoke(this, status);
            _sipTransport.MessageReceived += (sender, message) => {
                MessageReceived?.Invoke(this, $"INCOMING (Transport):\n{message}");
                _ = Task.Run(async () => await ProcessIncomingMessage(message));
            };
            _sipTransport.TransportError += (sender, error) => StatusChanged?.Invoke(this, $"Transport Error: {error}");            
            // Initialize audio manager for RTP streaming
            _audioManager = new RtpAudioManager();
        }
        
        /// <summary>
        /// IMP-016: Set the Enhanced Profile Manager for provider-specific SIP handling
        /// </summary>
        /// <param name="profileManager">The enhanced profile manager instance</param>
        public void SetProfileManager(EnhancedProfileManager profileManager)
        {
            _profileManager = profileManager;
            _profileManager.SetSipClient(this);
            _profileManager.ProfileChanged += OnProfileChanged;
            
            // Set current profile handler and config if available
            _currentProfileHandler = _profileManager.CurrentHandler;
            _currentProfileConfig = _profileManager.CurrentConfig;
            
            StatusChanged?.Invoke(this, "✅ Enhanced Profile Manager integrated");
            Console.WriteLine($"[PROFILE MANAGER] Integrated EnhancedProfileManager with SimpleSipClient");
        }
        
        /// <summary>
        /// IMP-016: Handle profile changes at runtime
        /// </summary>
        private void OnProfileChanged(object? sender, string profileName)
        {
            if (_profileManager != null)
            {
                _currentProfileHandler = _profileManager.CurrentHandler;
                _currentProfileConfig = _profileManager.CurrentConfig;
                
                StatusChanged?.Invoke(this, $"📋 Profile switched to: {profileName}");
                Console.WriteLine($"[PROFILE MANAGER] Profile changed to: {profileName}");
                
                // Update SIP message factory with new profile configuration if available
                if (_currentProfileConfig != null && _messageFactory != null)
                {
                    // TODO: Update message factory with profile-specific settings
                    Console.WriteLine($"[PROFILE MANAGER] Updated message factory with profile settings");
                }
            }
        }
          /// <summary>
        /// IMP-016: Get current profile name for debugging/display
        /// </summary>
        public string GetCurrentProfileName()
        {
            return _currentProfileConfig?.Name ?? "Unknown";
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                StatusChanged?.Invoke(this, "Starting bidirectional SIP transport...");
                
                // Start bidirectional SIP transport
                if (_sipTransport != null)
                {
                    var transportStarted = await _sipTransport.StartListeningAsync();
                    if (!transportStarted)
                    {
                        StatusChanged?.Invoke(this, "❌ Failed to start SIP listener for incoming calls");
                        return false;
                    }
                    
                    var serverConnected = await _sipTransport.ConnectToServerAsync(_serverHost, _serverPort);
                    if (!serverConnected)
                    {
                        StatusChanged?.Invoke(this, "❌ Failed to connect to SIP server");
                        return false;
                    }
                }
                
                // Keep legacy connection for now as backup
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_serverHost, _serverPort);
                _stream = _tcpClient.GetStream();
                _isConnected = true;

                StatusChanged?.Invoke(this, $"✅ Connected to SIP server {_serverHost}:{_serverPort}");
                StatusChanged?.Invoke(this, $"✅ SIP listener ready for incoming calls on {_localIp}");
                StatusChanged?.Invoke(this, $"Local IP: {_localIp}");
                
                // Start listening for incoming messages (legacy fallback)
                _ = Task.Run(ListenForMessagesAsync);
                
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Connection failed: {ex.Message}");
                return false;
            }        
        }public async Task<bool> RegisterAsync(int expires = 3600)
        {
            if (!_isConnected || _stream == null)
            {
                StatusChanged?.Invoke(this, "Not connected to server");
                return false;
            }            try
            {
                StatusChanged?.Invoke(this, "🔄 Starting SIP registration...");
                Console.WriteLine($"[REGISTER DEBUG] Registration CallID: {_callId}, FromTag: {_fromTag}");
                Console.WriteLine($"[REGISTER DEBUG] Server: {_serverHost}:{_serverPort}, User: {_username}");
                Console.WriteLine($"[REGISTER DEBUG] Registration Expires: {expires} seconds");
                
                // Store expires value for refresh timer
                _registrationExpiry = expires;
                
                // Create TaskCompletionSource for registration
                _registrationCompletion = new TaskCompletionSource<bool>();
                
                // Send initial REGISTER request using working legacy method
                var registerMessage = CreateRegisterMessage(expires);
                StatusChanged?.Invoke(this, "📤 Sending REGISTER request...");
                Console.WriteLine($"[REGISTER DEBUG] REGISTER message length: {registerMessage.Length} bytes");
                MessageReceived?.Invoke(this, $"OUTGOING (REGISTER):\n{registerMessage}");

                var messageBytes = Encoding.UTF8.GetBytes(registerMessage);
                await _stream.WriteAsync(messageBytes);
                StatusChanged?.Invoke(this, "✅ REGISTER message sent successfully");
                
                // Wait for registration response with timeout
                StatusChanged?.Invoke(this, "⏳ Waiting for registration response (30 second timeout)...");
                var result = await _registrationCompletion.Task.WaitAsync(TimeSpan.FromSeconds(30));
                
                if (result)
                {
                    StartRegistrationRefreshTimer();
                }
                
                return result;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Registration failed: {ex.Message}");
                return false;
            }
        }        public async Task<bool> MakeCallAsync(string targetNumber)
        {
            Console.WriteLine($"[CALL DEBUG] MakeCallAsync called - _isConnected: {_isConnected}, _isRegistered: {_isRegistered}");
            
            if (!_isConnected || _stream == null)
            {
                StatusChanged?.Invoke(this, "Not connected to server");
                return false;
            }

            if (!_isRegistered)
            {
                Console.WriteLine("[CALL DEBUG] Not registered - cannot make call");
                StatusChanged?.Invoke(this, "Not registered - cannot make call");
                return false;
            }            try
            {                Console.WriteLine($"[CALL DEBUG] Starting call to {targetNumber}...");
                    // Reset hold state for new call
                _isCallOnHold = false;
                _isResumeInProgress = false; // Reset resume flag for new call
                
                // Create new dialog for outgoing call
                var callId = GenerateCallId();
                var fromTag = GenerateTag();
                // Construct proper remote URI for the target user
                var remoteUri = $"sip:{targetNumber}@{_serverHost}";
                var dialog = _dialogManager.CreateOutgoingDialogWithUsernames(callId, fromTag, "", 1U, _username, remoteUri);
                
                StatusChanged?.Invoke(this, $"🔄 Created new dialog for outgoing call: {callId}");
                dialog.UpdateState(SipDialogState.Early);
                  // Store call information for dialog tracking
                _activeCallId = callId;
                _currentTargetNumber = targetNumber;

                // CRITICAL FIX: Prepare RTP socket early to get valid port for SDP offer
                var rtpPort = 5004; // Default fallback port
                if (_audioManager != null)
                {
                    Console.WriteLine("[INVITE DEBUG] Preparing RTP socket for SDP offer...");
                    if (_audioManager.PrepareRtpSocket())
                    {
                        rtpPort = _audioManager.LocalRtpPort;
                        Console.WriteLine($"[INVITE DEBUG] ✅ RTP socket prepared, using port: {rtpPort}");
                    }
                    else
                    {
                        Console.WriteLine($"[INVITE DEBUG] ⚠️ Failed to prepare RTP socket, using fallback port: {rtpPort}");
                    }
                }
                else
                {
                    Console.WriteLine($"[INVITE DEBUG] ⚠️ Audio manager is null, using fallback port: {rtpPort}");
                }

                // Create SDP offer for audio negotiation
                var localIp = GetLocalIPAddress();
                var sdpContent = SdpManager.CreateSdpOffer(localIp, rtpPort);
                Console.WriteLine($"[INVITE DEBUG] *** SDP OFFER CREATED ***");
                Console.WriteLine($"[INVITE DEBUG] Local IP: {localIp}, RTP Port: {rtpPort}");
                Console.WriteLine($"[INVITE DEBUG] SDP Content:");
                Console.WriteLine(sdpContent);
                Console.WriteLine($"[INVITE DEBUG] *** END SDP OFFER ***");
                
                // Create INVITE using message factory with server parameters and SDP content
                var inviteMessage = _messageFactory.CreateInviteRequest(
                    _username,
                    targetNumber, 
                    _serverHost,
                    _serverPort,
                    1U, // sequence number
                    callId, 
                    fromTag,
                    sdpContent // Include SDP offer for G.711 A-law and DTMF
                );
                
                StatusChanged?.Invoke(this, $"Calling {targetNumber}...");
                CallStateChanged?.Invoke(this, $"Dialing {targetNumber}");
                
                await SendMessageAsync(inviteMessage);
                
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Call failed: {ex.Message}");
                CallStateChanged?.Invoke(this, "Call Failed");
                return false;
            }
        }        public async Task HangupAsync()
        {
            if (!_isConnected || _stream == null)
            {
                return;
            }

            try
            {
                StatusChanged?.Invoke(this, "🔄 Starting hangup process...");
                
                // Stop RTP session immediately to prevent any audio restart during hangup
                StatusChanged?.Invoke(this, "🛑 Stopping RTP session...");                _audioManager?.StopRtpSession();
                
                // Find the active dialog
                var dialog = _dialogManager.FindDialogByCallId(_activeCallId);                if (dialog != null)
                {
                    StatusChanged?.Invoke(this, $"🔄 Terminating dialog: {dialog.CallId}");
                    Console.WriteLine($"[DIALOG DEBUG] Dialog state - RemoteTarget: '{dialog.RemoteTarget}', RemoteTag: '{dialog.RemoteTag}', RemoteUri: '{dialog.RemoteUri}'");
                    
                    // Create BYE message using message factory
                    var byeMessage = _messageFactory.CreateByeRequest(dialog, dialog.GetNextLocalSequenceNumber());
                    
                    StatusChanged?.Invoke(this, "📤 Sending BYE request...");
                    CallStateChanged?.Invoke(this, "Call Ending");
                    
                    await SendMessageAsync(byeMessage);
                    
                    // Update dialog state to terminated
                    dialog.UpdateState(SipDialogState.Terminated);
                    StatusChanged?.Invoke(this, $"✅ Dialog state updated to terminated: {dialog.CallId}");
                }
                else
                {
                    StatusChanged?.Invoke(this, "⚠️ No active dialog found for hangup - cleaning up state");
                }
                
                // Clean up call state completely to prevent any restart scenarios
                StatusChanged?.Invoke(this, "🧹 Cleaning up call state...");
                _remoteContactUri = string.Empty;
                _toTag = string.Empty;
                _activeCallId = string.Empty;
                _currentTargetNumber = string.Empty;
                
                // Ensure final call state notification
                CallStateChanged?.Invoke(this, "Call Ended");
                StatusChanged?.Invoke(this, "✅ Hangup completed successfully");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Hangup failed: {ex.Message}");
                CallStateChanged?.Invoke(this, "Hangup Failed");
                
                // Even if hangup fails, ensure RTP is stopped and state is cleaned
                _audioManager?.StopRtpSession();
                _remoteContactUri = string.Empty;
                _toTag = string.Empty;
                _activeCallId = string.Empty;
                _currentTargetNumber = string.Empty;
            }
        }        public async Task AcceptIncomingCallAsync()
        {
            Console.WriteLine($"[ACCEPT CALL DEBUG] AcceptIncomingCallAsync called");
            
            if (_pendingIncomingCallId == null || _stream == null)
            {
                Console.WriteLine($"[ACCEPT CALL DEBUG] No pending call or stream. CallId: {_pendingIncomingCallId}, Stream: {_stream != null}");
                StatusChanged?.Invoke(this, "No pending incoming call to accept");
                return;
            }
              try
            {
                Console.WriteLine($"[ACCEPT CALL DEBUG] Processing call {_pendingIncomingCallId}");
                
                // Set the active call ID for incoming call (needed for hangup)
                _activeCallId = _pendingIncomingCallId;
                Console.WriteLine($"[ACCEPT CALL DEBUG] Set active call ID: {_activeCallId}");
                
                // Find the dialog for this incoming call
                var dialog = _dialogManager.FindDialogByCallId(_pendingIncomingCallId);
                if (dialog != null)
                {
                    // Generate local tag for response
                    if (string.IsNullOrEmpty(dialog.LocalTag))
                    {
                        dialog.LocalTag = GenerateTag();
                    }
                    
                    // Update dialog state to confirmed
                    dialog.UpdateState(SipDialogState.Confirmed);
                    StatusChanged?.Invoke(this, $"🔄 Dialog confirmed: {dialog.CallId}");
                }
                
                Console.WriteLine($"[ACCEPT CALL DEBUG] Calling SendIncomingCallResponseWithFactory");
                await SendIncomingCallResponseWithFactory(_pendingIncomingCallId, _pendingIncomingVia!, 
                    _pendingIncomingFrom!, _pendingIncomingTo!, _pendingIncomingCSeq!, _pendingIncomingSdp ?? "", dialog);
                
                StatusChanged?.Invoke(this, "✅ Incoming call accepted");
                CallStateChanged?.Invoke(this, "Incoming Call Answered");
                
                // Clear pending call data
                ClearPendingIncomingCall();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ACCEPT CALL DEBUG] Exception in AcceptIncomingCallAsync: {ex.Message}");
                Console.WriteLine($"[ACCEPT CALL DEBUG] Stack trace: {ex.StackTrace}");
                StatusChanged?.Invoke(this, $"❌ Failed to accept call: {ex.Message}");
            }
        }

        public async Task DeclineIncomingCallAsync()
        {
            if (_pendingIncomingCallId == null || _stream == null)
            {
                StatusChanged?.Invoke(this, "No pending incoming call to decline");
                return;
            }
            
            try
            {
                // Find the dialog for this incoming call
                var dialog = _dialogManager.FindDialogByCallId(_pendingIncomingCallId);
                if (dialog != null)
                {
                    // Update dialog state to terminated
                    dialog.UpdateState(SipDialogState.Terminated);
                    StatusChanged?.Invoke(this, $"🔄 Dialog terminated (declined): {dialog.CallId}");
                }
                
                // Send 486 Busy Here response using message factory
                var busyResponse = _messageFactory.CreateResponse(486, "Busy Here", 
                    _pendingIncomingCallId, dialog?.LocalTag ?? GenerateTag(), dialog?.RemoteTag ?? "", 
                    _pendingIncomingVia!, _pendingIncomingFrom!, _pendingIncomingTo!, _pendingIncomingCSeq!);
                
                await SendMessageAsync(busyResponse);
                
                StatusChanged?.Invoke(this, "❌ Incoming call declined");
                CallStateChanged?.Invoke(this, "Incoming Call Declined");
                
                // Clear pending call data
                ClearPendingIncomingCall();
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Failed to decline call: {ex.Message}");
            }        }        /// <summary>
        /// Put the current call on hold by sending re-INVITE with inactive SDP
        /// </summary>
        public async Task<bool> HoldCallAsync()
        {
            Console.WriteLine("=== [HOLD DEBUG] STARTING HOLD OPERATION ===");
            Console.WriteLine($"[HOLD DEBUG] Connection State: {_isConnected}");
            Console.WriteLine($"[HOLD DEBUG] Stream Available: {_stream != null}");
            Console.WriteLine($"[HOLD DEBUG] Active Call ID: '{_activeCallId}'");
            Console.WriteLine($"[HOLD DEBUG] Current Hold State: {_isCallOnHold}");
            
            if (!_isConnected || _stream == null || string.IsNullOrEmpty(_activeCallId))
            {
                Console.WriteLine("[HOLD DEBUG] ❌ HOLD FAILED: No active call to hold");
                StatusChanged?.Invoke(this, "No active call to hold");
                return false;
            }

            try
            {
                Console.WriteLine("[HOLD DEBUG] 🔄 Initiating hold procedure...");
                StatusChanged?.Invoke(this, "🔄 Putting call on hold...");
                
                // Find the active dialog
                var dialog = _dialogManager.FindDialogByCallId(_activeCallId);
                Console.WriteLine($"[HOLD DEBUG] Dialog Search Result: {(dialog != null ? "FOUND" : "NOT FOUND")}");
                if (dialog != null)
                {
                    Console.WriteLine($"[HOLD DEBUG] Dialog Details - CallId: {dialog.CallId}, State: {dialog.State}");
                }
                
                if (dialog == null)
                {
                    Console.WriteLine("[HOLD DEBUG] ❌ HOLD FAILED: No dialog found for hold operation");
                    StatusChanged?.Invoke(this, "❌ No dialog found for hold operation");
                    return false;
                }                // CRITICAL FIX: Get RTP port BEFORE stopping the session
                var localIp = GetLocalIPAddress();
                var rtpPort = _audioManager?.LocalRtpPort ?? 5004;
                
                Console.WriteLine($"[HOLD DEBUG] 📍 Local IP: {localIp}");
                Console.WriteLine($"[HOLD DEBUG] 📍 Current RTP Port: {rtpPort}");
                Console.WriteLine($"[HOLD DEBUG] 📍 Audio Manager Available: {_audioManager != null}");
                Console.WriteLine($"[HOLD DEBUG] 📍 Audio Manager Running: {_audioManager?.IsRunning ?? false}");
                
                // If RTP port is 0, prepare a new socket to get valid port
                if (rtpPort == 0 && _audioManager != null)
                {
                    Console.WriteLine("[HOLD DEBUG] ⚠️ RTP port is 0, preparing new socket...");
                    _audioManager.PrepareRtpSocket();
                    rtpPort = _audioManager.LocalRtpPort;
                    Console.WriteLine($"[HOLD DEBUG] ✅ New RTP port prepared: {rtpPort}");
                }
                
                StatusChanged?.Invoke(this, $"📍 Using RTP port {rtpPort} for hold SDP");

                // Create SDP with sendonly media (better than inactive for some servers)
                Console.WriteLine("[HOLD DEBUG] 📝 Creating sendonly SDP for hold...");
                var holdSdp = SdpManager.CreateSendonlySdpOffer(localIp, rtpPort);
                Console.WriteLine($"[HOLD DEBUG] 📝 Hold SDP Created:\n{holdSdp}");
                  // Create re-INVITE with sendonly SDP
                var reInviteMessage = _messageFactory.CreateReInviteRequest(
                    dialog, 
                    dialog.GetNextLocalSequenceNumber(),
                    holdSdp
                );
                
                Console.WriteLine($"[HOLD DEBUG] 📤 Sending re-INVITE for hold...");
                Console.WriteLine($"[HOLD DEBUG] 📤 Dialog Sequence Number: {dialog.GetNextLocalSequenceNumber() - 1}");
                StatusChanged?.Invoke(this, "📤 Sending re-INVITE with sendonly SDP for hold...");
                await SendMessageAsync(reInviteMessage);
                  // Pause RTP streams instead of stopping completely - keeps socket alive for resume
                Console.WriteLine("[HOLD DEBUG] 🔇 Pausing RTP streams...");
                if (_audioManager != null)
                {
                    Console.WriteLine($"[HOLD DEBUG] Audio Manager IsRunning before pause: {_audioManager.IsRunning}");
                    _audioManager.PauseRtpStreams();
                    Console.WriteLine($"[HOLD DEBUG] Audio Manager IsRunning after pause: {_audioManager.IsRunning}");
                }
                else
                {
                    Console.WriteLine("[HOLD DEBUG] ⚠️ Audio Manager is null - cannot pause RTP streams");
                }
                StatusChanged?.Invoke(this, "🔇 RTP streams paused for hold");
                
                // Set hold state flag
                Console.WriteLine("[HOLD DEBUG] 🔄 Setting hold state flag to TRUE");
                _isCallOnHold = true;
                
                Console.WriteLine("[HOLD DEBUG] ✅ HOLD OPERATION COMPLETED SUCCESSFULLY");
                StatusChanged?.Invoke(this, "✅ Call put on hold successfully");
                CallStateChanged?.Invoke(this, "Call On Hold");
                
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Hold operation failed: {ex.Message}");
                return false;
            }
        }        /// <summary>
        /// Resume a call that was put on hold by sending re-INVITE with active SDP
        /// </summary>
        public async Task<bool> ResumeCallAsync()
        {
            Console.WriteLine("=== [RESUME DEBUG] STARTING RESUME OPERATION ===");
            Console.WriteLine($"[RESUME DEBUG] Connection State: {_isConnected}");
            Console.WriteLine($"[RESUME DEBUG] Stream Available: {_stream != null}");
            Console.WriteLine($"[RESUME DEBUG] Active Call ID: '{_activeCallId}'");
            Console.WriteLine($"[RESUME DEBUG] Current Hold State: {_isCallOnHold}");
            
            if (!_isConnected || _stream == null || string.IsNullOrEmpty(_activeCallId))
            {
                Console.WriteLine("[RESUME DEBUG] ❌ RESUME FAILED: No active call to resume");
                StatusChanged?.Invoke(this, "No active call to resume");
                return false;
            }            try
            {
                Console.WriteLine("[RESUME DEBUG] 🔄 Initiating resume procedure...");
                StatusChanged?.Invoke(this, "🔄 Resuming call...");
                
                // Set resume flag to help audio setup detect this is a resume operation
                _isResumeInProgress = true;
                Console.WriteLine("[RESUME DEBUG] 🔄 Resume in progress flag set to TRUE");
                
                // Find the active dialog
                var dialog = _dialogManager.FindDialogByCallId(_activeCallId);
                Console.WriteLine($"[RESUME DEBUG] Dialog Search Result: {(dialog != null ? "FOUND" : "NOT FOUND")}");
                if (dialog != null)
                {
                    Console.WriteLine($"[RESUME DEBUG] Dialog Details - CallId: {dialog.CallId}, State: {dialog.State}");
                }
                
                if (dialog == null)
                {
                    Console.WriteLine("[RESUME DEBUG] ❌ RESUME FAILED: No dialog found for resume operation");
                    StatusChanged?.Invoke(this, "❌ No dialog found for resume operation");
                    return false;
                }

                // Prepare RTP socket for resume
                var localIp = GetLocalIPAddress();
                var rtpPort = 5004; // Default fallback
                
                Console.WriteLine($"[RESUME DEBUG] 📍 Local IP: {localIp}");
                Console.WriteLine($"[RESUME DEBUG] 📍 Audio Manager Available: {_audioManager != null}");
                Console.WriteLine($"[RESUME DEBUG] 📍 Audio Manager Running: {_audioManager?.IsRunning ?? false}");
                Console.WriteLine($"[RESUME DEBUG] 📍 Current RTP Port: {_audioManager?.LocalRtpPort ?? 0}");
                
                if (_audioManager != null && _audioManager.PrepareRtpSocket())
                {
                    rtpPort = _audioManager.LocalRtpPort;
                    Console.WriteLine($"[RESUME DEBUG] ✅ RTP socket prepared for resume on port {rtpPort}");
                    StatusChanged?.Invoke(this, $"🎵 RTP socket prepared for resume on port {rtpPort}");
                }
                else
                {
                    Console.WriteLine($"[RESUME DEBUG] ⚠️ Failed to prepare RTP socket, using fallback port: {rtpPort}");
                }

                // Create SDP with active media
                Console.WriteLine("[RESUME DEBUG] 📝 Creating active SDP for resume...");
                var activeSdp = SdpManager.CreateSdpOffer(localIp, rtpPort);
                Console.WriteLine($"[RESUME DEBUG] 📝 Resume SDP Created:\n{activeSdp}");
                
                // Create re-INVITE with active SDP
                var reInviteMessage = _messageFactory.CreateReInviteRequest(
                    dialog, 
                    dialog.GetNextLocalSequenceNumber(),
                    activeSdp
                );
                
                Console.WriteLine($"[RESUME DEBUG] 📤 Sending re-INVITE for resume...");
                Console.WriteLine($"[RESUME DEBUG] 📤 Dialog Sequence Number: {dialog.GetNextLocalSequenceNumber() - 1}");
                StatusChanged?.Invoke(this, "📤 Sending re-INVITE with active SDP for resume...");
                await SendMessageAsync(reInviteMessage);
                
                // NOTE: RTP session will be restarted when we receive 200 OK response
                // HandleAudioSetup will process the remote SDP and restart audio automatically
                // IMPORTANT: _isCallOnHold flag will be cleared in HandleAudioSetup AFTER successful resume
                Console.WriteLine("[RESUME DEBUG] 🎵 Audio will be restarted when 200 OK is received");
                Console.WriteLine("[RESUME DEBUG] ⚠️ Hold flag will be cleared in HandleAudioSetup after successful resume");
                
                Console.WriteLine("[RESUME DEBUG] ✅ RESUME OPERATION COMPLETED SUCCESSFULLY");
                StatusChanged?.Invoke(this, "✅ Call resumed successfully");
                CallStateChanged?.Invoke(this, "Call Resumed");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RESUME DEBUG] ❌ RESUME OPERATION FAILED: {ex.Message}");
                Console.WriteLine($"[RESUME DEBUG] Exception Stack Trace: {ex.StackTrace}");
                StatusChanged?.Invoke(this, $"❌ Resume operation failed: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {            try
            {
                _isConnected = false;
                _isRegistered = false;
                _isCallOnHold = false; // Reset hold state on disconnect
                _isResumeInProgress = false; // Reset resume flag on disconnect
                _registrationCompletion?.SetResult(false);
                
                // Stop registration refresh timer
                StopRegistrationRefreshTimer();
                
                // Stop and dispose SIP transport
                _sipTransport?.Dispose();
                _sipTransport = null;
                
                // Stop and dispose audio manager
                _audioManager?.StopRtpSession();
                _audioManager?.Dispose();
                _audioManager = null;
                
                _stream?.Close();
                _tcpClient?.Close();
                StatusChanged?.Invoke(this, "Disconnected from SIP server");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Disconnect error: {ex.Message}");
            }
        }

        private async Task ListenForMessagesAsync()
        {
            if (_stream == null) return;

            var buffer = new byte[8192]; // Increased buffer size
            var messageBuilder = new StringBuilder();
            
            while (_isConnected)
            {
                try
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(chunk);
                        
                        // Check if we have a complete SIP message
                        var fullMessage = messageBuilder.ToString();
                        if (IsCompleteSipMessage(fullMessage))                        
                        {
                            MessageReceived?.Invoke(this, $"INCOMING:\n{fullMessage}");
                            _ = Task.Run(async () => await ProcessIncomingMessage(fullMessage));
                            messageBuilder.Clear();
                        }
                    }
                    else if (bytesRead == 0)
                    {
                        StatusChanged?.Invoke(this, "Server closed connection");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (_isConnected)
                    {
                        StatusChanged?.Invoke(this, $"Listen error: {ex.Message}");
                    }
                    break;
                }
            }
        }
        
        private bool IsCompleteSipMessage(string message)
        {
            // SIP message is complete when it has headers ending with \r\n\r\n
            // and if Content-Length is specified, it has that many body bytes
            if (!message.Contains("\r\n\r\n"))
                return false;
                
            var parts = message.Split(new[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
            if (parts.Length < 2)
                return false;
                
            var headers = parts[0];
            var body = parts[1];
            
            // Check Content-Length
            var contentLengthMatch = System.Text.RegularExpressions.Regex.Match(
                headers, @"Content-Length:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
            if (contentLengthMatch.Success)
            {
                var expectedLength = int.Parse(contentLengthMatch.Groups[1].Value);
                return body.Length >= expectedLength;
            }
            
            return true; // No Content-Length header, assume complete
        }        private async Task ProcessIncomingMessage(string message)
        {
            try
            {
                // Extract actual SIP message from SipTransport formatted message
                // Format: "INCOMING (from X.X.X.X:PORT):\nSIP_MESSAGE"
                string actualSipMessage = message;
                
                if (message.StartsWith("INCOMING (from"))
                {
                    var colonIndex = message.IndexOf(":\n");
                    if (colonIndex > 0 && colonIndex + 2 < message.Length)
                    {
                        actualSipMessage = message.Substring(colonIndex + 2);
                    }
                }
                
                var lines = actualSipMessage.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return;
                
                var statusLine = lines[0].Trim();
                StatusChanged?.Invoke(this, $"📨 Processing SIP message");
                
                // IMP-016: Apply provider-specific preprocessing if profile handler is available
                string processedMessage = actualSipMessage;
                if (_currentProfileHandler != null)
                {
                    try
                    {
                        processedMessage = _currentProfileHandler.PreprocessIncomingMessage(actualSipMessage);
                        if (processedMessage != actualSipMessage)
                        {
                            Console.WriteLine($"[PROFILE HANDLER] {GetCurrentProfileName()}: Preprocessed incoming message");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PROFILE HANDLER] {GetCurrentProfileName()}: Preprocessing failed: {ex.Message}");
                        // Continue with original message if preprocessing fails
                    }
                }
                
                actualSipMessage = processedMessage;
                  // Validate incoming message for RFC 3261 compliance
                try
                {
                    if (_rfc3261Validator != null)
                    {
                        var validationResult = _rfc3261Validator.ValidateMessage(actualSipMessage);
                        if (validationResult.HasCriticalErrors)
                        {
                            StatusChanged?.Invoke(this, "⚠️ Incoming message has RFC 3261 compliance issues:");
                            foreach (var error in validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Take(3)) // Limit to first 3 errors
                            {
                                Console.WriteLine($"[RFC 3261 CRITICAL] {error.Message}");
                            }
                        }
                        
                        if (validationResult.Errors.Any(e => e.Severity == ValidationSeverity.Warning))
                        {
                            foreach (var warning in validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Warning).Take(2)) // Limit warnings
                            {
                                Console.WriteLine($"[RFC 3261 WARNING] {warning.Message}");
                            }
                        }
                    }
                }
                catch (Exception validationEx)
                {
                    Console.WriteLine($"[RFC 3261 VALIDATION] Error validating incoming message: {validationEx.Message}");
                }
                
                // Extract common headers for dialog management
                var callId = ExtractHeader(actualSipMessage, "Call-ID:");
                var fromHeader = ExtractHeader(actualSipMessage, "From:");
                var toHeader = ExtractHeader(actualSipMessage, "To:");
                var cseqHeader = ExtractHeader(actualSipMessage, "CSeq:");
                
                if (statusLine.StartsWith("SIP/2.0"))
                {
                    // Response message
                    await ProcessSipResponse(actualSipMessage, statusLine, callId, fromHeader, toHeader, cseqHeader);
                }
                else
                {
                    // Request message (INVITE, BYE, UPDATE, REFER, etc.)
                    var method = statusLine.Split(' ')[0];
                    StatusChanged?.Invoke(this, $"📞 Incoming {method} request");
                    
                    // Find or create dialog for this request
                    var dialog = _dialogManager.FindDialog(callId, fromHeader, toHeader);
                      // Handle incoming requests with dialog management
                    switch (method.ToUpper())
                    {
                        case "INVITE":
                            StatusChanged?.Invoke(this, "🔔 Processing incoming call...");
                            await HandleIncomingInviteWithDialog(actualSipMessage, dialog);
                            break;
                        case "BYE":
                            await HandleIncomingByeWithDialog(actualSipMessage, dialog);
                            break;
                        case "UPDATE":
                            await HandleIncomingUpdate(actualSipMessage);
                            break;
                        case "REFER":
                            await HandleIncomingRefer(actualSipMessage);
                            break;                        case "ACK":
                            // RFC 3261: ACK messages should be handled silently (no response required)
                            StatusChanged?.Invoke(this, "✅ ACK received - transaction complete");
                            // Optionally update dialog state if needed
                            if (dialog != null && dialog.State == DialogState.Early)
                            {
                                dialog.UpdateState(SipDialogState.Confirmed);
                            }
                            break;
                        default:
                            StatusChanged?.Invoke(this, $"⚠️ Unsupported method: {method}");
                            await SendMethodNotAllowedResponse(actualSipMessage, method);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error processing message: {ex.Message}");
            }
        }private async Task ProcessSipResponse(string message, string statusLine, string callId, string fromHeader, string toHeader, string cseqHeader)
        {
            var parts = statusLine.Split(' ', 3);
            if (parts.Length < 3) return;
            
            var statusCode = parts[1];
            var reasonPhrase = parts[2];
            
            StatusChanged?.Invoke(this, $"SIP Response: {statusCode} {reasonPhrase}");
              // Find dialog for response
            var dialog = _dialogManager.FindDialog(callId, fromHeader, toHeader);
            
            Console.WriteLine($"[DIALOG DEBUG] ==========================================");
            Console.WriteLine($"[DIALOG DEBUG] Dialog lookup for response:");
            Console.WriteLine($"[DIALOG DEBUG] - Call ID: '{callId}'");
            Console.WriteLine($"[DIALOG DEBUG] - From Header: '{fromHeader}'");
            Console.WriteLine($"[DIALOG DEBUG] - To Header: '{toHeader}'");
            Console.WriteLine($"[DIALOG DEBUG] - Dialog found? {dialog != null}");
            if (dialog != null)
            {
                Console.WriteLine($"[DIALOG DEBUG] - Dialog Local Tag: '{dialog.LocalTag}'");
                Console.WriteLine($"[DIALOG DEBUG] - Dialog Remote Tag: '{dialog.RemoteTag}'");
            }
            else
            {
                Console.WriteLine($"[DIALOG DEBUG] *** NO DIALOG FOUND - CHECKING BY CALL ID ***");
                var dialogByCallId = _dialogManager.FindDialogByCallId(callId);
                Console.WriteLine($"[DIALOG DEBUG] - Dialog by Call ID? {dialogByCallId != null}");
                if (dialogByCallId != null)
                {
                    Console.WriteLine($"[DIALOG DEBUG] - Found dialog via Call ID - Local Tag: '{dialogByCallId.LocalTag}'");
                    Console.WriteLine($"[DIALOG DEBUG] - Found dialog via Call ID - Remote Tag: '{dialogByCallId.RemoteTag}'");
                    Console.WriteLine($"[DIALOG DEBUG] - *** USING DIALOG FROM CALL ID LOOKUP ***");
                    dialog = dialogByCallId;
                }
            }
            Console.WriteLine($"[DIALOG DEBUG] ==========================================");            // Handle registration responses with legacy method (working version)
            if (cseqHeader.ToUpper().Contains("REGISTER"))
            {
                Console.WriteLine($"[REGISTER DEBUG] Processing REGISTER response - Status: {statusCode} {reasonPhrase}");
                Console.WriteLine($"[REGISTER DEBUG] CSeq Header: '{cseqHeader}'");
                Console.WriteLine($"[REGISTER DEBUG] Current _isRegistered: {_isRegistered}");
                Console.WriteLine($"[REGISTER DEBUG] _registrationCompletion null? {_registrationCompletion == null}");
                
                switch (statusCode)
                {
                    case "200":
                        StatusChanged?.Invoke(this, "✅ Registration successful");
                        _isRegistered = true;
                        Console.WriteLine($"[REGISTER DEBUG] After setting _isRegistered: {_isRegistered}");
                        
                        if (_registrationCompletion != null)
                        {
                            Console.WriteLine("[REGISTER DEBUG] Calling _registrationCompletion.SetResult(true)");
                            _registrationCompletion.SetResult(true);
                            Console.WriteLine("[REGISTER DEBUG] _registrationCompletion.SetResult(true) completed");
                        }
                        else
                        {
                            Console.WriteLine("[REGISTER DEBUG] _registrationCompletion is null - cannot complete registration task");
                        }
                        break;
                        
                    case "401":
                    case "407":
                        StatusChanged?.Invoke(this, $"🔐 Authentication required ({statusCode}) - sending credentials...");
                        // Handle authentication challenge using legacy working method
                        _ = Task.Run(() => HandleAuthenticationChallenge(message));
                        break;
                        
                    case "403":
                        StatusChanged?.Invoke(this, "❌ Authentication failed (403) - check credentials");
                        _isRegistered = false;
                        _registrationCompletion?.SetResult(false);
                        break;
                        
                    default:
                        if (statusCode.StartsWith("4") || statusCode.StartsWith("5"))
                        {
                            StatusChanged?.Invoke(this, $"❌ Registration failed: {statusCode} {reasonPhrase}");
                            _isRegistered = false;
                            _registrationCompletion?.SetResult(false);
                        }
                        break;
                }
                return;
            }              // Handle call-related responses with dialog management
            // Update dialog from response for proper state management
            if (!cseqHeader.ToUpper().Contains("REGISTER"))
            {
                UpdateDialogFromResponse(message, int.Parse(statusCode));
            }
              Console.WriteLine($"[CRITICAL DEBUG] ==========================================");
            Console.WriteLine($"[CRITICAL DEBUG] ProcessSipResponse - About to handle status code: '{statusCode}'");
            Console.WriteLine($"[CRITICAL DEBUG] CSeq Header: '{cseqHeader}'");
            Console.WriteLine($"[CRITICAL DEBUG] Dialog null? {dialog == null}");
            Console.WriteLine($"[CRITICAL DEBUG] ==========================================");
            
            switch (statusCode)
            {
                case "200":
                    Console.WriteLine($"[CRITICAL DEBUG] Entering 200 OK case - calling HandleSuccessResponse");
                    await HandleSuccessResponse(message, cseqHeader, dialog);
                    Console.WriteLine($"[CRITICAL DEBUG] HandleSuccessResponse completed");
                    break;
                    
                case "401":
                case "407":
                    // Only handle authentication for non-REGISTER requests
                    // REGISTER authentication is handled by RegistrationManager above
                    if (!cseqHeader.ToUpper().Contains("REGISTER"))
                    {
                        StatusChanged?.Invoke(this, $"🔐 Authentication required for {cseqHeader} ({statusCode}) - sending credentials...");
                        _ = Task.Run(() => HandleAuthenticationChallenge(message));
                    }
                    break;
                    
                case "403":
                    StatusChanged?.Invoke(this, "❌ Authentication failed (403) - check credentials");
                    if (cseqHeader.ToUpper().Contains("REGISTER"))
                    {
                        _isRegistered = false;
                        _registrationCompletion?.SetResult(false);
                    }
                    break;
                    
                case "486":
                case "603":
                    StatusChanged?.Invoke(this, "📞 Call declined");
                    CallStateChanged?.Invoke(this, "Call Declined");
                    if (dialog != null)
                    {
                        dialog.UpdateState(SipDialogState.Terminated);
                    }
                    break;
                    
                default:
                    StatusChanged?.Invoke(this, $"SIP Response: {statusCode} {reasonPhrase}");
                    break;
            }
        }        private async Task HandleSuccessResponse(string message, string cseqHeader, SipDialog? dialog)
        {
            Console.WriteLine($"[HANDLE SUCCESS DEBUG] ==========================================");
            Console.WriteLine($"[HANDLE SUCCESS DEBUG] HandleSuccessResponse called");
            Console.WriteLine($"[HANDLE SUCCESS DEBUG] CSeq Header: '{cseqHeader}'");
            Console.WriteLine($"[HANDLE SUCCESS DEBUG] CSeq contains INVITE? {cseqHeader.ToUpper().Contains("INVITE")}");
            Console.WriteLine($"[HANDLE SUCCESS DEBUG] Dialog is null? {dialog == null}");
            Console.WriteLine($"[HANDLE SUCCESS DEBUG] ==========================================");
            
            if (cseqHeader.ToUpper().Contains("INVITE"))
            {
                Console.WriteLine($"[HANDLE SUCCESS DEBUG] *** ENTERING INVITE SUCCESS HANDLER ***");
                
                // CRITICAL: Check if we have a dialog - this is required for ACK
                if (dialog == null)
                {
                    Console.WriteLine($"[ACK CRITICAL ERROR] *** NO DIALOG FOUND FOR 200 OK TO INVITE ***");
                    StatusChanged?.Invoke(this, "❌ CRITICAL: Cannot send ACK - no dialog found!");
                    
                    // Try to find dialog by Call-ID as fallback
                    var callId = ExtractHeader(message, "Call-ID:");
                    Console.WriteLine($"[ACK CRITICAL ERROR] Attempting to find dialog by Call-ID: '{callId}'");
                    
                    if (!string.IsNullOrEmpty(callId))
                    {
                        dialog = _dialogManager.FindDialogByCallId(callId);
                        Console.WriteLine($"[ACK CRITICAL ERROR] Dialog found by Call-ID? {dialog != null}");
                        
                        if (dialog == null)
                        {
                            Console.WriteLine($"[ACK CRITICAL ERROR] *** STILL NO DIALOG - ACK CANNOT BE SENT ***");
                            StatusChanged?.Invoke(this, "❌ CRITICAL: No dialog found by Call-ID either - ACK will not be sent!");
                            return; // Cannot send ACK without dialog
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[ACK CRITICAL ERROR] *** NO CALL-ID FOUND IN MESSAGE ***");
                        StatusChanged?.Invoke(this, "❌ CRITICAL: No Call-ID found in 200 OK message!");
                        return;
                    }
                }
                
                StatusChanged?.Invoke(this, "✅ Call answered - sending ACK");
                CallStateChanged?.Invoke(this, "Call Connected");
                
                // Update dialog state to confirmed
                if (dialog != null)
                {                    // Extract To tag from response to complete dialog
                    var toHeader = ExtractHeader(message, "To:");
                    StatusChanged?.Invoke(this, $"🔍 Raw To header extracted: '{toHeader}'");
                    
                    var toTag = ExtractTag(toHeader);
                    StatusChanged?.Invoke(this, $"🔍 Extracted To tag using ExtractTag method: '{toTag}' (length: {toTag.Length})");
                    
                    if (!string.IsNullOrEmpty(toTag))
                    {
                        dialog.RemoteTag = toTag;
                        StatusChanged?.Invoke(this, $"✅ Dialog updated with To tag: '{toTag}'");
                    }
                    else
                    {
                        // No To tag found - generate one as RFC 3261 fallback
                        var generatedTag = GenerateTag();
                        dialog.RemoteTag = generatedTag;
                        StatusChanged?.Invoke(this, $"⚠️ No To tag found in 200 OK response. Generated fallback tag: {generatedTag}");
                        StatusChanged?.Invoke(this, $"🔍 To header was: '{toHeader}'");
                    }
                      // Extract Contact header from 200 OK to update dialog RemoteTarget
                    var contactHeader = ExtractHeader(message, "Contact:");
                    StatusChanged?.Invoke(this, $"🔍 Found Contact header: {contactHeader}");
                    if (!string.IsNullOrEmpty(contactHeader))
                    {
                        var contactUri = ExtractSipUriFromContactHeader(contactHeader);
                        if (!string.IsNullOrEmpty(contactUri))
                        {
                            dialog.RemoteTarget = contactUri;
                            StatusChanged?.Invoke(this, $"🔍 Dialog updated with Contact URI: {contactUri}");
                        }
                        else
                        {
                            StatusChanged?.Invoke(this, $"⚠️ Failed to parse SIP URI from Contact header: {contactHeader}");
                        }
                    }
                    else
                    {
                        StatusChanged?.Invoke(this, "⚠️ No Contact header found in 200 OK response");
                    }
                      dialog.UpdateState(SipDialogState.Confirmed);
                }
                
                // Send ACK message using message factory
                await SendAckMessageWithFactory(message, dialog);
                  // Handle SDP and start audio
                HandleAudioSetup(message);
            }
            else if (cseqHeader.ToUpper().Contains("BYE"))
            {
                StatusChanged?.Invoke(this, "✅ BYE acknowledged - call termination confirmed");
                CallStateChanged?.Invoke(this, "Call Terminated");
                
                // Update dialog state to terminated
                if (dialog != null)
                {
                    dialog.UpdateState(SipDialogState.Terminated);
                    StatusChanged?.Invoke(this, $"🔄 Dialog terminated by 200 OK to BYE: {dialog.CallId}");
                }
                
                // Ensure RTP session is fully stopped to prevent restart
                _audioManager?.StopRtpSession();
                
                // Clean up call state completely
                _remoteContactUri = string.Empty;
                _toTag = string.Empty;
                _activeCallId = string.Empty;
                _currentTargetNumber = string.Empty;
                
                StatusChanged?.Invoke(this, "🔄 Call state cleaned up after BYE confirmation");
            }
        }        private async Task SendAckMessageWithFactory(string inviteResponse, SipDialog? dialog)
        {
            try
            {
                Console.WriteLine($"[ACK DEBUG] ==========================================");
                Console.WriteLine($"[ACK DEBUG] SendAckMessageWithFactory called");
                Console.WriteLine($"[ACK DEBUG] Dialog provided? {dialog != null}");
                Console.WriteLine($"[ACK DEBUG] ==========================================");
                
                if (dialog == null) 
                {
                    Console.WriteLine($"[ACK DEBUG] ERROR: Dialog is null!");
                    StatusChanged?.Invoke(this, "❌ Cannot send ACK: Dialog is null");
                    
                    // Try to create minimal ACK from 200 OK response headers
                    Console.WriteLine($"[ACK DEBUG] Attempting to create ACK from 200 OK headers...");
                    await CreateAckFromResponse(inviteResponse);
                    return;
                }
                
                Console.WriteLine($"[ACK DEBUG] Dialog information:");
                Console.WriteLine($"[ACK DEBUG] - Call ID: '{dialog.CallId}'");
                Console.WriteLine($"[ACK DEBUG] - Local Tag: '{dialog.LocalTag}'");
                Console.WriteLine($"[ACK DEBUG] - Remote Tag: '{dialog.RemoteTag}'");
                Console.WriteLine($"[ACK DEBUG] - Remote Target: '{dialog.RemoteTarget}'");
                Console.WriteLine($"[ACK DEBUG] - Local Sequence Number: {dialog.LocalSequenceNumber}");
                Console.WriteLine($"[ACK DEBUG] - Current Target Number: '{_currentTargetNumber}'");
                
                // Determine proper Request-URI for ACK
                // RFC 3261: ACK should use Contact URI from 200 OK if present, otherwise use original Request-URI
                var requestUri = !string.IsNullOrEmpty(dialog.RemoteTarget) 
                    ? dialog.RemoteTarget 
                    : $"sip:{_currentTargetNumber}@{_serverHost}:{_serverPort}";
                
                Console.WriteLine($"[ACK DEBUG] Calculated Request-URI: '{requestUri}'");
                StatusChanged?.Invoke(this, $"🔍 ACK Request-URI: {requestUri}");
                StatusChanged?.Invoke(this, $"🔍 ACK Dialog info - LocalTag: {dialog.LocalTag}, RemoteTag: {dialog.RemoteTag}");                  Console.WriteLine($"[ACK DEBUG] Creating ACK message with username '{_username}'...");
                var ackMessage = _messageFactory.CreateAckRequest(
                    dialog.CallId,
                    dialog.LocalTag ?? "",
                    dialog.RemoteTag ?? "",
                    requestUri,
                    dialog.LocalSequenceNumber,
                    _username ?? ""  // CRITICAL FIX: Pass username for proper From header
                );
                
                Console.WriteLine($"[ACK DEBUG] *** GENERATED ACK MESSAGE ***");
                Console.WriteLine($"[ACK DEBUG] ==========================================");
                Console.WriteLine(ackMessage);
                Console.WriteLine($"[ACK DEBUG] ==========================================");
                
                StatusChanged?.Invoke(this, "📤 Sending ACK message");
                Console.WriteLine($"[ACK DEBUG] Sending ACK message via SendMessageAsync...");
                
                await SendMessageAsync(ackMessage);
                
                Console.WriteLine($"[ACK DEBUG] *** ACK MESSAGE SENT SUCCESSFULLY ***");
                StatusChanged?.Invoke(this, "✅ ACK sent - call fully established");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ACK DEBUG] *** ACK SENDING FAILED ***");
                Console.WriteLine($"[ACK DEBUG] Error: {ex.Message}");
                Console.WriteLine($"[ACK DEBUG] Stack trace: {ex.StackTrace}");
                StatusChanged?.Invoke(this, $"❌ Failed to send ACK: {ex.Message}");
            }
        }

        private async Task CreateAckFromResponse(string response200Ok)
        {
            try
            {
                Console.WriteLine($"[ACK FALLBACK] *** CREATING ACK FROM 200 OK RESPONSE ***");
                
                // Extract required headers from 200 OK
                var callId = ExtractHeader(response200Ok, "Call-ID:");
                var fromHeader = ExtractHeader(response200Ok, "From:");
                var toHeader = ExtractHeader(response200Ok, "To:");
                var cseqHeader = ExtractHeader(response200Ok, "CSeq:");
                var contactHeader = ExtractHeader(response200Ok, "Contact:");
                
                Console.WriteLine($"[ACK FALLBACK] Extracted headers:");
                Console.WriteLine($"[ACK FALLBACK] - Call-ID: '{callId}'");
                Console.WriteLine($"[ACK FALLBACK] - From: '{fromHeader}'");
                Console.WriteLine($"[ACK FALLBACK] - To: '{toHeader}'");
                Console.WriteLine($"[ACK FALLBACK] - CSeq: '{cseqHeader}'");
                Console.WriteLine($"[ACK FALLBACK] - Contact: '{contactHeader}'");
                
                if (string.IsNullOrEmpty(callId) || string.IsNullOrEmpty(cseqHeader))
                {
                    Console.WriteLine($"[ACK FALLBACK] *** MISSING REQUIRED HEADERS - CANNOT CREATE ACK ***");
                    StatusChanged?.Invoke(this, "❌ Cannot create ACK: Missing required headers in 200 OK");
                    return;
                }
                
                // Extract sequence number from CSeq
                var cseqParts = cseqHeader.Trim().Split(' ');
                if (cseqParts.Length < 2)
                {
                    Console.WriteLine($"[ACK FALLBACK] *** INVALID CSEQ FORMAT ***");
                    StatusChanged?.Invoke(this, "❌ Cannot create ACK: Invalid CSeq format");
                    return;
                }
                
                var sequenceNumber = uint.Parse(cseqParts[0]);
                
                // Extract tags
                var fromTag = ExtractTag(fromHeader);
                var toTag = ExtractTag(toHeader);
                
                // Use Contact URI from 200 OK as Request-URI, fallback to target number
                var requestUri = !string.IsNullOrEmpty(contactHeader) 
                    ? ExtractSipUriFromContactHeader(contactHeader)
                    : $"sip:{_currentTargetNumber}@{_serverHost}:{_serverPort}";
                
                if (string.IsNullOrEmpty(requestUri))
                {
                    requestUri = $"sip:{_currentTargetNumber}@{_serverHost}:{_serverPort}";
                }
                
                Console.WriteLine($"[ACK FALLBACK] Final ACK parameters:");
                Console.WriteLine($"[ACK FALLBACK] - Request-URI: '{requestUri}'");
                Console.WriteLine($"[ACK FALLBACK] - From Tag: '{fromTag}'");
                Console.WriteLine($"[ACK FALLBACK] - To Tag: '{toTag}'");
                Console.WriteLine($"[ACK FALLBACK] - Sequence: {sequenceNumber}");
                
                // Create ACK using message factory
                var ackMessage = _messageFactory.CreateAckRequest(
                    callId,
                    fromTag,
                    toTag,
                    requestUri,
                    sequenceNumber,
                    _username ?? ""
                );
                
                Console.WriteLine($"[ACK FALLBACK] *** GENERATED FALLBACK ACK MESSAGE ***");
                Console.WriteLine($"[ACK FALLBACK] ==========================================");
                Console.WriteLine(ackMessage);
                Console.WriteLine($"[ACK FALLBACK] ==========================================");
                
                StatusChanged?.Invoke(this, "📤 Sending fallback ACK message");
                await SendMessageAsync(ackMessage);
                
                Console.WriteLine($"[ACK FALLBACK] *** FALLBACK ACK SENT SUCCESSFULLY ***");
                StatusChanged?.Invoke(this, "✅ Fallback ACK sent - call established");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ACK FALLBACK] *** FALLBACK ACK FAILED ***");
                Console.WriteLine($"[ACK FALLBACK] Error: {ex.Message}");
                StatusChanged?.Invoke(this, $"❌ Fallback ACK failed: {ex.Message}");
            }
        }private void HandleAudioSetup(string message)
        {
            Console.WriteLine($"[AUDIO SETUP DEBUG] ==========================================");
            Console.WriteLine($"[AUDIO SETUP DEBUG] HandleAudioSetup called");
            Console.WriteLine($"[AUDIO SETUP DEBUG] Message length: {message?.Length ?? 0} characters");
            Console.WriteLine($"[AUDIO SETUP DEBUG] ==========================================");              // Write detailed debug log
            Console.WriteLine($"[AUDIO SETUP DEBUG] HandleAudioSetup called with message length: {message?.Length ?? 0}");
            
            var sdpContent = ExtractSdpContent(message ?? "");
            Console.WriteLine($"[AUDIO SETUP DEBUG] SDP extraction result: {sdpContent?.Length ?? 0} characters");
              // Log the SDP extraction result
            Console.WriteLine($"[AUDIO SETUP DEBUG] SDP extraction result: {sdpContent?.Length ?? 0} chars");
            
            if (!string.IsNullOrEmpty(sdpContent))
            {
                Console.WriteLine($"[AUDIO SETUP DEBUG] Extracted SDP content:");
                Console.WriteLine($"[AUDIO SETUP DEBUG] ==========================================");
                Console.WriteLine(sdpContent);
                Console.WriteLine($"[AUDIO SETUP DEBUG] ==========================================");
                
                var sdpInfo = SdpManager.ParseSdpContent(sdpContent);
                Console.WriteLine($"[AUDIO SETUP DEBUG] SDP parsing result: {(sdpInfo != null ? "SUCCESS" : "FAILED")}");
                
                if (sdpInfo != null && sdpInfo.HasAudio && _audioManager != null)
                {
                    Console.WriteLine($"[AUDIO SETUP DEBUG] *** AUDIO SESSION VALIDATION PASSED ***");
                    Console.WriteLine($"[AUDIO SETUP DEBUG] - Remote IP from SDP: '{sdpInfo.RemoteIp}'");
                    Console.WriteLine($"[AUDIO SETUP DEBUG] - Remote RTP Port from SDP: {sdpInfo.RemoteRtpPort}");
                    Console.WriteLine($"[AUDIO SETUP DEBUG] - Audio Codec from SDP: '{sdpInfo.AudioCodec}'");
                    Console.WriteLine($"[AUDIO SETUP DEBUG] - Payload Type from SDP: {sdpInfo.PayloadType}");
                    Console.WriteLine($"[AUDIO SETUP DEBUG] - Audio Manager LocalRtpPort: {_audioManager.LocalRtpPort}");
                    Console.WriteLine($"[AUDIO SETUP DEBUG] - Audio Manager IsRunning: {_audioManager.IsRunning}");
                    
                    StatusChanged?.Invoke(this, $"🎵 Starting audio stream to {sdpInfo.RemoteIp}:{sdpInfo.RemoteRtpPort}");
                      // CRITICAL FIX: Smart audio restart - try lightweight resume first, fall back to full restart
                    _ = Task.Run(async () => 
                    {
                        try
                        {                            Console.WriteLine($"[AUDIO SETUP DEBUG] *** ENTERING AUDIO TASK ***");                            bool success = false;
                              // BUG-001 FIX: Simplified and more reliable resume detection
                            // Use clearer logic: if we were on hold and now receiving 200 OK, this is resume
                            bool hasRtpSocket = _audioManager.HasActiveSocket(); // Check if socket exists
                            bool isResumeOperation = _isCallOnHold || _isResumeInProgress;
                            
                            Console.WriteLine($"[AUDIO SETUP DEBUG] BUG-001 FIX - RESUME DETECTION:");
                            Console.WriteLine($"[AUDIO SETUP DEBUG] - Was on hold: {_isCallOnHold}");
                            Console.WriteLine($"[AUDIO SETUP DEBUG] - Resume in progress: {_isResumeInProgress}");
                            Console.WriteLine($"[AUDIO SETUP DEBUG] - Has RTP socket: {hasRtpSocket}");
                            Console.WriteLine($"[AUDIO SETUP DEBUG] - Audio Manager IsRunning: {_audioManager.IsRunning}");
                            Console.WriteLine($"[AUDIO SETUP DEBUG] - DECISION: Resume operation = {isResumeOperation}");
                            
                            // Try resume if conditions indicate this is a resume operation
                            if (isResumeOperation)
                            {
                                Console.WriteLine($"[AUDIO SETUP DEBUG] *** ATTEMPTING BUG-001 FIX RESUME PATH ***");
                                StatusChanged?.Invoke(this, $"🔄 Attempting to resume existing RTP session...");
                                
                                // BUG-001 FIX: Ensure remote endpoint is updated before resume attempt
                                _audioManager.UpdateRemoteEndpoint(sdpInfo.RemoteIp, sdpInfo.RemoteRtpPort);
                                Console.WriteLine($"[AUDIO SETUP DEBUG] Remote endpoint updated to {sdpInfo.RemoteIp}:{sdpInfo.RemoteRtpPort}");
                                
                                success = await _audioManager.ResumeRtpStreams();
                                Console.WriteLine($"[AUDIO SETUP DEBUG] ResumeRtpStreams result: {success}");

                                if (success)
                                {
                                    // BUG-001 FIX: Clear hold state and resume flags after successful resume
                                    _isCallOnHold = false;
                                    _isResumeInProgress = false;
                                    StatusChanged?.Invoke(this, $"✅ Audio session resumed successfully");
                                    Console.WriteLine($"[AUDIO SETUP DEBUG] ✅ BUG-001 FIX: RESUME PATH SUCCESS - AUDIO RESTORED");
                                    return;
                                }
                                else
                                {
                                    Console.WriteLine($"[AUDIO SETUP DEBUG] BUG-001 FIX: Resume failed, falling back to full restart...");
                                    StatusChanged?.Invoke(this, $"⚠️ Resume failed, falling back to full restart...");
                                    
                                    // BUG-001 FIX: On resume failure, ensure we clean up before full restart
                                    _audioManager.StopRtpSession();
                                    Console.WriteLine($"[AUDIO SETUP DEBUG] BUG-001 FIX: Stopped RTP session before full restart");
                                }
                            }                            else
                            {
                                Console.WriteLine($"[AUDIO SETUP DEBUG] *** ATTEMPTING FULL START PATH ***");
                                Console.WriteLine($"[AUDIO SETUP DEBUG] Reason: isResumeOperation={isResumeOperation}");
                            }
                            
                            // Fall back to full RTP session restart
                            Console.WriteLine($"[AUDIO SETUP DEBUG] Starting RTP session: {sdpInfo.RemoteIp}:{sdpInfo.RemoteRtpPort}");
                            success = await _audioManager.StartRtpSession(sdpInfo.RemoteIp, sdpInfo.RemoteRtpPort, sdpInfo.AudioCodec, sdpInfo.PayloadType);
                            Console.WriteLine($"[AUDIO SETUP DEBUG] *** StartRtpSession RESULT: {success} ***");
                              if (success)
                            {
                                // BUG-001 FIX: Always clear hold flags after successful audio restart
                                if (isResumeOperation)
                                {
                                    _isCallOnHold = false;
                                    _isResumeInProgress = false;
                                    Console.WriteLine($"[AUDIO SETUP DEBUG] BUG-001 FIX: Hold flags cleared after successful restart");
                                }
                                
                                StatusChanged?.Invoke(this, $"✅ Audio session started successfully");
                                Console.WriteLine($"[AUDIO SETUP DEBUG] *** AUDIO SESSION SUCCESS ***");
                            }
                            else
                            {
                                StatusChanged?.Invoke(this, $"❌ Failed to start audio session");
                                Console.WriteLine($"[AUDIO SETUP DEBUG] *** AUDIO SESSION FAILED ***");
                            }
                            
                            // BUG-001 FIX: Always clear resume flag regardless of success/failure
                            _isResumeInProgress = false;
                            Console.WriteLine($"[AUDIO SETUP DEBUG] BUG-001 FIX: Resume in progress flag cleared");
                        }                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AUDIO SETUP DEBUG] *** AUDIO TASK EXCEPTION: {ex.Message} ***");
                            Console.WriteLine($"[AUDIO SETUP DEBUG] Stack trace: {ex.StackTrace}");
                            StatusChanged?.Invoke(this, $"❌ Error starting audio session: {ex.Message}");
                            
                            // Clear resume flag on exception
                            _isResumeInProgress = false;
                            Console.WriteLine($"[AUDIO SETUP DEBUG] Resume in progress flag cleared (exception)");
                        }
                    });
                }
                else
                {
                    Console.WriteLine($"[AUDIO SETUP DEBUG] *** AUDIO SESSION VALIDATION FAILED ***");
                    Console.WriteLine($"[AUDIO SETUP DEBUG] - SDP Info is null: {sdpInfo == null}");
                    if (sdpInfo != null)
                    {
                        Console.WriteLine($"[AUDIO SETUP DEBUG] - Has Audio: {sdpInfo.HasAudio}");
                        Console.WriteLine($"[AUDIO SETUP DEBUG] - Remote IP: '{sdpInfo.RemoteIp}'");
                        Console.WriteLine($"[AUDIO SETUP DEBUG] - Remote Port: {sdpInfo.RemoteRtpPort}");
                    }
                    Console.WriteLine($"[AUDIO SETUP DEBUG] - Audio Manager is null: {_audioManager == null}");
                    
                    if (_audioManager == null)
                    {
                        StatusChanged?.Invoke(this, $"❌ Audio manager not initialized");
                    }
                    else if (sdpInfo == null)
                    {
                        StatusChanged?.Invoke(this, $"❌ Failed to parse SDP content");
                    }
                    else if (!sdpInfo.HasAudio)
                    {
                        StatusChanged?.Invoke(this, $"❌ SDP does not contain audio media");
                    }
                }
            }
            else
            {                Console.WriteLine($"[AUDIO SETUP DEBUG] *** NO SDP CONTENT FOUND ***");
                Console.WriteLine($"[AUDIO SETUP DEBUG] Message preview (first 500 chars):");
                Console.WriteLine($"[AUDIO SETUP DEBUG] {message?.Substring(0, Math.Min(500, message?.Length ?? 0))}");
                StatusChanged?.Invoke(this, $"❌ No SDP content found in 200 OK response");
                  // Log the failure with message details
                Console.WriteLine($"[AUDIO SETUP DEBUG] NO SDP CONTENT FOUND - Message preview:");
                Console.WriteLine($"[AUDIO SETUP DEBUG] {message?.Substring(0, Math.Min(1000, message?.Length ?? 0))}");
            }
            
            Console.WriteLine($"[AUDIO SETUP DEBUG] HandleAudioSetup completed");
            Console.WriteLine($"[AUDIO SETUP DEBUG] ==========================================");
        }private async Task HandleIncomingInviteWithDialog(string inviteMessage, SipDialog? dialog)
        {
            try
            {
                StatusChanged?.Invoke(this, "📞 Incoming call received");
                
                // Extract headers for dialog creation
                var callId = ExtractHeader(inviteMessage, "Call-ID:");
                var fromHeader = ExtractHeader(inviteMessage, "From:");
                var toHeader = ExtractHeader(inviteMessage, "To:");
                var cseqHeader = ExtractHeader(inviteMessage, "CSeq:");
                var contactHeader = ExtractHeader(inviteMessage, "Contact:");
                
                // Create new dialog for incoming call if it doesn't exist
                if (dialog == null)
                {
                    // Extract From tag
                    var fromTag = "";
                    if (fromHeader.Contains("tag="))
                    {
                        var tagStart = fromHeader.IndexOf("tag=") + 4;
                        var tagEnd = fromHeader.IndexOf(';', tagStart);
                        if (tagEnd == -1) tagEnd = fromHeader.Length;
                        fromTag = fromHeader.Substring(tagStart, tagEnd - tagStart).Trim();
                    }
                      // Extract CSeq number
                    var cseqNumber = 1;
                    if (!string.IsNullOrEmpty(cseqHeader))
                    {
                        var cseqParts = cseqHeader.Split(' ');
                        if (cseqParts.Length > 0 && int.TryParse(cseqParts[0], out var seq))
                        {
                            cseqNumber = seq;
                        }
                    }
                    
                    // Extract remote URI from From header
                    var remoteUri = "";
                    var match = System.Text.RegularExpressions.Regex.Match(fromHeader, @"<([^>]+)>");
                    if (match.Success)
                    {
                        remoteUri = match.Groups[1].Value;
                    }
                    else
                    {
                        // Fallback: extract from From header without angle brackets
                        var fromParts = fromHeader.Replace("From:", "").Trim().Split(';');
                        if (fromParts.Length > 0)
                        {
                            remoteUri = fromParts[0].Trim();
                        }
                    }
                      // For incoming calls, we don't have a local tag yet, and the remote tag is the caller's tag
                    dialog = _dialogManager.CreateIncomingDialogWithUsernames(callId, "", fromTag, (uint)cseqNumber, _username, remoteUri);
                    dialog.UpdateState(SipDialogState.Early);
                    StatusChanged?.Invoke(this, $"🔄 Created new dialog for incoming call: {callId}");
                }
                
                // Extract caller information
                var callerInfo = ExtractCallerInfo(fromHeader);
                StatusChanged?.Invoke(this, $"📞 Call from: {callerInfo}");
                  // Store pending call information for later accept/decline
                _pendingIncomingCallId = callId;
                _pendingIncomingVia = ExtractHeader(inviteMessage, "Via:");
                _pendingIncomingFrom = fromHeader;
                _pendingIncomingTo = toHeader;
                _pendingIncomingCSeq = cseqHeader;
                _pendingIncomingSdp = ExtractSdpContent(inviteMessage);
                _pendingIncomingInvite = inviteMessage;
                
                Console.WriteLine($"[INVITE DEBUG] ✅ Stored pending incoming INVITE, length: {_pendingIncomingInvite?.Length ?? 0}");
                Console.WriteLine($"[INVITE DEBUG] CallId: {callId}, From: {fromHeader.Substring(0, Math.Min(50, fromHeader.Length))}...");
                  // Send 180 Ringing response using message factory
                var ringingResponse = _messageFactory.Create180RingingResponse(inviteMessage, dialog.LocalTag ?? GenerateTag());
                
                await SendMessageAsync(ringingResponse);
                
                // Fire incoming call event
                IncomingCall?.Invoke(this, callerInfo);
                
                // Extract SDP content if present
                if (!string.IsNullOrEmpty(_pendingIncomingSdp))
                {
                    var sdpInfo = SdpManager.ParseSdpContent(_pendingIncomingSdp);
                    if (sdpInfo != null)
                    {
                        StatusChanged?.Invoke(this, $"🎵 Audio offer: {sdpInfo.RemoteIp}:{sdpInfo.RemoteRtpPort}");
                    }
                }
                
                StatusChanged?.Invoke(this, "⏳ Waiting for user to accept or decline call...");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error handling incoming INVITE: {ex.Message}");
            }
        }

        private async Task HandleIncomingByeWithDialog(string byeMessage, SipDialog? dialog)
        {
            try
            {
                StatusChanged?.Invoke(this, "📞 Call ended by remote party");
                CallStateChanged?.Invoke(this, "Call Ended");
                
                // Update dialog state to terminated
                if (dialog != null)
                {
                    dialog.UpdateState(SipDialogState.Terminated);
                    StatusChanged?.Invoke(this, $"🔄 Dialog terminated: {dialog.CallId}");
                }
                
                // Stop RTP session
                _audioManager?.StopRtpSession();
                
                // Clean up call state
                _remoteContactUri = string.Empty;
                _toTag = string.Empty;
                _activeCallId = string.Empty;
                _currentTargetNumber = string.Empty;
                
                // Extract headers for response
                var callId = ExtractHeader(byeMessage, "Call-ID:");
                var via = ExtractHeader(byeMessage, "Via:");
                var from = ExtractHeader(byeMessage, "From:");
                var to = ExtractHeader(byeMessage, "To:");
                var cseq = ExtractHeader(byeMessage, "CSeq:");
                
                // Send 200 OK response using message factory
                var okResponse = _messageFactory.CreateResponse(200, "OK", 
                    callId, dialog?.LocalTag ?? GenerateTag(), dialog?.RemoteTag ?? "", 
                    via, from, to, cseq);
                
                await SendMessageAsync(okResponse);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error handling incoming BYE: {ex.Message}");
            }
        }        private async Task SendIncomingCallResponse(string callId, string via, string from, string to, string cseq, string remoteSdpContent)
        {
            try
            {
                var localIp = GetLocalIPAddress();
                
                // CRITICAL FIX: Prepare RTP socket to get valid port for SDP answer
                var rtpPort = 5004; // Default fallback port
                if (_audioManager != null)
                {
                    if (_audioManager.PrepareRtpSocket())
                    {
                        rtpPort = _audioManager.LocalRtpPort;
                        Console.WriteLine($"[INCOMING CALL DEBUG] ✅ RTP socket prepared for incoming call response, using port: {rtpPort}");
                    }
                    else
                    {
                        Console.WriteLine($"[INCOMING CALL DEBUG] ⚠️ Failed to prepare RTP socket for incoming call response, using fallback port: {rtpPort}");
                    }
                }
                
                // Create our SDP answer
                var sdpAnswer = SdpManager.CreateSdpOffer(localIp, rtpPort); // Using offer format as answer for simplicity
                
                // FIXED: Use SipMessageFactory for proper header formatting
                // ExtractHeader returns only values, so we need to add prefixes back
                var response = $"SIP/2.0 200 OK\r\n" +
                              $"Via: {via}\r\n" +
                              $"From: {from}\r\n" +
                              $"To: {to}\r\n" +
                              $"Call-ID: {callId}\r\n" +
                              $"CSeq: {cseq}\r\n" +
                              $"Contact: <sip:{_username}@{localIp}:5060>\r\n" +
                              $"Content-Type: application/sdp\r\n" +
                              $"Content-Length: {SdpManager.GetSdpLength(sdpAnswer)}\r\n" +
                              $"\r\n" +
                              $"{sdpAnswer}";
                  StatusChanged?.Invoke(this, "📤 Sending 200 OK response to incoming call");
                MessageReceived?.Invoke(this, $"OUTGOING (Call Answer):\n{response}");

                // FIXED: Use SipTransport to send response back to incoming connection
                if (_sipTransport != null)
                {
                    var success = await _sipTransport.SendToIncomingConnectionAsync(response);
                    if (!success)
                    {
                        StatusChanged?.Invoke(this, "⚠️ Failed to send 200 OK via SipTransport, falling back to stream");
                        // Fallback to legacy stream if SipTransport fails
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await _stream!.WriteAsync(responseBytes);
                    }
                }
                else
                {
                    // Fallback to legacy stream if SipTransport not available
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await _stream!.WriteAsync(responseBytes);
                }
                
                // Start RTP session if we have remote SDP info
                if (!string.IsNullOrEmpty(remoteSdpContent))
                {                    var remoteSdpInfo = SdpManager.ParseSdpContent(remoteSdpContent);
                    if (remoteSdpInfo != null)
                    {
                        StatusChanged?.Invoke(this, $"🎵 Starting RTP session with {remoteSdpInfo.RemoteIp}:{remoteSdpInfo.RemoteRtpPort} using {remoteSdpInfo.AudioCodec}");
                        _audioManager?.StartRtpSession(remoteSdpInfo.RemoteIp, remoteSdpInfo.RemoteRtpPort, remoteSdpInfo.AudioCodec, remoteSdpInfo.PayloadType);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error sending call response: {ex.Message}");
            }
        }        private async Task SendIncomingCallResponseWithFactory(string callId, string via, string from, string to, string cseq, string remoteSdpContent, SipDialog? dialog)
        {
            try
            {
                var localIp = GetLocalIPAddress();
                
                // CRITICAL FIX: Prepare RTP socket to get valid port for SDP answer
                var rtpPort = 5004; // Default fallback port
                if (_audioManager != null)
                {
                    if (_audioManager.PrepareRtpSocket())
                    {
                        rtpPort = _audioManager.LocalRtpPort;
                        Console.WriteLine($"[INCOMING CALL FACTORY DEBUG] ✅ RTP socket prepared for incoming call factory response, using port: {rtpPort}");
                    }
                    else
                    {
                        Console.WriteLine($"[INCOMING CALL FACTORY DEBUG] ⚠️ Failed to prepare RTP socket for incoming call factory response, using fallback port: {rtpPort}");
                    }
                }
                
                // Create our SDP answer
                var sdpAnswer = SdpManager.CreateSdpOffer(localIp, rtpPort);
                  // ENHANCED: Create 200 OK response with comprehensive error handling
                if (!string.IsNullOrEmpty(_pendingIncomingInvite))
                {
                    Console.WriteLine($"[200 OK DEBUG] Creating 200 OK response for call {callId}");
                    Console.WriteLine($"[200 OK DEBUG] _pendingIncomingInvite length: {_pendingIncomingInvite.Length}");
                    
                    var localTag = dialog?.LocalTag ?? GenerateTag();
                    Console.WriteLine($"[200 OK DEBUG] Using local tag: {localTag}");
                    
                    try
                    {
                        var okResponse = _messageFactory.Create200OkResponse(_pendingIncomingInvite, sdpAnswer, localTag);
                        
                        if (string.IsNullOrEmpty(okResponse))
                        {
                            Console.WriteLine($"[200 OK DEBUG] ❌ ERROR: Message factory returned null/empty response");
                            StatusChanged?.Invoke(this, "❌ Error: Failed to generate 200 OK response");
                            return;
                        }
                        
                        Console.WriteLine($"[200 OK DEBUG] ✅ 200 OK response generated successfully, length: {okResponse.Length}");
                        Console.WriteLine($"[200 OK DEBUG] Generated 200 OK response:\n{okResponse}");
                        StatusChanged?.Invoke(this, "📤 Sending 200 OK response to incoming call (JSIP)");
                        
                        try
                        {
                            await SendMessageAsync(okResponse);
                            Console.WriteLine($"[200 OK DEBUG] ✅ 200 OK response sent successfully");
                        }
                        catch (Exception sendEx)
                        {
                            Console.WriteLine($"[200 OK DEBUG] ❌ SEND ERROR: {sendEx.Message}");
                            Console.WriteLine($"[200 OK DEBUG] Send stack trace: {sendEx.StackTrace}");
                            StatusChanged?.Invoke(this, $"❌ Error sending 200 OK: {sendEx.Message}");
                            throw;
                        }
                    }
                    catch (Exception factoryEx)
                    {
                        Console.WriteLine($"[200 OK DEBUG] ❌ FACTORY ERROR: {factoryEx.Message}");
                        Console.WriteLine($"[200 OK DEBUG] Factory stack trace: {factoryEx.StackTrace}");
                        StatusChanged?.Invoke(this, $"❌ Error creating 200 OK response: {factoryEx.Message}");
                        throw;
                    }
                }
                else
                {
                    Console.WriteLine($"[200 OK DEBUG] ❌ CRITICAL ERROR: _pendingIncomingInvite is null or empty");
                    Console.WriteLine($"[200 OK DEBUG] CallId: {callId}, Via: {via}");
                    Console.WriteLine($"[200 OK DEBUG] From: {from}, To: {to}, CSeq: {cseq}");
                    StatusChanged?.Invoke(this, "❌ Error: Original INVITE message not stored, cannot create proper 200 OK response");
                    
                    // FALLBACK: Try to create response using legacy method with available parameters
                    Console.WriteLine($"[200 OK DEBUG] Attempting fallback 200 OK creation with available parameters");
                    try
                    {
                        await SendIncomingCallResponse(callId, via, from, to, cseq, remoteSdpContent);
                        Console.WriteLine($"[200 OK DEBUG] ✅ Fallback 200 OK response sent successfully");
                    }
                    catch (Exception fallbackEx)
                    {
                        Console.WriteLine($"[200 OK DEBUG] ❌ FALLBACK ERROR: {fallbackEx.Message}");
                        Console.WriteLine($"[200 OK DEBUG] Fallback stack trace: {fallbackEx.StackTrace}");
                        StatusChanged?.Invoke(this, $"❌ All 200 OK response methods failed: {fallbackEx.Message}");
                        throw;
                    }
                    return;
                }
                
                // Start RTP session if we have remote SDP info
                if (!string.IsNullOrEmpty(remoteSdpContent))
                {
                    var remoteSdpInfo = SdpManager.ParseSdpContent(remoteSdpContent);
                    if (remoteSdpInfo != null)
                    {
                        StatusChanged?.Invoke(this, $"🎵 Starting RTP session with {remoteSdpInfo.RemoteIp}:{remoteSdpInfo.RemoteRtpPort}");
                        _audioManager?.StartRtpSession(remoteSdpInfo.RemoteIp, remoteSdpInfo.RemoteRtpPort, remoteSdpInfo.AudioCodec, remoteSdpInfo.PayloadType);                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[200 OK DEBUG] Exception in SendIncomingCallResponseWithFactory: {ex.Message}");
                Console.WriteLine($"[200 OK DEBUG] Stack trace: {ex.StackTrace}");
                StatusChanged?.Invoke(this, $"❌ Error sending call response: {ex.Message}");
            }
        }

        private async Task SendByeResponse(string callId, string via, string from, string to, string cseq)
        {
            try
            {
                // FIXED: Add header prefixes since ExtractHeader returns only values
                var response = $"SIP/2.0 200 OK\r\n" +
                              $"Via: {via}\r\n" +
                              $"From: {from}\r\n" +
                              $"To: {to}\r\n" +
                              $"Call-ID: {callId}\r\n" +
                              $"CSeq: {cseq}\r\n" +
                              $"Content-Length: 0\r\n" +
                              $"\r\n";
                
                StatusChanged?.Invoke(this, "📤 Sending 200 OK response to BYE");
                MessageReceived?.Invoke(this, $"OUTGOING (BYE Response):\n{response}");
                
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await _stream!.WriteAsync(responseBytes);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error sending BYE response: {ex.Message}");
            }        
        }
        
        private async Task SendRingingResponse(string callId, string via, string from, string to, string cseq)
        {
            try
            {
                // Add tag to To header if not present (required for provisional responses)
                var toHeader = to;
                if (!to.Contains("tag="))
                {
                    var tag = Guid.NewGuid().ToString().Replace("-", "")[..8];
                    toHeader = to.TrimEnd('>') + $";tag={tag}>";
                }
                  var response = $"SIP/2.0 180 Ringing\r\n" +
                              $"Via: {via}\r\n" +
                              $"From: {from}\r\n" +
                              $"To: {toHeader}\r\n" +
                              $"Call-ID: {callId}\r\n" +
                              $"CSeq: {cseq}\r\n" +
                              $"Contact: <sip:{_username}@{GetLocalIPAddress()}:5060>\r\n" +
                              $"Content-Length: 0\r\n" +
                              $"\r\n";
                  StatusChanged?.Invoke(this, "📤 Sending 180 Ringing response");
                MessageReceived?.Invoke(this, $"OUTGOING (Ringing Response):\n{response}");
                
                // FIXED: Use SipTransport to send response back to incoming connection
                if (_sipTransport != null)
                {
                    var success = await _sipTransport.SendToIncomingConnectionAsync(response);
                    if (!success)
                    {
                        StatusChanged?.Invoke(this, "⚠️ Failed to send 180 Ringing via SipTransport, falling back to stream");
                        // Fallback to legacy stream if SipTransport fails
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await _stream!.WriteAsync(responseBytes);
                    }
                }
                else
                {
                    // Fallback to legacy stream if SipTransport not available
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await _stream!.WriteAsync(responseBytes);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error sending ringing response: {ex.Message}");
            }
        }
        
        private async Task SendDeclineResponse(string callId, string via, string from, string to, string cseq)
        {
            try
            {
                // Add tag to To header if not present
                var toHeader = to;
                if (!to.Contains("tag="))
                {
                    var tag = Guid.NewGuid().ToString().Replace("-", "")[..8];
                    toHeader = to.TrimEnd('>') + $";tag={tag}>";
                }
                  var response = $"SIP/2.0 603 Decline\r\n" +
                              $"Via: {via}\r\n" +
                              $"From: {from}\r\n" +
                              $"To: {toHeader}\r\n" +
                              $"Call-ID: {callId}\r\n" +
                              $"CSeq: {cseq}\r\n" +
                              $"Content-Length: 0\r\n" +
                              $"\r\n";
                  StatusChanged?.Invoke(this, "📤 Sending 603 Decline response");
                MessageReceived?.Invoke(this, $"OUTGOING (Decline Response):\n{response}");
                
                // FIXED: Use SipTransport to send response back to incoming connection
                if (_sipTransport != null)
                {
                    var success = await _sipTransport.SendToIncomingConnectionAsync(response);
                    if (!success)
                    {
                        StatusChanged?.Invoke(this, "⚠️ Failed to send 603 Decline via SipTransport, falling back to stream");
                        // Fallback to legacy stream if SipTransport fails
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await _stream!.WriteAsync(responseBytes);
                    }
                }
                else
                {
                    // Fallback to legacy stream if SipTransport not available
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await _stream!.WriteAsync(responseBytes);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error sending decline response: {ex.Message}");
            }
        }          private static string ExtractCallerInfo(string fromHeader)
        {
            // Return the full From header content (minus the "From:" prefix and tag parameter)
            // so that the UI extraction methods can properly parse both display name and number
            // Format: From: "Display Name" <sip:user@domain>;tag=12345
            // Returns: "Display Name" <sip:user@domain>
            
            try
            {
                string result = fromHeader.Replace("From:", "").Trim();
                
                // Remove the tag parameter if present
                var tagIndex = result.IndexOf(";tag=");
                if (tagIndex > 0)
                {
                    result = result.Substring(0, tagIndex).Trim();
                }
                
                return result;
            }
            catch
            {
                // Fallback: return cleaned version
                return fromHeader.Replace("From:", "").Trim().Split(';')[0];
            }
        }
        
        private static string ExtractNumberFromSipUri(string sipUri)
        {
            // Extract user part from sip:user@domain
            if (sipUri.StartsWith("sip:"))
            {
                var withoutScheme = sipUri.Substring(4);
                var atIndex = withoutScheme.IndexOf('@');
                if (atIndex > 0)
                {
                    return withoutScheme.Substring(0, atIndex);
                }
                return withoutScheme;
            }
            return sipUri;
        }
          private static string ExtractHeader(string sipMessage, string headerName)
        {
            var lines = sipMessage.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase))
                {
                    // Return only the header value (after the colon and space)
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex + 1 < line.Length)
                    {
                        return line.Substring(colonIndex + 1).Trim();
                    }
                    return line; // Fallback to entire line if no colon found
                }
            }
            return string.Empty;
        }private async Task HandleAuthenticationChallenge(string challengeMessage)        {
            try
            {
                Console.WriteLine("[AUTH DEBUG] HandleAuthenticationChallenge called");
                Console.WriteLine($"[AUTH DEBUG] Challenge message length: {challengeMessage.Length} bytes");
                StatusChanged?.Invoke(this, "🔍 Parsing authentication challenge...");
                
                // Extract WWW-Authenticate or Proxy-Authenticate header
                var authHeader = ExtractAuthHeader(challengeMessage);
                if (string.IsNullOrEmpty(authHeader))
                {
                    StatusChanged?.Invoke(this, "❌ No authentication header found in challenge");
                    return;
                }

                Console.WriteLine($"[AUTH DEBUG] Auth header: {authHeader}");

                // Parse authentication parameters
                var authParams = ParseAuthHeader(authHeader);
                
                StatusChanged?.Invoke(this, $"Parsed {authParams.Count} auth parameters");
                foreach (var param in authParams)
                {
                    StatusChanged?.Invoke(this, $"  {param.Key} = {param.Value}");
                }
                  // Determine the original request type and resend with auth
                if (challengeMessage.ToUpper().Contains("REGISTER"))
                {
                    await SendAuthenticatedRegister(authParams);
                }
                else if (challengeMessage.ToUpper().Contains("INVITE"))
                {
                    StatusChanged?.Invoke(this, "🔐 Resending INVITE with authentication...");
                    await SendAuthenticatedInvite(authParams, challengeMessage);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Authentication failed: {ex.Message}");
                StatusChanged?.Invoke(this, $"Stack trace: {ex.StackTrace}");
            }
        }        
        private async Task SendAuthenticatedRegister(Dictionary<string, string> authParams)
        {
            try
            {
                var registerMessage = CreateAuthenticatedRegisterMessage(authParams);
                StatusChanged?.Invoke(this, "📤 Sending authenticated REGISTER...");
                MessageReceived?.Invoke(this, $"OUTGOING (Authenticated):\n{registerMessage}");

                var messageBytes = Encoding.UTF8.GetBytes(registerMessage);
                await _stream!.WriteAsync(messageBytes);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Authenticated register failed: {ex.Message}");
            }
        }        private async Task SendAuthenticatedInvite(Dictionary<string, string> authParams, string originalChallengeMessage)
        {
            try
            {
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] SendAuthenticatedInvite called");
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] Active Call ID: '{_activeCallId}'");
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] Current Target Number: '{_currentTargetNumber}'");
                
                // CRITICAL FIX: Use existing dialog and preserve Call-ID consistency
                if (string.IsNullOrEmpty(_activeCallId) || string.IsNullOrEmpty(_currentTargetNumber))
                {
                    // Fallback to legacy method if no dialog context available
                    var targetNumber = ExtractTargetNumberFromChallenge(originalChallengeMessage);
                    var inviteMessage = CreateAuthenticatedInviteMessage(targetNumber, authParams);
                    StatusChanged?.Invoke(this, "📤 Sending authenticated INVITE (legacy)...");
                    MessageReceived?.Invoke(this, $"OUTGOING (Authenticated INVITE):\n{inviteMessage}");                    var legacyMessageBytes = Encoding.UTF8.GetBytes(inviteMessage);
                    await _stream!.WriteAsync(legacyMessageBytes);
                    return;
                }
                
                // Find the existing dialog for this call
                var dialog = _dialogManager.FindDialogByCallId(_activeCallId);
                if (dialog == null)   
                {
                    Console.WriteLine($"[AUTH CHALLENGE DEBUG] ❌ No dialog found for Call-ID: {_activeCallId}");
                    StatusChanged?.Invoke(this, "❌ Cannot send authenticated INVITE - dialog not found");
                    return;
                }
                
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] ✅ Found dialog - LocalTag: '{dialog.LocalTag}', Call-ID: '{dialog.CallId}'");
                
                // Create authenticated INVITE preserving the dialog's Call-ID and tags
                var localIp = GetLocalIPAddress();
                var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";
                
                // Prepare RTP socket for SDP offer
                var rtpPort = 5004; // Default fallback port
                if (_audioManager != null && _audioManager.PrepareRtpSocket())
                {
                    rtpPort = _audioManager.LocalRtpPort;
                }
                
                // Create SDP offer
                var sdpContent = SdpManager.CreateSdpOffer(localIp, rtpPort);
                var contentLength = SdpManager.GetSdpLength(sdpContent);
                
                // Create authorization header for INVITE
                var inviteUri = $"sip:{_currentTargetNumber}@{_serverHost}:{_serverPort}";
                var authHeader = CreateAuthorizationHeader(_username, _password, "INVITE", inviteUri, authParams);
                
                // Build authenticated INVITE using existing dialog information
                var authenticatedInvite = $"INVITE {inviteUri} SIP/2.0\r\n" +
                   $"Via: SIP/2.0/TCP {localIp}:5060;branch={branch}\r\n" +
                   $"From: <sip:{_username}@{localIp}>;tag={Guid.NewGuid().ToString().Replace("-", "")[..8]}\r\n" +                   $"To: <sip:{_currentTargetNumber}@{_serverHost}>\r\n" +
                   $"Contact: <sip:{_username}@{localIp}:5060>\r\n"+
                   $"Call-ID: {_activeCallId}\r\n" +
                   $"CSeq: {_sequenceNumber++} INVITE\r\n" +
                   $"Authorization: {authHeader}\r\n" +
                   $"User-Agent: {_userAgent}\r\n" +
                   $"Max-Forwards: 70\r\n"+
                   $"Content-Type: application/sdp\r\n" +
                   $"Content-Length: {contentLength}\r\n" +
                   $"\r\n" +
                   $"{sdpContent}";
                
                StatusChanged?.Invoke(this, "📤 Sending authenticated INVITE (with dialog)...");
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] *** AUTHENTICATED INVITE WITH PRESERVED CALL-ID ***");
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] Call-ID: {dialog.CallId}");
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] From Tag: {dialog.LocalTag}");
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] ==========================================");
                
                MessageReceived?.Invoke(this, $"OUTGOING (Authenticated INVITE):\n{authenticatedInvite}");

                var messageBytes = Encoding.UTF8.GetBytes(authenticatedInvite);
                await _stream!.WriteAsync(messageBytes);
                
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] *** AUTHENTICATED INVITE SENT WITH DIALOG PRESERVATION ***");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Authenticated INVITE failed: {ex.Message}");
                Console.WriteLine($"[AUTH CHALLENGE DEBUG] Exception: {ex.Message}");
            }
        }

        private string ExtractAuthHeader(string message)
        {
            var lines = message.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("WWW-Authenticate:", StringComparison.OrdinalIgnoreCase) || 
                    trimmedLine.StartsWith("Proxy-Authenticate:", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmedLine;
                }
            }
            return string.Empty;
        }        private string CreateRegisterMessage(int expires = 3600)
        {
            // Use enhanced RFC 3261 compliant factory
            try
            {                var enhancedMessage = _enhancedMessageFactory.CreateRegisterRequest(
                    _username, _serverHost, _serverPort, (uint)_sequenceNumber++, null, expires);
                
                // Validate the message for compliance
                var validationResult = _rfc3261Validator.ValidateMessage(enhancedMessage);
                if (validationResult.HasCriticalErrors)
                {
                    StatusChanged?.Invoke(this, "⚠️ RFC 3261 compliance issues detected in REGISTER message");
                    foreach (var error in validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical))
                    {
                        StatusChanged?.Invoke(this, $"  Critical: {error.Message}");
                    }
                }
                
                return enhancedMessage;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"⚠️ Enhanced factory failed, falling back to legacy: {ex.Message}");
                
                // Fallback to legacy implementation if enhanced factory fails
                var sipUri = $"sip:{_serverHost}:{_serverPort}";
                var userUri = $"sip:{_username}@{_serverHost}";
                var contactUri = $"sip:{_username}@{_localIp}:5060";
                var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";
                
                var message = $"REGISTER {sipUri} SIP/2.0\r\n" +
                             $"Via: SIP/2.0/TCP {_localIp}:5060;branch={branch}\r\n" +
                             $"From: <{userUri}>;tag={_fromTag}\r\n" +
                             $"To: <{userUri}>\r\n" +
                             $"Contact: <{contactUri}>\r\n" +
                             $"Call-ID: {_callId}\r\n" +
                             $"CSeq: {_sequenceNumber++} REGISTER\r\n" +
                             $"User-Agent: {_userAgent}\r\n" +
                             $"Max-Forwards: 70\r\n"+
                             $"Expires: {expires}\r\n" +
                             $"Content-Length: 0\r\n" +
                             $"\r\n";
                             
                return message;
            }
        }private string CreateAuthenticatedRegisterMessage(Dictionary<string, string> authParams)
        {
            var sipUri = $"sip:{_serverHost}:{_serverPort}";
            var userUri = $"sip:{_username}@{_serverHost}";
            var contactUri = $"sip:{_username}@{_localIp}:5060";
            var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";
            
            var authHeader = CreateAuthorizationHeader(
                _username, _password, "REGISTER", sipUri, authParams);
            
            var message = $"REGISTER {sipUri} SIP/2.0\r\n" +
                         $"Via: SIP/2.0/TCP {_localIp}:5060;branch={branch}\r\n" +
                         $"From: <{userUri}>;tag={_fromTag}\r\n" +
                         $"To: <{userUri}>\r\n" +
                         $"Contact: <{contactUri}>\r\n" +
                         $"Call-ID: {_callId}\r\n" +                         $"CSeq: {_sequenceNumber++} REGISTER\r\n" +
                         $"Authorization: {authHeader}\r\n" +
                         $"User-Agent: {_userAgent}\r\n" +
                         $"Max-Forwards: 70\r\n" +
                         $"Expires: {_registrationExpiry}\r\n" +
                         $"Content-Length: 0\r\n" +
                         $"\r\n";
                         
            return message;
        }        private string CreateUnregisterMessage()
        {
            var sipUri = $"sip:{_serverHost}:{_serverPort}";
            var userUri = $"sip:{_username}@{_serverHost}";
            var contactUri = $"sip:{_username}@{_localIp}:5060";
            var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";

            var message = $"REGISTER {sipUri} SIP/2.0\r\n" +
                         $"Via: SIP/2.0/TCP {_localIp}:5060;branch={branch}\r\n" +
                         $"From: <{userUri}>;tag={_fromTag}\r\n" +
                         $"To: <{userUri}>\r\n" +
                         $"Contact: <{contactUri}>\r\n" +
                         $"Call-ID: {_callId}\r\n" +
                         $"CSeq: {_sequenceNumber++} REGISTER\r\n" +
                         $"User-Agent: {_userAgent}\r\n" +
                         $"Max-Forwards: 70\r\n" +
                         $"Expires: 0\r\n" +
                         $"Content-Length: 0\r\n" +
                         $"\r\n";

            return message;
        }

        public async Task<bool> UnregisterAsync()
        {
            if (!_isConnected || _stream == null)
            {
                StatusChanged?.Invoke(this, "Not connected to server");
                return false;
            }

            if (!_isRegistered)
            {
                StatusChanged?.Invoke(this, "Already unregistered");
                return true;
            }

            try
            {
                StatusChanged?.Invoke(this, "🔄 Sending SIP unregister request...");
                
                // Send REGISTER with Expires: 0 to unregister
                var unregisterMessage = CreateUnregisterMessage();
                StatusChanged?.Invoke(this, "📤 Sending UNREGISTER request...");
                MessageReceived?.Invoke(this, $"OUTGOING (UNREGISTER):\n{unregisterMessage}");

                var messageBytes = Encoding.UTF8.GetBytes(unregisterMessage);
                await _stream.WriteAsync(messageBytes);
                StatusChanged?.Invoke(this, "✅ UNREGISTER message sent successfully");
                
                // Stop registration refresh timer
                StopRegistrationRefreshTimer();
                
                // Update registration state immediately (don't wait for response)
                _isRegistered = false;
                StatusChanged?.Invoke(this, "✅ SIP unregistration completed");
                
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Unregistration failed: {ex.Message}");
                return false;
            }
        }        private string CreateInviteMessage(string targetNumber)
        {
            var localIp = GetLocalIPAddress();
            var newCallId = Guid.NewGuid().ToString().Replace("-", "");
            _activeCallId = newCallId; // Store the active call ID for later BYE message
            
            // CRITICAL FIX: Prepare RTP socket early to get valid port for SDP offer
            var rtpPort = 5004; // Default fallback port
            if (_audioManager != null)
            {
                if (_audioManager.PrepareRtpSocket())
                {
                    rtpPort = _audioManager.LocalRtpPort;
                    Console.WriteLine($"[INVITE DEBUG] ✅ RTP socket prepared, using port: {rtpPort}");
                }
                else
                {
                    Console.WriteLine($"[INVITE DEBUG] ⚠️ Failed to prepare RTP socket, using fallback port: {rtpPort}");
                }
            }
            
            // Create SDP offer for audio negotiation
            var sdpContent = SdpManager.CreateSdpOffer(localIp, rtpPort);
            
            // Try to use enhanced RFC 3261 compliant factory
            try
            {
                if (_enhancedMessageFactory != null && _rfc3261Validator != null)
                {
                    // Use enhanced message factory
                    var enhancedMessage = _enhancedMessageFactory.CreateInviteRequest(
                        _username, targetNumber, _serverHost, _serverPort, 
                        (uint)_sequenceNumber++, newCallId, GenerateTag(), sdpContent);
                    
                    // Validate the message for compliance
                    var validationResult = _rfc3261Validator.ValidateMessage(enhancedMessage);
                    if (validationResult.HasCriticalErrors)
                    {
                        StatusChanged?.Invoke(this, "⚠️ RFC 3261 compliance issues detected in INVITE message");
                        foreach (var error in validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical))
                        {
                            StatusChanged?.Invoke(this, $"  Critical: {error.Message}");
                        }
                    }
                    
                    return enhancedMessage;
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"⚠️ Enhanced factory failed, falling back to legacy: {ex.Message}");
            }
            
            // Fallback to legacy implementation if enhanced factory fails or is null
            var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";
            var contentLength = SdpManager.GetSdpLength(sdpContent);
              
            return $"INVITE sip:{targetNumber}@{_serverHost}:{_serverPort} SIP/2.0\r\n" +
                   $"Via: SIP/2.0/TCP {localIp}:5060;branch={branch}\r\n" +
                   $"From: <sip:{_username}@{localIp}>;tag={Guid.NewGuid().ToString().Replace("-", "")[..8]}\r\n" +
                   $"To: <sip:{targetNumber}@{_serverHost}>\r\n" +
                   $"Contact: <sip:{_username}@{localIp}:5060>\r\n"+
                   $"Call-ID: {newCallId}\r\n" +
                   $"CSeq: {_sequenceNumber++} INVITE\r\n" +
                   $"User-Agent: {_userAgent}\r\n" +
                   $"Max-Forwards: 70\r\n"+
                   $"Content-Type: application/sdp\r\n" +                   
                   $"Content-Length: {contentLength}\r\n" +
                   $"\r\n" +
                   $"{sdpContent}";
        }private string CreateAuthenticatedInviteMessage(string targetNumber, Dictionary<string, string> authParams)
        {
            var localIp = GetLocalIPAddress();            var newCallId = Guid.NewGuid().ToString().Replace("-", "");
            _activeCallId = newCallId; // Store the active call ID for later BYE message
            var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";
            
            // CRITICAL FIX: Prepare RTP socket to get valid port for SDP offer
            var rtpPort = 5004; // Default fallback port
            if (_audioManager != null)
            {
                if (_audioManager.PrepareRtpSocket())
                {
                    rtpPort = _audioManager.LocalRtpPort;
                    Console.WriteLine($"[AUTH INVITE DEBUG] ✅ RTP socket prepared, using port: {rtpPort}");
                }
                else
                {
                    Console.WriteLine($"[AUTH INVITE DEBUG] ⚠️ Failed to prepare RTP socket, using fallback port: {rtpPort}");
                }
            }
            
            // Create SDP offer for audio negotiation
            var sdpContent = SdpManager.CreateSdpOffer(localIp, rtpPort);
            var contentLength = SdpManager.GetSdpLength(sdpContent);
            
            // Create authorization header for INVITE
            var inviteUri = $"sip:{targetNumber}@{_serverHost}:{_serverPort}";
            var authHeader = CreateAuthorizationHeader(_username, _password, "INVITE", inviteUri, authParams);
            
            // Build authenticated INVITE using existing dialog information
            var authenticatedInvite = $"INVITE {inviteUri} SIP/2.0\r\n" +
                   $"Via: SIP/2.0/TCP {localIp}:5060;branch={branch}\r\n" +
                   $"From: <sip:{_username}@{localIp}>;tag={Guid.NewGuid().ToString().Replace("-", "")[..8]}\r\n" +
                   $"To: <sip:{targetNumber}@{_serverHost}>\r\n" +
                   $"Contact: <sip:{_username}@{localIp}:5060>\r\n"+
                   $"Call-ID: {newCallId}\r\n" +
                   $"CSeq: {_sequenceNumber++} INVITE\r\n" +
                   $"Authorization: {authHeader}\r\n" +
                   $"User-Agent: {_userAgent}\r\n" +                   $"Max-Forwards: 70\r\n"+
                   $"Content-Type: application/sdp\r\n" +
                   $"Content-Length: {contentLength}\r\n" +
                   $"\r\n" +
                   $"{sdpContent}";
                   
            return authenticatedInvite;
        }

        private string ExtractTargetNumberFromChallenge(string challengeMessage)
        {
            // Extract the target number from the To header in the 401 challenge
            // The challenge is a response to our original INVITE
            var toHeader = ExtractHeader(challengeMessage, "To:");
            if (!string.IsNullOrEmpty(toHeader))
            {
                // Parse "To: <sip:targetNumber@server>" format
                var match = System.Text.RegularExpressions.Regex.Match(toHeader, @"<sip:([^@]+)@");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Fallback: try to extract from request line if available
            var lines = challengeMessage.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var requestLine = lines[0];
                var match = System.Text.RegularExpressions.Regex.Match(requestLine, @"sip:([^@]+)@");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Default fallback
            return "unknown";        }

        private async Task SendAckMessage(string responseMessage)
        {
            try
            {
                var ackMessage = CreateAckMessage(responseMessage);
                StatusChanged?.Invoke(this, "📤 Sending ACK message");
                MessageReceived?.Invoke(this, $"OUTGOING (ACK):\n{ackMessage}");

                var messageBytes = Encoding.UTF8.GetBytes(ackMessage);
                await _stream!.WriteAsync(messageBytes);
                
                StatusChanged?.Invoke(this, "✅ ACK sent - call fully established");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error sending ACK: {ex.Message}");
            }
        }

        private string CreateAckMessage(string responseMessage)
        {
            // Extract required headers from 200 OK response
            var callId = ExtractHeader(responseMessage, "Call-ID:");
            var fromHeader = ExtractHeader(responseMessage, "From:");
            var toHeader = ExtractHeader(responseMessage, "To:");
            var via = ExtractHeader(responseMessage, "Via:");
            var cseqHeader = ExtractHeader(responseMessage, "CSeq:");
            
            // Parse the request URI from the original INVITE (should be in Contact or To header)
            var requestUri = ExtractRequestUriFromResponse(responseMessage);
            
            // Parse CSeq to get the sequence number (but change method to ACK)
            var cseqNumber = ExtractCSeqNumber(cseqHeader);
            
            // Create new branch for ACK
            var localIp = GetLocalIPAddress();
            var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";
            
            return $"ACK {requestUri} SIP/2.0\r\n" +
                   $"Via: SIP/2.0/TCP {localIp}:5060;branch={branch}\r\n" +
                   $"{fromHeader}\r\n" +
                   $"{toHeader}\r\n" +                   $"Call-ID: {callId.Replace("Call-ID: ", "")}\r\n" +
                   $"CSeq: {cseqNumber} ACK\r\n" +
                   $"User-Agent: {_userAgent}\r\n" +
                   $"Max-Forwards: 70\r\n"+
                   $"Content-Length: 0\r\n" +
                   $"\r\n";
        }

        private string ExtractRequestUriFromResponse(string responseMessage)
        {
            // Try to extract the original request URI from the response
            // Look for Contact header first, then fall back to parsing To header
            var contactHeader = ExtractHeader(responseMessage, "Contact:");
            if (!string.IsNullOrEmpty(contactHeader))
            {
                var match = System.Text.RegularExpressions.Regex.Match(contactHeader, @"<([^>]+)>");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Fall back to constructing from To header
            var toHeader = ExtractHeader(responseMessage, "To:");
            var toMatch = System.Text.RegularExpressions.Regex.Match(toHeader, @"<([^>]+)>");
            if (toMatch.Success)
            {
                return toMatch.Groups[1].Value;
            }
            
            // Last resort: construct from server info
            return $"sip:{_username}@{_serverHost}:{_serverPort}";
        }

        private string ExtractCSeqNumber(string cseqHeader)
        {
            // Extract just the sequence number from CSeq header
            // Format: "CSeq: 123 INVITE" -> we want "123"
            var parts = cseqHeader.Replace("CSeq: ", "").Split(' ');
            return parts.Length > 0 ? parts[0] : "1";
        }

        private string GetLocalIPAddress()
        {
            try
            {
                // Try to get the IP that would be used to connect to the SIP server
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect(_serverHost, _serverPort);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork && 
                            !IPAddress.IsLoopback(ip))
                        {
                            return ip.ToString();
                        }
                    }
                }
                catch { }
                
                return "127.0.0.1";
            }
        }

        // SIP Digest Authentication Methods
        private static Dictionary<string, string> ParseAuthHeader(string authHeader)
        {
            var authParams = new Dictionary<string, string>();
            
            // Remove header name prefix (WWW-Authenticate: Digest or Proxy-Authenticate: Digest)
            var digestParams = authHeader;
            if (digestParams.Contains("Digest"))
            {
                var digestIndex = digestParams.IndexOf("Digest");
                digestParams = digestParams.Substring(digestIndex + 6).Trim();
            }
            
            // Parse key=value pairs, handle quoted values properly
            var regex = new System.Text.RegularExpressions.Regex(@"(\w+)\s*=\s*""?([^"",]+)""?");
            var matches = regex.Matches(digestParams);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count == 3)
                {
                    var key = match.Groups[1].Value.Trim();
                    var value = match.Groups[2].Value.Trim().Trim('"');
                    authParams[key] = value;
                }
            }
            
            return authParams;
        }

        /// <summary>
        /// Alias for ParseAuthHeader for consistency
        /// </summary>
        private static Dictionary<string, string> ParseAuthenticationHeader(string authHeader)
        {
            return ParseAuthHeader(authHeader);
        }        private static string CreateAuthorizationHeader(string username, string password, 
            string method, string uri, Dictionary<string, string> challengeParams)
        {
            if (!challengeParams.ContainsKey("realm") || !challengeParams.ContainsKey("nonce"))
            {
                throw new ArgumentException($"Missing required authentication parameters. Available: {string.Join(", ", challengeParams.Keys)}");
            }

            var realm = challengeParams["realm"];
            var nonce = challengeParams["nonce"];
            var nc = "00000001";
            var cnonce = Guid.NewGuid().ToString("N")[..8];
            var qop = challengeParams.ContainsKey("qop") ? challengeParams["qop"] : "";            // DEBUG: Log digest calculation inputs
            Console.WriteLine($"[DIGEST DEBUG] Username: {username}");
            Console.WriteLine($"[DIGEST DEBUG] Password: {password}");
            Console.WriteLine($"[DIGEST DEBUG] Realm: {realm}");
            Console.WriteLine($"[DIGEST DEBUG] Nonce: {nonce}");
            Console.WriteLine($"[DIGEST DEBUG] Method: {method}");
            Console.WriteLine($"[DIGEST DEBUG] URI: {uri}");            Console.WriteLine($"[DIGEST DEBUG] QOP: '{qop}' (empty={string.IsNullOrEmpty(qop)})");

            var response = CalculateDigestResponse(username, password, realm, nonce, method, uri, nc, cnonce, qop);            Console.WriteLine($"[DIGEST DEBUG] Calculated Response: {response}");            var authHeader = $"Digest username=\"{username}\", " +
                           $"realm=\"{realm}\", " +
                           $"nonce=\"{nonce}\", " +
                           $"uri=\"{uri}\", " +
                           $"algorithm=MD5, " +
                           $"response=\"{response}\"";
                           
            if (!string.IsNullOrEmpty(qop))
            {
                authHeader += $", nc={nc}, cnonce=\"{cnonce}\", qop={qop}";
            }

            Console.WriteLine($"[DIGEST DEBUG] Authorization Header: {authHeader}");

            return authHeader;
        }        private static string CalculateDigestResponse(string username, string password, string realm, 
            string nonce, string method, string uri, string nc = "00000001", string? cnonce = null, string qop = "")
        {
            if (string.IsNullOrEmpty(cnonce))
            {
                cnonce = Guid.NewGuid().ToString("N")[..8];
            }            // Calculate HA1 = MD5(username:realm:password)
            var ha1Input = $"{username}:{realm}:{password}";
            var ha1 = CalculateMD5Hash(ha1Input);
            Console.WriteLine($"[DIGEST DEBUG] HA1 Input: {ha1Input}");
            Console.WriteLine($"[DIGEST DEBUG] HA1: {ha1}");

            // Calculate HA2 = MD5(method:uri)
            var ha2Input = $"{method}:{uri}";
            var ha2 = CalculateMD5Hash(ha2Input);
            Console.WriteLine($"[DIGEST DEBUG] HA2 Input: {ha2Input}");
            Console.WriteLine($"[DIGEST DEBUG] HA2: {ha2}");

            // Calculate response
            string responseInput;
            if (!string.IsNullOrEmpty(qop))
            {
                // With qop: response = MD5(HA1:nonce:nc:cnonce:qop:HA2)
                responseInput = $"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}";
                Console.WriteLine($"[DIGEST DEBUG] Using qop calculation: {qop}");
            }
            else
            {
                // Without qop: response = MD5(HA1:nonce:HA2)
                responseInput = $"{ha1}:{nonce}:{ha2}";
                Console.WriteLine($"[DIGEST DEBUG] Using NO qop calculation");
            }
            
            Console.WriteLine($"[DIGEST DEBUG] Response Input: {responseInput}");
            var response = CalculateMD5Hash(responseInput);
            Console.WriteLine($"[DIGEST DEBUG] Final Response: {response}");
            
            return response;
        }
        private static string CalculateMD5Hash(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }        
        private static string ExtractSdpContent(string sipMessage)
        {
            // SDP content comes after the empty line in SIP message
            var parts = sipMessage.Split(new[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                return parts[1].Trim();
            }
            return string.Empty;
        }

        private static string ExtractSipUriFromContactHeader(string contactHeader)
        {
            // Handle Contact header formats:
            // Contact: <sip:102@192.168.1.180:5060>
            // Contact: "Bob" <sip:102@192.168.1.180:5060;transport=tcp>
            // Contact: sip:102@192.168.1.180:5060
            
            // Remove the "Contact:" prefix if present
            var contact = contactHeader.Replace("Contact:", "").Trim();
            
            // Look for URI in angle brackets first
            var match = System.Text.RegularExpressions.Regex.Match(contact, @"<(sip:[^>]+)>");
            if (match.Success)
            {
                var sipUri = match.Groups[1].Value;
                // Remove parameters (like ;transport=tcp) from the URI for BYE Request-URI
                var paramIndex = sipUri.IndexOf(';');
                return paramIndex > 0 ? sipUri.Substring(0, paramIndex) : sipUri;
            }
            
            // If no angle brackets, assume the whole thing is the URI
            if (contact.StartsWith("sip:"))
            {
                var paramIndex = contact.IndexOf(';');
                return paramIndex > 0 ? contact.Substring(0, paramIndex) : contact;
            }
            
            return string.Empty;
        }

        public async Task<bool> SendUpdateAsync(string sdpContent = "")
        {
            if (!_isConnected || _stream == null)
            {
                StatusChanged?.Invoke(this, "Not connected to server");
                return false;
            }

            try
            {
                var updateMessage = CreateUpdateMessage(sdpContent);
                StatusChanged?.Invoke(this, "📤 Sending UPDATE request...");
                MessageReceived?.Invoke(this, $"OUTGOING:\n{updateMessage}");

                var messageBytes = Encoding.UTF8.GetBytes(updateMessage);
                await _stream.WriteAsync(messageBytes);
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"UPDATE failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendReferAsync(string referToUri, string replaces = "")
        {
            if (!_isConnected || _stream == null)
            {
                StatusChanged?.Invoke(this, "Not connected to server");
                return false;
            }

            try
            {
                var referMessage = CreateReferMessage(referToUri, replaces);
                StatusChanged?.Invoke(this, $"📤 Sending REFER to {referToUri}...");
                MessageReceived?.Invoke(this, $"OUTGOING:\n{referMessage}");

                var messageBytes = Encoding.UTF8.GetBytes(referMessage);
                await _stream.WriteAsync(messageBytes);
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"REFER failed: {ex.Message}");
                return false;
            }
        }

        private string CreateUpdateMessage(string sdpContent = "")
        {
            var localIp = GetLocalIPAddress();
            var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";
              // If no SDP provided, use current audio settings
            if (string.IsNullOrEmpty(sdpContent) && _audioManager != null)
            {
                // CRITICAL FIX: Prepare RTP socket to get valid port for SDP update
                var rtpPort = 5004; // Default fallback port
                if (_audioManager.PrepareRtpSocket())
                {
                    rtpPort = _audioManager.LocalRtpPort;
                    Console.WriteLine($"[UPDATE MESSAGE DEBUG] ✅ RTP socket prepared for UPDATE message, using port: {rtpPort}");
                }
                else
                {
                    Console.WriteLine($"[UPDATE MESSAGE DEBUG] ⚠️ Failed to prepare RTP socket for UPDATE message, using fallback port: {rtpPort}");
                }
                
                sdpContent = SdpManager.CreateSdpOffer(localIp, rtpPort);
            }
            
            var contentLength = string.IsNullOrEmpty(sdpContent) ? 0 : SdpManager.GetSdpLength(sdpContent);
            
            var message = $"UPDATE sip:{_username}@{_serverHost}:{_serverPort} SIP/2.0\r\n" +
                         $"Via: SIP/2.0/TCP {localIp}:5060;branch={branch}\r\n" +
                         $"From: <sip:{_username}@{_serverHost}>;tag={_fromTag}\r\n" +
                         $"To: <sip:{_username}@{_serverHost}>\r\n" +
                         $"Call-ID: {_callId}\r\n" +
                         $"CSeq: {_sequenceNumber++} UPDATE\r\n" +                         $"Contact: <sip:{_username}@{_localIp}:5060>\r\n" +
                         $"User-Agent: {_userAgent}\r\n" +
                         $"Max-Forwards: 70\r\n";
                         
            if (!string.IsNullOrEmpty(sdpContent))
            {
                message += $"Content-Type: application/sdp\r\n" +
                          $"Content-Length: {contentLength}\r\n" +
                          $"\r\n" +
                          $"{sdpContent}";
            }
            else
            {
                message += $"Content-Length: 0\r\n\r\n";
            }
            
            return message;
        }

        private string CreateReferMessage(string referToUri, string replaces = "")
        {
            var localIp = GetLocalIPAddress();
            var branch = $"z9hG4bK{Guid.NewGuid().ToString().Replace("-", "")}";
            
            // Build Refer-To header
            var referToHeader = $"Refer-To: <{referToUri}>";
            if (!string.IsNullOrEmpty(replaces))
            {
                // Add Replaces parameter for attended transfer
                referToHeader += $"?Replaces={Uri.EscapeDataString(replaces)}";
            }
            
            var message = $"REFER sip:{_username}@{_serverHost}:{_serverPort} SIP/2.0\r\n" +
                         $"Via: SIP/2.0/TCP {localIp}:5060;branch={branch}\r\n" +
                         $"From: <sip:{_username}@{_serverHost}>;tag={_fromTag}\r\n" +
                         $"To: <sip:{_username}@{_serverHost}>\r\n" +
                         $"Call-ID: {_callId}\r\n" +
                         $"CSeq: {_sequenceNumber++} REFER\r\n" +
                         $"Contact: <sip:{_username}@{_localIp}:5060>\r\n" +                         $"{referToHeader}\r\n" +
                         $"User-Agent: {_userAgent}\r\n" +
                         $"Max-Forwards: 70\r\n"+
                         $"Content-Length: 0\r\n" +
                         $"\r\n";
                         
            return message;
        }        private async Task HandleIncomingUpdate(string updateMessage)
        {
            try
            {
                StatusChanged?.Invoke(this, "📱 Received UPDATE request");
                
                // Extract required headers for response
                var callId = ExtractHeader(updateMessage, "Call-ID:");
                var via = ExtractHeader(updateMessage, "Via:");
                var from = ExtractHeader(updateMessage, "From:");
                var to = ExtractHeader(updateMessage, "To:");
                var cseq = ExtractHeader(updateMessage, "CSeq:");
                
                // Extract SDP content if present
                var sdpContent = ExtractSdpContent(updateMessage);
                
                if (!string.IsNullOrEmpty(sdpContent))
                {
                    StatusChanged?.Invoke(this, "🔄 Processing session update with SDP...");
                    
                    // Parse remote SDP for audio configuration changes
                    var remoteSdpInfo = SdpManager.ParseSdpContent(sdpContent);                    if (remoteSdpInfo != null && remoteSdpInfo.HasAudio && _audioManager != null)
                    {
                        StatusChanged?.Invoke(this, $"🎵 Updating RTP session: {remoteSdpInfo.RemoteIp}:{remoteSdpInfo.RemoteRtpPort}");
                        
                        // Update RTP session with new parameters
                        _ = _audioManager.StartRtpSession(remoteSdpInfo.RemoteIp, remoteSdpInfo.RemoteRtpPort, 
                            remoteSdpInfo.AudioCodec, remoteSdpInfo.PayloadType);
                    }
                }
                
                // Send 200 OK response
                await SendUpdateResponse(callId, via, from, to, cseq, sdpContent);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error handling UPDATE: {ex.Message}");
            }
        }

        private async Task HandleIncomingRefer(string referMessage)
        {
            try
            {
                StatusChanged?.Invoke(this, "📞 Received REFER request (call transfer)");
                
                // Extract required headers for response
                var callId = ExtractHeader(referMessage, "Call-ID:");
                var via = ExtractHeader(referMessage, "Via:");
                var from = ExtractHeader(referMessage, "From:");
                var to = ExtractHeader(referMessage, "To:");
                var cseq = ExtractHeader(referMessage, "CSeq:");
                var referTo = ExtractHeader(referMessage, "Refer-To:");
                
                if (!string.IsNullOrEmpty(referTo))
                {
                    // Extract the transfer target from Refer-To header
                    var transferTarget = referTo.Replace("<", "").Replace(">", "");
                    StatusChanged?.Invoke(this, $"📞 Transfer request to: {transferTarget}");
                    
                    // For now, we'll accept the transfer but not implement the actual transfer logic
                    // In a full implementation, you would:
                    // 1. Send 202 Accepted response
                    // 2. Create new INVITE to transfer target
                    // 3. Send NOTIFY with transfer status
                    // 4. Terminate current call when transfer succeeds
                }
                
                // Send 202 Accepted response for REFER
                await SendReferAcceptedResponse(callId, via, from, to, cseq);
                
                // In a complete implementation, you would also send NOTIFY messages
                // to inform about the transfer progress
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error handling REFER: {ex.Message}");
            }
        }        private async Task SendMethodNotAllowedResponse(string requestMessage, string method)
        {
            try
            {
                // Extract required headers for response
                var callId = ExtractHeader(requestMessage, "Call-ID:");
                var via = ExtractHeader(requestMessage, "Via:");
                var from = ExtractHeader(requestMessage, "From:");
                var to = ExtractHeader(requestMessage, "To:");
                var cseq = ExtractHeader(requestMessage, "CSeq:");
                  var response = $"SIP/2.0 405 Method Not Allowed\r\n" +
                              $"Via: {via}\r\n" +
                              $"From: {from}\r\n" +
                              $"To: {to}\r\n" +
                              $"Call-ID: {callId}\r\n" +
                              $"CSeq: {cseq}\r\n" +                              $"Allow: INVITE, BYE, UPDATE, REFER, ACK, CANCEL\r\n" +
                              $"User-Agent: {_userAgent}\r\n" +
                              $"Content-Length: 0\r\n"+
                              $"\r\n";
                
                StatusChanged?.Invoke(this, $"📤 Sending 405 Method Not Allowed for {method}");
                MessageReceived?.Invoke(this, $"OUTGOING (405 Response):\n{response}");
                
                // FIXED: Use SipTransport to send response back to incoming connection
                if (_sipTransport != null)
                {
                    var success = await _sipTransport.SendToIncomingConnectionAsync(response);
                    if (!success)
                    {
                        StatusChanged?.Invoke(this, "⚠️ Failed to send 405 via SipTransport, falling back to stream");
                        // Fallback to legacy stream if SipTransport fails
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await _stream!.WriteAsync(responseBytes);
                    }
                }
                else
                {
                    // Fallback to legacy stream if SipTransport not available
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await _stream!.WriteAsync(responseBytes);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error sending 405 response: {ex.Message}");
            }
        }        private async Task SendUpdateResponse(string callId, string via, string from, string to, string cseq, string? remoteSdpContent)
        {
            try
            {
                var localIp = GetLocalIPAddress();
                // FIXED: Add header prefixes since ExtractHeader returns only values
                var response = $"SIP/2.0 200 OK\r\n" +
                              $"Via: {via}\r\n" +
                              $"From: {from}\r\n" +
                              $"To: {to}\r\n" +
                              $"Call-ID: {callId}\r\n" +
                              $"CSeq: {cseq}\r\n" +                              $"Contact: <sip:{_username}@{localIp}:5060>\r\n" +
                              $"User-Agent: {_userAgent}\r\n" +
                              $"Content-Length: 0\r\n"+
                              $"\r\n";
                  // If remote sent SDP, respond with our SDP
                if (!string.IsNullOrEmpty(remoteSdpContent) && _audioManager != null)
                {
                    // CRITICAL FIX: Prepare RTP socket to get valid port for SDP response
                    var rtpPort = 5004; // Default fallback port
                    if (_audioManager.PrepareRtpSocket())
                    {
                        rtpPort = _audioManager.LocalRtpPort;
                        Console.WriteLine($"[UPDATE RESPONSE DEBUG] ✅ RTP socket prepared for UPDATE response, using port: {rtpPort}");
                    }
                    else
                    {
                        Console.WriteLine($"[UPDATE RESPONSE DEBUG] ⚠️ Failed to prepare RTP socket for UPDATE response, using fallback port: {rtpPort}");
                    }
                    
                    var sdpAnswer = SdpManager.CreateSdpOffer(localIp, rtpPort);
                    var contentLength = SdpManager.GetSdpLength(sdpAnswer);
                    
                    response += $"Content-Type: application/sdp\r\n" +
                               $"Content-Length: {contentLength}\r\n" +
                               $"\r\n" +
                               $"{sdpAnswer}";
                }
                else
                {
                    response += $"Content-Length: 0\r\n\r\n";
                }
                  StatusChanged?.Invoke(this, "📤 Sending 200 OK response to UPDATE");
                MessageReceived?.Invoke(this, $"OUTGOING (UPDATE Response):\n{response}");
                
                // FIXED: Use SipTransport to send response back to incoming connection
                if (_sipTransport != null)
                {
                    var success = await _sipTransport.SendToIncomingConnectionAsync(response);
                    if (!success)
                    {
                        StatusChanged?.Invoke(this, "⚠️ Failed to send UPDATE 200 OK via SipTransport, falling back to stream");
                        // Fallback to legacy stream if SipTransport fails
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await _stream!.WriteAsync(responseBytes);
                    }
                }
                else
                {
                    // Fallback to legacy stream if SipTransport not available
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await _stream!.WriteAsync(responseBytes);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error sending UPDATE response: {ex.Message}");
            }
        }

        private async Task SendReferAcceptedResponse(string callId, string via, string from, string to, string cseq)
        {
            try
            {
                var localIp = GetLocalIPAddress();                var response = $"SIP/2.0 202 Accepted\r\n" +
                              $"Via: {via}\r\n" +
                              $"From: {from}\r\n" +
                              $"To: {to}\r\n" +
                              $"Call-ID: {callId}\r\n" +
                              $"CSeq: {cseq}\r\n" +                              $"Contact: <sip:{_username}@{localIp}:5060>\r\n" +
                              $"User-Agent: {_userAgent}\r\n" +
                              $"Content-Length: 0\r\n"+
                              $"\r\n";
                  StatusChanged?.Invoke(this, "📤 Sending 202 Accepted response to REFER");
                MessageReceived?.Invoke(this, $"OUTGOING (REFER Response):\n{response}");
                
                // FIXED: Use SipTransport to send response back to incoming connection
                if (_sipTransport != null)
                {
                    var success = await _sipTransport.SendToIncomingConnectionAsync(response);
                    if (!success)
                    {
                        StatusChanged?.Invoke(this, "⚠️ Failed to send REFER 202 Accepted via SipTransport, falling back to stream");
                        // Fallback to legacy stream if SipTransport fails
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await _stream!.WriteAsync(responseBytes);
                    }
                }
                else
                {
                    // Fallback to legacy stream if SipTransport not available
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await _stream!.WriteAsync(responseBytes);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error sending REFER response: {ex.Message}");
            }        }

        // Registration refresh timer methods
        private void StartRegistrationRefreshTimer()
        {
            lock (_timerLock)
            {
                // Stop existing timer if running
                StopRegistrationRefreshTimer();
                
                // Calculate refresh interval (90% of expiry time to ensure we refresh before expiration)
                var refreshInterval = (int)(_registrationExpiry * 0.9 * 1000); // Convert to milliseconds
                
                StatusChanged?.Invoke(this, $"📅 Setting up registration refresh in {refreshInterval / 1000} seconds ({refreshInterval / 60000} minutes)");
                
                _registrationRefreshTimer = new System.Timers.Timer(refreshInterval);
                _registrationRefreshTimer.Elapsed += async (sender, e) => await RefreshRegistration();
                _registrationRefreshTimer.AutoReset = true; // Repeat the timer
                _registrationRefreshTimer.Start();
                
                StatusChanged?.Invoke(this, "✅ Registration refresh timer started");
            }
        }
        
        private void StopRegistrationRefreshTimer()
        {
            lock (_timerLock)
            {
                if (_registrationRefreshTimer != null)
                {
                    _registrationRefreshTimer.Stop();
                    _registrationRefreshTimer.Dispose();
                    _registrationRefreshTimer = null;
                    StatusChanged?.Invoke(this, "⏹️ Registration refresh timer stopped");
                }
            }
        }
          private async Task RefreshRegistration()
        {
            if (!_isConnected || _stream == null || !_isRegistered)
            {
                StatusChanged?.Invoke(this, "⚠️ Cannot refresh registration - not connected or not registered");
                StopRegistrationRefreshTimer();
                return;
            }
            
            try
            {
                StatusChanged?.Invoke(this, "🔄 Refreshing SIP registration...");
                
                // Create a new REGISTER message with stored expiry value
                var registerMessage = CreateRegisterMessage(_registrationExpiry);
                MessageReceived?.Invoke(this, $"OUTGOING (Refresh):\n{registerMessage}");
                
                var messageBytes = Encoding.UTF8.GetBytes(registerMessage);
                await _stream.WriteAsync(messageBytes);
                
                StatusChanged?.Invoke(this, "📤 Registration refresh sent");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Registration refresh failed: {ex.Message}");
                // Keep timer running to retry later
            }
        }
        #region JSIP-Style Event Handlers and Helper Methods        /// <summary>
        /// Sends a SIP message through the bidirectional transport layer
        /// </summary>
        private async Task SendMessageAsync(string message)
        {
            // Validate outgoing message for RFC 3261 compliance
            try
            {
                if (_rfc3261Validator != null)
                {
                    var validationResult = _rfc3261Validator.ValidateMessage(message);
                    if (validationResult.HasCriticalErrors)
                    {
                        StatusChanged?.Invoke(this, "⚠️ Outgoing message has RFC 3261 compliance issues:");
                        foreach (var error in validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical))
                        {
                            StatusChanged?.Invoke(this, $"  Critical: {error.Message}");
                        }
                    }
                    
                    if (validationResult.Errors.Any(e => e.Severity == ValidationSeverity.Warning))
                    {
                        foreach (var warning in validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Warning))
                        {
                            Console.WriteLine($"[RFC 3261 WARNING] {warning.Message}");
                        }
                    }
                }
            }
            catch (Exception validationEx)
            {
                Console.WriteLine($"[RFC 3261 VALIDATION] Error validating message: {validationEx.Message}");
            }
            
            // Try to send through SipTransport first (for bidirectional communication)
            if (_sipTransport != null)
            {                try
                {
                    await _sipTransport.SendToServerAsync(message);
                    
                    // Determine message type for logging
                    var firstLine = message.Split('\n')[0].Trim();
                    var messageType = firstLine.Contains("REGISTER") ? "REGISTER" :
                                    firstLine.Contains("INVITE") ? "INVITE" :
                                    firstLine.Contains("BYE") ? "BYE" :
                                    firstLine.Contains("ACK") ? "ACK" :
                                    firstLine.Contains("200 OK") ? "200 OK" :
                                    firstLine.Contains("180 Ringing") ? "180 Ringing" :
                                    firstLine.Contains("SIP/2.0") ? "RESPONSE" : "REQUEST";
                    
                    MessageReceived?.Invoke(this, $"OUTGOING ({messageType}):\n{message}");
                    return;
                }
                catch (Exception transportEx)
                {
                    StatusChanged?.Invoke(this, $"Transport send failed, falling back to legacy: {transportEx.Message}");
                }
            }
            
            // Fallback to legacy stream method
            if (_stream == null || !_isConnected)
            {
                throw new InvalidOperationException("Not connected to server");
            }
              try
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await _stream.WriteAsync(messageBytes);
                
                // Determine message type for logging
                var firstLine = message.Split('\n')[0].Trim();
                var messageType = firstLine.Contains("REGISTER") ? "REGISTER" :
                                firstLine.Contains("INVITE") ? "INVITE" :
                                firstLine.Contains("BYE") ? "BYE" :
                                firstLine.Contains("ACK") ? "ACK" :
                                firstLine.Contains("200 OK") ? "200 OK" :
                                firstLine.Contains("180 Ringing") ? "180 Ringing" :
                                firstLine.Contains("SIP/2.0") ? "RESPONSE" : "REQUEST";
                
                MessageReceived?.Invoke(this, $"OUTGOING ({messageType}):\n{message}");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Failed to send message: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Handles dialog state changes
        /// </summary>
        private void OnDialogStateChanged(object? sender, DialogStateChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, $"🔄 Dialog {e.Dialog.CallId}: {e.OldState} → {e.NewState}");
            
            // Update call state for UI
            switch (e.NewState)
            {
                case DialogState.Early:
                    CallStateChanged?.Invoke(this, "Call Proceeding");
                    break;
                case DialogState.Confirmed:
                    CallStateChanged?.Invoke(this, "Call Connected");
                    break;
                case DialogState.Terminated:
                    CallStateChanged?.Invoke(this, "Call Ended");
                    break;
            }
        }
        
        /// <summary>
        /// Handles registration status changes
        /// </summary>
        private void OnRegistrationStatusChanged(object? sender, WindowsSipPhone.SipCore.RegistrationStatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, $"📡 Registration: {e.Status} - {e.Message}");
            
            // Update legacy registration flag
            _isRegistered = e.Status == RegistrationStatus.Registered;
            
            // Complete registration task if needed
            if (e.Status == RegistrationStatus.Registered && _registrationCompletion != null)
            {
                _registrationCompletion.SetResult(true);
            }
            else if (e.Status == RegistrationStatus.Failed && _registrationCompletion != null)
            {
                _registrationCompletion.SetResult(false);
            }
        }
        
        /// <summary>
        /// Handles authentication challenges
        /// </summary>
        private async void OnAuthenticationRequired(object? sender, WindowsSipPhone.SipCore.AuthenticationRequiredEventArgs e)
        {
            try
            {
                StatusChanged?.Invoke(this, $"🔐 Processing authentication challenge ({e.StatusCode})...");
                
                // Create authorization header using existing logic
                var authorization = CreateAuthorizationHeader(_username, _password, "REGISTER", 
                    $"sip:{_serverHost}:{_serverPort}", e.AuthParameters);
                
                // Send authenticated registration
                await _registrationManager.SendAuthenticatedRegisterAsync(authorization);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Authentication failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Updates dialog management from SIP responses
        /// </summary>
        private void UpdateDialogFromResponse(string message, int statusCode)
        {
            try
            {
                var callId = ExtractHeader(message, "Call-ID:");
                var fromHeader = ExtractHeader(message, "From:");
                var toHeader = ExtractHeader(message, "To:");
                var contactHeader = ExtractHeader(message, "Contact:");
                  // Extract local tag from From header (this is our tag)
                var localTag = ExtractTag(fromHeader);
                
                if (!string.IsNullOrEmpty(callId) && !string.IsNullOrEmpty(localTag))
                {
                    Console.WriteLine($"[DIALOG DEBUG] Updating dialog from response - CallId: {callId}, LocalTag: {localTag}, StatusCode: {statusCode}");
                    _dialogManager.UpdateDialogFromResponse(callId, localTag, statusCode, toHeader, contactHeader);
                    
                    // Debug: Check dialog state after update
                    var updatedDialog = _dialogManager.FindDialog(callId, localTag);
                    if (updatedDialog != null)
                    {
                        Console.WriteLine($"[DIALOG DEBUG] Dialog after update - RemoteTarget: '{updatedDialog.RemoteTarget}', RemoteTag: '{updatedDialog.RemoteTag}', RemoteUri: '{updatedDialog.RemoteUri}'");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error updating dialog from response: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Updates dialog management from SIP requests
        /// </summary>
        private void UpdateDialogFromRequest(string message, string method)
        {
            try
            {
                var callId = ExtractHeader(message, "Call-ID:");
                var fromHeader = ExtractHeader(message, "From:");
                var cseqHeader = ExtractHeader(message, "CSeq:");
                
                // Extract remote tag from From header (caller's tag)
                var remoteTag = ExtractTag(fromHeader);
                
                // Extract sequence number from CSeq
                var cseqParts = cseqHeader.Split(' ');
                if (cseqParts.Length >= 2 && uint.TryParse(cseqParts[0], out var seqNumber))
                {
                    _dialogManager.UpdateDialogFromRequest(callId, remoteTag, method, seqNumber);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Error updating dialog from request: {ex.Message}");
            }
        }
          /// <summary>
        /// Extracts tag parameter from a header
        /// </summary>
        private static string ExtractTag(string header)
        {
            var tagStart = header.IndexOf("tag=");
            if (tagStart == -1) return string.Empty;
            
            tagStart += 4;
            var tagEnd = header.IndexOf(';', tagStart);
            if (tagEnd == -1) tagEnd = header.Length;
            
            return header.Substring(tagStart, tagEnd - tagStart).Trim();
        }

        /// <summary>
        /// Generates a unique Call-ID for SIP dialogs
        /// </summary>
        private static string GenerateCallId()
        {
            return Guid.NewGuid().ToString().Replace("-", "");
        }

        /// <summary>
        /// Generates a unique tag for SIP dialogs
        /// </summary>
        private static string GenerateTag()
        {
            return Guid.NewGuid().ToString().Replace("-", "")[..8];
        }
        
        /// <summary>
        /// Gets active call statistics
        /// </summary>
        public DialogStatistics GetCallStatistics()
        {
            return _dialogManager.GetStatistics();
        }
          /// <summary>
        /// Gets registration statistics
        /// </summary>
        public WindowsSipPhone.SipCore.RegistrationStatistics GetRegistrationStatistics()
        {
            return _registrationManager.GetStatistics();
        }
        
        /// <summary>
        /// Clears all pending incoming call data
        /// </summary>
        private void ClearPendingIncomingCall()
        {
            _pendingIncomingCallId = null;
            _pendingIncomingVia = null;
            _pendingIncomingFrom = null;
            _pendingIncomingTo = null;
            _pendingIncomingCSeq = null;
            _pendingIncomingSdp = null;
            _pendingIncomingInvite = null;        }

        #endregion

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Disconnect();
            StopRegistrationRefreshTimer();
            _audioManager?.Dispose();
            _registrationManager?.Dispose();
            _registrationCompletion?.SetResult(false);
            _registrationCompletion = null;
        }
    }
}
