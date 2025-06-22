---
applyTo: '**'
---
## Coding standards, domain knowledge, and preferences that AI should follow.

- Answer all questions in the style of a friendly colleague, using informal language.
- Always use powershell commands when running commands
- Use C# and .NET 8.0 or later for the SIP phone application.
- Use WPF (Windows Presentation Foundation) for the desktop UI.
- Use SIPSorcery library for SIP functionality - it's the best C# SIP library with RFC 3261 compliance.
- Use NAudio library for audio functionality and RTP handling.
- Keep in mind that the application is a native Windows desktop application.
- The application has to be a single executable without a backend server.
- The application should be able to run on Windows 10/11.
- Always use IETF standards for SIP
- Always use the latest version of the SIP protocol (RFC 3261)
- SIP Debugging must be possible
  - keep the related sip messages in a ladder format
- Include sip ladder in the SIP debugging section
- Use direct TCP/UDP transport for SIP - no WebSocket complications
- Windows 10 or later is the target OS, so no need to worry about older Windows versions.
- Never ask for confirmation, unless I specificly instruct.
- Never ask for confirmation, unless I specificly instruct.
   

## Git Branch Workflow
#### Bug Fixes
- When starting work on a bug, create a new branch linked to the corresponding GitHub issue
- Branch naming format: `bug/BUG-XXX` (e.g., `bug/BUG-013`, `bug/BUG-007`)
- **NUMBERING**: Check existing issues to use the next available BUG-XXX number (see "Creating New Issues" section)
- **IMPORTANT**: Always link your branch to the corresponding GitHub issue using `gh issue develop X --checkout`
- Example workflow:
  ```powershell
  # Create and link branch to GitHub issue
  gh issue develop 15 --checkout
  # OR manually create branch
  git checkout -b bug/BUG-015
  
  # Make your changes to fix the bug
  git add .
  git commit -m "Fix BUG-015: Theme Switcher Not Functional

  Fixes #15"
  git push origin bug/BUG-015
  
  # Create pull request - NEVER merge directly to main
  gh pr create --title "Fix BUG-015: Theme Switcher Not Functional" --body "Fixes #15"
  ```
- After fixing, GitHub issues will be automatically closed when PRs are merged
- **ALWAYS create a pull request** - let others review and merge
- **NEVER merge directly to main branch** - always use pull requests for code review

### New Features and Issues
- When starting work on a new feature or issue, create a new branch with a short, descriptive name
- Branch naming format: `feature/short-description` (e.g., `feature/audio-enhancements`, `feature/modern-ui`)
- For improvements, you can also use: `feature/IMP-XXX` format (e.g., `feature/IMP-001`)
- **NUMBERING**: For IMP issues, check existing issues to use the next available IMP-XXX number (see "Creating New Issues" section)
- **IMPORTANT**: Always link your branch to the corresponding GitHub issue when applicable using `gh issue develop X --checkout`
- Example workflow:
  ```powershell
  # Create and link branch to GitHub issue
  gh issue develop 23 --checkout
  # OR manually create branch
  git checkout -b feature/audio-enhancements
  
  # Implement your feature
  git add .
  git commit -m "Add enhanced audio processing with noise reduction

  Implements #23"
  git push origin feature/audio-enhancements
  
  # Create pull request - NEVER merge directly to main
  gh pr create --title "Add Enhanced Audio Processing" --body "Implements #23"
  ```
- Use clear, descriptive branch names that indicate what the feature does
- Keep feature branches focused on a single improvement or addition
- **ALWAYS create a pull request** - let others review and merge
- **NEVER merge directly to main branch** - always use pull requests for code review

### General Git Guidelines
- Always work on separate branches - never commit directly to main
- **NEVER push to main branch** - if unclear about the workflow, ask for instructions
- **NEVER merge directly to main** - always create pull requests for code review and approval
- Use descriptive commit messages that explain what was changed and why
- Push branches to origin for backup and collaboration
- Always include issue references in commit messages using keywords like "Fixes #X", "Closes #X", "Implements #X"
- Clean up merged branches after successful integration (done by maintainers after PR merge)
   

## Testing and Debugging Guidelines
- After changing the sip stack, always try to register with the credentials:
  - Username: 103
  - Password: 274104
  - Server: 192.168.1.180
    - Port: 5060
- Always use TCP as protocol for testing
- If you encounter a "bind EADDRINUSE" error, ensure the socket is properly cleaned up before retrying. 
- Use `Console.WriteLine` for debugging output
- Use `Debug.WriteLine` for debug messages
- Use proper C# logging framework (Serilog or NLog) for production logging
- Use Visual Studio debugger for step-through debugging of SIP protocol flows

