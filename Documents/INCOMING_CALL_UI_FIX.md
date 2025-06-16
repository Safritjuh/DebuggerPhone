# Incoming Call Active State UI Fix

## Issue Description
When an incoming call was answered, the audio worked correctly both ways, but the active call was not displayed in the main window UI. Users could not see that they were in an active call, including the caller information, call duration, or call controls.

## Root Cause Analysis
The issue was in the `DialerPage.xaml.cs` call state handling logic:

1. **Incoming Call Flow Gap**: When an incoming call arrived, the `OnCallStateChanged` method received `"Incoming call: [number]"` but didn't store the caller information for later use.

2. **Missing Call Acceptance Handler**: When the call was accepted, `SipPhoneService` sent `"Call accepted"` but the DialerPage wasn't handling this state to start the active call UI.

3. **State Mismatch**: The `SetCallConnected()` method was called but without first calling `StartCall()`, so `_isCallActive` remained false and the UI didn't update.

## Solution Implemented

### 1. Added Incoming Call Number Storage
```csharp
private string _incomingCallNumber = ""; // Store incoming call number until accepted
```

### 2. Enhanced Call State Handling
Updated `OnCallStateChanged` method to:
- Store incoming caller information when `"Incoming call:"` is received
- Handle `"Call accepted"` state to start the call UI for incoming calls
- Clear the stored number when calls end

### 3. Improved Call Flow Logic
```csharp
else if (callState.Contains("Call accepted") || callState.Contains("Call Connected") || 
         callState.Contains("Call answered") || callState.Contains("200 OK") || 
         callState.Contains("Incoming Call Answered"))
{
    // For incoming calls that are accepted, start the call UI
    if (!_isCallActive && !string.IsNullOrEmpty(_incomingCallNumber))
    {
        _logger.LogSystemInfo("CALL", $"✅ Starting UI for accepted incoming call from: {_incomingCallNumber}");
        StartCall(_incomingCallNumber);
        _incomingCallNumber = ""; // Clear after use
    }
    
    // Call is connected and active - start the timer
    SetCallConnected();
}
```

## Expected SIP Message Flow
```
1. INCOMING: INVITE sip:103@192.168.1.9 SIP/2.0
   → DialerPage receives: "Incoming call: [caller_info]"
   → Stores caller info in _incomingCallNumber

2. OUTGOING: SIP/2.0 180 Ringing
   → IncomingCallWindow shows to user

3. [User presses Answer]
   → SipPhoneService.AcceptIncomingCallAsync() called

4. OUTGOING: SIP/2.0 200 OK
   → SipPhoneService sends: "Call accepted"
   → DialerPage calls StartCall(_incomingCallNumber)
   → DialerPage calls SetCallConnected() to start timer
   → UI shows active call with caller info and duration

5. INCOMING: ACK
   → Call fully established
```

## UI Components Affected

### Main Window
- Status bar shows "Call accepted from [caller]"
- Tray icon updates to "Call Active"

### DialerPage Active Call Display
- Green bordered call status area becomes visible
- Shows "Connected to [caller_number]"
- Displays real-time call duration (MM:SS format)
- Call control buttons (Hold, Mute, Audio, End Call) become available
- DTMF keypad remains functional during call

## Testing Instructions

1. **Setup**: Register SIP client with test credentials (103/274104 on 192.168.1.180:5060)
2. **Initiate**: Make incoming call from extension 101 to 103
3. **Answer**: Press Answer button in IncomingCallWindow popup
4. **Verify**: Check that main window shows:
   - Active call status in DialerPage
   - Caller number displayed
   - Call timer running
   - Call control buttons available
5. **Audio**: Confirm bidirectional audio works
6. **End**: Test call ending updates UI properly

## Debug Output
When working correctly, you should see these console messages:
```
📞 Incoming call from: [caller_info]
✅ Starting UI for accepted incoming call from: [caller_info]
[MAIN WINDOW DEBUG] CallAnswered event received - accepting call
[SIP SERVICE DEBUG] AcceptIncomingCallAsync called
[200 OK DEBUG] 200 OK response sent successfully
```

## Files Modified
- `Pages/DialerPage.xaml.cs`: Enhanced call state handling and added incoming call number storage

## Impact
- ✅ Incoming calls now properly display as active calls in main window
- ✅ Call duration timer works for incoming calls
- ✅ All call control features available for incoming calls
- ✅ Improved debugging and logging for call state transitions
- ✅ Better user experience - users can see and manage incoming calls properly

## Related Issues Fixed
- Active call not showing in main window for incoming calls
- Call timer not starting for accepted incoming calls
- Missing caller information display for incoming calls
- Call control buttons not available for incoming calls
