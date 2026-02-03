// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models.Ace;

namespace Codescene.VSExtension.Core.Application.Ace
{
    /// <summary>
    /// Implementation of IAceStateService that tracks ACE feature state
    /// and fires events on state changes.
    /// </summary>
    [Export(typeof(IAceStateService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceStateService : IAceStateService
    {
        private readonly ILogger _logger;

        public AceState CurrentState { get; private set; } = AceState.Loading;

        public Exception LastError { get; private set; }

        public event EventHandler<AceStateChangedEventArgs> StateChanged;

        [ImportingConstructor]
        public AceStateService(ILogger logger)
        {
            _logger = logger;
        }

        public void SetState(AceState state, Exception error = null)
        {
            var previousState = CurrentState;

            if (ShouldSkipStateChange(previousState, state, error))
            {
                return;
            }

            CurrentState = state;
            LastError = error;

            _logger.Debug($"ACE state changed: {previousState} -> {state}");

            // Log user-facing messages for important transitions
            LogStateTransition(previousState, state);

            // Fire the state changed event
            StateChanged?.Invoke(this, new AceStateChangedEventArgs(previousState, state, error));
        }

        private bool ShouldSkipStateChange(AceState previousState, AceState newState, Exception error)
        {
            // Skip if state hasn't changed and no error update
            return previousState == newState && error == null && LastError == null;
        }

        public void SetError(Exception error)
        {
            LastError = error;

            if (CurrentState != AceState.Error)
            {
                SetState(AceState.Error, error);
            }
            else
            {
                // State is already Error, just update the error and fire event
                _logger.Debug($"ACE error updated: {error?.Message}");
                StateChanged?.Invoke(this, new AceStateChangedEventArgs(AceState.Error, AceState.Error, error));
            }
        }

        /// <inheritdoc />
        public void ClearError()
        {
            if (LastError != null)
            {
                _logger.Debug("ACE error cleared");
                LastError = null;
            }
        }

        private void LogStateTransition(AceState previousState, AceState newState)
        {
            if (IsTransitionToOnline(previousState, newState))
            {
                _logger.Info("CodeScene ACE is back online.");
            }
            else if (IsTransitionToOffline(previousState, newState))
            {
                _logger.Warn("CodeScene ACE is running in offline mode. Some features may be unavailable.");
            }
            else if (IsTransitionToError(previousState, newState))
            {
                _logger.Warn("CodeScene ACE encountered an error.");
            }
            else if (IsTransitionToActive(previousState, newState))
            {
                _logger.Info("CodeScene ACE is active.");
            }
            else if (newState == AceState.Disabled)
            {
                _logger.Info("CodeScene ACE has been disabled.");
            }
        }

        private bool IsTransitionToOnline(AceState previousState, AceState newState)
        {
            return newState == AceState.Enabled && previousState == AceState.Offline;
        }

        private bool IsTransitionToOffline(AceState previousState, AceState newState)
        {
            return newState == AceState.Offline && previousState != AceState.Offline;
        }

        private bool IsTransitionToError(AceState previousState, AceState newState)
        {
            return newState == AceState.Error && previousState != AceState.Error;
        }

        private bool IsTransitionToActive(AceState previousState, AceState newState)
        {
            return newState == AceState.Enabled && previousState == AceState.Loading;
        }
    }
}
