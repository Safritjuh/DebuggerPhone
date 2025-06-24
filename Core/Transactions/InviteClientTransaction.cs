using System;
using System.Timers;

namespace WindowsSipPhone.Core.Transactions
{
    /// <summary>
    /// RFC 3261 Section 17.1.1 - INVITE Client Transaction (ICT)
    /// Handles outgoing INVITE requests and their responses
    /// </summary>
    public class InviteClientTransaction : SipTransaction
    {
        private string _lastRequest = string.Empty;
        private int _retransmissionCount = 0;
        private double _currentTimerInterval = T1;

        public InviteClientTransaction(string transactionId) 
            : base(transactionId, "INVITE", false)
        {
        }

        /// <summary>
        /// Send initial INVITE request
        /// </summary>
        public void SendInvite(string inviteMessage)
        {
            _lastRequest = inviteMessage;
            ChangeState(TransactionState.Calling);
            SendMessage(inviteMessage);
            
            // Start Timer A (retransmission timer)
            StartTimer(T1);
        }

        /// <summary>
        /// Process incoming response for this INVITE transaction
        /// </summary>
        public override void ProcessMessage(string sipMessage)
        {
            if (!sipMessage.StartsWith("SIP/2.0"))
            {
                // Not a response, ignore
                return;
            }

            var statusCode = ExtractStatusCode(sipMessage);
            
            if (statusCode >= 100 && statusCode < 200)
            {
                // Provisional response (1xx)
                HandleProvisionalResponse(sipMessage);
            }
            else if (statusCode >= 200 && statusCode < 300)
            {
                // Success response (2xx)
                HandleSuccessResponse(sipMessage);
            }
            else if (statusCode >= 300)
            {
                // Error response (3xx, 4xx, 5xx, 6xx)
                HandleErrorResponse(sipMessage);
            }
        }

        private void HandleProvisionalResponse(string response)
        {
            // RFC 3261 Section 17.1.1.2 - Proceeding State
            if (State == TransactionState.Calling)
            {
                ChangeState(TransactionState.Proceeding);
                StopTimer(); // Stop Timer A retransmissions
            }
            
            // Provisional responses don't change the state if already in Proceeding
        }

        private void HandleSuccessResponse(string response)
        {
            // RFC 3261 Section 17.1.1.3 - Completed State
            ChangeState(TransactionState.Completed);
            StopTimer();
            
            // For 2xx responses to INVITE, the transaction terminates immediately
            // The ACK is handled by the dialog, not the transaction
            ChangeState(TransactionState.Terminated);
        }

        private void HandleErrorResponse(string response)
        {
            // RFC 3261 Section 17.1.1.3 - Completed State
            ChangeState(TransactionState.Completed);
            StopTimer();
            
            // Send ACK for error responses (handled by transaction, not dialog)
            var ackMessage = CreateAckForErrorResponse(response);
            SendMessage(ackMessage);
            
            // Start Timer D
            StartTimer(32000); // 32 seconds for unreliable transport
        }

        protected override void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            switch (State)
            {
                case TransactionState.Calling:
                    HandleTimerA(); // Retransmission timer
                    break;
                    
                case TransactionState.Completed:
                    HandleTimerD(); // Wait timer for ACK absorption
                    break;
            }
        }

        private void HandleTimerA()
        {
            // RFC 3261 Section 17.1.1.2 - Timer A (retransmission)
            _retransmissionCount++;
            
            if (_retransmissionCount >= 7) // Maximum retransmissions
            {
                // Transaction timeout
                ChangeState(TransactionState.Terminated);
                OnTransactionFailed("INVITE transaction timeout");
                return;
            }
            
            // Retransmit the request
            SendMessage(_lastRequest);
            
            // Double the timer interval (exponential backoff), cap at T2
            _currentTimerInterval = Math.Min(_currentTimerInterval * 2, T2);
            StartTimer(_currentTimerInterval);
        }

        private void HandleTimerD()
        {
            // RFC 3261 Section 17.1.1.3 - Timer D expiration
            ChangeState(TransactionState.Terminated);
        }

        private string CreateAckForErrorResponse(string errorResponse)
        {
            // Extract necessary headers from error response
            var callId = ExtractHeader(errorResponse, "Call-ID");
            var from = ExtractHeader(errorResponse, "From");
            var to = ExtractHeader(errorResponse, "To");
            var via = ExtractHeader(errorResponse, "Via");
            var cseq = ExtractHeader(errorResponse, "CSeq");
            
            // Change method in CSeq to ACK
            var cseqParts = cseq.Split(' ');
            var ackCSeq = cseqParts.Length > 0 ? $"{cseqParts[0]} ACK" : "1 ACK";
            
            // Create ACK request
            var ack = $"ACK sip:placeholder@domain SIP/2.0\r\n" +
                     $"Via: {via}\r\n" +
                     $"From: {from}\r\n" +
                     $"To: {to}\r\n" +
                     $"Call-ID: {callId}\r\n" +
                     $"CSeq: {ackCSeq}\r\n" +
                     $"Content-Length: 0\r\n" +
                     $"\r\n";
            
            return ack;
        }

        private int ExtractStatusCode(string sipResponse)
        {
            var lines = sipResponse.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var statusLine = lines[0];
                var parts = statusLine.Split(' ');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int code))
                {
                    return code;
                }
            }
            return 0;
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
    }
}
