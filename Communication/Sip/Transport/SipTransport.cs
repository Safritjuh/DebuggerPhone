using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsSipPhone
{
    /// <summary>
    /// SIP Transport Layer - handles both outbound connections and inbound listening
    /// RFC 3261 compliant bidirectional SIP communication over TCP
    /// </summary>
    public class SipTransport : IDisposable
    {
        private TcpListener? _sipListener;
        private TcpClient? _outboundClient;
        private NetworkStream? _outboundStream;
        private readonly int _localSipPort;
        private readonly string _localIp;
        private bool _isListening = false;
        private bool _isConnectedOutbound = false;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        
        // Connection management
        private readonly ConcurrentDictionary<string, NetworkStream> _activeConnections = new();
        
        // Events
        public event EventHandler<string>? MessageReceived;
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? TransportError;
        
        public bool IsListening => _isListening;
        public bool IsConnectedOutbound => _isConnectedOutbound;
        public int LocalSipPort => _localSipPort;
        public string LocalIp => _localIp;
        
        public SipTransport(string localIp, int localSipPort = 5060)
        {
            _localIp = localIp;
            _localSipPort = localSipPort;
        }
        
        /// <summary>
        /// Start listening for incoming SIP connections
        /// This is essential for receiving calls!
        /// </summary>
        public async Task<bool> StartListeningAsync()
        {
            try
            {
                if (_isListening)
                {
                    StatusChanged?.Invoke(this, "SIP transport already listening");
                    return true;
                }
                
                StatusChanged?.Invoke(this, $"Starting SIP listener on {_localIp}:{_localSipPort}...");
                
                // Try to bind to the specified port
                var localEndPoint = new IPEndPoint(IPAddress.Parse(_localIp), _localSipPort);
                _sipListener = new TcpListener(localEndPoint);
                
                // Allow port reuse for multiple registrations
                _sipListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                
                _sipListener.Start();
                _isListening = true;
                
                StatusChanged?.Invoke(this, $"✅ SIP listener started on {_localIp}:{_localSipPort}");
                
                // Start accepting incoming connections
                _ = Task.Run(async () => await AcceptIncomingConnectionsAsync(_cancellationTokenSource.Token));
                
                return true;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                // Port 5060 is busy, try dynamic port
                return await StartListeningOnDynamicPortAsync();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to start SIP listener: {ex.Message}";
                StatusChanged?.Invoke(this, errorMsg);
                TransportError?.Invoke(this, errorMsg);
                return false;
            }
        }
          /// <summary>
        /// Fallback: Start listening on a dynamic port if 5060 is busy
        /// </summary>
        private Task<bool> StartListeningOnDynamicPortAsync()
        {
            try
            {
                StatusChanged?.Invoke(this, "Port 5060 busy, trying dynamic port...");
                
                // Let the OS choose an available port
                var localEndPoint = new IPEndPoint(IPAddress.Parse(_localIp), 0);
                _sipListener = new TcpListener(localEndPoint);
                _sipListener.Start();
                
                var actualPort = ((IPEndPoint)_sipListener.LocalEndpoint).Port;
                _isListening = true;
                  StatusChanged?.Invoke(this, $"✅ SIP listener started on {_localIp}:{actualPort} (dynamic port)");
                
                // Start accepting incoming connections
                _ = Task.Run(async () => await AcceptIncomingConnectionsAsync(_cancellationTokenSource.Token));
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {                var errorMsg = $"Failed to start SIP listener on dynamic port: {ex.Message}";
                StatusChanged?.Invoke(this, errorMsg);
                TransportError?.Invoke(this, errorMsg);
                return Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// Accept incoming SIP connections (for receiving calls)
        /// </summary>
        private async Task AcceptIncomingConnectionsAsync(CancellationToken cancellationToken)
        {
            if (_sipListener == null) return;
            
            StatusChanged?.Invoke(this, "🎧 Listening for incoming SIP connections...");
            
            while (!cancellationToken.IsCancellationRequested && _isListening)
            {
                try
                {
                    var tcpClient = await _sipListener.AcceptTcpClientAsync();
                    var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    
                    StatusChanged?.Invoke(this, $"📞 Incoming SIP connection from: {remoteEndPoint}");
                    
                    // Handle this connection in a separate task
                    _ = Task.Run(async () => await HandleIncomingConnectionAsync(tcpClient, cancellationToken));
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        StatusChanged?.Invoke(this, $"Error accepting connection: {ex.Message}");
                    }
                }
            }
            
            StatusChanged?.Invoke(this, "Stopped accepting incoming SIP connections");
        }
        
        /// <summary>
        /// Handle an individual incoming SIP connection
        /// </summary>
        private async Task HandleIncomingConnectionAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
            NetworkStream? stream = null;
            
            try
            {
                stream = tcpClient.GetStream();
                var connectionId = $"incoming_{remoteEndPoint}_{DateTime.Now.Ticks}";
                _activeConnections.TryAdd(connectionId, stream);
                
                StatusChanged?.Invoke(this, $"📨 Handling incoming connection: {connectionId}");
                
                var buffer = new byte[8192];
                var messageBuilder = new StringBuilder();
                
                while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        // Connection closed by remote
                        StatusChanged?.Invoke(this, $"Connection closed by remote: {remoteEndPoint}");
                        break;
                    }
                    
                    var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(chunk);
                    
                    // Check for complete SIP messages
                    var fullMessage = messageBuilder.ToString();
                    if (IsCompleteSipMessage(fullMessage))
                    {
                        StatusChanged?.Invoke(this, $"📨 Received SIP message from {remoteEndPoint}");
                        MessageReceived?.Invoke(this, $"INCOMING (from {remoteEndPoint}):\n{fullMessage}");
                        messageBuilder.Clear();
                    }
                }
                
                _activeConnections.TryRemove(connectionId, out _);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error handling incoming connection from {remoteEndPoint}: {ex.Message}");
            }
            finally
            {
                stream?.Close();
                tcpClient?.Close();
                StatusChanged?.Invoke(this, $"Closed incoming connection: {remoteEndPoint}");
            }
        }
        
        /// <summary>
        /// Connect to remote SIP server (outbound)
        /// </summary>
        public async Task<bool> ConnectToServerAsync(string serverHost, int serverPort)
        {
            try
            {
                if (_isConnectedOutbound)
                {
                    StatusChanged?.Invoke(this, "Already connected to SIP server");
                    return true;
                }
                
                StatusChanged?.Invoke(this, $"Connecting to SIP server {serverHost}:{serverPort}...");
                
                _outboundClient = new TcpClient();
                await _outboundClient.ConnectAsync(serverHost, serverPort);
                _outboundStream = _outboundClient.GetStream();
                _isConnectedOutbound = true;
                
                StatusChanged?.Invoke(this, $"✅ Connected to SIP server {serverHost}:{serverPort}");
                
                // Start listening for messages from server
                _ = Task.Run(async () => await ListenToServerAsync(_cancellationTokenSource.Token));
                
                return true;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to connect to SIP server: {ex.Message}";
                StatusChanged?.Invoke(this, errorMsg);
                TransportError?.Invoke(this, errorMsg);
                return false;
            }
        }
        
        /// <summary>
        /// Listen for messages from the SIP server
        /// </summary>
        private async Task ListenToServerAsync(CancellationToken cancellationToken)
        {
            if (_outboundStream == null) return;
            
            var buffer = new byte[8192];
            var messageBuilder = new StringBuilder();
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isConnectedOutbound)
                {
                    var bytesRead = await _outboundStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        StatusChanged?.Invoke(this, "Server closed connection");
                        _isConnectedOutbound = false;
                        break;
                    }
                    
                    var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(chunk);
                    
                    var fullMessage = messageBuilder.ToString();
                    if (IsCompleteSipMessage(fullMessage))
                    {
                        MessageReceived?.Invoke(this, $"INCOMING (from server):\n{fullMessage}");
                        messageBuilder.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    StatusChanged?.Invoke(this, $"Error listening to server: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Send SIP message to server
        /// </summary>
        public async Task<bool> SendToServerAsync(string message)
        {
            if (_outboundStream == null || !_isConnectedOutbound)
            {
                StatusChanged?.Invoke(this, "Not connected to server - cannot send message");
                return false;
            }
            
            try
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await _outboundStream.WriteAsync(messageBytes, 0, messageBytes.Length);
                StatusChanged?.Invoke(this, "📤 Message sent to server");
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Failed to send message to server: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Send SIP response back to incoming connection (for INVITE responses, 405 responses, etc.)
        /// This is critical for proper SIP response routing!
        /// </summary>
        public async Task<bool> SendToIncomingConnectionAsync(string message, string? preferredConnectionId = null)
        {
            try
            {
                NetworkStream? targetStream = null;
                
                // If a specific connection ID is provided, use it
                if (!string.IsNullOrEmpty(preferredConnectionId) && _activeConnections.TryGetValue(preferredConnectionId, out targetStream))
                {
                    StatusChanged?.Invoke(this, $"📤 Sending response via connection: {preferredConnectionId}");
                }
                else
                {
                    // Use the most recent active connection (common for single-connection scenarios)
                    var connections = _activeConnections.ToArray();
                    if (connections.Length > 0)
                    {
                        var lastConnection = connections[connections.Length - 1];
                        targetStream = lastConnection.Value;
                        StatusChanged?.Invoke(this, $"📤 Sending response via latest connection: {lastConnection.Key}");
                    }
                }
                
                if (targetStream == null)
                {
                    StatusChanged?.Invoke(this, "❌ No active incoming connections to send response to");
                    return false;
                }
                
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await targetStream.WriteAsync(messageBytes, 0, messageBytes.Length);
                StatusChanged?.Invoke(this, "✅ Response sent to incoming connection");
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Failed to send response to incoming connection: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Check if we have a complete SIP message
        /// </summary>
        private bool IsCompleteSipMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            
            // SIP message ends with \r\n\r\n
            if (!message.Contains("\r\n\r\n")) return false;
            
            // Check Content-Length if present
            var contentLengthMatch = System.Text.RegularExpressions.Regex.Match(message, @"Content-Length:\s*(\d+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (contentLengthMatch.Success)
            {
                var contentLength = int.Parse(contentLengthMatch.Groups[1].Value);
                var headerEnd = message.IndexOf("\r\n\r\n") + 4;
                var bodyLength = message.Length - headerEnd;
                
                return bodyLength >= contentLength;
            }
            
            return true; // No content-length header, assume complete
        }
        
        /// <summary>
        /// Get transport status for debugging
        /// </summary>
        public string GetTransportStatus()
        {
            return $"""
                SIP Transport Status:
                ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                📡 Local Listener: {(_isListening ? "✅ Active" : "❌ Inactive")} on {_localIp}:{_localSipPort}
                🔗 Server Connection: {(_isConnectedOutbound ? "✅ Connected" : "❌ Disconnected")}
                💬 Active Connections: {_activeConnections.Count}
                
                Configuration:
                • Local IP: {_localIp}
                • Local SIP Port: {_localSipPort}
                • Can receive calls: {(_isListening ? "YES" : "NO")}
                • Can make calls: {(_isConnectedOutbound ? "YES" : "NO")}
                ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                """;
        }
        
        public void Dispose()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                
                // Close all active connections
                foreach (var connection in _activeConnections.Values)
                {
                    connection?.Close();
                }
                _activeConnections.Clear();
                
                // Close outbound connection
                _outboundStream?.Close();
                _outboundClient?.Close();
                _isConnectedOutbound = false;
                
                // Stop listener
                _sipListener?.Stop();
                _isListening = false;
                
                _cancellationTokenSource.Dispose();
                
                StatusChanged?.Invoke(this, "SIP transport disposed");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error disposing SIP transport: {ex.Message}");
            }
        }
    }
}
