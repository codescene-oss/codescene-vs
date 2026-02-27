// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;

namespace Codescene.VSExtension.VS2022.Application.Cli
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CachingCodeReviewerProvider : ICodeReviewer
    {
        private readonly CachingCodeReviewer _inner;
        private readonly CodeHealthMonitorNotifier _notifier;

        [ImportingConstructor]
        public CachingCodeReviewerProvider(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executor,
            ITelemetryManager telemetryManager,
            IGitService git)
        {
            var baseReviewer = new CodeReviewer(logger, mapper, executor, telemetryManager, git);
            _notifier = new CodeHealthMonitorNotifier();
            _notifier.ViewUpdateRequested += OnViewUpdateRequested;

            _inner = new CachingCodeReviewer(
                innerReviewer: baseReviewer,
                logger: logger,
                git: git,
                telemetryManager: telemetryManager,
                notifier: _notifier);
        }

        public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
        {
            return _inner.ReviewAsync(path, content, isBaseline, cancellationToken);
        }

        public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, CancellationToken cancellationToken = default)
        {
            return _inner.DeltaAsync(review, currentCode, precomputedBaselineRawScore, cancellationToken);
        }

        public Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, CancellationToken cancellationToken = default)
        {
            return _inner.ReviewAndBaselineAsync(path, currentCode, cancellationToken);
        }

        public Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            return _inner.ReviewWithDeltaAsync(path, content, cancellationToken);
        }

        public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, CancellationToken cancellationToken = default)
        {
            return _inner.GetOrComputeBaselineRawScoreAsync(path, baselineContent, cancellationToken);
        }

        private void OnViewUpdateRequested(object sender, EventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await CodeSceneToolWindow.UpdateViewAsync();
            });
        }
    }
}
