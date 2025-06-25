## Summary

Fixes the profile name format mismatch issue where switching to a profile (e.g., 'Avaya IP Office') and attempting to register would fail with 'Profile 'Avaya_IP_Office' not found'.

## Root Cause

The `EnhancedProfileManager.GetAvailableProfiles()` method was returning profile names based on filenames (e.g., 'Avaya_IP_Office' from 'Avaya_IP_Office.ini'), but the registration logic was looking up profiles using `SipProfile.GetPredefinedProfile()` which searches by the actual profile names defined in the INI files (e.g., 'Avaya IP Office' from Name= field).

## Solution

**Before:**
- `GetAvailableProfiles()` returned filenames: `['Avaya_IP_Office', 'Avaya_Aura', ...]`
- `GetPredefinedProfile()` searched by INI names: `['Avaya IP Office', 'Avaya Aura', ...]`
- ❌ Mismatch caused registration failures

**After:**
- `GetAvailableProfiles()` now reads actual profile names from INI files
- Both methods use consistent naming: `['Avaya IP Office', 'Avaya Aura', ...]`
- ✅ Profile selection and registration now work correctly

## Changes Made

### Core/Managers/EnhancedProfileManager.cs
- ✅ Modified `GetAvailableProfiles()` to read actual profile names from INI files using `IniFileHandler`
- ✅ Added comprehensive error handling for corrupted or missing profile sections  
- ✅ Added fallback to filename if profile name cannot be read
- ✅ Added proper logging for debugging profile loading issues

## Testing

- ✅ Build succeeds without errors
- ✅ All profile files verified to have correct Name= entries:
  - `Avaya_IP_Office.ini` → `Name=Avaya IP Office`
  - `Avaya_Aura.ini` → `Name=Avaya Aura`
  - `Elevate.ini` → `Name=Elevate`
  - `Generic.ini` → `Name=Generic`

## Impact

- **High Priority Bug Fixed**: Users can now register with any profile after switching
- **Consistent UX**: Profile selection dropdown shows the same names used internally
- **Robust Error Handling**: Graceful fallback for corrupted profile files
- **No Breaking Changes**: Existing functionality preserved

Fixes #93
