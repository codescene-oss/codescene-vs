// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Refactoring;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Refactoring;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.Core.Application.Refactoring
{
    [Export(typeof(IRefactoringOrchestrator))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class RefactoringOrchestrator : IRefactoringOrchestrator
    {
        private readonly IRefactoringHost _host;
        private readonly IAgentSkillRunner _runner;
        private readonly IAgentAvailability _availability;
        private readonly IProviderKeyStore _keyStore;
        private readonly ISettingsProvider _settings;
        private readonly ITelemetryManager _telemetry;
        private readonly IAsyncTaskScheduler _scheduler;
        private readonly AceComponentMapper _mapper;
        private readonly ILogger _logger;
        private readonly object _lock = new object();

        private RefactoringSession _current;

        [ImportingConstructor]
        public RefactoringOrchestrator(
            IRefactoringHost host,
            IAgentSkillRunner runner,
            IAgentAvailability availability,
            IProviderKeyStore keyStore,
            ISettingsProvider settings,
            ITelemetryManager telemetry,
            IAsyncTaskScheduler scheduler,
            AceComponentMapper mapper,
            ILogger logger)
        {
            _host = Guard.NotNull(host, nameof(host));
            _runner = Guard.NotNull(runner, nameof(runner));
            _availability = Guard.NotNull(availability, nameof(availability));
            _keyStore = Guard.NotNull(keyStore, nameof(keyStore));
            _settings = Guard.NotNull(settings, nameof(settings));
            _telemetry = Guard.NotNull(telemetry, nameof(telemetry));
            _scheduler = Guard.NotNull(scheduler, nameof(scheduler));
            _mapper = Guard.NotNull(mapper, nameof(mapper));
            _logger = Guard.NotNull(logger, nameof(logger));
        }

        public RefactoringSession Current
        {
            get
            {
                lock (_lock)
                {
                    return _current;
                }
            }
        }

        public async Task RequestAsync(string source, string path, FnToRefactorModel fn)
        {
            if (!_availability.Usable)
            {
                return;
            }

            if (_host.IsDirty(path))
            {
                var fileName = System.IO.Path.GetFileName(path);
                var confirmed = await _host.ConfirmSaveAsync(fileName);
                if (!confirmed)
                {
                    return;
                }

                await _host.SaveAsync(path);
            }

            var storedProviders = _keyStore.GetAll().Keys.ToList();
            var model = AgentModelResolver.Resolve(_settings, storedProviders);
            var missingProvider = ProviderKeyCheck.FindMissing(model, storedProviders, Environment.GetEnvironmentVariable);

            if (missingProvider != null)
            {
                var message = ProviderKeyCheck.MissingMessage(missingProvider, model);
                await _host.OfferSetApiKeyAsync(message);
                return;
            }

            RefactoringSession oldSession;
            RefactoringSession newSession;
            lock (_lock)
            {
                oldSession = _current;
                newSession = new RefactoringSession(path, fn, source);
                _current = newSession;
            }

            oldSession?.Cancel();

            SendTelemetry(Constants.Telemetry.ACEREFACTORREQUESTED, new Dictionary<string, object>
            {
                { "source", source },
                { "skipCache", false },
            });

            await _host.ShowReviewAsync(_mapper.MapLoading(path, fn));

            _scheduler.Schedule(ct => RunAsync(newSession));
        }

        public void Cancel()
        {
            RefactoringSession session;
            lock (_lock)
            {
                session = _current;
            }

            if (session != null)
            {
                _logger.Debug("Cancelling refactoring");
                session.Cancel();
            }
        }

        public void MarkStale()
        {
            RefactoringSession session;
            lock (_lock)
            {
                session = _current;
            }

            if (session != null && session.State == RefactoringSessionState.Presented)
            {
                session.MarkStale();
                _scheduler.Schedule(ct => _host.UpdateReviewAsync(_mapper.MapSession(session)));
            }
        }

        public void UpdateRange(CliRangeModel range)
        {
            RefactoringSession session;
            lock (_lock)
            {
                session = _current;
            }

            if (session != null)
            {
                session.UpdateRange(range);
            }
        }

        private async Task RunAsync(RefactoringSession session)
        {
            try
            {
                var workDir = AgentWorkingDirectory.ForFile(session.Path);
                var input = AgentInputBuilder.ForRefactoring(session.TaskId, session.Path, session.Function);

                var output = await _runner.RunAsync<AgentOutput>(
                    AgentSkill.RenderCodeFix,
                    workDir,
                    input,
                    progressMessage =>
                    {
                        if (IsActive(session))
                        {
                            session.SetProgress(progressMessage);
                            _scheduler.Schedule(ct => _host.UpdateReviewAsync(_mapper.MapSession(session)));
                        }
                    },
                    session.CancellationTokenSource.Token);

                if (!IsActive(session))
                {
                    return;
                }

                var documentText = _host.ReadDocumentText(session.Path);
                var response = AgentOutputMapper.Map(output, documentText, session.Function, session.Path, _logger);

                session.SetResponse(response);
                await _host.UpdateReviewAsync(_mapper.MapSession(session));

                _scheduler.Schedule(async ct => await _telemetry.SendTelemetryAsync(Constants.Telemetry.ACEREFACTORPRESENTED, new Dictionary<string, object>
                {
                    { "confidence", response.Confidence.Level },
                    { "isCached", false },
                }));
            }
            catch (AgentFailure ex)
            {
                if (!IsActive(session))
                {
                    return;
                }

                var error = ex.Kind == AgentFailureKind.Auth ? AceViewErrorTypes.AUTH : AceViewErrorTypes.GENERIC;
                session.SetError(error);
                await _host.UpdateReviewAsync(_mapper.MapSession(session));
            }
            catch (Exception ex)
            {
                if (!IsActive(session))
                {
                    return;
                }

                _logger.Error("Error during refactoring", ex);

                session.SetError(AceViewErrorTypes.GENERIC);
                await _host.UpdateReviewAsync(_mapper.MapSession(session));
            }
        }

        private bool IsActive(RefactoringSession session)
        {
            lock (_lock)
            {
                return _current == session && !session.CancellationTokenSource.IsCancellationRequested;
            }
        }

        private void SendTelemetry(string eventName, Dictionary<string, object> data)
        {
            _scheduler.Schedule(async ct => await _telemetry.SendTelemetryAsync(eventName, data));
        }
    }
}
