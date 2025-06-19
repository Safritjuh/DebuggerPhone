# SIP Profiles Configuration

This directory contains editable SIP profile configurations in INI format. You can modify existing profiles or create new ones by editing or adding INI files.

## Profile File Format

Each profile is stored as an INI file with the following sections and settings:

### [Profile] Section
- `Name`: Display name of the profile
- `Description`: Brief description of the profile's purpose
- `IsCustom`: Set to `true` for custom profiles, `false` for predefined ones

### [Connection] Section
- `RegistrationExpiry`: Registration expiry time in seconds (300-86400)
- `RequireKeepAlive`: Enable keep-alive packets (true/false)
- `KeepAliveInterval`: Keep-alive interval in seconds (1-300)
- `Transport`: SIP transport protocol (TCP, UDP, or TLS)
- `DefaultPort`: Default SIP port (typically 5060 for TCP/UDP, 5061 for TLS)

### [Protocol] Section
- `UserAgentString`: User-Agent header value
- `UseShortHeaders`: Use short SIP headers (true/false)
- `SendPreciseTimers`: Send precise timing values (true/false)

### [Media] Section
- `PreferredCodecs`: Comma-separated list of preferred codecs (e.g., "PCMU,PCMA,G722")
- `RequireSTUN`: Require STUN server for NAT traversal (true/false)
- `STUNServer`: STUN server address (if required)

### [CustomHeaders] Section
Custom SIP headers can be added using the format `Header_HeaderName=Value`:
```ini
[CustomHeaders]
Header_X-Custom-Client=MyClient
Header_X-Platform-Version=1.0
```

## Example Profile

Here's an example of a complete profile file:

```ini
[Profile]
Name=My Custom Profile
Description=Custom profile for my SIP provider
IsCustom=true

[Connection]
RegistrationExpiry=600
RequireKeepAlive=true
KeepAliveInterval=60
Transport=TCP
DefaultPort=5060

[Protocol]
UserAgentString=Windows-SIP-Phone/2.0 (Custom)
UseShortHeaders=false
SendPreciseTimers=true

[Media]
PreferredCodecs=G722,PCMU,PCMA
RequireSTUN=false
STUNServer=

[CustomHeaders]
Header_X-Client-Type=WindowsPhone
Header_X-Custom-Version=2.0
```

## Predefined Profiles

The following predefined profiles are available and can be customized:

- **Generic.ini**: Default RFC 3261 compliant settings
- **Avaya_IP_Office.ini**: Optimized for Avaya IP Office systems
- **Cloud_Generic.ini**: Optimized for cloud SIP providers
- **FreeSWITCH.ini**: Optimized for FreeSWITCH platforms
- **Cisco.ini**: Optimized for Cisco systems

## How to Create Custom Profiles

1. Copy one of the existing INI files as a starting point
2. Rename the file to match your new profile (e.g., `MyProvider.ini`)
3. Edit the `Name` and `Description` in the `[Profile]` section
4. Set `IsCustom=true` for custom profiles
5. Modify other settings as needed for your SIP provider
6. Save the file and restart the application

## Notes

- Changes to INI files take effect after restarting the application
- Invalid settings will cause the profile to be ignored
- Boolean values can be `true`/`false`, `yes`/`no`, or `1`/`0`
- Values containing spaces or special characters should be quoted
- Lines starting with `;` or `#` are treated as comments