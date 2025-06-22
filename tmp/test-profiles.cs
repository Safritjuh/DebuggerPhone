using System;
using System.Linq;
using Core.Models;

class Program 
{
    static void Main()
    {
        try 
        {
            Console.WriteLine("Testing SIP Profiles from new directory structure...");
            
            var profiles = SipProfile.GetPredefinedProfiles();
            Console.WriteLine($"Found {profiles.Count} profiles:");
            
            foreach (var profile in profiles)
            {
                Console.WriteLine($"  - {profile.Name}: {profile.Description}");
            }
            
            var expectedProfiles = new[] { "Generic", "Avaya IP Office", "Elevate", "Avaya Aura" };
            
            foreach (var expectedName in expectedProfiles)
            {
                var profile = profiles.FirstOrDefault(p => p.Name == expectedName);
                if (profile == null)
                {
                    Console.WriteLine($"❌ Missing expected profile: {expectedName}");
                }
                else
                {
                    Console.WriteLine($"✅ Found profile: {expectedName}");
                }
            }
            
            Console.WriteLine("Profile test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
