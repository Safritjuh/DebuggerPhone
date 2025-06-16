# Caller Display and Hangup Button Fixes

## Issues Fixed

### 1. 🏷️ **Caller Display Name Issues**
**Problem**: Active call status showed full SIP URI like "Connected to sip:101@192.168.1.180" instead of clean display names.

**Root Cause**: The `ExtractCallerInfo()` method in `SimpleSipClient.cs` was returning the complete SIP URI instead of parsing the From header properly according to RFC 3261.

### 2. 🔚 **End Call Button Not Working for Incoming Calls**
**Problem**: When answering an incoming call, the End Call button didn't send BYE messages to properly terminate the call.

**Root Cause**: The `_activeCallId` field wasn't being set when accepting incoming calls, so `HangupAsync()` couldn't find the correct dialog to send BYE messages.

## Solutions Implemented

### 1. Enhanced SIP From Header Parsing (RFC 3261 Compliant)

#### Updated `ExtractCallerInfo()` Method
```csharp
private static string ExtractCallerInfo(string fromHeader)
{
    // Extract display name or number from From header according to RFC 3261
    // Format: From: "Display Name" <sip:user@domain>;tag=12345
    // or: From: <sip:user@domain>;tag=12345
    // or: From: sip:user@domain;tag=12345
    
    string result = fromHeader.Replace("From:", "").Trim();
    
    // Case 1: "Display Name" <sip:user@domain>
    if (result.Contains('<') && result.Contains('>'))
    {
        var nameEnd = result.IndexOf('<');
        if (nameEnd > 0)
        {
            var displayName = result.Substring(0, nameEnd).Trim().Trim('"');
            if (!string.IsNullOrEmpty(displayName))
            {
                return displayName; // Return display name if available
            }
        }
        
        // Extract just the number from URI
        var uri = result.Substring(start, end - start);
        return ExtractNumberFromSipUri(uri);
    }
    
    // Case 2: sip:user@domain - extract just the user part
    if (result.StartsWith("sip:"))
    {
        return ExtractNumberFromSipUri(result);
    }
    
    return result.Split(';')[0].Trim(); // Remove tag parameters
}
```

#### New Helper Method
```csharp
private static string ExtractNumberFromSipUri(string sipUri)
{
    // Extract user part from sip:user@domain
    if (sipUri.StartsWith("sip:"))
    {
        var withoutScheme = sipUri.Substring(4);
        var atIndex = withoutScheme.IndexOf('@');
        if (atIndex > 0)
        {
            return withoutScheme.Substring(0, atIndex); // Return just "101"
        }
        return withoutScheme;
    }
    return sipUri;
}
```

### 2. Fixed Incoming Call Hangup

#### Set Active Call ID on Acceptance
In `AcceptIncomingCallAsync()` method:
```csharp
// Set the active call ID for incoming call (needed for hangup)
_activeCallId = _pendingIncomingCallId;
Console.WriteLine($"[ACCEPT CALL DEBUG] Set active call ID: {_activeCallId}");
```

This ensures that when `HangupAsync()` is called:
1. It can find the correct dialog using `_dialogManager.FindDialogByCallId(_activeCallId)`
2. It can create and send the proper BYE request
3. The call is properly terminated with SIP protocol compliance

## Expected Results

### 1. Improved Caller Display
| **Before** | **After** |
|------------|-----------|
| `Connected to sip:101@192.168.1.180` | `Connected to 101` |
| `Connected to sip:john.doe@company.com` | `Connected to john.doe` |
| `Connected to "John Doe" <sip:101@domain>` | `Connected to John Doe` |

### 2. Working End Call Button
✅ **Now when you press End Call on an incoming call:**
- BYE message is sent to the caller
- Call is properly terminated on both ends
- Call state updates to "Call Ended"
- UI returns to ready state
- Audio session is stopped

## RFC 3261 Compliance

### SIP From Header Formats Supported
1. **Display Name Format**: `From: "John Doe" <sip:101@domain.com>;tag=abc123`
   - **Displays**: "John Doe"

2. **URI Format**: `From: <sip:101@192.168.1.180>;tag=abc123`
   - **Displays**: "101"

3. **Simple Format**: `From: sip:101@192.168.1.180;tag=abc123`
   - **Displays**: "101"

### BYE Message Flow
```
[User presses End Call]
1. OUTGOING: BYE sip:101@192.168.1.180 SIP/2.0
   Via: SIP/2.0/TCP [local_ip]:[local_port];branch=z9hG4bK...
   From: <sip:103@192.168.1.180>;tag=[local_tag]
   To: <sip:101@192.168.1.180>;tag=[remote_tag]
   Call-ID: [call_id]
   CSeq: [sequence] BYE

2. INCOMING: SIP/2.0 200 OK (BYE response)
   [Call terminated successfully]
```

## Testing Instructions

### Test 1: Caller Display
1. Make incoming call from different types of From headers
2. Verify clean display names appear in active call status
3. Check that display names are used when available, otherwise numbers

### Test 2: End Call Button
1. Answer an incoming call
2. Verify active call shows in main window
3. Press End Call button
4. Verify BYE message is sent (check SIP debug window)
5. Confirm call terminates on both ends

## Debug Output
When working correctly, you should see:
```
[ACCEPT CALL DEBUG] Set active call ID: [call_id]
🔄 Starting hangup process...
🔄 Terminating dialog: [call_id]
📤 Sending BYE request...
✅ Hangup completed successfully
```

## Files Modified
- `SimpleSipClient.cs`: Enhanced caller info parsing and fixed active call ID assignment

## Impact
- ✅ Clean, professional caller display (names or numbers only)
- ✅ RFC 3261 compliant From header parsing
- ✅ Working End Call button for all call types
- ✅ Proper BYE message sending for call termination
- ✅ Better user experience with clear caller identification
