// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Text;

namespace Codescene.VSExtension.VS2022.Util
{
    public static class AceUtils
    {
        /// <summary>
        /// Checks if a file contains refactorable functions.
        /// </summary>
        public static async Task<IList<FnToRefactorModel>> CheckContainsRefactorableFunctionsAsync(FileReviewModel result, string code)
        {
            var aceRefactorService = await VS.GetMefServiceAsync<IAceRefactorService>();
            return aceRefactorService.CheckContainsRefactorableFunctions(result, code);
        }

        /// <summary>
        /// Finds the refactorable function matching a code smell.
        /// </summary>
        public static FnToRefactorModel GetRefactorableFunction(CodeSmellModel codeSmell, IList<FnToRefactorModel> refactorableFunctions)
        {
            return refactorableFunctions.FirstOrDefault(function =>
                function.RefactoringTargets.Any(target =>
                    target.Category == codeSmell.Category &&
                    target.Line == codeSmell.Range.StartLine));
        }

        public static async Task<FnToRefactorModel> GetRefactorableFunctionDeltaAsync(GetRefactorableFunctionsModel model)
        {
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();
            var preflight = preflightManager.GetPreflightResponse();

            if (model.FunctionRange == null)
            {
                return null;
            }

            var fileContent = await GetFileContentAsync(model);

            var deltaCache = new DeltaCacheService();
            var cache = deltaCache.GetAll();

            if (cache.TryGetValue(model.Path, out var delta) && delta != null)
            {
                var refactorableFunctions = aceManager.GetRefactorableFunctionsFromDelta(model.Path, fileContent, delta, preflight);
                return refactorableFunctions?.FirstOrDefault();
            }

            return null;
        }

        public static async Task<FnToRefactorModel> GetRefactorableFunctionCodeSmellAsync(GetRefactorableFunctionsModel model)
        {
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();
            var preflight = preflightManager.GetPreflightResponse();

            if (model.FunctionRange == null)
            {
                return null;
            }

            var codeSmell = new CliCodeSmellModel()
            {
                Details = model.Details,
                Category = model.Category,
                Range = new Core.Models.Cli.CliRangeModel()
                {
                    StartColumn = model.FunctionRange.StartColumn,
                    EndColumn = model.FunctionRange.EndColumn,
                    StartLine = model.FunctionRange.StartLine,
                    EndLine = model.FunctionRange.EndLine,
                },
            };

            var fileContent = await GetFileContentAsync(model);

            var refactorableFunctions = aceManager.GetRefactorableFunctionsFromCodeSmells(model.Path, fileContent, new List<CliCodeSmellModel> { codeSmell }, preflight);
            return refactorableFunctions?.FirstOrDefault();
        }

        public static async Task UpdateDeltaCacheWithRefactorableFunctionsAsync(DeltaResponseModel delta, string path, string code, ILogger logger)
        {
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var fileName = Path.GetFileName(path);
            var preflight = preflightManager.GetPreflightResponse();

            logger.Info($"Checking if refactorable functions from delta available for file {path}");
            if (ShouldSkipUpdate(path, fileName, delta, logger))
            {
                return;
            }

            var refactorableFunctions = aceManager.GetRefactorableFunctionsFromDelta(fileName, code, delta, preflight);
            if (refactorableFunctions == null || !refactorableFunctions.Any())
            {
                logger.Debug("No refactorable functions found. Skipping update of delta cache.");
                return;
            }

            logger.Debug($"Updating delta cache with refactorable functions for {path}. Found {refactorableFunctions.Count} refactorable functions.");
            UpdateFindings(delta, refactorableFunctions);
        }

        public static bool ShouldSkipUpdate(string path, string fileName, DeltaResponseModel delta, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                logger.Warn($"Invalid file name for path: {path}");
                return true;
            }

            if (delta == null)
            {
                logger.Debug("Delta response null. Skipping update of delta cache.");
                return true;
            }

            return false;
        }

        public static void UpdateFindings(DeltaResponseModel delta, IList<FnToRefactorModel> refactorableFunctions)
        {
            if (delta?.FunctionLevelFindings == null)
            {
                return;
            }

            foreach (var finding in delta.FunctionLevelFindings)
            {
                var functionName = finding.Function?.Name;
                if (string.IsNullOrEmpty(functionName))
                {
                    continue;
                }

                UpdateFindingIfNotUpdated(finding, functionName, refactorableFunctions);
            }
        }

        public static void UpdateFindingIfNotUpdated(FunctionFindingModel finding, string functionName, IList<FnToRefactorModel> refactorableFunctions)
        {
            if (finding.RefactorableFn == null)
            {
                var match = refactorableFunctions.FirstOrDefault(fn => fn.Name == functionName && CheckRange(finding, fn));
                if (match != null)
                {
                    finding.RefactorableFn = match;
                }
            }
        }

        public static bool CheckRange(FunctionFindingModel finding, FnToRefactorModel refFunction)
        {
            var findingRange = finding.Function?.Range;
            var refactorRange = refFunction?.Range;

            if (findingRange == null || refactorRange == null)
            {
                return false;
            }

            return refactorRange.StartLine <= findingRange.StartLine && findingRange.StartLine <= refactorRange.EndLine;
        }

        private static async Task<string> GetFileContentAsync(GetRefactorableFunctionsModel model)
        {
            var fileContent = string.Empty;
            var docView = await VS.Documents.OpenAsync(model.Path);
            if (docView?.TextBuffer is ITextBuffer buffer)
            {
                fileContent = buffer.CurrentSnapshot.GetText();
            }

            return fileContent;
        }
    }
}