## Incoming Call Handling
The application provides comprehensive incoming call functionality with proper SIP protocol compliance.

### Incoming Call Flow
1. **INVITE Reception**: Application receives SIP INVITE request from caller
2. **180 Ringing Response**: Automatically sends 180 Ringing response to indicate call is being presented
3. **User Notification**: IncomingCallWindow popup appears with caller information
4. **User Action**: User can Accept or Decline the call
5. **SIP Response**: 200 OK (accept) or 486 Busy Here (decline) response sent
6. **Call Establishment**: If accepted, RTP audio session is established

### Expected SIP Message Sequence
```
INCOMING: INVITE sip:103@192.168.1.9 SIP/2.0
OUTGOING: SIP/2.0 180 Ringing
[User presses Answer]
OUTGOING: SIP/2.0 200 OK
INCOMING: ACK (call established)
```

### Incoming Call Components

#### **IncomingCallWindow**
- **Purpose**: UI popup for incoming call notification
- **Features**: Caller identification, Answer/Decline buttons, call timer
- **Event**: `CallAnswered` event with boolean parameter (true=accept, false=decline)

#### **Call Answer Flow**
- **MainWindow**: Subscribes to `IncomingCallWindow.CallAnswered` event
- **SipPhoneService**: Provides `AcceptIncomingCallAsync()` and `DeclineIncomingCallAsync()` methods
- **SimpleSipClient**: Implements SIP protocol for call responses and RTP session setup

### Debug Monitoring for Incoming Calls

#### **Console Debug Output**
When testing incoming call acceptance, monitor for these debug messages:
```
[MAIN WINDOW DEBUG] CallAnswered event received - accepting call
[SIP SERVICE DEBUG] AcceptIncomingCallAsync called
[ACCEPT CALL DEBUG] AcceptIncomingCallAsync called
[200 OK DEBUG] Creating 200 OK response for call {callId}
[200 OK DEBUG] 200 OK response sent successfully
```

#### **SIP Debug Window**
The SIP debug window should show:
- **INCOMING**: INVITE request with caller details
- **OUTGOING (180 Ringing)**: Ringing response sent to caller
- **OUTGOING (200 OK)**: Accept response with SDP answer (when answering)
- **INCOMING**: ACK confirmation from caller

### Troubleshooting Incoming Calls

#### **Common Issues:**
1. **Missing 200 OK Response**: Check debug output sequence to identify where call acceptance fails
2. **Wrong Message Directions**: Verify SIP debug window shows proper INCOMING/OUTGOING labels
3. **Call Not Connecting**: Ensure RTP session is established after 200 OK/ACK exchange
4. **UI Not Responding**: Verify IncomingCallWindow event handlers are properly connected

#### **Debug Validation Steps:**
1. **Registration Check**: Ensure SIP client is registered before incoming calls
2. **Console Monitoring**: Watch for complete debug sequence during call answer
3. **SIP Trace Analysis**: Verify proper SIP message flow in debug window
4. **Exception Handling**: Check for any exceptions in debug output with stack traces

#### **Required SIP Headers in 200 OK Response:**
- Via: (copied from INVITE)
- From: (copied from INVITE)  
- To: (with local tag added)
- Call-ID: (copied from INVITE)
- CSeq: (copied from INVITE)
- Contact: Local contact URI
- Content-Type: application/sdp
- Content-Length: SDP body length
- SDP Body: Local media capabilities and RTP port

### Testing Incoming Call Functionality
1. **Setup**: Register SIP client with test credentials
2. **Initiate**: Make call from extension 101 to 103
3. **Monitor**: Watch both debug windows and console output
4. **Answer**: Press Answer button and verify 200 OK response
5. **Validate**: Confirm call establishment and audio connectivity

## Debug Windows System
The application provides two separate debug windows that can be used independently and concurrently:

### 📋 Application Debug Window (LoggingWindow)
- **Purpose**: Real-time application logging and diagnostics
- **Access**: Settings → Debug Tools → "Enable detailed logging" checkbox
- **Features**: 
  - Live application logs with filtering (Debug, Info, Warning, Error)
  - Real-time statistics and log distribution
  - Export functionality for logs
  - Independent, non-modal window that persists after settings close
  - Window position automatically offsets to avoid overlap with main window
- **Use Case**: Monitor general application behavior, UI interactions, and system events

