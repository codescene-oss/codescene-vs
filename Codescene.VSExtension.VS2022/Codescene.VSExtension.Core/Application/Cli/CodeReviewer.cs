// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public class CodeReviewer : ICodeReviewer
    {
        private readonly ILogger _logger;
        private readonly IModelMapper _mapper;
        private readonly ICliExecutor _executor;
        private readonly IPreflightManager _preflightManager;
        private readonly IReviewPipeline _pipeline;
        private readonly ISettingsProvider _settings;

        public CodeReviewer(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executor,
            IReviewPipeline pipeline,
            IPreflightManager preflightManager = null,
            ISettingsProvider settings = null)
        {
            _logger = logger;
            _mapper = mapper;
            _executor = executor;
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _preflightManager = preflightManager;
            _settings = settings;
        }

        public Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            return ReviewWithPipelineAsync(path, content, cancellationToken);
        }

        private async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithPipelineAsync(string path, string content, CancellationToken cancellationToken)
        {
            var repoRoot = GitPathDiscovery.TryGetWorkingDirectory(path);
            if (string.IsNullOrEmpty(repoRoot))
            {
                _logger.Debug($"Skipping pipeline review for '{path}'. Repository root was not found.");
                return (null, null);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var document = new ReviewDocument { FilePath = path, Content = content, IsDirty = true };
            var (cliReview, delta) = await _pipeline.SubmitAsync(
                repoRoot,
                new ReviewSubmission
                {
                    Document = document,
                    RelPath = RpcPath.RelativePosix(repoRoot, path),
                    Content = content,
                    UpdateDiagnosticsPane = true,
                    UpdateMonitor = ShouldUpdateMonitor(path, content),
                });

            if (!BufferDiffersFromDisk(path, content))
            {
                _pipeline.RestoreFromDiskIfBufferOwned(repoRoot, document);
            }

            if (cliReview == null)
            {
                return (null, null);
            }

            var review = _mapper.Map(path, cliReview);
            if (review?.RawScore == null)
            {
                return (review, null);
            }

            var enriched = await EnrichDeltaAsync(path, content, delta, cancellationToken);
            return (review, enriched);
        }

        private bool ShouldUpdateMonitor(string path, string content)
        {
            if (!(_settings?.EnableAutomaticAnalysis ?? true))
            {
                return false;
            }

            return BufferDiffersFromDisk(path, content);
        }

        private bool BufferDiffersFromDisk(string path, string content)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return true;
            }

            try
            {
                return !string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return true;
            }
        }

        private async Task<DeltaResponseModel> EnrichDeltaAsync(string path, string currentCode, DeltaResponseModel delta, CancellationToken cancellationToken)
        {
            if (_preflightManager == null || delta == null)
            {
                return delta;
            }

            var preflight = await _preflightManager.GetPreflightResponseAsync(cancellationToken);
            var refactorableFunctions = await _executor.FnsToRefactorFromDeltaAsync(path, currentCode, delta, preflight, cancellationToken);
            if (refactorableFunctions is not { Count: > 0 })
            {
                return delta;
            }

            foreach (var refactorableFunction in refactorableFunctions)
            {
                var function = delta.FunctionLevelFindings?.FirstOrDefault(x => x.Function.Name == refactorableFunction.Name);
                if (function != null)
                {
                    function.RefactorableFn = refactorableFunction;
                }
            }

            return delta;
        }
    }
}
