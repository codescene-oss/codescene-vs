// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models.Ace;
using Codescene.VSExtension.VS2022.Options;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

namespace Codescene.VSExtension.VS2022.Handlers
{
    /// <summary>
    /// Handles ACE state change events for side effects like refreshing analysis
    /// or showing user notifications.
    /// </summary>
    [Export(typeof(AceStateChangeHandler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceStateChangeHandler
    {
        private readonly IAceStateService _aceStateService;
        private readonly ILogger _logger;
        private readonly IPreflightManager _preflightManager;
        private readonly IAsyncTaskScheduler _scheduler;

        [ImportingConstructor]
        public AceStateChangeHandler(IAceStateService aceStateService, ILogger logger, IPreflightManager preflightManager, IAsyncTaskScheduler scheduler)
        {
            _aceStateService = aceStateService;
            _logger = logger;
            _preflightManager = preflightManager;
            _scheduler = scheduler;

            // Subscribe to state changes
            _aceStateService.StateChanged += OnStateChanged;

            // Subscribe to auth token changes
            General.AuthTokenChanged += OnAuthTokenChanged;
        }

        private void OnAuthTokenChanged(object sender, EventArgs e)
        {
            _logger.Debug("Auth token changed");
            var settings = sender as General;
            if (settings == null)
            {
                _logger.Debug("Unable to read settings after token change");
                return;
            }

            var hasAuthToken = !string.IsNullOrWhiteSpace(settings.AuthToken);
            _preflightManager.SetHasAceToken(hasAuthToken);
            _scheduler.Schedule(ct => RefreshWindowsAsync());
        }

        private void OnStateChanged(object sender, AceStateChangedEventArgs e)
        {
            _logger.Debug($"AceStateChangeHandler: {e.PreviousState} -> {e.NewState}");

            // Handle specific state transitions
            switch (e.NewState)
            {
                case AceState.Enabled when e.PreviousState == AceState.Disabled:
                case AceState.Enabled when e.PreviousState == AceState.Loading:
                    OnAceEnabled();
                    break;

                case AceState.Disabled when e.PreviousState == AceState.Enabled:
                    OnAceDisabled();
                    break;

                case AceState.Offline when e.PreviousState != AceState.Offline:
                    OnAceWentOffline();
                    break;

                case AceState.Enabled when e.PreviousState == AceState.Offline:
                    OnAceCameBackOnline();
                    break;
            }

            _scheduler.Schedule(ct => RefreshWindowsAsync());
    }

        /// <summary>
        /// Called when ACE becomes enabled.
        /// Refreshes deltas to add refactorable functions to the code health monitor.
        /// </summary>
        private void OnAceEnabled()
        {
            _logger.Debug("ACE enabled");
        }

        /// <summary>
        /// Called when ACE becomes disabled.
        /// Refreshes deltas to remove refactorable functions from the code health monitor.
        /// </summary>
        private void OnAceDisabled()
        {
            _logger.Debug("ACE disabled");
        }

        /// <summary>
        /// Called when ACE transitions to offline mode.
        /// Shows an information message to the user.
        /// </summary>
        private void OnAceWentOffline()
        {
            _logger.Debug("ACE went offline");
        }

        /// <summary>
        /// Called when ACE comes back online after being offline.
        /// Shows an information message and refreshes the code health monitor.
        /// </summary>
        private void OnAceCameBackOnline()
        {
            _logger.Debug("ACE back online");
        }

        private async Task RefreshWindowsAsync()
        {
            try
            {
                await CodeSceneToolWindow.UpdateViewAsync();
                await CodeSmellDocumentationWindow.RefreshViewAsync();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to refresh Tool Windows", ex);
            }
        }
    }
}
