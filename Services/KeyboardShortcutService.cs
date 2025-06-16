using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;

namespace WindowsSipPhone
{
    /// <summary>
    /// Service for managing keyboard shortcuts and global hotkeys
    /// Supports application-level shortcuts and system-wide global hotkeys
    /// </summary>
    public class KeyboardShortcutService : IDisposable
    {
        // Win32 API for global hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifier keys for global hotkeys
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // Virtual key codes
        private const uint VK_A = 0x41;
        private const uint VK_H = 0x48;

        // Hotkey IDs
        private const int HOTKEY_ANSWER = 1001;
        private const int HOTKEY_HANGUP = 1002;

        // Fields
        private readonly IntPtr _windowHandle;
        private readonly HwndSource _hwndSource;
        private bool _disposed = false;

        // Speed dial mappings (F1-F12 to phone numbers)
        private readonly Dictionary<Key, string> _speedDialMappings;        // Events - initialized to avoid CS8618 warnings
        public event EventHandler AnswerRequested = delegate { };
        public event EventHandler HangupRequested = delegate { };
        public event EventHandler MuteRequested = delegate { };
        public event EventHandler DtmfRequested = delegate { };
        public event EventHandler<string> SpeedDialRequested = delegate { };
        public event EventHandler<char> DtmfDigitRequested = delegate { };

        // Alternative event names for MainWindow compatibility
        public event EventHandler AnswerCallRequested
        {
            add { AnswerRequested += value; }
            remove { AnswerRequested -= value; }
        }

        public event EventHandler HangupCallRequested
        {
            add { HangupRequested += value; }
            remove { HangupRequested -= value; }
        }

        public event EventHandler MuteToggleRequested
        {
            add { MuteRequested += value; }
            remove { MuteRequested -= value; }
        }

        public event EventHandler ShowDtmfKeypadRequested
        {
            add { DtmfRequested += value; }
            remove { DtmfRequested -= value; }
        }

        /// <summary>
        /// Initialize keyboard shortcut service
        /// </summary>
        /// <param name="mainWindow">Main window for global hotkey registration</param>
        public KeyboardShortcutService(Window mainWindow)
        {
            if (mainWindow == null)
                throw new ArgumentNullException(nameof(mainWindow));

            // Get window handle for global hotkeys
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(mainWindow).Handle);
            _windowHandle = _hwndSource.Handle;

            // Hook into window message processing
            _hwndSource.AddHook(WndProc);

            // Initialize speed dial mappings with default numbers
            _speedDialMappings = new Dictionary<Key, string>
            {
                { Key.F1, "101" },
                { Key.F2, "102" },
                { Key.F3, "103" },
                { Key.F4, "104" },
                { Key.F5, "105" },
                { Key.F6, "106" },
                { Key.F7, "107" },
                { Key.F8, "108" },
                { Key.F9, "109" },
                { Key.F10, "110" },
                { Key.F11, "111" },
                { Key.F12, "112" }
            };

            // Register global hotkeys
            RegisterGlobalHotkeys();
        }        /// <summary>
        /// Get current speed dial mappings
        /// </summary>
        public Dictionary<Key, string> GetSpeedDialMappings()
        {
            return new Dictionary<Key, string>(_speedDialMappings);
        }

