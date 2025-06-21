# Project Structure Cleanup and Organization - Issue #68

## 🎯 Objective
Refactor and reorganize the project structure to improve maintainability, reliability, and performance by creating logical folder hierarchies and consolidating related functionality.

## 📊 Current State Analysis

### Current Root Directory Files (Scattered):
```
App.xaml/.cs                    # Application entry point
CallHistoryService.cs           # Service (should be in Services/)
EnhancedRingtoneService.cs      # Service (should be in Services/)
IRingtoneService.cs            # Interface (should be in Services/)
RingtoneService.cs             # Service (should be in Services/)
IncomingCallWindow.xaml/.cs     # Window (should be in UI/Windows/)
LoggingWindow.xaml/.cs          # Window (should be in UI/Windows/)
MainWindow.xaml/.cs             # Window (should be in UI/Windows/)
SettingsWindow.xaml/.cs         # Window (should be in UI/Windows/)
SipAccountDialog.xaml/.cs       # Dialog (should be in UI/Dialogs/)
SipMessageDetailsWindow.xaml/.cs # Window (should be in UI/Windows/)
SipMessagesWindow.xaml/.cs      # Window (should be in UI/Windows/)
SpeedDialConfigWindow.xaml/.cs  # Window (should be in UI/Windows/)
SimpleSipClient.cs              # Core logic (should be in Core/)
SipPhoneService.cs              # Service (should be in Services/)
SipDigestAuth.cs                # SIP utility (should be in Communication/Sip/)
SipTransport.cs                 # SIP transport (should be in Communication/Sip/)
RtpAudioManager.cs              # Audio (should be in Communication/Audio/)
SipSorceryAudioManager.cs       # Audio (should be in Communication/Audio/)
SdpManager.cs                   # SIP protocol (should be in Communication/Sip/)
QuickDbCheck.cs                 # Utility (should be in Utils/ or Services/Data/)
```

