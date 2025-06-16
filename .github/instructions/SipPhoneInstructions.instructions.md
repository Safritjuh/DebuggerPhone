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
   

## Git Branch Workflow
### Bug Fixes
- When starting work on a bug, create a new branch with the BUG name from the bug list
- Branch naming format: `bug/BUG-XXX` (e.g., `bug/BUG-013`, `bug/BUG-007`)
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
- This includes files like:
  - Implementation summaries and reports (e.g., `ACK_FIX_IMPLEMENTATION_REPORT.md`)
  - Audio enhancement documentation (e.g., `AUDIO_ENHANCEMENTS.md`, `VOICE_CLIPPING_FIX.md`)
  - UI/UX improvement plans (e.g., `UI_REORGANIZATION_PLAN.md`, `THEME_SWITCHING_COMPLETION.md`)
  - Testing guides and status reports (e.g., `TESTING_GUIDE.md`, `TESTING-STATUS-REPORT.md`)
  - Project roadmaps and task lists (e.g., `PROJECT_ROADMAP.md`, `TASK_LIST.md`)
  - Quick reference materials (e.g., `QUICK_START_GUIDE.md`, `QUICK_REFERENCE_GUIDE.md`)
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

### GitHub Issue & Pull Request Workflow
- **ALWAYS work on GitHub issues** - link branches to issues using `gh issue develop <issue-number> --checkout`
- **ALWAYS create pull requests** - never merge directly to main branch
- **GitHub issues are the single source of truth** for bug tracking and project management
- **Required commit message format** with issue references:
  ```
  Fix BUG-XXX: Brief description of the fix
  
  - Detailed change 1
  - Detailed change 2
  - Closes issue automatically when PR is merged
  
  Fixes #<issue-number>
  ```
- **Pull request creation** after pushing branch:
  ```powershell
  # After committing and pushing your branch
  gh pr create --title "Fix BUG-XXX: Brief Description" --body "Fixes #<issue-number>
  ## Summary
  Brief description of changes

  ## Changes Made
  - List of specific changes
  - GitHub issue will be closed automatically when PR is merged

  ## Testing
  - Build verification
  - Functional testing results"
  ```
- **Issue management**:
  - Use `gh issue develop <number> --checkout` to start work
  - Include "Fixes #X" in commit messages for automatic issue closure
  - GitHub issues are automatically closed when PRs with "Fixes #X" are merged
  - Let maintainers merge PRs and clean up branches
