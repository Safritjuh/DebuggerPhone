using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;

namespace WindowsSipPhone;

public partial class SipAccountDialog : Window
{
    public SipAccountDialog()
    {
        InitializeComponent();
        
        // Set default password for testing
        PasswordBox.Password = "274104";
    }
    
    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        TestConnectionButton.IsEnabled = false;
        TestResultText.Text = "Testing connection...";
        
        try
        {
            var server = ServerTextBox.Text;
            var port = int.Parse(PortTextBox.Text);
            
            TestResultText.Text = $"Connecting to {server}:{port}...";
            
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(IPAddress.Parse(server), port);
            var timeoutTask = Task.Delay(5000); // 5 second timeout
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == connectTask && tcpClient.Connected)
            {
                TestResultText.Text = $"✅ Successfully connected to {server}:{port}\nServer is reachable via TCP";
            }
            else
            {
                TestResultText.Text = $"❌ Connection timeout\nCannot reach {server}:{port}";
            }
        }
        catch (FormatException)
        {
            TestResultText.Text = "❌ Invalid port number";
        }
        catch (ArgumentException)
        {
            TestResultText.Text = "❌ Invalid server address";
        }
        catch (Exception ex)
        {
            TestResultText.Text = $"❌ Connection failed: {ex.Message}";
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
        }
    }
    
    private void OK_Click(object sender, RoutedEventArgs e)
    {        // Validate input
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            System.Windows.MessageBox.Show("Username is required", "Validation Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            System.Windows.MessageBox.Show("Password is required", "Validation Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(ServerTextBox.Text))
        {
            System.Windows.MessageBox.Show("Server is required", "Validation Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        
        if (!int.TryParse(PortTextBox.Text, out int port) || port <= 0 || port > 65535)
        {
            System.Windows.MessageBox.Show("Valid port number is required (1-65535)", "Validation Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    public SipAccountConfig GetAccountConfig()
    {
        return new SipAccountConfig
        {
            Username = UsernameTextBox.Text,
            Password = PasswordBox.Password,
            Server = ServerTextBox.Text,
            Port = int.Parse(PortTextBox.Text),
            Transport = TransportComboBox.Text,
            DisplayName = DisplayNameTextBox.Text,
            AutoRegister = AutoRegisterCheckBox.IsChecked ?? false
        };
    }
}

public class SipAccountConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Server { get; set; } = "";
    public int Port { get; set; } = 5060;
    public string Transport { get; set; } = "TCP";
    public string DisplayName { get; set; } = "";
    public bool AutoRegister { get; set; } = false;
}