        /// <summary>
        /// Get all speed dial numbers as a dictionary with function key names as keys
        /// </summary>
        public Dictionary<string, string> GetAllSpeedDialNumbers()
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in _speedDialMappings)
            {
                result[kvp.Key.ToString()] = kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// Set speed dial number for a specific function key
        /// </summary>
        public void SetSpeedDialNumber(string keyName, string phoneNumber)
        {
            if (Enum.TryParse<Key>(keyName, out Key key) && _speedDialMappings.ContainsKey(key))
            {
                _speedDialMappings[key] = phoneNumber ?? "";
            }
        }

        /// <summary>
        /// Update speed dial mapping
        /// </summary>
        public void SetSpeedDialMapping(Key key, string phoneNumber)
        {
            if (_speedDialMappings.ContainsKey(key))
            {
                _speedDialMappings[key] = phoneNumber ?? "";
            }
        }

        /// <summary>
        /// Update multiple speed dial mappings
        /// </summary>
        public void SetSpeedDialMappings(Dictionary<Key, string> mappings)
        {
            if (mappings == null) return;

            foreach (var kvp in mappings)
            {
                if (_speedDialMappings.ContainsKey(kvp.Key))
                {
                    _speedDialMappings[kvp.Key] = kvp.Value ?? "";
                }
            }
        }

        /// <summary>
        /// Reset speed dial mappings to defaults
        /// </summary>
        public void ResetSpeedDialToDefaults()
        {
            _speedDialMappings[Key.F1] = "101";
            _speedDialMappings[Key.F2] = "102";
            _speedDialMappings[Key.F3] = "103";
            _speedDialMappings[Key.F4] = "104";
            _speedDialMappings[Key.F5] = "105";
            _speedDialMappings[Key.F6] = "106";
            _speedDialMappings[Key.F7] = "107";
            _speedDialMappings[Key.F8] = "108";
            _speedDialMappings[Key.F9] = "109";
            _speedDialMappings[Key.F10] = "110";
            _speedDialMappings[Key.F11] = "111";
            _speedDialMappings[Key.F12] = "112";
        }        /// <summary>
        /// Handle application-level key presses (for Ctrl combinations)
        /// This should be called from the main window's KeyDown event
        /// </summary>
        public bool HandleKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e == null) return false;

            var modifiers = Keyboard.Modifiers;
            var key = e.Key;

            // Handle Ctrl combinations for call control
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                switch (key)
                {
                    case Key.A:
                        AnswerRequested?.Invoke(this, EventArgs.Empty);
                        e.Handled = true;
                        return true;

                    case Key.H:
                        HangupRequested?.Invoke(this, EventArgs.Empty);
                        e.Handled = true;
                        return true;

                    case Key.M:
                        MuteRequested?.Invoke(this, EventArgs.Empty);
                        e.Handled = true;
                        return true;

                    case Key.D:
                        DtmfRequested?.Invoke(this, EventArgs.Empty);
                        e.Handled = true;
                        return true;
                }
            }

            // Handle F1-F12 speed dial (always enabled for convenience)
            if (_speedDialMappings.ContainsKey(key))
            {
                string phoneNumber = _speedDialMappings[key];
                if (!string.IsNullOrEmpty(phoneNumber))
                {
                    SpeedDialRequested?.Invoke(this, phoneNumber);
                    e.Handled = true;
                    return true;
                }
            }            // Note: DTMF digit handling removed from here
            // DTMF digits are now only handled by the OnDtmfDigitRequested method
            // which is only active during calls, preventing interference with text input

            return false;
        }

        /// <summary>
        /// Handle DTMF digit input specifically (only called during active calls)
        /// </summary>
        public bool HandleDtmfInput(System.Windows.Input.KeyEventArgs e)
        {
            if (e == null) return false;

            var key = e.Key;
            if (HandleDtmfDigit(key))
            {
                e.Handled = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Register global hotkeys (Ctrl+Alt combinations)
        /// </summary>
        private void RegisterGlobalHotkeys()
        {
            try
            {
                // Register Ctrl+Alt+A for Answer
                RegisterHotKey(_windowHandle, HOTKEY_ANSWER, MOD_CONTROL | MOD_ALT, VK_A);

                // Register Ctrl+Alt+H for Hangup
                RegisterHotKey(_windowHandle, HOTKEY_HANGUP, MOD_CONTROL | MOD_ALT, VK_H);
            }
            catch (Exception ex)
            {
                // Global hotkey registration can fail if already in use
                System.Diagnostics.Debug.WriteLine($"Failed to register global hotkeys: {ex.Message}");
            }
        }

        /// <summary>
        /// Unregister global hotkeys
        /// </summary>
        private void UnregisterGlobalHotkeys()
        {
            try
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ANSWER);
                UnregisterHotKey(_windowHandle, HOTKEY_HANGUP);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to unregister global hotkeys: {ex.Message}");
            }
        }

        /// <summary>
        /// Window message handler for global hotkeys
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                
                switch (hotkeyId)
                {
                    case HOTKEY_ANSWER:
                        AnswerRequested?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;

                    case HOTKEY_HANGUP:
                        HangupRequested?.Invoke(this, EventArgs.Empty);
                        handled = true;
                        break;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Handle DTMF digit keys (0-9, *, #)
        /// </summary>
        private bool HandleDtmfDigit(Key key)
        {
            char digit = key switch
            {
                Key.D0 or Key.NumPad0 => '0',
                Key.D1 or Key.NumPad1 => '1',
                Key.D2 or Key.NumPad2 => '2',
                Key.D3 when (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift => '#', // # key (Shift+3)
                Key.D3 or Key.NumPad3 => '3',
                Key.D4 or Key.NumPad4 => '4',
                Key.D5 or Key.NumPad5 => '5',
                Key.D6 or Key.NumPad6 => '6',
                Key.D7 or Key.NumPad7 => '7',
                Key.D8 when (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift => '*', // * key (Shift+8)
                Key.D8 or Key.NumPad8 => '8',
                Key.D9 or Key.NumPad9 => '9',
                Key.Multiply => '*', // Numpad * key
                _ => '\0'
            };

            if (digit != '\0')
            {
                DtmfDigitRequested?.Invoke(this, digit);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unregister global hotkeys
                    UnregisterGlobalHotkeys();

                    // Remove window message hook
                    _hwndSource?.RemoveHook(WndProc);
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~KeyboardShortcutService()
        {
            Dispose(false);
        }
    }
}
