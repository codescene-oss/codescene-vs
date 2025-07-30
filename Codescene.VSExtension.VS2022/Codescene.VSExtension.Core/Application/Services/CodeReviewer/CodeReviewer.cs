using Codescene.VSExtension.Core.Application.Services.Cache.Review;
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
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Newtonsoft.Json;
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
                _logger.Debug($"Skipping review for '{path}'. Missing content or file path.");
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

            _logger.Debug($"Updating delta cache with refactorable functions for {path}. Found {refactorableFunctions.Count} refactorable functions.");
            _logger.Debug($"Delta response: {JsonConvert.SerializeObject(delta) ?? "null"}");
            _logger.Debug($"Refactorable functions: {JsonConvert.SerializeObject(refactorableFunctions)}");


            if (delta == null)
            {
                _logger.Debug("Delta response null. Skipping update of delta cache.");
                return;
            }
            if (!refactorableFunctions.Any())
            {
                _logger.Debug("No refactorable functions found. Skipping update of delta cache.");
                return;
            }

            foreach (var finding in delta.FunctionLevelFindings)
            {
                var functionName = finding.Function?.Name;
                if (string.IsNullOrEmpty(functionName))
                    continue;

                // update only if not already updated, for case when multiple methods have same name
                if (finding.RefactorableFn == null) 
                {
                    var match = refactorableFunctions.FirstOrDefault(fn => fn.Name == functionName && checkRange(finding, fn));
                    if (match != null)
                    {
                        finding.RefactorableFn = match;
                    }
                }
            }
        }

        private bool checkRange(FunctionFindingModel finding, FnToRefactorModel refFunction)
        {
            // this check is because of ComplexConditional code smell which is inside of the method
            return refFunction.Range.Startline <= finding.Function.Range.Startline &&
                finding.Function.Range.Startline <= refFunction.Range.EndLine;
        }
    }
}
