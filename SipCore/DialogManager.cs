using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WindowsSipPhone.SipCore
{
    /// <summary>
    /// Manages SIP dialogs for the endpoint, following JSIP patterns
    /// </summary>
    public class DialogManager
    {
        private readonly ConcurrentDictionary<string, SipDialog> _dialogs = new();
        private readonly object _lock = new object();
        
        /// <summary>
        /// Event fired when a dialog state changes
        /// </summary>
        public event EventHandler<DialogStateChangedEventArgs>? DialogStateChanged;
        
        /// <summary>
        /// Creates a new outgoing dialog for UAC (User Agent Client)
        /// </summary>
        public SipDialog CreateOutgoingDialog(string callId, string localTag, string localUri, string localContact, string remoteUri)
        {
            lock (_lock)
            {
                var dialog = new SipDialog(callId, localTag, localUri, localContact)
                {
                    RemoteUri = remoteUri,
                    State = DialogState.Null
                };
                
                _dialogs[dialog.DialogId] = dialog;
                OnDialogStateChanged(dialog, DialogState.Null, DialogState.Null);
                
                return dialog;
            }
        }
        
        /// <summary>
        /// Creates a new incoming dialog for UAS (User Agent Server)
        /// </summary>
        public SipDialog CreateIncomingDialog(string callId, string remoteTag, string localTag, string localUri, string localContact, string remoteUri)
        {
            lock (_lock)
            {
                var dialog = new SipDialog(callId, localTag, localUri, localContact)
                {
                    RemoteTag = remoteTag,
                    RemoteUri = remoteUri,
                    State = DialogState.Null
                };
                
                _dialogs[dialog.DialogId] = dialog;
                OnDialogStateChanged(dialog, DialogState.Null, DialogState.Null);
                
                return dialog;
            }
        }
        
        /// <summary>
        /// Finds an existing dialog by Call-ID and tags
        /// </summary>
        public SipDialog? FindDialog(string callId, string? localTag = null, string? remoteTag = null)
        {
            lock (_lock)
            {
                // If we have both tags, create exact dialog ID
                if (!string.IsNullOrEmpty(localTag) && !string.IsNullOrEmpty(remoteTag))
                {
                    var dialogId = $"{callId}-{localTag}-{remoteTag}";
                    _dialogs.TryGetValue(dialogId, out var exactDialog);
                    return exactDialog;
                }
                
                // Otherwise, search by Call-ID and available tag
                return _dialogs.Values.FirstOrDefault(d => 
                    d.CallId == callId && 
                    (string.IsNullOrEmpty(localTag) || d.LocalTag == localTag) &&
                    (string.IsNullOrEmpty(remoteTag) || d.RemoteTag == remoteTag));
            }
        }
        
        /// <summary>
        /// Finds a dialog by Call-ID only (compatibility method)
        /// </summary>
        public SipDialog? FindDialogByCallId(string callId)
        {
            return FindDialog(callId);
        }
          /// <summary>
        /// Creates a new dialog (compatibility method)
        /// </summary>
        public SipDialog CreateDialog(string callId, string localTag, string remoteTag, uint sequenceNumber)
        {
            // Generate local URI and contact based on local IP
            var localUri = $"sip:{localTag}@{GetLocalIPAddress()}";
            var localContact = $"<sip:{localTag}@{GetLocalIPAddress()}:5060>";
            // Don't construct remoteUri from remoteTag - leave it empty to be set properly later
            var remoteUri = "";
            
            lock (_lock)
            {
                var dialog = new SipDialog(callId, localTag, localUri, localContact)
                {
                    RemoteTag = remoteTag,
                    RemoteUri = remoteUri,
                    LocalSequenceNumber = sequenceNumber,
                    State = DialogState.Null
                };
                
                _dialogs[dialog.DialogId] = dialog;
                OnDialogStateChanged(dialog, DialogState.Null, DialogState.Null);
                
                return dialog;
            }
        }        /// <summary>
        /// Creates a new dialog with proper remote target URI
        /// </summary>
        public SipDialog CreateDialogWithRemoteUri(string callId, string localTag, string remoteTag, uint sequenceNumber, string remoteUri)
        {
            // Generate local URI and contact based on local IP
            var localUri = $"sip:{localTag}@{GetLocalIPAddress()}";
            var localContact = $"<sip:{localTag}@{GetLocalIPAddress()}:5060>";
            
            lock (_lock)
            {
                var dialog = new SipDialog(callId, localTag, localUri, localContact)
                {
                    RemoteTag = remoteTag,
                    RemoteUri = remoteUri,
                    LocalSequenceNumber = sequenceNumber,
                    State = DialogState.Null
                };
                
                _dialogs[dialog.DialogId] = dialog;
                OnDialogStateChanged(dialog, DialogState.Null, DialogState.Null);
                
                return dialog;
            }
        }        /// <summary>
        /// Creates a new dialog with proper local and remote URIs for outgoing calls
        /// </summary>
        public SipDialog CreateOutgoingDialogWithUsernames(string callId, string localTag, string remoteTag, uint sequenceNumber, string localUsername, string remoteUri)
        {
            // Generate local URI using actual username instead of tag
            var localUri = $"sip:{localUsername}@{GetLocalIPAddress()}";
            var localContact = $"<sip:{localUsername}@{GetLocalIPAddress()}:5060>";
            
            lock (_lock)
            {
                var dialog = new SipDialog(callId, localTag, localUri, localContact)
                {
                    RemoteTag = remoteTag,
                    RemoteUri = remoteUri,
                    LocalSequenceNumber = sequenceNumber,
                    State = DialogState.Null
                };
                
                _dialogs[dialog.DialogId] = dialog;
                OnDialogStateChanged(dialog, DialogState.Null, DialogState.Null);
                
                return dialog;
            }
        }

        /// <summary>
        /// Creates a new dialog for incoming calls with proper local and remote URIs
        /// </summary>
        public SipDialog CreateIncomingDialogWithUsernames(string callId, string localTag, string remoteTag, uint sequenceNumber, string localUsername, string remoteUri)
        {
            // Generate local URI using actual username instead of tag
            var localUri = $"sip:{localUsername}@{GetLocalIPAddress()}";
            var localContact = $"<sip:{localUsername}@{GetLocalIPAddress()}:5060>";
            
            lock (_lock)
            {
                var dialog = new SipDialog(callId, localTag, localUri, localContact)
                {
                    RemoteTag = remoteTag,
                    RemoteUri = remoteUri,
                    LocalSequenceNumber = sequenceNumber,
                    State = DialogState.Null
                };
                
                _dialogs[dialog.DialogId] = dialog;
                OnDialogStateChanged(dialog, DialogState.Null, DialogState.Null);
                
                return dialog;
            }
        }
        
        /// <summary>
        /// Gets local IP address for URI generation
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
        
        /// <summary>
        /// Updates dialog from incoming response
        /// </summary>
        public void UpdateDialogFromResponse(string callId, string localTag, int statusCode, string toHeader, string? contactHeader = null)
        {
            lock (_lock)
            {
                var dialog = FindDialog(callId, localTag);
                if (dialog != null)
                {
                    var oldState = dialog.State;
                    dialog.UpdateFromResponse(statusCode, toHeader, contactHeader);
                    
                    // Update dialog ID if remote tag was added
                    if (!string.IsNullOrEmpty(dialog.RemoteTag) && dialog.DialogId != $"{callId}-{localTag}-{dialog.RemoteTag}")
                    {
                        _dialogs.TryRemove(dialog.DialogId, out _);
                        _dialogs[dialog.DialogId] = dialog;
                    }
                    
                    OnDialogStateChanged(dialog, oldState, dialog.State);
                }
            }
        }
        
        /// <summary>
        /// Updates dialog from incoming request
        /// </summary>
        public void UpdateDialogFromRequest(string callId, string? remoteTag, string method, uint cseqNumber)
        {
            lock (_lock)
            {
                var dialog = FindDialog(callId, remoteTag: remoteTag);
                if (dialog != null)
                {
                    var oldState = dialog.State;
                    dialog.UpdateFromRequest(method, cseqNumber);
                    OnDialogStateChanged(dialog, oldState, dialog.State);
                }
            }
        }
        
        /// <summary>
        /// Terminates a dialog
        /// </summary>
        public void TerminateDialog(string callId, string? localTag = null, string? remoteTag = null)
        {
            lock (_lock)
            {
                var dialog = FindDialog(callId, localTag, remoteTag);
                if (dialog != null)
                {
                    var oldState = dialog.State;
                    dialog.State = DialogState.Terminated;
                    OnDialogStateChanged(dialog, oldState, DialogState.Terminated);
                    
                    // Remove terminated dialogs after a delay to allow final processing
                    _ = Task.Delay(30000).ContinueWith(_ => RemoveDialog(dialog.DialogId));
                }
            }
        }
        
        /// <summary>
        /// Removes a dialog from management
        /// </summary>
        public void RemoveDialog(string dialogId)
        {
            lock (_lock)
            {
                _dialogs.TryRemove(dialogId, out _);
            }
        }
        
        /// <summary>
        /// Gets all active dialogs
        /// </summary>
        public IEnumerable<SipDialog> GetActiveDialogs()
        {
            lock (_lock)
            {
                return _dialogs.Values.Where(d => d.State != DialogState.Terminated).ToList();
            }
        }
        
        /// <summary>
        /// Gets all dialogs
        /// </summary>
        public IEnumerable<SipDialog> GetAllDialogs()
        {
            lock (_lock)
            {
                return _dialogs.Values.ToList();
            }
        }
        
        /// <summary>
        /// Cleans up old terminated dialogs
        /// </summary>
        public void CleanupTerminatedDialogs()
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.Now.AddMinutes(-5);
                var toRemove = _dialogs.Values
                    .Where(d => d.State == DialogState.Terminated && d.LastActivity < cutoffTime)
                    .Select(d => d.DialogId)
                    .ToList();
                
                foreach (var dialogId in toRemove)
                {
                    _dialogs.TryRemove(dialogId, out _);
                }
            }
        }
        
        /// <summary>
        /// Gets dialog statistics
        /// </summary>
        public DialogStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new DialogStatistics
                {
                    TotalDialogs = _dialogs.Count,
                    ConfirmedDialogs = _dialogs.Values.Count(d => d.State == DialogState.Confirmed),
                    EarlyDialogs = _dialogs.Values.Count(d => d.State == DialogState.Early),
                    TerminatedDialogs = _dialogs.Values.Count(d => d.State == DialogState.Terminated)
                };
            }
        }
        
        private void OnDialogStateChanged(SipDialog dialog, DialogState oldState, DialogState newState)
        {
            DialogStateChanged?.Invoke(this, new DialogStateChangedEventArgs(dialog, oldState, newState));
        }
    }
    
    /// <summary>
    /// Event arguments for dialog state changes
    /// </summary>
    public class DialogStateChangedEventArgs : EventArgs
    {
        public SipDialog Dialog { get; }
        public DialogState OldState { get; }
        public DialogState NewState { get; }
        
        public DialogStateChangedEventArgs(SipDialog dialog, DialogState oldState, DialogState newState)
        {
            Dialog = dialog;
            OldState = oldState;
            NewState = newState;
        }
    }
    
    /// <summary>
    /// Dialog statistics for monitoring
    /// </summary>
    public class DialogStatistics
    {
        public int TotalDialogs { get; set; }
        public int ConfirmedDialogs { get; set; }
        public int EarlyDialogs { get; set; }
        public int TerminatedDialogs { get; set; }
    }
}
