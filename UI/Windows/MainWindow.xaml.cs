using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using WindowsSipPhone.Core.Models;
using WindowsSipPhone.Core.Utilities;
using WindowsSipPhone.Services.Logging;

namespace WindowsSipPhone.UI.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private DispatcherTimer _timer = null!;
        private SipPhoneService _sipService = null!;
        private NotifyIcon? _trayIcon;
        private bool _isClosingToTray = false;
        private KeyboardShortcutService? _keyboardService;
        private readonly ApplicationLogger _logger = ApplicationLogger.Instance;
        private LoggingWindow? _loggingWindow;
        private IRingtoneService? _ringtoneService;

        // Event for notifying call state changes
        public event EventHandler<string>? CallStateChanged;

        public MainWindow()
        {
            try
            {
                ApplicationTracker.TrackUIEvent("MainWindow opening", "Application main window initialization started");

                InitializeComponent();
                InitializeComponents();

                // Initialize keyboard shortcuts after window handle is available
                this.SourceInitialized += MainWindow_SourceInitialized;

                ApplicationTracker.TrackUIEvent("MainWindow initialized", "Main window successfully initialized");
            }
            catch (Exception ex)
            {
                ApplicationTracker.TrackError("MainWindow", "Failed to initialize main window", ex);

#if DEBUG
                Console.WriteLine($"MainWindow initialization error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
#endif
                // Show error and rethrow to let global handlers deal with it
                System.Windows.MessageBox.Show($"Failed to initialize main window: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // Initialize keyboard shortcuts now that window handle is available
            InitializeKeyboardShortcuts();
        }

        private void InitializeComponents()
        {
            try
            {
                // Initialize timer for status updates
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _timer.Tick += Timer_Tick;
                _timer.Start();
                // Initialize SIP service
                _sipService = new SipPhoneService();
                _sipService.StatusChanged += SipService_StatusChanged;
                _sipService.MessageReceived += SipService_MessageReceived;
                _sipService.CallStateChanged += SipService_CallStateChanged;
                // Initialize ringtone service
                _ringtoneService = new EnhancedRingtoneService();

                // Initialize system tray (make this optional in case of issues)
                try
                {
                    InitializeSystemTray();
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"System tray initialization failed (continuing without tray): {ex.Message}");
#else
                    // Suppress unused variable warning in release build
                    _ = ex;
#endif
                    // Continue without system tray if it fails
                }

                // NOTE: Keyboard shortcuts will be initialized in SourceInitialized event
                // to ensure window handle is available

                // Set initial UI state
                UpdateUI();

                // Initialize page integration
                InitializePageIntegration();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine($"Component initialization error: {ex.Message}");
#else
                // Suppress unused variable warning in release build
                _ = ex;
#endif
                throw;
            }
        }
        private void InitializePageIntegration()
        {
            // Connect SIP service to dialer page
            if (DialerPageControl != null)
            {
                DialerPageControl.SipService = _sipService;

                // Subscribe to dialer page events for better coordination
                DialerPageControl.PropertyChanged += DialerPage_PropertyChanged;
            }

            // Note: SIP and Audio settings are now handled in the Settings window
            // The settings pages will get their SIP service reference when opened

            // Apply saved audio settings to RTP audio manager on startup
            InitializeSavedAudioSettings();
        }

        private void InitializeSavedAudioSettings()
        {
            try
            {            // Load saved audio configuration and apply to RTP audio manager
                var audioConfig = AudioConfiguration.Load();
                var settings = audioConfig.ToAudioSettings();

                // Apply to RTP audio manager if available
                if (_sipService.RtpAudioManager != null)
                {
                    _sipService.RtpAudioManager.ApplySettings(settings);
                    Console.WriteLine("[MAIN WINDOW] Applied saved audio settings to RTP audio manager on startup");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAIN WINDOW] Error applying saved audio settings: {ex.Message}");
            }
        }

        private void DialerPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DialerPageControl.IsCallActive))
            {
                // Update main window status based on call state
                Dispatcher.Invoke(() =>
                {
                    if (DialerPageControl.IsCallActive)
                    {
                        StatusBarText.Text = $"Active call: {DialerPageControl.ActiveCallNumber}";
                        StatusBarIcon.Text = "📞";
                    }
                    else
                    {
                        // When call ends, restore normal status display
                        StatusBarText.Text = _sipService.IsRegistered ? "Ready - SIP Registered" : "Ready - Configure SIP settings to begin";
                        StatusBarIcon.Text = _sipService.IsRegistered ? "🟢" : "🔴";
                    }
                });
            }
        }

        private string ExtractSipMessageType(string message)
        {
            if (message.Contains("REGISTER")) return "REGISTER";
            if (message.Contains("INVITE")) return "INVITE";
            if (message.Contains("BYE")) return "BYE";
            if (message.Contains("OPTIONS")) return "OPTIONS";
            if (message.Contains("ACK")) return "ACK";
            if (message.Contains("CANCEL")) return "CANCEL";
            if (message.Contains("200 OK")) return "INFO";
            if (message.Contains("401 Unauthorized") || message.Contains("407 Proxy Authentication Required")) return "ERROR";
            if (message.Contains("404 Not Found")) return "ERROR";
            if (message.Contains("486 Busy Here") || message.Contains("603 Decline")) return "ERROR";
            if (message.Contains("180 Ringing") || message.Contains("183 Session Progress")) return "INFO";
            return "INFO";
        }

        private string DetermineMessageDirection(string message)
        {
            // Check for explicit direction markers from SimpleSipClient (including enhanced prefixes)
            if (message.StartsWith("OUTGOING") || message.Contains("sent:"))
                return "OUTGOING";
            if (message.StartsWith("INCOMING") || message.Contains("received:"))
                return "INCOMING";

            // Determine by message type - requests are typically outbound, responses inbound
            if (message.Contains("REGISTER ") || message.Contains("INVITE ") || message.Contains("BYE ") || message.Contains("ACK "))
                return "OUTGOING";
            if (message.Contains("SIP/2.0 "))
                return "INCOMING";

            return "System";
        }

        private string CleanSipMessage(string message)
        {
            // Remove direction prefixes added by SimpleSipClient (including enhanced prefixes)
            var cleaned = message;

            // Handle enhanced prefixes like "OUTGOING (ACK):", "OUTGOING (Call Answer):", etc.
            if (message.StartsWith("OUTGOING") || message.StartsWith("INCOMING"))
            {
                var colonIndex = message.IndexOf(':');
                if (colonIndex != -1 && message.Length > colonIndex + 1)
                {
                    cleaned = message.Substring(colonIndex + 1);
                    if (cleaned.StartsWith("\n"))
                    {
                        cleaned = cleaned.Substring(1);
                    }
                }
            }

            return cleaned.Trim();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            TimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        #region System Tray Implementation

        private void InitializeSystemTray()
        {
            try
            {
                // Create system tray icon
                _trayIcon = new NotifyIcon();

                // Set tray icon - use embedded resource or file path
                // Note: In production, you'd want to embed these as resources
                try
                {
                    var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", "phone.ico");
                    if (System.IO.File.Exists(iconPath))
                    {
                        _trayIcon.Icon = new Icon(iconPath);
                    }
                    else
                    {
                        // Fallback: create a simple icon programmatically
                        _trayIcon.Icon = CreateDefaultTrayIcon();
                    }
                }
                catch
                {
                    // Fallback: create a simple icon programmatically
                    _trayIcon.Icon = CreateDefaultTrayIcon();
                }

                _trayIcon.Text = "SIP Phone - Ready";
                _trayIcon.Visible = true;

                // Create context menu
                var contextMenu = new ContextMenuStrip();

                // Show main window option
                var showItem = new ToolStripMenuItem("Show SIP Phone");
                showItem.Click += (s, e) => ShowMainWindow();
                showItem.Font = new System.Drawing.Font(showItem.Font, System.Drawing.FontStyle.Bold);
                contextMenu.Items.Add(showItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Call control options (will be enabled/disabled based on call state)
                var answerItem = new ToolStripMenuItem("Answer Call");
                answerItem.Click += async (s, e) => await AnswerCallFromTray();
                answerItem.Enabled = false;
                contextMenu.Items.Add(answerItem);

                var declineItem = new ToolStripMenuItem("Decline Call");
                declineItem.Click += async (s, e) => await DeclineCallFromTray();
                declineItem.Enabled = false;
                contextMenu.Items.Add(declineItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Settings and utilities
                var settingsItem = new ToolStripMenuItem("Settings");
                settingsItem.Click += (s, e) => ShowSettingsFromTray();
                contextMenu.Items.Add(settingsItem);

                var aboutItem = new ToolStripMenuItem("About");
                aboutItem.Click += (s, e) => ShowAboutFromTray();
                contextMenu.Items.Add(aboutItem);

                contextMenu.Items.Add(new ToolStripSeparator());

                // Exit option
                var exitItem = new ToolStripMenuItem("Exit");
                exitItem.Click += (s, e) => ExitApplicationFromTray();
                contextMenu.Items.Add(exitItem);

                _trayIcon.ContextMenuStrip = contextMenu;

                // Handle tray icon events
                _trayIcon.DoubleClick += TrayIcon_DoubleClick;
                _trayIcon.Click += TrayIcon_Click;

                Console.WriteLine("[SYSTEM TRAY] System tray initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSTEM TRAY] Error initializing system tray: {ex.Message}");
            }
        }

        private Icon CreateDefaultTrayIcon()
        {
            // Create a simple 16x16 icon programmatically
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                using (var brush = new SolidBrush(Color.Green))
                {
                    graphics.FillEllipse(brush, 2, 2, 12, 12);
                }
                using (var pen = new Pen(Color.DarkGreen, 1))
                {
                    graphics.DrawEllipse(pen, 2, 2, 12, 12);
                }
            }
            var handle = bitmap.GetHicon();
            return System.Drawing.Icon.FromHandle(handle);
        }

        private void UpdateTrayIcon(string status, bool hasIncomingCall = false)
        {
            if (_trayIcon == null) return;

            try
            {
                // Update tray icon based on status
                string iconFileName = hasIncomingCall ? "phone-active.ico" : "phone.ico";
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", iconFileName);

                if (System.IO.File.Exists(iconPath))
                {
                    _trayIcon.Icon?.Dispose();
                    _trayIcon.Icon = new Icon(iconPath);
                }

                // Update tooltip text (Windows tray text must be < 128 characters)
                var truncatedStatus = status.Length > 100 ? status.Substring(0, 100) + "..." : status;
                _trayIcon.Text = $"SIP Phone - {truncatedStatus}";

                // Update context menu items based on call state
                if (_trayIcon.ContextMenuStrip != null)
                {
                    var answerItem = _trayIcon.ContextMenuStrip.Items.Cast<ToolStripItem>()
                        .FirstOrDefault(item => item.Text == "Answer Call") as ToolStripMenuItem;
                    var declineItem = _trayIcon.ContextMenuStrip.Items.Cast<ToolStripItem>()
                        .FirstOrDefault(item => item.Text == "Decline Call") as ToolStripMenuItem;

                    if (answerItem != null && declineItem != null)
                    {
                        answerItem.Enabled = hasIncomingCall;
                        declineItem.Enabled = hasIncomingCall;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSTEM TRAY] Error updating tray icon: {ex.Message}");
            }
        }

        private void ShowToastNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (_trayIcon == null) return;

            try
            {
                _trayIcon.ShowBalloonTip(5000, title, message, icon);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSTEM TRAY] Error showing toast notification: {ex.Message}");
            }
        }

        #endregion

        #region Tray Event Handlers

        private void TrayIcon_DoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void TrayIcon_Click(object? sender, EventArgs e)
        {
            // Single click can be used for quick actions if needed
            // For now, we'll just handle double-click to show the window
        }

        private void ShowMainWindow()
        {
            try
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                this.Focus();

                Console.WriteLine("[SYSTEM TRAY] Main window restored from tray");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSTEM TRAY] Error showing main window: {ex.Message}");
            }
        }

        private async Task AnswerCallFromTray()
        {
            try
            {
                await _sipService.AcceptIncomingCallAsync();
                ShowMainWindow(); // Show the window when answering a call

                // No need to switch tabs since we have direct dialer access

                Console.WriteLine("[SYSTEM TRAY] Call answered from tray");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSTEM TRAY] Error answering call from tray: {ex.Message}");
                ShowToastNotification("Call Error", "Failed to answer call", ToolTipIcon.Error);
            }
        }

        private async Task DeclineCallFromTray()
        {
            try
            {
                await _sipService.DeclineIncomingCallAsync();
                Console.WriteLine("[SYSTEM TRAY] Call declined from tray");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYSTEM TRAY] Error declining call from tray: {ex.Message}");
                ShowToastNotification("Call Error", "Failed to decline call", ToolTipIcon.Error);
            }
        }

        private void ShowSettingsFromTray()
        {
            ShowMainWindow();
            Settings_Click(this, new RoutedEventArgs()); // Open settings window directly
        }

        private void ShowAboutFromTray()
        {
            var aboutMessage = $"SIP Phone Application\n\n" +
                              $"Version: 1.0\n" +
                              $"Built with SIPSorcery\n\n" +
                              $"Features:\n" +
                              $"• SIP Registration & Calling\n" +
                              $"• DTMF Support\n" +
                              $"• Call History\n" +
                              $"• Audio Device Selection\n" +
                              $"• System Tray Integration";

            System.Windows.MessageBox.Show(aboutMessage, "About SIP Phone", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExitApplicationFromTray()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to exit the SIP Phone application?",
                "Exit Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _isClosingToTray = false; // Force actual close
                System.Windows.Application.Current.Shutdown();
            }
        }

        #endregion

        #region Event Handlers

        private void SipService_StatusChanged(object? sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                HeaderStatusText.Text = status;
                StatusBarText.Text = status;

                // Update status bar icon based on status message content
                // Check for successful registration states
                if (status.Contains("Registration successful") ||
                    status.Contains("✅ Registration successful") ||
                    (status.Contains("Registered") && !status.Contains("Not Registered")))
                {
                    StatusBarIcon.Text = "🟢";
                }
                // Check for in-progress states
                else if (status.Contains("Registering") ||
                         status.Contains("Connecting") ||
                         status.Contains("attempting registration") ||
                         status.Contains("🔍 DEBUG: SIP client connected"))
                {
                    StatusBarIcon.Text = "🟡";
                }
                // Default to red for failures, disconnected states, or unknown states
                else
                {
                    StatusBarIcon.Text = "🔴";
                }

                // Update tray icon
                UpdateTrayIcon(status);
            });
        }

        private void SipService_MessageReceived(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                // SIP messages are now handled by the debug tools in Settings window
                // No need to forward messages here as the debug window will handle this directly
            });
        }

        private void SipService_CallStateChanged(object? sender, string callInfo)
        {
            Dispatcher.Invoke(() =>
            {
                StatusBarText.Text = callInfo;

                // Handle incoming call notifications
                if (callInfo.StartsWith("Incoming call:"))
                {
                    HandleIncomingCall(callInfo);
                    UpdateTrayIcon("Incoming Call", true);
                    ShowToastNotification("Incoming Call", callInfo.Replace("Incoming call: ", ""), ToolTipIcon.Info);
                }
                else if (callInfo.Contains("Call Connected") || callInfo.Contains("Call answered"))
                {
                    // No need to switch tabs since we have direct dialer access
                    UpdateTrayIcon("Call Active");
                }
            });
        }

        private void HandleIncomingCall(string callInfo)
        {
            try
            {
                var callerInfo = callInfo.Replace("Incoming call: ", "").Trim();
                // Show incoming call window
                var incomingCallWindow = new IncomingCallWindow(callerInfo, _ringtoneService);incomingCallWindow.CallAnswered += async (sender, accepted) =>
                {
                    if (accepted)
                    {
                        Console.WriteLine($"[MAIN WINDOW DEBUG] CallAnswered event received - accepting call");

                        // Accept the call through SipPhoneService
                        try
                        {
                            Console.WriteLine($"[MAIN WINDOW DEBUG] Calling _sipService.AcceptIncomingCallAsync()");
                            await _sipService.AcceptIncomingCallAsync();
                            Console.WriteLine($"[MAIN WINDOW DEBUG] AcceptIncomingCallAsync completed successfully");

                            StatusBarText.Text = $"Call accepted from {callerInfo}";
                            CallStateChanged?.Invoke(this, "Incoming Call Answered");

                            // No need to switch tabs since we have direct dialer access

                            // Add to call history in DialerPage
                            if (DialerPageControl?.SipService != null)
                            {
                                // The DialerPage will handle adding to call history through its event handler
                            }

                            UpdateTrayIcon("Call Active");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MAIN WINDOW DEBUG] Exception in AcceptIncomingCallAsync: {ex.Message}");
                            Console.WriteLine($"[MAIN WINDOW DEBUG] Stack trace: {ex.StackTrace}");
                        }
                    }
                    else
                    {
                        // Decline the call through SipPhoneService
                        await _sipService.DeclineIncomingCallAsync();
                        StatusBarText.Text = "Incoming call declined";
                        CallStateChanged?.Invoke(this, "Incoming Call Declined");
                        UpdateTrayIcon("Ready");
                    }
                };

                // Show the window
                incomingCallWindow.Show();
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Error handling incoming call: {ex.Message}";
            }
        }

        #endregion

        #region Button Event Handlers
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogAction("UI", "Settings button clicked");

                // Create and show settings window with SIP service, keyboard service, and main window reference
                var settingsWindow = new SettingsWindow(_sipService, _keyboardService, this, _ringtoneService)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                // Show as non-modal window
                settingsWindow.Show();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAIN WINDOW ERROR] Failed to open Settings window: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to open Settings: {ex.Message}", "Settings Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateUI()
        {
            // Update header status
            HeaderStatusText.Text = _sipService.IsRegistered ? "✅ Registered" : "❌ Not Registered";

            // Update status bar
            StatusBarText.Text = _sipService.IsRegistered ? "Ready - SIP Registered" : "Ready - Configure SIP settings to begin";

            // Update status bar icon to reflect current registration state
            // This is used during initialization and periodic updates
            StatusBarIcon.Text = _sipService.IsRegistered ? "🟢" : "🔴";
        }

        private void InitializeKeyboardShortcuts()
        {
            try
            {
                // Ensure window handle is available
                var helper = new WindowInteropHelper(this);
                if (helper.Handle == IntPtr.Zero)
                {
                    Console.WriteLine("[KEYBOARD] ⚠️ Window handle not yet available, will retry...");
                    // Retry after a short delay
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        InitializeKeyboardShortcuts();
                    }), DispatcherPriority.Loaded);
                    return;
                }

                _keyboardService = new KeyboardShortcutService(this);

                // Subscribe to keyboard shortcut events
                _keyboardService.AnswerCallRequested += OnAnswerCallRequested;
                _keyboardService.HangupCallRequested += OnHangupCallRequested;
                _keyboardService.MuteToggleRequested += OnMuteToggleRequested;
                _keyboardService.ShowDtmfKeypadRequested += OnShowDtmfKeypadRequested;
                _keyboardService.SpeedDialRequested += OnSpeedDialRequested;
                _keyboardService.DtmfDigitRequested += OnDtmfDigitRequested;

                // Enable keyboard input for the main window
                this.KeyDown += MainWindow_KeyDown;

                Console.WriteLine("[KEYBOARD] ✅ Keyboard shortcuts initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEYBOARD] ❌ Error initializing keyboard shortcuts: {ex.Message}");
            }
        }

        #region Window State Management

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // Minimize to tray instead of taskbar
                this.Hide();
                ShowToastNotification("SIP Phone", "Application minimized to system tray", ToolTipIcon.Info);
            }

            // Update maximize/restore button icon based on window state
            if (MaximizeRestoreButton != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    MaximizeRestoreButton.Content = "\uE923"; // Restore icon
                    MaximizeRestoreButton.ToolTip = "Restore Down";
                }
                else
                {
                    MaximizeRestoreButton.Content = "\uE922"; // Maximize icon
                    MaximizeRestoreButton.ToolTip = "Maximize";
                }
            }

            // Adjust window chrome for maximized state
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    // Remove resize border when maximized to prevent extending beyond screen
                    chrome.ResizeBorderThickness = new Thickness(0);
                }
                else
                {
                    // Restore resize border for normal state
                    chrome.ResizeBorderThickness = new Thickness(4);
                }
            }

            base.OnStateChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isClosingToTray)
            {
                // Ask user if they want to minimize to tray or actually exit
                var result = System.Windows.MessageBox.Show(
                    "Do you want to minimize SIP Phone to system tray?\n\n" +
                    "Click 'Yes' to minimize to tray\n" +
                    "Click 'No' to exit completely\n" +
                    "Click 'Cancel' to continue using the application",
                    "Close Application",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    this.Hide();
                    ShowToastNotification("SIP Phone", "Application minimized to system tray", ToolTipIcon.Info);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
                // If No, let the application close normally
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up system tray icon
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _timer?.Stop();
            _sipService?.Dispose();
            _keyboardService?.Dispose();
            base.OnClosed(e);
        }
        #endregion

        #region Keyboard Shortcut Event Handlers

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                // Check if focus is on a text input control - if so, don't process shortcuts
                if (IsTextInputFocused())
                {
                    // Let the text input control handle the key normally
                    return;
                }

                // First try to handle general keyboard shortcuts (Ctrl+combinations, F keys)
                if (_keyboardService?.HandleKeyDown(e) == true)
                {
                    // Key was handled by shortcut service
                    return;
                }

                // If there's an active call and no text input is focused, handle DTMF digits
                if (DialerPageControl?.IsCallActive == true && _keyboardService != null)
                {
                    if (_keyboardService.HandleDtmfInput(e))
                    {
                        // DTMF digit was handled
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEYBOARD] ❌ Error handling key down: {ex.Message}");
            }
        }    /// <summary>
        /// Check if the currently focused element is a text input control
        /// </summary>
        private bool IsTextInputFocused()
        {
            var focusedElement = Keyboard.FocusedElement;

            // Check for various WPF text input controls
            return focusedElement is System.Windows.Controls.TextBox ||
                   focusedElement is System.Windows.Controls.PasswordBox ||
                   focusedElement is System.Windows.Controls.ComboBox ||
                   focusedElement is System.Windows.Controls.RichTextBox ||
                   (focusedElement != null &&
                    (focusedElement.GetType().Name.Contains("TextBox") ||
                     focusedElement.GetType().Name.Contains("PasswordBox") ||
                     focusedElement.GetType().Name.Contains("ComboBox")));
        }

        private void OnAnswerCallRequested(object? sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("[KEYBOARD] Answer call shortcut triggered");

                // Answer call through dialer page if there's an active incoming call
                if (DialerPageControl?.SipService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await DialerPageControl.SipService.AcceptIncomingCallAsync();

                            // No need to switch tabs since we have direct dialer access
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[KEYBOARD] ❌ Error answering call via shortcut: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEYBOARD] ❌ Error in answer call handler: {ex.Message}");
            }
        }

        private void OnHangupCallRequested(object? sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("[KEYBOARD] Hangup call shortcut triggered");

                // Hangup through SIP service
                if (_sipService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sipService.HangupAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[KEYBOARD] ❌ Error hanging up call via shortcut: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEYBOARD] ❌ Error in hangup call handler: {ex.Message}");
            }
        }

        private void OnMuteToggleRequested(object? sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("[KEYBOARD] Mute toggle shortcut triggered");

                // Toggle mute through dialer page
                if (DialerPageControl != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Access the audio command from the dialer page
                        if (DialerPageControl.AudioCommand?.CanExecute(null) == true)
                        {
                            DialerPageControl.AudioCommand.Execute(null);
                        }

                        // No need to switch tabs since we have direct dialer access
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEYBOARD] ❌ Error in mute toggle handler: {ex.Message}");
            }
        }
        private void OnShowDtmfKeypadRequested(object? sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("[KEYBOARD] Show DTMF keypad shortcut triggered");

                // No need to switch tabs since we have direct dialer access
                Dispatcher.Invoke(() =>
                {
                    // Focus on the number display for DTMF input
                    if (DialerPageControl != null)
                    {
                        // The DTMF keypad is automatically visible during active calls
                        // Just ensure the user is on the right tab
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEYBOARD] ❌ Error in show DTMF handler: {ex.Message}");
            }
        }

        private void OnSpeedDialRequested(object? sender, string number)
        {
            try
            {
                Console.WriteLine($"[KEYBOARD] Speed dial requested: {number}");

                if (string.IsNullOrEmpty(number))
                {
                    Console.WriteLine("[KEYBOARD] ⚠️ Speed dial number is empty");
                    return;
                }
                // Set the dialed number and initiate call
                if (DialerPageControl != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // No need to switch tabs since we have direct dialer access
                        DialerPageControl.DialedNumber = number;

                        // Auto-dial if registered
                        if (DialerPageControl.CallCommand?.CanExecute(null) == true)
                        {
                            DialerPageControl.CallCommand.Execute(null);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEYBOARD] ❌ Error in speed dial handler: {ex.Message}");
            }
        }

        private void OnDtmfDigitRequested(object? sender, char digit)
        {
            try
            {
                // Only handle DTMF during active calls
                if (DialerPageControl?.IsCallActive == true)
                {
                    Console.WriteLine($"[KEYBOARD] DTMF digit requested during call: {digit}");

                    // Send DTMF digit through dialer page
                    Dispatcher.Invoke(() =>
                    {
                        if (DialerPageControl.KeypadCommand?.CanExecute(digit.ToString()) == true)
                        {
                            DialerPageControl.KeypadCommand.Execute(digit.ToString());
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KEYBOARD] ❌ Error in DTMF digit handler: {ex.Message}");        }
        }

        #endregion

        #region Logging Window Management

        public void ShowLoggingWindow()
        {
            try
            {
                if (_loggingWindow == null || !_loggingWindow.IsVisible)
                {
                    _loggingWindow = new LoggingWindow();
                    _loggingWindow.Closed += (s, args) => _loggingWindow = null;
                    _loggingWindow.Show();

                    // Position the window to the right of the main window
                    _loggingWindow.Left = this.Left + this.Width + 10;
                    _loggingWindow.Top = this.Top;
                }
                else
                {
                    _loggingWindow.Activate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAIN WINDOW] Error opening logging window: {ex.Message}");
            }
        }

        public void HideLoggingWindow()
        {
            try
            {
                if (_loggingWindow != null && _loggingWindow.IsVisible)
                {
                    _loggingWindow.Close();
                    _loggingWindow = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAIN WINDOW] Error closing logging window: {ex.Message}");
            }
        }

        public bool IsLoggingWindowVisible => _loggingWindow != null && _loggingWindow.IsVisible;

        #endregion

        #region Window Control Events        /// <summary>
        /// Handles mouse down events on the title bar to enable window dragging
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only handle left button down for dragging
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                // Handle double-click to maximize/restore
                if (e.ClickCount == 2)
                {
                    // Toggle between maximized and normal state
                    if (WindowState == WindowState.Maximized)
                    {
                        WindowState = WindowState.Normal;
                    }
                    else
                    {
                        WindowState = WindowState.Maximized;
                    }
                }
                else if (e.ClickCount == 1)
                {
                    // Handle single click for dragging
                    try
                    {
                        this.DragMove();
                    }
                    catch (InvalidOperationException)
                    {
                        // DragMove can throw if called when the window is not in a state where it can be moved
                        // This can happen if the window is maximized, so we ignore this exception
                    }
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeRestoreButton.Content = "\uE922"; // Maximize icon
                MaximizeRestoreButton.ToolTip = "Maximize";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeRestoreButton.Content = "\uE923"; // Restore icon
                MaximizeRestoreButton.ToolTip = "Restore Down";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #endregion
    }
}