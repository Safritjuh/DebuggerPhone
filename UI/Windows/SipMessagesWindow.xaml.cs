using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsSipPhone
{    public partial class SipMessagesWindow : Window
    {
        private readonly StringBuilder _messageBuilder = new();
        private int _messageCount = 0;
        private readonly ObservableCollection<SipMessageEntry> _messageList = new();
        private readonly ObservableCollection<SipMessageEntry> _filteredMessageList = new();
        private readonly ObservableCollection<ConversationInfo> _conversations = new();
        private int _outgoingCount = 0;
        private int _incomingCount = 0;
        private int _errorCount = 0;
        private double _ladderYPosition = 50;
        private readonly List<string> _recentActivity = new();
        private string? _selectedCallId = null;
        
        public bool IsClosed { get; private set; } = false;

        // Expose message collection for related message searches
        public IEnumerable<SipMessageEntry> GetAllMessages()
        {
            return _messageList.ToList(); // Return a copy to avoid collection modification issues
        }

        public SipMessagesWindow()
        {
            InitializeComponent();
            
            // Wire up event handlers
            ClearButton.Click += ClearButton_Click;
            SaveButton.Click += SaveButton_Click;
            MessageFilterComboBox.SelectionChanged += MessageFilterComboBox_SelectionChanged;
            MessageListView.SelectionChanged += MessageListView_SelectionChanged;
            
            // Setup data binding
            MessageListView.ItemsSource = _filteredMessageList;
            ConversationComboBox.ItemsSource = _conversations;
            
            UpdateStatus();
            UpdateConnectionStatus(false);
            UpdateConversationStats();
        }

        public void AddMessage(string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var formattedMessage = $"[{timestamp}] {message}";
                
                _messageBuilder.AppendLine(formattedMessage);
                _messageCount++;
                
                MessagesTextBox.Text = _messageBuilder.ToString();
                
                if (AutoScrollCheckBox.IsChecked == true)
                {
                    MessagesScrollViewer.ScrollToEnd();
                }
                
                AddToRecentActivity(message);
                UpdateStatus();
            });
        }

        public void AddSipMessage(string direction, string message)
        {
            // Don't process messages if window is closed
            if (IsClosed)
                return;

            Dispatcher.BeginInvoke(() =>
            {
                // Double-check after dispatcher invoke
                if (IsClosed)
                    return;

                var timestamp = DateTime.Now;
                var parsedMessage = ParseSipMessage(message, direction, timestamp);
                
                // Add to different views
                AddToRawView(direction, message);
                AddToMessageList(parsedMessage);
                AddToLadderView(parsedMessage);
                
                // Update counters
                if (direction.Contains("OUTGOING"))
                    _outgoingCount++;
                else if (direction.Contains("INCOMING"))
                    _incomingCount++;
                
                if (IsErrorMessage(message))
                    _errorCount++;
                
                UpdateStatistics();
                UpdateStatus();
            });
        }

        public void UpdateConnectionStatus(bool isConnected)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (isConnected)
                {
                    ConnectionStatusLabel.Text = "🟢 Connected";
                    ConnectionStatusLabel.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96));
                }
                else
                {
                    ConnectionStatusLabel.Text = "🔴 Disconnected";
                    ConnectionStatusLabel.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
                }
            });
        }

        public void UpdateServerInfo(string serverAddress, int port)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ServerInfoLabel.Text = $"{serverAddress}:{port}";
            });
        }        private void AddToRawView(string direction, string message)
        {
            var formattedMessage = new StringBuilder();
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            
            // Extract the actual SIP message (remove OUTGOING:/INCOMING: prefix if present)
            var actualMessage = message;
            if (message.StartsWith("OUTGOING") || message.StartsWith("INCOMING"))
            {
                var colonIndex = message.IndexOf(':');
                if (colonIndex != -1 && message.Length > colonIndex + 1)
                {
                    actualMessage = message.Substring(colonIndex + 1);
                    if (actualMessage.StartsWith("\n"))
                    {
                        actualMessage = actualMessage.Substring(1);
                    }
                }
            }
            
            var lines = actualMessage.Split('\n');
            var firstLine = lines.FirstOrDefault()?.Trim() ?? "";
            
            // Enhanced direction indicators and message type detection
            var emoji = direction.Contains("OUTGOING") ? "📤" : "📥";
            var arrow = direction.Contains("OUTGOING") ? "CLIENT ──→ SERVER" : "CLIENT ←── SERVER";
            var color = direction.Contains("OUTGOING") ? "Blue" : "Green";
            
            // Determine message type for better labeling
            var messageTypeLabel = "";
            if (firstLine.StartsWith("SIP/2.0"))
            {
                var parts = firstLine.Split(' ', 3);
                if (parts.Length >= 3)
                {
                    messageTypeLabel = $"SIP RESPONSE: {parts[1]} {parts[2]}";
                }
            }
            else
            {
                var method = firstLine.Split(' ').FirstOrDefault();
                messageTypeLabel = $"SIP REQUEST: {method}";
            }
            
            formattedMessage.AppendLine($"[{timestamp}] {emoji} {arrow} === {direction} {messageTypeLabel} ===");
            
            foreach (var line in actualMessage.Split('\n'))
            {
                formattedMessage.AppendLine($"  {line}");
            }
            formattedMessage.AppendLine("=== END MESSAGE ===");
            formattedMessage.AppendLine();
            
            _messageBuilder.Append(formattedMessage.ToString());
            MessagesTextBox.Text = _messageBuilder.ToString();
            
            if (AutoScrollCheckBox.IsChecked == true)
            {
                MessagesScrollViewer.ScrollToEnd();
            }
        }

        private void UpdateStatus()
        {
            MessageCountLabel.Text = $"{_messageCount} messages";
            LastUpdateLabel.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
        }

        private void UpdateStatistics()
        {
            TotalMessagesLabel.Text = $"Total Messages: {_messageList.Count}";
            OutgoingCountLabel.Text = $"Outgoing: {_outgoingCount}";
            IncomingCountLabel.Text = $"Incoming: {_incomingCount}";
            ErrorCountLabel.Text = $"Errors: {_errorCount}";
            
            // Update recent activity
            var recentText = string.Join("\n", _recentActivity.TakeLast(5));
            RecentActivityText.Text = recentText;
        }

        private void AddToRecentActivity(string activity)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _recentActivity.Add($"[{timestamp}] {activity}");
            
            // Keep only last 10 items
            if (_recentActivity.Count > 10)
                _recentActivity.RemoveAt(0);
        }        private SipMessageEntry ParseSipMessage(string message, string direction, DateTime timestamp)
        {
            // Clean up the message by removing OUTGOING:/INCOMING: prefixes (including variations)
            var cleanMessage = message;
            if (message.StartsWith("OUTGOING") || message.StartsWith("INCOMING"))
            {
                var messageLines = message.Split('\n');
                if (messageLines.Length > 1)
                {
                    // Skip the first line which contains the direction prefix
                    cleanMessage = string.Join('\n', messageLines.Skip(1));
                }
            }
            
            var lines = cleanMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var firstLine = lines.FirstOrDefault()?.Trim() ?? "";
            
            var entry = new SipMessageEntry
            {
                Timestamp = timestamp.ToString("HH:mm:ss.fff"),
                Direction = direction.Contains("OUTGOING") ? "OUT" : "IN",
                RawMessage = cleanMessage,
                IsOutgoing = direction.Contains("OUTGOING")
            };

            // Parse message type with enhanced descriptions
            if (firstLine.StartsWith("SIP/2.0"))
            {
                var parts = firstLine.Split(' ', 3);
                if (parts.Length >= 3)
                {
                    var statusCode = parts[1];
                    var reasonPhrase = parts[2];
                    
                    // Enhanced status code descriptions
                    entry.MessageType = statusCode switch
                    {
                        "100" => "100 Trying",
                        "180" => "180 Ringing", 
                        "183" => "183 Session Progress",
                        "200" => "200 OK",
                        "401" => "401 Unauthorized",
                        "407" => "407 Proxy Auth Required",
                        "403" => "403 Forbidden",
                        "404" => "404 Not Found",
                        "408" => "408 Request Timeout",
                        "480" => "480 Temporarily Unavailable",
                        "486" => "486 Busy Here",
                        "487" => "487 Request Terminated",
                        "488" => "488 Not Acceptable Here",
                        "603" => "603 Decline",
                        _ => $"{statusCode} {reasonPhrase}"
                    };
                    
                    entry.StatusCode = statusCode;
                }
            }
            else
            {
                var method = firstLine.Split(' ').FirstOrDefault();
                
                // Enhanced method descriptions
                entry.MessageType = method?.ToUpperInvariant() switch
                {
                    "REGISTER" => "REGISTER",
                    "INVITE" => "INVITE",
                    "BYE" => "BYE",
                    "ACK" => "ACK",
                    "CANCEL" => "CANCEL",
                    "UPDATE" => "UPDATE",
                    "REFER" => "REFER",
                    "NOTIFY" => "NOTIFY",
                    "SUBSCRIBE" => "SUBSCRIBE",
                    "OPTIONS" => "OPTIONS",
                    "INFO" => "INFO",
                    "PRACK" => "PRACK",
                    _ => method ?? "Unknown"
                };
                
                entry.Method = method;
            }

            // Extract key headers for summary
            var summary = new StringBuilder();
            foreach (var line in lines)
            {
                if (line.StartsWith("Call-ID:"))
                    entry.CallId = line.Substring(8).Trim();
                else if (line.StartsWith("CSeq:"))
                    entry.CSeq = line.Substring(5).Trim();
                else if (line.StartsWith("From:"))
                    summary.AppendLine($"From: {ExtractSipUri(line)}");
                else if (line.StartsWith("To:"))
                    summary.AppendLine($"To: {ExtractSipUri(line)}");
            }

            entry.Summary = summary.ToString().Trim();
            return entry;
        }

        private string ExtractSipUri(string headerLine)
        {
            var match = Regex.Match(headerLine, @"<sip:([^>]+)>");
            return match.Success ? match.Groups[1].Value : "Unknown";
        }

        private bool IsErrorMessage(string message)
        {
            return message.Contains("SIP/2.0 4") || message.Contains("SIP/2.0 5") || 
                   message.Contains("SIP/2.0 6") || message.Contains("ERROR");        }

        private void MessageFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyMessageFilter();
        }        private void MessageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MessageListView.SelectedItem is SipMessageEntry selectedMessage)
            {
                ShowMessageDetails(selectedMessage);
            }
        }        private void ShowMessageDetails(SipMessageEntry message)
        {
            // Create and show message details window with access to message store
            var detailsWindow = new SipMessageDetailsWindow(message, this);
            detailsWindow.Owner = this;
            detailsWindow.Show();
        }private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _messageBuilder.Clear();
            _messageCount = 0;
            _outgoingCount = 0;
            _incomingCount = 0;
            _errorCount = 0;
            _ladderYPosition = 50;
            _recentActivity.Clear();
            _selectedCallId = null;
            
            MessagesTextBox.Text = string.Empty;
            _messageList.Clear();
            _filteredMessageList.Clear();
            _conversations.Clear();
            LadderCanvas.Children.Clear();
            ConversationComboBox.SelectedItem = null;
            
            UpdateStatus();
            UpdateStatistics();
            UpdateConversationStats();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "SIP Log files (*.sip)|*.sip|Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
                DefaultExt = "sip",
                FileName = $"SipDebugLog_{DateTime.Now:yyyyMMdd_HHmmss}.sip"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var logContent = GenerateDetailedLog();
                    File.WriteAllText(saveDialog.FileName, logContent);                    System.Windows.MessageBox.Show("SIP debug log saved successfully!", "Save Complete", 
                                  System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {                    System.Windows.MessageBox.Show($"Error saving log: {ex.Message}", "Save Error", 
                                  System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private string GenerateDetailedLog()
        {
            var log = new StringBuilder();
            
            // Header
            log.AppendLine("================================================");
            log.AppendLine("    SIP PROTOCOL DEBUG LOG - RFC 3261 COMPLIANT");
            log.AppendLine("================================================");
            log.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine($"Total Messages: {_messageList.Count}");
            log.AppendLine($"Outgoing: {_outgoingCount}, Incoming: {_incomingCount}, Errors: {_errorCount}");
            log.AppendLine($"Client: {ClientInfoLabel.Text}");
            log.AppendLine($"Server: {ServerInfoLabel.Text}");
            log.AppendLine("================================================");
            log.AppendLine();

            // Message ladder summary
            log.AppendLine("SIP MESSAGE LADDER:");
            log.AppendLine("==================");
            foreach (var message in _messageList)
            {
                var arrow = message.IsOutgoing ? "CLIENT ──→ SERVER" : "CLIENT ←── SERVER";
                log.AppendLine($"[{message.Timestamp}] {arrow}: {message.MessageType}");
                if (!string.IsNullOrEmpty(message.Summary))
                {
                    log.AppendLine($"    {message.Summary.Replace("\n", "\n    ")}");
                }
                log.AppendLine();
            }

            log.AppendLine("\nDETAILED SIP MESSAGES:");
            log.AppendLine("=====================");
            log.AppendLine(_messageBuilder.ToString());

            return log.ToString();
        }        private void AddToMessageList(SipMessageEntry entry)
        {
            _messageList.Add(entry);
            
            // Update or add conversation info
            UpdateConversationInfo(entry);
            
            // Apply current filter and conversation selection
            ApplyFilters();
        }        private void AddToLadderView(SipMessageEntry entry)
        {
            // Safety checks to prevent UI exceptions
            if (IsClosed || LadderCanvas == null || entry == null)
                return;

            const double clientX = 100;
            const double serverX = 700;
            const double messageSpacing = 40;

            // Draw client and server columns if this is the first message
            if (_ladderYPosition == 50)
            {
                DrawLadderColumns();
            }

            // Determine direction and arrow
            double startX = entry.IsOutgoing ? clientX : serverX;
            double endX = entry.IsOutgoing ? serverX : clientX;
            
            // Draw message arrow
            var arrow = new Line
            {
                X1 = startX,
                Y1 = _ladderYPosition,
                X2 = endX,
                Y2 = _ladderYPosition,
                Stroke = entry.IsOutgoing ? System.Windows.Media.Brushes.Blue : System.Windows.Media.Brushes.Green,
                StrokeThickness = 2
            };

            // Add arrow head
            var arrowHead = CreateArrowHead(endX, _ladderYPosition, entry.IsOutgoing);            // Enhanced message label with better formatting
            var labelText = entry.MessageType;
            
            // If MessageType is still empty or contains direction prefixes, fall back to parsing
            if (string.IsNullOrEmpty(labelText) || labelText.Contains("OUTGOING") || labelText.Contains("INCOMING"))
            {
                // Parse the raw message directly as a fallback
                var rawLines = entry.RawMessage?.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var sipLine = rawLines?.FirstOrDefault(line => 
                    line.StartsWith("SIP/2.0") || 
                    line.StartsWith("REGISTER") || 
                    line.StartsWith("INVITE") || 
                    line.StartsWith("BYE") || 
                    line.StartsWith("ACK")) ?? "";
                
                if (sipLine.StartsWith("SIP/2.0"))
                {
                    var parts = sipLine.Split(' ', 3);
                    if (parts.Length >= 2)
                    {
                        labelText = parts[1] switch
                        {
                            "100" => "100 Trying",
                            "180" => "180 Ringing",
                            "200" => "200 OK",
                            "401" => "401 Unauthorized",
                            "486" => "486 Busy Here",
                            _ => parts.Length > 2 ? $"{parts[1]} {parts[2]}" : parts[1]
                        };
                    }
                }
                else
                {
                    var method = sipLine.Split(' ').FirstOrDefault();
                    labelText = method ?? "Unknown";
                }
            }
            
            // Add additional context for specific message types
            if (entry.MessageType == "INVITE" && !string.IsNullOrEmpty(entry.Summary))
            {
                var toMatch = System.Text.RegularExpressions.Regex.Match(entry.Summary, @"To: ([^@\s]+)");
                if (toMatch.Success)
                {
                    labelText = $"INVITE → {toMatch.Groups[1].Value}";
                }
            }
            else if (entry.MessageType == "BYE")
            {
                labelText = "BYE (Hangup)";
            }
            else if (entry.MessageType == "200 OK")
            {
                // Determine what the 200 OK is responding to based on CSeq
                if (!string.IsNullOrEmpty(entry.CSeq))
                {
                    if (entry.CSeq.Contains("REGISTER"))
                        labelText = "200 OK (Registration)";
                    else if (entry.CSeq.Contains("INVITE"))
                        labelText = "200 OK (Call Answer)";
                    else if (entry.CSeq.Contains("BYE"))
                        labelText = "200 OK (BYE Ack)";
                    else if (entry.CSeq.Contains("UPDATE"))
                        labelText = "200 OK (Update Ack)";
                }
            }
            else if (entry.MessageType == "180 Ringing")
            {
                labelText = "180 Ringing";
            }
            else if (entry.MessageType == "401 Unauthorized" || entry.MessageType == "407 Proxy Auth Required")
            {
                labelText = entry.MessageType.Contains("401") ? "401 Auth Required" : "407 Proxy Auth";
            }
            
            var label = new TextBlock
            {
                Text = labelText,
                FontSize = 10,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };
              // Color code the label based on message type
            if (entry.MessageType.StartsWith("2"))
                label.Foreground = System.Windows.Media.Brushes.Green;
            else if (entry.MessageType.StartsWith("1"))
                label.Foreground = System.Windows.Media.Brushes.Blue;
            else if (entry.MessageType.StartsWith("4") || entry.MessageType.StartsWith("5") || entry.MessageType.StartsWith("6"))
                label.Foreground = System.Windows.Media.Brushes.Red;
            else
                label.Foreground = System.Windows.Media.Brushes.Black;
            
            Canvas.SetLeft(label, (startX + endX) / 2 - 50);
            Canvas.SetTop(label, _ladderYPosition - 15);

            // Add to canvas with safety check
            try
            {
                if (LadderCanvas != null && !IsClosed)
                {
                    LadderCanvas.Children.Add(arrow);
                    LadderCanvas.Children.Add(arrowHead);
                    LadderCanvas.Children.Add(label);
                    
                    _ladderYPosition += messageSpacing;
                    
                    // Update canvas height
                    LadderCanvas.Height = Math.Max(400, _ladderYPosition + 50);
                }
            }
            catch (Exception ex)
            {
                // Log the exception but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error adding to ladder view: {ex.Message}");
            }
        }        private void DrawLadderColumns()
        {
            // Safety check to prevent UI exceptions
            if (IsClosed || LadderCanvas == null)
                return;

            const double clientX = 100;
            const double serverX = 700;
            const double columnHeight = 600;

            // Client column
            var clientLine = new Line
            {
                X1 = clientX,
                Y1 = 30,                X2 = clientX,
                Y2 = columnHeight,
                Stroke = System.Windows.Media.Brushes.Gray,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 3 }
            };

            var clientLabel = new TextBlock
            {
                Text = "SIP CLIENT",
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };
            Canvas.SetLeft(clientLabel, clientX - 30);
            Canvas.SetTop(clientLabel, 10);

            // Server column
            var serverLine = new Line
            {
                X1 = serverX,
                Y1 = 30,                X2 = serverX,
                Y2 = columnHeight,
                Stroke = System.Windows.Media.Brushes.Gray,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 3 }
            };

            var serverLabel = new TextBlock
            {
                Text = "SIP SERVER",
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };            Canvas.SetLeft(serverLabel, serverX - 30);
            Canvas.SetTop(serverLabel, 10);

            // Add to canvas with safety check
            try
            {
                if (LadderCanvas != null && !IsClosed)
                {
                    LadderCanvas.Children.Add(clientLine);
                    LadderCanvas.Children.Add(clientLabel);
                    LadderCanvas.Children.Add(serverLine);
                    LadderCanvas.Children.Add(serverLabel);
                }
            }
            catch (Exception ex)
            {
                // Log the exception but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error drawing ladder columns: {ex.Message}");
            }
        }

        private Polygon CreateArrowHead(double x, double y, bool pointingRight)
        {
            var arrowHead = new Polygon();
            
            if (pointingRight)
            {                arrowHead.Points = new PointCollection
                {
                    new System.Windows.Point(x, y),
                    new System.Windows.Point(x - 8, y - 4),
                    new System.Windows.Point(x - 8, y + 4)
                };
                arrowHead.Fill = System.Windows.Media.Brushes.Blue;
            }
            else
            {                arrowHead.Points = new PointCollection
                {
                    new System.Windows.Point(x, y),
                    new System.Windows.Point(x + 8, y - 4),
                    new System.Windows.Point(x + 8, y + 4)
                };
                arrowHead.Fill = System.Windows.Media.Brushes.Green;
            }

            return arrowHead;
        }        private void ApplyMessageFilter()
        {
            ApplyFilters();
        }

        private void UpdateConversationInfo(SipMessageEntry entry)
        {
            if (string.IsNullOrEmpty(entry.CallId))
                return;

            var existingConversation = _conversations.FirstOrDefault(c => c.CallId == entry.CallId);
            if (existingConversation != null)
            {
                existingConversation.MessageCount++;
                existingConversation.LastActivity = entry.Timestamp;
                existingConversation.LastMessageType = entry.MessageType;
            }
            else
            {
                var newConversation = new ConversationInfo
                {
                    CallId = entry.CallId,
                    MessageCount = 1,
                    FirstActivity = entry.Timestamp,
                    LastActivity = entry.Timestamp,
                    LastMessageType = entry.MessageType
                };
                _conversations.Add(newConversation);
            }
            
            UpdateConversationStats();
        }

        private void UpdateConversationStats()
        {
            ConversationStatsLabel.Text = $"{_conversations.Count} conversation{(_conversations.Count != 1 ? "s" : "")}";
        }

        private void ApplyFilters()
        {
            var filteredMessages = _messageList.AsEnumerable();

            // Apply conversation filter first
            if (!string.IsNullOrEmpty(_selectedCallId))
            {
                filteredMessages = filteredMessages.Where(m => m.CallId == _selectedCallId);
            }

            // Apply message type filter
            if (MessageFilterComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var filter = selectedItem.Content.ToString();
                
                switch (filter)
                {
                    case "REGISTER":
                        filteredMessages = filteredMessages.Where(m => m.MessageType.Contains("REGISTER"));
                        break;
                    case "INVITE":
                        filteredMessages = filteredMessages.Where(m => m.MessageType.Contains("INVITE"));
                        break;
                    case "BYE":
                        filteredMessages = filteredMessages.Where(m => m.MessageType.Contains("BYE"));
                        break;
                    case "200 OK":
                        filteredMessages = filteredMessages.Where(m => m.MessageType.Contains("200"));
                        break;
                    case "4xx/5xx Errors":
                        filteredMessages = filteredMessages.Where(m => m.StatusCode?.StartsWith("4") == true || 
                                                                       m.StatusCode?.StartsWith("5") == true);
                        break;
                    case "Authentication":
                        filteredMessages = filteredMessages.Where(m => m.MessageType.Contains("401") || 
                                                                       m.MessageType.Contains("407") ||
                                                                       m.RawMessage.Contains("Authorization"));
                        break;
                }
            }

            // Update the ListView with filtered results
            _filteredMessageList.Clear();
            foreach (var message in filteredMessages)
            {
                _filteredMessageList.Add(message);
            }
        }

        private void ConversationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConversationComboBox.SelectedItem is ConversationInfo selectedConversation)
            {
                _selectedCallId = selectedConversation.CallId;
                ApplyFilters();
                RefreshLadderView();
            }
        }

        private void ShowAllConversationsButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedCallId = null;
            ConversationComboBox.SelectedItem = null;
            ApplyFilters();
            RefreshLadderView();
        }

        private void RefreshLadderView()
        {
            // Clear and rebuild ladder view with filtered messages
            LadderCanvas.Children.Clear();
            _ladderYPosition = 50;
            
            foreach (var message in _filteredMessageList)
            {
                AddToLadderView(message);
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            IsClosed = true;
            base.OnClosed(e);
        }
    }    // Data model for SIP message entries
    public class SipMessageEntry
    {
        public string Timestamp { get; set; } = "";
        public string Direction { get; set; } = "";
        public string MessageType { get; set; } = "";
        public string Summary { get; set; } = "";
        public string RawMessage { get; set; } = "";
        public bool IsOutgoing { get; set; }
        public string? StatusCode { get; set; }
        public string? Method { get; set; }
        public string? CallId { get; set; }
        public string? CSeq { get; set; }
    }

    // Data model for conversation grouping
    public class ConversationInfo
    {
        public string CallId { get; set; } = "";
        public int MessageCount { get; set; }
        public string FirstActivity { get; set; } = "";
        public string LastActivity { get; set; } = "";
        public string LastMessageType { get; set; } = "";
    }
}
