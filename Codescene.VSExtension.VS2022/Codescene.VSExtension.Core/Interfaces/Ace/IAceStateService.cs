// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models.Ace;

namespace Codescene.VSExtension.Core.Interfaces.Ace
{
    /// <summary>
    /// Service for tracking and managing ACE feature state.
    /// </summary>
    public interface IAceStateService
    {
        /// <summary>
        /// Gets the current ACE state.
        /// </summary>
        AceState CurrentState { get; }

        /// <summary>
        /// Gets the last error that occurred, if any.
        /// </summary>
        Exception LastError { get; }

        /// <summary>
        /// Event fired when the ACE state changes.
        /// </summary>
        event EventHandler<AceStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Sets the ACE state and optionally an associated error.
        /// </summary>
        /// <param name="state">The new state.</param>
        /// <param name="error">Optional error associated with the state change.</param>
        void SetState(AceState state, Exception error = null);

        /// <summary>
        /// Sets an error without necessarily changing the state to Error.
        /// If the current state is not Error, transitions to Error state.
        /// </summary>
        /// <param name="error">The error to set.</param>
        void SetError(Exception error);

        /// <summary>
        /// Clears the last error without changing the current state.
        /// </summary>
        void ClearError();
    }
}
