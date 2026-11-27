// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.VS2022.Options;
using Codescene.VSExtension.VS2022.Tagger;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

namespace Codescene.VSExtension.VS2022.Handlers
{
    [Export(typeof(AgentAvailabilityBinder))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class AgentAvailabilityBinder
    {
        private readonly IAgentAvailability _availability;
        private readonly ILogger _logger;
        private readonly IAsyncTaskScheduler _scheduler;
        private readonly IAceRefactorSuggestedActionsNotifier _lightBulbNotifier;

        [ImportingConstructor]
        public AgentAvailabilityBinder(
            IAgentAvailability availability,
            ILogger logger,
            IAsyncTaskScheduler scheduler,
            IAceRefactorSuggestedActionsNotifier lightBulbNotifier)
        {
            _availability = availability;
            _logger = logger;
            _scheduler = scheduler;
            _lightBulbNotifier = lightBulbNotifier;

            General.RefactoringAgentEnabledChanged += OnRefactoringAgentEnabledChanged;
            General.AuthTokenChanged += OnAuthTokenChanged;
            _availability.Changed += OnAvailabilityChanged;
        }

        public void Unsubscribe()
        {
            General.RefactoringAgentEnabledChanged -= OnRefactoringAgentEnabledChanged;
            General.AuthTokenChanged -= OnAuthTokenChanged;
            _availability.Changed -= OnAvailabilityChanged;
        }

        private void OnRefactoringAgentEnabledChanged(object sender, EventArgs e)
        {
            _logger.Debug("Refactoring agent enabled setting changed");
            _availability.NotifySettingsChanged();
        }

        private void OnAuthTokenChanged(object sender, EventArgs e)
        {
            _logger.Debug("Auth token changed");
            _availability.NotifySettingsChanged();
        }

        private void OnAvailabilityChanged(object sender, EventArgs e)
        {
            _logger.Debug("Agent availability changed, refreshing views");
            _scheduler.Schedule(ct => RefreshViewsAsync());
        }

        private async Task RefreshViewsAsync()
        {
            try
            {
                await CodeSceneToolWindow.UpdateViewAsync();

                if (CodeSmellDocumentationWindow.IsCreated())
                {
                    await CodeSmellDocumentationWindow.RefreshViewAsync();
                }

                _lightBulbNotifier.NotifyAll();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to refresh views after agent availability change", ex);
            }
        }
    }
}
