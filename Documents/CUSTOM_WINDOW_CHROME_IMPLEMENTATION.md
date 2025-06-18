# Custom Window Chrome Implementation

## Overview
This document describes the implementation of custom window chrome for the Windows SIP Phone application to create a seamless title bar that blends with the modern flat UI.

## Implementation Details

### MainWindow.xaml Changes
- **WindowStyle**: Set to `None` to remove default Windows chrome
- **WindowChrome**: Configured with 32px caption height and 4px resize borders
- **Custom Title Bar**: Added as first row in main grid with app-consistent styling
- **Window Controls**: Custom minimize, maximize/restore, and close buttons

### Theme Integration
Both `LightTheme.xaml` and `DarkTheme.xaml` include new resources:
- `TitleBarBackgroundBrush`: Matches application background
- `TitleBarTextBrush`: Appropriate text color for title
- `TitleBarButtonHoverBrush`: Hover state for window control buttons
- `TitleBarCloseButtonHoverBrush`: Special red hover for close button

### Event Handling
MainWindow.xaml.cs includes:
- Window control button event handlers
- Automatic icon switching for maximize/restore button
- Resize border management for maximized state

## Key Features
- **Seamless Integration**: Title bar matches application theme colors
- **Full Functionality**: Maintains drag, resize, minimize, maximize, close
- **Theme Support**: Works with both Light and Dark themes
- **Windows Standards**: Uses Segoe MDL2 Assets icons
- **Accessibility**: Preserves tooltips and keyboard navigation

## Technical Notes
- Uses `WindowChrome.IsHitTestVisibleInChrome="True"` for button interactivity
- Title area allows window dragging through transparent background
- Resize borders automatically adjust based on window state
- Dynamic resource binding ensures theme changes apply immediately

## Result
The application now presents a modern, cohesive appearance with the title bar visually integrated into the main UI while preserving all standard window functionality.