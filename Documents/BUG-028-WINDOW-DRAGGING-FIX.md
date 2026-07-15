# BUG-028 Fix: Main Window Dragging Functionality

## Problem
The main window was not movable/draggable when using the custom title bar. Users could not drag the window to move it around the screen.

## Root Cause
The application uses a custom title bar with `WindowStyle="None"` and `WindowChrome`, but there was no mouse event handling to enable window dragging when clicking on the title bar area.

## Solution
Added mouse event handling to the title bar to enable window dragging:

### 1. XAML Changes (MainWindow.xaml)
- Added `MouseLeftButtonDown="TitleBar_MouseLeftButtonDown"` to the title bar Border element
- Added the same event handler to the title/icon StackPanel to ensure the entire title area is draggable

### 2. Code-Behind Changes (MainWindow.xaml.cs)
- Added `TitleBar_MouseLeftButtonDown` event handler method
- Implemented `DragMove()` functionality for single clicks
- Added double-click handling to maximize/restore the window
- Included proper exception handling for cases where `DragMove()` cannot be called (e.g., when maximized)

## Key Features
- **Single Click + Drag**: Moves the window around the screen
- **Double Click**: Toggles between maximized and normal window state
- **Exception Handling**: Gracefully handles scenarios where dragging is not possible
- **Clean Implementation**: Uses standard WPF `DragMove()` method for reliable functionality

## Code Changes
```csharp
private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.ButtonState == MouseButtonState.Pressed)
    {
        if (e.ClickCount == 2)
        {
            // Toggle between maximized and normal state
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }
        else if (e.ClickCount == 1)
        {
            try
            {
                this.DragMove();
            }
            catch (InvalidOperationException)
            {
                // Handle cases where dragging is not possible
            }
        }
    }
}
```

## Testing
- Tested application startup and compilation
- Verified window can be dragged by clicking and dragging the title bar
- Confirmed double-click maximize/restore functionality works
- Build succeeds without errors

## Result
✅ Main window is now fully draggable and movable
✅ Double-click to maximize/restore works correctly  
✅ No regression in existing functionality
✅ Clean, maintainable code implementation

This fix resolves BUG-028 completely.
