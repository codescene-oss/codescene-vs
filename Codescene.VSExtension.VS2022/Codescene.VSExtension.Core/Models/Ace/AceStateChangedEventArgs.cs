using System;

namespace Codescene.VSExtension.Core.Models.Ace
{
    /// <summary>
    /// Event arguments for ACE state change events.
    /// </summary>
    public class AceStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The state before the change.
        /// </summary>
        public AceState PreviousState { get; }

        /// <summary>
        /// The new state after the change.
        /// </summary>
        public AceState NewState { get; }

        /// <summary>
        /// Optional error associated with the state change.
        /// </summary>
        public Exception Error { get; }

        public AceStateChangedEventArgs(AceState previousState, AceState newState, Exception error = null)
        {
            PreviousState = previousState;
            NewState = newState;
            Error = error;
        }
    }
}
