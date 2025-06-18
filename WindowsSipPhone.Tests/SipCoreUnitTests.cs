using Xunit;
using Xunit.Abstractions;

namespace WindowsSipPhone.Tests.Unit
{
    /// <summary>
    /// Unit tests for SIP core functionality
    /// Tests basic SIP operations without requiring external server
    /// </summary>
    public class SipCoreUnitTests
    {
        private readonly ITestOutputHelper _output;

        public SipCoreUnitTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SipDigestAuth_CreateAuthorizationHeader_ShouldGenerateValidHeader()
        {
            // Arrange
            var username = "103";
            var password = "274104";
            var challengeParams = new Dictionary<string, string>
            {
                ["realm"] = "asterisk",
                ["nonce"] = "test-nonce-123",
                ["qop"] = "auth"
            };
            var uri = "sip:192.168.1.180";
            var method = "REGISTER";

            // Act
            var authHeader = SipDigestAuth.CreateAuthorizationHeader(
                username, password, method, uri, challengeParams);

            // Assert
            Assert.NotNull(authHeader);
            Assert.Contains("username=\"103\"", authHeader);
            Assert.Contains("realm=\"asterisk\"", authHeader);
            Assert.Contains("nonce=\"test-nonce-123\"", authHeader);
            Assert.Contains("uri=\"sip:192.168.1.180\"", authHeader);
            Assert.Contains("response=", authHeader);
            
            _output.WriteLine($"Generated auth header: {authHeader}");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SdpManager_CreateSdpOffer_ShouldGenerateValidSdp()
        {
            // Arrange
            var localIp = "192.168.1.100";
            var rtpPort = 8000;

            // Act
            var sdpContent = SdpManager.CreateSdpOffer(localIp, rtpPort);

            // Assert
            Assert.NotNull(sdpContent);
            Assert.Contains("v=0", sdpContent); // Version
            Assert.Contains($"c=IN IP4 {localIp}", sdpContent); // Connection info
            Assert.Contains($"m=audio {rtpPort}", sdpContent); // Media line
            Assert.Contains("a=rtpmap:", sdpContent); // RTP mapping
            
            _output.WriteLine($"Generated SDP:\n{sdpContent}");
        }

        [Theory]
        [InlineData("192.168.1.100", 8000)]
        [InlineData("10.0.0.5", 9000)]
        [InlineData("172.16.1.10", 7000)]
        [Trait("Category", "Unit")]
        public void SdpManager_CreateSdpOffer_WithDifferentAddresses_ShouldWork(string localIp, int rtpPort)
        {
            // Act
            var sdpContent = SdpManager.CreateSdpOffer(localIp, rtpPort);

            // Assert
            Assert.NotNull(sdpContent);
            Assert.Contains($"c=IN IP4 {localIp}", sdpContent);
            Assert.Contains($"m=audio {rtpPort}", sdpContent);
            
            _output.WriteLine($"SDP for {localIp}:{rtpPort} generated successfully");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SipDigestAuth_CalculateResponse_WithSameCnonce_ShouldGenerateConsistentHash()
        {
            // Arrange
            var username = "testuser";
            var password = "testpass";
            var realm = "testrealm";
            var nonce = "testnonce";
            var method = "REGISTER";
            var uri = "sip:test.com";
            var cnonce = "testcnonce"; // Fixed cnonce for consistent results

            // Act
            var response1 = SipDigestAuth.CalculateResponse(username, password, realm, nonce, method, uri, "00000001", cnonce);
            var response2 = SipDigestAuth.CalculateResponse(username, password, realm, nonce, method, uri, "00000001", cnonce);

            // Assert
            Assert.Equal(response1, response2);
            Assert.Equal(32, response1.Length); // MD5 hash is 32 characters
            Assert.Matches("^[a-f0-9]+$", response1); // Should be lowercase hex
            
            _output.WriteLine($"MD5 response for auth challenge is '{response1}'");
        }

        [Fact]
        [Trait("Category", "Performance")]
        public void SdpManager_CreateSdpOffer_PerformanceTest()
        {
            // Arrange
            var localIp = "192.168.1.100";
            var rtpPort = 8000;
            var iterations = 1000;

            // Act
            var startTime = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                SdpManager.CreateSdpOffer(localIp, rtpPort + i);
            }
            var endTime = DateTime.UtcNow;
            
            var totalMs = (endTime - startTime).TotalMilliseconds;
            var avgMs = totalMs / iterations;

            // Assert
            Assert.True(avgMs < 1.0, $"SDP creation should take less than 1ms on average, actual: {avgMs:F3}ms");
            Assert.True(totalMs < 1000, $"Total time for {iterations} iterations should be less than 1000ms, actual: {totalMs:F1}ms");
            
            _output.WriteLine($"Performance: {iterations} SDP creations in {totalMs:F1}ms (avg: {avgMs:F3}ms per operation)");
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [Trait("Category", "Performance")]
        public void SipDigestAuth_Performance_ShouldBeWithinBounds(int iterations)
        {
            // Arrange
            var username = "testuser";
            var password = "testpass";
            var challengeParams = new Dictionary<string, string>
            {
                ["realm"] = "testrealm",
                ["nonce"] = "testnonce",
                ["qop"] = "auth"
            };
            var uri = "sip:test.com";
            var method = "REGISTER";

            // Act
            var startTime = DateTime.UtcNow;
            for (int i = 0; i < iterations; i++)
            {
                challengeParams["nonce"] = $"testnonce-{i}";
                SipDigestAuth.CreateAuthorizationHeader(username, password, method, uri, challengeParams);
            }
            var endTime = DateTime.UtcNow;
            
            var totalMs = (endTime - startTime).TotalMilliseconds;
            var avgMs = totalMs / iterations;

            // Assert
            Assert.True(avgMs < 2.0, $"Auth header creation should take less than 2ms on average, actual: {avgMs:F3}ms");
            _output.WriteLine($"Performance: {iterations} auth headers in {totalMs:F1}ms (avg: {avgMs:F3}ms per operation)");
        }
    }
}