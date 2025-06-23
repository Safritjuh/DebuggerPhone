using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using WindowsSipPhone.Core.Utilities;
using WindowsSipPhone.Services.Data;
using WindowsSipPhone.Services.Logging;

namespace WindowsSipPhone.UI.Pages
{    
    public partial class DialerPage : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {        private string _dialedNumber = "";
        private CallHistoryEntry? _selectedCall;
        private string _currentFilter = "All";        private SipPhoneService? _sipService;
        private bool _isCallActive = false;
        private string _activeCallNumber = "";
        private string _incomingCallNumber = ""; // Store incoming call number until accepted
        private bool _isIncomingCall = false; // Track if current call is incoming or outgoing
        private DateTime? _callStartTime;
        private System.Windows.Threading.DispatcherTimer? _callTimer;
        private bool _isMuted = false;
        private double _audioVolume = 0.8; // 80% default volume
        private bool _isSpeakerOn = false;
        
        // Call Hold/Resume Support
        private bool _isCallOnHold = false;

        // DTMF Support Properties
        private bool _isDtmfActive = false;
        private string _dtmfStatusText = "";
        private string _lastDtmfDigit = "";        // Database Service
        private readonly CallHistoryService _callHistoryService;
        
        // Logging Service
        private readonly ApplicationLogger _logger;        public DialerPage()
        {
            _callHistoryService = new CallHistoryService();
            _logger = ApplicationLogger.Instance;
            InitializeComponent();
            DataContext = this;
            InitializeCommands();
            LoadCallHistory();
            InitializeCallTimer();
        }

        public SipPhoneService? SipService
        {
            get => _sipService;
            set
            {
                if (_sipService != null)
                {
                    _sipService.CallStateChanged -= OnCallStateChanged;
                }
                _sipService = value;
                if (_sipService != null)
                {
                    _sipService.CallStateChanged += OnCallStateChanged;
                }
                OnPropertyChanged();
            }
        }

        private void InitializeCallTimer()
        {
            _callTimer = new System.Windows.Threading.DispatcherTimer();
            _callTimer.Interval = TimeSpan.FromSeconds(1);
            _callTimer.Tick += CallTimer_Tick;
        }

        private void CallTimer_Tick(object? sender, EventArgs e)
        {
            if (_isCallActive && _callStartTime.HasValue)
            {
                var duration = DateTime.Now - _callStartTime.Value;
                OnPropertyChanged(nameof(CallDurationText));
            }
        }        
          private void OnCallStateChanged(object? sender, string callState)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {                if (callState.StartsWith("Outgoing call to") || callState.StartsWith("Dialing"))
                {
                    var number = callState.Contains("Dialing") ? 
                        callState.Replace("Dialing ", "") : 
                        callState.Replace("Outgoing call to ", "");
                    StartCall(number, false); // false = outgoing call
                }else if (callState.StartsWith("Incoming call:"))
                {
                    // Store incoming call info for when it's accepted
                    var callerInfo = callState.Replace("Incoming call: ", "");
                    _incomingCallNumber = callerInfo;
                    _logger.LogSystemInfo("CALL", $"📞 Incoming call from: {callerInfo}");
                    Console.WriteLine($"[DEBUG] Incoming call detected from: {callerInfo}");
                }else if (callState.Contains("Call accepted") || callState.Contains("Call Connected") || 
                         callState.Contains("Call answered") || callState.Contains("200 OK") || 
                         callState.Contains("Incoming Call Answered"))
                {
                    // For incoming calls that are accepted, start the call UI
                    if (!_isCallActive && !string.IsNullOrEmpty(_incomingCallNumber))
                    {
                        _logger.LogSystemInfo("CALL", $"✅ Starting UI for accepted incoming call from: {_incomingCallNumber}");
                        
                        // Create call history entry for the accepted incoming call
                        var incomingCall = new CallHistoryEntry
                        {
                            Number = CallHistoryEntry.ExtractNumberPart(_incomingCallNumber),
                            CallerName = CallHistoryEntry.ExtractDisplayName(_incomingCallNumber),
                            CallType = CallType.Incoming,
                            DateTime = DateTime.Now,
                            Duration = TimeSpan.Zero,
                            Status = CallStatus.InProgress
                        };
                        
                        // Add to UI and database
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            CallHistory.Insert(0, incomingCall);
                            ApplyCurrentFilter();
                        });
                        
                        _callHistoryService.AddCall(WindowsSipPhone.Services.Data.CallHistoryEntry.FromUiModel(incomingCall));
                        _logger.LogSystemInfo("CALL_HISTORY", $"📞← Incoming call added to history: {_incomingCallNumber}");
                        
                        StartCall(_incomingCallNumber, true); // true = incoming call
                        _incomingCallNumber = ""; // Clear after use
                    }
                    
