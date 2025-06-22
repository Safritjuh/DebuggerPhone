# SIP Profile Configuration Guide

## Overview
The SIP phone application uses profile configurations stored in INI files located in the `profiles/` directory. These profiles define connection settings, protocol parameters, and codec preferences for different SIP providers.

## How It Works
- **Location**: All profile INI files are stored in the `profiles/` directory at the project root
- **Loading**: The application automatically detects and loads all `.ini` files from this directory
- **Live Editing**: You can edit any INI file and restart the application to see changes immediately
- **No Copying Required**: The application reads directly from the source `profiles/` directory, not from copied files

## Editing Profiles

### Quick Steps:
1. Navigate to the `profiles/` directory in your project
2. Open any `.ini` file in a text editor
3. Make your changes
4. Save the file
5. Restart the SIP phone application
6. Your changes will be reflected in the SIP Profile dropdown

### Example Edit:
To change the registration expiry time for the Generic profile:
1. Open `profiles/Generic.ini`
2. Find the line `RegistrationExpiry=300`
3. Change it to `RegistrationExpiry=600` (for 10 minutes)
4. Save and restart the app

## Available Profiles
- **Generic.ini**: Default RFC 3261 compliant settings
- **Avaya_IP_Office.ini**: Optimized for Avaya IP Office systems  
- **Avaya_Aura.ini**: Profile for Avaya Aura systems
- **Elevate.ini**: Intermedia Elevate platform settings
- **Custom_Provider_Example.ini**: Template for creating custom profiles

## Creating New Profiles
1. Copy an existing INI file (e.g., `Generic.ini`)
2. Rename it to match your provider (e.g., `MyProvider.ini`)
3. Edit the `Name` and `Description` fields
4. Modify settings as needed for your SIP provider
5. Save and restart the application

## Profile Structure
Each profile INI file contains these sections:
- **[Profile]**: Basic profile information
- **[Connection]**: Registration and transport settings
- **[Protocol]**: SIP protocol parameters
- **[Media]**: Audio codec preferences
- **[CustomHeaders]**: Provider-specific SIP headers

For detailed parameter descriptions, see `profiles/README.md`.

## Troubleshooting
- **Profile not appearing**: Check the INI file syntax and ensure it's in the `profiles/` directory
- **Changes not reflected**: Make sure you restarted the application after editing
- **Invalid settings**: Check the debug console for error messages about profile loading

## Debug Information
When the application starts, you can see debug messages in the console showing:
- Which directory profiles are loaded from
- How many INI files were found
- Which profiles were successfully loaded

This helps verify that your edits are being picked up correctly.
