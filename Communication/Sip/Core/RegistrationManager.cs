using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace WindowsSipPhone.SipCore
{
    /// <summary>
    /// Manages SIP registration with automatic keep-alive (re-registration)
    /// Following RFC 3261 registration procedures
    /// </summary>
    public class RegistrationManager : IDisposable
    {
        private readonly SipMessageFactory _messageFactory;
        private readonly Func<string, Task> _sendMessage;
        private System.Timers.Timer? _registrationTimer;
        private readonly object _timerLock = new object();
        
        public string Username { get; private set; } = string.Empty;
        public string ServerHost { get; private set; } = string.Empty;
        public int ServerPort { get; private set; } = 5060;
        public bool IsRegistered { get; private set; } = false;
        public int ExpiresSeconds { get; private set; } = 300;
        public DateTime? LastRegistrationTime { get; private set; }
        public DateTime? NextRegistrationTime { get; private set; }
        
        private uint _sequenceNumber = 1;
        private TaskCompletionSource<bool>? _registrationCompletion;
        
        /// <summary>
        /// Event fired when registration status changes
        /// </summary>
        public event EventHandler<RegistrationStatusChangedEventArgs>? RegistrationStatusChanged;
        
        /// <summary>
        /// Event fired when registration needs authentication
        /// </summary>
        public event EventHandler<AuthenticationRequiredEventArgs>? AuthenticationRequired;
        
        public RegistrationManager(SipMessageFactory messageFactory, Func<string, Task> sendMessage)
        {
            _messageFactory = messageFactory;
            _sendMessage = sendMessage;
        }
        
        /// <summary>
        /// Starts registration process
        /// </summary>
        public async Task<bool> RegisterAsync(string username, string password, string serverHost, int serverPort, int expiresSeconds = 300)
        {
            Username = username;
            ServerHost = serverHost;
            ServerPort = serverPort;
            ExpiresSeconds = expiresSeconds;
            
            _registrationCompletion = new TaskCompletionSource<bool>();
            
            try
            {
                OnRegistrationStatusChanged(RegistrationStatus.Registering, "Starting registration process...");
                
                // Send initial REGISTER request
                var registerMessage = _messageFactory.CreateRegisterRequest(
                    username, serverHost, serverPort, _sequenceNumber++, expires: expiresSeconds);
                
                await _sendMessage(registerMessage);
                
                // Wait for registration response with timeout
                var result = await _registrationCompletion.Task.WaitAsync(TimeSpan.FromSeconds(30));
                
                if (result)
                {
                    StartRegistrationTimer();
                }
                
                return result;
            }
            catch (Exception ex)
            {
                OnRegistrationStatusChanged(RegistrationStatus.Failed, $"Registration failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Processes registration response
        /// </summary>
        public void ProcessRegistrationResponse(int statusCode, string reasonPhrase, string response, Dictionary<string, string>? authParams = null)
        {
            switch (statusCode)
            {
                case 200:
                    HandleRegistrationSuccess(response);
                    break;
                    
                case 401:
                case 407:
                    HandleAuthenticationChallenge(statusCode, authParams);
                    break;
                    
                case 403:
                    HandleRegistrationFailure("Authentication failed - check credentials");
                    break;
                    
                default:
                    HandleRegistrationFailure($"Registration failed: {statusCode} {reasonPhrase}");
                    break;
            }
        }
        
        /// <summary>
        /// Handles successful registration
        /// </summary>
        private void HandleRegistrationSuccess(string response)
        {
            IsRegistered = true;
            LastRegistrationTime = DateTime.Now;
            
            // Parse expires from response if available
            var expiresMatch = System.Text.RegularExpressions.Regex.Match(response, @"Expires:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (expiresMatch.Success)
            {
                ExpiresSeconds = int.Parse(expiresMatch.Groups[1].Value);
            }
            
            NextRegistrationTime = DateTime.Now.AddSeconds(ExpiresSeconds * 0.9); // Re-register at 90% of expires time
            
            OnRegistrationStatusChanged(RegistrationStatus.Registered, $"Registration successful, expires in {ExpiresSeconds} seconds");
            _registrationCompletion?.SetResult(true);
        }
        
        /// <summary>
        /// Handles authentication challenge
        /// </summary>
        private void HandleAuthenticationChallenge(int statusCode, Dictionary<string, string>? authParams)
        {
            OnRegistrationStatusChanged(RegistrationStatus.Authenticating, $"Authentication required ({statusCode})");
            
            if (authParams != null)
            {
                AuthenticationRequired?.Invoke(this, new AuthenticationRequiredEventArgs(statusCode, authParams));
            }
            else
            {
                HandleRegistrationFailure("Authentication challenge received but no parameters available");
            }
        }
        
        /// <summary>
        /// Handles registration failure
        /// </summary>
        private void HandleRegistrationFailure(string reason)
        {
            IsRegistered = false;
            OnRegistrationStatusChanged(RegistrationStatus.Failed, reason);
            _registrationCompletion?.SetResult(false);
            StopRegistrationTimer();
        }
        
        /// <summary>
        /// Sends authenticated registration request
        /// </summary>
        public async Task SendAuthenticatedRegisterAsync(string authorization)
        {
            try
            {
                var registerMessage = _messageFactory.CreateRegisterRequest(
                    Username, ServerHost, ServerPort, _sequenceNumber++, authorization, ExpiresSeconds);
                
                await _sendMessage(registerMessage);
            }
            catch (Exception ex)
            {
                HandleRegistrationFailure($"Failed to send authenticated register: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Unregisters from the server
        /// </summary>
        public async Task<bool> UnregisterAsync()
        {
            if (!IsRegistered)
                return true;
                
            try
            {
                OnRegistrationStatusChanged(RegistrationStatus.Unregistering, "Starting unregistration...");
                
                // Send REGISTER with Expires: 0
                var registerMessage = _messageFactory.CreateRegisterRequest(
                    Username, ServerHost, ServerPort, _sequenceNumber++, expires: 0);
                
                await _sendMessage(registerMessage);
                
                IsRegistered = false;
                StopRegistrationTimer();
                OnRegistrationStatusChanged(RegistrationStatus.Unregistered, "Unregistration completed");
                
                return true;
            }
            catch (Exception ex)
            {
                OnRegistrationStatusChanged(RegistrationStatus.Failed, $"Unregistration failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Starts the registration refresh timer
        /// </summary>
        private void StartRegistrationTimer()
        {
            lock (_timerLock)
            {
                StopRegistrationTimer();
                
                // Calculate refresh interval (90% of expires time)
                var refreshInterval = TimeSpan.FromSeconds(ExpiresSeconds * 0.9);
                
                _registrationTimer = new System.Timers.Timer(refreshInterval.TotalMilliseconds);
                _registrationTimer.Elapsed += RegistrationTimer_Elapsed;
                _registrationTimer.AutoReset = true;
                _registrationTimer.Start();
                
                OnRegistrationStatusChanged(RegistrationStatus.Registered, 
                    $"Registration refresh scheduled in {refreshInterval.TotalMinutes:F1} minutes");
            }
        }
        
        /// <summary>
        /// Stops the registration refresh timer
        /// </summary>
        private void StopRegistrationTimer()
        {
            lock (_timerLock)
            {
                if (_registrationTimer != null)
                {
                    _registrationTimer.Stop();
                    _registrationTimer.Dispose();
                    _registrationTimer = null;
                }
            }
        }
        
        /// <summary>
        /// Handles registration timer elapsed event
        /// </summary>
        private async void RegistrationTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                OnRegistrationStatusChanged(RegistrationStatus.Refreshing, "Refreshing registration...");
                
                // Send refresh registration
                var registerMessage = _messageFactory.CreateRegisterRequest(
                    Username, ServerHost, ServerPort, _sequenceNumber++, expires: ExpiresSeconds);
                
                await _sendMessage(registerMessage);
            }
            catch (Exception ex)
            {
                OnRegistrationStatusChanged(RegistrationStatus.Failed, $"Registration refresh failed: {ex.Message}");
                IsRegistered = false;
                StopRegistrationTimer();
            }
        }
        
        /// <summary>
        /// Gets registration statistics
        /// </summary>
        public RegistrationStatistics GetStatistics()
        {
            return new RegistrationStatistics
            {
                IsRegistered = IsRegistered,
                Username = Username,
                ServerHost = ServerHost,
                ServerPort = ServerPort,
                ExpiresSeconds = ExpiresSeconds,
                LastRegistrationTime = LastRegistrationTime,
                NextRegistrationTime = NextRegistrationTime,
                SequenceNumber = _sequenceNumber
            };
        }
        
        private void OnRegistrationStatusChanged(RegistrationStatus status, string message)
        {
            RegistrationStatusChanged?.Invoke(this, new RegistrationStatusChangedEventArgs(status, message));
        }
        
        public void Dispose()
        {
            StopRegistrationTimer();
        }
    }
    
    /// <summary>
    /// Registration status enumeration
    /// </summary>
    public enum RegistrationStatus
    {
        Unregistered,
        Registering,
        Authenticating,
        Registered,
        Refreshing,
        Unregistering,
        Failed
    }
    
    /// <summary>
    /// Event arguments for registration status changes
    /// </summary>
    public class RegistrationStatusChangedEventArgs : EventArgs
    {
        public RegistrationStatus Status { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }
        
        public RegistrationStatusChangedEventArgs(RegistrationStatus status, string message)
        {
            Status = status;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }
    
    /// <summary>
    /// Event arguments for authentication required
    /// </summary>
    public class AuthenticationRequiredEventArgs : EventArgs
    {
        public int StatusCode { get; }
        public Dictionary<string, string> AuthParameters { get; }
        
        public AuthenticationRequiredEventArgs(int statusCode, Dictionary<string, string> authParameters)
        {
            StatusCode = statusCode;
            AuthParameters = authParameters;
        }
    }
    
    /// <summary>
    /// Registration statistics for monitoring
    /// </summary>
    public class RegistrationStatistics
    {
        public bool IsRegistered { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ServerHost { get; set; } = string.Empty;
        public int ServerPort { get; set; }
        public int ExpiresSeconds { get; set; }
        public DateTime? LastRegistrationTime { get; set; }
        public DateTime? NextRegistrationTime { get; set; }
        public uint SequenceNumber { get; set; }
    }
}