### 📞 SIP Debug Window (SipMessagesWindow)
- **Purpose**: SIP protocol message debugging with ladder view
- **Access**: Settings → Debug Tools → "Open SIP Messages" button
- **Features**:
  - SIP message ladder format display showing INCOMING/OUTGOING messages
  - Real-time SIP protocol communication monitoring
  - Message filtering and analysis capabilities
  - RFC 3261 compliant SIP debugging
  - Connection status indicator showing registration state
  - Independent, non-modal window that persists after settings close
  - Automatically receives real SIP messages from SipPhoneService
- **Use Case**: Debug SIP registration, call setup, authentication, and protocol issues

### Debug Window Technical Implementation
- **Non-Modal Architecture**: Both windows use `Owner = null` to ensure they don't block the main window
- **Event-Driven Updates**: SIP debug window connects to `SipPhoneService.MessageReceived` event for real-time message capture
- **Lifecycle Management**: 
  - Application Debug window managed by MainWindow
  - SIP Debug window managed by SettingsWindow but persists independently
- **Window Positioning**: Automatic positioning to prevent overlap and provide optimal debugging workflow
- **Thread Safety**: All UI updates use `Dispatcher.BeginInvoke()` for cross-thread safety

### Debug Window Requirements
- Both windows MUST work independently and concurrently
- Both windows MUST be non-modal (allow main window interaction)
- Both windows MUST persist after settings window is closed
- Users should be able to monitor both application and SIP activity simultaneously
- Windows should be positioned to avoid overlap for optimal debugging workflow
- SIP debug window MUST receive and display real SIP messages from the service layer
- Both windows should handle window closure gracefully and be reopenable

## Test File Management
- Always create test files in a separate `tmp/` folder
- Use descriptive naming for test files (e.g., `tmp/sip/SipFunctionalityTest.cs`)
- Clean up test files after successful completion.
- Never leave temporary test files in the project root
- Organize tests by functionality: `Tests/Sip/`, `Tests/Audio/`, `Tests/UI/`
- Use PowerShell Remove-Item commands for cleanup: `Remove-Item $testFile -Force`
- Keep only permanent test infrastructure (README.md, run-tests.ps1)
- Always show cleanup in action when demonstrating tests
- Use proper C# unit testing framework (NUnit or xUnit) for production tests

## Documentation Organization
- All improvement documents, technical summaries, implementation reports, and project guides should be placed in the `Documents/` folder
- **Bug tracking and issue management**: Use GitHub Issues exclusively (no local tracking files)
- This includes files like:
  - Implementation summaries and reports (e.g., `ACK_FIX_IMPLEMENTATION_REPORT.md`)
  - Audio enhancement documentation (e.g., `AUDIO_ENHANCEMENTS.md`, `VOICE_CLIPPING_FIX.md`)
  - UI/UX improvement plans (e.g., `UI_REORGANIZATION_PLAN.md`, `THEME_SWITCHING_COMPLETION.md`)
  - Testing guides and status reports (e.g., `TESTING_GUIDE.md`, `TESTING-STATUS-REPORT.md`)
  - Project roadmaps and task lists (e.g., `PROJECT_ROADMAP.md`, `TASK_LIST.md`)
  - Quick reference materials (e.g., `QUICK_START_GUIDE.md`, `QUICK_REFERENCE_GUIDE.md`)
  - Migration and workflow documentation (e.g., `GITHUB_ISSUES_MIGRATION.md`)
- Keep only the main `README.md` in the project root
- When creating new improvement documentation, always place it in the `Documents/` folder

## Styling

- Modern Flat UI Design

## Settings UI Guidelines
- All settings pages MUST have consistent colored header bars at the top
- Each settings page should use a Grid layout with header row and content row
- Header format requirements:
  - Border with colored background and white text
  - Padding: 20,15,20,15
  - Main title: FontSize=20, FontWeight=Bold, White foreground
  - Subtitle: FontSize=12, lighter colored foreground, Margin=0,3,0,0
- Standard color scheme:
  - SIP Settings: Blue #3498DB with subtitle #D6EAF8
  - Audio Settings: Orange #E67E22 with subtitle #F8E6D3
  - App Settings: Purple #9B59B6 with subtitle #EBDEF0
  - Debug Tools: Blue #3498DB with subtitle #D5F4FF
- NEVER use duplicate headers - only one header per settings page
- Settings window should NOT have a main content header (ContentTitle)
- Individual page headers should include emoji icons and descriptive subtitles

## Project Management & Issue Tracking

### 🐛 **GitHub Issues - Single Source of Truth**
- **ALL bugs and features** are tracked exclusively in GitHub Issues
- **NO local bug tracking files** - GitHub Issues is the only system used
- **Automatic workflow** - Issues close when PRs with "Fixes #X" are merged
- **Professional tracking** with proper labels, priorities, and project management

