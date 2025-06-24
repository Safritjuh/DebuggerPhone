using System;
using System.Timers;

namespace WindowsSipPhone.Core.Transactions
{
    /// <summary>
    /// RFC 3261 Section 17.1.2 - Non-INVITE Client Transaction (NICT)
    /// Handles outgoing non-INVITE requests (REGISTER, BYE, CANCEL, etc.) and their responses
    /// </summary>
    public class NonInviteClientTransaction : SipTransaction
    {
        private string _lastRequest = string.Empty;
        private int _retransmissionCount = 0;
        private double _currentTimerInterval = T1;

        public NonInviteClientTransaction(string transactionId, string method) 
            : base(transactionId, method, false)
        {
        }

        /// <summary>
        /// Send the non-INVITE request
        /// </summary>
        public void SendRequest(string requestMessage)
        {
            _lastRequest = requestMessage;
            ChangeState(TransactionState.Trying);
            SendMessage(requestMessage);
            
            // Start Timer E (retransmission timer) for unreliable transport
            StartTimer(T1);
        }

        /// <summary>
        /// Process incoming response for this non-INVITE transaction
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
            else if (statusCode >= 200)
            {
                // Final response (2xx, 3xx, 4xx, 5xx, 6xx)
                HandleFinalResponse(sipMessage);
            }
        }

        private void HandleProvisionalResponse(string response)
        {
            // RFC 3261 Section 17.1.2.2 - Proceeding State
            if (State == TransactionState.Trying)
            {
                ChangeState(TransactionState.Proceeding);
                StopTimer(); // Stop Timer E retransmissions
            }
            
            // Additional provisional responses don't change state
        }

        private void HandleFinalResponse(string response)
        {
            // RFC 3261 Section 17.1.2.3 - Completed State
            ChangeState(TransactionState.Completed);
            StopTimer();
            
            // Start Timer K for unreliable transport
            // Timer K = T4 for unreliable transport, 0 for reliable transport
            StartTimer(T4); // Assuming unreliable transport (UDP)
        }

        protected override void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            switch (State)
            {
                case TransactionState.Trying:
                case TransactionState.Proceeding:
                    HandleTimerE(); // Retransmission timer
                    break;
                    
                case TransactionState.Completed:
                    HandleTimerK(); // Wait timer
                    break;
            }
        }

        private void HandleTimerE()
        {
            // RFC 3261 Section 17.1.2.2 - Timer E (retransmission)
            _retransmissionCount++;
            
            // Timer E fires with intervals T1, 2*T1, 4*T1, ..., up to T2
            // Maximum retransmissions based on 64*T1 total time
            if (_retransmissionCount >= 11) // Approximately 64*T1 total time
            {
                // Transaction timeout
                ChangeState(TransactionState.Terminated);
                OnTransactionFailed($"{Method} transaction timeout");
                return;
            }
            
            // Retransmit the request
            SendMessage(_lastRequest);
            
            // Double the timer interval (exponential backoff), cap at T2
            _currentTimerInterval = Math.Min(_currentTimerInterval * 2, T2);
            StartTimer(_currentTimerInterval);
        }

        private void HandleTimerK()
        {
            // RFC 3261 Section 17.1.2.3 - Timer K expiration
            ChangeState(TransactionState.Terminated);
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
    }
}
