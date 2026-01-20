using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.ReviewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.AceManager
{
    [Export(typeof(IAceRefactorService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceRefactorService : IAceRefactorService
    {
        private readonly IAceManager _aceManager;
        private readonly IPreflightManager _preflightManager;
        private readonly IModelMapper _mapper;
        private readonly ILogger _logger;
        private readonly AceRefactorableFunctionsCacheService _cache = new();

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

        public IList<FnToRefactorModel> CheckContainsRefactorableFunctions(FileReviewModel result, string code)
        {
            _preflightManager.GetPreflightResponse();

            var path = result.FilePath;
            var fileName = Path.GetFileName(path);

            _logger.Info($"Checking if refactorable functions available for file {path}");

            // Check fileName before proceeding
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.Warn($"Invalid file name for path: {path}");
                return new List<FnToRefactorModel>();
            }

            var extension = Path.GetExtension(fileName).Replace(".", "");

            if (!ShouldCheckRefactorableFunctions(extension))
            {
                return new List<FnToRefactorModel>();
            }

            var codeSmellModelList = result.FunctionLevel.Concat(result.FileLevel);
            var cliCodeSmellModelList = new List<CliCodeSmellModel>();

            foreach (var codeSmellModel in codeSmellModelList)
            {
                var cliCodeSmellModel = _mapper.Map(codeSmellModel);
                cliCodeSmellModelList.Add(cliCodeSmellModel);
            }

            var preflight = _preflightManager.GetPreflightResponse();

            try
            {
                var refactorableFunctions = _aceManager.GetRefactorableFunctions(fileName, code, cliCodeSmellModelList, preflight);

                if (refactorableFunctions.Any())
                {
                    _logger.Info($"Found {refactorableFunctions.Count} refactorable function(s) in path {path}");

                    var cacheEntry = new AceRefactorableFunctionsEntry(path, code, refactorableFunctions);
                    _logger.Debug($"Caching refactorable functions for path: {path}.");
                    _cache.Put(cacheEntry);
                    return refactorableFunctions;
                }
                else
                {
                    _logger.Warn($"No refactorable functions found for path: {path}");
                    return new List<FnToRefactorModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking refactorable functions for path: {path}", ex);
                return new List<FnToRefactorModel>();
            }
        }

        public FnToRefactorModel GetRefactorableFunction(CodeSmellModel codeSmell, IList<FnToRefactorModel> refactorableFunctions)
        {
            return refactorableFunctions.FirstOrDefault(function =>
                function.RefactoringTargets.Any(target =>
                    target.Category == codeSmell.Category &&
                    target.Line == codeSmell.Range.StartLine
                )
            );
        }

        public bool ShouldCheckRefactorableFunctions(string extension)
        {
            if (_preflightManager.IsSupportedLanguage(extension) == false)
            {
                _logger.Debug($"Auto refactor is not supported for language: {extension}");
                return false;
            }
            return true;
        }
    }
}
