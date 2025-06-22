using System;
using System.IO;

class Program 
{
    static void Main()
    {
        try 
        {
            Console.WriteLine("Testing profile directory resolution...");
            
            // Simulate the same logic as in SipProfile.cs
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Console.WriteLine($"Base directory: {baseDirectory}");
            
            string profilesDirectory = null;
            
            // Try to find the source profiles directory
            var currentDir = new DirectoryInfo(baseDirectory);
            while (currentDir != null)
            {
                var testProfilesDir = Path.Combine(currentDir.FullName, "profiles");
                Console.WriteLine($"Testing: {testProfilesDir}");
                if (Directory.Exists(testProfilesDir))
                {
                    profilesDirectory = testProfilesDir;
                    Console.WriteLine($"✅ Found profiles directory: {profilesDirectory}");
                    break;
                }
                currentDir = currentDir.Parent;
            }
            
            if (profilesDirectory == null)
            {
                Console.WriteLine("❌ No profiles directory found");
                return;
            }
            
            // List INI files found
            var iniFiles = Directory.GetFiles(profilesDirectory, "*.ini");
            Console.WriteLine($"Found {iniFiles.Length} INI files:");
            foreach (var file in iniFiles)
            {
                Console.WriteLine($"  - {Path.GetFileName(file)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
