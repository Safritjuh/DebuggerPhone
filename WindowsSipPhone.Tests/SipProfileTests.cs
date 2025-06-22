using System;
using System.IO;
using System.Linq;
using WindowsSipPhone.Models;
using WindowsSipPhone.Utils;

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
            TestIniFileHandling();
            TestProfileImportExport();
            
            Console.WriteLine("✅ All SIP Profile tests completed successfully!");
        }
          private static void TestPredefinedProfiles()
        {
            Console.WriteLine("\n📋 Testing Predefined Profiles...");
            
            var profiles = SipProfile.GetPredefinedProfiles();
            
            if (profiles.Count != 4)
                throw new Exception($"Expected 4 predefined profiles, got {profiles.Count}");
            
            var expectedProfiles = new[] { "Generic", "Avaya IP Office", "Elevate", "Avaya Aura" };
            
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
            
            Console.WriteLine($"   ✓ Profile selection: {avayaProfile.Name}");        }
        
        private static void TestProfileSelection()
        {
            Console.WriteLine("\n🎯 Testing Profile-Specific Settings...");
            
            var genericProfile = SipProfile.GetPredefinedProfile("Generic");
            var avayaProfile = SipProfile.GetPredefinedProfile("Avaya IP Office");
            var elevateProfile = SipProfile.GetPredefinedProfile("Elevate");
            
            if (genericProfile == null || avayaProfile == null || elevateProfile == null)
                throw new Exception("Failed to get predefined profiles");
            
            // Test different registration expiry times
            Console.WriteLine($"   ✓ Generic expiry: {genericProfile.RegistrationExpiry}s");
            Console.WriteLine($"   ✓ Avaya IP Office expiry: {avayaProfile.RegistrationExpiry}s");
            Console.WriteLine($"   ✓ Elevate expiry: {elevateProfile.RegistrationExpiry}s");
            
            // Test user agent differences
            Console.WriteLine($"   ✓ Generic user agent: {genericProfile.UserAgentString}");
            Console.WriteLine($"   ✓ Avaya user agent: {avayaProfile.UserAgentString}");
            Console.WriteLine($"   ✓ Elevate user agent: {elevateProfile.UserAgentString}");
            
            // Test codec preferences
            Console.WriteLine($"   ✓ Generic codecs: {string.Join(",", genericProfile.PreferredCodecs)}");
            Console.WriteLine($"   ✓ Avaya codecs: {string.Join(",", avayaProfile.PreferredCodecs)}");
            Console.WriteLine($"   ✓ Elevate codecs: {string.Join(",", elevateProfile.PreferredCodecs)}");
            
            // Test that Avaya IP Office has updated settings
            if (avayaProfile.RegistrationExpiry != 180)
                throw new Exception($"Expected Avaya IP Office expiry to be 180s, got {avayaProfile.RegistrationExpiry}s");
            
            if (avayaProfile.UserAgentString != "SIP TEST Phone")
                throw new Exception($"Expected Avaya IP Office user agent to be 'SIP TEST Phone', got '{avayaProfile.UserAgentString}'");
            
            Console.WriteLine($"   ✓ Avaya IP Office updated settings verified");
        }
        
        private static void TestIniFileHandling()
        {
            Console.WriteLine("\n📄 Testing INI File Handling...");
            
            var tempFile = Path.GetTempFileName() + ".ini";
            
            try
            {
                // Create test profile
                var testProfile = new SipProfile
                {
                    Name = "Test Profile",
                    Description = "Test profile for INI handling",
                    IsCustom = true,
                    RegistrationExpiry = 1234,
                    RequireKeepAlive = true,
                    KeepAliveInterval = 45,
                    Transport = "UDP",
                    UserAgentString = "Test-Agent/1.0",
                    PreferredCodecs = new System.Collections.Generic.List<string> { "G722", "PCMU" },
                    CustomHeaders = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "X-Test-Header", "TestValue" },
                        { "X-Another-Header", "Another Value" }
                    }
                };
                
                // Save to INI file
                SipProfile.SaveProfileToIniFile(testProfile, tempFile);
                Console.WriteLine($"   ✓ Profile saved to INI file: {tempFile}");
                
                // Verify INI file content
                var iniContent = File.ReadAllText(tempFile);
                if (!iniContent.Contains("[Profile]") || !iniContent.Contains("Name=Test Profile"))
                    throw new Exception("INI file format is incorrect");
                
                Console.WriteLine("   ✓ INI file format verified");
                
                // Load from INI file
                var loadedProfile = ProfileManager.ImportProfileFromIni(tempFile);
                
                // Verify data
                if (loadedProfile.Name != testProfile.Name)
                    throw new Exception($"Name mismatch: expected '{testProfile.Name}', got '{loadedProfile.Name}'");
                
                if (loadedProfile.RegistrationExpiry != testProfile.RegistrationExpiry)
                    throw new Exception($"RegistrationExpiry mismatch: expected {testProfile.RegistrationExpiry}, got {loadedProfile.RegistrationExpiry}");
                
                if (loadedProfile.RequireKeepAlive != testProfile.RequireKeepAlive)
                    throw new Exception($"RequireKeepAlive mismatch: expected {testProfile.RequireKeepAlive}, got {loadedProfile.RequireKeepAlive}");
                
                if (loadedProfile.PreferredCodecs.Count != testProfile.PreferredCodecs.Count)
                    throw new Exception($"Codec count mismatch: expected {testProfile.PreferredCodecs.Count}, got {loadedProfile.PreferredCodecs.Count}");
                
                if (loadedProfile.CustomHeaders.Count != testProfile.CustomHeaders.Count)
                    throw new Exception($"Custom headers count mismatch: expected {testProfile.CustomHeaders.Count}, got {loadedProfile.CustomHeaders.Count}");
                
                Console.WriteLine("   ✓ Profile loaded and verified from INI file");
                Console.WriteLine($"     - Name: {loadedProfile.Name}");
                Console.WriteLine($"     - Expiry: {loadedProfile.RegistrationExpiry}s");
                Console.WriteLine($"     - Keep-alive: {loadedProfile.RequireKeepAlive}");
                Console.WriteLine($"     - Codecs: {string.Join(", ", loadedProfile.PreferredCodecs)}");
                Console.WriteLine($"     - Custom headers: {loadedProfile.CustomHeaders.Count}");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        
        private static void TestProfileImportExport()
        {
            Console.WriteLine("\n🔄 Testing Profile Import/Export...");
            
            var tempDir = Path.Combine(Path.GetTempPath(), "SipProfileTest");
            Directory.CreateDirectory(tempDir);
            
            try
            {
                // Export all profiles to INI files
                ProfileManager.ExportAllProfilesToIni(tempDir);
                Console.WriteLine($"   ✓ Exported all profiles to: {tempDir}");
                
                // Verify files were created
                var iniFiles = Directory.GetFiles(tempDir, "*.ini");
                if (iniFiles.Length == 0)
                    throw new Exception("No INI files were created during export");
                
                Console.WriteLine($"   ✓ Created {iniFiles.Length} INI files");
                
                foreach (var file in iniFiles)
                {
                    var fileName = Path.GetFileName(file);
                    Console.WriteLine($"     - {fileName}");
                    
                    // Test importing each file
                    var imported = ProfileManager.ImportProfileFromIni(file);
                    if (string.IsNullOrEmpty(imported.Name))
                        throw new Exception($"Failed to import profile from {fileName}");
                    
                    Console.WriteLine($"       ✓ Imported: {imported.Name}");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}