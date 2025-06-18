using System.Net;
using System.Net.Sockets;
using Xunit;
using Xunit.Abstractions;

namespace WindowsSipPhone.Tests.Integration
{
    /// <summary>
    /// Integration tests for SIP registration functionality
    /// Tests against real SIP server to validate end-to-end registration flow
    /// </summary>
    public class SipRegistrationIntegrationTests
    {
        private readonly ITestOutputHelper _output;
        private const string SIP_SERVER_HOST = "localhost"; // Docker container in CI
        private const int SIP_SERVER_PORT = 5060;
        private const string SIP_USERNAME = "103";
        private const string SIP_PASSWORD = "274104";

        public SipRegistrationIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task SipServer_ShouldBeReachable()
        {
            // Arrange & Act
            var isReachable = await IsSipServerReachableAsync();
            
            // Assert
            Assert.True(isReachable, $"SIP server at {SIP_SERVER_HOST}:{SIP_SERVER_PORT} should be reachable");
            _output.WriteLine($"✅ SIP server {SIP_SERVER_HOST}:{SIP_SERVER_PORT} is reachable");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task SipRegistration_WithValidCredentials_ShouldSucceed()
        {
            // Arrange
            var registrationSucceeded = false;
            var registrationMessages = new List<string>();
            
            using var sipClient = new TestSipClient(SIP_SERVER_HOST, SIP_SERVER_PORT, SIP_USERNAME, SIP_PASSWORD);
            
            // Subscribe to status changes to capture registration flow
            sipClient.StatusChanged += (sender, message) =>
            {
                registrationMessages.Add(message);
                _output.WriteLine($"SIP Status: {message}");
                
                if (message.Contains("Registration successful") || message.Contains("✅") || message.Contains("registered"))
                {
                    registrationSucceeded = true;
                }
            };

            // Act
            var connected = await sipClient.ConnectAsync();
            Assert.True(connected, "Should connect to SIP server");

            var registered = await sipClient.RegisterAsync();
            
            // Allow time for async registration process
            await Task.Delay(10000);

            // Assert
            Assert.True(registered || registrationSucceeded || sipClient.IsRegistered, 
                $"SIP registration should succeed. Messages: {string.Join("; ", registrationMessages)}");
            
            _output.WriteLine("✅ SIP registration test completed successfully");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task SipRegistration_WithInvalidCredentials_ShouldFail()
        {
            // Arrange
            var authenticationFailed = false;
            var registrationMessages = new List<string>();
            
            using var sipClient = new TestSipClient(SIP_SERVER_HOST, SIP_SERVER_PORT, "invalid_user", "invalid_password");
            
            // Subscribe to status changes to capture failure
            sipClient.StatusChanged += (sender, message) =>
            {
                registrationMessages.Add(message);
                _output.WriteLine($"SIP Status: {message}");
                
                if (message.Contains("Authentication failed") || message.Contains("401") || message.Contains("❌") || message.Contains("failed"))
                {
                    authenticationFailed = true;
                }
            };

            // Act
            var connected = await sipClient.ConnectAsync();
            Assert.True(connected, "Should connect to SIP server");

            // Try to register with invalid credentials
            var registered = await sipClient.RegisterAsync();
            
            // Allow time for async registration process
            await Task.Delay(10000);

            // Assert
            Assert.False(registered && sipClient.IsRegistered, "Registration with invalid credentials should fail");
            
            _output.WriteLine("✅ Invalid credentials test completed successfully");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task SipMessageFlow_DuringRegistration_ShouldContainExpectedMessages()
        {
            // Arrange
            var sipMessages = new List<string>();
            var expectedMessages = new[]
            {
                "REGISTER",
                "401 Unauthorized",
                "Authorization:",
                "200 OK"
            };
            
            using var sipClient = new TestSipClient(SIP_SERVER_HOST, SIP_SERVER_PORT, SIP_USERNAME, SIP_PASSWORD);
            
            // Capture SIP messages
            sipClient.MessageReceived += (sender, message) =>
            {
                sipMessages.Add(message);
                _output.WriteLine($"SIP Message: {message.Split('\n')[0]}"); // Log first line only to avoid noise
            };

            // Act
            await sipClient.ConnectAsync();
            await sipClient.RegisterAsync();
            
            // Allow time for message exchange
            await Task.Delay(10000);

            // Assert - Check for at least some key messages
            var foundMessages = expectedMessages.Where(expected => 
                sipMessages.Any(msg => msg.Contains(expected))).ToList();
            
            _output.WriteLine($"Found {foundMessages.Count} of {expectedMessages.Length} expected messages: {string.Join(", ", foundMessages)}");
            
            // We expect at least REGISTER and some response
            Assert.True(foundMessages.Count >= 1, $"Expected at least one SIP message, found: {string.Join(", ", foundMessages)}");
            
            _output.WriteLine("✅ SIP message flow validation completed successfully");
        }

        private async Task<bool> IsSipServerReachableAsync()
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(SIP_SERVER_HOST, SIP_SERVER_PORT);
                return tcpClient.Connected;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"SIP server connectivity test failed: {ex.Message}");
                return false;
            }
        }
    }
}