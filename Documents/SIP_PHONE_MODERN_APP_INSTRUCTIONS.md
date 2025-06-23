# SIP Phone Modern Windows 11 Application - Complete Instructions

## Overview
This document provides comprehensive instructions for building a modern, user-friendly SIP phone application for Windows 11. The goal is to deliver a polished, reliable, and easy-to-use desktop app with a beautiful UI, robust SIP/RTP support, and seamless integration with Windows 11 features.

---

## 1. Technology Stack
- **Language:** C# (.NET 8.0 or later)
- **UI Framework:** WPF (Windows Presentation Foundation)
- **SIP Library:** SIPSorcery (RFC 3261 compliant)
- **Audio Library:** NAudio (for RTP and device handling)
- **Target OS:** Windows 11 (optimized for modern look & feel)
- **Build:** Single executable, no backend server

---

## 2. User Experience & UI Design
- **Modern Flat UI:** Use clean, flat design with accent colors and rounded corners
- **Dark & Light Themes:** Support for both, with easy toggle
- **Touch & Mouse Friendly:** Large buttons, clear icons, responsive layouts
- **Accessibility:** High contrast, keyboard navigation, screen reader support
- **Window Chrome:** Custom title bar, draggable, resizable, with minimize/maximize/close
- **System Tray Integration:** Minimize to tray, notifications for calls/messages

---

## 3. Core Features
- **SIP Registration:** Easy configuration, clear status display (Registered, Registering, Not Registered, Not Configured)
- **Call Handling:**
  - Make/receive calls (audio only)
  - Answer, decline, hang up
  - Call timer, call status, DTMF keypad
  - Call history with search/filter
- **Audio:**
  - Device selection (input/output)
  - Mute/unmute, volume control
  - Ringtone and call progress tones
- **Debugging:**
  - SIP message ladder view
  - Application log window
  - Export logs/messages
- **Settings:**
  - SIP account, audio, app preferences
  - Modern settings UI with colored headers

---

## 4. Implementation Guidelines
- **Use SIPSorcery for all SIP protocol logic** (registration, INVITE, BYE, etc.)
- **Use NAudio for RTP and device management**
- **Follow MVVM pattern** for WPF (separate UI, logic, and data)
- **All UI updates must be thread-safe** (use Dispatcher)
- **Never block the UI thread** (use async/await)
- **All SIP and RTP errors must be logged and shown in debug window**
- **All user actions must have clear feedback** (toasts, status bar, etc.)
- **No operational or debug messages in the main status bar** (only registration status)
- **All settings must be persisted between sessions**
- **Use PowerShell for all build/test scripts**

---

## 5. UI/UX Standards
- **Main Window:**
  - Status bar (registration only)
  - Dialpad with large buttons
  - Call status header (direction, name/number, status)
  - Call history (clean, two-line format)
  - Modern icons and color scheme
- **Incoming Call Popup:**
  - Clear caller info, answer/decline buttons
  - Call timer, ringtone
- **Settings:**
  - Colored header bars, grid layout
  - Grouped sections (SIP, Audio, App, Debug)
- **Debug Windows:**
  - Application log (filterable)
  - SIP ladder (real-time, RFC 3261 compliant)

---

## 6. SIP & Audio Requirements
- **SIP:**
  - Full RFC 3261 compliance
  - TCP/UDP transport (no WebSocket)
  - Proper SIP header parsing (From, To, Contact, etc.)
  - Clean call flow: INVITE, 180 Ringing, 200 OK, ACK, BYE
  - Handle registration, authentication, NAT
- **Audio:**
  - Use NAudio for device enumeration and RTP
  - Support for mute, volume, device switching
  - Play ringtone on incoming call

---

## 7. Testing & Debugging
- **Always test with real SIP credentials** (see project instructions)
- **Use Visual Studio debugger for step-through**
- **Monitor all SIP and RTP flows in debug windows**
- **Export logs for troubleshooting**
- **Automated tests in `tmp/` folder, never in project root**

---

## 8. Project & Git Workflow
- **Always use feature/bug branches** (never commit to main)
- **Use GitHub Issues for all tracking** (no local bug files)
- **Follow branch naming and PR standards** (see project instructions)
- **All improvements must be documented in `Documents/`**

---

## 9. Documentation & Support
- **All technical docs, guides, and reports go in `Documents/`**
- **README.md in root for quick start**
- **Use clear, friendly language in all docs**

---

## 10. Final Product Checklist
- [ ] Modern, beautiful Windows 11 UI
- [ ] Easy SIP account setup
- [ ] Reliable call handling (make, receive, hang up)
- [ ] Clean call history and status
- [ ] Debug windows for SIP and app logs
- [ ] All settings persist between sessions
- [ ] No backend server required
- [ ] Single .exe deployment
- [ ] Fully tested on Windows 11

---

**Let's build the best SIP phone for Windows 11!**
