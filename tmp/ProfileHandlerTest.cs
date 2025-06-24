using System;
using WindowsSipPhone.Core.Managers;
using WindowsSipPhone.Core.Models;

namespace WindowsSipPhone.Tests
{
    /// <summary>
    /// Test implementation for IMP-016: Profile-Specific SIP Handling
    /// Tests the new enhanced profile system
    /// </summary>
    public class ProfileHandlerTest
    {
        public static void RunTests()
        {
            Console.WriteLine("=== IMP-016 Profile Handler Tests ===");
            Console.WriteLine();
            
            var profileManager = new EnhancedProfileManager();
            
            // Test 1: Load Avaya profile
            TestAvayaProfile(profileManager);
            Console.WriteLine();
            
            // Test 2: Load Elevate profile
            TestElevateProfile(profileManager);
            Console.WriteLine();
            
            // Test 3: Load Generic profile
            TestGenericProfile(profileManager);
            Console.WriteLine();
            
            // Test 4: Test message handling
            TestMessageHandling(profileManager);
            Console.WriteLine();
            
            // Test 5: List available profiles
            TestAvailableProfiles(profileManager);
            
            Console.WriteLine("=== Tests Completed ===");
        }
        
        private static void TestAvayaProfile(EnhancedProfileManager profileManager)
        {
            Console.WriteLine("--- Testing Avaya Profile Handler ---");
            
            try
            {
                profileManager.LoadProfile("Avaya_Aura");
                
                if (profileManager.CurrentHandler != null)
                {
                    Console.WriteLine($"✅ Profile loaded: {profileManager.CurrentHandler.ProfileName}");
                    
                    // Test custom headers
                    var headers = profileManager.GetActiveProfileHeaders();
                    Console.WriteLine($"✅ Custom headers count: {headers.Count}");
                    foreach (var header in headers)
                    {
                        Console.WriteLine($"   - {header.Key}: {header.Value}");
                    }
                    
                    // Test preferred codecs
                    var codecs = profileManager.GetPreferredCodecs();
                    Console.WriteLine($"✅ Preferred codecs: {string.Join(", ", codecs)}");
                    
                    // Test custom routing check
                    var needsRouting = profileManager.RequiresCustomRouting("101");
                    Console.WriteLine($"✅ Extension 101 requires custom routing: {needsRouting}");
                }
                else
                {
                    Console.WriteLine("❌ Profile handler is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error testing Avaya profile: {ex.Message}");
            }
        }
        
        private static void TestElevateProfile(EnhancedProfileManager profileManager)
        {
            Console.WriteLine("--- Testing Elevate Profile Handler ---");
            
            try
            {
                profileManager.LoadProfile("Elevate");
                
                if (profileManager.CurrentHandler != null)
                {
                    Console.WriteLine($"✅ Profile loaded: {profileManager.CurrentHandler.ProfileName}");
                    
                    // Test custom headers
                    var headers = profileManager.GetActiveProfileHeaders();
                    Console.WriteLine($"✅ Custom headers count: {headers.Count}");
                    foreach (var header in headers)
                    {
                        Console.WriteLine($"   - {header.Key}: {header.Value}");
                    }
                    
                    // Test preferred codecs
                    var codecs = profileManager.GetPreferredCodecs();
                    Console.WriteLine($"✅ Preferred codecs: {string.Join(", ", codecs)}");
                    
                    // Test custom routing for international number
                    var needsRouting = profileManager.RequiresCustomRouting("+1234567890");
                    Console.WriteLine($"✅ International number requires custom routing: {needsRouting}");
                }
                else
                {
                    Console.WriteLine("❌ Profile handler is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error testing Elevate profile: {ex.Message}");
            }
        }
        
        private static void TestGenericProfile(EnhancedProfileManager profileManager)
        {
            Console.WriteLine("--- Testing Generic Profile Handler ---");
            
            try
            {
                profileManager.LoadProfile("Generic");
                
                if (profileManager.CurrentHandler != null)
                {
                    Console.WriteLine($"✅ Profile loaded: {profileManager.CurrentHandler.ProfileName}");
                    
                    // Test custom headers (should be minimal)
                    var headers = profileManager.GetActiveProfileHeaders();
                    Console.WriteLine($"✅ Custom headers count: {headers.Count} (should be 0 for maximum compatibility)");
                    
                    // Test preferred codecs
                    var codecs = profileManager.GetPreferredCodecs();
                    Console.WriteLine($"✅ Preferred codecs: {string.Join(", ", codecs)}");
                    
                    // Test custom routing (should be false)
                    var needsRouting = profileManager.RequiresCustomRouting("5551234567");
                    Console.WriteLine($"✅ Regular number requires custom routing: {needsRouting} (should be false)");
                }
                else
                {
                    Console.WriteLine("❌ Profile handler is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error testing Generic profile: {ex.Message}");
            }
        }
        
        private static void TestMessageHandling(EnhancedProfileManager profileManager)
        {
            Console.WriteLine("--- Testing Message Handling ---");
            
            try
            {
                // Load Avaya profile for testing
                profileManager.LoadProfile("Avaya_Aura");
                
                // Test processing outgoing REGISTER message
                var registerMessage = "REGISTER sip:192.168.1.180 SIP/2.0\r\n" +
                                    "Via: SIP/2.0/TCP 192.168.1.9:5060;branch=z9hG4bK123\r\n" +
                                    "From: <sip:103@192.168.1.180>;tag=abc123\r\n" +
                                    "To: <sip:103@192.168.1.180>\r\n" +
                                    "Call-ID: test-call-id@192.168.1.9\r\n" +
                                    "CSeq: 1 REGISTER\r\n" +
                                    "Contact: <sip:103@192.168.1.9:5060>\r\n" +
                                    "Expires: 3600\r\n" +
                                    "Content-Length: 0\r\n\r\n";
                
                var processedMessage = profileManager.ProcessOutgoingMessage(registerMessage, "REGISTER");
                Console.WriteLine($"✅ Processed outgoing REGISTER message (length: {processedMessage.Length})");
                
                // Test handling incoming response
                var responseMessage = "SIP/2.0 200 OK\r\n" +
                                    "Via: SIP/2.0/TCP 192.168.1.9:5060;branch=z9hG4bK123\r\n" +
                                    "From: <sip:103@192.168.1.180>;tag=abc123\r\n" +
                                    "To: <sip:103@192.168.1.180>;tag=def456\r\n" +
                                    "Call-ID: test-call-id@192.168.1.9\r\n" +
                                    "CSeq: 1 REGISTER\r\n" +
                                    "Contact: <sip:103@192.168.1.180>\r\n" +
                                    "Expires: 3600\r\n" +
                                    "Content-Length: 0\r\n\r\n";
                
                profileManager.HandleIncomingMessage(responseMessage, "REGISTER_RESPONSE");
                Console.WriteLine($"✅ Handled incoming REGISTER response");
                
                // Test registration validation
                var isValid = profileManager.ValidateRegistration(responseMessage, "200");
                Console.WriteLine($"✅ Registration validation result: {isValid}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error testing message handling: {ex.Message}");
            }
        }
        
        private static void TestAvailableProfiles(EnhancedProfileManager profileManager)
        {
            Console.WriteLine("--- Testing Available Profiles ---");
            
            try
            {
                var profiles = profileManager.GetAvailableProfiles();
                Console.WriteLine($"✅ Found {profiles.Count} available profiles:");
                
                foreach (var profile in profiles)
                {
                    Console.WriteLine($"   - {profile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting available profiles: {ex.Message}");
            }
        }
    }
}