                    // Call is connected and active - start the timer
                    SetCallConnected();
                }else if (callState.Contains("Call Ended") || callState.Contains("Call ended") || 
                         callState.Contains("Call Failed") || callState.Contains("Call failed") ||
                         callState.Contains("Call Declined"))
                {                    // Handle missed call for incoming calls that are declined or not answered
                    if (callState.Contains("Call Declined") && !string.IsNullOrEmpty(_incomingCallNumber))
                    {
                        _logger.LogSystemInfo("CALL", $"📞❌ Missed call from: {_incomingCallNumber}");
                          // Add missed call to history
                        var missedCall = new CallHistoryEntry
                        {
                            Number = CallHistoryEntry.ExtractNumberPart(_incomingCallNumber),
                            CallerName = CallHistoryEntry.ExtractDisplayName(_incomingCallNumber),
                            CallType = CallType.Missed,
                            DateTime = DateTime.Now,
                            Duration = TimeSpan.Zero,
                            Status = CallStatus.Missed
                        };
                        
                        // Ensure UI updates happen on UI thread
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            CallHistory.Insert(0, missedCall);
                            ApplyCurrentFilter();
                        });
                        
                        _callHistoryService.AddCall(WindowsSipPhone.Services.Data.CallHistoryEntry.FromUiModel(missedCall));
                        
