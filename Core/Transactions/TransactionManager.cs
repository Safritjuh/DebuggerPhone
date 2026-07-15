using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace WindowsSipPhone.Core.Transactions
{
    /// <summary>
    /// RFC 3261 Transaction Manager
    /// Manages all SIP transactions and provides transaction matching
    /// </summary>
    public class TransactionManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, SipTransaction> _transactions = new();
        private readonly System.Timers.Timer _cleanupTimer;
        private bool _disposed = false;

        public event EventHandler<string>? MessageToSend;
        public event EventHandler<TransactionEventArgs>? TransactionCompleted;
        public event EventHandler<TransactionEventArgs>? TransactionFailed;

        public TransactionManager()
        {
            _cleanupTimer = new System.Timers.Timer(30000); // Cleanup every 30 seconds
            _cleanupTimer.Elapsed += CleanupExpiredTransactions;
            _cleanupTimer.Start();
        }

        /// <summary>
        /// Create a client transaction for outgoing requests
        /// </summary>
        public SipTransaction CreateClientTransaction(string method, string requestMessage)
        {
            var transactionId = GenerateClientTransactionId(requestMessage);
            
            SipTransaction transaction = method.ToUpper() switch
            {
                "INVITE" => new InviteClientTransaction(transactionId),
                _ => new NonInviteClientTransaction(transactionId, method)
            };

            // Wire up transaction events
            transaction.MessageToSend += (sender, message) => MessageToSend?.Invoke(this, message);
            transaction.TransactionCompleted += OnTransactionCompleted;
            transaction.TransactionFailed += OnTransactionFailed;

            // Add to transaction table
            _transactions[transactionId] = transaction;

            return transaction;
        }

        /// <summary>
        /// Create a server transaction for incoming requests
        /// </summary>
        public SipTransaction? CreateServerTransaction(string requestMessage)
        {
            var transactionId = GenerateServerTransactionId(requestMessage);
            
            // Check if transaction already exists
            if (_transactions.ContainsKey(transactionId))
            {
                return _transactions[transactionId];
            }

            var method = ExtractMethod(requestMessage);
            
            SipTransaction transaction = method?.ToUpper() switch
            {
                "INVITE" => new InviteServerTransaction(transactionId),
                "ACK" => null, // ACK requests don't create transactions
                _ => new NonInviteServerTransaction(transactionId, method ?? "UNKNOWN")
            };

            if (transaction != null)
            {
                // Wire up transaction events
                transaction.MessageToSend += (sender, message) => MessageToSend?.Invoke(this, message);
                transaction.TransactionCompleted += OnTransactionCompleted;
                transaction.TransactionFailed += OnTransactionFailed;

                // Add to transaction table
                _transactions[transactionId] = transaction;
            }

            return transaction;
        }

        /// <summary>
        /// Process incoming response and route to appropriate client transaction
        /// </summary>
        public void ProcessResponse(string responseMessage)
        {
            var transactionId = GenerateClientTransactionIdFromResponse(responseMessage);
            
            if (_transactions.TryGetValue(transactionId, out var transaction))
            {
                transaction.ProcessMessage(responseMessage);
            }
            else
            {
                // Stray response - log and ignore
                Console.WriteLine($"[TRANSACTION] Received stray response for transaction {transactionId}");
            }
        }

        /// <summary>
        /// Process incoming request and route to appropriate server transaction
        /// </summary>
        public SipTransaction? ProcessRequest(string requestMessage)
        {
            var method = ExtractMethod(requestMessage);
            
            // Handle ACK requests specially
            if (method?.ToUpper() == "ACK")
            {
                HandleAckRequest(requestMessage);
                return null;
            }

            var transactionId = GenerateServerTransactionId(requestMessage);
            
            // Check for existing transaction (retransmission)
            if (_transactions.TryGetValue(transactionId, out var existingTransaction))
            {
                existingTransaction.ProcessMessage(requestMessage);
                return existingTransaction;
            }

            // Create new server transaction
            return CreateServerTransaction(requestMessage);
        }

        /// <summary>
        /// Generate RFC 3261 compliant client transaction ID
        /// </summary>
        private string GenerateClientTransactionId(string requestMessage)
        {
            // RFC 3261 Section 17.1.3: Client transaction ID = Via branch parameter
            var viaBranch = ExtractViaBranch(requestMessage);
            var method = ExtractMethod(requestMessage);
            
            return $"{viaBranch}-{method}-client";
        }

        /// <summary>
        /// Generate RFC 3261 compliant server transaction ID
        /// </summary>
        private string GenerateServerTransactionId(string requestMessage)
        {
            // RFC 3261 Section 17.2.3: Server transaction ID = Via branch + sent-by + method
            var viaBranch = ExtractViaBranch(requestMessage);
            var viaSentBy = ExtractViaSentBy(requestMessage);
            var method = ExtractMethod(requestMessage);
            
            return $"{viaBranch}-{viaSentBy}-{method}-server";
        }

        /// <summary>
        /// Generate client transaction ID from response message
        /// </summary>
        private string GenerateClientTransactionIdFromResponse(string responseMessage)
        {
            var viaBranch = ExtractViaBranch(responseMessage);
            var method = ExtractCSeqMethod(responseMessage);
            
            return $"{viaBranch}-{method}-client";
        }

        /// <summary>
        /// Handle ACK requests (which don't create transactions)
        /// </summary>
        private void HandleAckRequest(string ackMessage)
        {
            // ACK requests are handled by the dialog layer, not transactions
            // For error responses, ACK is handled by the INVITE client transaction
            Console.WriteLine("[TRANSACTION] Received ACK request - forwarding to dialog layer");
        }

        /// <summary>
        /// Clean up expired transactions
        /// </summary>
        private void CleanupExpiredTransactions(object? sender, ElapsedEventArgs e)
        {
            var expiredTransactions = _transactions.Values
                .Where(t => t.State == TransactionState.Terminated)
                .ToList();

            foreach (var transaction in expiredTransactions)
            {
                if (_transactions.TryRemove(transaction.TransactionId, out var removed))
                {
                    removed.Dispose();
                }
            }

            Console.WriteLine($"[TRANSACTION] Cleaned up {expiredTransactions.Count} expired transactions");
        }

        private void OnTransactionCompleted(object? sender, string transactionId)
        {
            TransactionCompleted?.Invoke(this, new TransactionEventArgs(transactionId, "completed"));
        }

        private void OnTransactionFailed(object? sender, string error)
        {
            var transaction = sender as SipTransaction;
            TransactionFailed?.Invoke(this, new TransactionEventArgs(transaction?.TransactionId ?? "unknown", error));
        }

        #region Header Extraction Methods

        private string ExtractMethod(string sipMessage)
        {
            var lines = sipMessage.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var requestLine = lines[0];
                var parts = requestLine.Split(' ');
                if (parts.Length > 0)
                {
                    return parts[0];
                }
            }
            return string.Empty;
        }

        private string ExtractViaBranch(string sipMessage)
        {
            var viaHeader = ExtractHeader(sipMessage, "Via");
            if (!string.IsNullOrEmpty(viaHeader))
            {
                var branchIndex = viaHeader.IndexOf("branch=");
                if (branchIndex >= 0)
                {
                    var branchStart = branchIndex + 7; // Length of "branch="
                    var branchEnd = viaHeader.IndexOf(';', branchStart);
                    if (branchEnd == -1) branchEnd = viaHeader.Length;
                    
                    return viaHeader.Substring(branchStart, branchEnd - branchStart);
                }
            }
            return string.Empty;
        }

        private string ExtractViaSentBy(string sipMessage)
        {
            var viaHeader = ExtractHeader(sipMessage, "Via");
            if (!string.IsNullOrEmpty(viaHeader))
            {
                // Extract the sent-by portion (host:port)
                var protocolEnd = viaHeader.IndexOf(' ');
                if (protocolEnd >= 0 && protocolEnd + 1 < viaHeader.Length)
                {
                    var sentBy = viaHeader.Substring(protocolEnd + 1);
                    var paramIndex = sentBy.IndexOf(';');
                    if (paramIndex >= 0)
                    {
                        sentBy = sentBy.Substring(0, paramIndex);
                    }
                    return sentBy.Trim();
                }
            }
            return string.Empty;
        }

        private string ExtractCSeqMethod(string sipMessage)
        {
            var cseqHeader = ExtractHeader(sipMessage, "CSeq");
            if (!string.IsNullOrEmpty(cseqHeader))
            {
                var parts = cseqHeader.Split(' ');
                if (parts.Length > 1)
                {
                    return parts[1];
                }
            }
            return string.Empty;
        }

        private string ExtractHeader(string sipMessage, string headerName)
        {
            var lines = sipMessage.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase))
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex + 1 < line.Length)
                    {
                        return line.Substring(colonIndex + 1).Trim();
                    }
                }
            }
            return string.Empty;
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Stop();
                _cleanupTimer?.Dispose();
                
                // Dispose all transactions
                foreach (var transaction in _transactions.Values)
                {
                    transaction.Dispose();
                }
                _transactions.Clear();
                
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Transaction event arguments
    /// </summary>
    public class TransactionEventArgs : EventArgs
    {
        public string TransactionId { get; }
        public string Message { get; }

        public TransactionEventArgs(string transactionId, string message)
        {
            TransactionId = transactionId;
            Message = message;
        }
    }

}
