# 🐛 SIP Phone Bug List & Known Issues

## 📊 **Bug Overview**
This document tracks all known bugs, issues, and defects in the SIP Phone application for systematic resolution.

**Last Updated**: June 16, 2025  
**Total Bugs**: 21  
**Critical**: 4 | **High**: 5 | **Medium**: 5 | **Low**: 5 | **Fixed**: 2

---

## 🔥 **CRITICAL BUGS** (Fix Immediately)

### **BUG-001**: Audio Lost After Hold/Resume Operations
**Priority**: CRITICAL | **Status**: CONFIRMED | **Tested**: June 14, 2025

**Description**: 
Audio is not restored after performing hold/unhold (resume) operations. The SIP signaling works correctly, but RTP audio streams fail to resume properly.

**Test Results**: ✅ **CONFIRMED** - Two-way audio works initially, but lost after hold/unhold

### **BUG-002**: RTP Packet Send Errors During Active Calls
**Priority**: CRITICAL | **Status**: NEEDS INVESTIGATION | **Assigned**: -

**Description**:
Consistent RTP packet send errors occur during active calls, potentially causing audio quality issues.

**Error Pattern**:
```
[RTPAUDIO] ❌ Error sending RTP packet: Negating the minimum value of a twos complement number is invalid.
```

**Frequency**: Multiple occurrences per call  
**Impact**: May cause audio dropouts or quality degradation

**Files Involved**:
- `RtpAudioManager.cs` (SendRtpPacket method)

**Possible Causes**:
- Audio sample data conversion issue
- Buffer overflow/underflow
- Arithmetic operation on audio samples

---

### **BUG-012**: Registration Status Not Properly Tracked
**Priority**: CRITICAL | **Status**: NEW | **Discovered**: June 14, 2025

**Description**:
The application shows "registered" status even after registration has been lost on the server side. Registration refresh mechanism fails silently.

**Symptoms**:
- Message "registration refresh timer stopped" appears
- Followed by "Closed incoming connection 192.168.1.180:4613"
- Registration refresh sent but no response received
- Server shows registration lost, but client still shows "registered"
- Application doesn't detect/handle registration failures

**Impact**: User believes they can receive calls when actually unreachable

---

### **BUG-018**: Application Hangs When Declining Incoming Calls
**Priority**: CRITICAL | **Status**: NEW | **Discovered**: June 16, 2025

**Description**:
When user clicks "Decline" button on incoming call window, the application hangs completely due to DialogResult exception.

**Console Output**:
```
[INCOMING CALL DEBUG] Decline clicked, stopping ringtone
[RINGTONE DEBUG] Stopped ringtone playback
[INCOMING CALL DEBUG] Window closing, stopping ringtone
[RINGTONE DEBUG] Stopped ringtone playback
Unhandled UI exception: DialogResult can be set only after Window is created and shown as dialog.
```

