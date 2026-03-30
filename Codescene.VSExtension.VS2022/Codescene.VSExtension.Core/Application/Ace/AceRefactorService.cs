// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Application.Ace
{
    [Export(typeof(IAceRefactorService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceRefactorService : IAceRefactorService
    {
        private readonly IAceManager _aceManager;
        private readonly IPreflightManager _preflightManager;
        private readonly IModelMapper _mapper;
        private readonly ILogger _logger;
        private readonly AceRefactorableFunctionsCacheService _cache = new AceRefactorableFunctionsCacheService();

        [ImportingConstructor]
        public AceRefactorService(
            IAceManager aceManager,
            IPreflightManager preflightManager,
            IModelMapper mapper,
            ILogger logger)
        {
            _aceManager = aceManager;
            _preflightManager = preflightManager;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IList<FnToRefactorModel>> CheckContainsRefactorableFunctionsAsync(FileReviewModel result, string code, CancellationToken cancellationToken = default)
        {
            if (result == null)
            {
                return new List<FnToRefactorModel>();
            }

            return await CheckContainsRefactorableFunctionsForReviewAsync(result, code, cancellationToken);
        }

        public FnToRefactorModel GetRefactorableFunction(CodeSmellModel codeSmell, IList<FnToRefactorModel> refactorableFunctions)
        {
            return refactorableFunctions.FirstOrDefault(function =>
                function.RefactoringTargets.Any(target =>
                    target.Category == codeSmell.Category &&
                    target.Line == codeSmell.Range.StartLine));
        }

        public bool ShouldCheckRefactorableFunctions(string extension)
        {
            if (_preflightManager.IsSupportedLanguage(extension))
            {
                return true;
            }

            _logger.Debug($"Auto refactor is not supported for language: {extension}");
            return false;
        }

        private async Task<IList<FnToRefactorModel>> CheckContainsRefactorableFunctionsForReviewAsync(FileReviewModel result, string code, CancellationToken cancellationToken)
        {
            var operationGeneration = CacheGeneration.Current;
            await _preflightManager.GetPreflightResponseAsync(cancellationToken);

            var path = result.FilePath;
            var fileName = Path.GetFileName(path);

            _logger.Info($"Checking if refactorable functions available for file {path}");

            // Check fileName before proceeding
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.Warn($"Invalid file name for path: {path}");
                return new List<FnToRefactorModel>();
            }

            var extension = Path.GetExtension(fileName).Replace(".", string.Empty);

            if (!ShouldCheckRefactorableFunctions(extension))
            {
                return new List<FnToRefactorModel>();
            }

            var codeSmellModelList = (result.FunctionLevel ?? Enumerable.Empty<CodeSmellModel>()).Concat(result.FileLevel ?? Enumerable.Empty<CodeSmellModel>());
            var cliCodeSmellModelList = new List<CliCodeSmellModel>();

            foreach (var codeSmellModel in codeSmellModelList)
            {
                var cliCodeSmellModel = _mapper.Map(codeSmellModel);
                cliCodeSmellModelList.Add(cliCodeSmellModel);
            }

            var preflight = await _preflightManager.GetPreflightResponseAsync(cancellationToken);

            return await TryFetchAndCacheRefactorableFunctionsAsync(
                (fileName, code, path),
                cliCodeSmellModelList,
                preflight,
                operationGeneration,
                cancellationToken);
        }

        private async Task<IList<FnToRefactorModel>> TryFetchAndCacheRefactorableFunctionsAsync(
            (string FileName, string Code, string Path) file,
            List<CliCodeSmellModel> cliCodeSmellModelList,
            PreFlightResponseModel preflight,
            long operationGeneration,
            CancellationToken cancellationToken)
        {
            try
            {
                var refactorableFunctions = await _aceManager.GetRefactorableFunctionsFromCodeSmellsAsync(file.FileName, file.Code, cliCodeSmellModelList, preflight, cancellationToken);

                if (refactorableFunctions != null && refactorableFunctions.Any())
                {
                    _logger.Info($"Found {refactorableFunctions.Count} refactorable function(s) in path {file.Path}", true);
                    var cacheEntry = new AceRefactorableFunctionsEntry(file.Path, file.Code, refactorableFunctions);
                    _logger.Debug($"Caching refactorable functions for path: {file.Path}.");
                    _cache.Put(cacheEntry, operationGeneration);
                    return refactorableFunctions;
                }
                else
                {
                    _logger.Warn($"No refactorable functions found for path: {file.Path}");
                    return new List<FnToRefactorModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking refactorable functions for path: {file.Path}", ex);
                return new List<FnToRefactorModel>();
            }
        }
    }
}
