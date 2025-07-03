using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.Core.Models.WebComponent;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        public static CachedRefactoringActionModel LastRefactoring;

        public async Task<CachedRefactoringActionModel> Refactor(string path, string content, bool invalidateCache = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // TODO: meaningful log
                return null;
            }

            var cache = new ReviewCacheService();
            var review = cache.Get(new ReviewCacheQuery(content, path));

            var codeSmellModelList = review.FunctionLevel.Concat(review.FileLevel);

            var cliCodeSmellModelList = new List<CliCodeSmellModel>();
            foreach (var codeSmellModel in codeSmellModelList)
            {
                var cliCodeSmellModel = _mapper.Map(codeSmellModel);
                cliCodeSmellModelList.Add(cliCodeSmellModel);
            }

            var codesmellsJson = JsonConvert.SerializeObject(cliCodeSmellModelList);

            var preflight = JsonConvert.SerializeObject(_executer.Preflight());

            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                // TODO: meaningful log
                return null;
            }

            var extension = Path.GetExtension(fileName).Replace(".", "");
            IList<FnToRefactorModel> refactorableFunctions = await GetRefactorableFunctions(content, codesmellsJson, preflight, extension);
            
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

            LastRefactoring = cacheItem;

            return cacheItem;
        }

        public async Task<IList<FnToRefactorModel>> GetRefactorableFunctions(string content, string codesmellsJson, string preflight, string extension)
        {
            return await _executer.FnsToRefactorFromCodeSmellsAsync(content, extension, codesmellsJson, preflight);
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

            LastRefactoring = cacheItem;

            return refactoredFunctions;
        }

        public CachedRefactoringActionModel GetCachedRefactoredCode()
        {
            return LastRefactoring;
        }
    }
}