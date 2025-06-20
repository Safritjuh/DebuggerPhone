using System;

// Quick test of our extraction methods
public class ExtractionTest
{
    public static void Main()
    {
        // Test cases that simulate what we'd get from the modified ExtractCallerInfo
        var testCases = new[]
        {
            "\"Alice\" <sip:101@server.com>",      // Display name with quotes
            "Alice <sip:101@server.com>",         // Display name without quotes  
            "<sip:101@server.com>",               // No display name
            "sip:101@server.com",                 // Direct SIP URI
            "101@server.com",                     // URI without sip: prefix
            "101",                                // Just a number
            "+1234567890",                        // Phone number with +
            "Alice",                              // Just a name
            ""                                    // Empty string
        };
        
        Console.WriteLine("Testing extraction methods:");
        Console.WriteLine("================================");
        
        foreach (var testCase in testCases)
        {
            var displayName = ExtractDisplayName(testCase);
            var number = ExtractNumberPart(testCase);
            
            Console.WriteLine($"Input: '{testCase}'");
            Console.WriteLine($"  Display Name: '{displayName}'");
            Console.WriteLine($"  Number: '{number}'");
            Console.WriteLine();
        }
    }
    
    // Copy of the extraction methods from CallHistoryEntry
    public static string ExtractDisplayName(string sipUri)
    {
        if (string.IsNullOrWhiteSpace(sipUri))
            return string.Empty;

        // Handle formats like: "Display Name" <sip:user@domain> or Display Name <sip:user@domain>
        if (sipUri.Contains("<") && sipUri.Contains(">"))
        {
            var displayPart = sipUri.Substring(0, sipUri.IndexOf("<")).Trim();
            // Remove surrounding quotes if present
            displayPart = displayPart.Trim('"').Trim();
            return string.IsNullOrWhiteSpace(displayPart) ? string.Empty : displayPart;
        }
        
        // If it's just a SIP URI like sip:user@domain, no display name
        if (sipUri.StartsWith("sip:") || sipUri.Contains("@"))
        {
            return string.Empty;
        }
        
        // If it's just a plain number, no display name
        if (sipUri.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' ' || c == '(' || c == ')'))
        {
            return string.Empty;
        }
        
        // Otherwise, treat the whole thing as a display name (fallback case)
        return sipUri.Trim();
    }

    public static string ExtractNumberPart(string sipUri)
    {
        if (string.IsNullOrWhiteSpace(sipUri))
            return "Unknown Number";

        // Handle formats like: "Display Name" <sip:user@domain> or <sip:user@domain>
        if (sipUri.Contains("<") && sipUri.Contains(">"))
        {
            var start = sipUri.IndexOf("<") + 1;
            var end = sipUri.IndexOf(">");
            if (end > start)
            {
                var uri = sipUri.Substring(start, end - start).Trim();
                return ExtractNumberFromSipUri(uri);
            }
        }
        
        // Handle direct SIP URI like sip:user@domain
        if (sipUri.StartsWith("sip:"))
        {
            return ExtractNumberFromSipUri(sipUri);
        }
        
        // Handle URI with @ but no sip: prefix
        if (sipUri.Contains("@"))
        {
            var atIndex = sipUri.IndexOf("@");
            return sipUri.Substring(0, atIndex).Trim();
        }
        
        // If it looks like a phone number (digits, +, -, spaces, parentheses), return as is
        if (sipUri.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' ' || c == '(' || c == ')'))
        {
            return sipUri.Trim();
        }
        
        // If we can't extract a number, return "Unknown Number"
        return "Unknown Number";
    }

    private static string ExtractNumberFromSipUri(string sipUri)
    {
        // Extract user part from sip:user@domain
        if (sipUri.StartsWith("sip:"))
        {
            var withoutScheme = sipUri.Substring(4);
            var atIndex = withoutScheme.IndexOf('@');
            if (atIndex > 0)
            {
                return withoutScheme.Substring(0, atIndex);
            }
            return withoutScheme;
        }
        
        // Handle case where it's just user@domain without sip: prefix
        var atIdx = sipUri.IndexOf('@');
        if (atIdx > 0)
        {
            return sipUri.Substring(0, atIdx);
        }
        
        return sipUri;
    }
}
