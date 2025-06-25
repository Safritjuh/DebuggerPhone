using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using WindowsSipPhone.Core.Utilities;
using WindowsSipPhone.Core.Models;
using WindowsSipPhone.Core.Managers;
using System.Linq;
using System.Text;
using WindowsSipPhone.Services.Data;

namespace WindowsSipPhone.UI.Pages
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
          // Profile Management (IMP-016)
        private EnhancedProfileManager? _profileManager;
        private List<string> _availableProfiles = new();
        private string _selectedProfile = "Generic";

        // Reference to PasswordBox control
        private PasswordBox PasswordBoxRef;        public SipSettingsPage()
        {            InitializeComponent();
            DataContext = this;
            // Assign PasswordBoxRef after InitializeComponent
            PasswordBoxRef = (PasswordBox)this.FindName("PasswordBox");
            InitializeProfiles(); // Initialize profile system
            InitializeCommands();
            LoadSettings();
        }        public SipPhoneService? SipService 
        { 
            get => _sipService;
            set
            {
                _sipService = value;
                if (_sipService != null)
                {
                    _sipService.StatusChanged += OnSipStatusChanged;
                    UpdateRegistrationStatus();
                      // Sync profile selection with SIP service
                    if (_sipService.ProfileManager != null)
                    {
                        var serviceProfiles = _sipService.GetAvailableProfiles();
                        AvailableProfiles = serviceProfiles.ToList();
                        SelectedProfile = _sipService.CurrentProfileName;
                    }
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
        public List<string> AvailableProfiles
        {
            get => _availableProfiles;
            set
            {
                _availableProfiles = value;
                OnPropertyChanged();
            }
        }
        
        public string SelectedProfile
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
        public ICommand ExportProfileCommand { get; private set; } = null!;
        public ICommand ImportProfileCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            RegisterCommand = new RelayCommand(RegisterSip, CanRegister);
            UnregisterCommand = new RelayCommand(UnregisterSip, CanUnregister);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ResetSettingsCommand = new RelayCommand(ResetSettings);
            ExportProfileCommand = new RelayCommand(ExportProfile);
            ImportProfileCommand = new RelayCommand(ImportProfile);
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
            }            try
            {
                RegistrationStatus = "Registering...";
                StatusDetails = $"Connecting to {ServerHost}:{ServerPort} using profile '{SelectedProfile}'";
                LastUpdated = DateTime.Now;

                var password = PasswordBoxRef.Password;
                if (string.IsNullOrWhiteSpace(password))
                {
                    StatusDetails = "Password is required";
                    RegistrationStatus = "Registration Failed";
                    return;
                }

                if (!int.TryParse(ServerPort, out var port))
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
                }                // Use enhanced profile-based registration
                Console.WriteLine($"[DEBUG] Looking up profile: '{SelectedProfile}'");
                var profile = WindowsSipPhone.Core.Models.SipProfile.GetPredefinedProfile(SelectedProfile);
                if (profile == null)
                {
                    Console.WriteLine($"[DEBUG] Profile '{SelectedProfile}' not found!");
                    RegistrationStatus = "Registration Failed";
                    StatusDetails = $"Profile '{SelectedProfile}' not found";
                    LastUpdated = DateTime.Now;
                    return;
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Profile '{SelectedProfile}' found successfully!");
                }
                
                await _sipService.RegisterWithProfileAsync(Username, password, ServerHost, port, profile, expires);
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
        }        private void SaveSettings()
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
                    SelectedProfileName = SelectedProfile
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
            // Reset to Generic profile (default)
            SelectedProfile = "Generic";
            
            // Reset basic settings
            Username = "103";
            ServerHost = "192.168.1.180";
            ServerPort = "5060";
            PasswordBoxRef.Password = "";
            
            // Profile-specific settings will be set by OnProfileChanged
            
            StatusDetails = "Settings reset to defaults with Generic profile";
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
                
                // Load selected profile name
                SelectedProfile = config.SelectedProfileName ?? "Generic";
                
                StatusDetails = $"Settings loaded from configuration (Profile: {SelectedProfile})";
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
                SelectedProfile = "Generic";
            }
        }

        #endregion
          #region Profile Management
        
        private void InitializeProfiles()
        {
            try
            {                _profileManager = new EnhancedProfileManager();
                AvailableProfiles = _profileManager.GetAvailableProfiles().ToList();
                
                // DEBUG: Log available profiles to console
                Console.WriteLine($"[DEBUG] InitializeProfiles: {AvailableProfiles.Count} profiles loaded:");
                foreach (var profile in AvailableProfiles)
                {
                    Console.WriteLine($"[DEBUG]   - '{profile}'");
                }
                
                // Set default profile or load from SIP service
                if (_sipService?.ProfileManager != null)
                {
                    SelectedProfile = _sipService.CurrentProfileName;
                }
                else
                {
                    SelectedProfile = AvailableProfiles.FirstOrDefault() ?? "Generic";
                }
                
                StatusDetails = $"Enhanced profile system initialized - {AvailableProfiles.Count} profiles available";
                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                StatusDetails = $"Failed to initialize enhanced profiles: {ex.Message}";
                LastUpdated = DateTime.Now;
                
                // Fallback to at least generic profile
                AvailableProfiles = new List<string> { "Generic" };
                SelectedProfile = "Generic";
            }        }
          private async void OnProfileChanged()
        {
            if (string.IsNullOrEmpty(_selectedProfile)) return;
            
            try
            {
                StatusDetails = $"Switching to enhanced profile: {_selectedProfile}";
                LastUpdated = DateTime.Now;
                
                // Load the profile using the enhanced profile manager
                if (_profileManager != null)
                {
                    _profileManager.LoadProfile(_selectedProfile);
                    
                    // Auto-fill Advanced SIP settings from profile (IMP-017)
                    AutoFillAdvancedSettingsFromProfile();
                }
                
                // If we have a SIP service, switch the profile
                if (_sipService != null)
                {
                    var success = await _sipService.SwitchProfileAsync(_selectedProfile);
                    if (success)
                    {
                        StatusDetails = $"✅ Successfully switched to profile: {_selectedProfile}";
                        
                        // If currently registered, may need to re-register with new profile settings
                        if (_isRegistered)
                        {
                            StatusDetails += " - Re-registration may be required for full effect";
                        }
                    }
                    else
                    {
                        StatusDetails = $"❌ Failed to switch to profile: {_selectedProfile}";
                    }
                }
                else
                {
                    StatusDetails = $"Profile '{_selectedProfile}' selected - will be applied when connecting";
                }
                
                LastUpdated = DateTime.Now;
                OnPropertyChanged(nameof(ProfileDetails));
            }
            catch (Exception ex)
            {
                StatusDetails = $"Error switching profile: {ex.Message}";
                LastUpdated = DateTime.Now;
            }        }
        
        /// <summary>
        /// Auto-fills Advanced SIP settings from the selected profile configuration (IMP-017)
        /// Settings can still be overridden by the user after auto-fill
        /// </summary>
        private void AutoFillAdvancedSettingsFromProfile()
        {
            if (_profileManager?.CurrentConfig == null) return;
            
            try
            {
                var config = _profileManager.CurrentConfig;
                
                // Auto-fill Registration Expires from profile
                if (config.RegistrationRefreshInterval > 0)
                {
                    RegistrationExpires = config.RegistrationRefreshInterval.ToString();
                }
                
                // Auto-fill User Agent from profile
                if (!string.IsNullOrWhiteSpace(config.CustomUserAgent))
                {
                    UserAgent = config.CustomUserAgent;
                }
                
                // Auto-fill Transport from profile
                if (!string.IsNullOrWhiteSpace(config.PreferredTransport))
                {
                    // Normalize transport value to match ComboBox options
                    var transport = config.PreferredTransport.ToUpperInvariant();
                    if (transport == "TCP" || transport == "UDP" || transport == "TLS")
                    {
                        SelectedTransport = transport;
                    }
                }
                
                // Auto-fill Server Port from profile
                if (config.Port > 0)
                {
                    ServerPort = config.Port.ToString();
                }
                
                // Update status to show auto-fill occurred
                StatusDetails = $"Advanced SIP settings auto-filled from profile '{_selectedProfile}'. Settings can be customized as needed.";
                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                StatusDetails = $"Warning: Could not auto-fill some settings from profile: {ex.Message}";
                LastUpdated = DateTime.Now;
            }
        }
          /// <summary>
        /// Gets detailed information about the selected enhanced profile
        /// </summary>
        public string ProfileDetails
        {
            get
            {
                if (_profileManager == null || string.IsNullOrEmpty(_selectedProfile)) 
                    return "No profile selected";
                  
                try
                {
                    var config = _profileManager.CurrentConfig;
                    if (config == null) return "Profile configuration not available";
                      
                    var details = new StringBuilder();
                    details.AppendLine($"📋 Enhanced Profile: {config.Name}");
                    details.AppendLine($"📝 Description: {config.Description}");
                    details.AppendLine($"🏷️ Protocol: {config.Protocol}");
                    details.AppendLine($"� Port: {config.Port}");
                    
                    if (config.IsCustom)
                    {
                        details.AppendLine("⚙️ Type: Custom Profile");
                    }
                    
                    // Add SIP handling information
                    details.AppendLine();
                    details.AppendLine("🔧 SIP Configuration:");
                    details.AppendLine($"   User Agent: {config.CustomUserAgent}");
                    details.AppendLine($"   Transport: {config.PreferredTransport}");
                    details.AppendLine($"   Registration Interval: {config.RegistrationRefreshInterval}s");
                    details.AppendLine($"   Max Forwards: {config.MaxForwards}");
                    
                    if (config.SupportedCodecs.Any())
                    {
                        details.AppendLine($"   Codecs: {string.Join(", ", config.SupportedCodecs)}");
                    }
                    
                    if (config.CustomHeaders.Any())
                    {
                        details.AppendLine($"   Custom Headers: {config.CustomHeaders.Count}");
                    }
                    
                    return details.ToString().Trim();
                }
                catch
                {
                    return "Error loading profile details";
                }
            }
        }          private void ExportProfile()
        {
            try
            {
                StatusDetails = "Export functionality not yet implemented for enhanced profile system";
                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                StatusDetails = $"❌ Export failed: {ex.Message}";
                LastUpdated = DateTime.Now;
            }
        }
        
        private void ImportProfile()
        {
            try
            {
                StatusDetails = "Import functionality not yet implemented for enhanced profile system";                LastUpdated = DateTime.Now;
            }
            catch (Exception ex)
            {
                StatusDetails = $"❌ Import failed: {ex.Message}";
                LastUpdated = DateTime.Now;
            }
        }

        #endregion

        #region Mouse Wheel Event Handling

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // Scroll the ScrollViewer directly with improved responsiveness
                double scrollAmount = e.Delta * 1.5; // Increased sensitivity for better UX
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollAmount);
                e.Handled = true;
            }
        }

        private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Find the parent ScrollViewer
            var scrollViewer = FindParent<ScrollViewer>((DependencyObject)sender);
            if (scrollViewer != null)
            {
                // Forward the mouse wheel event to the ScrollViewer with improved responsiveness
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (e.Delta * 1.5));
                e.Handled = true;
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            
            if (parent == null)
                return null;
            
            if (parent is T parentAsT)
                return parentAsT;
            
            return FindParent<T>(parent);
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