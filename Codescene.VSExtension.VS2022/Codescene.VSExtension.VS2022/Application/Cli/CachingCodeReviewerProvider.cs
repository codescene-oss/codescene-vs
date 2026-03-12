// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Codescene.VSExtension.VS2022.Application.Cli
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CachingCodeReviewerProvider : ICodeReviewer
    {
        private readonly CachingCodeReviewer _inner;
        private readonly CodeHealthMonitorNotifier _notifier;
        private readonly IAsyncTaskScheduler _scheduler;
        private readonly IPreflightManager _preflightManager;

        [ImportingConstructor]
        public CachingCodeReviewerProvider(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executor,
            ITelemetryManager telemetryManager,
            IGitService git,
            IAsyncTaskScheduler scheduler,
            IPreflightManager preflightManager)
        {
            _scheduler = scheduler;
            _preflightManager = preflightManager;
            _notifier = new CodeHealthMonitorNotifier();
            _notifier.ViewUpdateRequested += OnViewUpdateRequested;

            var baseReviewer = new CodeReviewer(logger, mapper, executor, telemetryManager, git, _notifier, _preflightManager);
            _inner = new CachingCodeReviewer(
                innerReviewer: baseReviewer,
                logger: logger,
                git: git,
                telemetryManager: telemetryManager,
                notifier: _notifier);
        }

        public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            return _inner.ReviewAsync(path, content, isBaseline, operationGeneration, cancellationToken);
        }

        public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            return _inner.DeltaAsync(review, currentCode, precomputedBaselineRawScore, operationGeneration, cancellationToken);
        }

        public Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            return _inner.ReviewAndBaselineAsync(path, currentCode, operationGeneration, cancellationToken);
        }

        public Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            return _inner.ReviewWithDeltaAsync(path, content, operationGeneration, cancellationToken);
        }

        public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default)
        {
            return _inner.GetOrComputeBaselineRawScoreAsync(path, baselineContent, operationGeneration, cancellationToken);
        }

        private void OnViewUpdateRequested(object sender, EventArgs e)
        {
            _scheduler.Schedule(ct => CodeSceneToolWindow.UpdateViewAsync());
        }
    }
}
