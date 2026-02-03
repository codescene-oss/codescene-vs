using System;
using System.ComponentModel.Composition;
using System.Linq;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Application.Ace
{
    [Export(typeof(IPreflightManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PreflightManager : IPreflightManager
    {
        private readonly ICliExecutor _executer;
        private readonly ILogger _logger;
        private readonly IAceStateService _aceStateService;
        private readonly ISettingsProvider _settingsProvider;
        private PreFlightResponseModel _preflightResponse;
        private AutoRefactorConfig _autoRefactorConfig;

        [ImportingConstructor]
        public PreflightManager(ICliExecutor executer, ILogger logger, IAceStateService aceStateService, ISettingsProvider settingsProvider)
        {
            _executer = executer;
            _logger = logger;
            _aceStateService = aceStateService;
            _settingsProvider = settingsProvider;
        }

        public PreFlightResponseModel RunPreflight(bool force = false)
        {
            _logger.Debug($"Running preflight with force: {force}");
            var hasToken = !string.IsNullOrWhiteSpace(_settingsProvider.AuthToken);
            _aceStateService.SetState(AceState.Loading);
            try
            {
                var response = _executer.Preflight(force);

                if (response != null)
                {
                    _logger.Info("Got preflight response. ACE service is active.");
                    _preflightResponse = response;
                    _aceStateService.SetState(AceState.Enabled);
                    _autoRefactorConfig = new ()
                    {
                        Activated = true,
                        Visible = true,
                        Disabled = !hasToken,
                        AceStatus = GetAceStatus(_aceStateService.CurrentState),
                    };

                    return response;
                }
                else
                {
                    _logger.Info("Problem getting preflight response. ACE service is down.");
                    _preflightResponse = null;
                    _autoRefactorConfig = new ()
                    {
                        Activated = true,
                        Visible = true,
                        Disabled = false,
                        AceStatus = GetAceStatus(_aceStateService.CurrentState),
                    };

                    _aceStateService.SetState(AceState.Offline);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Problem getting preflight response. ACE service is down.", ex);
                _preflightResponse = null;
                _aceStateService.SetState(AceState.Error, ex);

                _autoRefactorConfig = new ()
                {
                    Activated = true,
                    Visible = true,
                    Disabled = false,
                    AceStatus = GetAceStatus(_aceStateService.CurrentState),
                };
            }
            return null;
        }

        private AceStatusType GetAceStatus(AceState state)
        {
            var hasToken = !string.IsNullOrWhiteSpace(_settingsProvider.AuthToken);

            return new AceStatusType
            {
                Status = MapAceState(state),
                HasToken = hasToken,
            };
        }

        public bool IsSupportedLanguage(string extension) => _preflightResponse?.FileTypes.Contains(extension.Replace(".", string.Empty).ToLower()) ?? false;

        public PreFlightResponseModel GetPreflightResponse()
        {
            if (_preflightResponse == null)
            {
                return RunPreflight(true);
            }

            return _preflightResponse;
        }

        public AutoRefactorConfig GetAutoRefactorConfig() => _autoRefactorConfig ?? new () { Activated = true, Visible = true, Disabled = false, AceStatus = new AceStatusType { HasToken = false, Status = MapAceState(AceState.Disabled) } };

        public void SetHasAceToken(bool hasAceToken)
        {
            if (_autoRefactorConfig?.AceStatus == null)
            {
                return;
            }
            _autoRefactorConfig.AceStatus.HasToken = hasAceToken;
            _autoRefactorConfig.Disabled = !hasAceToken;
        }

        private static string MapAceState(AceState state)
        {
            return state.ToString().ToLower();
        }
    }
}
