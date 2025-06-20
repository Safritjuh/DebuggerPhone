## Feature Request: SIP Profile System

### Problem Statement
Currently, the SIP phone uses generic settings that may not work optimally with different SIP platforms (Avaya, FreeSWITCH, cloud providers, etc.). Different platforms have varying requirements for:
- Registration expiry times
- Keep-alive mechanisms  
- User-Agent strings
- Codec preferences
- Authentication methods
- Protocol quirks and timing

### Proposed Solution
Implement a SIP Profile system that allows users to select predefined profiles optimized for specific platforms, or create custom profiles.

### Key Features

#### 1. Predefined Profiles
- **Avaya IP Office**: Optimized for Avaya systems (3600s expiry, no keep-alive, G.711 preference)
- **Cloud Generic**: For cloud SIP providers (300s expiry, keep-alive enabled, G.722/G.711 codecs)  
- **FreeSWITCH**: Optimized for FreeSWITCH platforms (1800s expiry, flexible timers)
- **Cisco**: Cisco-specific optimizations
- **Generic**: Current default behavior

#### 2. Profile Settings
```csharp
public class SipProfile
{
    // Connection Settings
    public int RegistrationExpiry { get; set; } = 3600;
    public bool RequireKeepAlive { get; set; } = false;
    public int KeepAliveInterval { get; set; } = 30;
    public string Transport { get; set; } = "TCP";
    
    // Protocol Settings
    public string UserAgentString { get; set; } = "Windows-SIP-Phone/2.0";
    public bool UseShortHeaders { get; set; } = false;
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
    
    // Media Settings  
    public List<string> PreferredCodecs { get; set; } = new();
    public bool RequireSTUN { get; set; } = false;
    public string STUNServer { get; set; } = "";
    
    // Timing & Behavior
    public bool SendPreciseTimers { get; set; } = true;
    public int DefaultPort { get; set; } = 5060;
}
```

#### 3. UI Integration
- Add profile selection dropdown in SIP Settings
- Profile-specific help text and recommendations
- Custom profile creation/editing capability
- Import/export profile functionality

#### 4. Implementation Areas
- Modify SimpleSipClient to use profile settings
- Update SipMessageFactory for profile-specific message creation
- Enhance Settings UI with profile management
- Add profile persistence to configuration

### Benefits
1. **Better Compatibility**: Out-of-the-box support for major SIP platforms
2. **Reduced Configuration**: Users select a profile instead of tweaking individual settings
3. **Easier Troubleshooting**: Platform-specific optimizations reduce connectivity issues
4. **Professional Features**: Matches expectations of enterprise SIP clients
5. **Future-Proof**: Easy to add new platforms as needed

### Success Criteria
- [ ] Predefined profiles for at least 4 major platforms
- [ ] Profile selection integrated into Settings UI
- [ ] All current SIP functionality works with profile system
- [ ] Custom profile creation and management
- [ ] Profile settings properly applied to SIP messages and behavior
- [ ] Documentation and user guidance for profile selection

### Implementation Priority
Medium - This would significantly improve platform compatibility and user experience, but current basic functionality should be stabilized first.

### Related Issues
This would help resolve platform-specific connectivity issues and complement the current ACK bug fixes by providing platform-optimized settings.