**Stack Trace**:
```
at System.Windows.Window.set_DialogResult(Nullable`1 value)
at WindowsSipPhone.IncomingCallWindow.OnClosing(CancelEventArgs e) in E:\GitHub-test\Sip-Phone\IncomingCallWindow.xaml.cs:line 192
at System.Windows.Window.WmClose()
at System.Windows.Window.WindowFilterMessage(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam, Boolean& handled)
```

**Root Cause**: IncomingCallWindow is trying to set DialogResult in OnClosing event, but window was not opened as a modal dialog.

**Files Involved**:
- `IncomingCallWindow.xaml.cs` (line 192)

**Impact**: Application becomes unresponsive, requiring force quit when declining calls

---

### **BUG-013**: Incoming Call Not Properly Accepted
**Priority**: CRITICAL | **Status**: ✅ FIXED | **Discovered**: June 14, 2025 | **Fixed**: June 16, 2025

**Description**:
When an incoming call popup appears and user clicks Accept, the call doesn't actually get accepted. No call status indication.

**Symptoms**:
- Incoming call popup appears
- User clicks Accept button
- No visible call status or connection established
- Call appears to be ignored/rejected

**Resolution**:
- Fixed data binding issues in IncomingCallWindow.xaml
- Restored working caller name and number display from git commit 902c0bf
- Updated button styling to use text labels instead of circular icons
- Direct TextBlock assignment ensures caller information displays properly
- Caller name and number now show correctly during incoming calls

**Impact**: ✅ **RESOLVED** - Can now properly answer incoming calls with working caller identification

---

## 🚨 **HIGH PRIORITY BUGS**

### **BUG-003**: Network Connection Errors During Call Termination
**Priority**: HIGH | **Status**: NEEDS INVESTIGATION | **Assigned**: -

**Description**:
Multiple network errors occur when calls are terminated, suggesting improper socket cleanup.

**Error Pattern**:
```
[RTPAUDIO] ❌❌ Error receiving RTP packet: De externe host heeft een verbinding verbroken.
[RTPAUDIO] ❌❌ Error receiving RTP packet: De I/O-bewerking is afgebroken vanwege het afsluiten van een thread of vanwege een opdracht van een toepassing.
```

**Impact**: Resource leaks, potential stability issues

**Files Involved**:
- `RtpAudioManager.cs` (RTP socket management)

---

### **BUG-004**: Dialog Lookup Failures in SIP Processing
**Priority**: HIGH | **Status**: NEEDS INVESTIGATION | **Assigned**: -

**Description**:
SIP dialog lookup frequently fails, requiring fallback to Call-ID lookup.

**Debug Pattern**:
```
[DIALOG DEBUG] - Dialog found? False
[DIALOG DEBUG] *** NO DIALOG FOUND - CHECKING BY CALL ID ***
[DIALOG DEBUG] - Dialog by Call ID? True
```

**Impact**: Inefficient SIP processing, potential call handling issues

**Files Involved**:
- `SimpleSipClient.cs` (Dialog management)

---

### **BUG-005**: Incomplete Configuration Persistence
**Priority**: HIGH | **Status**: DOCUMENTED | **Assigned**: -

**Description**:
Several configuration settings are not persisted between application sessions.

**Missing Persistence**:
- Speed dial configuration
- Diagnostics filter settings

**Files Involved**:
- `SpeedDialConfigWindow.xaml.cs` (Lines 223, 260)
- `Pages/DiagnosticsPage.xaml.cs` (Line 205)

**TODOs Found**:
```csharp
// TODO: Save to configuration file for persistence
// TODO: Implement configuration persistence
// TODO: Implement actual filtering logic
```

---

### **BUG-006**: Diagnostics Page SIP Tracing Not Connected
**Priority**: HIGH | **Status**: DOCUMENTED | **Assigned**: -

**Description**:
The diagnostics page is not connected to the actual SIP service for real-time message tracing.

**File**: `Pages/DiagnosticsPage.xaml.cs` (Line 124)
```csharp
// TODO: Connect to actual SIP service for real tracing
```

**Impact**: Debugging and troubleshooting capabilities are limited

---

## ⚠️ **MEDIUM PRIORITY BUGS**

### **BUG-007**: Audio Device Selection Not Fully Implemented
**Priority**: MEDIUM | **Status**: PARTIAL | **Assigned**: -

**Description**:
Audio device selection UI exists but switching logic may not be fully functional.

**Files Involved**:
- `Pages/AudioSettingsPage.xaml.cs`
- `RtpAudioManager.cs`

**Status**: Device enumeration works, but dynamic switching needs verification

---

### **BUG-008**: System Tray Integration Incomplete
**Priority**: MEDIUM | **Status**: PARTIAL | **Assigned**: -

**Description**:
System tray functionality is implemented but may have edge cases with minimize/restore behavior.

**Files Involved**:
- `MainWindow.xaml.cs`

**Needs Testing**: Multi-monitor scenarios, Windows 11 compatibility

---

### **BUG-009**: Build Warnings Were Present
**Priority**: MEDIUM | **Status**: ✅ FIXED | **Assigned**: -

**Description**:
Project had CS0019 errors and CA1416 warnings that prevented clean builds.

**Resolution**: 
- Fixed null-coalescing operator usage in `DialerPage.xaml.cs`
- Added `.editorconfig` to suppress Windows platform warnings
- Build now succeeds with 0 errors, 0 warnings

---

### **BUG-019**: Duplicate Name Display in Incoming Call Window
**Priority**: MEDIUM | **Status**: ✅ FIXED | **Discovered**: June 16, 2025 | **Fixed**: June 16, 2025

**Description**:
In the incoming call window, the caller's name was displayed twice instead of following the proper format.

**Root Cause**:
The `ExtractCallerInfo()` method in `SimpleSipClient.cs` was returning only the display name when available, losing the SIP URI information needed to extract the phone number separately.

**Resolution**:
- Modified `ExtractCallerInfo()` to preserve the complete caller information structure
- Now returns full format: `"Display Name" <sip:user@domain>` instead of just "Display Name"
- This allows `IncomingCallWindow.ParseCallerInfo()` to properly extract both name and number
- Fixed display format: Line 1 shows name (or number if no name), Line 2 always shows number

**Files Involved**:
- `SimpleSipClient.cs` (ExtractCallerInfo method)

**Impact**: ✅ **RESOLVED** - Incoming call window now displays caller name and number correctly

---

### **BUG-020**: Duplicate Name Display in Call History List
**Priority**: MEDIUM | **Status**: NEW | **Discovered**: June 16, 2025

**Description**:
In the call history list, the caller's name is displayed twice instead of following the proper format.

**Current Behavior**:
- Line 1: Shows caller name
- Line 2: Shows caller name again

**Required Behavior**:
- Line 1: Show caller name, or if name not available, show the number
- Line 2: Always show the number

**Files Involved**:
- Call history UI components
- Call history data binding logic

**Impact**: Redundant information display, missing phone number context

---

### **BUG-021**: Active Call Display Shows Full SIP Header Instead of Clean Name
**Priority**: MEDIUM | **Status**: NEW | **Discovered**: June 16, 2025

**Description**:
During an active call, the dialer shows the full SIP header format instead of displaying a clean caller name.

**Current Behavior**:
- Shows: `Connected to "Alice" <sip:101@192.168.1.180>`

**Required Behavior**:
- Should show: `Connected to Alice` (just the display name)
- Or if no display name: `Connected to 101` (just the number)

**Root Cause**:
The fix for BUG-019 modified `ExtractCallerInfo()` to preserve full SIP header format, which now affects active call display in DialerPage.

**Files Involved**:
- `Pages/DialerPage.xaml.cs` (CallStatusText property, line 284)
- Caller info parsing logic

**Impact**: Unprofessional UI display showing technical SIP formatting to end users

---

### **BUG-013**: Incoming Call UI and Acceptance
**Priority**: CRITICAL | **Status**: ✅ FIXED | **Fixed**: June 16, 2025

**Description**:
Incoming call window had display and functionality issues including caller info not showing and UI problems.

**Resolution**:
- Fixed XAML data binding issues for caller name and number display
- Restored working implementation from git commit 902c0bf
- Updated button styling to use clear "Answer"/"Decline" text labels
- Implemented direct TextBlock assignment for reliable display
- Fixed XML syntax errors in multiple XAML files
- Added missing Mute_Click method to prevent build errors

**Impact**: ✅ **RESOLVED** - Incoming calls now display caller information correctly with functional Accept/Decline buttons

---

## 📝 **LOW PRIORITY BUGS**

### **BUG-010**: DTMF Support Not Implemented
**Priority**: LOW | **Status**: NOT STARTED | **Assigned**: -

**Description**:
DTMF (dual-tone multi-frequency) support for IVR interaction is not implemented.

**Files Needed**:
- DTMF packet generation in `RtpAudioManager.cs`
- DTMF UI keypad component

**Impact**: Cannot interact with phone systems requiring DTMF input

---

### **BUG-011**: Call Transfer Functionality Missing
**Priority**: LOW | **Status**: NOT STARTED | **Assigned**: -

**Description**:
SIP REFER method for call transfer is not implemented.

**Files Involved**:
- `SimpleSipClient.cs` (needs REFER method support)

---

### **BUG-014**: Debug Text Visible in Registration Status
**Priority**: LOW | **Status**: NEW | **Discovered**: June 14, 2025

**Description**:
Debug text "DEBUG: _RegistrationCompletion.SetResult(true) completed" appears in the UI status section after successful registration.

**Location**: Below Register/Unregister buttons in status section
**Impact**: Unprofessional appearance, confuses users
**Fix**: Remove debug text from UI display

### **BUG-015**: Theme Switcher Not Functional  
**Priority**: LOW | **Status**: NEW | **Discovered**: June 14, 2025

**Description**:
Theme dropdown changes from Light to Dark but visual appearance doesn't change.

**Symptoms**:
- Theme selector shows different values
- UI appearance remains unchanged
- Dark/Light theme not applied

### **BUG-016**: Call Duration Not Tracked in History
**Priority**: LOW | **Status**: NEW | **Discovered**: June 14, 2025

**Description**:
Call history doesn't track actual speaking time/call duration.

**Impact**: Cannot analyze call patterns or billing information

### **BUG-017**: Redial Requires Manual Call Initiation
**Priority**: LOW | **Status**: NEW | **Discovered**: June 14, 2025

**Description**:
When clicking Redial, number is populated in keypad but user must manually press Call button.

**Expected**: Immediate call initiation on Redial click
**Actual**: Two-step process (populate + manual call)

---

## 🔧 **INVESTIGATION GUIDELINES**

### **For BUG-001 (Audio Resume)**:
1. Test hold/resume with debug output enabled
2. Verify RTP socket state before/after resume
3. Check Windows audio session state
4. Monitor NAudio device initialization sequence
5. Test with different audio devices

### **For BUG-002 (RTP Send Errors)**:
1. Examine audio sample data conversion
2. Check buffer boundaries and data types
3. Verify RTP packet format
4. Test with different audio codecs

### **For Network Errors (BUG-003)**:
1. Implement proper socket disposal pattern
2. Add timeout handling for RTP operations
3. Verify thread cancellation logic

---

## 📋 **TESTING CHECKLIST**

### **Before Fixing Audio Issues**:
- [ ] Clean build verification (currently ✅)
- [ ] SIP registration test with 103/274104@192.168.1.180:5060
- [ ] Basic call establishment test
- [ ] Audio device enumeration verification

### **Audio Resume Testing Protocol**:
1. [ ] Establish call with audio working
2. [ ] Press Hold button - verify audio stops
3. [ ] Press Resume button - verify audio returns
4. [ ] Test multiple hold/resume cycles
5. [ ] Test with different audio devices
6. [ ] Monitor debug output for error patterns

---

## 🎯 **NEXT ACTIONS**

1. **Immediate**: Test BUG-001 (audio resume) with current debugging
2. **Week 1**: Investigate and fix RTP packet send errors (BUG-002)
3. **Week 2**: Fix configuration persistence issues (BUG-005, BUG-006)
4. **Week 3**: Complete audio device switching verification (BUG-007)

---

**Repository State**: All changes committed to `feature/keyboard-shortcuts` branch  
**Build Status**: ✅ Clean (0 errors, 0 warnings)  
**Debug Infrastructure**: ✅ Comprehensive logging in place
