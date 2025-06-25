using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
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

namespace Codescene.VSExtension.Core.Application.Services.AceManager
{
    [Export(typeof(IAceManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceManager : IAceManager
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly ICliExecuter _executer;

        public async Task<CachedRefactoringActionModel> Refactor(string path, string content, bool invalidateCache = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // TODO: meaningful log
                return null;
            }

            var cache = new ReviewCacheService();
            var review = cache.Get(new ReviewCacheQuery(content, path));

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

            //_cache.Add(cacheItem); Use new cache impl, but also, should we cache this? ACE already has cache on the API side. We don't cache it in JB.

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

            //_cache.Add(cacheItem);

            return refactoredFunctions;
        }

        public CachedRefactoringActionModel GetCachedRefactoredCode()
        {
            return null;
        }
    }
}