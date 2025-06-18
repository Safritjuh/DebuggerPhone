using System.Net.Sockets;
using System.Text;

namespace WindowsSipPhone.Tests
{
    /// <summary>
    /// Simplified SIP client for testing purposes
    /// Cross-platform compatible - no Windows dependencies
    /// </summary>
    public class TestSipClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private bool _isConnected;
        private readonly string _serverHost;
        private readonly int _serverPort;
        private readonly string _username;
        private readonly string _password;
        private readonly string _userAgent;
        private int _sequenceNumber = 1;
        private string _callId = string.Empty;
        private string _fromTag = string.Empty;
        private string _localIp = "127.0.0.1";
        private bool _isRegistered = false;
        private readonly object _lock = new object();

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<string>? StatusChanged;
        
        public bool IsRegistered => _isRegistered;

        public TestSipClient(string serverHost, int serverPort, string username, string password, string userAgent = "Test-SIP-Client/1.0")
        {
            _serverHost = serverHost;
            _serverPort = serverPort;
            _username = username;
            _password = password;
            _userAgent = userAgent;
            _callId = Guid.NewGuid().ToString().Replace("-", "");
            _fromTag = Guid.NewGuid().ToString().Replace("-", "")[..8];
            _localIp = GetLocalIPAddress();
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                StatusChanged?.Invoke(this, $"Connecting to SIP server {_serverHost}:{_serverPort}...");
                
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_serverHost, _serverPort);
                _stream = _tcpClient.GetStream();
                _isConnected = true;

                StatusChanged?.Invoke(this, $"✅ Connected to SIP server {_serverHost}:{_serverPort}");
                
                // Start listening for incoming messages
                _ = Task.Run(ListenForMessagesAsync);
                
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RegisterAsync(int expires = 3600)
        {
            if (!_isConnected || _stream == null)
            {
                StatusChanged?.Invoke(this, "❌ Not connected to server");
                return false;
            }

            try
            {
                StatusChanged?.Invoke(this, "🔄 Starting SIP registration...");
                
                // Create initial REGISTER message
                var registerMessage = CreateRegisterMessage(expires);
                
                // Send REGISTER message
                await SendMessageAsync(registerMessage);
                StatusChanged?.Invoke(this, "📤 REGISTER message sent");
                
                // Wait for authentication challenge and response
                await Task.Delay(2000);
                
                return _isRegistered;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"❌ Registration failed: {ex.Message}");
                return false;
            }
        }

        private async Task SendMessageAsync(string message)
        {
            if (_stream == null) return;
            
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _stream.WriteAsync(messageBytes, 0, messageBytes.Length);
        }

        private async Task ListenForMessagesAsync()
        {
            if (_stream == null) return;
            
            var buffer = new byte[4096];
            
            try
            {
                while (_isConnected && _stream.CanRead)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        MessageReceived?.Invoke(this, message);
                        
                        // Process the message
                        await ProcessIncomingMessage(message);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Listening stopped: {ex.Message}");
            }
        }

        private async Task ProcessIncomingMessage(string message)
        {
            try
            {
                if (message.Contains("401 Unauthorized"))
                {
                    StatusChanged?.Invoke(this, "🔐 Authentication challenge received");
                    await HandleAuthenticationChallenge(message);
                }
                else if (message.Contains("200 OK") && message.Contains("REGISTER"))
                {
                    lock (_lock)
                    {
                        _isRegistered = true;
                    }
                    StatusChanged?.Invoke(this, "✅ Registration successful");
                }
                else if (message.Contains("403 Forbidden") || message.Contains("Authentication failed"))
                {
                    StatusChanged?.Invoke(this, "❌ Authentication failed");
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error processing message: {ex.Message}");
            }
        }

        private async Task HandleAuthenticationChallenge(string challengeMessage)
        {
            try
            {
                // Extract nonce and realm from challenge
                var nonce = ExtractHeaderValue(challengeMessage, "nonce");
                var realm = ExtractHeaderValue(challengeMessage, "realm");

                if (string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(realm))
                {
                    StatusChanged?.Invoke(this, "❌ Failed to extract authentication parameters");
                    return;
                }

                // Create authenticated REGISTER message
                var authenticatedRegister = CreateAuthenticatedRegisterMessage(realm, nonce);
                
                // Send authenticated REGISTER
                await SendMessageAsync(authenticatedRegister);
                StatusChanged?.Invoke(this, "📤 Authenticated REGISTER sent");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Authentication handling failed: {ex.Message}");
            }
        }

        private string CreateRegisterMessage(int expires)
        {
            var requestUri = $"sip:{_serverHost}";
            var fromUri = $"sip:{_username}@{_serverHost}";
            var toUri = fromUri;
            var contactUri = $"sip:{_username}@{_localIp}:5060";

            return $"REGISTER {requestUri} SIP/2.0\r\n" +
                   $"Via: SIP/2.0/TCP {_localIp}:5060;branch=z9hG4bK{_fromTag}\r\n" +
                   $"From: <{fromUri}>;tag={_fromTag}\r\n" +
                   $"To: <{toUri}>\r\n" +
                   $"Call-ID: {_callId}\r\n" +
                   $"CSeq: {_sequenceNumber++} REGISTER\r\n" +
                   $"Contact: <{contactUri}>\r\n" +
                   $"Expires: {expires}\r\n" +
                   $"User-Agent: {_userAgent}\r\n" +
                   $"Content-Length: 0\r\n\r\n";
        }

        private string CreateAuthenticatedRegisterMessage(string realm, string nonce)
        {
            var requestUri = $"sip:{_serverHost}";
            var fromUri = $"sip:{_username}@{_serverHost}";
            var toUri = fromUri;
            var contactUri = $"sip:{_username}@{_localIp}:5060";

            // Create authorization header using SipDigestAuth
            var challengeParams = new Dictionary<string, string>
            {
                ["realm"] = realm,
                ["nonce"] = nonce,
                ["qop"] = "auth"
            };
            
            var authHeader = SipDigestAuth.CreateAuthorizationHeader(
                _username, _password, "REGISTER", requestUri, challengeParams);

            return $"REGISTER {requestUri} SIP/2.0\r\n" +
                   $"Via: SIP/2.0/TCP {_localIp}:5060;branch=z9hG4bK{_fromTag}\r\n" +
                   $"From: <{fromUri}>;tag={_fromTag}\r\n" +
                   $"To: <{toUri}>\r\n" +
                   $"Call-ID: {_callId}\r\n" +
                   $"CSeq: {_sequenceNumber++} REGISTER\r\n" +
                   $"Contact: <{contactUri}>\r\n" +
                   $"Authorization: {authHeader}\r\n" +
                   $"Expires: 3600\r\n" +
                   $"User-Agent: {_userAgent}\r\n" +
                   $"Content-Length: 0\r\n\r\n";
        }

        private string ExtractHeaderValue(string message, string headerName)
        {
            var lines = message.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains($"{headerName}=\""))
                {
                    var startIndex = line.IndexOf($"{headerName}=\"") + headerName.Length + 2;
                    var endIndex = line.IndexOf("\"", startIndex);
                    if (endIndex > startIndex)
                    {
                        return line.Substring(startIndex, endIndex - startIndex);
                    }
                }
            }
            return string.Empty;
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var hostName = System.Net.Dns.GetHostName();
                var addresses = System.Net.Dns.GetHostAddresses(hostName);
                var ipv4Address = addresses.FirstOrDefault(addr => addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                return ipv4Address?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        public void Dispose()
        {
            _isConnected = false;
            _stream?.Close();
            _tcpClient?.Close();
        }
    }
}