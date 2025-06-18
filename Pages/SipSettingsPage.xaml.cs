using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsSipPhone.Commands;
using WindowsSipPhone.Models;

namespace WindowsSipPhone.Pages
{
    public partial class SipSettingsPage : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        private string _username = "103";
        private string _serverHost = "192.168.1.180";
        private string _serverPort = "5060";
        private string _selectedTransport = "TCP";
        private string _registrationExpires = "300";
        private string _userAgent = "Windows-SIP-Phone/2.0";
        private string _timerT1 = "500";
        private string _timerT2 = "4000";
        private string _timerT4 = "5000";
        private string _registrationStatus = "Not Registered";
        private string _statusDetails = "Configure settings and click Register to connect";
        private DateTime _lastUpdated = DateTime.Now;
        private bool _isRegistered = false;
        private SipPhoneService? _sipService;
        
        // Profile system properties
        private List<SipProfile> _availableProfiles = new();
        private SipProfile _selectedProfile = SipProfile.GetDefaultProfile();

        public SipSettingsPage()
        {
            InitializeComponent();
            DataContext = this;
            InitializeProfiles();
            InitializeCommands();
            LoadSettings();
        }

        public SipPhoneService? SipService 
        { 
            get => _sipService;
            set
            {
                _sipService = value;
                if (_sipService != null)
                {
                    _sipService.StatusChanged += OnSipStatusChanged;
                    UpdateRegistrationStatus();
                }
            }
        }

        public void SetSipService(SipPhoneService sipService)
        {
            SipService = sipService;
        }

        #region Properties

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        public string ServerHost
        {
            get => _serverHost;
            set
            {
                _serverHost = value;
                OnPropertyChanged();
            }
        }

        public string ServerPort
        {
            get => _serverPort;
            set
            {
                _serverPort = value;
                OnPropertyChanged();
            }
        }

        public string SelectedTransport
        {
            get => _selectedTransport;
            set
            {
                _selectedTransport = value;
                OnPropertyChanged();
            }
        }

        public string RegistrationExpires
        {
            get => _registrationExpires;
            set
            {
                _registrationExpires = value;
                OnPropertyChanged();
            }
        }

        public string UserAgent
        {
            get => _userAgent;
            set
            {
                _userAgent = value;
                OnPropertyChanged();
            }
        }

        public string TimerT1
        {
            get => _timerT1;
            set
            {
                _timerT1 = value;
                OnPropertyChanged();
            }
        }

        public string TimerT2
        {
            get => _timerT2;
            set
            {
                _timerT2 = value;
                OnPropertyChanged();
            }
        }

        public string TimerT4
        {
            get => _timerT4;
            set
            {
                _timerT4 = value;
                OnPropertyChanged();
            }
        }

