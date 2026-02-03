// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeReviewer : ICodeReviewer
    {
        private readonly ILogger _logger;
        private readonly IModelMapper _mapper;
        private readonly ICliExecutor _executer;
        private readonly ITelemetryManager _telemetryManager;
        private readonly IGitService _git;

        [ImportingConstructor]
        public CodeReviewer(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executer,
            ITelemetryManager telemetryManager,
            IGitService git)
        {
            _logger = logger;
            _mapper = mapper;
            _executer = executer;
            _telemetryManager = telemetryManager;
            _git = git;
        }

        public FileReviewModel Review(string path, string content)
        {
            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(content))
            {
                _logger.Debug($"Skipping review for '{path}'. Missing content or file path.");
                return null;
            }

            var review = _executer.ReviewContent(fileName, content);
            return _mapper.Map(path, review);
        }

        public DeltaResponseModel Delta(FileReviewModel review, string currentCode)
        {
            var path = review.FilePath;
            var currentRawScore = review.RawScore ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.Warn($"Could not review file, missing file path.");
                return null;
            }

            try
            {
                var oldCode = _git.GetFileContentForCommit(path);
                var cache = new DeltaCacheService();

                // Skip delta if content is identical (no changes since baseline)
                if (oldCode == currentCode)
                {
                    _logger.Debug($"Delta analysis skipped for {Path.GetFileName(path)}: content unchanged since baseline.");

                    // Cache null delta to remove file from monitor if it was previously shown
                    cache.Put(new DeltaCacheEntry(path, oldCode, currentCode, null));
                    return null;
                }

                var entry = cache.Get(new DeltaCacheQuery(path, oldCode, currentCode));

                // If cache hit
                if (entry.Item1)
                {
                    return entry.Item2;
                }

                var oldCodeReview = Review(path, oldCode);
                var oldRawScore = oldCodeReview?.RawScore ?? string.Empty;

                // Skip delta if scores are identical (same code health, no meaningful change)
                if (oldRawScore == currentRawScore)
                {
                    _logger.Debug($"Delta analysis skipped for {Path.GetFileName(path)}: scores are identical.");

                    // Cache null delta to remove file from monitor if it was previously shown
                    cache.Put(new DeltaCacheEntry(path, oldCode, currentCode, null));
                    return null;
                }

                var delta = _executer.ReviewDelta(oldRawScore, currentRawScore);

                var cacheSnapshot = new Dictionary<string, DeltaResponseModel>(cache.GetAll());
                var cacheEntry = new DeltaCacheEntry(path, oldCode, currentCode, delta);
                cache.Put(cacheEntry);

                DeltaTelemetryHelper.HandleDeltaTelemetryEvent(cacheSnapshot, cache.GetAll(), cacheEntry, _telemetryManager);

                return delta;
            }
            catch (Exception e)
            {
                _logger.Error($"Could not perform delta analysis on file {path}", e);
                return null;
            }
        }
    }
}