                        _logger.LogSystemInfo("CALL", $"✅ Missed call added to history: {_incomingCallNumber}");
                    }
                    
                    // Clear incoming call number on any call end
                    _incomingCallNumber = "";
                    EndCall();
                }
            });
        }        private void StartCall(string number, bool isIncoming = false)
        {
            _isCallActive = true;
            _activeCallNumber = number;
            _isIncomingCall = isIncoming;
            // Do NOT start timer or set start time here - wait for call to be answered
            OnPropertyChanged(nameof(IsCallActive));
            OnPropertyChanged(nameof(CallStatusText));
            OnPropertyChanged(nameof(ActiveCallNumber));
        }

        private void SetCallConnected()
        {
            if (_isCallActive)
            {
                // Start timer only when call is actually connected (200 OK received)
                _callStartTime = DateTime.Now;
                _callTimer?.Start();
                OnPropertyChanged(nameof(CallStatusText));
                OnPropertyChanged(nameof(CallDurationText));
            }
        }        private void EndCall()
        {
            _logger.LogSystemInfo("CALL", $"Ending call - Previous state: Active={_isCallActive}, OnHold={_isCallOnHold}, Number='{_activeCallNumber}'");
            
            // Calculate actual call duration before resetting timer
            TimeSpan actualCallDuration = TimeSpan.Zero;
            if (_callStartTime.HasValue)
            {
                actualCallDuration = DateTime.Now - _callStartTime.Value;
                _logger.LogSystemInfo("CALL", $"📊 Actual call duration: {actualCallDuration.TotalSeconds:F1} seconds");
            }
              _isCallActive = false;
            _activeCallNumber = "";
            _isIncomingCall = false; // Reset direction flag
            _callStartTime = null;
            _callTimer?.Stop();
            
            // Reset hold state when call ends
            IsCallOnHold = false;
            
            OnPropertyChanged(nameof(IsCallActive));
            OnPropertyChanged(nameof(CallStatusText));
            OnPropertyChanged(nameof(ActiveCallNumber));
            OnPropertyChanged(nameof(CallDurationText));

            // Update call history with actual call duration
            var activeCall = CallHistory.FirstOrDefault(c => c.Status == CallStatus.InProgress);
            if (activeCall != null)
            {
                activeCall.Status = CallStatus.Completed;
                
                // Use actual call duration (speaking time) instead of total time from initiation
                if (actualCallDuration.TotalSeconds > 0)
                {
                    activeCall.Duration = actualCallDuration;
                    _logger.LogSystemInfo("CALL", $"📊 Call duration updated to speaking time: {activeCall.DurationText}");
                }
                else
                {
                    // If call was never connected, duration remains 0
                    activeCall.Duration = TimeSpan.Zero;
                    _logger.LogSystemInfo("CALL", "📊 Call was never connected - duration remains 0");
                }
                
                // Persist the call completion to database
                _callHistoryService.UpdateCall(WindowsSipPhone.Services.Data.CallHistoryEntry.FromUiModel(activeCall));
                _logger.LogSystemInfo("CALL", $"✅ Call ended and updated in database: {activeCall.Number} - Duration: {activeCall.DurationText}");
            }
            
            _logger.LogSystemInfo("CALL", "✅ End call completed - UI state reset");
        }

        #region Properties

        public string DialedNumber
        {
            get => _dialedNumber;
            set
            {
                _dialedNumber = value;
                OnPropertyChanged();
            }
        }

        public CallHistoryEntry? SelectedCall
        {
            get => _selectedCall;
            set
            {
                _selectedCall = value;
                OnPropertyChanged();
            }
        }

        public bool IsCallActive
        {
            get => _isCallActive;
        }

        public string ActiveCallNumber
        {
            get => _activeCallNumber;
        }        public string CallStatusText
        {
            get
            {
                if (!_isCallActive) return "";
                
                // Extract clean display name and number for better UI
                var displayName = CallHistoryEntry.ExtractDisplayName(_activeCallNumber);
                var number = CallHistoryEntry.ExtractNumberPart(_activeCallNumber);
                
                // Create a clean display format
                string displayText;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    displayText = $"{displayName} ({number})";
                }
                else
                {
                    displayText = number;
                }
                  // Add direction indicator
                string directionIcon = _isIncomingCall ? "📞←" : "📞→";
                
                if (_callStartTime.HasValue)
                {
                    if (_isCallOnHold)
                    {
                        return $"{directionIcon} On Hold: {displayText}";
                    }
                    return $"{directionIcon} Connected to {displayText}";
                }
                else
                {
                    return $"{directionIcon} {(_isIncomingCall ? "Answering" : "Calling")} {displayText}...";
                }
            }
        }

        public string CallDurationText
        {
            get
            {
                if (!_isCallActive || !_callStartTime.HasValue) return "";
                
                var duration = DateTime.Now - _callStartTime.Value;
                return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
        }

        public ObservableCollection<CallHistoryEntry> CallHistory { get; } = new();
        
        public ObservableCollection<CallHistoryEntry> FilteredCallHistory { get; } = new();

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AudioButtonText));
            }
        }        public double AudioVolume
        {
            get => _audioVolume;
            set
            {
                _audioVolume = Math.Max(0.0, Math.Min(1.0, value));
                OnPropertyChanged();
                _sipService?.SetAudioVolume(_audioVolume);
            }
        }

        public bool IsSpeakerOn
        {
            get => _isSpeakerOn;
            set
            {
                _isSpeakerOn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AudioButtonText));
            }        }

        public string AudioButtonText
        {
            get
            {
                if (_isMuted)
                    return "🔇 Unmute";
                else if (_isSpeakerOn)
                    return "🔊 Speaker";
                else
                    return "🔊 Audio";
            }
        }        public bool IsCallOnHold
        {
            get => _isCallOnHold;
            set
            {
                _logger.LogSystemInfo("CALL", $"Hold state changing: {_isCallOnHold} → {value}");
                _isCallOnHold = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HoldButtonText));
                OnPropertyChanged(nameof(CallStatusText));
            }
        }

        public string HoldButtonText
        {
            get => _isCallOnHold ? "⏸️ Resume" : "⏸️ Hold";
        }

        // DTMF Properties
        public bool IsDtmfActive
        {
            get => _isDtmfActive;
            set
            {
                _isDtmfActive = value;
                OnPropertyChanged();
            }
        }

        public string DtmfStatusText
        {
            get => _dtmfStatusText;
            set
            {
                _dtmfStatusText = value;
                OnPropertyChanged();
            }
        }

        #region Commands
        public ICommand KeypadCommand { get; private set; } = null!; // Unified keypad command for dialing/DTMF
        public ICommand ClearCommand { get; private set; } = null!;
        public ICommand CallCommand { get; private set; } = null!;
        public ICommand HangupCommand { get; private set; } = null!;
        public ICommand HoldCommand { get; private set; } = null!;
        public ICommand AudioCommand { get; private set; } = null!;
        public ICommand ShowDtmfKeypadCommand { get; private set; } = null!;
        public ICommand FilterCallsCommand { get; private set; } = null!;
        public ICommand RedialCommand { get; private set; } = null!;        public ICommand ClearHistoryCommand { get; private set; } = null!;        public ICommand ExportCsvCommand { get; private set; } = null!;
        // TestCallCommand and RefreshCommand removed for cleaner UI

        #endregion

        private void InitializeCommands()
        {
            KeypadCommand = new RelayCommand<string>(HandleKeypadPress); // Unified keypad handler
            ClearCommand = new RelayCommand(Clear);
            CallCommand = new RelayCommand(() => _ = MakeCallAsync(), CanMakeCall);
            HangupCommand = new RelayCommand(() => _ = HangupCallAsync());
            HoldCommand = new RelayCommand(() => _ = ToggleHoldAsync());
            AudioCommand = new RelayCommand(ToggleAudio);
            ShowDtmfKeypadCommand = new RelayCommand(ShowDtmfKeypad, () => _isCallActive);
            FilterCallsCommand = new RelayCommand<string>(FilterCalls);
            RedialCommand = new RelayCommand(RedialCall, CanRedial);
            ClearHistoryCommand = new RelayCommand(ClearCallHistory);
            ExportCsvCommand = new RelayCommand(ExportCallHistoryToCsv);            // TestCallCommand and RefreshCommand initialization removed
        }

        #endregion

        private async Task MakeCallAsync()
        {
            await MakeCall();
        }        private async Task HangupCallAsync()
        {
            await HangupCall();
        }

        private async Task ToggleHoldAsync()
        {
            await ToggleHold();
        }private void ToggleAudio()
        {            if (_sipService?.SipClient?.AudioManager != null)
            {
                IsMuted = !IsMuted;
                _sipService.SipClient.AudioManager.SetMuted(IsMuted);
                
                _logger.LogAction("AUDIO", $"Audio {(IsMuted ? "muted" : "unmuted")} by user");
                
                // The audio manager will provide status feedback through StatusChanged events
            }
            else
            {
                // Only show warning if no active call, but don't block the UI
                if (!_isCallActive)
                {
                    _logger.LogWarning("AUDIO", "Audio control not available - no active call");
                    System.Windows.MessageBox.Show(
                        "Audio control not available - no active call", 
                        "Audio Control", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Information);
                }            }
        }

        #region Command Implementations

        private void AddDigit(string? digit)
        {
            if (!string.IsNullOrEmpty(digit))
            {
                DialedNumber += digit;
            }
        }        private void Clear()
        {
            _logger.LogAction("UI", "Clear button pressed - dialed number cleared");
            DialedNumber = "";
        }
        
        private bool CanMakeCall()
        {
            return !string.IsNullOrWhiteSpace(DialedNumber) && _sipService?.IsRegistered == true;
        }            private async Task MakeCall()        {
            var debugLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsSipPhone", "debug.log");
            Directory.CreateDirectory(Path.GetDirectoryName(debugLog) ?? "");
            
            try 
            {
                File.AppendAllText(debugLog, $"[{DateTime.Now}] MakeCall called with DialedNumber: '{DialedNumber}'\n");
                File.AppendAllText(debugLog, $"[{DateTime.Now}] SipService is null: {_sipService == null}\n");
                File.AppendAllText(debugLog, $"[{DateTime.Now}] SipService.IsRegistered: {_sipService?.IsRegistered}\n");
            }
            catch { }
            
            Console.WriteLine($"[DEBUG] MakeCall called with DialedNumber: '{DialedNumber}'");
            Console.WriteLine($"[DEBUG] SipService is null: {_sipService == null}");
            Console.WriteLine($"[DEBUG] SipService.IsRegistered: {_sipService?.IsRegistered}");
            
            if (string.IsNullOrWhiteSpace(DialedNumber) || _sipService == null) 
            {
                Console.WriteLine("[DEBUG] Early return - empty number or null service");
                File.AppendAllText(debugLog, $"[{DateTime.Now}] Early return - empty number or null service\n");
                return;
            }

            if (!_sipService.IsRegistered)
            {
                Console.WriteLine("[DEBUG] SIP service not registered - showing warning");
                File.AppendAllText(debugLog, $"[{DateTime.Now}] SIP service not registered - showing warning\n");
                _logger.LogWarning("CALL", "Attempted to make call without SIP registration");
                System.Windows.MessageBox.Show("Please register with SIP server first before making calls.", 
                    "Not Registered", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }try
            {
                Console.WriteLine($"[DEBUG] MakeCall started for: {DialedNumber}");
                _logger.LogAction("CALL", $"User initiated call to: {DialedNumber}");
                  // Create call entry in history
                var callEntry = new CallHistoryEntry
                {
                    Number = CallHistoryEntry.ExtractNumberPart(DialedNumber),
                    CallerName = CallHistoryEntry.ExtractDisplayName(DialedNumber),
                    CallType = CallType.Outgoing,
                    DateTime = DateTime.Now,
                    Duration = TimeSpan.Zero,
                    Status = CallStatus.InProgress
                };

                Console.WriteLine($"[DEBUG] Created call entry: {callEntry.Number}, Type: {callEntry.CallType}");
                
                _logger.LogSystemInfo("CALL_HISTORY", $"🔍 Adding call to history: {DialedNumber}");
                _logger.LogSystemInfo("CALL_HISTORY", $"🔍 CallHistory count before add: {CallHistory.Count}");
                
                Console.WriteLine($"[DEBUG] CallHistory count before: {CallHistory.Count}");
                
                // Ensure UI updates happen on UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CallHistory.Insert(0, callEntry);
                    Console.WriteLine($"[DEBUG] CallHistory count after insert: {CallHistory.Count}");
                    _logger.LogSystemInfo("CALL_HISTORY", $"🔍 CallHistory count after add: {CallHistory.Count}");
                    
                    ApplyCurrentFilter();
                    Console.WriteLine($"[DEBUG] FilteredCallHistory count after filter: {FilteredCallHistory.Count}");
                    _logger.LogSystemInfo("CALL_HISTORY", $"🔍 FilteredCallHistory count after filter: {FilteredCallHistory.Count}");
                    _logger.LogSystemInfo("CALL_HISTORY", $"🔍 Current filter: {_currentFilter}");
                });
                
                Console.WriteLine($"[DEBUG] About to save to database...");
                _callHistoryService.AddCall(WindowsSipPhone.Services.Data.CallHistoryEntry.FromUiModel(callEntry)); // Save to database
                Console.WriteLine($"[DEBUG] Database save completed");
                _logger.LogSystemInfo("CALL_HISTORY", $"🔍 Call saved to database: {DialedNumber}");// Make the actual SIP call
                await _sipService.MakeCallAsync(DialedNumber);
                
                _logger.LogSystemInfo("CALL", $"✅ Call initiated successfully to: {DialedNumber}");
                
                // Clear the dialed number after successful call initiation
                DialedNumber = "";
            }
            catch (Exception ex)
            {
                _logger.LogError("CALL", $"Call failed to {DialedNumber}: {ex.Message}");
                // Update call status to failed
                var failedCall = CallHistory.FirstOrDefault(c => c.Status == CallStatus.InProgress);
                if (failedCall != null)
                {
                    failedCall.Status = CallStatus.Failed;
                }

                System.Windows.MessageBox.Show($"Call failed: {ex.Message}", 
                    "Call Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }        private async Task HangupCall()
        {
            if (_sipService == null) return;

            try
            {
                _logger.LogAction("CALL", "User initiated hangup");
                
                // Hangup the active call
                await _sipService.HangupAsync();
                
                _logger.LogSystemInfo("CALL", "✅ Call hangup completed");
                
                // Ensure UI state is reset regardless of event handling
                // The event handler should also call EndCall(), but this ensures consistency
                EndCall();
            }
            catch (Exception ex)
            {
                _logger.LogError("CALL", $"Hangup failed: {ex.Message}");
                
                System.Windows.MessageBox.Show($"Hangup failed: {ex.Message}", 
                    "Hangup Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                
                // Even if hangup fails, reset the UI state
                EndCall();
            }
        }        private async Task ToggleHold()
        {
            if (_sipService == null || !_isCallActive) return;

            try
            {
                _logger.LogSystemInfo("CALL", $"Toggle hold requested - Current state: {(_isCallOnHold ? "On Hold" : "Active")}");
                
                if (_isCallOnHold)
                {
                    // Resume call
                    _logger.LogAction("CALL", "User initiated resume from hold");
                    
                    bool resumeResult = _sipService != null ? await _sipService.ResumeCallAsync() : false;
                    
                    if (resumeResult)
                    {
                        IsCallOnHold = false;
                        _logger.LogSystemInfo("CALL", "✅ Call resumed successfully");
                    }
                    else
                    {
                        _logger.LogError("CALL", "❌ Resume failed");
                    }
                }
                else
                {
                    // Hold call
                    _logger.LogAction("CALL", "User initiated hold");
                    
                    bool holdResult = _sipService != null ? await _sipService.HoldCallAsync() : false;
                    
                    if (holdResult)
                    {
                        IsCallOnHold = true;
                        _logger.LogSystemInfo("CALL", "✅ Call placed on hold successfully");
                    }
                    else
                    {
                        _logger.LogError("CALL", "❌ Hold failed");
                    }
                }
                
                _logger.LogSystemInfo("CALL", $"Final hold state: {(_isCallOnHold ? "On Hold" : "Active")}");
            }
            catch (Exception ex)
            {
                _logger.LogError("CALL", $"Toggle hold exception: {ex.Message}");
                
                System.Windows.MessageBox.Show($"Hold/Resume failed: {ex.Message}", 
                    "Call Control Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void FilterCalls(string? filter)
        {
            _currentFilter = filter ?? "All";
            ApplyCurrentFilter();
        }

        private void ApplyCurrentFilter()
        {
            FilteredCallHistory.Clear();
            
            var filtered = _currentFilter switch
            {
                "Outgoing" => CallHistory.Where(c => c.CallType == CallType.Outgoing),
                "Incoming" => CallHistory.Where(c => c.CallType == CallType.Incoming),
                "Missed" => CallHistory.Where(c => c.CallType == CallType.Missed),
                _ => CallHistory
            };

            foreach (var call in filtered)
            {
                FilteredCallHistory.Add(call);
            }
        }

        private bool CanRedial()
        {
            return SelectedCall != null;
        }        private void RedialCall()
        {
            if (SelectedCall != null)
            {
                _logger.LogAction("CALL", $"Redial selected: {SelectedCall.Number}");
                DialedNumber = SelectedCall.Number;
            }
        }        private void ClearCallHistory()
        {
            _logger.LogAction("UI", "Clear call history button pressed");
            
            // Clear database first
            _callHistoryService.ClearHistory();
            
            // Then clear UI collections
            CallHistory.Clear();
            FilteredCallHistory.Clear();
            
            _logger.LogSystemInfo("CALL_HISTORY", "✅ Call history cleared from database and UI");
        }        /// <summary>
        /// Unified keypad handler - adds digits when dialing, sends DTMF when in call
        /// </summary>
        private void HandleKeypadPress(string? digit)
        {
            if (string.IsNullOrEmpty(digit)) return;

            if (_isCallActive)
            {
                // During active call - send DTMF
                _logger.LogAction("UI", $"Keypad pressed: '{digit}' (sending DTMF)");
                SendDtmfDigit(digit[0]);
            }
            else
            {
                // When not in call - add to dialed number
                _logger.LogAction("UI", $"Keypad pressed: '{digit}' (adding to dialed number)");
                AddDigit(digit);
            }        }

        /// <summary>
        /// Send DTMF digit during active call
        /// </summary>
        private async void SendDtmfDigit(char digit)
        {
            try
            {                // Validate DTMF digit
                if (!"0123456789*#".Contains(digit))
                {
                    _logger.LogError("DTMF", $"Invalid DTMF digit: '{digit}'");
                    return;
                }

                // Show DTMF status
                IsDtmfActive = true;
                DtmfStatusText = $"📞 Sending DTMF: {digit}";
                _lastDtmfDigit = digit.ToString();

                _logger.LogAction("DTMF", $"User pressed '{digit}' during active call");

                // Send DTMF through audio manager
                var audioManager = _sipService?.SipClient?.AudioManager;
                if (audioManager != null)
                {
                    // Send RTP DTMF event
                    audioManager.SendDtmfDigit(digit);
                      // Play local tone for user feedback
                    audioManager.PlayDtmfTone(digit);
                    
                    _logger.LogSystemInfo("DTMF", $"✅ DTMF digit '{digit}' sent successfully");
                }
                else
                {
                    _logger.LogWarning("DTMF", "Audio manager not available");
                    DtmfStatusText = "❌ DTMF not available";
                }

                // Clear DTMF status after a short delay
                await Task.Delay(150);
                IsDtmfActive = false;
                DtmfStatusText = "";
            }            catch (Exception ex)
            {
                _logger.LogError("DTMF", $"Error sending DTMF digit '{digit}': {ex.Message}");
                DtmfStatusText = $"❌ DTMF Error: {digit}";
                  await Task.Delay(1000);
                IsDtmfActive = false;
                DtmfStatusText = "";
            }
        }

        /// <summary>
        /// Show dedicated DTMF keypad window during active call
        /// </summary>
        private void ShowDtmfKeypad()
        {
            try
            {
                if (!_isCallActive)
                {
                    _logger.LogWarning("UI", "DTMF keypad requested but no active call");
                    return;
                }

                _logger.LogAction("UI", "Opening DTMF keypad window");

                var dtmfWindow = new WindowsSipPhone.UI.Dialogs.DtmfKeypadWindow(_sipService, CallStatusText)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                dtmfWindow.Show();
            }
            catch (Exception ex)
            {
                _logger.LogError("UI", $"Error opening DTMF keypad: {ex.Message}");
            }
        }private void ExportCallHistoryToCsv()
        {
            try
            {
                _logger.LogAction("UI", "Export call history to CSV button pressed");
                
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"CallHistory_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    _callHistoryService.ExportToCsv(saveDialog.FileName);
                    
                    _logger.LogSystemInfo("CALL_HISTORY", $"✅ Call history exported to: {saveDialog.FileName}");
                    
                    System.Windows.MessageBox.Show(
                        $"Call history exported successfully to:\n{saveDialog.FileName}", 
                        "Export Complete", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogSystemInfo("CALL_HISTORY", "CSV export cancelled by user");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("CALL_HISTORY", $"Failed to export call history: {ex.Message}");
                
                System.Windows.MessageBox.Show(
                    $"Failed to export call history: {ex.Message}", 
                    "Export Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }        }

        #endregion

        #region Data Loading

        private void LoadCallHistory()
        {
            try
            {
                // Load call history from database
                var dbCalls = _callHistoryService.GetRecentCalls();
                
                CallHistory.Clear();
                foreach (var call in dbCalls)
                {
                    CallHistory.Add(call.ToUiModel());
                }
                
                // If database is empty, add sample data for demonstration
                if (CallHistory.Count == 0)
                {
                    _logger.LogSystemInfo("CALL_HISTORY", "Database empty, adding sample data for demonstration");                    var sampleCalls = new[]
                    {
                        new CallHistoryEntry { Number = "101", CallerName = "Alice", CallType = CallType.Incoming, DateTime = DateTime.Now.AddMinutes(-15), Duration = TimeSpan.FromMinutes(3), Status = CallStatus.Completed },
                        new CallHistoryEntry { Number = "102", CallerName = "Bob", CallType = CallType.Outgoing, DateTime = DateTime.Now.AddMinutes(-30), Duration = TimeSpan.FromMinutes(5), Status = CallStatus.Completed },
                        new CallHistoryEntry { Number = "105", CallType = CallType.Missed, DateTime = DateTime.Now.AddHours(-1), Duration = TimeSpan.Zero, Status = CallStatus.Missed },
                        new CallHistoryEntry { Number = "103", CallerName = "Charlie", CallType = CallType.Outgoing, DateTime = DateTime.Now.AddHours(-2), Duration = TimeSpan.FromMinutes(8), Status = CallStatus.Completed },
                    };

                    foreach (var call in sampleCalls)
                    {
                        CallHistory.Add(call); // UI model, no conversion needed
                        _callHistoryService.AddCall(WindowsSipPhone.Services.Data.CallHistoryEntry.FromUiModel(call)); // Save to database
                    }
                }
                
                ApplyCurrentFilter();
                _logger.LogSystemInfo("CALL_HISTORY", $"Loaded {CallHistory.Count} calls from database");
            }
            catch (Exception ex)
            {
                _logger.LogError("CALL_HISTORY", $"Failed to load call history: {ex.Message}");
            }        }

        // RefreshCallHistory and AddTestCall methods removed for cleaner UI
        // Call history is automatically updated when calls are made/received

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region Data Models

    public class CallHistoryEntry : INotifyPropertyChanged
    {
        private string _number = "";
        private string? _callerName; // New property
        private CallType _callType;
        private DateTime _dateTime;
        private TimeSpan _duration;
        private CallStatus _status;

        public string Number
        {
            get => _number;
            set
            {
                _number = ExtractNumberPart(value); // Always store just the number
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string? CallerName
        {
            get => _callerName;
            set
            {
                _callerName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public CallType CallType
        {
            get => _callType;
            set
            {
                _callType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CallTypeIcon));
                OnPropertyChanged(nameof(CallTypeColor));
            }
        }

        public DateTime DateTime
        {
            get => _dateTime;
            set
            {
                _dateTime = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                _duration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationText));
            }
        }

        public CallStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public string CallTypeIcon => CallType switch
        {
            CallType.Incoming => "📞←",    // Phone with left arrow (incoming)
            CallType.Outgoing => "📞→",    // Phone with right arrow (outgoing) - clean arrow  
            CallType.Missed => "📞❌",      // Phone with X mark (missed)
            _ => "📞→"
        };

        public string CallTypeColor => CallType switch
        {
            CallType.Incoming => "#3498DB",   // Blue for incoming
            CallType.Outgoing => "#27AE60",   // Green for outgoing  
            CallType.Missed => "#E74C3C",     // Red for missed
            _ => "#7F8C8D"
        };

        // DisplayName: show caller name if available, otherwise number
        public string DisplayName => !string.IsNullOrWhiteSpace(CallerName) ? CallerName : Number;        public static string ExtractDisplayName(string sipUri)
        {
            if (string.IsNullOrWhiteSpace(sipUri))
                return string.Empty;

            // Handle formats like: "Display Name" <sip:user@domain> or Display Name <sip:user@domain>
            if (sipUri.Contains("<") && sipUri.Contains(">"))
            {
                var displayPart = sipUri.Substring(0, sipUri.IndexOf("<")).Trim();
                // Remove surrounding quotes if present
                displayPart = displayPart.Trim('"').Trim();
                return string.IsNullOrWhiteSpace(displayPart) ? string.Empty : displayPart;
            }
            
            // If it's just a SIP URI like sip:user@domain, no display name
            if (sipUri.StartsWith("sip:") || sipUri.Contains("@"))
            {
                return string.Empty;
            }
            
            // If it's just a plain number, no display name
            if (sipUri.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' ' || c == '(' || c == ')'))
            {
                return string.Empty;
            }
            
            // Otherwise, treat the whole thing as a display name (fallback case)
            return sipUri.Trim();
        }        public static string ExtractNumberPart(string sipUri)
        {
            if (string.IsNullOrWhiteSpace(sipUri))
                return "Unknown Number";

            // Handle formats like: "Display Name" <sip:user@domain> or <sip:user@domain>
            if (sipUri.Contains("<") && sipUri.Contains(">"))
            {
                var start = sipUri.IndexOf("<") + 1;
                var end = sipUri.IndexOf(">");
                if (end > start)
                {
                    var uri = sipUri.Substring(start, end - start).Trim();
                    return ExtractNumberFromSipUri(uri);
                }
            }
            
            // Handle direct SIP URI like sip:user@domain
            if (sipUri.StartsWith("sip:"))
            {
                return ExtractNumberFromSipUri(sipUri);
            }
            
            // Handle URI with @ but no sip: prefix
            if (sipUri.Contains("@"))
            {
                var atIndex = sipUri.IndexOf("@");
                return sipUri.Substring(0, atIndex).Trim();
            }
            
            // If it looks like a phone number (digits, +, -, spaces, parentheses), return as is
            if (sipUri.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' ' || c == '(' || c == ')'))
            {
                return sipUri.Trim();
            }
            
            // If we can't extract a number, return "Unknown Number"
            return "Unknown Number";
        }

        private static string ExtractNumberFromSipUri(string sipUri)
        {
            // Extract user part from sip:user@domain
            if (sipUri.StartsWith("sip:"))
            {
                var withoutScheme = sipUri.Substring(4);
                var atIndex = withoutScheme.IndexOf('@');
                if (atIndex > 0)
                {
                    return withoutScheme.Substring(0, atIndex);
                }
                return withoutScheme;
            }
            
            // Handle case where it's just user@domain without sip: prefix
            var atIdx = sipUri.IndexOf('@');
            if (atIdx > 0)
            {
                return sipUri.Substring(0, atIdx);
            }
            
            return sipUri;
        }

        public string DurationText => Duration.TotalSeconds < 1 ? "0s" : 
                                    Duration.TotalMinutes < 1 ? $"{Duration.Seconds}s" :
                                    $"{Duration.Minutes}:{Duration.Seconds:D2}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum CallType
    {
        Incoming,
        Outgoing,
        Missed
    }

    public enum CallStatus
    {
        InProgress,
        Completed,
        Missed,        Failed    
    }

    #endregion
}
