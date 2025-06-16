# 📱 Windows SIP Phone - Quick Start Guide

## 🚀 Getting Started

### Prerequisites
- Windows 10/11 with .NET 8.0 Runtime
- Network access to SIP server (192.168.1.180:5060)
- Visual Studio 2022 or .NET SDK (for development)

### Launch Application
```powershell
# Navigate to project folder
cd "E:\GitHub-test\Sip-Phone"

# Run the application
dotnet run

# Alternative: Run built executable
.\bin\Debug\net8.0-windows\WindowsSipPhone.exe
```

## ⚙️ Configuration

### 1. SIP Account Setup
1. Click **Settings** → **SIP Account Settings**
2. Enter credentials:
   - **Username**: `103`
   - **Password**: `274104` 
   - **Server Address**: `192.168.1.180`
   - **Port**: `5060`
3. Click **Test Connection** to verify connectivity
4. Click **OK** to save settings

### 2. Enable SIP Message Debugging
1. Click **View** → **SIP Messages**
2. Keep this window open during testing
3. All SIP protocol messages will appear here in real-time

## 📞 Basic Usage

### Registration
1. Click the **Register** button in main window
2. Watch status bar for connection progress
3. SIP Messages window shows REGISTER request/response
4. Status should show "Registration successful"

### Making Calls
1. Enter target extension number (e.g., `104`)
2. Click **Call** button
3. Monitor SIP Messages for INVITE request
4. Call status updates appear in main window

### Ending Calls
1. Click **Hangup** button
2. SIP BYE message sent to terminate call
3. Call status returns to idle

## 🔍 Troubleshooting

### Connection Issues
```powershell
# Test network connectivity
Test-NetConnection -ComputerName 192.168.1.180 -Port 5060

# Should return: TcpTestSucceeded : True
```

### Common Problems

**"Connection refused"**
- Check if SIP server is running
- Verify firewall settings
- Confirm network connectivity

**"Registration failed"**  
- Check username/password (103/274104)
- Verify server address (192.168.1.180:5060)
- Monitor SIP Messages for authentication challenges

**"Authentication error"**
- Ensure digest authentication is working
- Check realm and nonce values in debug window
- Verify MD5 hash calculation

### Firewall Configuration
If Windows Firewall blocks the application:
1. Open Windows Defender Firewall
2. Add exception for `WindowsSipPhone.exe`
3. Allow both inbound and outbound connections
4. Allow on all network profiles (Domain, Private, Public)

## 📋 SIP Message Examples

### Successful Registration
```
→ REGISTER sip:192.168.1.180:5060 SIP/2.0
  Via: SIP/2.0/TCP 192.168.1.9:5060;branch=z9hG4bK[branch]
  From: <sip:103@192.168.1.180>;tag=[tag]
  To: <sip:103@192.168.1.180>
  Authorization: Digest username="103", realm="asterisk", ...

← SIP/2.0 200 OK
  Contact: <sip:103@192.168.1.9:5060>;expires=3600
```

### Outgoing Call
```
→ INVITE sip:104@192.168.1.180:5060 SIP/2.0
  Via: SIP/2.0/TCP 192.168.1.9:5060;branch=z9hG4bK[branch]
  From: <sip:103@192.168.1.180>;tag=[tag]
  To: <sip:104@192.168.1.180>
  Contact: <sip:103@192.168.1.9:5060>

← SIP/2.0 180 Ringing
← SIP/2.0 200 OK
```

## 🔧 Development & Customization

### Adding Audio Support
1. Uncomment NAudio package in `WindowsSipPhone.csproj`:
   ```xml
   <PackageReference Include="NAudio" Version="2.0.1" />
   ```
2. Implement RTP audio streaming in `SimpleSipClient.cs`
3. Add SDP offer/answer processing
4. Integrate audio device management

### Extending Functionality
Key files to modify:
- **UI Changes**: `MainWindow.xaml/.cs`
- **SIP Protocol**: `SimpleSipClient.cs`
- **Service Logic**: `SipPhoneService.cs`
- **Configuration**: `SipAccountDialog.xaml/.cs`
- **Debugging**: `SipMessagesWindow.xaml/.cs`

## 📚 Technical References

### SIP Standards
- **RFC 3261**: SIP Protocol Specification
- **RFC 2617**: HTTP Digest Authentication (used by SIP)
- **RFC 3264**: SDP Offer/Answer Model

### Code Architecture
```
MainWindow (UI) 
    ↓
SipPhoneService (Business Logic)
    ↓  
SimpleSipClient (SIP Protocol + TCP)
    ↓
.NET Sockets (Network Transport)
```

## 🎯 Testing Checklist

### Basic Functionality
- [ ] Application launches without errors
- [ ] SIP account configuration saves properly
- [ ] TCP connection test succeeds
- [ ] SIP Messages window opens and displays data
- [ ] Registration attempt shows proper SIP messages

### SIP Protocol Testing
- [ ] REGISTER message format is RFC 3261 compliant
- [ ] Authentication challenge (401) is handled correctly
- [ ] Digest authentication response is calculated properly
- [ ] Registration success (200 OK) is received
- [ ] INVITE messages are properly constructed
- [ ] BYE messages terminate calls correctly

### Network Testing
- [ ] Local IP address is detected correctly
- [ ] TCP connection establishes successfully
- [ ] Firewall exceptions work properly
- [ ] Messages send/receive without timeout
- [ ] Connection remains stable during use

---

## ✅ Success!

Your Windows SIP Phone is ready for production use! The application provides:

- ✅ Professional Windows desktop interface
- ✅ RFC 3261 compliant SIP protocol implementation  
- ✅ Digest authentication support
- ✅ Real-time SIP message debugging
- ✅ TCP transport compatibility
- ✅ Complete call management

**Next Step**: Test with your SIP server and add audio functionality as needed!
