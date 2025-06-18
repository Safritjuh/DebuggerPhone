using System;
using System.Linq;
using WindowsSipPhone.Models;

namespace WindowsSipPhone.Tests
{
    /// <summary>
    /// Simple test to verify SIP Profile system functionality
    /// </summary>
    public static class SipProfileTests
    {
        public static void RunTests()
        {
            Console.WriteLine("🧪 Running SIP Profile System Tests...");
            
            TestPredefinedProfiles();
            TestProfileConfiguration();
            TestProfileSelection();
            
            Console.WriteLine("✅ All SIP Profile tests completed successfully!");
        }
        
        private static void TestPredefinedProfiles()
        {
            Console.WriteLine("\n📋 Testing Predefined Profiles...");
            
            var profiles = SipProfile.GetPredefinedProfiles();
            
            if (profiles.Count != 5)
                throw new Exception($"Expected 5 predefined profiles, got {profiles.Count}");
            
            var expectedProfiles = new[] { "Generic", "Avaya IP Office", "Cloud Generic", "FreeSWITCH", "Cisco" };
            
            foreach (var expectedName in expectedProfiles)
            {
                var profile = profiles.FirstOrDefault(p => p.Name == expectedName);
                if (profile == null)
                    throw new Exception($"Profile '{expectedName}' not found");
                
                Console.WriteLine($"   ✓ {profile.Name}: {profile.Description}");
                Console.WriteLine($"     - Expiry: {profile.RegistrationExpiry}s, Transport: {profile.Transport}");
                Console.WriteLine($"     - User Agent: {profile.UserAgentString}");
                Console.WriteLine($"     - Custom Headers: {profile.CustomHeaders.Count}");
            }
        }
        
        private static void TestProfileConfiguration()
        {
            Console.WriteLine("\n⚙️ Testing Profile Configuration...");
            
            var config = new SipConfiguration();
            
            // Test default profile
            var defaultProfile = config.GetSelectedProfile();
            if (defaultProfile.Name != "Generic")
                throw new Exception($"Expected default profile 'Generic', got '{defaultProfile.Name}'");
            
            Console.WriteLine($"   ✓ Default profile: {defaultProfile.Name}");
            
            // Test profile selection
            config.SelectedProfileName = "Avaya IP Office";
            var avayaProfile = config.GetSelectedProfile();
            if (avayaProfile.Name != "Avaya IP Office")
                throw new Exception($"Expected 'Avaya IP Office', got '{avayaProfile.Name}'");
            
            Console.WriteLine($"   ✓ Profile selection: {avayaProfile.Name}");
            Console.WriteLine($"     - Registration Expiry: {avayaProfile.RegistrationExpiry}s");
        }
        
        private static void TestProfileSelection()
        {
            Console.WriteLine("\n🎯 Testing Profile-Specific Settings...");
            
            var genericProfile = SipProfile.GetPredefinedProfile("Generic");
            var avayaProfile = SipProfile.GetPredefinedProfile("Avaya IP Office");
            var cloudProfile = SipProfile.GetPredefinedProfile("Cloud Generic");
            
            if (genericProfile == null || avayaProfile == null || cloudProfile == null)
                throw new Exception("Failed to get predefined profiles");
            
            // Test different registration expiry times
            Console.WriteLine($"   ✓ Generic expiry: {genericProfile.RegistrationExpiry}s");
            Console.WriteLine($"   ✓ Avaya expiry: {avayaProfile.RegistrationExpiry}s");
            Console.WriteLine($"   ✓ Cloud expiry: {cloudProfile.RegistrationExpiry}s");
            
            // Test keep-alive differences
            Console.WriteLine($"   ✓ Cloud keep-alive: {cloudProfile.RequireKeepAlive} ({cloudProfile.KeepAliveInterval}s)");
            Console.WriteLine($"   ✓ Avaya keep-alive: {avayaProfile.RequireKeepAlive}");
            
            // Test custom headers
            Console.WriteLine($"   ✓ Avaya headers: {avayaProfile.CustomHeaders.Count}");
            Console.WriteLine($"   ✓ Cloud headers: {cloudProfile.CustomHeaders.Count}");
            
            if (avayaProfile.CustomHeaders.Count > 0)
            {
                var firstHeader = avayaProfile.CustomHeaders.First();
                Console.WriteLine($"     - {firstHeader.Key}: {firstHeader.Value}");
            }
        }
    }
}