using System;
using System.Timers;

namespace WindowsSipPhone.Core.Transactions
{
    /// <summary>
    /// RFC 3261 Section 17.2.2 - Non-INVITE Server Transaction (NIST)
    /// Handles incoming non-INVITE requests (REGISTER, BYE, OPTIONS, etc.)
    /// and the responses the TU sends for them.
    ///
    /// State machine (RFC 3261 Figure 8):
    ///   Trying --(TU sends 1xx)--&gt; Proceeding --(TU sends final)--&gt; Completed --(Timer J)--&gt; Terminated
    ///   Trying --(TU sends final directly)--&gt; Completed --(Timer J)--&gt; Terminated
    /// </summary>
    public class NonInviteServerTransaction : SipTransaction
    {
        private string _lastResponse = string.Empty;
        private readonly bool _isReliableTransport;

        // RFC 3261 17.2.2 - Timer J: 64*T1 for unreliable transports,
        // fires immediately for reliable transports (e.g. TCP/TLS).
        private const int TimerJ = 64 * T1;

        public NonInviteServerTransaction(string transactionId, string method, bool isReliableTransport = true)
            : base(transactionId, method, true)
        {
            _isReliableTransport = isReliableTransport;
            // RFC 3261 17.2.2: the transaction starts in the Trying state
            // upon receipt of the request.
            State = TransactionState.Trying;
        }

        /// <summary>
        /// Called by the TU (SipPhoneService) to send a response for this request.
        /// </summary>
        public void SendResponse(string responseMessage)
        {
            var statusCode = ExtractStatusCode(responseMessage);

            if (statusCode >= 100 && statusCode < 200)
            {
                // Provisional response - move Trying -> Proceeding
                SendMessage(responseMessage);
                if (State == TransactionState.Trying)
                {
                    ChangeState(TransactionState.Proceeding);
                }
                return;
            }

            // Final response (2xx-6xx): move to Completed and start Timer J
            _lastResponse = responseMessage;
            SendMessage(responseMessage);
            ChangeState(TransactionState.Completed);

            if (_isReliableTransport)
            {
                // Reliable transport: no retransmissions expected, terminate immediately
                ChangeState(TransactionState.Terminated);
            }
            else
            {
                StartTimer(TimerJ);
            }
        }

        /// <summary>
        /// Process a retransmitted request for this transaction. Non-INVITE
        /// transactions never receive an ACK (RFC 3261 17.2.2), so this only
        /// needs to handle request retransmissions.
        /// </summary>
        public override void ProcessMessage(string sipMessage)
        {
            if (string.IsNullOrEmpty(_lastResponse))
            {
                // No response sent yet (still Trying/Proceeding) - nothing to
                // retransmit; the retransmitted request is simply absorbed.
                return;
            }

            if (State == TransactionState.Proceeding || State == TransactionState.Completed)
            {
                // RFC 3261 17.2.2: retransmit the last provisional/final response
                SendMessage(_lastResponse);
            }
        }

        protected override void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (State == TransactionState.Completed)
            {
                // Timer J fired: done absorbing request retransmissions
                ChangeState(TransactionState.Terminated);
            }
        }

        private int ExtractStatusCode(string sipMessage)
        {
            var lines = sipMessage.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var parts = lines[0].Split(' ');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int code))
                {
                    return code;
                }
            }
            return 0;
        }
    }
}