### Current Folder Analysis:
- **Services/** (2 files) - Underutilized, should contain all services
- **SipCore/** (6 files) - Good concept, but should be expanded and reorganized
- **Models/** (4 files) - Good, but could be organized by domain
- **Utils/** (4 files) - Good concept, could be expanded
- **Controls/** - Good UI organization
- **Pages/** - Good UI organization  
- **Converters/** - Good UI organization
- **Commands/** - Good organization
- **Themes/** - Good organization

## 🏗️ Proposed New Structure

```
DebuggerPhone/
├── App.xaml/.cs                           # Keep in root (Application entry)
├── WindowsSipPhone.csproj                 # Keep in root (Project file)
├── README.md, LICENSE, etc.               # Keep in root (Project metadata)
│
├── Core/                                  # Core application logic
│   ├── Application/
│   │   ├── SimpleSipClient.cs             # Main SIP client logic
│   │   └── ApplicationCore.cs             # Core app management
│   └── Interfaces/                        # Core interfaces
│       ├── ISipClient.cs
│       └── IApplicationCore.cs
│
├── Communication/                         # All communication-related code
│   ├── Sip/                              # SIP protocol implementation
│   │   ├── Core/
│   │   │   ├── SipDialog.cs              # From SipCore/
│   │   │   ├── SipMessageFactory.cs      # From SipCore/
│   │   │   ├── DialogManager.cs          # From SipCore/
│   │   │   └── RegistrationManager.cs    # From SipCore/
│   │   ├── Auth/
│   │   │   └── SipDigestAuth.cs          # From root
│   │   ├── Transport/
│   │   │   └── SipTransport.cs           # From root
│   │   ├── Protocol/
│   │   │   └── SdpManager.cs             # From root
│   │   └── Profiles/
│   │       ├── SipProfile.cs             # From SipCore/
│   │       └── SipProfileManager.cs      # From SipCore/
│   │
│   ├── Audio/                            # Audio processing
│   │   ├── Managers/
│   │   │   ├── RtpAudioManager.cs        # From root
│   │   │   └── SipSorceryAudioManager.cs # From root
│   │   └── Processing/
│   │       # Future audio processing classes
│   │
│   └── Network/                          # Network utilities
│       # Future network-related classes
│
├── Services/                             # Application services
│   ├── Audio/
│   │   ├── IRingtoneService.cs           # From root
│   │   ├── RingtoneService.cs            # From root
│   │   └── EnhancedRingtoneService.cs    # From root
│   ├── Data/
│   │   ├── CallHistoryService.cs         # From root
│   │   └── DatabaseService.cs            # From QuickDbCheck.cs refactor
│   ├── Communication/
│   │   └── SipPhoneService.cs            # From root
│   ├── Logging/
│   │   └── ApplicationLogger.cs          # From Services/
│   ├── Configuration/
│   │   └── ConfigurationService.cs       # New service for settings
│   └── System/
│       └── KeyboardShortcutService.cs    # From Services/
│
├── UI/                                   # User Interface components
│   ├── Windows/                          # Main application windows
│   │   ├── MainWindow.xaml/.cs           # From root
│   │   ├── IncomingCallWindow.xaml/.cs   # From root
│   │   ├── LoggingWindow.xaml/.cs        # From root
│   │   ├── SettingsWindow.xaml/.cs       # From root
│   │   ├── SipMessagesWindow.xaml/.cs    # From root
│   │   ├── SipMessageDetailsWindow.xaml/.cs # From root
│   │   └── SpeedDialConfigWindow.xaml/.cs # From root
│   ├── Dialogs/                          # Dialog windows
│   │   └── SipAccountDialog.xaml/.cs     # From root
│   ├── Pages/                            # Existing Pages/ folder
│   │   ├── DialerPage.xaml/.cs
│   │   ├── AudioSettingsPage.xaml/.cs
│   │   ├── DiagnosticsPage.xaml/.cs
│   │   └── SipSettingsPage.xaml/.cs
│   ├── Controls/                         # Existing Controls/ folder
│   │   ├── AudioDeviceHealthControl.xaml/.cs
│   │   ├── AudioLevelMeter.xaml/.cs
│   │   ├── AudioQualityControl.xaml/.cs
│   │   ├── ErrorMessageControl.xaml/.cs
│   │   ├── NetworkDiagnosticsControl.xaml/.cs
│   │   ├── SipServerHealthControl.xaml/.cs
│   │   └── ThemeToggleControl.xaml/.cs
│   ├── Converters/                       # Existing Converters/ folder
│   │   ├── BooleanToVisibilityConverter.cs
│   │   └── StringToVisibilityConverter.cs
│   ├── Commands/                         # Existing Commands/ folder
│   │   └── RelayCommand.cs
│   └── Resources/                        # UI resources
│       ├── Themes/                       # Existing Themes/ folder moved here
│       │   └── ThemeManager.cs
│       ├── Icons/                        # Existing Icons/ folder moved here
│       └── Styles/                       # Future styles
│
├── Models/                               # Data models organized by domain
│   ├── Configuration/
│   │   ├── AudioConfiguration.cs         # From Models/
│   │   └── SipConfiguration.cs           # From Models/
│   ├── Communication/
│   │   └── SipProfile.cs                 # From Models/ (consolidate with SipCore version)
│   ├── Logging/
│   │   └── LogEntry.cs                   # From Models/
│   └── CallHistory/
│       # Future call history models
│
├── Infrastructure/                       # Low-level infrastructure
│   ├── Data/
│   │   ├── DatabaseContext.cs            # New for data access
│   │   └── Repositories/                 # Future repository pattern
│   ├── Configuration/
│   │   ├── IniFileHandler.cs             # From Utils/
│   │   └── ProfileManager.cs             # From Utils/
│   └── Diagnostics/
│       ├── ApplicationTracker.cs         # From Utils/
│       └── DiagnosticReportGenerator.cs  # From Utils/
│
├── Shared/                               # Shared utilities and helpers
│   ├── Extensions/                       # Extension methods
│   ├── Helpers/                          # Helper classes
│   └── Constants/                        # Application constants
│
└── Tests/                                # Test projects
    ├── WindowsSipPhone.Tests/            # Existing test project
    ├── Core.Tests/                       # Core logic tests
    ├── Services.Tests/                   # Service tests
    └── UI.Tests/                         # UI tests
```

## 🔧 Implementation Plan

### Phase 1: Core Infrastructure Setup
1. **Create new folder structure**
2. **Move core application files**
3. **Update namespaces for moved files**
4. **Update project references**

### Phase 2: Communication Layer Reorganization
1. **Move SIP-related classes to Communication/Sip/**
2. **Move audio classes to Communication/Audio/**
3. **Consolidate duplicate SipProfile classes**
4. **Update namespaces and references**

### Phase 3: Services Reorganization
1. **Move service classes to appropriate Services/ subfolders**
2. **Create service interfaces where missing**
3. **Implement dependency injection patterns**
4. **Update service registrations**

### Phase 4: UI Structure Enhancement
1. **Move windows and dialogs to UI/ subfolders**
2. **Reorganize existing UI folders under UI/**
3. **Move Themes/ and Icons/ under UI/Resources/**
4. **Update XAML references**

### Phase 5: Models and Infrastructure
1. **Organize models by domain**
2. **Move utilities to Infrastructure/**
3. **Create shared helpers and extensions**
4. **Update all references**

### Phase 6: Cleanup and Optimization
1. **Remove duplicate files**
2. **Merge related small files**
3. **Split large multi-responsibility files**
4. **Optimize using statements**
5. **Update documentation**

## 🔄 Implementation Progress - Updated June 21, 2025

### ✅ COMPLETED - Phase 1 (Initial Structure Creation & Key Services)
- [x] Created new folder structure (Core, Communication, Services, UI, Infrastructure)
- [x] Moved key service files to Services/ subfolders
- [x] Moved core SIP communication files to Communication/ structure
- [x] Moved SimpleSipClient to Core/Application/
- [x] Initial commit and branch setup

### ✅ COMPLETED - Phase 2 (Major UI and Structure Reorganization)
- [x] Moved all main windows to UI/Windows/ (MainWindow, SettingsWindow, LoggingWindow, IncomingCallWindow, SipMessagesWindow)
- [x] Moved all dialogs to UI/Dialogs/ (SipAccountDialog, SipMessageDetailsWindow, SpeedDialConfigWindow)
- [x] Moved all pages to UI/Pages/ (AudioSettingsPage, DiagnosticsPage, DialerPage, SipSettingsPage)
- [x] Moved all controls to UI/Controls/ (14 different controls)
- [x] Moved themes to UI/Themes/ (DarkTheme, LightTheme)
- [x] Moved converters to UI/Converters/ (BooleanToVisibility, StringToVisibility)
- [x] Moved all models to Core/Models/ (AudioConfiguration, LogEntry, SipConfiguration, SipProfile)
- [x] Moved utilities to Core/Utilities/ (ApplicationTracker, IniFileHandler, RelayCommand)
- [x] Moved remaining SipCore files to Communication/Sip/Core/ (DialogManager, RegistrationManager, SipDialog)
- [x] Moved SipMessageFactory to Communication/Sip/Protocol/
- [x] Moved utility services to appropriate Services/ subfolders
- [x] Created Infrastructure/ structure and moved Icons, Profiles
- [x] Removed duplicate empty SipProfile.cs
- [x] Archived old IncomingCallWindow variants in UI/Windows/Archive/

### 🚧 IN PROGRESS - Phase 3 (Namespace and Reference Updates)
- [ ] Update all namespace declarations to match new folder structure
- [ ] Update all using statements throughout the project
- [ ] Update XAML references and resource paths
- [ ] Update project file references

### 📋 PENDING - Phase 4 (Final Cleanup and Optimization)
- [ ] Remove empty folders (Commands/, Controls/, Converters/, Models/, Pages/, Themes/, Utils/, SipCore/)
- [ ] Merge or split files as needed for better maintainability
- [ ] Create missing service interfaces
- [ ] Implement dependency injection patterns
- [ ] Validate that application builds and runs correctly
- [ ] Update developer documentation

## 📊 Current Project Structure Status

### ✅ New Well-Organized Structure:
```
Core/
├── Application/          # SimpleSipClient.cs
├── Models/              # AudioConfiguration, LogEntry, SipConfiguration, SipProfile
└── Utilities/           # ApplicationTracker, IniFileHandler, RelayCommand

Communication/
├── Sip/
│   ├── Core/           # DialogManager, RegistrationManager, SipDialog
│   ├── Auth/           # SipDigestAuth
│   ├── Transport/      # SipTransport
│   └── Protocol/       # SdpManager, SipMessageFactory
└── Audio/
    └── Managers/       # RtpAudioManager, SipSorceryAudioManager

Services/
├── Audio/              # IRingtoneService, RingtoneService, EnhancedRingtoneService
├── Data/               # CallHistoryService, ProfileManager, QuickDbCheck, SipProfileManager
├── Communication/      # SipPhoneService
├── Logging/           # ApplicationLogger
└── System/            # KeyboardShortcutService, DiagnosticReportGenerator

UI/
├── Windows/           # All main windows + Archive/ subfolder
├── Dialogs/           # All dialog windows
├── Pages/             # All settings/content pages
├── Controls/          # All reusable UI controls (14 files)
├── Converters/        # Value converters
└── Themes/            # Dark and Light themes

Infrastructure/
├── Configuration/
│   └── Profiles/      # SIP provider profiles
└── Resources/
    └── Icons/         # Application icons
```

### 📈 Progress Summary:
- **68 files successfully moved** and organized into logical structure
- **Major UI reorganization completed** - all windows, dialogs, pages, controls properly categorized
- **Core foundation established** - models, utilities, and application core organized
- **Communication layer structured** - SIP and audio components properly separated
- **Services properly categorized** - by functionality (Audio, Data, Communication, Logging, System)
- **Infrastructure foundation created** - for configuration and resources
- **Version history preserved** - all moves done via git mv

### 🎯 Next Steps:
The major structural reorganization is complete. The next critical phase is updating all namespace declarations and references to match the new structure, followed by validation that the application builds and runs correctly.

---

**Created**: June 21, 2025
**Issue**: #68
**Branch**: `68-improvement-project-structure-cleanup`
**Priority**: Enhancement
**Estimated Effort**: High (8-12 hours over multiple sessions)