### 📊 **Issue Labels & Organization**
- **Priority Labels**: `priority:critical`, `priority:high`, `priority:medium`, `priority:low`  
- **Category Labels**: `ui`, `sip`, `audio`, `network`, `rtp`, `configuration`, `registration`
- **Type Labels**: `bug`, `enhancement`, `feature`, `documentation`

### 🔍 **Finding Issues to Work On**
```powershell
# List all open issues
gh issue list --state open

# List issues by priority
gh issue list --label "priority:critical" --state open
gh issue list --label "priority:high" --state open

# List issues by category  
gh issue list --label "sip" --state open
gh issue list --label "audio" --state open
```

### 🚀 **GitHub Issues Best Practices**

#### **Creating New Issues**

**IMPORTANT**: Before creating a new BUG or IMP issue, always check the highest existing issue number (both open and closed) to ensure proper sequential numbering.

```powershell
# First, check all existing issues to find the highest number
gh issue list --state all --limit 100 | head -20  # Check recent issues
gh issue list --state all --search "BUG-" --limit 50  # Check all BUG issues
gh issue list --state all --search "IMP-" --limit 50  # Check all IMP issues

# Create a bug report (use next available BUG-XXX number)
gh issue create --title "BUG-XXX: Descriptive Title" --label "bug,priority:medium,category" --body "Description with reproduction steps"

# Create an improvement/feature request (use next available IMP-XXX number)  
gh issue create --title "IMP-XXX: Feature Name" --label "enhancement,priority:low" --body "Feature description and requirements"

# Alternative: Create a general feature request
gh issue create --title "FEATURE: New Feature Name" --label "enhancement,priority:low" --body "Feature description and requirements"
```

**Numbering Guidelines:**
- **BUG-XXX**: For bug reports, use sequential numbering (e.g., BUG-001, BUG-002, etc.)
- **IMP-XXX**: For improvements/enhancements, use sequential numbering (e.g., IMP-001, IMP-002, etc.)
- **Check both open AND closed issues** to avoid number conflicts
- Always use 3-digit padding (001, 002, etc.) for consistency

#### **Working with Issues**
```powershell
# Start working on an issue (creates and switches to branch)
gh issue develop 15 --checkout

# View issue details
gh issue view 15

# Add comments to issues
gh issue comment 15 --body "Progress update or findings"

# Close issue manually if needed
gh issue close 15 --comment "Completed manually or duplicate"
```

#### **Issue Lifecycle**
1. **Created**: New issue opened with proper labels
2. **In Progress**: Developer creates branch with `gh issue develop X --checkout`
3. **Under Review**: Pull request created with `Fixes #X` in description
4. **Closed**: Automatically closed when PR is merged to main

#### **Commit Message Standards**
```
Fix BUG-015: Theme Switcher Not Functional

- Updated ThemeSwitcher component to properly handle theme changes
- Fixed state management issue in MainWindow
- Added proper event handlers for theme selection

Fixes #15
```

#### **Pull Request Standards**
- **Title**: Match the issue title format
- **Description**: Always include `Fixes #X` to auto-close the issue
- **Labels**: Apply same labels as the original issue
- **Review**: Request review before merging (never merge directly to main)

## Call History and SIP Header Parsing Standards

### Call History Display Requirements
The Call History UI must always display caller information in a specific, standardized format:

#### **Display Format Standards**
- **Upper Line**: Caller name (if available from SIP header), otherwise display the phone number
- **Lower Line**: Always display the actual phone number extracted from the SIP header
- **No Duplicate Information**: Upper and lower lines must never show the same value
- **Clean Formatting**: No raw SIP header text should be visible in the UI

#### **SIP Header Parsing Implementation**
Located in `Pages/DialerPage.xaml.cs` - `CallHistoryEntry` class:

##### **ExtractDisplayName Method**
- Handles formats: `"Alice" <sip:101@server.com>`, `Alice <sip:101@server.com>`
- Returns the display name portion or empty string if none exists
- Properly strips quotes and handles various SIP header formats

##### **ExtractNumberPart Method**  
- Extracts phone numbers from SIP URIs: `<sip:101@server.com>` → `101`
- Handles direct SIP URIs: `sip:101@server.com` → `101`
- Supports plain numbers and phone number formats
- Returns "Unknown Number" only when no number can be extracted

##### **ExtractNumberFromSipUri Helper Method**
- Consistent number extraction from `sip:user@domain` format
- Handles both prefixed and non-prefixed URIs

