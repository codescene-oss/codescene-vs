// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Enums;

namespace Codescene.VSExtension.Core.Models.Ace
{
    /// <summary>
    /// Event arguments for ACE state change events.
    /// </summary>
    public class AceStateChangedEventArgs : EventArgs
    {
        public AceStateChangedEventArgs(AceState previousState, AceState newState, Exception error = null)
        {
            PreviousState = previousState;
            NewState = newState;
            Error = error;
        }

        /// <summary>
        /// Gets the state before the change.
        /// </summary>
        public AceState PreviousState { get; }

        /// <summary>
        /// Gets the new state after the change.
        /// </summary>
        public AceState NewState { get; }

        /// <summary>
        /// Gets optional error associated with the state change.
        /// </summary>
        public Exception Error { get; }
    }
}
