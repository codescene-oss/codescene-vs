﻿using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Git;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeReviewer : ICodeReviewer
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly ICliExecutor _executer;

        [Import]
        private readonly ITelemetryManager _telemetryManager;

        [Import]
        private readonly IGitService _git;

        public FileReviewModel Review(string path, string content)
        {
            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(content))
            {
                _logger.Warn($"Could not review path {path}. Missing content or file path.");
                return null;
            }

            var review = _executer.ReviewContent(fileName, content);

            return _mapper.Map(path, review); ;
        }

        public DeltaResponseModel Delta(FileReviewModel review, string currentCode)
        {
            var path = review.FilePath;
            var currentRawScore = review.RawScore ?? "";

            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.Warn($"Could not review file, missing file path.");
                return null;
            }

            try
            {
                var oldCode = _git.GetFileContentForCommit(path);
                var cache = new DeltaCacheService();
                var entry = cache.Get(new DeltaCacheQuery(path, oldCode, currentCode));

                // If cache hit
                if (entry.Item1) return entry.Item2;

                var oldCodeReview = Review(path, oldCode);
                var oldRawScore = oldCodeReview?.RawScore ?? "";

                var delta = _executer.ReviewDelta(oldRawScore, currentRawScore);
                UpdateDeltaCacheWithRefactorableFunctions(delta, path, currentCode);

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

        private void UpdateDeltaCacheWithRefactorableFunctions(DeltaResponseModel delta, string path, string code)
        {
            AceRefactorableFunctionsCacheService cacheService = new AceRefactorableFunctionsCacheService();
            var refactorableFunctions = cacheService.Get(new AceRefactorableFunctionsQuery(path, code));


            if (delta == null || !refactorableFunctions.Any())
            {
                _logger.Debug("Delta response null or refactorable functions list is empty. Skipping update.");
                return;
            }

            foreach (var finding in delta.FunctionLevelFindings)
            {
                var functionName = finding.Function?.Name;
                if (string.IsNullOrEmpty(functionName))
                    continue;

                var match = refactorableFunctions.FirstOrDefault(fn => fn.Name == functionName);
                if (match != null)
                {
                    finding.RefactorableFn = match;
                }
            }
        }
    }
}
