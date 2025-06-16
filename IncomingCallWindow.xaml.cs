using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace WindowsSipPhone
{
    /// <summary>
    /// Incoming call notification window with accept/decline functionality
    /// </summary>
    public partial class IncomingCallWindow : Window, INotifyPropertyChanged
    {
        private string _callerNumber = "";
        private string _callerName = "Unknown Caller";        private string _duration = "Incoming...";
        private DateTime _callStartTime;
        private DispatcherTimer? _durationTimer;
        private bool _callAccepted = false;
        private IRingtoneService? _ringtoneService;
        
        public event EventHandler<bool>? CallAnswered;
          public string CallerNumber
        {
            get => _callerNumber;
            set
            {
                _callerNumber = value;
                Console.WriteLine($"[INCOMING CALL DEBUG] CallerNumber set to: '{value}'");
                OnPropertyChanged();
            }
        }
        
        public string CallerName
        {
            get => _callerName;
            set
            {
                _callerName = value;
                Console.WriteLine($"[INCOMING CALL DEBUG] CallerName set to: '{value}'");
                OnPropertyChanged();
            }
        }
        
        public string Duration
        {
            get => _duration;
            set
            {
                _duration = value;
                OnPropertyChanged();
            }
        }        public IncomingCallWindow(string callerInfo, IRingtoneService? ringtoneService = null)
        {
            Console.WriteLine($"[INCOMING CALL DEBUG] IncomingCallWindow constructor called with: '{callerInfo}'");
            
            InitializeComponent();
            
            Console.WriteLine($"[INCOMING CALL DEBUG] Setting DataContext to this");
            DataContext = this;
            
            _ringtoneService = ringtoneService;
            
            Console.WriteLine($"[INCOMING CALL DEBUG] Initial values - Name: '{_callerName}', Number: '{_callerNumber}'");
            
            ParseCallerInfo(callerInfo);
            StartDurationTimer();
            
            Console.WriteLine($"[INCOMING CALL DEBUG] After parsing - Name: '{CallerName}', Number: '{CallerNumber}'");
            
            // Start playing ringtone
            Console.WriteLine($"[INCOMING CALL DEBUG] IncomingCallWindow created, starting ringtone");
            _ringtoneService?.PlayRingtone();
            Console.WriteLine($"[INCOMING CALL DEBUG] Ringtone play command sent");
            
            // Window settings for incoming call
            this.Topmost = true;
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.Focus();
        }        private void ParseCallerInfo(string callerInfo)
        {
            Console.WriteLine($"[INCOMING CALL DEBUG] Parsing caller info: '{callerInfo}'");
            
            string displayName = "";
            string actualNumber = "";
            
            // Parse SIP URI format: sip:user@domain or "Name" <sip:user@domain>
            if (callerInfo.Contains("<") && callerInfo.Contains(">"))
            {
                // Format: "Display Name" <sip:user@domain>
                var nameEnd = callerInfo.IndexOf('<');
                if (nameEnd > 0)
                {
                    displayName = callerInfo.Substring(0, nameEnd).Trim().Trim('"');
                    Console.WriteLine($"[INCOMING CALL DEBUG] Extracted display name: '{displayName}'");
                }
                
                var uriStart = callerInfo.IndexOf('<') + 1;
                var uriEnd = callerInfo.IndexOf('>');
                if (uriEnd > uriStart)
                {
                    var uri = callerInfo.Substring(uriStart, uriEnd - uriStart);
                    actualNumber = ExtractNumberFromUri(uri);
                    Console.WriteLine($"[INCOMING CALL DEBUG] Extracted number from URI: '{actualNumber}'");
                }
            }
            else if (callerInfo.StartsWith("sip:"))
            {
                // Format: sip:user@domain
                actualNumber = ExtractNumberFromUri(callerInfo);
                Console.WriteLine($"[INCOMING CALL DEBUG] SIP format - Number: '{actualNumber}'");
            }
            else
            {
                // Fallback: use as-is
                actualNumber = callerInfo;
                Console.WriteLine($"[INCOMING CALL DEBUG] Fallback format - Number: '{actualNumber}'");
            }
            
            // Set display values according to requirements:
            // First row: Name if available, otherwise number
            // Second row: Always the incoming number
            CallerName = !string.IsNullOrWhiteSpace(displayName) ? displayName : actualNumber;
            CallerNumber = actualNumber;
            
            Console.WriteLine($"[INCOMING CALL DEBUG] Final display - Name: '{CallerName}', Number: '{CallerNumber}'");
        }
        
        private string ExtractNumberFromUri(string sipUri)
        {
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
            return sipUri;
        }
        
        private void StartDurationTimer()
        {
            _callStartTime = DateTime.Now;
            _durationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _durationTimer.Tick += DurationTimer_Tick;
            _durationTimer.Start();
        }
        
        private void DurationTimer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _callStartTime;
            Duration = $"Ringing: {elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            _callAccepted = true;
            _durationTimer?.Stop();
            Console.WriteLine($"[INCOMING CALL DEBUG] Accept clicked, stopping ringtone");
            _ringtoneService?.StopRingtone(); // Stop ringtone when call is accepted
            Duration = "Call accepted";
            
            CallAnswered?.Invoke(this, true);
            this.Close();
        }
          private void Decline_Click(object sender, RoutedEventArgs e)
        {
            _callAccepted = false;
            _durationTimer?.Stop();
            Console.WriteLine($"[INCOMING CALL DEBUG] Decline clicked, stopping ringtone");
            _ringtoneService?.StopRingtone(); // Stop ringtone when call is declined
            Duration = "Call declined";
            
            CallAnswered?.Invoke(this, false);
            this.Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _durationTimer?.Stop();
            Console.WriteLine($"[INCOMING CALL DEBUG] Window closing, stopping ringtone");
            _ringtoneService?.StopRingtone(); // Stop ringtone when window closes
            
            // If window is closed without accepting/declining, treat as decline
            if (!_callAccepted && DialogResult != true && DialogResult != false)
            {
                CallAnswered?.Invoke(this, false);
                DialogResult = false;
            }
            
            base.OnClosing(e);
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