        public string RegistrationStatus
        {
            get => _registrationStatus;
            set
            {
                _registrationStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBackgroundBrush));
            }
        }

        public string StatusDetails
        {
            get => _statusDetails;
            set
            {
                _statusDetails = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set
            {
                _lastUpdated = value;
                OnPropertyChanged();
            }
        }

        public System.Windows.Media.Brush StatusBackgroundBrush
        {
            get
            {                return _isRegistered switch
                {
                    true => new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)), // Green
                    false when RegistrationStatus.Contains("Error") || RegistrationStatus.Contains("Failed") => 
                        new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)), // Red
                    false when RegistrationStatus.Contains("Connecting") || RegistrationStatus.Contains("Registering") => 
                        new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)), // Blue
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(149, 165, 166)) // Gray
                };
            }
        }
        
        // Profile System Properties
        public List<SipProfile> AvailableProfiles
        {
            get => _availableProfiles;
            set
            {
                _availableProfiles = value;
                OnPropertyChanged();
            }
        }
        
        public SipProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                _selectedProfile = value;
                OnPropertyChanged();
                OnProfileChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand RegisterCommand { get; private set; } = null!;
        public ICommand UnregisterCommand { get; private set; } = null!;
        public ICommand SaveSettingsCommand { get; private set; } = null!;
        public ICommand ResetSettingsCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            RegisterCommand = new RelayCommand(RegisterSip, CanRegister);
            UnregisterCommand = new RelayCommand(UnregisterSip, CanUnregister);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ResetSettingsCommand = new RelayCommand(ResetSettings);
        }

        #endregion

        #region Command Implementations

        private bool CanRegister()
        {
            return !string.IsNullOrWhiteSpace(Username) && 
                   !string.IsNullOrWhiteSpace(ServerHost) && 
                   !string.IsNullOrWhiteSpace(ServerPort) &&
                   !_isRegistered;
        }

        private bool CanUnregister()
        {
            return _isRegistered;
        }

        private async void RegisterSip()
        {
            if (_sipService == null)
            {
                StatusDetails = "SIP service not available";
                return;
            }

            try
            {
                RegistrationStatus = "Registering...";
                StatusDetails = $"Connecting to {ServerHost}:{ServerPort} using profile '{SelectedProfile.Name}'";
                LastUpdated = DateTime.Now;

                var password = PasswordBox.Password;
                if (string.IsNullOrWhiteSpace(password))
                {
                    StatusDetails = "Password is required";
                    RegistrationStatus = "Registration Failed";
                    return;
                }                if (!int.TryParse(ServerPort, out var port))
                {
                    StatusDetails = "Invalid port number";
                    RegistrationStatus = "Registration Failed";
                    return;
                }

                if (!int.TryParse(RegistrationExpires, out var expires) || expires <= 0)
                {
                    StatusDetails = "Invalid registration expires value";
                    RegistrationStatus = "Registration Failed";
                    return;
                }

                // Use profile-based registration
                await _sipService.RegisterWithProfileAsync(Username, password, ServerHost, port, SelectedProfile, expires);
            }
            catch (Exception ex)
            {
                RegistrationStatus = "Registration Failed";
                StatusDetails = $"Error: {ex.Message}";
                LastUpdated = DateTime.Now;
            }
        }

        private async void UnregisterSip()
        {
            if (_sipService == null) return;

            try
            {
                RegistrationStatus = "Unregistering...";
                StatusDetails = "Disconnecting from server";
                LastUpdated = DateTime.Now;

                await _sipService.UnregisterAsync();

                _isRegistered = false;
                RegistrationStatus = "Not Registered";
                StatusDetails = "Successfully disconnected";
                LastUpdated = DateTime.Now;
            }            catch (Exception ex)
            {
                StatusDetails = $"Unregister error: {ex.Message}";
                LastUpdated = DateTime.Now;
            }
        }

        private void SaveSettings()
        {
            try
            {
                var config = new SipConfiguration
                {
                    Username = Username,
                    ServerHost = ServerHost,
                    ServerPort = ServerPort,
                    Transport = SelectedTransport,
                    RememberCredentials = true, // Could be a checkbox in UI
                    AutoRegisterOnStartup = false,
                    SelectedProfileName = SelectedProfile.Name
                };
                
                config.Save();
                StatusDetails = "✅ Settings saved successfully";
                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                StatusDetails = $"❌ Save error: {ex.Message}";
                LastUpdated = DateTime.Now;
            }
        }

        private void ResetSettings()
        {
            Username = "103";
            ServerHost = "192.168.1.180";
            ServerPort = "5060";
            SelectedTransport = "TCP";
            RegistrationExpires = "300";
            UserAgent = "Windows-SIP-Phone/2.0";
            TimerT1 = "500";
            TimerT2 = "4000";
            TimerT4 = "5000";
            PasswordBox.Password = "";
            
            StatusDetails = "Settings reset to defaults";
            LastUpdated = DateTime.Now;
        }

        #endregion

        #region Event Handlers

        private void OnSipStatusChanged(object? sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusDetails = status;
                LastUpdated = DateTime.Now;

                if (status.Contains("Registration successful") || status.Contains("✅"))
                {
                    _isRegistered = true;
                    RegistrationStatus = "Registered";
                }
                else if (status.Contains("Registration failed") || status.Contains("❌") || status.Contains("Authentication failed"))
                {
                    _isRegistered = false;
                    RegistrationStatus = "Registration Failed";
                }
                else if (status.Contains("Unregistered") || status.Contains("Disconnected"))
                {
                    _isRegistered = false;
                    RegistrationStatus = "Not Registered";
                }
                else if (status.Contains("Connecting") || status.Contains("Registering"))
                {
                    RegistrationStatus = "Connecting...";
                }
            });
        }

        private void UpdateRegistrationStatus()
        {
            if (_sipService != null)
            {
                _isRegistered = _sipService.IsRegistered;
                RegistrationStatus = _isRegistered ? "Registered" : "Not Registered";
                StatusDetails = _isRegistered ? $"Connected to {_sipService.ServerAddress}" : "Ready to register";
                LastUpdated = DateTime.Now;
            }
        }

        #endregion

        #region Helper Methods

        private void LoadSettings()
        {
            try
            {
                var config = SipConfiguration.Load();
                
                Username = config.Username;
                ServerHost = config.ServerHost;
                ServerPort = config.ServerPort;
                SelectedTransport = config.Transport;
                
                // Load selected profile
                var selectedProfile = config.GetSelectedProfile();
                SelectedProfile = selectedProfile;
                
                StatusDetails = $"Settings loaded from configuration (Profile: {selectedProfile.Name})";
                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                StatusDetails = $"Failed to load settings: {ex.Message}";
                LastUpdated = DateTime.Now;
                
                // Use defaults if loading fails
                Username = "103";
                ServerHost = "192.168.1.180";
                ServerPort = "5060";
                SelectedTransport = "TCP";
            }        }

        #endregion
        
        #region Profile Management
        
        private void InitializeProfiles()
        {
            // Load predefined profiles
            AvailableProfiles = SipProfile.GetPredefinedProfiles();
            
            // Load selected profile from configuration
            var config = SipConfiguration.Load();
            var selectedProfile = config.GetSelectedProfile();
            SelectedProfile = selectedProfile;
        }
        
        private void OnProfileChanged()
        {
            if (_selectedProfile != null)
            {
                // Update UI fields based on selected profile
                RegistrationExpires = _selectedProfile.RegistrationExpiry.ToString();
                UserAgent = _selectedProfile.UserAgentString;
                SelectedTransport = _selectedProfile.Transport;
                
                StatusDetails = $"Profile '{_selectedProfile.Name}' selected - {_selectedProfile.Description}";
                LastUpdated = DateTime.Now;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}