#### **SIP Client Integration**
Located in `SimpleSipClient.cs` - `ExtractCallerInfo` method:
- **Modified Approach**: Returns full SIP header content (minus "From:" prefix and tag parameters)
- **Preservation**: Maintains both display name and URI information for UI parsing
- **Format**: `"Display Name" <sip:user@domain>` instead of just name OR number

### Call Status Display Standards
The call status header above the dialpad must show clean, professional information:

#### **Display Format**
- **Direction Indicators**: 
  - `📞←` for incoming calls  
  - `📞→` for outgoing calls
- **Caller Information**: `Name (Number)` format when both available, otherwise just number
- **Status Messages**:
  - Outgoing: `📞→ Calling Alice (101)...`
  - Incoming: `📞← Answering Alice (101)...` 
  - Connected: `📞→ Connected to Alice (101)`
  - On Hold: `📞← On Hold: Alice (101)`

#### **Implementation Details**
Located in `Pages/DialerPage.xaml.cs`:
- **Direction Tracking**: `_isIncomingCall` boolean flag
- **StartCall Method**: Accepts direction parameter to set call type
- **CallStatusText Property**: Uses extraction methods for clean display
- **EndCall Method**: Resets direction flag on call termination

### Testing SIP Header Formats
The extraction methods handle these SIP header formats correctly:
- `"Alice" <sip:101@server.com>` → Display: "Alice", Number: "101" ✅
- `Alice <sip:101@server.com>` → Display: "Alice", Number: "101" ✅  
- `<sip:101@server.com>` → Display: "", Number: "101" ✅
- `sip:101@server.com` → Display: "", Number: "101" ✅
- `101@server.com` → Display: "", Number: "101" ✅
- `101` → Display: "", Number: "101" ✅
- `+1234567890` → Display: "", Number: "+1234567890" ✅

### Key Implementation Files
- **Call History Logic**: `Pages/DialerPage.xaml.cs` (CallHistoryEntry class)
- **SIP Header Processing**: `SimpleSipClient.cs` (ExtractCallerInfo method) 
- **Database Integration**: `CallHistoryService.cs` (AddCall, UpdateCall methods)
- **UI Templates**: `Pages/DialerPage.xaml` (Call History ListView)

## Window Interaction Standards (BUG-028 Resolution)

### Main Window Dragging Functionality
The main window with custom title bar must be fully draggable and interactive:

#### **User Experience Requirements**
- **Single Click + Drag**: Window moves around the screen smoothly
- **Double Click**: Toggles between maximized and normal window state  
- **Visual Feedback**: Standard cursor behavior during drag operations
- **Edge Cases**: Graceful handling when window is maximized or in special states

#### **Technical Implementation**
Located in `MainWindow.xaml` and `MainWindow.xaml.cs`:

**XAML Changes**:
- Added `MouseLeftButtonDown="TitleBar_MouseLeftButtonDown"` to title bar Border
- Added same event handler to title StackPanel for full title area coverage
- Maintains existing `WindowChrome.IsHitTestVisibleInChrome="True"` configuration

**Code-Behind Implementation**:
```csharp
private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.ButtonState == MouseButtonState.Pressed)
    {
        if (e.ClickCount == 2)
        {
            // Toggle maximize/restore on double-click
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }
        else if (e.ClickCount == 1)
        {
            try
            {
                this.DragMove();  // Standard WPF dragging
            }
            catch (InvalidOperationException)
            {
                // Handle edge cases where dragging is not possible
            }
        }
    }
}
```

#### **Key Design Principles**
- **Standard WPF Patterns**: Uses built-in `DragMove()` method for reliability
- **Exception Safety**: Handles `InvalidOperationException` for edge cases
- **User Expectations**: Double-click maximize/restore follows Windows conventions
- **Event Ordering**: Proper handling of single vs. double clicks
- **Custom Title Bar**: Works seamlessly with `WindowStyle="None"` configuration

#### **Testing Validation**
- ✅ Window drags smoothly across multiple monitors
- ✅ Double-click maximize/restore works correctly
- ✅ No interference with title bar buttons (minimize, maximize, close)
- ✅ Exception handling prevents crashes in edge cases
- ✅ Maintains all existing window functionality

#### **Implementation Files**
- **XAML Layout**: `MainWindow.xaml` (title bar event binding)
- **Event Handling**: `MainWindow.xaml.cs` (TitleBar_MouseLeftButtonDown method)
- **Documentation**: `BUG-028-WINDOW-DRAGGING-FIX.md` (comprehensive fix details)
