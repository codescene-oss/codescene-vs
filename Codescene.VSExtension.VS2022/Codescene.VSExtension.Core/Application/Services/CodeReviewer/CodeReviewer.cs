using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent;
using Newtonsoft.Json;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeReviewer : ICodeReviewer
    {
        private string _content = string.Empty;

        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly ICliExecuter _executer;

        [Import]
        private readonly IReviewedFilesCacheHandler _cache;

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

        public async Task<CachedRefactoringActionModel> Refactor(string path, string content, bool invalidateCache = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // TODO: meaningful log
                return null;
            }

            var review = Review(path, content);

            // JsonConvert.SerializeObject(review.FunctionLevelCodeSmells[0].CodeSmells);
            var codesmellsJson = "{}"; // fix

            var preflight = JsonConvert.SerializeObject(_executer.Preflight());

            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                // TODO: meaningful log
                return null;
            }

            var extension = Path.GetExtension(fileName).Replace(".", "");

            var refactorableFunctions = await _executer.FnsToRefactorFromCodeSmellsAsync(content, extension, codesmellsJson, preflight);

            var f = refactorableFunctions.First();

            //Fix for csharp ACE api
            if (string.IsNullOrWhiteSpace(f.FunctionType))
            {
                f.FunctionType = "MemberFn";
            }

            var refactorableFunctionsString = JsonConvert.SerializeObject(f);

            var refactoredFunctions = await _executer.PostRefactoring(fnToRefactor: refactorableFunctionsString, skipCache: true);

            if (refactoredFunctions == null)
            {
                throw new Exception("Refactoring has failed!");
            }

            var cacheItem = new CachedRefactoringActionModel
            {
                Path = path,
                RefactorableCandidate = f,
                Refactored = refactoredFunctions
            };

            _cache.Add(cacheItem);

            return cacheItem;
        }

        public async Task<RefactorResponseModel> Refactor(string path, FnToRefactorModel refactorableFunction, bool invalidateCache = false)
        {
            if (string.IsNullOrWhiteSpace(refactorableFunction.FunctionType))
            {
                refactorableFunction.FunctionType = "MemberFn";
            }

            var refactorableFunctionsString = JsonConvert.SerializeObject(refactorableFunction);

            var refactoredFunctions = await _executer.PostRefactoring(fnToRefactor: refactorableFunctionsString, skipCache: true);

            if (refactoredFunctions == null)
            {
                throw new Exception("Refactoring has failed!");
            }

            var cacheItem = new CachedRefactoringActionModel
            {
                Path = path,
                RefactorableCandidate = refactorableFunction,
                Refactored = refactoredFunctions
            };

            _cache.Add(cacheItem);

            return refactoredFunctions;
        }

        public CachedRefactoringActionModel GetCachedRefactoredCode()
        {
            return _cache.GetRefactored();
        }
    }
}
