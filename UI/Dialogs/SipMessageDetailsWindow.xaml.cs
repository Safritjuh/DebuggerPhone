using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace WindowsSipPhone
{    public partial class SipMessageDetailsWindow : Window
    {
        private readonly SipMessageEntry _message;
        private readonly ObservableCollection<SipHeaderEntry> _headers = new();
        private readonly ObservableCollection<RelatedMessageEntry> _relatedMessages = new();
        private readonly SipMessagesWindow? _messageStore;

        public SipMessageDetailsWindow(SipMessageEntry message, SipMessagesWindow? messageStore = null)
        {
            InitializeComponent();
            _message = message;
            _messageStore = messageStore;
            
            HeadersListView.ItemsSource = _headers;
            RelatedMessagesListView.ItemsSource = _relatedMessages;
            
            LoadMessageDetails();
        }private void LoadMessageDetails()
        {
            // Set header information
            MessageTypeText.Text = _message.MessageType;
            TimestampText.Text = _message.Timestamp;
            DirectionText.Text = _message.Direction;
            AnalysisDirectionText.Text = _message.Direction;
            CallIdText.Text = $"Call-ID: {_message.CallId ?? "N/A"}";
            
            // Set direction icon
            DirectionIcon.Text = _message.IsOutgoing ? "📤" : "📥";
            DirectionText.Foreground = _message.IsOutgoing ? 
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)) :
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96));

            // Load raw message
            RawMessageTextBox.Text = _message.RawMessage;

            // Parse and load headers
            ParseHeaders(_message.RawMessage);
            
            // Load analysis
            LoadMessageAnalysis();
            
            // Generate interpretation
            GenerateInterpretation();
            
            // Load related messages
            LoadRelatedMessages();
        }

        private void ParseHeaders(string rawMessage)
        {
            var lines = rawMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var inHeaders = true;
            
            _headers.Clear();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip first line (method/response line)
                if (trimmedLine.StartsWith("SIP/2.0") || 
                    trimmedLine.StartsWith("INVITE") || 
                    trimmedLine.StartsWith("REGISTER") ||
                    trimmedLine.StartsWith("BYE") ||
                    trimmedLine.StartsWith("ACK") ||
                    trimmedLine.StartsWith("OPTIONS"))
                {
                    continue;
                }
                
                // Empty line indicates end of headers
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    inHeaders = false;
                    continue;
                }
                
                if (inHeaders && trimmedLine.Contains(':'))
                {
                    var colonIndex = trimmedLine.IndexOf(':');
                    var name = trimmedLine.Substring(0, colonIndex).Trim();
                    var value = trimmedLine.Substring(colonIndex + 1).Trim();
                    
                    _headers.Add(new SipHeaderEntry
                    {
                        Name = name,
                        Value = value,
                        Description = GetHeaderDescription(name)
                    });
                }
            }
        }

        private string GetHeaderDescription(string headerName)
        {
            return headerName.ToLower() switch
            {
                "via" => "Routing path taken by the request",
                "from" => "Originator of the request",
                "to" => "Intended recipient of the request",
                "call-id" => "Unique identifier for the call",
                "cseq" => "Command sequence number",
                "contact" => "Direct route to the user agent",
                "max-forwards" => "Maximum number of hops allowed",
                "user-agent" => "Software information",
                "content-type" => "Type of message body",
                "content-length" => "Length of message body in bytes",
                "authorization" => "Authentication credentials",
                "www-authenticate" => "Authentication challenge",
                "expires" => "When the registration expires",
                "allow" => "Supported SIP methods",
                "supported" => "Supported SIP extensions",
                _ => "SIP header field"
            };
        }

        private void LoadMessageAnalysis()
        {
            var message = _message.RawMessage;
            var lines = message.Split('\n');
            
            // Basic message info
            MethodText.Text = _message.MessageType;
            SizeText.Text = $"{Encoding.UTF8.GetByteCount(message)} bytes";
            LineCountText.Text = lines.Length.ToString();
            ProtocolText.Text = "SIP/2.0";
            
            // Routing info
            var routingInfo = new StringBuilder();
            var fromHeader = _headers.FirstOrDefault(h => h.Name.ToLower() == "from");
            var toHeader = _headers.FirstOrDefault(h => h.Name.ToLower() == "to");
            var viaHeader = _headers.FirstOrDefault(h => h.Name.ToLower() == "via");
            var contactHeader = _headers.FirstOrDefault(h => h.Name.ToLower() == "contact");
            
            routingInfo.AppendLine($"From: {ExtractSipUri(fromHeader?.Value) ?? "N/A"}");
            routingInfo.AppendLine($"To: {ExtractSipUri(toHeader?.Value) ?? "N/A"}");
            routingInfo.AppendLine($"Via: {viaHeader?.Value ?? "N/A"}");
            routingInfo.AppendLine($"Contact: {ExtractSipUri(contactHeader?.Value) ?? "N/A"}");
            
            RoutingInfoText.Text = routingInfo.ToString();
        }

        private string? ExtractSipUri(string? headerValue)
        {
            if (string.IsNullOrEmpty(headerValue)) return null;
            
            var match = Regex.Match(headerValue, @"<sip:([^>]+)>");
            return match.Success ? match.Groups[1].Value : headerValue.Split(';')[0].Trim();
        }

        private void GenerateInterpretation()
        {
            var interpretation = new StringBuilder();
            
            if (_message.MessageType.StartsWith("INVITE"))
            {
                interpretation.AppendLine("📞 INVITE Request - Initiating a new call session");
                interpretation.AppendLine("This message is requesting to establish a multimedia session (voice call).");
                interpretation.AppendLine("The caller is inviting the callee to participate in a session.");
            }
            else if (_message.MessageType.StartsWith("200 OK"))
            {
                interpretation.AppendLine("✅ 200 OK Response - Request was successful");
                interpretation.AppendLine("The server/client has successfully processed the request.");
                interpretation.AppendLine("For INVITE: Call was accepted. For REGISTER: Registration successful.");
            }
            else if (_message.MessageType.StartsWith("REGISTER"))
            {
                interpretation.AppendLine("📝 REGISTER Request - User registration");
                interpretation.AppendLine("The client is registering its location with the SIP server.");
                interpretation.AppendLine("This allows the server to route incoming calls to this client.");
            }
            else if (_message.MessageType.StartsWith("BYE"))
            {
                interpretation.AppendLine("👋 BYE Request - Terminating session");
                interpretation.AppendLine("One party is requesting to end the current call session.");
                interpretation.AppendLine("This will close the RTP media streams and clean up the call.");
            }
            else if (_message.MessageType.StartsWith("401") || _message.MessageType.StartsWith("407"))
            {
                interpretation.AppendLine("🔐 Authentication Required");
                interpretation.AppendLine("The server is requesting authentication credentials.");
                interpretation.AppendLine("The client needs to retry with proper authorization headers.");
            }
            else if (_message.MessageType.StartsWith("180"))
            {
                interpretation.AppendLine("📱 180 Ringing - Call is ringing");
                interpretation.AppendLine("The called party's phone is ringing.");
                interpretation.AppendLine("Waiting for the user to answer or decline the call.");
            }
            else if (_message.MessageType.StartsWith("4") || _message.MessageType.StartsWith("5") || _message.MessageType.StartsWith("6"))
            {
                interpretation.AppendLine("❌ Error Response");
                interpretation.AppendLine($"The request failed with status: {_message.MessageType}");
                interpretation.AppendLine("Check the response details for the specific error reason.");
            }
            else
            {
                interpretation.AppendLine($"📋 SIP Message: {_message.MessageType}");
                interpretation.AppendLine("This is a standard SIP protocol message.");
            }
            
            // Add timing context if available
            if (!string.IsNullOrEmpty(_message.CallId))
            {
                interpretation.AppendLine($"\n🔗 Part of call session: {_message.CallId}");
            }
            
            InterpretationText.Text = interpretation.ToString();
        }

        private void CopyRaw_Click(object sender, RoutedEventArgs e)
        {
            try
            {                System.Windows.Clipboard.SetText(_message.RawMessage);
                System.Windows.MessageBox.Show("Raw message copied to clipboard!", "Copy Successful", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Copy Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|SIP files (*.sip)|*.sip|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"SipMessage_{_message.MessageType.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var export = new StringBuilder();
                    export.AppendLine($"SIP Message Export - {DateTime.Now}");
                    export.AppendLine($"Message Type: {_message.MessageType}");
                    export.AppendLine($"Direction: {_message.Direction}");
                    export.AppendLine($"Timestamp: {_message.Timestamp}");
                    export.AppendLine($"Call-ID: {_message.CallId}");
                    export.AppendLine();
                    export.AppendLine("Raw Message:");
                    export.AppendLine(new string('=', 50));
                    export.AppendLine(_message.RawMessage);
                    export.AppendLine();
                    export.AppendLine("Parsed Headers:");
                    export.AppendLine(new string('=', 50));
                    
                    foreach (var header in _headers)
                    {
                        export.AppendLine($"{header.Name}: {header.Value}");
                    }
                    
                    File.WriteAllText(saveDialog.FileName, export.ToString());                    System.Windows.MessageBox.Show("Message exported successfully!", "Export Complete", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Export Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }        private void LoadRelatedMessages()
        {
            _relatedMessages.Clear();
            
            if (string.IsNullOrEmpty(_message.CallId))
            {
                // Add a message indicating no Call-ID available
                _relatedMessages.Add(new RelatedMessageEntry
                {
                    Time = "N/A",
                    Direction = "INFO",
                    Method = "No Call-ID",
                    Summary = "This message has no Call-ID header to find related messages"
                });
                return;
            }

            // If we have access to the message store, search for related messages
            if (_messageStore != null)
            {
                var allMessages = _messageStore.GetAllMessages();
                var relatedMessages = allMessages
                    .Where(m => !string.IsNullOrEmpty(m.CallId) && m.CallId == _message.CallId)
                    .OrderBy(m => DateTime.ParseExact(m.Timestamp, "HH:mm:ss.fff", null))
                    .ToList();

                if (relatedMessages.Any())
                {
                    foreach (var relatedMessage in relatedMessages)
                    {
                        var isCurrent = relatedMessage == _message;
                        _relatedMessages.Add(new RelatedMessageEntry
                        {
                            Time = relatedMessage.Timestamp,
                            Direction = relatedMessage.Direction,
                            Method = relatedMessage.MessageType,
                            Summary = isCurrent ? $"→ {relatedMessage.Summary} ← (Current)" : relatedMessage.Summary
                        });
                    }
                }
                else
                {
                    // No related messages found
                    _relatedMessages.Add(new RelatedMessageEntry
                    {
                        Time = _message.Timestamp,
                        Direction = _message.Direction,
                        Method = _message.MessageType,
                        Summary = "This is the only message with this Call-ID"
                    });
                }
            }
            else
            {
                // Fallback when no message store is available
                _relatedMessages.Add(new RelatedMessageEntry
                {
                    Time = _message.Timestamp,
                    Direction = _message.Direction,
                    Method = _message.MessageType,
                    Summary = "Current message (message store not available for related search)"
                });
                
                // Add a helpful note
                _relatedMessages.Add(new RelatedMessageEntry
                {
                    Time = "N/A",
                    Direction = "INFO",
                    Method = "Note",
                    Summary = "To see related messages, open details from the SIP Messages window"
                });
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class SipHeaderEntry
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class RelatedMessageEntry
    {
        public string Time { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Method { get; set; } = "";
        public string Summary { get; set; } = "";
    }
}
