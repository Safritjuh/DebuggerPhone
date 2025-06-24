using System;
using System.Collections.Generic;
using System.Timers;

namespace WindowsSipPhone.Core.Transactions
{
    /// <summary>
    /// RFC 3261 Section 17 - SIP Transaction Base Class
    /// Implements the fundamental transaction behavior for SIP protocol compliance
    /// </summary>
    public abstract class SipTransaction : IDisposable
    {
        protected readonly System.Timers.Timer _timer;
        protected readonly object _stateLock = new object();
        private bool _disposed = false;

        // RFC 3261 Transaction properties
        public string TransactionId { get; }
        public TransactionState State { get; protected set; }
        public DateTime CreatedTime { get; }
        public string Method { get; }
        public bool IsServerTransaction { get; }
        
        // RFC 3261 Timer values (in milliseconds)
        protected const int T1 = 500;   // RTT estimate
        protected const int T2 = 4000;  // Maximum retransmit interval
        protected const int T4 = 5000;  // Maximum duration to wait for responses
        
        public event EventHandler<TransactionStateChangedEventArgs>? StateChanged;
        public event EventHandler<string>? MessageToSend;
        public event EventHandler<string>? TransactionCompleted;
        public event EventHandler<string>? TransactionFailed;

        protected SipTransaction(string transactionId, string method, bool isServerTransaction)
        {
            TransactionId = transactionId;
            Method = method;
            IsServerTransaction = isServerTransaction;
            CreatedTime = DateTime.UtcNow;
            State = TransactionState.Trying;
            
            _timer = new System.Timers.Timer();
            _timer.Elapsed += OnTimerElapsed;
        }

        /// <summary>
        /// Process incoming SIP message for this transaction
        /// </summary>
        public abstract void ProcessMessage(string sipMessage);

        /// <summary>
        /// Handle timer events (retransmissions, timeouts)
        /// </summary>
        protected abstract void OnTimerElapsed(object? sender, ElapsedEventArgs e);

        /// <summary>
        /// Change transaction state with proper event handling
        /// </summary>
        protected virtual void ChangeState(TransactionState newState)
        {
            lock (_stateLock)
            {
                if (State == newState) return;
                
                var oldState = State;
                State = newState;
                
                OnStateChanged(oldState, newState);
                StateChanged?.Invoke(this, new TransactionStateChangedEventArgs(oldState, newState));
                  // Handle terminal states
                if (newState == TransactionState.Terminated)
                {
                    _timer?.Stop();
                    OnTransactionCompleted(TransactionId);
                }
            }
        }

        /// <summary>
        /// Override in derived classes for state-specific behavior
        /// </summary>
        protected virtual void OnStateChanged(TransactionState oldState, TransactionState newState)
        {
            // Base implementation - override in derived classes
        }        /// <summary>
        /// Send message through the transaction
        /// </summary>
        protected void SendMessage(string message)
        {
            MessageToSend?.Invoke(this, message);
        }

        /// <summary>
        /// Signal transaction completion
        /// </summary>
        protected void OnTransactionCompleted(string reason)
        {
            TransactionCompleted?.Invoke(this, reason);
        }

        /// <summary>
        /// Signal transaction failure
        /// </summary>
        protected void OnTransactionFailed(string reason)
        {
            TransactionFailed?.Invoke(this, reason);
        }

        /// <summary>
        /// Start transaction timer with specified interval
        /// </summary>
        protected void StartTimer(double intervalMs)
        {
            _timer.Interval = intervalMs;
            _timer.Start();
        }

        /// <summary>
        /// Stop transaction timer
        /// </summary>
        protected void StopTimer()
        {
            _timer?.Stop();
        }

        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _timer?.Stop();
                _timer?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// RFC 3261 Transaction States
    /// </summary>
    public enum TransactionState
    {
        // Client Transaction States
        Trying,      // Initial state for client transactions
        Calling,     // INVITE client transaction specific
        Proceeding,  // Received provisional response
        Completed,   // Received final response
        
        // Server Transaction States  
        Confirmed,   // INVITE server transaction specific
        Terminated   // Final state for all transactions
    }

    /// <summary>
    /// Transaction state change event arguments
    /// </summary>
    public class TransactionStateChangedEventArgs : EventArgs
    {
        public TransactionState OldState { get; }
        public TransactionState NewState { get; }

        public TransactionStateChangedEventArgs(TransactionState oldState, TransactionState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }
}
