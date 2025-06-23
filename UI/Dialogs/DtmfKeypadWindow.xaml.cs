using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WindowsSipPhone.Services.Logging;

namespace WindowsSipPhone.UI.Dialogs
{
    public partial class DtmfKeypadWindow : Window, INotifyPropertyChanged
    {
        private readonly ApplicationLogger _logger;
        private SipPhoneService? _sipService;
        private bool _isDtmfActive = false;
        private string _dtmfStatusText = "";
        private string _callStatusText = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public DtmfKeypadWindow(SipPhoneService? sipService, string callStatus)
        {
            _logger = ApplicationLogger.Instance;
            _sipService = sipService;
            _callStatusText = callStatus;
            
            InitializeComponent();
            DataContext = this;
            InitializeCommands();
            
            _logger.LogAction("UI", "DTMF keypad window opened");
        }

        private void InitializeCommands()
        {
            DtmfKeypadCommand = new RelayCommand<string>(ExecuteDtmfKeypad);
        }

        // Properties
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

        public string CallStatusText
        {
            get => _callStatusText;
            set
            {
                _callStatusText = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand DtmfKeypadCommand { get; private set; } = null!;

        private async void ExecuteDtmfKeypad(string? parameter)
        {
            if (string.IsNullOrEmpty(parameter) || parameter.Length != 1)
                return;

            char digit = parameter[0];
            
            try
            {
                // Validate DTMF digit
                if (!"0123456789*#".Contains(digit))
                {
                    _logger.LogError("DTMF", $"Invalid DTMF digit: '{digit}'");
                    return;
                }

                // Show DTMF status
                IsDtmfActive = true;
                DtmfStatusText = $"📞 Sending DTMF: {digit}";

                _logger.LogAction("DTMF", $"DTMF keypad - User pressed '{digit}' during active call");

                // Send DTMF through audio manager
                var audioManager = _sipService?.SipClient?.AudioManager;
                if (audioManager != null)
                {
                    // Send RTP DTMF event
                    audioManager.SendDtmfDigit(digit);
                    
                    // Play local tone for user feedback
                    audioManager.PlayDtmfTone(digit);
                    
                    _logger.LogSystemInfo("DTMF", $"✅ DTMF digit '{digit}' sent successfully from keypad");
                }
                else
                {
                    _logger.LogWarning("DTMF", "Audio manager not available");
                    DtmfStatusText = "❌ DTMF not available";
                }

                // Clear DTMF status after a short delay
                await System.Threading.Tasks.Task.Delay(150);
                IsDtmfActive = false;
                DtmfStatusText = "";
            }
            catch (Exception ex)
            {
                _logger.LogError("DTMF", $"Error sending DTMF digit '{digit}' from keypad: {ex.Message}");
                DtmfStatusText = $"❌ DTMF Error: {digit}";
                
                await System.Threading.Tasks.Task.Delay(1000);
                IsDtmfActive = false;
                DtmfStatusText = "";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogAction("UI", "DTMF keypad window closed");
            Close();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Handle window closing
        protected override void OnClosing(CancelEventArgs e)
        {
            _logger.LogAction("UI", "DTMF keypad window closing");
            base.OnClosing(e);
        }        // Handle keyboard input for the DTMF window
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // Allow ESC to close the window
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            // Handle numeric keypad and regular number keys for DTMF
            char? dtmfChar = null;
              switch (e.Key)
            {
                case Key.D0:
                case Key.NumPad0:
                    dtmfChar = '0';
                    break;
                case Key.D1:
                case Key.NumPad1:
                    dtmfChar = '1';
                    break;
                case Key.D2:
                case Key.NumPad2:
                    dtmfChar = '2';
                    break;
                case Key.D3:
                case Key.NumPad3:
                    if (Keyboard.Modifiers == ModifierKeys.Shift) // # key (Shift+3)
                        dtmfChar = '#';
                    else
                        dtmfChar = '3';
                    break;
                case Key.D4:
                case Key.NumPad4:
                    dtmfChar = '4';
                    break;
                case Key.D5:
                case Key.NumPad5:
                    dtmfChar = '5';
                    break;
                case Key.D6:
                case Key.NumPad6:
                    dtmfChar = '6';
                    break;
                case Key.D7:
                case Key.NumPad7:
                    dtmfChar = '7';
                    break;
                case Key.D8:
                case Key.NumPad8:
                    if (Keyboard.Modifiers == ModifierKeys.Shift) // * key (Shift+8)
                        dtmfChar = '*';
                    else
                        dtmfChar = '8';
                    break;
                case Key.D9:
                case Key.NumPad9:
                    dtmfChar = '9';
                    break;
                case Key.Multiply:
                    dtmfChar = '*';
                    break;
                case Key.OemQuestion: // # key
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        dtmfChar = '#';
                    break;
            }

            if (dtmfChar.HasValue)
            {
                ExecuteDtmfKeypad(dtmfChar.Value.ToString());
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
    }

    // RelayCommand implementation for the DTMF window
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;

        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke((T?)parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute((T?)parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
