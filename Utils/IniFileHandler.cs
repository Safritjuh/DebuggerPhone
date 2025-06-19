using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WindowsSipPhone.Utils
{
    /// <summary>
    /// Simple INI file handler for reading and writing INI files
    /// </summary>
    public static class IniFileHandler
    {
        /// <summary>
        /// Read an INI file and return a dictionary of sections and key-value pairs
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> ReadIniFile(string filePath)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            
            if (!File.Exists(filePath))
                return result;
            
            var lines = File.ReadAllLines(filePath);
            string currentSection = "";
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;
                
                // Check for section header
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    if (!result.ContainsKey(currentSection))
                        result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                
                // Parse key-value pair
                var equalIndex = trimmedLine.IndexOf('=');
                if (equalIndex > 0)
                {
                    var key = trimmedLine.Substring(0, equalIndex).Trim();
                    var value = trimmedLine.Substring(equalIndex + 1).Trim();
                    
                    // Remove quotes if present
                    if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length > 1)
                        value = value.Substring(1, value.Length - 2);
                    
                    if (!result.ContainsKey(currentSection))
                        result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    
                    result[currentSection][key] = value;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Write a dictionary of sections and key-value pairs to an INI file
        /// </summary>
        public static void WriteIniFile(string filePath, Dictionary<string, Dictionary<string, string>> data)
        {
            var sb = new StringBuilder();
            
            foreach (var section in data)
            {
                sb.AppendLine($"[{section.Key}]");
                
                foreach (var kvp in section.Value)
                {
                    var value = kvp.Value;
                    // Quote values that contain spaces or special characters
                    if (value.Contains(" ") || value.Contains(";") || value.Contains("#") || value.Contains("="))
                        value = $"\"{value}\"";
                    
                    sb.AppendLine($"{kvp.Key}={value}");
                }
                
                sb.AppendLine(); // Empty line between sections
            }
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            File.WriteAllText(filePath, sb.ToString());
        }
        
        /// <summary>
        /// Get a value from an INI data structure with a default fallback
        /// </summary>
        public static string GetValue(Dictionary<string, Dictionary<string, string>> data, string section, string key, string defaultValue = "")
        {
            if (data.TryGetValue(section, out var sectionData) && sectionData.TryGetValue(key, out var value))
                return value;
            
            return defaultValue;
        }
        
        /// <summary>
        /// Get a boolean value from an INI data structure
        /// </summary>
        public static bool GetBoolValue(Dictionary<string, Dictionary<string, string>> data, string section, string key, bool defaultValue = false)
        {
            var value = GetValue(data, section, key, defaultValue.ToString());
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) || 
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase) || 
                   value.Equals("1", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Get an integer value from an INI data structure
        /// </summary>
        public static int GetIntValue(Dictionary<string, Dictionary<string, string>> data, string section, string key, int defaultValue = 0)
        {
            var value = GetValue(data, section, key, defaultValue.ToString());
            return int.TryParse(value, out var result) ? result : defaultValue;
        }
        
        /// <summary>
        /// Get a string list from an INI data structure (comma-separated values)
        /// </summary>
        public static List<string> GetListValue(Dictionary<string, Dictionary<string, string>> data, string section, string key, List<string>? defaultValue = null)
        {
            var value = GetValue(data, section, key, "");
            if (string.IsNullOrEmpty(value))
                return defaultValue ?? new List<string>();
            
            return value.Split(',')
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .ToList();
        }
        
        /// <summary>
        /// Get a dictionary of custom headers from an INI section
        /// </summary>
        public static Dictionary<string, string> GetDictionaryValue(Dictionary<string, Dictionary<string, string>> data, string section, string keyPattern = "Header_")
        {
            var result = new Dictionary<string, string>();
            
            if (!data.TryGetValue(section, out var sectionData))
                return result;
            
            foreach (var kvp in sectionData)
            {
                if (kvp.Key.StartsWith(keyPattern, StringComparison.OrdinalIgnoreCase))
                {
                    var headerName = kvp.Key.Substring(keyPattern.Length);
                    result[headerName] = kvp.Value;
                }
            }
            
            return result;
        }
    }
}